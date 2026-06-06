using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace InteractiveWorldMap.Services;

/// <summary>
/// Caches high-quality zoomed region images for final zoomed state.
/// Extracts regions from the full-resolution source image on-demand for best quality.
/// </summary>
public class ZoomedRegionCache
{
    private readonly string _cacheDirectory;
    private readonly ILogger _logger;
    private readonly string _fullResolutionImagePath;
    private const int CacheVersion = 7;

    public ZoomedRegionCache(ILogger logger, string fullResolutionImagePath)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _fullResolutionImagePath = fullResolutionImagePath;
        
        // Store cache in AppData
        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        _cacheDirectory = Path.Combine(appDataPath, "InteractiveWorldMap", "zoomed_regions");
        
        Directory.CreateDirectory(_cacheDirectory);
        ValidateCacheVersion();
    }

    private void ValidateCacheVersion()
    {
        var versionFile = Path.Combine(_cacheDirectory, "cache_version.txt");
        
        try
        {
            if (File.Exists(versionFile))
            {
                var storedVersion = int.Parse(File.ReadAllText(versionFile));
                if (storedVersion != CacheVersion)
                {
                    _logger.LogInfo($"Zoomed region cache version mismatch (stored: {storedVersion}, current: {CacheVersion}). Clearing old cache.");
                    CleanupOldCacheFiles();
                }
            }
            
            File.WriteAllText(versionFile, CacheVersion.ToString());
        }
        catch (Exception ex)
        {
            _logger.LogWarning($"Failed to validate zoomed region cache version: {ex.Message}");
        }
    }

    private void CleanupOldCacheFiles()
    {
        try
        {
            if (!Directory.Exists(_cacheDirectory))
                return;

            var files = Directory.GetFiles(_cacheDirectory, "*.png");
            int deletedCount = 0;

            foreach (var file in files)
            {
                try
                {
                    File.Delete(file);
                    deletedCount++;
                }
                catch { }
            }

            if (deletedCount > 0)
                _logger.LogInfo($"Cleaned up {deletedCount} old zoomed region cache files");
        }
        catch (Exception ex)
        {
            _logger.LogWarning($"Failed to cleanup old zoomed region cache: {ex.Message}");
        }
    }

    private string GetCacheKey(double centerX, double centerY, double zoomLevel, int displayWidth, int displayHeight)
    {
        var key = $"v{CacheVersion}_center_{centerX:F1}_{centerY:F1}_zoom_{zoomLevel:F1}_{displayWidth}x{displayHeight}";
        
        using var sha = SHA256.Create();
        var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(key));
        return Convert.ToHexString(hash)[..16];
    }

    /// <summary>
    /// Tries to load a cached high-quality zoomed region.
    /// </summary>
    public BitmapSource? TryLoadRegion(double centerX, double centerY, double zoomLevel, int displayWidth, int displayHeight)
    {
        try
        {
            var cacheKey = GetCacheKey(centerX, centerY, zoomLevel, displayWidth, displayHeight);
            var cachePath = Path.Combine(_cacheDirectory, $"{cacheKey}.png");

            if (!File.Exists(cachePath))
                return null;

            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.UriSource = new Uri(cachePath);
            bitmap.EndInit();
            bitmap.Freeze();

            return bitmap;
        }
        catch (Exception ex)
        {
            _logger.LogWarning($"Failed to load cached zoomed region: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Generates and caches a high-quality zoomed region.
    /// Loads full-resolution source on-demand, extracts region, applies high-quality interpolation, then releases.
    /// </summary>
    public BitmapSource GenerateAndCacheRegion(BitmapSource halfResSource, System.Windows.Int32Rect halfResRect,
                                                double centerX, double centerY, double zoomLevel,
                                                int displayWidth, int displayHeight)
    {
        try
        {
            BitmapSource finalImage;
            
            // Check if full-resolution source exists
            if (File.Exists(_fullResolutionImagePath))
            {
                _logger.LogInfo($"  Loading full-res image on-demand: {_fullResolutionImagePath}");
                
                // Load full-resolution image (only when needed, not kept in memory)
                var fullResBitmap = new BitmapImage();
                fullResBitmap.BeginInit();
                fullResBitmap.CacheOption = BitmapCacheOption.OnLoad;
                fullResBitmap.UriSource = new Uri(_fullResolutionImagePath);
                fullResBitmap.EndInit();
                fullResBitmap.Freeze();
                
                _logger.LogInfo($"  Full-res loaded: {fullResBitmap.PixelWidth}x{fullResBitmap.PixelHeight}");
                
                var scaleX = fullResBitmap.PixelWidth / (double)halfResSource.PixelWidth;
                var scaleY = fullResBitmap.PixelHeight / (double)halfResSource.PixelHeight;

                _logger.LogInfo($"  Full-res scale factors: x={scaleX:F4}, y={scaleY:F4}");

                var fullResRect = new System.Windows.Int32Rect(
                    (int)Math.Round(halfResRect.X * scaleX),
                    (int)Math.Round(halfResRect.Y * scaleY),
                    Math.Max(1, (int)Math.Round(halfResRect.Width * scaleX)),
                    Math.Max(1, (int)Math.Round(halfResRect.Height * scaleY)));
                
                // Clamp to full-res image bounds
                fullResRect.X = Math.Max(0, Math.Min(fullResRect.X, fullResBitmap.PixelWidth - 1));
                fullResRect.Y = Math.Max(0, Math.Min(fullResRect.Y, fullResBitmap.PixelHeight - 1));
                fullResRect.Width = Math.Min(fullResRect.Width, fullResBitmap.PixelWidth - fullResRect.X);
                fullResRect.Height = Math.Min(fullResRect.Height, fullResBitmap.PixelHeight - fullResRect.Y);
                
                _logger.LogInfo($"  Extracting {fullResRect.Width}x{fullResRect.Height} region from full-res");
                
                finalImage = ScaleBitmap(fullResBitmap, fullResRect, displayWidth, displayHeight);
                
                // Full-res bitmap will be garbage collected after this method
                _logger.LogInfo("  High-quality region generated from full-res source");
            }
            else
            {
                // Fallback: upsample from half-res with high-quality interpolation
                _logger.LogWarning($"  Full-res source not found at: {_fullResolutionImagePath}");
                _logger.LogInfo("  Upsampling from half-res with Fant interpolation");
                
                finalImage = ScaleBitmap(halfResSource, halfResRect, displayWidth, displayHeight);
            }

            // Save to cache
            var cacheKey = GetCacheKey(centerX, centerY, zoomLevel, displayWidth, displayHeight);
            var cachePath = Path.Combine(_cacheDirectory, $"{cacheKey}.png");

            using var fileStream = new FileStream(cachePath, FileMode.Create);
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(finalImage));
            encoder.Save(fileStream);
            
            _logger.LogInfo("  High-quality region cached to disk");

            return finalImage;
        }
        catch (Exception ex)
        {
            _logger.LogError($"Failed to generate and cache zoomed region: {ex.Message}");
            
            // Fallback: return a basic scaled version from half-res
            return ScaleBitmap(halfResSource, halfResRect, displayWidth, displayHeight);
        }
    }

    private static BitmapSource ScaleBitmap(BitmapSource source, System.Windows.Int32Rect sourceRect, int displayWidth, int displayHeight)
    {
        var croppedBitmap = new CroppedBitmap(source, sourceRect);
        var scaledBitmap = new TransformedBitmap(croppedBitmap,
            new ScaleTransform(displayWidth / (double)sourceRect.Width, displayHeight / (double)sourceRect.Height));

        RenderOptions.SetBitmapScalingMode(scaledBitmap, BitmapScalingMode.Fant);

        var writeableBitmap = new WriteableBitmap(scaledBitmap);
        writeableBitmap.Freeze();
        return writeableBitmap;
    }

    /// <summary>
    /// Clears all cached zoomed regions.
    /// </summary>
    public void ClearCache()
    {
        try
        {
            if (Directory.Exists(_cacheDirectory))
            {
                Directory.Delete(_cacheDirectory, true);
                Directory.CreateDirectory(_cacheDirectory);
                _logger.LogInfo("Zoomed region cache cleared");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError($"Failed to clear zoomed region cache: {ex.Message}");
        }
    }
}
