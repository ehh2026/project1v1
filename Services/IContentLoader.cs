using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using InteractiveWorldMap.Models;
using InteractiveWorldMap.Utilities;

namespace InteractiveWorldMap.Services;

public interface IContentLoader
{
    double ClusterDistanceThreshold { get; set; }
    int MaxCachedLocations { get; set; }

    /// <summary>
    /// Width of the decode target box in pixels. Images larger than the box are downscaled at decode
    /// time (aspect ratio preserved). A non-positive value on both dimensions means full-resolution
    /// decode. See <see cref="Utilities.ImageDecodeMath"/>.
    /// </summary>
    int MaxDecodePixelWidth { get; set; }

    /// <summary>
    /// Height of the decode target box in pixels. See <see cref="MaxDecodePixelWidth"/>.
    /// </summary>
    int MaxDecodePixelHeight { get; set; }

    /// <summary>
    /// File-size threshold in bytes at/above which <see cref="LargeImageDetected"/> fires while a
    /// content image loads. Advisory only; <c>0</c> disables it.
    /// </summary>
    long LargeImageWarnBytes { get; set; }

    /// <summary>
    /// Gates content-image diagnostics: the <see cref="LargeImageDetected"/> notice and the heavy-file /
    /// downscale log warnings. When <c>false</c> (default) they are suppressed; downscaling still runs.
    /// </summary>
    bool EnableImageDiagnostics { get; set; }

    /// <summary>
    /// Raised (on the caller's thread, before decode) when a content image file is at/over
    /// <see cref="LargeImageWarnBytes"/>. Arguments are the file name and its size in bytes. The image
    /// is still loaded (downscaled to the display box); this drives a non-blocking UI notice.
    /// </summary>
    event Action<string, long>? LargeImageDetected;
    string ContentFolderPath { get; set; }
    string? ExcelCoordinateFilePath { get; set; }
    bool IsInitialized { get; }
    string ActiveContentSetPath { get; }
    ContentSetKind ActiveContentSetKind { get; }

    string GetWorldMapPath();
    string GetFullResolutionWorldMapPath();
    string ResolveContentFilePath(string fileName);
    string ResolvePinPartPath(string relativePath);
    BitmapImage? TryLoadContentBitmap(string fileName);
    Dictionary<string, PinPartGeometryEntry> LoadPinPartGeometry(string relativePath);
    Task<BitmapImage> LoadMapImageAsync();
    Task<List<LocationCluster>> LoadClustersAsync();
    Task<List<Location>> LoadLocationsAsync();
    Task<(BitmapImage Image, string? TranslationText, string? CaptionText)[]> LoadAllLocationImagesWithTranslationsAsync(Location location);
    Task<BitmapImage[]> LoadAllLocationImagesAsync(Location location);
    Task<BitmapImage?> LoadLocationContentAsync(Location location);
    bool ValidateContentFolder();
    Task<string?> LoadDidacticTextAsync(Location location);
}
