using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using InteractiveWorldMap.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace InteractiveWorldMap.Services;

/// <summary>
/// Validates the application environment at startup.
/// </summary>
public class StartupValidator
{
    private readonly ILogger _logger;
    private readonly string _contentFolderPath;

    public StartupValidator(ILogger logger, string contentFolderPath)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _contentFolderPath = contentFolderPath ?? throw new ArgumentNullException(nameof(contentFolderPath));
    }

    /// <summary>
    /// Validates the application environment including folder structure and required files.
    /// </summary>
    /// <returns>ValidationResult containing errors and warnings</returns>
    public ValidationResult ValidateEnvironment()
    {
        var result = new ValidationResult();

        try
        {
            _logger.LogInfo("=== Starting Environment Validation ===");
            _logger.LogInfo($"Base directory: {AppDomain.CurrentDomain.BaseDirectory}");
            _logger.LogInfo($"Content folder path: {_contentFolderPath}");

            // Check Content_Folder existence
            if (!Directory.Exists(_contentFolderPath))
            {
                result.Errors.Add($"Content folder not found: {_contentFolderPath}");
                _logger.LogError($"Content folder not found: {_contentFolderPath}");
                return result; // Cannot continue validation without the folder
            }

            _logger.LogInfo($"✓ Content folder found: {_contentFolderPath}");

            // Check for world map image
            var mapPath = Path.Combine(_contentFolderPath, ContentFileNames.WorldMapFileName);
            if (!File.Exists(mapPath))
            {
                result.Errors.Add($"World map image not found: {mapPath}");
                _logger.LogError($"✗ World map image not found: {mapPath}");
            }
            else
            {
                _logger.LogInfo($"✓ World map image found: {mapPath}");
            }

            // Check for locations.json
            var locationsPath = Path.Combine(_contentFolderPath, ContentFileNames.LocationsJsonFileName);
            if (!File.Exists(locationsPath))
            {
                _logger.LogInfo($"locations.json not found at: {locationsPath}");
                _logger.LogInfo("Will attempt to load from Excel file instead");
            }
            else
            {
                _logger.LogInfo($"✓ locations.json found: {locationsPath}");
                // Validate locations.json format
                ValidateLocationsJson(locationsPath, result);
            }

            // Check for Excel file
            var excelPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Coordinates for map.xlsx");
            if (File.Exists(excelPath))
            {
                _logger.LogInfo($"✓ Excel file found: {excelPath}");
            }
            else
            {
                _logger.LogInfo($"Excel file not found at: {excelPath}");
            }

            // Log validation summary
            if (result.IsValid)
            {
                _logger.LogInfo("✓ Environment validation passed");
            }
            else
            {
                _logger.LogError($"✗ Environment validation failed with {result.Errors.Count} errors and {result.Warnings.Count} warnings");
                foreach (var error in result.Errors)
                {
                    _logger.LogError($"  - {error}");
                }
            }

            _logger.LogInfo("=== Environment Validation Complete ===");
        }
        catch (Exception ex)
        {
            result.Errors.Add($"Unexpected error during validation: {ex.Message}");
            _logger.LogError($"✗ Unexpected error during validation: {ex.Message}\n{ex.StackTrace}");
        }

        return result;
    }

    /// <summary>
    /// Validates the format and content of locations.json.
    /// </summary>
    private void ValidateLocationsJson(string locationsPath, ValidationResult result)
    {
        try
        {
            var json = File.ReadAllText(locationsPath);
            var locations = JsonConvert.DeserializeObject<JArray>(json);

            if (locations == null || !locations.Any())
            {
                result.Warnings.Add("locations.json is empty or contains no locations");
                _logger.LogWarning("locations.json is empty or contains no locations");
                return;
            }

            _logger.LogInfo($"locations.json contains {locations.Count} locations");

            // Validate each location has required fields
            for (int i = 0; i < locations.Count; i++)
            {
                var location = locations[i] as JObject;
                if (location == null)
                {
                    result.Warnings.Add($"Location at index {i} is not a valid object");
                    continue;
                }

                // Check required fields
                if (!location.ContainsKey("Id"))
                    result.Warnings.Add($"Location at index {i} is missing 'Id' field");
                
                if (!location.ContainsKey("Name"))
                    result.Warnings.Add($"Location at index {i} is missing 'Name' field");
                
                if (!location.ContainsKey("PixelX"))
                    result.Warnings.Add($"Location at index {i} is missing 'PixelX' field");
                
                if (!location.ContainsKey("PixelY"))
                    result.Warnings.Add($"Location at index {i} is missing 'PixelY' field");

                // Validate pixel coordinates are positive
                if (location.TryGetValue("PixelX", out var pixelXToken))
                {
                    var pixelX = pixelXToken.Value<double>();
                    if (pixelX < 0 || pixelX > 16397)
                        result.Warnings.Add($"Location at index {i} has invalid PixelX: {pixelX} (must be between 0 and 16397)");
                }

                if (location.TryGetValue("PixelY", out var pixelYToken))
                {
                    var pixelY = pixelYToken.Value<double>();
                    if (pixelY < 0 || pixelY > 11085)
                        result.Warnings.Add($"Location at index {i} has invalid PixelY: {pixelY} (must be between 0 and 11085)");
                }
            }
        }
        catch (JsonException ex)
        {
            result.Errors.Add($"Invalid JSON format in locations.json: {ex.Message}");
            _logger.LogError($"Invalid JSON format in locations.json: {ex.Message}");
        }
        catch (Exception ex)
        {
            result.Warnings.Add($"Error validating locations.json: {ex.Message}");
            _logger.LogWarning($"Error validating locations.json: {ex.Message}");
        }
    }
}

/// <summary>
/// Represents the result of environment validation.
/// </summary>
public class ValidationResult
{
    public List<string> Errors { get; }
    public List<string> Warnings { get; }
    public bool IsValid => !Errors.Any();

    public ValidationResult()
    {
        Errors = new List<string>();
        Warnings = new List<string>();
    }
}
