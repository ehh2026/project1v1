using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using InteractiveWorldMap.Models;
using InteractiveWorldMap.Utilities;
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
    private readonly IContentSetResolver _contentSetResolver;

    public StartupValidator(ILogger logger, string contentFolderPath, IContentSetResolver contentSetResolver)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _contentFolderPath = contentFolderPath ?? throw new ArgumentNullException(nameof(contentFolderPath));
        _contentSetResolver = contentSetResolver ?? throw new ArgumentNullException(nameof(contentSetResolver));
    }

    private string ResolveAssetPath(string fileName)
    {
        var underAssets = Path.Combine(_contentFolderPath, ContentFileNames.AssetsFolderName, fileName);
        if (File.Exists(underAssets)) return underAssets;
        return Path.Combine(_contentFolderPath, fileName); // legacy-root fallback
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
            var mapPath = ResolveAssetPath(ContentFileNames.WorldMapFileName);
            if (!File.Exists(mapPath))
            {
                result.Errors.Add($"World map image not found: {mapPath}");
                _logger.LogError($"✗ World map image not found: {mapPath}");
            }
            else
            {
                _logger.LogInfo($"✓ World map image found: {mapPath}");
            }

            var activeSet = _contentSetResolver.ResolveActiveContentSet(_contentFolderPath);
            _logger.LogInfo($"Resolved active content set: {activeSet.Kind} at {activeSet.Path}");

            // Ensure active set has a coordinate source
            if (!_contentSetResolver.HasCoordinateSource(activeSet.Path))
            {
                if (activeSet.Kind == ContentSetKind.Legacy)
                {
                    var errorMsg = "Validation error: Legacy content root is missing a coordinate source. " +
                        "Please organize Images&Content into Demo-Content or Production-Content (or rename Production-Content.disabled to Production-Content to enable it). " +
                        "Alternatively, place locations.json or 'Coordinates for map.xlsx' directly under Images&Content to run in developer legacy fallback mode.";
                    result.Errors.Add(errorMsg);
                    _logger.LogError(errorMsg);
                }
                else
                {
                    var errorMsg = $"Active content set '{activeSet.Path}' is missing a coordinate source (locations.json or Coordinates for map.xlsx).";
                    result.Errors.Add(errorMsg);
                    _logger.LogError(errorMsg);
                }
            }
            else
            {
                // Validate locations.json if it's the chosen coordinate source
                var locationsPath = Path.Combine(activeSet.Path, ContentFileNames.LocationsJsonFileName);
                if (File.Exists(locationsPath))
                {
                    _logger.LogInfo($"✓ locations.json found: {locationsPath}");
                    ValidateLocationsJson(locationsPath, result);
                }
                else
                {
                    _logger.LogInfo($"locations.json not found at: {locationsPath}, will load from Excel file.");
                }

                // Check for Excel file
                var excelPath = Path.Combine(activeSet.Path, ContentFileNames.ExcelCoordinateFileName);
                if (File.Exists(excelPath))
                {
                    _logger.LogInfo($"✓ Excel file found: {excelPath}");
                }
                else
                {
                    _logger.LogInfo($"Excel file not found at: {excelPath}");
                }
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
                ValidateLocationObject(locations[i], i, result);
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

    private static void ValidateLocationObject(JToken token, int index, ValidationResult result)
    {
        if (token is not JObject location)
        {
            result.Warnings.Add($"Location at index {index} is not a valid object");
            return;
        }

        AddMissingFieldWarnings(location, index, result);
        AddCoordinateWarning(location, "PixelX", 16397, index, result);
        AddCoordinateWarning(location, "PixelY", 11085, index, result);
    }

    private static void AddMissingFieldWarnings(JObject location, int index, ValidationResult result)
    {
        foreach (var field in RequiredLocationFields)
        {
            if (!location.ContainsKey(field))
                result.Warnings.Add($"Location at index {index} is missing '{field}' field");
        }
    }

    private static void AddCoordinateWarning(
        JObject location,
        string field,
        double maximum,
        int index,
        ValidationResult result)
    {
        if (!location.TryGetValue(field, out var token))
            return;

        var value = token.Value<double>();
        if (value < 0 || value > maximum)
            result.Warnings.Add($"Location at index {index} has invalid {field}: {value} (must be between 0 and {maximum})");
    }

    private static readonly string[] RequiredLocationFields =
    {
        "Id",
        "Name",
        "PixelX",
        "PixelY"
    };
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
