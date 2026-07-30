using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using InteractiveWorldMap.Models;
using InteractiveWorldMap.Services;

namespace InteractiveWorldMap.Utilities;

/// <summary>
/// Utility to update locations.json from the Excel spreadsheet.
/// </summary>
public class UpdateLocationsFromExcel
{
    /// <summary>
    /// Reads locations from Excel and writes them to locations.json.
    /// </summary>
    public static void UpdateJson(ILogger logger, string? contentSetPath = null)
    {
        try
        {
            var targetPath = contentSetPath ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ContentFileNames.ContentFolderName, ContentFileNames.DemoContentFolderName);
            var excelPath = Path.Combine(targetPath, ContentFileNames.ExcelCoordinateFileName);
            var jsonPath = Path.Combine(targetPath, ContentFileNames.LocationsJsonFileName);

            logger.LogInfo($"Reading from Excel: {excelPath}");

            if (!File.Exists(excelPath))
            {
                logger.LogError($"Excel file not found: {excelPath}");
                return;
            }

            var reader = new ExcelCoordinateReader(logger);
            var locations = reader.ReadLocationsFromExcel(excelPath);

            if (locations.Count == 0)
            {
                logger.LogWarning("No locations found in Excel file");
                return;
            }

            logger.LogInfo($"Found {locations.Count} locations, writing to JSON");

            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };

            var json = JsonSerializer.Serialize(locations, options);
            File.WriteAllText(jsonPath, json);

            logger.LogInfo($"Successfully updated {jsonPath} with {locations.Count} locations");

            // Log the locations for verification
            foreach (var loc in locations)
            {
                logger.LogInfo($"  - {loc.Name}: ({loc.PixelX}, {loc.PixelY})");
            }
        }
        catch (Exception ex)
        {
            logger.LogError($"Error updating locations.json: {ex.Message}\n{ex.StackTrace}");
        }
    }
}
