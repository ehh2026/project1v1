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
            var loader = new ContentLoader(new MockLogger()) { ContentFolderPath = tempDir };
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
            File.WriteAllText(Path.Combine(tempDir, "locations.json"),
                """
                [
                  {"Id":"a1","Name":"Paris","PixelX":100,"PixelY":200}
                ]
                """);

            var loader = new ContentLoader(new MockLogger()) { ContentFolderPath = tempDir };
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
            var loader = new ContentLoader(new MockLogger()) { ContentFolderPath = tempDir };

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

    private static string CreateContentFolderWithMap()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "iwm-cl-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        File.WriteAllText(Path.Combine(tempDir, "World Map Extra Large.jpg"), "fake-image-data");
        return tempDir;
    }
}
