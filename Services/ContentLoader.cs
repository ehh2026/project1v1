using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using InteractiveWorldMap.Models;
using Newtonsoft.Json;

namespace InteractiveWorldMap.Services;

/// <summary>
/// Loads map images, location data, and location content from the Content_Folder.
/// </summary>
public class ContentLoader
{
    private readonly ILogger _logger;
    private readonly Dictionary<string, BitmapImage> _contentCache;

    /// <summary>
    /// Gets or sets the path to the Content_Folder.
    /// </summary>
    public string ContentFolderPath { get; set; }

    /// <summary>
    /// Gets a value indicating whether the ContentLoader has been initialized.
    /// </summary>
    public bool IsInitialized { get; private set; }

    public ContentLoader(ILogger logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _contentCache = new Dictionary<string, BitmapImage>();
        ContentFolderPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Images&Content");
        _logger.LogInfo($"ContentLoader initialized with path: {ContentFolderPath}");
    }

    /// <summary>
    /// Loads the world map image from the Content_Folder.
    /// </summary>
    /// <returns>BitmapImage of the world map</returns>
    public async Task<BitmapImage> LoadMapImageAsync()
    {
        try
        {
            var mapPath = Path.Combine(ContentFolderPath, "Large_World_Map_bright.jpg");
            
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
    /// Loads location data from locations.json in the Content_Folder.
    /// </summary>
    /// <returns>List of Location objects</returns>
    public async Task<List<Location>> LoadLocationsAsync()
    {
        try
        {
            var locationsPath = Path.Combine(ContentFolderPath, "locations.json");
            
            if (!File.Exists(locationsPath))
            {
                _logger.LogWarning($"Locations file not found at: {locationsPath}");
                return new List<Location>();
            }

            var json = await File.ReadAllTextAsync(locationsPath);
            var locations = JsonConvert.DeserializeObject<List<Location>>(json);
            
            if (locations == null || !locations.Any())
            {
                _logger.LogWarning("No locations found in locations.json");
                return new List<Location>();
            }

            _logger.LogInfo($"Successfully loaded {locations.Count} locations");
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
    /// </summary>
    /// <param name="location">The location to load content for</param>
    /// <returns>BitmapImage for image content, or null for text content</returns>
    public async Task<BitmapImage?> LoadLocationContentAsync(Location location)
    {
        if (location == null)
            throw new ArgumentNullException(nameof(location));

        try
        {
            // Return cached content if available
            if (_contentCache.TryGetValue(location.ContentFilePath, out var cachedImage))
            {
                _logger.LogInfo($"Returning cached content for location: {location.Name}");
                return cachedImage;
            }

            var contentPath = Path.Combine(ContentFolderPath, location.ContentFilePath);
            
            if (!File.Exists(contentPath))
            {
                _logger.LogWarning($"Content file not found for location {location.Name}: {contentPath}");
                return null;
            }

            // Only load images, text content is handled separately
            if (location.ContentType == LocationContentType.Image)
            {
                var bitmap = await Task.Run(() =>
                {
                    var img = new BitmapImage();
                    img.BeginInit();
                    img.UriSource = new Uri(contentPath, UriKind.Absolute);
                    img.CacheOption = BitmapCacheOption.OnLoad;
                    img.EndInit();
                    img.Freeze(); // Make it thread-safe
                    return img;
                });

                // Cache the loaded image
                _contentCache[location.ContentFilePath] = bitmap;
                _logger.LogInfo($"Successfully loaded and cached content for location: {location.Name}");
                return bitmap;
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError($"Failed to load content for location {location.Name}: {ex.Message}");
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

            var mapPath = Path.Combine(ContentFolderPath, "Large_World_Map_bright.jpg");
            if (!File.Exists(mapPath))
            {
                _logger.LogError($"World map image not found: {mapPath}");
                return false;
            }

            var locationsPath = Path.Combine(ContentFolderPath, "locations.json");
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
}
