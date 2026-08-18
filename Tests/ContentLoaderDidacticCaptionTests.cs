using System;
using System.IO;
using System.Threading.Tasks;
using InteractiveWorldMap.Models;
using InteractiveWorldMap.Services;
using InteractiveWorldMap.Utilities;
using InteractiveWorldMap.Tests.TestHelpers;
using Xunit;

namespace InteractiveWorldMap.Tests;

/// <summary>
/// ContentLoader didactic text, caption/translation sidecars, and related error paths.
/// </summary>
public class ContentLoaderDidacticCaptionTests
{
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
        var tempDir = ContentLoaderTestFixtures.CreateContentFolderWithMap();
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
        var tempDir = ContentLoaderTestFixtures.CreateContentFolderWithMap();
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
        var tempDir = ContentLoaderTestFixtures.CreateContentFolderWithMap();
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
        var tempDir = ContentLoaderTestFixtures.CreateContentFolderWithMap();
        try
        {
            var locationFolder = Path.Combine(tempDir, "Captions");
            Directory.CreateDirectory(locationFolder);
            ContentLoaderTestFixtures.SaveTinyPng(Path.Combine(locationFolder, "1-photo.png"));
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
        var tempDir = ContentLoaderTestFixtures.CreateContentFolderWithMap();
        try
        {
            var locationFolder = Path.Combine(tempDir, "NoCaption");
            Directory.CreateDirectory(locationFolder);
            ContentLoaderTestFixtures.SaveTinyPng(Path.Combine(locationFolder, "1-photo.png"));

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
        var tempDir = ContentLoaderTestFixtures.CreateContentFolderWithMap();
        try
        {
            var locationFolder = Path.Combine(tempDir, "Trans");
            Directory.CreateDirectory(locationFolder);
            ContentLoaderTestFixtures.SaveTinyPng(Path.Combine(locationFolder, "1-photo.png"));
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
        var tempDir = ContentLoaderTestFixtures.CreateContentFolderWithMap();
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
            ContentLoaderTestFixtures.SafeDeleteDirectory(tempDir);
        }
    }

    [Fact]
    public async Task LoadLocationContentAsync_InvalidImage_ReturnsNullAndLogsError()
    {
        var tempDir = ContentLoaderTestFixtures.CreateContentFolderWithMap();
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
            ContentLoaderTestFixtures.SafeDeleteDirectory(tempDir);
        }
    }

    [Fact]
    public async Task LoadLocationsAsync_WhenLocationsJsonEmptyArray_ReturnsEmptyList()
    {
        var tempDir = ContentLoaderTestFixtures.CreateContentFolderWithMap();
        try
        {
            File.WriteAllText(Path.Combine(tempDir, "locations.json"), "[]");

            var loader = ContentLoaderTestFixtures.CreateLoaderForJsonTests(tempDir);
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
