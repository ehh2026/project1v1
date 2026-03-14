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

    public AnimationFrameCache(ILogger logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        
        // Store cache in AppData
        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        _cacheDirectory = Path.Combine(appDataPath, "InteractiveWorldMap", "frame_cache");
        
        Directory.CreateDirectory(_cacheDirectory);
    }

    /// <summary>
    /// Generates a cache key based on animation parameters.
    /// </summary>
    private string GetCacheKey(double startX, double startY, double startW, double startH,
                               double endX, double endY, double endW, double endH,
                               int displayWidth, int displayHeight, int frameIndex)
    {
        var key = $"{startX:F1}_{startY:F1}_{startW:F1}_{startH:F1}_" +
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
