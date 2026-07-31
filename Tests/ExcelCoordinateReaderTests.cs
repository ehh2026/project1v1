using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using InteractiveWorldMap.Tests.TestHelpers;
using InteractiveWorldMap.Utilities;
using Xunit;

namespace InteractiveWorldMap.Tests;

public class ExcelCoordinateReaderTests
{
    [Fact]
    public void ReadLocationsFromExcel_ReadsWorkbookBackedImagesBioAndCaptions()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "iwm-excel-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        var excelPath = Path.Combine(tempDir, "Coordinates for map.xlsx");

        try
        {
            WriteWorkbook(excelPath);
            var reader = new ExcelCoordinateReader(new MockLogger());

            var locations = reader.ReadLocationsFromExcel(excelPath);

            Assert.Equal(2, locations.Count);
            var location = Assert.Single(locations, loc => loc.Name == "Kevin");
            Assert.Equal("Kevin", location.Name);
            Assert.Equal(2460, location.PixelX);
            Assert.Equal(2577, location.PixelY);
            Assert.Equal("New York, NY", location.ContentFilePath);
            Assert.Equal(new[] { "1-letter-product-pic.jpg", "2-second-image.jpg" }, location.ImageFileNames);
            Assert.Equal("Bio text from workbook", location.DidacticText);
            Assert.Equal("CAPTION", location.CaptionsByImageFileName["1-letter-product-pic.jpg"]);
            Assert.Equal("CAPTION", location.CaptionsByImageFileName["2-second-image.jpg"]);

            var halfOnly = Assert.Single(locations, loc => loc.Name == "Half Only");
            Assert.Equal(6378, halfOnly.PixelX);
            Assert.Equal(2933, halfOnly.PixelY);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void ReadLocationsFromExcel_MissingFile_ReturnsEmptyAndLogsError()
    {
        var logger = new MockLogger();
        var reader = new ExcelCoordinateReader(logger);
        var missingPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "missing.xlsx");

        var locations = reader.ReadLocationsFromExcel(missingPath);

        Assert.Empty(locations);
        Assert.Contains(logger.ErrorMessages, message => message.Contains("Excel file not found"));
    }

    [Fact]
    public void ReadLocationsFromExcel_EmptyWorkbook_ReturnsEmptyAndLogsWarning()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "iwm-excel-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        var excelPath = Path.Combine(tempDir, "empty.xlsx");

        try
        {
            using (ZipFile.Open(excelPath, ZipArchiveMode.Create))
            {
            }

            var logger = new MockLogger();
            var reader = new ExcelCoordinateReader(logger);

            var locations = reader.ReadLocationsFromExcel(excelPath);

            Assert.Empty(locations);
            Assert.Contains(logger.WarningMessages, message => message.Contains("No worksheets"));
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void ReadLocationsFromExcel_MalformedWorksheetXml_ReturnsEmptyAndLogsError()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "iwm-excel-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        var excelPath = Path.Combine(tempDir, "malformed.xlsx");

        try
        {
            WriteWorkbook(excelPath, malformedLocationsSheet: true);
            var logger = new MockLogger();
            var reader = new ExcelCoordinateReader(logger);

            var locations = reader.ReadLocationsFromExcel(excelPath);

            Assert.Empty(locations);
            Assert.Contains(logger.ErrorMessages, message => message.Contains("Error reading Excel file"));
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void ReadLocationsFromExcel_MissingRequiredCoordinateCells_SkipsRow()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "iwm-excel-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        var excelPath = Path.Combine(tempDir, "missing-columns.xlsx");

        try
        {
            WriteWorkbook(excelPath, omitCoordinateCells: true);
            var logger = new MockLogger();
            var reader = new ExcelCoordinateReader(logger);

            var locations = reader.ReadLocationsFromExcel(excelPath);

            Assert.Empty(locations);
            Assert.Contains(logger.InfoMessages, message => message.Contains("Successfully read 0 locations"));
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    /// <summary>
    /// Characterization: Excel workbooks often store text in sharedStrings.xml with cell type t="s".
    /// Streaming and DOM parsers must resolve those indices the same way.
    /// </summary>
    [Fact]
    public void ReadLocationsFromExcel_SharedStringCells_ResolvesNamesCoordsAndImages()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "iwm-excel-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        var excelPath = Path.Combine(tempDir, "shared-strings.xlsx");

        try
        {
            WriteSharedStringWorkbook(excelPath);
            var reader = new ExcelCoordinateReader(new MockLogger());

            var locations = reader.ReadLocationsFromExcel(excelPath);

            Assert.Equal(2, locations.Count);
            var location = Assert.Single(locations, loc => loc.Name == "Shared Kevin");
            Assert.Equal(2460, location.PixelX);
            Assert.Equal(2577, location.PixelY);
            Assert.Equal("New York, NY", location.ContentFilePath);
            Assert.Equal(new[] { "1-letter-product-pic.jpg", "2-second-image.jpg" }, location.ImageFileNames);
            Assert.Equal("Shared bio text", location.DidacticText);
            Assert.Equal("SHARED CAPTION", location.CaptionsByImageFileName["1-letter-product-pic.jpg"]);

            var halfOnly = Assert.Single(locations, loc => loc.Name == "Shared Half");
            Assert.Equal(6378, halfOnly.PixelX);
            Assert.Equal(2933, halfOnly.PixelY);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    private static void WriteWorkbook(
        string excelPath,
        bool malformedLocationsSheet = false,
        bool omitCoordinateCells = false)
    {
        using var zip = ZipFile.Open(excelPath, ZipArchiveMode.Create);

        WriteEntry(zip, "[Content_Types].xml",
            @"<?xml version=""1.0"" encoding=""UTF-8"" standalone=""yes""?>
<Types xmlns=""http://schemas.openxmlformats.org/package/2006/content-types"">
  <Default Extension=""rels"" ContentType=""application/vnd.openxmlformats-package.relationships+xml""/>
  <Default Extension=""xml"" ContentType=""application/xml""/>
  <Override PartName=""/xl/workbook.xml"" ContentType=""application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml""/>
  <Override PartName=""/xl/worksheets/sheet1.xml"" ContentType=""application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml""/>
  <Override PartName=""/xl/worksheets/sheet2.xml"" ContentType=""application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml""/>
  <Override PartName=""/xl/worksheets/sheet3.xml"" ContentType=""application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml""/>
  <Override PartName=""/xl/styles.xml"" ContentType=""application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml""/>
</Types>");

        WriteEntry(zip, "_rels/.rels",
            @"<?xml version=""1.0"" encoding=""UTF-8"" standalone=""yes""?>
<Relationships xmlns=""http://schemas.openxmlformats.org/package/2006/relationships"">
  <Relationship Id=""rId1"" Type=""http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument"" Target=""xl/workbook.xml""/>
</Relationships>");

        WriteEntry(zip, "xl/workbook.xml",
            @"<?xml version=""1.0"" encoding=""UTF-8"" standalone=""yes""?>
<workbook xmlns=""http://schemas.openxmlformats.org/spreadsheetml/2006/main"" xmlns:r=""http://schemas.openxmlformats.org/officeDocument/2006/relationships"">
  <sheets>
    <sheet name=""Locations"" sheetId=""1"" r:id=""rId1""/>
    <sheet name=""Bio Text"" sheetId=""2"" r:id=""rId2""/>
    <sheet name=""Captions"" sheetId=""3"" r:id=""rId3""/>
  </sheets>
</workbook>");

        WriteEntry(zip, "xl/_rels/workbook.xml.rels",
            @"<?xml version=""1.0"" encoding=""UTF-8"" standalone=""yes""?>
<Relationships xmlns=""http://schemas.openxmlformats.org/package/2006/relationships"">
  <Relationship Id=""rId1"" Type=""http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet"" Target=""worksheets/sheet1.xml""/>
  <Relationship Id=""rId2"" Type=""http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet"" Target=""worksheets/sheet2.xml""/>
  <Relationship Id=""rId3"" Type=""http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet"" Target=""worksheets/sheet3.xml""/>
  <Relationship Id=""rId4"" Type=""http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles"" Target=""styles.xml""/>
</Relationships>");

        WriteEntry(zip, "xl/styles.xml",
            @"<?xml version=""1.0"" encoding=""UTF-8"" standalone=""yes""?>
<styleSheet xmlns=""http://schemas.openxmlformats.org/spreadsheetml/2006/main"">
  <fonts count=""1""><font><sz val=""11""/><name val=""Calibri""/></font></fonts>
  <fills count=""1""><fill><patternFill patternType=""none""/></fill></fills>
  <borders count=""1""><border><left/><right/><top/><bottom/><diagonal/></border></borders>
  <cellStyleXfs count=""1""><xf numFmtId=""0"" fontId=""0"" fillId=""0"" borderId=""0""/></cellStyleXfs>
  <cellXfs count=""1""><xf numFmtId=""0"" fontId=""0"" fillId=""0"" borderId=""0"" xfId=""0""/></cellXfs>
</styleSheet>");

        var locationsSheet = malformedLocationsSheet
            ? @"<?xml version=""1.0"" encoding=""UTF-8"" standalone=""yes""?><worksheet>"
            : omitCoordinateCells
                ? @"<?xml version=""1.0"" encoding=""UTF-8"" standalone=""yes""?>
<worksheet xmlns=""http://schemas.openxmlformats.org/spreadsheetml/2006/main"">
  <sheetData>
    <row r=""1"">
      <c r=""A1"" t=""inlineStr""><is><t>Name</t></is></c>
    </row>
    <row r=""2"">
      <c r=""A2"" t=""inlineStr""><is><t>Kevin</t></is></c>
    </row>
  </sheetData>
</worksheet>"
                : @"<?xml version=""1.0"" encoding=""UTF-8"" standalone=""yes""?>
<worksheet xmlns=""http://schemas.openxmlformats.org/spreadsheetml/2006/main"">
  <sheetData>
    <row r=""1"">
      <c r=""A1"" t=""inlineStr""><is><t>Name</t></is></c>
      <c r=""B1"" t=""inlineStr""><is><t>Coordinate X</t></is></c>
      <c r=""C1"" t=""inlineStr""><is><t>Coordinates Y</t></is></c>
      <c r=""D1"" t=""inlineStr""><is><t>Address</t></is></c>
      <c r=""E1"" t=""inlineStr""><is><t>Coordinate X halfsize</t></is></c>
      <c r=""F1"" t=""inlineStr""><is><t>Coordinate Y halfsize</t></is></c>
      <c r=""G1"" t=""inlineStr""><is><t>Image 1 filename</t></is></c>
      <c r=""H1"" t=""inlineStr""><is><t>Image 2 filename</t></is></c>
    </row>
    <row r=""2"">
      <c r=""A2"" t=""inlineStr""><is><t>Kevin</t></is></c>
      <c r=""B2""><v>4920</v></c>
      <c r=""C2""><v>5153</v></c>
      <c r=""D2"" t=""inlineStr""><is><t>New York, NY</t></is></c>
      <c r=""E2""><v>2460</v></c>
      <c r=""F2""><v>2577</v></c>
      <c r=""G2"" t=""inlineStr""><is><t>1-letter-product-pic.jpg</t></is></c>
      <c r=""H2"" t=""inlineStr""><is><t>2-second-image.jpg</t></is></c>
    </row>
    <row r=""3"">
      <c r=""A3"" t=""inlineStr""><is><t>Half Only</t></is></c>
      <c r=""E3""><v>6378</v></c>
      <c r=""F3""><v>2933</v></c>
    </row>
  </sheetData>
</worksheet>";

        WriteEntry(zip, "xl/worksheets/sheet1.xml", locationsSheet);

        WriteEntry(zip, "xl/worksheets/sheet2.xml",
            @"<?xml version=""1.0"" encoding=""UTF-8"" standalone=""yes""?>
<worksheet xmlns=""http://schemas.openxmlformats.org/spreadsheetml/2006/main"">
  <sheetData>
    <row r=""1"">
      <c r=""A1"" t=""inlineStr""><is><t>Name</t></is></c>
      <c r=""B1"" t=""inlineStr""><is><t>Bio Text</t></is></c>
    </row>
    <row r=""2"">
      <c r=""A2"" t=""inlineStr""><is><t>Kevin</t></is></c>
      <c r=""B2"" t=""inlineStr""><is><t>Bio text from workbook</t></is></c>
    </row>
  </sheetData>
</worksheet>");

        WriteEntry(zip, "xl/worksheets/sheet3.xml",
            @"<?xml version=""1.0"" encoding=""UTF-8"" standalone=""yes""?>
<worksheet xmlns=""http://schemas.openxmlformats.org/spreadsheetml/2006/main"">
  <sheetData>
    <row r=""1"">
      <c r=""A1"" t=""inlineStr""><is><t>Name</t></is></c>
      <c r=""B1"" t=""inlineStr""><is><t>Image filename</t></is></c>
      <c r=""C1"" t=""inlineStr""><is><t>Caption text</t></is></c>
    </row>
    <row r=""2"">
      <c r=""A2"" t=""inlineStr""><is><t>Kevin</t></is></c>
      <c r=""B2"" t=""inlineStr""><is><t>1-letter-product-pic.jpg</t></is></c>
      <c r=""C2"" t=""inlineStr""><is><t>CAPTION</t></is></c>
    </row>
    <row r=""3"">
      <c r=""A3"" t=""inlineStr""><is><t>Kevin</t></is></c>
      <c r=""B3"" t=""inlineStr""><is><t>2-second-image.jpg</t></is></c>
      <c r=""C3"" t=""inlineStr""><is><t>CAPTION</t></is></c>
    </row>
  </sheetData>
</worksheet>");
    }

    /// <summary>
    /// Builds a minimal OOXML workbook that stores text in sharedStrings.xml (cell type t="s").
    /// Numeric coordinate cells remain inline &lt;v&gt; values (no shared-string index).
    /// </summary>
    private static void WriteSharedStringWorkbook(string excelPath)
    {
        using var zip = ZipFile.Open(excelPath, ZipArchiveMode.Create);

        WriteEntry(zip, "[Content_Types].xml",
            @"<?xml version=""1.0"" encoding=""UTF-8"" standalone=""yes""?>
<Types xmlns=""http://schemas.openxmlformats.org/package/2006/content-types"">
  <Default Extension=""rels"" ContentType=""application/vnd.openxmlformats-package.relationships+xml""/>
  <Default Extension=""xml"" ContentType=""application/xml""/>
  <Override PartName=""/xl/workbook.xml"" ContentType=""application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml""/>
  <Override PartName=""/xl/worksheets/sheet1.xml"" ContentType=""application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml""/>
  <Override PartName=""/xl/worksheets/sheet2.xml"" ContentType=""application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml""/>
  <Override PartName=""/xl/worksheets/sheet3.xml"" ContentType=""application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml""/>
  <Override PartName=""/xl/sharedStrings.xml"" ContentType=""application/vnd.openxmlformats-officedocument.spreadsheetml.sharedStrings+xml""/>
  <Override PartName=""/xl/styles.xml"" ContentType=""application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml""/>
</Types>");

        WriteEntry(zip, "_rels/.rels",
            @"<?xml version=""1.0"" encoding=""UTF-8"" standalone=""yes""?>
<Relationships xmlns=""http://schemas.openxmlformats.org/package/2006/relationships"">
  <Relationship Id=""rId1"" Type=""http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument"" Target=""xl/workbook.xml""/>
</Relationships>");

        WriteEntry(zip, "xl/workbook.xml",
            @"<?xml version=""1.0"" encoding=""UTF-8"" standalone=""yes""?>
<workbook xmlns=""http://schemas.openxmlformats.org/spreadsheetml/2006/main"" xmlns:r=""http://schemas.openxmlformats.org/officeDocument/2006/relationships"">
  <sheets>
    <sheet name=""Locations"" sheetId=""1"" r:id=""rId1""/>
    <sheet name=""Bio Text"" sheetId=""2"" r:id=""rId2""/>
    <sheet name=""Captions"" sheetId=""3"" r:id=""rId3""/>
  </sheets>
</workbook>");

        WriteEntry(zip, "xl/_rels/workbook.xml.rels",
            @"<?xml version=""1.0"" encoding=""UTF-8"" standalone=""yes""?>
<Relationships xmlns=""http://schemas.openxmlformats.org/package/2006/relationships"">
  <Relationship Id=""rId1"" Type=""http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet"" Target=""worksheets/sheet1.xml""/>
  <Relationship Id=""rId2"" Type=""http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet"" Target=""worksheets/sheet2.xml""/>
  <Relationship Id=""rId3"" Type=""http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet"" Target=""worksheets/sheet3.xml""/>
  <Relationship Id=""rId4"" Type=""http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles"" Target=""styles.xml""/>
  <Relationship Id=""rId5"" Type=""http://schemas.openxmlformats.org/officeDocument/2006/relationships/sharedStrings"" Target=""sharedStrings.xml""/>
</Relationships>");

        WriteEntry(zip, "xl/styles.xml",
            @"<?xml version=""1.0"" encoding=""UTF-8"" standalone=""yes""?>
<styleSheet xmlns=""http://schemas.openxmlformats.org/spreadsheetml/2006/main"">
  <fonts count=""1""><font><sz val=""11""/><name val=""Calibri""/></font></fonts>
  <fills count=""1""><fill><patternFill patternType=""none""/></fill></fills>
  <borders count=""1""><border><left/><right/><top/><bottom/><diagonal/></border></borders>
  <cellStyleXfs count=""1""><xf numFmtId=""0"" fontId=""0"" fillId=""0"" borderId=""0""/></cellStyleXfs>
  <cellXfs count=""1""><xf numFmtId=""0"" fontId=""0"" fillId=""0"" borderId=""0"" xfId=""0""/></cellXfs>
</styleSheet>");

        // Indices: 0 Name, 1 Address, 2 X half, 3 Y half, 4 Image1 hdr, 5 Image2 hdr,
        // 6 Shared Kevin, 7 New York, 8 img1, 9 img2, 10 Shared Half,
        // 11 Bio Name hdr, 12 Bio Text hdr, 13 Shared bio text,
        // 14 Cap Name hdr, 15 Image filename hdr, 16 Caption text hdr, 17 SHARED CAPTION
        WriteEntry(zip, "xl/sharedStrings.xml",
            @"<?xml version=""1.0"" encoding=""UTF-8"" standalone=""yes""?>
<sst xmlns=""http://schemas.openxmlformats.org/spreadsheetml/2006/main"" count=""18"" uniqueCount=""18"">
  <si><t>Name</t></si>
  <si><t>Address</t></si>
  <si><t>Coordinate X halfsize</t></si>
  <si><t>Coordinate Y halfsize</t></si>
  <si><t>Image 1 filename</t></si>
  <si><t>Image 2 filename</t></si>
  <si><t>Shared Kevin</t></si>
  <si><t>New York, NY</t></si>
  <si><t>1-letter-product-pic.jpg</t></si>
  <si><t>2-second-image.jpg</t></si>
  <si><t>Shared Half</t></si>
  <si><t>Name</t></si>
  <si><t>Bio Text</t></si>
  <si><t>Shared bio text</t></si>
  <si><t>Name</t></si>
  <si><t>Image filename</t></si>
  <si><t>Caption text</t></si>
  <si><t>SHARED CAPTION</t></si>
</sst>");

        WriteEntry(zip, "xl/worksheets/sheet1.xml",
            @"<?xml version=""1.0"" encoding=""UTF-8"" standalone=""yes""?>
<worksheet xmlns=""http://schemas.openxmlformats.org/spreadsheetml/2006/main"">
  <sheetData>
    <row r=""1"">
      <c r=""A1"" t=""s""><v>0</v></c>
      <c r=""D1"" t=""s""><v>1</v></c>
      <c r=""E1"" t=""s""><v>2</v></c>
      <c r=""F1"" t=""s""><v>3</v></c>
      <c r=""G1"" t=""s""><v>4</v></c>
      <c r=""H1"" t=""s""><v>5</v></c>
    </row>
    <row r=""2"">
      <c r=""A2"" t=""s""><v>6</v></c>
      <c r=""D2"" t=""s""><v>7</v></c>
      <c r=""E2""><v>2460</v></c>
      <c r=""F2""><v>2577</v></c>
      <c r=""G2"" t=""s""><v>8</v></c>
      <c r=""H2"" t=""s""><v>9</v></c>
    </row>
    <row r=""3"">
      <c r=""A3"" t=""s""><v>10</v></c>
      <c r=""E3""><v>6378</v></c>
      <c r=""F3""><v>2933</v></c>
    </row>
  </sheetData>
</worksheet>");

        WriteEntry(zip, "xl/worksheets/sheet2.xml",
            @"<?xml version=""1.0"" encoding=""UTF-8"" standalone=""yes""?>
<worksheet xmlns=""http://schemas.openxmlformats.org/spreadsheetml/2006/main"">
  <sheetData>
    <row r=""1"">
      <c r=""A1"" t=""s""><v>11</v></c>
      <c r=""B1"" t=""s""><v>12</v></c>
    </row>
    <row r=""2"">
      <c r=""A2"" t=""s""><v>6</v></c>
      <c r=""B2"" t=""s""><v>13</v></c>
    </row>
  </sheetData>
</worksheet>");

        WriteEntry(zip, "xl/worksheets/sheet3.xml",
            @"<?xml version=""1.0"" encoding=""UTF-8"" standalone=""yes""?>
<worksheet xmlns=""http://schemas.openxmlformats.org/spreadsheetml/2006/main"">
  <sheetData>
    <row r=""1"">
      <c r=""A1"" t=""s""><v>14</v></c>
      <c r=""B1"" t=""s""><v>15</v></c>
      <c r=""C1"" t=""s""><v>16</v></c>
    </row>
    <row r=""2"">
      <c r=""A2"" t=""s""><v>6</v></c>
      <c r=""B2"" t=""s""><v>8</v></c>
      <c r=""C2"" t=""s""><v>17</v></c>
    </row>
  </sheetData>
</worksheet>");
    }

    private static void WriteEntry(ZipArchive zip, string path, string contents)
    {
        var entry = zip.CreateEntry(path);
        using var stream = entry.Open();
        using var writer = new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        writer.Write(contents);
    }
}
