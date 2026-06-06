using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using InteractiveWorldMap.Models;

namespace InteractiveWorldMap.Services;

public interface IContentLoader
{
    double ClusterDistanceThreshold { get; set; }
    string ContentFolderPath { get; set; }
    string? ExcelCoordinateFilePath { get; set; }
    bool IsInitialized { get; }

    string GetWorldMapPath();
    string GetFullResolutionWorldMapPath();
    string ResolveContentFilePath(string fileName);
    string ResolvePinPartPath(string relativePath);
    BitmapImage? TryLoadContentBitmap(string fileName);
    Dictionary<string, PinPartGeometryEntry> LoadPinPartGeometry(string relativePath);
    Task<BitmapImage> LoadMapImageAsync();
    Task<List<LocationCluster>> LoadClustersAsync();
    Task<List<Location>> LoadLocationsAsync();
    Task<(BitmapImage Image, string? TranslationText)[]> LoadAllLocationImagesWithTranslationsAsync(Location location);
    Task<BitmapImage[]> LoadAllLocationImagesAsync(Location location);
    Task<BitmapImage?> LoadLocationContentAsync(Location location);
    bool ValidateContentFolder();
    Task<string?> LoadDidacticTextAsync(Location location);
}
