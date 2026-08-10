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
    /// Raised (on the caller's thread, before decode) when a content image is large enough that it is
    /// being downscaled to fit the decode box. Arguments are the file name and the image's native
    /// pixel width and height. The image is still loaded (downscaled); this drives a UI notice.
    /// </summary>
    event Action<string, int, int>? LargeImageDetected;
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
