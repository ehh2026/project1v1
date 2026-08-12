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
        var tempDir = CreateContentFolderWithMap();
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
    public async Task LoadClustersAsync_UsesCurrentClusterDistanceThreshold()
    {
        var tempDir = CreateContentFolderWithMap();
        try
        {
            File.WriteAllText(
                Path.Combine(tempDir, "locations.json"),
                "[" +
                "{\"Id\":\"a1\",\"Name\":\"Alpha\",\"PixelX\":100,\"PixelY\":100}," +
                "{\"Id\":\"b1\",\"Name\":\"Beta\",\"PixelX\":140,\"PixelY\":100}" +
                "]");

            var closeThresholdLoader = CreateLoaderForJsonTests(tempDir);
            closeThresholdLoader.ClusterDistanceThreshold = 50.0;
            var closeThresholdClusters = await closeThresholdLoader.LoadClustersAsync();

            var smallThresholdLoader = CreateLoaderForJsonTests(tempDir);
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
        var loader = new ContentLoader(new MockLogger(), new ContentSetResolver());
        await Assert.ThrowsAsync<ArgumentNullException>(() => loader.LoadLocationContentAsync(null!));
    }

    [Fact]
    public async Task LoadLocationContentAsync_MissingFolder_ReturnsNull()
    {
        var tempDir = CreateContentFolderWithMap();
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
        var tempDir = CreateContentFolderWithMap();
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
        var tempDir = CreateContentFolderWithMap();
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
        var tempDir = CreateContentFolderWithMap();
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
        var tempDir = CreateContentFolderWithMap();
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
        var tempDir = CreateContentFolderWithMap();
        try
        {
            var locationFolder = Path.Combine(tempDir, "Test");
            Directory.CreateDirectory(locationFolder);
            SaveTinyPng(Path.Combine(locationFolder, "1-letter.png"));
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
        var tempDir = CreateContentFolderWithMap();
        try
        {
            var locationFolder = Path.Combine(tempDir, "BioOnly");
            Directory.CreateDirectory(locationFolder);
            SaveTinyPng(Path.Combine(locationFolder, "1-photo.png"));

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
        var tempDir = CreateContentFolderWithMap();
        try
        {
            var locationFolder = Path.Combine(tempDir, "DidacticFile");
            Directory.CreateDirectory(locationFolder);
            SaveTinyPng(Path.Combine(locationFolder, "1-photo.png"));
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
        var tempDir = CreateContentFolderWithMap();
        try
        {
            var locationFolder = Path.Combine(tempDir, "SkipMissing");
            Directory.CreateDirectory(locationFolder);
            SaveTinyPng(Path.Combine(locationFolder, "2-present.png"));

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
        var tempDir = CreateContentFolderWithMap();
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
        var tempDir = CreateContentFolderWithMap();
        try
        {
            var locationFolder = Path.Combine(tempDir, "Paris");
            Directory.CreateDirectory(locationFolder);
            SaveTinyPng(Path.Combine(locationFolder, "1.png"));

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
        var tempDir = CreateContentFolderWithMap();
        try
        {
            var folderA = Path.Combine(tempDir, "SameName");
            Directory.CreateDirectory(folderA);
            SaveTinyPng(Path.Combine(folderA, "1.png"));

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
        var tempDir = CreateContentFolderWithMap();
        try
        {
            foreach (var name in new[] { "A", "B", "C" })
            {
                var folder = Path.Combine(tempDir, name);
                Directory.CreateDirectory(folder);
                SaveTinyPng(Path.Combine(folder, "1.png"));
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
        var tempDir = CreateContentFolderWithMap();
        try
        {
            foreach (var name in new[] { "A", "B", "C" })
            {
                var folder = Path.Combine(tempDir, name);
                Directory.CreateDirectory(folder);
                SaveTinyPng(Path.Combine(folder, "1.png"));
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
        var tempDir = CreateContentFolderWithMap();
        try
        {
            var folder = Path.Combine(tempDir, "Big");
            Directory.CreateDirectory(folder);
            SaveTinyPng(Path.Combine(folder, "1.png"));

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
        var tempDir = CreateContentFolderWithMap();
        try
        {
            var folder = Path.Combine(tempDir, "Small");
            Directory.CreateDirectory(folder);
            SaveTinyPng(Path.Combine(folder, "1.png"));

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
        var tempDir = CreateContentFolderWithMap();
        try
        {
            var folder = Path.Combine(tempDir, "Big");
            Directory.CreateDirectory(folder);
            SaveTinyPng(Path.Combine(folder, "1.png"));

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

    /// <summary>
    /// Loader that skips the repo Excel file so JSON-only tests stay isolated.
    /// </summary>
    private static ContentLoader CreateLoaderForJsonTests(string contentFolderPath)
    {
        return new ContentLoader(new MockLogger(), new ContentSetResolver())
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

    private static string CreateContentFolderWithAssets()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "iwm-assets-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        var assetsDir = Path.Combine(tempDir, ContentFileNames.AssetsFolderName);
        Directory.CreateDirectory(assetsDir);
        File.WriteAllText(Path.Combine(assetsDir, ContentFileNames.WorldMapFileName), "fake-image-data");
        return tempDir;
    }

    private static void SafeDeleteDirectory(string path, int maxRetries = 6)
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        for (int attempt = 0; attempt < maxRetries; attempt++)
        {
            try
            {
                if (Directory.Exists(path))
                {
                    foreach (var file in Directory.GetFiles(path, "*", SearchOption.AllDirectories))
                    {
                        try { File.Delete(file); }
                        catch (IOException) { /* file still locked; retry on next pass */ }
                    }
                    Directory.Delete(path, recursive: true);
                }
                return;
            }
            catch (IOException) when (attempt < maxRetries - 1)
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
                Thread.Sleep(100 * (attempt + 1));
            }
        }
    }

    private static void SaveTinyPng(string path)
    {
        var bitmap = BitmapSource.Create(
            1,
            1,
            96,
            96,
            PixelFormats.Bgra32,
            null,
            new byte[] { 0, 0, 255, 255 },
            4);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));

        using var stream = File.Create(path);
        encoder.Save(stream);
    }

    [Fact]
    public void GetWorldMapPath_AssetsFolderPresent_ReturnsAssetsPath()
    {
        var tempDir = CreateContentFolderWithAssets();
        try
        {
            var loader = new ContentLoader(new MockLogger(), new ContentSetResolver()) { ContentFolderPath = tempDir };
            var mapPath = loader.GetWorldMapPath();
            Assert.Equal(Path.Combine(tempDir, ContentFileNames.AssetsFolderName, ContentFileNames.WorldMapFileName), mapPath);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void GetWorldMapPath_AssetsFolderMissing_LegacyRootFallback()
    {
        var tempDir = CreateContentFolderWithMap();
        try
        {
            var loader = new ContentLoader(new MockLogger(), new ContentSetResolver()) { ContentFolderPath = tempDir };
            var mapPath = loader.GetWorldMapPath();
            Assert.Equal(Path.Combine(tempDir, ContentFileNames.WorldMapFileName), mapPath);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void ResolvePinPartPath_RelativePath_RoutesThroughAssets()
    {
        var tempDir = CreateContentFolderWithAssets();
        try
        {
            var loader = new ContentLoader(new MockLogger(), new ContentSetResolver()) { ContentFolderPath = tempDir };
            var relativePath = "Pins_v2/parts/pin_part_geometry.json";

            // Write a dummy file under Assets/Pins_v2/parts
            var fullPath = Path.Combine(tempDir, ContentFileNames.AssetsFolderName, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
            File.WriteAllText(fullPath, "dummy");

            var resolved = loader.ResolvePinPartPath(relativePath);
            Assert.Equal(fullPath, resolved);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void ActiveContentSetPath_AfterFirstResolve_IsStableForSession()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "iwm-stable-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            var demoDir = Path.Combine(tempDir, ContentFileNames.DemoContentFolderName);
            Directory.CreateDirectory(demoDir);
            File.WriteAllText(Path.Combine(demoDir, ContentFileNames.LocationsJsonFileName), "[]");

            var loader = new ContentLoader(new MockLogger(), new ContentSetResolver()) { ContentFolderPath = tempDir };
            var resolvedPath1 = loader.ActiveContentSetPath;

            // Delete the demo directory to simulate changes on disk
            Directory.Delete(demoDir, recursive: true);

            var resolvedPath2 = loader.ActiveContentSetPath;
            Assert.Equal(resolvedPath1, resolvedPath2);
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void ValidateContentFolder_MissingCoordinateSource_ReturnsFalse()
    {
        // Legacy root lacks coordinate source
        var tempDir = Path.Combine(Path.GetTempPath(), "iwm-empty-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            // Include map under Assets
            var assetsDir = Path.Combine(tempDir, ContentFileNames.AssetsFolderName);
            Directory.CreateDirectory(assetsDir);
            File.WriteAllText(Path.Combine(assetsDir, ContentFileNames.WorldMapFileName), "fake");

            var loader = new ContentLoader(new MockLogger(), new ContentSetResolver()) { ContentFolderPath = tempDir };

            // Should fail validation because there is no locations.json or Excel in active set (resolved to Legacy here)
            Assert.False(loader.ValidateContentFolder());
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void ActiveContentSetKind_WithDemoContentSet_ReturnsDemo()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "iwm-kind-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            var demoDir = Path.Combine(tempDir, ContentFileNames.DemoContentFolderName);
            Directory.CreateDirectory(demoDir);
            File.WriteAllText(Path.Combine(demoDir, ContentFileNames.LocationsJsonFileName), "[]");

            var loader = new ContentLoader(new MockLogger(), new ContentSetResolver()) { ContentFolderPath = tempDir };

            Assert.Equal(ContentSetKind.Demo, loader.ActiveContentSetKind);
        }
        finally
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void DecodeAndWarningProperties_ClampNegativeValues()
    {
        var loader = new ContentLoader(new MockLogger(), new ContentSetResolver())
        {
            MaxCachedLocations = -1,
            MaxDecodePixelWidth = -10,
            MaxDecodePixelHeight = -20,
            LargeImageWarnBytes = -30
        };

        Assert.Equal(0, loader.MaxCachedLocations);
        Assert.Equal(0, loader.MaxDecodePixelWidth);
        Assert.Equal(0, loader.MaxDecodePixelHeight);
        Assert.Equal(0, loader.LargeImageWarnBytes);
    }

    [Fact]
    public void GetFullResolutionWorldMapPath_AssetsFolderPresent_ReturnsAssetsPath()
    {
        var tempDir = CreateContentFolderWithAssets();
        try
        {
            var fullPath = Path.Combine(
                tempDir,
                ContentFileNames.AssetsFolderName,
                ContentFileNames.FullResolutionWorldMapFileName);
            File.WriteAllText(fullPath, "fake-full-res");
            var loader = new ContentLoader(new MockLogger(), new ContentSetResolver()) { ContentFolderPath = tempDir };

            Assert.Equal(fullPath, loader.GetFullResolutionWorldMapPath());
        }
        finally
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void ResolveContentFilePath_ExistingAsset_ReturnsAssetsPath()
    {
        var tempDir = CreateContentFolderWithAssets();
        try
        {
            var assetPath = Path.Combine(tempDir, ContentFileNames.AssetsFolderName, "asset.txt");
            File.WriteAllText(assetPath, "asset");
            var loader = new ContentLoader(new MockLogger(), new ContentSetResolver()) { ContentFolderPath = tempDir };

            Assert.Equal(assetPath, loader.ResolveContentFilePath("asset.txt"));
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void TryLoadContentBitmap_WithMissingFile_ReturnsNull()
    {
        var tempDir = CreateContentFolderWithMap();
        try
        {
            var loader = new ContentLoader(new MockLogger(), new ContentSetResolver()) { ContentFolderPath = tempDir };

            Assert.Null(loader.TryLoadContentBitmap("missing.png"));
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void TryLoadContentBitmap_WithInvalidImage_ReturnsNullAndLogsWarning()
    {
        var tempDir = CreateContentFolderWithMap();
        var logger = new MockLogger();
        var badPath = Path.Combine(tempDir, "bad.png");
        try
        {
            File.WriteAllText(badPath, "not an image");
            var loader = new ContentLoader(logger, new ContentSetResolver()) { ContentFolderPath = tempDir };

            Assert.Null(loader.TryLoadContentBitmap("bad.png"));
            Assert.Contains(logger.WarningMessages, message => message.Contains("Failed to load content bitmap"));
        }
        finally
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void TryLoadContentBitmap_WithValidPng_ReturnsFrozenBitmap()
    {
        var tempDir = CreateContentFolderWithMap();
        try
        {
            SaveTinyPng(Path.Combine(tempDir, "valid.png"));
            var loader = new ContentLoader(new MockLogger(), new ContentSetResolver()) { ContentFolderPath = tempDir };

            var bitmap = loader.TryLoadContentBitmap("valid.png");

            Assert.NotNull(bitmap);
            Assert.True(bitmap!.IsFrozen);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task LoadMapImageAsync_WhenMapMissing_ThrowsFileNotFoundException()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "iwm-map-missing-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            var loader = new ContentLoader(new MockLogger(), new ContentSetResolver()) { ContentFolderPath = tempDir };

            await Assert.ThrowsAsync<FileNotFoundException>(() => loader.LoadMapImageAsync());
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void LoadPinPartGeometry_WhenFileMissing_ThrowsFileNotFoundException()
    {
        var tempDir = CreateContentFolderWithMap();
        try
        {
            var loader = new ContentLoader(new MockLogger(), new ContentSetResolver()) { ContentFolderPath = tempDir };

            Assert.Throws<FileNotFoundException>(() => loader.LoadPinPartGeometry("missing.json"));
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void LoadPinPartGeometry_WithInvalidJson_ThrowsInvalidOperationException()
    {
        var tempDir = CreateContentFolderWithMap();
        try
        {
            File.WriteAllText(Path.Combine(tempDir, "bad-geometry.json"), "{ bad json");
            var loader = new ContentLoader(new MockLogger(), new ContentSetResolver()) { ContentFolderPath = tempDir };

            Assert.Throws<InvalidOperationException>(() => loader.LoadPinPartGeometry("bad-geometry.json"));
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void LoadPinPartGeometry_WithEmptyJson_ThrowsInvalidOperationException()
    {
        var tempDir = CreateContentFolderWithMap();
        try
        {
            File.WriteAllText(Path.Combine(tempDir, "empty-geometry.json"), "{}");
            var loader = new ContentLoader(new MockLogger(), new ContentSetResolver()) { ContentFolderPath = tempDir };

            Assert.Throws<InvalidOperationException>(() => loader.LoadPinPartGeometry("empty-geometry.json"));
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task LoadLocationContentAsync_WithDecodeBounds_LoadsDownscaledFrozenBitmap()
    {
        var tempDir = CreateContentFolderWithMap();
        try
        {
            var folder = Path.Combine(tempDir, "Bounded");
            Directory.CreateDirectory(folder);
            SaveTinyPng(Path.Combine(folder, "1.png"));

            var loader = new ContentLoader(new MockLogger(), new ContentSetResolver())
            {
                ContentFolderPath = tempDir,
                MaxDecodePixelWidth = 1,
                MaxDecodePixelHeight = 1,
                EnableImageDiagnostics = true
            };

            var bitmap = await loader.LoadLocationContentAsync(new Location { Name = "Bounded", Id = "bounded" });

            Assert.NotNull(bitmap);
            Assert.True(bitmap!.IsFrozen);
            Assert.Equal(1, bitmap.PixelWidth);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task LoadAllLocationImagesWithTranslationsAsync_UsesCaptionFromLocationMetadata()
    {
        var tempDir = CreateContentFolderWithMap();
        try
        {
            var locationFolder = Path.Combine(tempDir, "CaptionMeta");
            Directory.CreateDirectory(locationFolder);
            SaveTinyPng(Path.Combine(locationFolder, "1-photo.png"));

            var loader = new ContentLoader(new MockLogger(), new ContentSetResolver()) { ContentFolderPath = tempDir };
            var location = new Location { Name = "CaptionMeta", Id = "caption-meta" };
            location.CaptionsByImageFileName["1-photo.png"] = "Caption from metadata";

            var result = await loader.LoadAllLocationImagesWithTranslationsAsync(location);

            var image = Assert.Single(result);
            Assert.Equal("Caption from metadata", image.CaptionText);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    // -------------------------------------------------------------------------
    // LoadDidacticTextAsync — Phase 3 Task 3.2 coverage
    // -------------------------------------------------------------------------

    [Fact]
    public async Task LoadDidacticTextAsync_NullLocation_Throws()
    {
        var loader = new ContentLoader(new MockLogger(), new ContentSetResolver());
        await Assert.ThrowsAsync<ArgumentNullException>(() => loader.LoadDidacticTextAsync(null!));
    }

    [Fact]
    public async Task LoadDidacticTextAsync_WhenFileMissing_ReturnsNull()
    {
        var tempDir = CreateContentFolderWithMap();
        try
        {
            var locationFolder = Path.Combine(tempDir, "NoDidactic");
            Directory.CreateDirectory(locationFolder);

            var loader = new ContentLoader(new MockLogger(), new ContentSetResolver()) { ContentFolderPath = tempDir };
            var location = new Location { Name = "NoDidactic", Id = "nd" };

            var result = await loader.LoadDidacticTextAsync(location);

            Assert.Null(result);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task LoadDidacticTextAsync_WhenExcelBioExists_PrefersWorkbookText()
    {
        var tempDir = CreateContentFolderWithMap();
        try
        {
            var locationFolder = Path.Combine(tempDir, "WithBoth");
            Directory.CreateDirectory(locationFolder);
            File.WriteAllText(Path.Combine(locationFolder, "didactic.txt"), "File didactic text.");

            var loader = new ContentLoader(new MockLogger(), new ContentSetResolver()) { ContentFolderPath = tempDir };
            var location = new Location
            {
                Name = "WithBoth",
                Id = "wb",
                DidacticText = "Excel workbook didactic text."
            };

            var result = await loader.LoadDidacticTextAsync(location);

            Assert.Equal("Excel workbook didactic text.", result);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task LoadDidacticTextAsync_WithDidacticFile_ReturnsFileContent()
    {
        var tempDir = CreateContentFolderWithMap();
        try
        {
            var locationFolder = Path.Combine(tempDir, "FileOnly");
            Directory.CreateDirectory(locationFolder);
            File.WriteAllText(Path.Combine(locationFolder, "didactic.txt"), "Didactic from file.");

            var loader = new ContentLoader(new MockLogger(), new ContentSetResolver()) { ContentFolderPath = tempDir };
            var location = new Location { Name = "FileOnly", Id = "fo" };

            var result = await loader.LoadDidacticTextAsync(location);

            Assert.Equal("Didactic from file.", result);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task LoadDidacticTextAsync_WithLocationDidacticText_ReturnsPropertyText()
    {
        var loader = new ContentLoader(new MockLogger(), new ContentSetResolver())
        {
            ContentFolderPath = Path.Combine(Path.GetTempPath(), "iwm-cl-" + Guid.NewGuid().ToString("N"))
        };
        var location = new Location { Name = "Any", Id = "any", DidacticText = "From property." };

        var result = await loader.LoadDidacticTextAsync(location);

        Assert.Equal("From property.", result);
    }

    // -------------------------------------------------------------------------
    // Caption sidecar coverage (tested via LoadAllLocationImagesWithTranslationsAsync)
    // -------------------------------------------------------------------------

    [Fact]
    public async Task LoadCaptionsAsync_WhenSidecarExists_ReturnsCaption()
    {
        var tempDir = CreateContentFolderWithMap();
        try
        {
            var locationFolder = Path.Combine(tempDir, "Captions");
            Directory.CreateDirectory(locationFolder);
            SaveTinyPng(Path.Combine(locationFolder, "1-photo.png"));
            File.WriteAllText(
                Path.Combine(locationFolder, "1-photo-caption.txt"),
                "A detailed caption for this photo.");

            var loader = new ContentLoader(new MockLogger(), new ContentSetResolver()) { ContentFolderPath = tempDir };
            var location = new Location { Name = "Captions", Id = "cap" };

            var result = await loader.LoadAllLocationImagesWithTranslationsAsync(location);

            var image = Assert.Single(result);
            Assert.Equal("A detailed caption for this photo.", image.CaptionText);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task LoadCaptionsAsync_WhenCaptionMissing_ReturnsEmptyCaption()
    {
        var tempDir = CreateContentFolderWithMap();
        try
        {
            var locationFolder = Path.Combine(tempDir, "NoCaption");
            Directory.CreateDirectory(locationFolder);
            SaveTinyPng(Path.Combine(locationFolder, "1-photo.png"));

            var loader = new ContentLoader(new MockLogger(), new ContentSetResolver()) { ContentFolderPath = tempDir };
            var location = new Location { Name = "NoCaption", Id = "nc" };

            var result = await loader.LoadAllLocationImagesWithTranslationsAsync(location);

            var image = Assert.Single(result);
            Assert.Null(image.CaptionText);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    // -------------------------------------------------------------------------
    // Additional uncovered error paths
    // -------------------------------------------------------------------------

    [Fact]
    public async Task LoadAllLocationImagesWithTranslationsAsync_WithTranslationSidecar_ReturnsTranslation()
    {
        var tempDir = CreateContentFolderWithMap();
        try
        {
            var locationFolder = Path.Combine(tempDir, "Trans");
            Directory.CreateDirectory(locationFolder);
            SaveTinyPng(Path.Combine(locationFolder, "1-photo.png"));
            File.WriteAllText(
                Path.Combine(locationFolder, "1-photo.txt"),
                "Translated content here.");

            var loader = new ContentLoader(new MockLogger(), new ContentSetResolver()) { ContentFolderPath = tempDir };
            var location = new Location { Name = "Trans", Id = "tr" };

            var result = await loader.LoadAllLocationImagesWithTranslationsAsync(location);

            var image = Assert.Single(result);
            Assert.Equal("Translated content here.", image.TranslationText);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task LoadAllLocationImagesWithTranslationsAsync_InvalidImage_SkipsAndLogsWarning()
    {
        var tempDir = CreateContentFolderWithMap();
        try
        {
            var locationFolder = Path.Combine(tempDir, "BadImg");
            Directory.CreateDirectory(locationFolder);
            File.WriteAllText(Path.Combine(locationFolder, "1-broken.png"), "not-a-real-image");

            var logger = new MockLogger();
            var loader = new ContentLoader(logger, new ContentSetResolver()) { ContentFolderPath = tempDir };
            var location = new Location { Name = "BadImg", Id = "bi" };

            var result = await loader.LoadAllLocationImagesWithTranslationsAsync(location);

            Assert.Empty(result);
            Assert.Contains(logger.WarningMessages, m => m.Contains("Failed to load image file for location BadImg"));
        }
        finally
        {
            SafeDeleteDirectory(tempDir);
        }
    }

    [Fact]
    public async Task LoadLocationContentAsync_InvalidImage_ReturnsNullAndLogsError()
    {
        var tempDir = CreateContentFolderWithMap();
        try
        {
            var folder = Path.Combine(tempDir, "BadContent");
            Directory.CreateDirectory(folder);
            File.WriteAllText(Path.Combine(folder, "1.png"), "corrupt");

            var logger = new MockLogger();
            var loader = new ContentLoader(logger, new ContentSetResolver()) { ContentFolderPath = tempDir };
            var location = new Location { Name = "BadContent", Id = "bc" };

            var result = await loader.LoadLocationContentAsync(location);

            Assert.Null(result);
            Assert.Contains(logger.ErrorMessages, m => m.Contains("Failed to load content for location BadContent"));
        }
        finally
        {
            SafeDeleteDirectory(tempDir);
        }
    }

    [Fact]
    public async Task LoadLocationsAsync_WhenLocationsJsonEmptyArray_ReturnsEmptyList()
    {
        var tempDir = CreateContentFolderWithMap();
        try
        {
            File.WriteAllText(Path.Combine(tempDir, "locations.json"), "[]");

            var loader = CreateLoaderForJsonTests(tempDir);
            var locations = await loader.LoadLocationsAsync();

            Assert.Empty(locations);
            Assert.False(loader.IsInitialized);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }
}
