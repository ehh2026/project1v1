using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using InteractiveWorldMap.Models;
using InteractiveWorldMap.Utilities;
using Newtonsoft.Json;

namespace InteractiveWorldMap.Services;

/// <summary>
/// Loads map images, location data, and location content from the Content_Folder.
/// </summary>
public class ContentLoader : IContentLoader
{
    private readonly ILogger _logger;
    private readonly IContentSetResolver _contentSetResolver;
    private readonly Dictionary<string, BitmapImage> _contentCache;
    private readonly LocationClusterer _clusterer;
    private ClusterCache _clusterCache = null!;
    private Lazy<ContentSetResolution> _activeSetResolution = null!;

    public string ActiveContentSetPath => _activeSetResolution.Value.Path;
    public ContentSetKind ActiveContentSetKind => _activeSetResolution.Value.Kind;

    /// <summary>
    /// Gets or sets the distance threshold for clustering locations (in pixels).
    /// Default is 300 pixels.
    /// </summary>
    public double ClusterDistanceThreshold
    {
        get => _clusterer.DistanceThreshold;
        set => _clusterer.DistanceThreshold = value;
    }

    private string _contentFolderPath = string.Empty;

    /// <summary>
    /// Gets or sets the path to the Content_Folder.
    /// </summary>
    public string ContentFolderPath
    {
        get => _contentFolderPath;
        set
        {
            if (_contentFolderPath != value)
            {
                _contentFolderPath = value;
                _activeSetResolution = new Lazy<ContentSetResolution>(() => _contentSetResolver.ResolveActiveContentSet(_contentFolderPath));
                var activeSet = _activeSetResolution.Value;
                _clusterCache = new ClusterCache(_logger, activeSet.Kind.ToSuffix());
                _logger.LogInfo($"ContentLoader path set to: {_contentFolderPath}, active set: {activeSet.Kind}");
            }
        }
    }

    /// <summary>
    /// Optional override for the Excel coordinate file path.
    /// When null, uses "Coordinates for map.xlsx" next to the executable.
    /// </summary>
    public string? ExcelCoordinateFilePath { get; set; }

    /// <summary>
    /// Gets a value indicating whether the ContentLoader has been initialized.
    /// </summary>
    public bool IsInitialized { get; private set; }

    public ContentLoader(ILogger logger, IContentSetResolver contentSetResolver)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _contentSetResolver = contentSetResolver ?? throw new ArgumentNullException(nameof(contentSetResolver));
        _contentCache = new Dictionary<string, BitmapImage>();
        _clusterer = new LocationClusterer();
        ContentFolderPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ContentFileNames.ContentFolderName);
    }

    private string ResolveAssetPath(string fileName)
    {
        var underAssets = Path.Combine(ContentFolderPath, ContentFileNames.AssetsFolderName, fileName);
        if (File.Exists(underAssets)) return underAssets;
        return Path.Combine(ContentFolderPath, fileName); // legacy-root fallback
    }

    /// <summary>Absolute path to the primary world map image.</summary>
    public string GetWorldMapPath() => ResolveAssetPath(ContentFileNames.WorldMapFileName);

    /// <summary>Absolute path to the full-resolution world map image used for zoomed crops.</summary>
    public string GetFullResolutionWorldMapPath() => ResolveAssetPath(ContentFileNames.FullResolutionWorldMapFileName);

    /// <summary>Resolves a file name under the content folder.</summary>
    public string ResolveContentFilePath(string fileName) => ResolveAssetPath(fileName);

    /// <summary>Resolves a relative pin-part path under the content folder.</summary>
    public string ResolvePinPartPath(string relativePath) => ResolveAssetPath(relativePath);

    /// <summary>
    /// Loads a bitmap from the content folder if present; returns null when missing or on error.
    /// </summary>
    public BitmapImage? TryLoadContentBitmap(string fileName)
    {
        try
        {
            var path = ResolveContentFilePath(fileName);
            if (!File.Exists(path))
                return null;

            return LoadFrozenBitmap(path);
        }
        catch (Exception ex)
        {
            _logger.LogWarning($"Failed to load content bitmap '{fileName}': {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Loads composite pin-part geometry metadata from JSON.
    /// </summary>
    public Dictionary<string, PinPartGeometryEntry> LoadPinPartGeometry(string relativePath)
    {
        try
        {
            var path = ResolvePinPartPath(relativePath);
            if (!File.Exists(path))
            {
                throw new FileNotFoundException("Pin part geometry metadata not found.", path);
            }

            var json = File.ReadAllText(path);
            var geometry = JsonConvert.DeserializeObject<Dictionary<string, PinPartGeometryEntry>>(json);
            if (geometry == null || geometry.Count == 0)
            {
                throw new InvalidOperationException($"No pin part geometry entries were found in {path}");
            }

            _logger.LogInfo($"Loaded {geometry.Count} pin part geometry entries from: {path}");
            return geometry;
        }
        catch (JsonException ex)
        {
            _logger.LogError($"Failed to parse pin part geometry metadata: {ex.Message}");
            throw new InvalidOperationException("Invalid JSON format in pin part geometry metadata.", ex);
        }
        catch (Exception ex) when (ex is not InvalidOperationException && ex is not FileNotFoundException)
        {
            _logger.LogError($"Failed to load pin part geometry metadata: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// Loads the world map image from the Content_Folder.
    /// </summary>
    /// <returns>BitmapImage of the world map</returns>
    public async Task<BitmapImage> LoadMapImageAsync()
    {
        try
        {
            var mapPath = GetWorldMapPath();
            
            if (!File.Exists(mapPath))
            {
                _logger.LogError($"World map image not found at: {mapPath}");
                throw new FileNotFoundException("World map image not found", mapPath);
            }

            return await Task.Run(() =>
            {
                var bitmap = LoadFrozenBitmap(mapPath);
                _logger.LogInfo($"Successfully loaded world map from: {mapPath}");
                return bitmap;
            });
        }
        catch (Exception ex)
        {
            _logger.LogError($"Failed to load world map image: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// Loads location data and clusters them based on proximity.
    /// </summary>
    /// <returns>List of LocationCluster objects</returns>
    public async Task<List<LocationCluster>> LoadClustersAsync()
    {
        try
        {
            _logger.LogInfo("Loading and clustering locations");
            
            var locations = await LoadLocationsAsync();
            
            if (!locations.Any())
            {
                _logger.LogWarning("No locations to cluster");
                return new List<LocationCluster>();
            }

            // Try cache first
            var cached = _clusterCache.TryLoad(locations, ClusterDistanceThreshold);
            if (cached != null)
                return cached;

            // Compute clusters and save to cache
            var clusters = _clusterer.ClusterLocations(locations);
            var stats = _clusterer.GetClusteringStats(clusters);
            
            _logger.LogInfo($"Clustering complete: {stats.TotalClusters} clusters " +
                          $"({stats.SingleLocationClusters} single, {stats.MultiLocationClusters} multi) " +
                          $"from {stats.TotalLocations} locations");

            _clusterCache.Save(locations, clusters, ClusterDistanceThreshold);
            
            return clusters;
        }
        catch (Exception ex)
        {
            _logger.LogError($"Failed to load and cluster locations: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// Loads location data from Excel file first, then falls back to locations.json.
    /// </summary>
    /// <returns>List of Location objects</returns>
    public async Task<List<Location>> LoadLocationsAsync()
    {
        try
        {
            // Try to load from Excel file first
            var excelPath = ExcelCoordinateFilePath
                ?? Path.Combine(ActiveContentSetPath, ContentFileNames.ExcelCoordinateFileName);
            _logger.LogInfo($"Checking for Excel file at: {excelPath}");
            
            if (File.Exists(excelPath))
            {
                _logger.LogInfo("Excel file found, attempting to load locations from Excel");
                var reader = new ExcelCoordinateReader(_logger);
                var locationsFromExcel = reader.ReadLocationsFromExcel(excelPath);
                
                if (locationsFromExcel.Any())
                {
                    _logger.LogInfo($"Successfully loaded {locationsFromExcel.Count} locations from Excel");
                    IsInitialized = true;
                    return locationsFromExcel;
                }
                else
                {
                    _logger.LogWarning("Excel file exists but no locations were parsed");
                }
            }
            else
            {
                _logger.LogInfo("Excel file not found, will try locations.json");
            }

            // Fall back to locations.json
            var locationsPath = Path.Combine(ActiveContentSetPath, ContentFileNames.LocationsJsonFileName);
            _logger.LogInfo($"Checking for locations.json at: {locationsPath}");
            
            if (!File.Exists(locationsPath))
            {
                _logger.LogWarning($"Neither Excel file nor locations.json found");
                return new List<Location>();
            }

            _logger.LogInfo("Loading locations from locations.json");
            var json = await File.ReadAllTextAsync(locationsPath);
            var locations = JsonConvert.DeserializeObject<List<Location>>(json);
            
            if (locations == null || !locations.Any())
            {
                _logger.LogWarning("No locations found in locations.json");
                return new List<Location>();
            }

            _logger.LogInfo($"Successfully loaded {locations.Count} locations from JSON");
            IsInitialized = true;
            return locations;
        }
        catch (JsonException ex)
        {
            _logger.LogError($"Failed to parse locations.json: {ex.Message}");
            throw new InvalidOperationException("Invalid JSON format in locations.json", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError($"Failed to load locations: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// Loads content for a specific location with caching support.
    /// Content is expected to be in a subfolder named after the location.
    /// </summary>
    /// <param name="location">The location to load content for</param>
    /// <returns>Array of tuples containing BitmapImage and optional translation/caption text</returns>
    public async Task<(BitmapImage Image, string? TranslationText, string? CaptionText)[]> LoadAllLocationImagesWithTranslationsAsync(Location location)
    {
        if (location == null)
            throw new ArgumentNullException(nameof(location));

        try
        {
            _logger.LogInfo($"Loading all images with translations for location: {location.Name}");

            var imageFiles = ResolveLocationImageFiles(location);
            if (imageFiles.Length == 0)
            {
                _logger.LogWarning($"No image files found for location {location.Name}");
                return Array.Empty<(BitmapImage, string?, string?)>();
            }

            var results = new List<(BitmapImage Image, string? TranslationText, string? CaptionText)>();
            var locationFolder = Path.Combine(ActiveContentSetPath, location.Name);
            
            for (int i = 0; i < imageFiles.Length; i++)
            {
                var imagePath = imageFiles[i];
                if (!Path.IsPathRooted(imagePath))
                {
                    imagePath = Path.Combine(locationFolder, imagePath);
                }

                if (!File.Exists(imagePath))
                {
                    _logger.LogWarning($"Missing image file for location {location.Name}: {imagePath}");
                    continue;
                }
                
                BitmapImage image;
                try
                {
                    image = await Task.Run(() => LoadFrozenBitmap(imagePath));
                }
                catch (Exception ex)
                {
                    _logger.LogWarning($"Failed to load image file for location {location.Name}: {imagePath} ({ex.Message})");
                    continue;
                }

                var imageFileName = Path.GetFileName(imagePath);
                var translationText = await TryReadSidecarTextAsync(
                    locationFolder,
                    Path.GetFileNameWithoutExtension(imageFileName) + ".txt",
                    "translation",
                    Path.GetFileNameWithoutExtension(imageFileName));
                var captionText = GetCaptionText(location, imageFileName) ?? await TryReadSidecarTextAsync(
                    locationFolder,
                    Path.GetFileNameWithoutExtension(imageFileName) + "-caption.txt",
                    "caption",
                    Path.GetFileNameWithoutExtension(imageFileName));

                results.Add((image, translationText, captionText));
            }

            _logger.LogInfo($"Successfully loaded {results.Count} images with translations for location: {location.Name}");
            return results.ToArray();
        }
        catch (Exception ex)
        {
            _logger.LogError($"Failed to load images with translations for location {location.Name}: {ex.Message}\n{ex.StackTrace}");
            return Array.Empty<(BitmapImage, string?, string?)>();
        }
    }

    /// <summary>
    /// Loads all images from a location's folder.
    /// Content is expected to be in a subfolder named after the location.
    /// </summary>
    /// <param name="location">The location to load content for</param>
    /// <returns>BitmapImage for image content, or null for text content</returns>
    public async Task<BitmapImage[]> LoadAllLocationImagesAsync(Location location)
    {
        if (location == null)
            throw new ArgumentNullException(nameof(location));

        var results = await LoadAllLocationImagesWithTranslationsAsync(location);
        return results.Select(r => r.Image).ToArray();
    }

    /// <summary>
    /// Loads content for a location.
    /// Content is expected to be in a subfolder named after the location.
    /// </summary>
    /// <param name="location">The location to load content for</param>
    /// <returns>BitmapImage for image content, or null for text content</returns>
    public async Task<BitmapImage?> LoadLocationContentAsync(Location location)
    {
        if (location == null)
            throw new ArgumentNullException(nameof(location));

        try
        {
            _logger.LogInfo($"Loading content for location: {location.Name}");

            // Return cached content if available
            if (_contentCache.TryGetValue(location.Name, out var cachedImage))
            {
                _logger.LogInfo($"Returning cached content for location: {location.Name}");
                return cachedImage;
            }

            // Look for content in a subfolder named after the location
            var locationFolder = Path.Combine(ActiveContentSetPath, location.Name);
            
            if (!Directory.Exists(locationFolder))
            {
                _logger.LogWarning($"Content folder not found for location {location.Name}: {locationFolder}");
                return null;
            }

            // Find the first image file in the location folder
            var imageFiles = FindImageFiles(locationFolder).FirstOrDefault();

            if (string.IsNullOrEmpty(imageFiles))
            {
                _logger.LogWarning($"No image files found in location folder: {locationFolder}");
                return null;
            }

            var bitmap = await Task.Run(() => LoadFrozenBitmap(imageFiles!));

            // Cache the loaded image
            _contentCache[location.Name] = bitmap;
            _logger.LogInfo($"Successfully loaded and cached content for location: {location.Name}");
            return bitmap;
        }
        catch (Exception ex)
        {
            _logger.LogError($"Failed to load content for location {location.Name}: {ex.Message}\n{ex.StackTrace}");
            return null;
        }
    }

    /// <summary>
    /// Validates that the Content_Folder exists and contains required files.
    /// </summary>
    /// <returns>True if validation passes, false otherwise</returns>
    public bool ValidateContentFolder()
    {
        try
        {
            var activeSet = _activeSetResolution.Value;
            _logger.LogInfo($"Validating content folder. Selected set: {activeSet.Kind} at {activeSet.Path}");

            if (!Directory.Exists(ContentFolderPath))
            {
                _logger.LogError($"Content folder not found: {ContentFolderPath}");
                return false;
            }

            var mapPath = GetWorldMapPath();
            if (!File.Exists(mapPath))
            {
                _logger.LogError($"World map image not found: {mapPath}");
                return false;
            }

            if (activeSet.Kind == ContentSetKind.Production || activeSet.Kind == ContentSetKind.Demo)
            {
                if (!_contentSetResolver.HasCoordinateSource(activeSet.Path))
                {
                    _logger.LogError($"Active content set folder '{activeSet.Path}' exists but is missing a coordinate source (locations.json or Coordinates for map.xlsx).");
                    return false;
                }
            }
            else if (activeSet.Kind == ContentSetKind.Legacy)
            {
                if (!_contentSetResolver.HasCoordinateSource(activeSet.Path))
                {
                    _logger.LogError("Validation error: Legacy content root is missing a coordinate source. " +
                        "Please organize Images&Content into Demo-Content or Production-Content (or rename Production-Content.disabled to Production-Content to enable it). " +
                        "Alternatively, place locations.json or 'Coordinates for map.xlsx' directly under Images&Content to run in developer legacy fallback mode.");
                    return false;
                }
            }

            _logger.LogInfo("Content folder validation passed");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError($"Content folder validation failed: {ex.Message}");
            return false;
        }
    }

    // -------------------------------------------------------------------------
    // Private helpers
    // -------------------------------------------------------------------------

    /// <summary>
    /// Loads a WPF bitmap from an absolute file path and freezes it for thread safety.
    /// </summary>
    private static BitmapImage LoadFrozenBitmap(string absolutePath)
    {
        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.UriSource = new Uri(absolutePath, UriKind.Absolute);
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.EndInit();
        bitmap.Freeze();
        return bitmap;
    }

    /// <summary>
    /// Returns all .jpg/.png/.jpeg files in <paramref name="folder"/> in file-system order.
    /// </summary>
    private static string[] FindImageFiles(string folder) =>
        Directory.GetFiles(folder, "*.jpg")
            .Concat(Directory.GetFiles(folder, "*.png"))
            .Concat(Directory.GetFiles(folder, "*.jpeg"))
            .ToArray();

    private async Task<string?> TryReadSidecarTextAsync(
        string folder,
        string fileName,
        string label,
        string imagePrefix)
    {
        var path = Path.Combine(folder, fileName);
        if (!File.Exists(path))
            return null;

        try
        {
            var text = await File.ReadAllTextAsync(path);
            _logger.LogInfo($"  Found {label} for: {imagePrefix}");
            return text;
        }
        catch (Exception ex)
        {
            _logger.LogWarning($"Failed to read {label} file {path}: {ex.Message}");
            return null;
        }
    }

    private string[] ResolveLocationImageFiles(Location location)
    {
        if (location.ImageFileNames.Count > 0)
        {
            return location.ImageFileNames
                .Where(fileName => !string.IsNullOrWhiteSpace(fileName))
                .OrderBy(fileName => ExtractLeadingSortKey(fileName ?? string.Empty))
                .ThenBy(fileName => fileName, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        var folder = Path.Combine(ActiveContentSetPath, location.Name);
        if (!Directory.Exists(folder))
            return Array.Empty<string>();

        return FindImageFiles(folder)
            .Select(Path.GetFileName)
            .Where(fileName => !string.IsNullOrWhiteSpace(fileName))
            .OrderBy(fileName => ExtractLeadingSortKey(fileName ?? string.Empty))
            .ThenBy(fileName => fileName, StringComparer.OrdinalIgnoreCase)
            .ToArray()!;
    }

    private string? GetCaptionText(Location location, string imageFileName)
    {
        return location.CaptionsByImageFileName.TryGetValue(imageFileName, out var caption)
            ? caption
            : null;
    }

    private static int ExtractLeadingSortKey(string fileName)
    {
        var baseName = Path.GetFileNameWithoutExtension(fileName);
        var digits = new string(baseName.TakeWhile(char.IsDigit).ToArray());
        return int.TryParse(digits, out var value) ? value : int.MaxValue;
    }

    /// <summary>
    /// Loads didactic text from a location's folder if it exists.
    /// </summary>
    /// <param name="location">The location to load didactic text for</param>
    /// <returns>The didactic text content, or null if not found</returns>
    public async Task<string?> LoadDidacticTextAsync(Location location)
    {
        if (location == null)
            throw new ArgumentNullException(nameof(location));

        try
        {
            if (!string.IsNullOrWhiteSpace(location.DidacticText))
            {
                return location.DidacticText;
            }

            var locationFolder = Path.Combine(ActiveContentSetPath, location.Name);
            var didacticPath = Path.Combine(locationFolder, "didactic.txt");

            if (File.Exists(didacticPath))
            {
                var text = await File.ReadAllTextAsync(didacticPath);
                _logger.LogInfo($"Loaded didactic text for location: {location.Name}");
                return text;
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning($"Failed to load didactic text for location {location.Name}: {ex.Message}");
            return null;
        }
    }
}
