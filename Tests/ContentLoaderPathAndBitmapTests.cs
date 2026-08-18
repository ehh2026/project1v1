using System;
using System.IO;
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
/// ContentLoader path resolution, content-set, bitmap, and pin-geometry coverage.
/// </summary>
public class ContentLoaderPathAndBitmapTests
{
    [Fact]
    public void GetWorldMapPath_AssetsFolderPresent_ReturnsAssetsPath()
    {
        var tempDir = ContentLoaderTestFixtures.CreateContentFolderWithAssets();
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
        var tempDir = ContentLoaderTestFixtures.CreateContentFolderWithMap();
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
        var tempDir = ContentLoaderTestFixtures.CreateContentFolderWithAssets();
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
        var tempDir = ContentLoaderTestFixtures.CreateContentFolderWithAssets();
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
        var tempDir = ContentLoaderTestFixtures.CreateContentFolderWithAssets();
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
        var tempDir = ContentLoaderTestFixtures.CreateContentFolderWithMap();
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
        var tempDir = ContentLoaderTestFixtures.CreateContentFolderWithMap();
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
        var tempDir = ContentLoaderTestFixtures.CreateContentFolderWithMap();
        try
        {
            ContentLoaderTestFixtures.SaveTinyPng(Path.Combine(tempDir, "valid.png"));
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
        var tempDir = ContentLoaderTestFixtures.CreateContentFolderWithMap();
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
        var tempDir = ContentLoaderTestFixtures.CreateContentFolderWithMap();
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
        var tempDir = ContentLoaderTestFixtures.CreateContentFolderWithMap();
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
        var tempDir = ContentLoaderTestFixtures.CreateContentFolderWithMap();
        try
        {
            var folder = Path.Combine(tempDir, "Bounded");
            Directory.CreateDirectory(folder);
            ContentLoaderTestFixtures.SaveTinyPng(Path.Combine(folder, "1.png"));

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
        var tempDir = ContentLoaderTestFixtures.CreateContentFolderWithMap();
        try
        {
            var locationFolder = Path.Combine(tempDir, "CaptionMeta");
            Directory.CreateDirectory(locationFolder);
            ContentLoaderTestFixtures.SaveTinyPng(Path.Combine(locationFolder, "1-photo.png"));

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
}
