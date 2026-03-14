using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using System.IO.Compression;
using System.Linq;
using InteractiveWorldMap.Models;
using InteractiveWorldMap.Services;
using InteractiveWorldMap.Utilities;

namespace InteractiveWorldMap.Utilities;

/// <summary>
/// Reads coordinate data from Excel files.
/// </summary>
public class ExcelCoordinateReader
{
    private readonly ILogger _logger;

    public ExcelCoordinateReader(ILogger logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Reads location data from an Excel file.
    /// </summary>
    /// <param name="excelPath">Path to the Excel file</param>
    /// <returns>List of Location objects</returns>
    public List<Location> ReadLocationsFromExcel(string excelPath)
    {
        var locations = new List<Location>();

        try
        {
            if (!File.Exists(excelPath))
            {
                _logger.LogError($"Excel file not found: {excelPath}");
                return locations;
            }

            _logger.LogInfo($"Reading Excel file: {excelPath}");

            // Excel files are ZIP archives
            using (var zip = ZipFile.OpenRead(excelPath))
            {
                // Read shared strings first
                var sharedStrings = ReadSharedStrings(zip);
                _logger.LogInfo($"Loaded {sharedStrings.Count} shared strings");

                // Read worksheet data
                var entry = zip.GetEntry("xl/worksheets/sheet1.xml");
                if (entry == null)
                {
                    _logger.LogError("sheet1.xml not found in Excel file");
                    return locations;
                }

                using (var stream = entry.Open())
                using (var reader = new StreamReader(stream))
                {
                    var xml = new XmlDocument();
                    xml.Load(reader);

                    var rows = xml.GetElementsByTagName("row");
                    _logger.LogInfo($"Found {rows.Count} rows in worksheet");

                    int rowIndex = 0;
                    foreach (XmlElement row in rows)
                    {
                        rowIndex++;
                        
                        // Skip header row
                        if (rowIndex == 1)
                        {
                            _logger.LogInfo("Skipping header row");
                            continue;
                        }

                        var location = ParseLocationRow(row, sharedStrings, rowIndex);
                        if (location != null)
                        {
                            locations.Add(location);
                            _logger.LogInfo($"Parsed location: {location.Name} at ({location.PixelX}, {location.PixelY})");
                        }
                    }
                }
            }

            _logger.LogInfo($"Successfully read {locations.Count} locations from Excel");
            return locations;
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error reading Excel file: {ex.Message}\n{ex.StackTrace}");
            return locations;
        }
    }

    private Dictionary<int, string> ReadSharedStrings(ZipArchive zip)
    {
        var sharedStrings = new Dictionary<int, string>();

        try
        {
            var entry = zip.GetEntry("xl/sharedStrings.xml");
            if (entry == null)
            {
                _logger.LogWarning("sharedStrings.xml not found");
                return sharedStrings;
            }

            using (var stream = entry.Open())
            using (var reader = new StreamReader(stream))
            {
                var xml = new XmlDocument();
                xml.Load(reader);

                var sis = xml.GetElementsByTagName("si");
                int index = 0;
                foreach (XmlElement si in sis)
                {
                    var t = si.GetElementsByTagName("t");
                    if (t.Count > 0)
                    {
                        sharedStrings[index] = t[0].InnerText;
                    }
                    index++;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error reading shared strings: {ex.Message}");
        }

        return sharedStrings;
    }

    private Location? ParseLocationRow(XmlElement row, Dictionary<int, string> sharedStrings, int rowIndex)
    {
        try
        {
            var cells = row.GetElementsByTagName("c");
            
            // Build a dictionary of column -> cell value
            var cellValues = new Dictionary<string, string>();
            foreach (XmlElement cell in cells)
            {
                var cellRef = cell.GetAttribute("r"); // e.g., "A2", "D2"
                if (string.IsNullOrEmpty(cellRef))
                    continue;
                    
                // Extract column letter (e.g., "A" from "A2")
                var column = new string(cellRef.TakeWhile(char.IsLetter).ToArray());
                cellValues[column] = GetCellValue(cell, sharedStrings);
            }

            // Column A: Name
            if (!cellValues.TryGetValue("A", out var name) || string.IsNullOrEmpty(name))
            {
                _logger.LogWarning($"Row {rowIndex}: Missing name in column A");
                return null;
            }

            // Columns E & F: Half size coordinates (Coordinate X halfsize, Coordinate Y halfsize)
            if (!cellValues.TryGetValue("E", out var pixelXStr) || string.IsNullOrEmpty(pixelXStr))
            {
                _logger.LogWarning($"Row {rowIndex}: Missing X coordinate in column E (available columns: {string.Join(", ", cellValues.Keys)})");
                return null;
            }

            if (!cellValues.TryGetValue("F", out var pixelYStr) || string.IsNullOrEmpty(pixelYStr))
            {
                _logger.LogWarning($"Row {rowIndex}: Missing Y coordinate in column F (available columns: {string.Join(", ", cellValues.Keys)})");
                return null;
            }

            // Column D: Address (optional)
            cellValues.TryGetValue("D", out var address);

            // Log what we're trying to parse
            _logger.LogInfo($"Row {rowIndex}: Attempting to parse X='{pixelXStr}', Y='{pixelYStr}'");

            if (!double.TryParse(pixelXStr, out var pixelX) || !double.TryParse(pixelYStr, out var pixelY))
            {
                _logger.LogWarning($"Row {rowIndex}: Could not parse pixel coordinates (X='{pixelXStr}', Y='{pixelYStr}')");
                return null;
            }

            return new Location
            {
                Id = $"loc_{rowIndex:D3}",
                Name = name,
                PixelX = pixelX,
                PixelY = pixelY,
                ContentFilePath = address ?? string.Empty,
                ContentType = LocationContentType.Image
            };
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error parsing row {rowIndex}: {ex.Message}");
            return null;
        }
    }

    private string GetCellValue(XmlElement cell, Dictionary<int, string> sharedStrings)
    {
        try
        {
            var vElement = cell.GetElementsByTagName("v");
            if (vElement.Count == 0)
                return string.Empty;

            var value = vElement[0].InnerText;
            var cellType = cell.GetAttribute("t");

            // If it's a shared string reference
            if (cellType == "s" && int.TryParse(value, out var index))
            {
                return sharedStrings.TryGetValue(index, out var str) ? str : string.Empty;
            }

            return value;
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error getting cell value: {ex.Message}");
            return string.Empty;
        }
    }
}
