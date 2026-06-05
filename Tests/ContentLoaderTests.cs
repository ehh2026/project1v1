using System;
using System.IO;
using System.Threading.Tasks;
using InteractiveWorldMap.Models;
using InteractiveWorldMap.Services;
using InteractiveWorldMap.Tests.TestHelpers;
using Xunit;

namespace InteractiveWorldMap.Tests;

/// <summary>
/// Unit tests for ContentLoader error paths and validation.
/// </summary>
public class ContentLoaderTests
{
    [Fact]
    public void ValidateContentFolder_MissingFolder_ReturnsFalse()
    {
        var loader = new ContentLoader(new MockLogger())
        {
            ContentFolderPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"))
        };

        Assert.False(loader.ValidateContentFolder());
    }

    [Fact]
    public void ValidateContentFolder_WithMapFile_ReturnsTrue()
    {
        var tempDir = CreateContentFolderWithMap();
        try
        {
            var loader = new ContentLoader(new MockLogger()) { ContentFolderPath = tempDir };
            Assert.True(loader.ValidateContentFolder());
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void ValidateContentFolder_MissingMap_ReturnsFalse()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "iwm-cl-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            var loader = new ContentLoader(new MockLogger()) { ContentFolderPath = tempDir };
            Assert.False(loader.ValidateContentFolder());
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task LoadLocationsAsync_NoSources_ReturnsEmptyList()
    {
        var tempDir = CreateContentFolderWithMap();
        try
        {
            var loader = CreateLoaderForJsonTests(tempDir);
            var locations = await loader.LoadLocationsAsync();
            Assert.Empty(locations);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task LoadLocationsAsync_ValidLocationsJson_ReturnsLocations()
    {
        var tempDir = CreateContentFolderWithMap();
        try
        {
            File.WriteAllText(
                Path.Combine(tempDir, "locations.json"),
                "[{\"Id\":\"a1\",\"Name\":\"Paris\",\"PixelX\":100,\"PixelY\":200}]");

            var loader = CreateLoaderForJsonTests(tempDir);
            var locations = await loader.LoadLocationsAsync();

            Assert.Single(locations);
            Assert.Equal("Paris", locations[0].Name);
            Assert.True(loader.IsInitialized);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task LoadLocationsAsync_InvalidJson_ThrowsInvalidOperationException()
    {
        var tempDir = CreateContentFolderWithMap();
        try
        {
            File.WriteAllText(Path.Combine(tempDir, "locations.json"), "{ bad json");
            var loader = CreateLoaderForJsonTests(tempDir);

            await Assert.ThrowsAsync<InvalidOperationException>(() => loader.LoadLocationsAsync());
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task LoadLocationContentAsync_NullLocation_Throws()
    {
        var loader = new ContentLoader(new MockLogger());
        await Assert.ThrowsAsync<ArgumentNullException>(() => loader.LoadLocationContentAsync(null!));
    }

    [Fact]
    public async Task LoadLocationContentAsync_MissingFolder_ReturnsNull()
    {
        var tempDir = CreateContentFolderWithMap();
        try
        {
            var loader = new ContentLoader(new MockLogger()) { ContentFolderPath = tempDir };
            var location = new Location { Name = "Nonexistent", Id = "x" };

            var result = await loader.LoadLocationContentAsync(location);

            Assert.Null(result);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void LoadPinPartGeometry_WithValidJson_ReturnsEntries()
    {
        var tempDir = CreateContentFolderWithMap();
        try
        {
            var partsDir = Path.Combine(tempDir, "Pins_v2", "parts");
            Directory.CreateDirectory(partsDir);
            File.WriteAllText(
                Path.Combine(partsDir, "pin_part_geometry.json"),
                @"
                {
                  ""pin_01"": {
                    ""head_file"": ""pin_01_head.png"",
                    ""shaft_file"": ""pin_01_shaft.png"",
                    ""head"": {
                      ""local_center"": { ""x"": 10.0, ""y"": 12.0 },
                      ""local_attach"": { ""x"": 14.0, ""y"": 22.0 },
                      ""stub_direction_deg"": 150.0
                    },
                    ""shaft"": {
                      ""local_tip"": { ""x"": 50.0, ""y"": 90.0 },
                      ""local_join"": { ""x"": 11.0, ""y"": 5.0 },
                      ""native_angle_deg"": 330.0,
                      ""native_length"": 120.0,
                      ""segmentation"": {
                        ""tip_cap_length"": 18.0,
                        ""head_cap_length"": 19.0,
                        ""stretch_start_distance"": 18.0,
                        ""stretch_end_distance"": 101.0
                      }
                    }
                  }
                }");

            var loader = new ContentLoader(new MockLogger()) { ContentFolderPath = tempDir };

            var geometry = loader.LoadPinPartGeometry("Pins_v2\\parts\\pin_part_geometry.json");

            Assert.Single(geometry);
            Assert.True(geometry.ContainsKey("pin_01"));
            Assert.Equal(150.0, geometry["pin_01"].Head.StubDirectionDeg);
            Assert.Equal(120.0, geometry["pin_01"].Shaft.NativeLength);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    /// <summary>
    /// Loader that skips the repo Excel file so JSON-only tests stay isolated.
    /// </summary>
    private static ContentLoader CreateLoaderForJsonTests(string contentFolderPath)
    {
        return new ContentLoader(new MockLogger())
        {
            ContentFolderPath = contentFolderPath,
            ExcelCoordinateFilePath = Path.Combine(contentFolderPath, "no-excel-for-test.xlsx"),
        };
    }

    private static string CreateContentFolderWithMap()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "iwm-cl-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        File.WriteAllText(Path.Combine(tempDir, ContentFileNames.WorldMapFileName), "fake-image-data");
        return tempDir;
    }
}
