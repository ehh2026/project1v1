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
public class ContentLoader
{
    private readonly ILogger _logger;
    private readonly Dictionary<string, BitmapImage> _contentCache;
    private readonly LocationClusterer _clusterer;
    private readonly ClusterCache _clusterCache;

    /// <summary>
    /// Gets or sets the distance threshold for clustering locations (in pixels).
    /// Default is 300 pixels.
    /// </summary>
    public double ClusterDistanceThreshold
    {
        get => _clusterer.DistanceThreshold;
        set => _clusterer.DistanceThreshold = value;
    }

    /// <summary>
    /// Gets or sets the path to the Content_Folder.
    /// </summary>
    public string ContentFolderPath { get; set; }

    /// <summary>
    /// Optional override for the Excel coordinate file path.
    /// When null, uses "Coordinates for map.xlsx" next to the executable.
    /// </summary>
    public string? ExcelCoordinateFilePath { get; set; }

    /// <summary>
    /// Gets a value indicating whether the ContentLoader has been initialized.
    /// </summary>
    public bool IsInitialized { get; private set; }

    public ContentLoader(ILogger logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _contentCache = new Dictionary<string, BitmapImage>();
        _clusterer = new LocationClusterer();
        _clusterCache = new ClusterCache(logger);
        ContentFolderPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ContentFileNames.ContentFolderName);
        _logger.LogInfo($"ContentLoader initialized with path: {ContentFolderPath}");
    }

    /// <summary>Absolute path to the primary world map image.</summary>
    public string GetWorldMapPath() => Path.Combine(ContentFolderPath, ContentFileNames.WorldMapFileName);

    /// <summary>Absolute path to the full-resolution world map image used for zoomed crops.</summary>
    public string GetFullResolutionWorldMapPath() => Path.Combine(ContentFolderPath, ContentFileNames.FullResolutionWorldMapFileName);

    /// <summary>Resolves a file name under the content folder.</summary>
    public string ResolveContentFilePath(string fileName) => Path.Combine(ContentFolderPath, fileName);

    /// <summary>Resolves a relative pin-part path under the content folder.</summary>
    public string ResolvePinPartPath(string relativePath) => Path.Combine(ContentFolderPath, relativePath);

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

            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.UriSource = new Uri(path, UriKind.Absolute);
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.EndInit();
            bitmap.Freeze();
            return bitmap;
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
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.UriSource = new Uri(mapPath, UriKind.Absolute);
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.EndInit();
                bitmap.Freeze(); // Make it thread-safe
                
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
                ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Coordinates for map.xlsx");
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
            var locationsPath = Path.Combine(ContentFolderPath, ContentFileNames.LocationsJsonFileName);
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
    /// <returns>Array of tuples containing BitmapImage and optional translation text</returns>
    public async Task<(BitmapImage Image, string? TranslationText)[]> LoadAllLocationImagesWithTranslationsAsync(Location location)
    {
        if (location == null)
            throw new ArgumentNullException(nameof(location));

        try
        {
            _logger.LogInfo($"Loading all images with translations for location: {location.Name}");

            var locationFolder = Path.Combine(ContentFolderPath, location.Name);
            
            if (!Directory.Exists(locationFolder))
            {
                _logger.LogWarning($"Content folder not found for location {location.Name}: {locationFolder}");
                return Array.Empty<(BitmapImage, string?)>();
            }

            // Find all image files in the location folder and sort by filename
            var imageFiles = Directory.GetFiles(locationFolder, "*.jpg")
                .Concat(Directory.GetFiles(locationFolder, "*.png"))
                .Concat(Directory.GetFiles(locationFolder, "*.jpeg"))
                .OrderBy(f => Path.GetFileName(f))
                .ToArray();

            if (imageFiles.Length == 0)
            {
                _logger.LogWarning($"No image files found in location folder: {locationFolder}");
                return Array.Empty<(BitmapImage, string?)>();
            }

            var results = new (BitmapImage, string?)[imageFiles.Length];
            
            for (int i = 0; i < imageFiles.Length; i++)
            {
                var imagePath = imageFiles[i];
                
                // Load image
                var image = await Task.Run(() =>
                {
                    var img = new BitmapImage();
                    img.BeginInit();
                    img.UriSource = new Uri(imagePath, UriKind.Absolute);
                    img.CacheOption = BitmapCacheOption.OnLoad;
                    img.EndInit();
                    img.Freeze();
                    return img;
                });

                // Look for corresponding translation text file
                var imageFileNameWithoutExt = Path.GetFileNameWithoutExtension(imagePath);
                var translationPath = Path.Combine(locationFolder, imageFileNameWithoutExt + ".txt");
                
                string? translationText = null;
                if (File.Exists(translationPath))
                {
                    try
                    {
                        translationText = await File.ReadAllTextAsync(translationPath);
                        _logger.LogInfo($"  Found translation for: {imageFileNameWithoutExt}");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning($"Failed to read translation file {translationPath}: {ex.Message}");
                    }
                }

                results[i] = (image, translationText);
            }

            _logger.LogInfo($"Successfully loaded {results.Length} images with translations for location: {location.Name}");
            return results;
        }
        catch (Exception ex)
        {
            _logger.LogError($"Failed to load images with translations for location {location.Name}: {ex.Message}\n{ex.StackTrace}");
            return Array.Empty<(BitmapImage, string?)>();
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

        try
        {
            _logger.LogInfo($"Loading all images for location: {location.Name}");

            var locationFolder = Path.Combine(ContentFolderPath, location.Name);
            
            if (!Directory.Exists(locationFolder))
            {
                _logger.LogWarning($"Content folder not found for location {location.Name}: {locationFolder}");
                return Array.Empty<BitmapImage>();
            }

            // Find all image files in the location folder
            var imageFiles = Directory.GetFiles(locationFolder, "*.jpg")
                .Concat(Directory.GetFiles(locationFolder, "*.png"))
                .Concat(Directory.GetFiles(locationFolder, "*.jpeg"))
                .ToArray();

            if (imageFiles.Length == 0)
            {
                _logger.LogWarning($"No image files found in location folder: {locationFolder}");
                return Array.Empty<BitmapImage>();
            }

            var images = new BitmapImage[imageFiles.Length];
            
            for (int i = 0; i < imageFiles.Length; i++)
            {
                var imagePath = imageFiles[i];
                images[i] = await Task.Run(() =>
                {
                    var img = new BitmapImage();
                    img.BeginInit();
                    img.UriSource = new Uri(imagePath, UriKind.Absolute);
                    img.CacheOption = BitmapCacheOption.OnLoad;
                    img.EndInit();
                    img.Freeze();
                    return img;
                });
            }

            _logger.LogInfo($"Successfully loaded {images.Length} images for location: {location.Name}");
            return images;
        }
        catch (Exception ex)
        {
            _logger.LogError($"Failed to load images for location {location.Name}: {ex.Message}\n{ex.StackTrace}");
            return Array.Empty<BitmapImage>();
        }
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
            var locationFolder = Path.Combine(ContentFolderPath, location.Name);
            
            if (!Directory.Exists(locationFolder))
            {
                _logger.LogWarning($"Content folder not found for location {location.Name}: {locationFolder}");
                return null;
            }

            // Find the first image file in the location folder
            var imageFiles = Directory.GetFiles(locationFolder, "*.jpg")
                .Concat(Directory.GetFiles(locationFolder, "*.png"))
                .Concat(Directory.GetFiles(locationFolder, "*.jpeg"))
                .FirstOrDefault();

            if (string.IsNullOrEmpty(imageFiles))
            {
                _logger.LogWarning($"No image files found in location folder: {locationFolder}");
                return null;
            }

            var bitmap = await Task.Run(() =>
            {
                var img = new BitmapImage();
                img.BeginInit();
                img.UriSource = new Uri(imageFiles, UriKind.Absolute);
                img.CacheOption = BitmapCacheOption.OnLoad;
                img.EndInit();
                img.Freeze(); // Make it thread-safe
                return img;
            });

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

            var locationsPath = Path.Combine(ContentFolderPath, ContentFileNames.LocationsJsonFileName);
            if (!File.Exists(locationsPath))
            {
                _logger.LogWarning($"Locations file not found: {locationsPath}");
                // This is a warning, not a critical error
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
            var locationFolder = Path.Combine(ContentFolderPath, location.Name);
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
