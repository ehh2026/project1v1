using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Windows;
using InteractiveWorldMap.Models;
using InteractiveWorldMap.Services;
using InteractiveWorldMap.Tests.TestHelpers;
using Xunit;

namespace InteractiveWorldMap.Tests;

public class ClusterCacheTests : IDisposable
{
    private readonly string _tempDir;

    public ClusterCacheTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "ClusterCache_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    private static List<Location> CreateLocations() => new()
    {
        new Location { Name = "Alpha", PixelX = 10, PixelY = 20 },
        new Location { Name = "Beta", PixelX = 12, PixelY = 22 },
        new Location { Name = "Gamma", PixelX = 500, PixelY = 500 }
    };

    private static List<LocationCluster> CreateClusters(List<Location> locations) => new()
    {
        new LocationCluster
        {
            Id = "cluster-1",
            CenterPoint = new Point(11, 21),
            Locations = new List<Location> { locations[0], locations[1] }
        },
        new LocationCluster
        {
            Id = "cluster-2",
            CenterPoint = new Point(500, 500),
            Locations = new List<Location> { locations[2] }
        }
    };

    [Fact]
    public void Save_ThenTryLoad_WithTempCacheRoot_ReturnsClusters()
    {
        var logger = new MockLogger();
        var cache = new ClusterCache(logger, "test", _tempDir);
        var locations = CreateLocations();
        var clusters = CreateClusters(locations);

        cache.Save(locations, clusters, threshold: 50);
        var loaded = cache.TryLoad(locations, threshold: 50);

        Assert.NotNull(loaded);
        Assert.Equal(2, loaded!.Count);
    }

    [Fact]
    public void TryLoad_WithMissingCacheFile_ReturnsNull()
    {
        var logger = new MockLogger();
        var cache = new ClusterCache(logger, "test", _tempDir);

        var loaded = cache.TryLoad(CreateLocations(), threshold: 50);

        Assert.Null(loaded);
    }

    [Fact]
    public void Constructor_WithExplicitLegacyPath_MigratesLegacyCache()
    {
        var logger = new MockLogger();
        var clustersRoot = Path.Combine(_tempDir, "clusters");
        Directory.CreateDirectory(clustersRoot);
        var legacyPath = Path.Combine(_tempDir, "cluster_cache.json");
        var legacyData = new { LocationHash = "unused", Clusters = new List<object>() };
        File.WriteAllText(legacyPath, JsonSerializer.Serialize(legacyData));

        var cache = new ClusterCache(logger, "demo", clustersRoot, legacyPath);

        var expectedNewPath = Path.Combine(clustersRoot, "demo.json");
        Assert.True(File.Exists(expectedNewPath));
        Assert.False(File.Exists(legacyPath));
    }

    [Fact]
    public void Constructor_WithCustomRoot_DoesNotMigrateParentCache()
    {
        var logger = new MockLogger();
        var clustersRoot = Path.Combine(_tempDir, "clusters");
        Directory.CreateDirectory(clustersRoot);
        var unrelatedParentCache = Path.Combine(_tempDir, "cluster_cache.json");
        File.WriteAllText(unrelatedParentCache, "unrelated cache data");

        _ = new ClusterCache(logger, "demo", clustersRoot);

        Assert.True(File.Exists(unrelatedParentCache));
        Assert.False(File.Exists(Path.Combine(clustersRoot, "demo.json")));
    }

    [Fact]
    public void TryLoad_AfterSave_PreservesClusterIdsCentersAndLocations()
    {
        var logger = new MockLogger();
        var cache = new ClusterCache(logger, "test", _tempDir);
        var locations = CreateLocations();
        var clusters = CreateClusters(locations);

        cache.Save(locations, clusters, threshold: 50);
        var loaded = cache.TryLoad(locations, threshold: 50);

        Assert.NotNull(loaded);
        Assert.Equal("cluster-1", loaded![0].Id);
        Assert.Equal(11, loaded[0].CenterPoint.X);
        Assert.Equal(21, loaded[0].CenterPoint.Y);
        Assert.Equal(2, loaded[0].Locations.Count);
        Assert.Equal("cluster-2", loaded[1].Id);
        Assert.Single(loaded[1].Locations);
    }

    [Fact]
    public void TryLoad_WithChangedThreshold_ReturnsNull()
    {
        var logger = new MockLogger();
        var cache = new ClusterCache(logger, "test", _tempDir);
        var locations = CreateLocations();
        var clusters = CreateClusters(locations);

        cache.Save(locations, clusters, threshold: 50);
        var loaded = cache.TryLoad(locations, threshold: 100);

        Assert.Null(loaded);
    }

    [Fact]
    public void TryLoad_WithChangedLocationCoordinate_ReturnsNull()
    {
        var logger = new MockLogger();
        var cache = new ClusterCache(logger, "test", _tempDir);
        var locations = CreateLocations();
        var clusters = CreateClusters(locations);

        cache.Save(locations, clusters, threshold: 50);

        locations[0].PixelX = 999;
        var loaded = cache.TryLoad(locations, threshold: 50);

        Assert.Null(loaded);
    }

    [Fact]
    public void TryLoad_WithLocationOrderChanged_ReturnsClusters()
    {
        var logger = new MockLogger();
        var cache = new ClusterCache(logger, "test", _tempDir);
        var locations = CreateLocations();
        var clusters = CreateClusters(locations);

        cache.Save(locations, clusters, threshold: 50);

        var reversed = new List<Location> { locations[2], locations[1], locations[0] };
        var loaded = cache.TryLoad(reversed, threshold: 50);

        Assert.NotNull(loaded);
        Assert.Equal(2, loaded!.Count);
    }

    [Fact]
    public void TryLoad_WithMissingLocationReferencedByCache_ReturnsNull()
    {
        var logger = new MockLogger();
        var cache = new ClusterCache(logger, "test", _tempDir);
        var locations = CreateLocations();
        var clusters = CreateClusters(locations);

        cache.Save(locations, clusters, threshold: 50);

        var trimmed = new List<Location> { locations[0], locations[1] };
        var loaded = cache.TryLoad(trimmed, threshold: 50);

        Assert.Null(loaded);
    }

    [Fact]
    public void TryLoad_WithMalformedJson_ReturnsNull()
    {
        var logger = new MockLogger();
        var cache = new ClusterCache(logger, "test", _tempDir);
        var locations = CreateLocations();
        var clusters = CreateClusters(locations);

        cache.Save(locations, clusters, threshold: 50);

        var cacheFile = Path.Combine(_tempDir, "test.json");
        File.WriteAllText(cacheFile, "{ this is not valid json");

        var loaded = cache.TryLoad(locations, threshold: 50);

        Assert.Null(loaded);
    }

    [Fact]
    public void Save_WhenDirectoryCannotBeCreated_DoesNotThrowAndLogsWarning()
    {
        var logger = new MockLogger();
        var fileAsDir = Path.Combine(_tempDir, "blocker");
        File.WriteAllText(fileAsDir, "I am a file not a directory");
        var badRoot = Path.Combine(fileAsDir, "sub");
        var cache = new ClusterCache(logger, "test", badRoot);

        var exception = Record.Exception(() =>
            cache.Save(CreateLocations(), CreateClusters(CreateLocations()), threshold: 50));

        Assert.Null(exception);
        Assert.Contains(logger.WarningMessages, m => m.Contains("Failed to save cache"));
    }
}
