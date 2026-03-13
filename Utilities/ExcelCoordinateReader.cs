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
            if (cells.Count < 4)
            {
                _logger.LogWarning($"Row {rowIndex} has fewer than 4 cells, skipping");
                return null;
            }

            var name = GetCellValue((XmlElement)cells[0]!, sharedStrings);
            var pixelXStr = GetCellValue((XmlElement)cells[1]!, sharedStrings);
            var pixelYStr = GetCellValue((XmlElement)cells[2]!, sharedStrings);
            var address = GetCellValue((XmlElement)cells[3]!, sharedStrings);

            if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(pixelXStr) || string.IsNullOrEmpty(pixelYStr))
            {
                _logger.LogWarning($"Row {rowIndex} has missing required fields");
                return null;
            }

            if (!double.TryParse(pixelXStr, out var pixelX) || !double.TryParse(pixelYStr, out var pixelY))
            {
                _logger.LogWarning($"Row {rowIndex}: Could not parse pixel coordinates");
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
