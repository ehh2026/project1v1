using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using InteractiveWorldMap.Models;
using InteractiveWorldMap.Services;
using InteractiveWorldMap.Utilities;
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
        var loader = new ContentLoader(new MockLogger(), new ContentSetResolver())
        {
            ContentFolderPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"))
        };

        Assert.False(loader.ValidateContentFolder());
    }

    [Fact]
    public void ValidateContentFolder_WithMapAndCoordinates_ReturnsTrue()
    {
        var tempDir = ContentLoaderTestFixtures.CreateContentFolderWithMap();
        try
        {
            File.WriteAllText(Path.Combine(tempDir, ContentFileNames.LocationsJsonFileName), "[]");
            var loader = new ContentLoader(new MockLogger(), new ContentSetResolver()) { ContentFolderPath = tempDir };
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
            var loader = new ContentLoader(new MockLogger(), new ContentSetResolver()) { ContentFolderPath = tempDir };
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
        var tempDir = ContentLoaderTestFixtures.CreateContentFolderWithMap();
        try
        {
            var loader = ContentLoaderTestFixtures.CreateLoaderForJsonTests(tempDir);
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
        var tempDir = ContentLoaderTestFixtures.CreateContentFolderWithMap();
        try
        {
            File.WriteAllText(
                Path.Combine(tempDir, "locations.json"),
                "[{\"Id\":\"a1\",\"Name\":\"Paris\",\"PixelX\":100,\"PixelY\":200}]");

            var loader = ContentLoaderTestFixtures.CreateLoaderForJsonTests(tempDir);
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
    public async Task LoadClustersAsync_UsesCurrentClusterDistanceThreshold()
    {
        var tempDir = ContentLoaderTestFixtures.CreateContentFolderWithMap();
        try
        {
            File.WriteAllText(
                Path.Combine(tempDir, "locations.json"),
                "[" +
                "{\"Id\":\"a1\",\"Name\":\"Alpha\",\"PixelX\":100,\"PixelY\":100}," +
                "{\"Id\":\"b1\",\"Name\":\"Beta\",\"PixelX\":140,\"PixelY\":100}" +
                "]");

            var closeThresholdLoader = ContentLoaderTestFixtures.CreateLoaderForJsonTests(tempDir);
            closeThresholdLoader.ClusterDistanceThreshold = 50.0;
            var closeThresholdClusters = await closeThresholdLoader.LoadClustersAsync();

            var smallThresholdLoader = ContentLoaderTestFixtures.CreateLoaderForJsonTests(tempDir);
            smallThresholdLoader.ClusterDistanceThreshold = 10.0;
            var smallThresholdClusters = await smallThresholdLoader.LoadClustersAsync();

            Assert.Single(closeThresholdClusters);
            Assert.Equal(2, smallThresholdClusters.Count);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task LoadLocationsAsync_InvalidJson_ThrowsInvalidOperationException()
    {
        var tempDir = ContentLoaderTestFixtures.CreateContentFolderWithMap();
        try
        {
            File.WriteAllText(Path.Combine(tempDir, "locations.json"), "{ bad json");
            var loader = ContentLoaderTestFixtures.CreateLoaderForJsonTests(tempDir);

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
        var loader = new ContentLoader(new MockLogger(), new ContentSetResolver());
        await Assert.ThrowsAsync<ArgumentNullException>(() => loader.LoadLocationContentAsync(null!));
    }

    [Fact]
    public async Task LoadLocationContentAsync_MissingFolder_ReturnsNull()
    {
        var tempDir = ContentLoaderTestFixtures.CreateContentFolderWithMap();
        try
        {
            var loader = new ContentLoader(new MockLogger(), new ContentSetResolver()) { ContentFolderPath = tempDir };
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
        var tempDir = ContentLoaderTestFixtures.CreateContentFolderWithMap();
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

            var loader = new ContentLoader(new MockLogger(), new ContentSetResolver()) { ContentFolderPath = tempDir };

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

    // -------------------------------------------------------------------------
    // LoadAllLocationImagesAsync
    // -------------------------------------------------------------------------

    [Fact]
    public async Task LoadAllLocationImagesAsync_NullLocation_Throws()
    {
        var loader = new ContentLoader(new MockLogger(), new ContentSetResolver());
        await Assert.ThrowsAsync<ArgumentNullException>(() => loader.LoadAllLocationImagesAsync(null!));
    }

    [Fact]
    public async Task LoadAllLocationImagesAsync_MissingFolder_ReturnsEmpty()
    {
        var tempDir = ContentLoaderTestFixtures.CreateContentFolderWithMap();
        try
        {
            var loader = new ContentLoader(new MockLogger(), new ContentSetResolver()) { ContentFolderPath = tempDir };
            var location = new Location { Name = "Nonexistent", Id = "x" };

            var result = await loader.LoadAllLocationImagesAsync(location);

            Assert.Empty(result);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task LoadAllLocationImagesAsync_NoImages_ReturnsEmpty()
    {
        var tempDir = ContentLoaderTestFixtures.CreateContentFolderWithMap();
        try
        {
            var locationFolder = Path.Combine(tempDir, "Paris");
            Directory.CreateDirectory(locationFolder);
            File.WriteAllText(Path.Combine(locationFolder, "notes.txt"), "just text");

            var loader = new ContentLoader(new MockLogger(), new ContentSetResolver()) { ContentFolderPath = tempDir };
            var location = new Location { Name = "Paris", Id = "p" };

            var result = await loader.LoadAllLocationImagesAsync(location);

            Assert.Empty(result);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    // -------------------------------------------------------------------------
    // LoadAllLocationImagesWithTranslationsAsync
    // -------------------------------------------------------------------------

    [Fact]
    public async Task LoadAllLocationImagesWithTranslationsAsync_NullLocation_Throws()
    {
        var loader = new ContentLoader(new MockLogger(), new ContentSetResolver());
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => loader.LoadAllLocationImagesWithTranslationsAsync(null!));
    }

    [Fact]
    public async Task LoadAllLocationImagesWithTranslationsAsync_MissingFolder_ReturnsEmpty()
    {
        var tempDir = ContentLoaderTestFixtures.CreateContentFolderWithMap();
        try
        {
            var loader = new ContentLoader(new MockLogger(), new ContentSetResolver()) { ContentFolderPath = tempDir };
            var location = new Location { Name = "Nowhere", Id = "n" };

            var result = await loader.LoadAllLocationImagesWithTranslationsAsync(location);

            Assert.Empty(result);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task LoadAllLocationImagesWithTranslationsAsync_NoImages_ReturnsEmpty()
    {
        var tempDir = ContentLoaderTestFixtures.CreateContentFolderWithMap();
        try
        {
            var locationFolder = Path.Combine(tempDir, "Rome");
            Directory.CreateDirectory(locationFolder);
            File.WriteAllText(Path.Combine(locationFolder, "readme.txt"), "no images here");

            var loader = new ContentLoader(new MockLogger(), new ContentSetResolver()) { ContentFolderPath = tempDir };
            var location = new Location { Name = "Rome", Id = "r" };

            var result = await loader.LoadAllLocationImagesWithTranslationsAsync(location);

            Assert.Empty(result);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task LoadAllLocationImagesWithTranslationsAsync_LoadsCaptionSidecarByImagePrefix()
    {
        var tempDir = ContentLoaderTestFixtures.CreateContentFolderWithMap();
        try
        {
            var locationFolder = Path.Combine(tempDir, "Test");
            Directory.CreateDirectory(locationFolder);
            ContentLoaderTestFixtures.SaveTinyPng(Path.Combine(locationFolder, "1-letter.png"));
            File.WriteAllText(Path.Combine(locationFolder, "1-letter.txt"), "Translated text");
            File.WriteAllText(
                Path.Combine(locationFolder, "1-letter-caption.txt"),
                "Caption text about this letter.");

            var loader = new ContentLoader(new MockLogger(), new ContentSetResolver()) { ContentFolderPath = tempDir };
            var location = new Location { Name = "Test", Id = "t" };

            var result = await loader.LoadAllLocationImagesWithTranslationsAsync(location);

            var image = Assert.Single(result);
            Assert.Equal("Translated text", image.TranslationText);
            Assert.Equal("Caption text about this letter.", image.CaptionText);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task LoadAllLocationImagesWithTranslationsAsync_DoesNotUseDidacticTextAsTranslation()
    {
        var tempDir = ContentLoaderTestFixtures.CreateContentFolderWithMap();
        try
        {
            var locationFolder = Path.Combine(tempDir, "BioOnly");
            Directory.CreateDirectory(locationFolder);
            ContentLoaderTestFixtures.SaveTinyPng(Path.Combine(locationFolder, "1-photo.png"));

            var loader = new ContentLoader(new MockLogger(), new ContentSetResolver()) { ContentFolderPath = tempDir };
            var location = new Location
            {
                Name = "BioOnly",
                Id = "b",
                DidacticText = "This biography belongs in the didactic window, not the Translate overlay."
            };

            var result = await loader.LoadAllLocationImagesWithTranslationsAsync(location);

            var image = Assert.Single(result);
            Assert.Null(image.TranslationText);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task LoadAllLocationImagesWithTranslationsAsync_DoesNotUseDidacticFileAsTranslation()
    {
        var tempDir = ContentLoaderTestFixtures.CreateContentFolderWithMap();
        try
        {
            var locationFolder = Path.Combine(tempDir, "DidacticFile");
            Directory.CreateDirectory(locationFolder);
            ContentLoaderTestFixtures.SaveTinyPng(Path.Combine(locationFolder, "1-photo.png"));
            File.WriteAllText(Path.Combine(locationFolder, "didactic.txt"), "Folder biography only.");

            var loader = new ContentLoader(new MockLogger(), new ContentSetResolver()) { ContentFolderPath = tempDir };
            var location = new Location { Name = "DidacticFile", Id = "d" };

            var result = await loader.LoadAllLocationImagesWithTranslationsAsync(location);

            var image = Assert.Single(result);
            Assert.Null(image.TranslationText);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task LoadAllLocationImagesWithTranslationsAsync_SkipsMissingListedImages()
    {
        var tempDir = ContentLoaderTestFixtures.CreateContentFolderWithMap();
        try
        {
            var locationFolder = Path.Combine(tempDir, "SkipMissing");
            Directory.CreateDirectory(locationFolder);
            ContentLoaderTestFixtures.SaveTinyPng(Path.Combine(locationFolder, "2-present.png"));

            var logger = new MockLogger();
            var loader = new ContentLoader(logger, new ContentSetResolver()) { ContentFolderPath = tempDir };
            var location = new Location
            {
                Name = "SkipMissing",
                Id = "s",
                ImageFileNames = { "1-missing.png", "2-present.png" }
            };

            var result = await loader.LoadAllLocationImagesWithTranslationsAsync(location);

            var image = Assert.Single(result);
            Assert.NotNull(image.Image);
            Assert.Contains(
                logger.WarningMessages,
                message => message.Contains("Missing image file for location SkipMissing"));
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    // -------------------------------------------------------------------------
    // LoadLocationContentAsync — additional coverage
    // -------------------------------------------------------------------------

    [Fact]
    public async Task LoadLocationContentAsync_NoImages_ReturnsNull()
    {
        var tempDir = ContentLoaderTestFixtures.CreateContentFolderWithMap();
        try
        {
            var locationFolder = Path.Combine(tempDir, "Berlin");
            Directory.CreateDirectory(locationFolder);
            File.WriteAllText(Path.Combine(locationFolder, "notes.txt"), "text only");

            var loader = new ContentLoader(new MockLogger(), new ContentSetResolver()) { ContentFolderPath = tempDir };
            var location = new Location { Name = "Berlin", Id = "b" };

            var result = await loader.LoadLocationContentAsync(location);

            Assert.Null(result);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task LoadLocationContentAsync_SecondCall_ReturnsSameCachedInstance()
    {
        var tempDir = ContentLoaderTestFixtures.CreateContentFolderWithMap();
        try
        {
            var locationFolder = Path.Combine(tempDir, "Paris");
            Directory.CreateDirectory(locationFolder);
            ContentLoaderTestFixtures.SaveTinyPng(Path.Combine(locationFolder, "1.png"));

            var loader = new ContentLoader(new MockLogger(), new ContentSetResolver())
            {
                ContentFolderPath = tempDir,
                MaxCachedLocations = 0
            };
            var location = new Location { Name = "Paris", Id = "loc_paris" };

            var first = await loader.LoadLocationContentAsync(location);
            var second = await loader.LoadLocationContentAsync(location);

            Assert.NotNull(first);
            Assert.Same(first, second);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task LoadLocationContentAsync_CachesByLocationId_NotName()
    {
        var tempDir = ContentLoaderTestFixtures.CreateContentFolderWithMap();
        try
        {
            var folderA = Path.Combine(tempDir, "SameName");
            Directory.CreateDirectory(folderA);
            ContentLoaderTestFixtures.SaveTinyPng(Path.Combine(folderA, "1.png"));

            var logger = new MockLogger();
            var loader = new ContentLoader(logger, new ContentSetResolver()) { ContentFolderPath = tempDir };
            var first = new Location { Name = "SameName", Id = "id_one" };
            var second = new Location { Name = "SameName", Id = "id_two" };

            var imageOne = await loader.LoadLocationContentAsync(first);
            var imageTwo = await loader.LoadLocationContentAsync(second);

            Assert.NotNull(imageOne);
            Assert.NotNull(imageTwo);
            // Distinct Ids ⇒ independent cache entries (both loads perform work / log cache miss then cache).
            Assert.Contains(logger.InfoMessages, m => m.Contains("key=id_one") && m.Contains("Successfully loaded"));
            Assert.Contains(logger.InfoMessages, m => m.Contains("key=id_two") && m.Contains("Successfully loaded"));
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task LoadLocationContentAsync_WhenMaxCachedLocationsExceeded_EvictsLeastRecentlyUsed()
    {
        var tempDir = ContentLoaderTestFixtures.CreateContentFolderWithMap();
        try
        {
            foreach (var name in new[] { "A", "B", "C" })
            {
                var folder = Path.Combine(tempDir, name);
                Directory.CreateDirectory(folder);
                ContentLoaderTestFixtures.SaveTinyPng(Path.Combine(folder, "1.png"));
            }

            var logger = new MockLogger();
            var loader = new ContentLoader(logger, new ContentSetResolver())
            {
                ContentFolderPath = tempDir,
                MaxCachedLocations = 2
            };

            var locA = new Location { Name = "A", Id = "id_a" };
            var locB = new Location { Name = "B", Id = "id_b" };
            var locC = new Location { Name = "C", Id = "id_c" };

            var imageA = await loader.LoadLocationContentAsync(locA);
            var imageB = await loader.LoadLocationContentAsync(locB);
            // Touch A so B becomes LRU
            var imageAAgain = await loader.LoadLocationContentAsync(locA);
            Assert.Same(imageA, imageAAgain);

            var imageC = await loader.LoadLocationContentAsync(locC);

            Assert.NotNull(imageA);
            Assert.NotNull(imageB);
            Assert.NotNull(imageC);
            Assert.Contains(logger.InfoMessages, m => m.Contains("Evicted cached location content") && m.Contains("key=id_b"));

            // B was evicted: reload is a fresh load, not the same instance.
            var imageBReloaded = await loader.LoadLocationContentAsync(locB);
            Assert.NotNull(imageBReloaded);
            Assert.NotSame(imageB, imageBReloaded);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task MaxCachedLocations_WhenLowered_EvictsImmediately()
    {
        var tempDir = ContentLoaderTestFixtures.CreateContentFolderWithMap();
        try
        {
            foreach (var name in new[] { "A", "B", "C" })
            {
                var folder = Path.Combine(tempDir, name);
                Directory.CreateDirectory(folder);
                ContentLoaderTestFixtures.SaveTinyPng(Path.Combine(folder, "1.png"));
            }

            var logger = new MockLogger();
            var loader = new ContentLoader(logger, new ContentSetResolver())
            {
                ContentFolderPath = tempDir,
                MaxCachedLocations = 0
            };

            var locA = new Location { Name = "A", Id = "id_a" };
            var locB = new Location { Name = "B", Id = "id_b" };
            var locC = new Location { Name = "C", Id = "id_c" };

            var imageA = await loader.LoadLocationContentAsync(locA);
            var imageB = await loader.LoadLocationContentAsync(locB);
            var imageC = await loader.LoadLocationContentAsync(locC);
            Assert.NotNull(imageA);
            Assert.NotNull(imageB);
            Assert.NotNull(imageC);

            // Touch A and C so B is LRU when the limit drops to 2.
            Assert.Same(imageA, await loader.LoadLocationContentAsync(locA));
            Assert.Same(imageC, await loader.LoadLocationContentAsync(locC));

            loader.MaxCachedLocations = 2;

            Assert.Contains(logger.InfoMessages, m => m.Contains("Evicted cached location content") && m.Contains("key=id_b"));

            // A and C must still be cached immediately after the shrink (before any new inserts).
            Assert.Same(imageA, await loader.LoadLocationContentAsync(locA));
            Assert.Same(imageC, await loader.LoadLocationContentAsync(locC));

            var imageBReloaded = await loader.LoadLocationContentAsync(locB);
            Assert.NotNull(imageBReloaded);
            Assert.NotSame(imageB, imageBReloaded);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task LoadLocationContentAsync_HeavyFile_RaisesLargeImageDetected_WhenDiagnosticsEnabled()
    {
        var tempDir = ContentLoaderTestFixtures.CreateContentFolderWithMap();
        try
        {
            var folder = Path.Combine(tempDir, "Big");
            Directory.CreateDirectory(folder);
            ContentLoaderTestFixtures.SaveTinyPng(Path.Combine(folder, "1.png"));

            var loader = new ContentLoader(new MockLogger(), new ContentSetResolver())
            {
                ContentFolderPath = tempDir,
                EnableImageDiagnostics = true,
                LargeImageWarnBytes = 1 // any real file exceeds one byte
            };

            string? firedFile = null;
            long firedBytes = 0;
            loader.LargeImageDetected += (file, bytes) => { firedFile = file; firedBytes = bytes; };

            await loader.LoadLocationContentAsync(new Location { Name = "Big", Id = "id_big" });

            Assert.Equal("1.png", firedFile);
            Assert.True(firedBytes > 0);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task LoadLocationContentAsync_SubThresholdFile_DoesNotRaiseLargeImageDetected()
    {
        var tempDir = ContentLoaderTestFixtures.CreateContentFolderWithMap();
        try
        {
            var folder = Path.Combine(tempDir, "Small");
            Directory.CreateDirectory(folder);
            ContentLoaderTestFixtures.SaveTinyPng(Path.Combine(folder, "1.png"));

            var loader = new ContentLoader(new MockLogger(), new ContentSetResolver())
            {
                ContentFolderPath = tempDir,
                EnableImageDiagnostics = true,
                LargeImageWarnBytes = 10_000_000 // tiny png is well under 10 MB
            };

            var fired = false;
            loader.LargeImageDetected += (_, _) => fired = true;

            await loader.LoadLocationContentAsync(new Location { Name = "Small", Id = "id_small" });

            Assert.False(fired);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task LoadLocationContentAsync_DiagnosticsDisabled_DoesNotRaise_EvenOverThreshold()
    {
        var tempDir = ContentLoaderTestFixtures.CreateContentFolderWithMap();
        try
        {
            var folder = Path.Combine(tempDir, "Big");
            Directory.CreateDirectory(folder);
            ContentLoaderTestFixtures.SaveTinyPng(Path.Combine(folder, "1.png"));

            var loader = new ContentLoader(new MockLogger(), new ContentSetResolver())
            {
                ContentFolderPath = tempDir,
                EnableImageDiagnostics = false, // gate off
                LargeImageWarnBytes = 1
            };

            var fired = false;
            loader.LargeImageDetected += (_, _) => fired = true;

            await loader.LoadLocationContentAsync(new Location { Name = "Big", Id = "id_big2" });

            Assert.False(fired);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }
}
