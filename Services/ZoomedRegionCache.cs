using System;
using System.IO;
using System.Windows.Media.Imaging;
using InteractiveWorldMap.Models;

namespace InteractiveWorldMap.Services;

public class ZoomedRegionCache
{
    private static readonly object VersionLock = new();
    private readonly string _cacheDirectory;
    private readonly ILogger _logger;
    private readonly string _fullPath;
    private readonly string _fallbackPath;
    private readonly IZoomedMapResampler _resampler;
    private readonly ZoomedRegionCacheKeyBuilder _keys = new();
    private bool _fullUnavailable;

    public ZoomedRegionCache(ILogger logger, string fullResolutionImagePath)
        : this(logger, fullResolutionImagePath, fullResolutionImagePath) { }

    public ZoomedRegionCache(ILogger logger, string fullResolutionImagePath,
        string fallbackImagePath, IZoomedMapResampler? resampler = null, string? cacheDirectory = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _fullPath = fullResolutionImagePath;
        _fallbackPath = fallbackImagePath;
        _resampler = resampler ?? new ZoomedMapResampler();
        _cacheDirectory = cacheDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "InteractiveWorldMap", "zoomed_regions");
        Directory.CreateDirectory(_cacheDirectory);
        ValidateVersion();
    }

    private void ValidateVersion()
    {
        var path = Path.Combine(_cacheDirectory, "cache_version.txt");
        lock (VersionLock)
        {
            try
            {
                if (File.Exists(path) &&
                    File.ReadAllText(path) != ZoomedRegionCacheKeyBuilder.CacheSchemaVersion.ToString())
                    foreach (var file in Directory.GetFiles(_cacheDirectory, "*.png"))
                        try { File.Delete(file); } catch { }
                File.WriteAllText(path, ZoomedRegionCacheKeyBuilder.CacheSchemaVersion.ToString());
            }
            catch (Exception ex) { _logger.LogWarning($"Failed to validate zoomed region cache: {ex.Message}"); }
        }
    }

    private (string Role, string Path) SelectSource() =>
        !_fullUnavailable && File.Exists(_fullPath)
            ? ("full-resolution", _fullPath)
            : ("fallback", _fallbackPath);

    private string CachePath(ZoomedRegionRenderRequest request,
        ZoomedMapResamplingMode? actualMode = null)
    {
        var selected = SelectSource();
        var fingerprint = _keys.Fingerprint(selected.Role, selected.Path);
        return Path.Combine(_cacheDirectory, _keys.Build(request, fingerprint, actualMode) + ".png");
    }

    public BitmapSource? TryLoadRegion(ZoomedRegionRenderRequest request)
    {
        var path = CachePath(request);
        if (!File.Exists(path)) return null;
        try
        {
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.UriSource = new Uri(path);
            bitmap.EndInit();
            bitmap.Freeze();
            return bitmap;
        }
        catch (Exception ex)
        {
            _logger.LogWarning($"Cached zoomed region is invalid; regenerating: {ex.Message}");
            try { File.Delete(path); } catch { }
            return null;
        }
    }

    public BitmapSource GenerateAndCacheRegion(BitmapSource halfResSource,
        ZoomedRegionRenderRequest request)
    {
        BitmapSource source = halfResSource;
        var rect = request.HalfResSourceRect;
        if (!_fullUnavailable && File.Exists(_fullPath))
        {
            try
            {
                var full = new BitmapImage();
                full.BeginInit();
                full.CacheOption = BitmapCacheOption.OnLoad;
                full.UriSource = new Uri(_fullPath);
                full.EndInit();
                full.Freeze();
                var sx = full.PixelWidth / (double)halfResSource.PixelWidth;
                var sy = full.PixelHeight / (double)halfResSource.PixelHeight;
                rect = new System.Windows.Int32Rect(
                    Math.Max(0, (int)Math.Round(rect.X * sx)),
                    Math.Max(0, (int)Math.Round(rect.Y * sy)),
                    Math.Max(1, (int)Math.Round(rect.Width * sx)),
                    Math.Max(1, (int)Math.Round(rect.Height * sy)));
                rect.Width = Math.Min(rect.Width, full.PixelWidth - rect.X);
                rect.Height = Math.Min(rect.Height, full.PixelHeight - rect.Y);
                source = full;
            }
            catch (Exception ex)
            {
                _fullUnavailable = true;
                _logger.LogWarning($"Full-resolution map unavailable; using fallback: {ex.Message}");
                source = halfResSource;
                rect = request.HalfResSourceRect;
            }
        }

        var crop = new CroppedBitmap(source, rect);
        BitmapSource result;
        var actualMode = request.ResamplingMode;
        try
        {
            result = _resampler.Resize(crop, request.PixelWidth, request.PixelHeight, actualMode);
        }
        catch (Exception ex) when (actualMode != ZoomedMapResamplingMode.Fant)
        {
            _logger.LogWarning($"{actualMode} zoomed-map resampling failed; using Fant: {ex.Message}");
            actualMode = ZoomedMapResamplingMode.Fant;
            result = _resampler.Resize(crop, request.PixelWidth, request.PixelHeight, actualMode);
        }

        var path = CachePath(request, actualMode);
        try
        {
            using var stream = new FileStream(path, FileMode.Create);
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(result));
            encoder.Save(stream);
        }
        catch (Exception ex) { _logger.LogWarning($"Failed to cache zoomed region: {ex.Message}"); }
        return result;
    }

    public BitmapSource? TryLoadRegion(double centerX, double centerY, double zoomLevel,
        int displayWidth, int displayHeight) =>
        TryLoadRegion(new(centerX, centerY, zoomLevel, displayWidth, displayHeight, 1, 1,
            ZoomedMapResamplingMode.Fant, new System.Windows.Int32Rect(0, 0, 1, 1)));

    public BitmapSource GenerateAndCacheRegion(BitmapSource source, System.Windows.Int32Rect rect,
        double centerX, double centerY, double zoomLevel, int displayWidth, int displayHeight) =>
        GenerateAndCacheRegion(source, new(centerX, centerY, zoomLevel, displayWidth, displayHeight,
            1, 1, ZoomedMapResamplingMode.Fant, rect));

    public void ClearCache()
    {
        try
        {
            foreach (var file in Directory.GetFiles(_cacheDirectory, "*.png")) File.Delete(file);
        }
        catch (Exception ex) { _logger.LogError($"Failed to clear zoomed region cache: {ex.Message}"); }
    }
}
