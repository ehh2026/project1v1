using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Windows.Media.Imaging;

namespace InteractiveWorldMap.Services;

/// <summary>
/// Caches pre-rendered animation frames to disk for faster subsequent animations.
/// </summary>
public class AnimationFrameCache
{
    private readonly string _cacheDirectory;
    private readonly ILogger _logger;
    // Increment when interpolation geometry or pixel-resampling policy changes.
    private const int CacheVersion = 16;

    public AnimationFrameCache(ILogger logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        
        // Store cache in AppData
        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        _cacheDirectory = Path.Combine(appDataPath, "InteractiveWorldMap", "frame_cache");
        
        Directory.CreateDirectory(_cacheDirectory);
        
        // Check cache version and clear if outdated
        ValidateCacheVersion();
    }

    /// <summary>
    /// Validates the cache version and clears if outdated.
    /// </summary>
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
                    _logger.LogInfo($"Cache version mismatch (stored: {storedVersion}, current: {CacheVersion}). Clearing old cache files.");
                    CleanupOldCacheFiles();
                }
            }
            else
            {
                // First run with versioning - clean up any old unversioned cache
                _logger.LogInfo("First run with cache versioning. Cleaning up old cache files.");
                CleanupOldCacheFiles();
            }
            
            // Write current version
            File.WriteAllText(versionFile, CacheVersion.ToString());
        }
        catch (Exception ex)
        {
            _logger.LogWarning($"Failed to validate cache version: {ex.Message}");
        }
    }

    /// <summary>
    /// Removes old cache files that don't match the current version.
    /// </summary>
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
                catch
                {
                    // Ignore individual file deletion errors
                }
            }

            _logger.LogInfo($"Cleaned up {deletedCount} old cache files");
        }
        catch (Exception ex)
        {
            _logger.LogWarning($"Failed to cleanup old cache files: {ex.Message}");
        }
    }

    /// <summary>
    /// Generates a cache key based on animation parameters.
    /// Includes cache version to auto-invalidate when algorithm changes.
    /// </summary>
    private string GetCacheKey(double startX, double startY, double startW, double startH,
                               double endX, double endY, double endW, double endH,
                               int displayWidth, int displayHeight, int frameIndex)
    {
        var key = $"v{CacheVersion}_{startX:F1}_{startY:F1}_{startW:F1}_{startH:F1}_" +
                  $"{endX:F1}_{endY:F1}_{endW:F1}_{endH:F1}_" +
                  $"{displayWidth}x{displayHeight}_f{frameIndex}";
        
        // Hash to keep filename reasonable
        using var sha = SHA256.Create();
        var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(key));
        return Convert.ToHexString(hash)[..16]; // First 16 chars
    }

    /// <summary>
    /// Tries to load a cached frame from disk.
    /// </summary>
    public BitmapSource? TryLoadFrame(double startX, double startY, double startW, double startH,
                                      double endX, double endY, double endW, double endH,
                                      int displayWidth, int displayHeight, int frameIndex)
    {
        try
        {
            var cacheKey = GetCacheKey(startX, startY, startW, startH, endX, endY, endW, endH,
                                       displayWidth, displayHeight, frameIndex);
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
            _logger.LogWarning($"Failed to load cached frame: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Saves a frame to disk cache.
    /// </summary>
    public void SaveFrame(BitmapSource frame, double startX, double startY, double startW, double startH,
                         double endX, double endY, double endW, double endH,
                         int displayWidth, int displayHeight, int frameIndex)
    {
        try
        {
            var cacheKey = GetCacheKey(startX, startY, startW, startH, endX, endY, endW, endH,
                                       displayWidth, displayHeight, frameIndex);
            var cachePath = Path.Combine(_cacheDirectory, $"{cacheKey}.png");

            using var fileStream = new FileStream(cachePath, FileMode.Create);
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(frame));
            encoder.Save(fileStream);
        }
        catch (Exception ex)
        {
            _logger.LogWarning($"Failed to save frame to cache: {ex.Message}");
        }
    }

    /// <summary>
    /// Clears all cached frames.
    /// </summary>
    public void ClearCache()
    {
        try
        {
            if (Directory.Exists(_cacheDirectory))
            {
                Directory.Delete(_cacheDirectory, true);
                Directory.CreateDirectory(_cacheDirectory);
                _logger.LogInfo("Animation frame cache cleared");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError($"Failed to clear cache: {ex.Message}");
        }
    }
}
