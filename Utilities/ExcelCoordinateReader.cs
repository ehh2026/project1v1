using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Xml;
using InteractiveWorldMap.Models;
using InteractiveWorldMap.Services;

namespace InteractiveWorldMap.Utilities;

/// <summary>
/// Reads location and content data from Excel (.xlsx) workbooks using streaming XML parse.
/// </summary>
public class ExcelCoordinateReader
{
    private static readonly XmlReaderSettings ReaderSettings = new()
    {
        IgnoreComments = true,
        IgnoreWhitespace = true,
        DtdProcessing = DtdProcessing.Prohibit,
        XmlResolver = null
    };

    private readonly ILogger _logger;

    private sealed record WorkbookRows(
        List<Dictionary<string, string>> LocationRows,
        List<Dictionary<string, string>> BioRows,
        List<Dictionary<string, string>> CaptionRows)
    {
        public static WorkbookRows Empty { get; } = new(
            new List<Dictionary<string, string>>(),
            new List<Dictionary<string, string>>(),
            new List<Dictionary<string, string>>());
    }

    public ExcelCoordinateReader(ILogger logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

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

            var workbook = ReadWorkbookData(excelPath);
            if (workbook.LocationRows.Count == 0)
            {
                _logger.LogWarning("No worksheets were found in the Excel file");
                return locations;
            }

            var imageColumns = GetImageColumns(workbook.LocationRows);
            var bioByName = BuildBioDictionary(workbook.BioRows);
            var captionsByName = BuildCaptionsByName(workbook.CaptionRows);

            var locationIndex = 0;
            foreach (var row in workbook.LocationRows.Skip(1))
            {
                locationIndex++;
                var location = ParseLocationRow(row, locationIndex, imageColumns);
                if (location == null)
                    continue;

                AddLocationContent(location, bioByName, captionsByName);
                locations.Add(location);
                _logger.LogInfo($"Parsed location: {location.Name} at ({location.PixelX}, {location.PixelY})");
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

    private WorkbookRows ReadWorkbookData(string excelPath)
    {
        using var zip = ZipFile.OpenRead(excelPath);
        var sharedStrings = ReadSharedStrings(zip);
        var sheetPaths = ReadWorksheetPaths(zip);

        if (sheetPaths.Count == 0)
            return WorkbookRows.Empty;

        return new WorkbookRows(
            ReadWorksheetRows(zip, sheetPaths[0], sharedStrings),
            sheetPaths.Count > 1
                ? ReadWorksheetRows(zip, sheetPaths[1], sharedStrings)
                : new List<Dictionary<string, string>>(),
            sheetPaths.Count > 2
                ? ReadWorksheetRows(zip, sheetPaths[2], sharedStrings)
                : new List<Dictionary<string, string>>());
    }

    private static IReadOnlyList<string> GetImageColumns(
        IReadOnlyList<Dictionary<string, string>> locationRows)
    {
        var locationHeaderRow = locationRows.FirstOrDefault() ?? new Dictionary<string, string>();
        return locationHeaderRow
            .Where(pair => pair.Value.StartsWith("Image ", StringComparison.OrdinalIgnoreCase))
            .Select(pair => pair.Key)
            .OrderBy(column => column, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static Dictionary<string, string> BuildBioDictionary(
        IEnumerable<Dictionary<string, string>> bioRows) =>
        bioRows
            .Where(row => row.TryGetValue("A", out var name) && !string.IsNullOrWhiteSpace(name))
            .ToDictionary(
                row => row["A"],
                row => row.TryGetValue("B", out var text) ? text : string.Empty,
                StringComparer.OrdinalIgnoreCase);

    private static Dictionary<string, Dictionary<string, string>> BuildCaptionsByName(
        IEnumerable<Dictionary<string, string>> captionRows) =>
        captionRows
            .Where(row => row.TryGetValue("A", out var name) && !string.IsNullOrWhiteSpace(name))
            .GroupBy(row => row["A"], StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => group
                    .Where(row => row.TryGetValue("B", out var imageName) && !string.IsNullOrWhiteSpace(imageName))
                    .ToDictionary(
                        row => row["B"],
                        row => row.TryGetValue("C", out var caption) ? caption : string.Empty,
                        StringComparer.OrdinalIgnoreCase),
                StringComparer.OrdinalIgnoreCase);

    private static void AddLocationContent(
        Location location,
        IReadOnlyDictionary<string, string> bioByName,
        IReadOnlyDictionary<string, Dictionary<string, string>> captionsByName)
    {
        if (bioByName.TryGetValue(location.Name, out var bioText) && !string.IsNullOrWhiteSpace(bioText))
        {
            location.DidacticText = bioText;
        }

        if (captionsByName.TryGetValue(location.Name, out var captions))
        {
            location.CaptionsByImageFileName = captions;
        }
    }

    /// <summary>
    /// Streams <c>xl/sharedStrings.xml</c> into an indexable list (empty if the part is absent).
    /// </summary>
    private static List<string> ReadSharedStrings(ZipArchive zip)
    {
        var sharedStrings = new List<string>();
        var entry = zip.GetEntry("xl/sharedStrings.xml");
        if (entry == null)
            return sharedStrings;

        using var stream = entry.Open();
        using var reader = XmlReader.Create(stream, ReaderSettings);
        while (reader.Read())
        {
            if (reader.NodeType != XmlNodeType.Element || reader.LocalName != "si")
                continue;

            using var subtree = reader.ReadSubtree();
            sharedStrings.Add(ReadSharedStringItem(subtree));
        }

        return sharedStrings;
    }

    /// <summary>
    /// Reads one <c>si</c> subtree (possibly with multiple <c>t</c> runs) and returns concatenated text.
    /// </summary>
    private static string ReadSharedStringItem(XmlReader reader)
    {
        var text = new StringBuilder();
        reader.MoveToContent();
        while (reader.Read())
        {
            if (reader.NodeType == XmlNodeType.Element && reader.LocalName == "t")
                text.Append(reader.ReadElementContentAsString());
        }

        return text.ToString();
    }

    private static List<string> ReadWorksheetPaths(ZipArchive zip)
    {
        var paths = new List<string>();
        var workbookEntry = zip.GetEntry("xl/workbook.xml");
        var relsEntry = zip.GetEntry("xl/_rels/workbook.xml.rels");
        if (workbookEntry == null || relsEntry == null)
            return paths;

        // Workbook + rels are tiny; DOM keeps r:id / Relationship wiring reliable.
        var workbook = LoadXmlDocument(workbookEntry);
        var rels = LoadXmlDocument(relsEntry);

        var ns = new XmlNamespaceManager(workbook.NameTable);
        ns.AddNamespace("x", "http://schemas.openxmlformats.org/spreadsheetml/2006/main");
        ns.AddNamespace("r", "http://schemas.openxmlformats.org/officeDocument/2006/relationships");

        var relMap = rels
            .SelectNodes("//*[local-name()='Relationship']")!
            .Cast<XmlElement>()
            .ToDictionary(
                rel => rel.GetAttribute("Id"),
                rel => rel.GetAttribute("Target"),
                StringComparer.OrdinalIgnoreCase);

        foreach (XmlElement sheet in workbook.SelectNodes("//x:sheets/x:sheet", ns)!)
        {
            var relId = sheet.GetAttribute("r:id");
            if (!relMap.TryGetValue(relId, out var target))
                continue;

            paths.Add("xl/" + target.Replace('\\', '/'));
        }

        return paths;
    }

    private static XmlDocument LoadXmlDocument(ZipArchiveEntry entry)
    {
        using var stream = entry.Open();
        var xml = new XmlDocument();
        xml.Load(stream);
        return xml;
    }

    private static List<Dictionary<string, string>> ReadWorksheetRows(
        ZipArchive zip,
        string path,
        List<string> sharedStrings)
    {
        var rows = new List<Dictionary<string, string>>();
        var normalized = path.Replace(Path.DirectorySeparatorChar, '/');
        var entry = zip.GetEntry(normalized);
        if (entry == null)
            return rows;

        using var stream = entry.Open();
        using var reader = XmlReader.Create(stream, ReaderSettings);
        while (reader.Read())
        {
            if (reader.NodeType != XmlNodeType.Element || reader.LocalName != "row")
                continue;

            using var subtree = reader.ReadSubtree();
            rows.Add(ReadWorksheetRow(subtree, sharedStrings));
        }

        return rows;
    }

    private static Dictionary<string, string> ReadWorksheetRow(XmlReader reader, List<string> sharedStrings)
    {
        var cellValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        reader.MoveToContent();
        while (reader.Read())
        {
            if (reader.NodeType != XmlNodeType.Element || reader.LocalName != "c")
                continue;

            var cellRef = reader.GetAttribute("r");
            if (string.IsNullOrWhiteSpace(cellRef))
            {
                using var skipped = reader.ReadSubtree();
                skipped.MoveToContent();
                continue;
            }

            var column = new string(cellRef.TakeWhile(char.IsLetter).ToArray());
            using var cellSubtree = reader.ReadSubtree();
            cellValues[column] = ReadCellValue(cellSubtree, sharedStrings);
        }

        return cellValues;
    }

    private static string ReadCellValue(XmlReader reader, List<string> sharedStrings)
    {
        // Subtree readers start at Initial; MoveToContent lands on <c>.
        reader.MoveToContent();
        var cellType = reader.GetAttribute("t") ?? string.Empty;
        var rawValue = string.Empty;
        var inlineText = new StringBuilder();

        while (reader.Read())
        {
            if (reader.NodeType != XmlNodeType.Element)
                continue;

            if (reader.LocalName == "v")
                rawValue = reader.ReadElementContentAsString();
            else if (reader.LocalName == "t")
                inlineText.Append(reader.ReadElementContentAsString());
        }

        if (cellType == "inlineStr")
            return inlineText.ToString();

        if (cellType == "s"
            && int.TryParse(rawValue, out var sharedIndex)
            && sharedIndex >= 0
            && sharedIndex < sharedStrings.Count)
        {
            return sharedStrings[sharedIndex];
        }

        return rawValue;
    }

    private Location? ParseLocationRow(Dictionary<string, string> cellValues, int locationIndex, IReadOnlyList<string> imageColumns)
    {
        if (!cellValues.TryGetValue("A", out var name) || string.IsNullOrWhiteSpace(name))
            return null;

        if (!TryGetDouble(cellValues, "E", out var pixelX) && !TryGetDouble(cellValues, "B", out pixelX))
            return null;

        if (!TryGetDouble(cellValues, "F", out var pixelY) && !TryGetDouble(cellValues, "C", out pixelY))
            return null;

        var location = new Location
        {
            Id = $"loc_{locationIndex:D3}",
            Name = name,
            PixelX = pixelX,
            PixelY = pixelY,
            ContentType = LocationContentType.Image
        };

        if (cellValues.TryGetValue("D", out var address) && !string.IsNullOrWhiteSpace(address))
        {
            location.ContentFilePath = address;
        }

        foreach (var column in imageColumns)
        {
            if (!cellValues.TryGetValue(column, out var fileName) || string.IsNullOrWhiteSpace(fileName))
                continue;

            location.ImageFileNames.Add(fileName);
        }

        return location;
    }

    private static bool TryGetDouble(IReadOnlyDictionary<string, string> cellValues, string column, out double value)
    {
        value = default;
        return cellValues.TryGetValue(column, out var text) && double.TryParse(text, out value);
    }
}
