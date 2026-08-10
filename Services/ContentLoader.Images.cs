using System;
using System.IO;
using System.Windows.Media.Imaging;
using InteractiveWorldMap.Utilities;

namespace InteractiveWorldMap.Services;

/// <summary>
/// Content-image loading helpers: a file-size advisory (UI-thread safe) and display-box downscaling
/// (background thread). Split out of <see cref="ContentLoader"/> to keep that file focused.
/// </summary>
public partial class ContentLoader
{
    /// <summary>
    /// Advisory check for a heavy content file. Uses <see cref="FileInfo.Length"/> (a filesystem
    /// metadata stat — instant, no file read) so it is safe to call on the UI thread before the
    /// backgrounded decode. When the file is at/over <see cref="LargeImageWarnBytes"/> it logs an
    /// actionable warning and raises <see cref="LargeImageDetected"/> so a notice can appear while the
    /// image loads. File size drives this notice because it tracks decode/IO latency (the symptom seen
    /// with 200&#160;MB TIFFs); pixel dimensions separately drive the decode downscale in
    /// <see cref="LoadImageDownscaled"/>. Never throws.
    /// </summary>
    private void WarnIfHeavyImageFile(string absolutePath)
    {
        if (!EnableImageDiagnostics || _largeImageWarnBytes <= 0)
            return;

        try
        {
            var bytes = new FileInfo(absolutePath).Length;
            if (bytes < _largeImageWarnBytes)
                return;

            _logger.LogWarning(
                $"Heavy content image file ({bytes / (1024 * 1024)} MB): {absolutePath}. " +
                "Consider replacing it with a display-sized, compressed version for faster loading.");
            LargeImageDetected?.Invoke(Path.GetFileName(absolutePath), bytes);
        }
        catch (Exception ex)
        {
            _logger.LogWarning($"Could not check size of content image: {absolutePath} ({ex.Message})");
        }
    }

    /// <summary>
    /// Loads and freezes a content bitmap, downscaling at decode time to fit the configured display box
    /// so a very large image (e.g. a high-megapixel TIFF) cannot blow up render cost/memory and hang
    /// the UI. Reads the image header to size the decode, then decodes. Runs entirely on a background
    /// thread — all file IO (header read + full decode) happens here, never on the UI thread.
    /// </summary>
    private BitmapImage LoadImageDownscaled(string absolutePath) =>
        LoadFrozenBitmap(absolutePath, ComputeDecodeWidth(absolutePath));

    /// <summary>
    /// Loads and freezes a bitmap at native resolution (no display-box downscaling). Used for the world
    /// map and other assets that need full resolution; location content images go through
    /// <see cref="LoadImageDownscaled"/> instead.
    /// </summary>
    private static BitmapImage LoadFrozenBitmap(string absolutePath, int decodePixelWidth = 0)
    {
        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.UriSource = new Uri(absolutePath, UriKind.Absolute);
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        if (decodePixelWidth > 0)
            bitmap.DecodePixelWidth = decodePixelWidth;
        bitmap.EndInit();
        bitmap.Freeze();
        return bitmap;
    }

    /// <summary>
    /// Reads an image's native dimensions (header only, no full-pixel decode) and returns the
    /// <c>DecodePixelWidth</c> needed to fit the configured box (<c>0</c> = decode at native size).
    /// Called on the background decode thread. Never throws.
    /// </summary>
    private int ComputeDecodeWidth(string absolutePath)
    {
        if (_maxDecodePixelWidth <= 0 && _maxDecodePixelHeight <= 0)
            return 0; // no cap configured → native decode

        int sourceWidth, sourceHeight;
        try
        {
            using var stream = File.OpenRead(absolutePath);
            var decoder = BitmapDecoder.Create(
                stream,
                BitmapCreateOptions.DelayCreation | BitmapCreateOptions.IgnoreColorProfile,
                BitmapCacheOption.None);
            if (decoder.Frames.Count == 0)
                return 0;
            sourceWidth = decoder.Frames[0].PixelWidth;
            sourceHeight = decoder.Frames[0].PixelHeight;
        }
        catch (Exception ex)
        {
            _logger.LogWarning($"Could not read image dimensions for decode cap: {absolutePath} ({ex.Message})");
            return 0; // fall back to native decode
        }

        var decodeWidth = ImageDecodeMath.ComputeDecodePixelWidth(
            sourceWidth, sourceHeight, _maxDecodePixelWidth, _maxDecodePixelHeight);

        if (decodeWidth > 0 && EnableImageDiagnostics)
        {
            _logger.LogWarning(
                $"Large content image {sourceWidth}x{sourceHeight}px downscaled to fit " +
                $"{_maxDecodePixelWidth}x{_maxDecodePixelHeight}: {absolutePath}. " +
                "Consider replacing it with a display-sized version.");
        }

        return decodeWidth;
    }
}
