using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using InteractiveWorldMap.Models;
using InteractiveWorldMap.Services;
using InteractiveWorldMap.Tests.TestHelpers;
using InteractiveWorldMap.Utilities;
using Xunit;

namespace InteractiveWorldMap.Tests;

public class CompositePinApplicationServiceTests
{
    [Fact]
    public void TryCacheLoad_WithCacheHit_ReturnsPlansAndComputedKey()
    {
        var tempDir = NewTempDir();
        var cache = new CompositePinPlanCache(new MockLogger(), tempDir);
        var service = new CompositePinApplicationService(cache, new FakePlanningResultProvider());
        var layout = CreateLayout();
        var config = new PinPartConfig();
        var geometryPath = CreateGeometryFile();

        try
        {
            var miss = service.TryCacheLoad(layout, config, layout.GroupKey, geometryPath, out var cacheKey);
            Assert.Null(miss);

            cache.Save(
                cacheKey,
                layout.GroupKey,
                layout.VariantId,
                new[] { new CachedCompositePlanEntry("LocA", CreatePlan("pair-a", "head-a.png")) });

            var loaded = service.TryCacheLoad(layout, config, layout.GroupKey, geometryPath, out var loadedKey);

            Assert.Equal(cacheKey, loadedKey);
            Assert.NotNull(loaded);
            var entry = Assert.Single(loaded!);
            Assert.Equal("LocA", entry.Key);
            Assert.Equal("pair-a", entry.Value.PairId);
        }
        finally
        {
            DeleteTempDir(tempDir);
            File.Delete(geometryPath);
        }
    }

    [Fact]
    public void TryCacheLoad_WithCacheMiss_ReturnsNullAndComputedKey()
    {
        var tempDir = NewTempDir();
        var cache = new CompositePinPlanCache(new MockLogger(), tempDir);
        var service = new CompositePinApplicationService(cache, new FakePlanningResultProvider());
        var geometryPath = CreateGeometryFile();

        try
        {
            var loaded = service.TryCacheLoad(
                CreateLayout(),
                new PinPartConfig(),
                "group-a",
                geometryPath,
                out var cacheKey);

            Assert.Null(loaded);
            Assert.False(string.IsNullOrWhiteSpace(cacheKey));
        }
        finally
        {
            DeleteTempDir(tempDir);
            File.Delete(geometryPath);
        }
    }

    [Fact]
    public void TryCacheLoad_WhenGeometryFileChanges_ComputesDifferentCacheKey()
    {
        var tempDir = NewTempDir();
        var cache = new CompositePinPlanCache(new MockLogger(), tempDir);
        var service = new CompositePinApplicationService(cache, new FakePlanningResultProvider());
        var geometryPath = CreateGeometryFile("{\"version\":1}");

        try
        {
            service.TryCacheLoad(CreateLayout(), new PinPartConfig(), "group-a", geometryPath, out var firstKey);

            File.WriteAllText(geometryPath, "{\"version\":2}");
            service.TryCacheLoad(CreateLayout(), new PinPartConfig(), "group-a", geometryPath, out var secondKey);

            Assert.NotEqual(firstKey, secondKey);
        }
        finally
        {
            DeleteTempDir(tempDir);
            File.Delete(geometryPath);
        }
    }

    [Fact]
    public void InvalidateGroup_RemovesMatchingCachedPlan()
    {
        var tempDir = NewTempDir();
        var cache = new CompositePinPlanCache(new MockLogger(), tempDir);
        var service = new CompositePinApplicationService(cache, new FakePlanningResultProvider());
        var layout = CreateLayout();
        var geometryPath = CreateGeometryFile();

        try
        {
            service.TryCacheLoad(layout, new PinPartConfig(), layout.GroupKey, geometryPath, out var cacheKey);
            cache.Save(
                cacheKey,
                layout.GroupKey,
                layout.VariantId,
                new[] { new CachedCompositePlanEntry("LocA", CreatePlan("pair-a", "head-a.png")) });

            service.InvalidateGroup(layout.GroupKey);

            Assert.Null(cache.TryLoad(cacheKey));
        }
        finally
        {
            DeleteTempDir(tempDir);
            File.Delete(geometryPath);
        }
    }

    [Fact]
    public void BuildApplyInstructions_WithViewportSourceCoords_ProjectsOriginalPosition()
    {
        var service = CreateService(out var tempDir);
        var viewport = ViewportState.CreateZoomedView(1100, 900, 20, 8198, 5542, 1000, 600);
        var applications = new[]
        {
            new LayoutEditorController.LayoutMarkerApplication(
                "LocA",
                new Point(1, 2),
                new Point(3, 4),
                true)
            {
                Angle = 90,
                LineLength = 40
            }
        };

        try
        {
            var result = service.BuildApplyInstructions(
                CreateLayout(),
                applications,
                new Dictionary<string, (double PixelX, double PixelY)> { ["LocA"] = (1100, 900) },
                viewport,
                1000,
                600,
                new PinPartConfig(),
                groupKey: "group-a",
                absoluteGeometryPath: MissingGeometryPath(),
                canUseCompositePins: false);

            var instruction = Assert.Single(result.Instructions);
            Assert.Equal(viewport.SourceToScreen(1100, 900, 1000, 600), instruction.OriginalScreen);
            Assert.NotEqual(applications[0].OriginalPosition, instruction.OriginalScreen);
        }
        finally
        {
            DeleteTempDir(tempDir);
        }
    }

    [Fact]
    public void BuildApplyInstructions_WithSourceExtendedCoords_PreservesFullMapOffset()
    {
        var service = CreateService(out var tempDir);
        var viewport = ViewportState.CreateZoomedView(1100, 900, 55, 8198, 5542, 1920, 1080);
        var fullMap = ViewportState.CreateFullMapView(8198, 5542, 1920, 1080);
        var applications = new[]
        {
            new LayoutEditorController.LayoutMarkerApplication(
                "LocA",
                new Point(0, 0),
                new Point(0, 0),
                true)
            {
                SourceExtendedX = 1150,
                SourceExtendedY = 900,
                Angle = 10,
                LineLength = 999
            }
        };

        try
        {
            var result = service.BuildApplyInstructions(
                CreateLayout(),
                applications,
                new Dictionary<string, (double PixelX, double PixelY)> { ["LocA"] = (1100, 900) },
                viewport,
                1920,
                1080,
                new PinPartConfig(),
                "fullmap",
                MissingGeometryPath(),
                canUseCompositePins: false,
                fullMapViewport: fullMap);

            var instruction = Assert.Single(result.Instructions);
            var fullAnchor = fullMap.SourceToScreen(1100, 900, 1920, 1080);
            var fullHead = fullMap.SourceToScreen(1150, 900, 1920, 1080);

            Assert.Equal(fullHead.X - fullAnchor.X, instruction.ExtendedScreen.X - instruction.OriginalScreen.X, 3);
            Assert.Equal(fullHead.Y - fullAnchor.Y, instruction.ExtendedScreen.Y - instruction.OriginalScreen.Y, 3);
        }
        finally
        {
            DeleteTempDir(tempDir);
        }
    }

    [Fact]
    public void BuildApplyInstructions_ClusterLayout_DoesNotShrinkOffsetToStub()
    {
        // Regression, reproduced from a real failure: a Taipei cluster layout saved at zoom 55
        // replayed with every pin as a short vertical stub. A ~59 screen-pixel drag at that zoom is
        // barely one source pixel, and re-projecting it at the full-map fit scale collapsed it to a
        // fraction of a pixel — under ManualLayoutPlacementPolicy.ExtensionLineThreshold (5px), so
        // each pin fell through to the auto-stub branch. Cluster layouts must re-project at the
        // viewport they were authored at.
        var service = CreateService(out var tempDir);
        var viewport = ViewportState.CreateZoomedView(6383, 2933, 55, 8198, 5542, 1920, 1080);
        var fullMap = ViewportState.CreateFullMapView(8198, 5542, 1920, 1080);

        var applications = new[]
        {
            new LayoutEditorController.LayoutMarkerApplication(
                "Chang Dai-chien",
                new Point(737.8, 445.5),
                new Point(795.0, 431.0),
                true)
            {
                SourceExtendedX = 6384.4375,
                SourceExtendedY = 2931.3677,
                Angle = 75.7,
                LineLength = 59.0
            }
        };

        try
        {
            var result = service.BuildApplyInstructions(
                CreateLayout(),
                applications,
                new Dictionary<string, (double PixelX, double PixelY)> { ["Chang Dai-chien"] = (6383, 2933) },
                viewport,
                1920,
                1080,
                new PinPartConfig(),
                // The key is incidental here: it only feeds plan caching, which is skipped because
                // canUseCompositePins is false. What drives this case is the zoomed viewport and
                // the tiny source-space offset, not the layout's identity.
                groupKey: "cluster-taipei",
                absoluteGeometryPath: MissingGeometryPath(),
                canUseCompositePins: false,
                fullMapViewport: fullMap);

            var instruction = Assert.Single(result.Instructions);
            var dx = instruction.ExtendedScreen.X - instruction.OriginalScreen.X;
            var dy = instruction.ExtendedScreen.Y - instruction.OriginalScreen.Y;
            var length = Math.Sqrt((dx * dx) + (dy * dy));

            Assert.True(
                length > ManualLayoutPlacementPolicy.ExtensionLineThreshold,
                $"Cluster head offset collapsed to {length:N2}px, at or under the {ManualLayoutPlacementPolicy.ExtensionLineThreshold}px " +
                "threshold, so the pin would replay as an auto stub.");
            Assert.True(instruction.RequiresExtensionLine);
        }
        finally
        {
            DeleteTempDir(tempDir);
        }
    }

    [Fact]
    public void BuildApplyInstructions_WithoutSourceExtendedCoords_UsesAngleAndLength()
    {
        var service = CreateService(out var tempDir);
        var applications = new[]
        {
            new LayoutEditorController.LayoutMarkerApplication(
                "LocA",
                new Point(10, 20),
                new Point(10, 20),
                true)
            {
                Angle = 90,
                LineLength = 30
            }
        };

        try
        {
            var result = service.BuildApplyInstructions(
                CreateLayout(),
                applications,
                new Dictionary<string, (double PixelX, double PixelY)>(),
                null,
                0,
                0,
                new PinPartConfig(),
                "group-a",
                MissingGeometryPath(),
                canUseCompositePins: false);

            var instruction = Assert.Single(result.Instructions);
            Assert.Equal(40, instruction.ExtendedScreen.X, 6);
            Assert.Equal(20, instruction.ExtendedScreen.Y, 6);
        }
        finally
        {
            DeleteTempDir(tempDir);
        }
    }

    [Fact]
    public void BuildApplyInstructions_WithCachedPlan_AttachesPlanByLocationName()
    {
        var tempDir = NewTempDir();
        var cache = new CompositePinPlanCache(new MockLogger(), tempDir);
        var service = new CompositePinApplicationService(cache, new FakePlanningResultProvider());
        var layout = CreateLayout();
        var geometryPath = CreateGeometryFile();
        var applications = new[]
        {
            new LayoutEditorController.LayoutMarkerApplication(
                "LocA",
                new Point(10, 20),
                new Point(40, 20),
                true)
        };

        try
        {
            service.TryCacheLoad(layout, new PinPartConfig(), layout.GroupKey, geometryPath, out var cacheKey);
            cache.Save(
                cacheKey,
                layout.GroupKey,
                layout.VariantId,
                new[] { new CachedCompositePlanEntry("LocA", CreatePlan("pair-a", "head-a.png")) });

            var result = service.BuildApplyInstructions(
                layout,
                applications,
                new Dictionary<string, (double PixelX, double PixelY)>(),
                null,
                0,
                0,
                new PinPartConfig(),
                layout.GroupKey,
                geometryPath,
                canUseCompositePins: true);

            var instruction = Assert.Single(result.Instructions);
            Assert.NotNull(instruction.CachedPlan);
            Assert.Equal("pair-a", instruction.CachedPlan!.PairId);
            Assert.False(result.ShouldSaveToCache);
        }
        finally
        {
            DeleteTempDir(tempDir);
            File.Delete(geometryPath);
        }
    }

    [Fact]
    public void BuildApplyInstructions_WithCacheMiss_SetsShouldSaveToCache()
    {
        var service = CreateService(out var tempDir);

        try
        {
            var result = service.BuildApplyInstructions(
                CreateLayout(),
                new[] { new LayoutEditorController.LayoutMarkerApplication("LocA", new Point(10, 20), new Point(40, 20), true) },
                new Dictionary<string, (double PixelX, double PixelY)>(),
                null,
                0,
                0,
                new PinPartConfig(),
                "group-a",
                MissingGeometryPath(),
                canUseCompositePins: true);

            Assert.True(result.ShouldSaveToCache);
            Assert.False(string.IsNullOrWhiteSpace(result.CacheKey));
        }
        finally
        {
            DeleteTempDir(tempDir);
        }
    }

    [Fact]
    public void BuildApplyInstructions_WhenCompositePinsDisabled_DoesNotAttemptCache()
    {
        var service = CreateService(out var tempDir);

        try
        {
            var result = service.BuildApplyInstructions(
                CreateLayout(),
                new[] { new LayoutEditorController.LayoutMarkerApplication("LocA", new Point(10, 20), new Point(40, 20), true) },
                new Dictionary<string, (double PixelX, double PixelY)>(),
                null,
                0,
                0,
                new PinPartConfig(),
                "group-a",
                MissingGeometryPath(),
                canUseCompositePins: false);

            Assert.False(result.ShouldSaveToCache);
            Assert.Equal(string.Empty, result.CacheKey);
        }
        finally
        {
            DeleteTempDir(tempDir);
        }
    }

    [Fact]
    public void BuildApplyInstructions_WhenGroupKeyBlank_DoesNotAttemptCache()
    {
        var service = CreateService(out var tempDir);

        try
        {
            var result = service.BuildApplyInstructions(
                CreateLayout(),
                new[] { new LayoutEditorController.LayoutMarkerApplication("LocA", new Point(10, 20), new Point(40, 20), true) },
                new Dictionary<string, (double PixelX, double PixelY)>(),
                null,
                0,
                0,
                new PinPartConfig(),
                groupKey: string.Empty,
                absoluteGeometryPath: MissingGeometryPath(),
                canUseCompositePins: true);

            Assert.False(result.ShouldSaveToCache);
            Assert.Equal(string.Empty, result.CacheKey);
        }
        finally
        {
            DeleteTempDir(tempDir);
        }
    }

    [Fact]
    public void SaveIfMissed_WhenNoMatchingPlans_DoesNotWriteCacheEntry()
    {
        var tempDir = NewTempDir();
        var cache = new CompositePinPlanCache(new MockLogger(), tempDir);
        var provider = new FakePlanningResultProvider();
        var service = new CompositePinApplicationService(cache, provider);
        var cacheKey = NewCacheKey();
        var groupKey = NewGroupKey();

        try
        {
            service.SaveIfMissed(
                cacheKey,
                groupKey,
                variantId: "manual-default",
                new[] { "LocA" });

            Assert.Null(cache.TryLoad(cacheKey));
        }
        finally
        {
            DeleteTempDir(tempDir);
        }
    }

    [Fact]
    public void SaveIfMissed_WithMatchingPlans_SavesEntries()
    {
        var tempDir = NewTempDir();
        var cache = new CompositePinPlanCache(new MockLogger(), tempDir);
        var provider = new FakePlanningResultProvider();
        provider.Add("LocA", CreateResult("pair-a", "head-a.png"));
        provider.Add("LocB", CreateResult("pair-b", "head-b.png"));
        var service = new CompositePinApplicationService(cache, provider);
        var cacheKey = NewCacheKey();
        var groupKey = NewGroupKey();

        try
        {
            service.SaveIfMissed(
                cacheKey,
                groupKey,
                variantId: "manual-default",
                new[] { "LocA", "LocB" });

            var entries = cache.TryLoad(cacheKey);

            Assert.NotNull(entries);
            Assert.Collection(
                entries!,
                first =>
                {
                    Assert.Equal("LocA", first.LocationId);
                    Assert.Equal("pair-a", first.Plan.PairId);
                    Assert.Equal("head-a.png", first.Plan.HeadSourcePath);
                },
                second =>
                {
                    Assert.Equal("LocB", second.LocationId);
                    Assert.Equal("pair-b", second.Plan.PairId);
                    Assert.Equal("head-b.png", second.Plan.HeadSourcePath);
                });
        }
        finally
        {
            DeleteTempDir(tempDir);
        }
    }

    [Fact]
    public void SaveIfMissed_OmitsMissingAndNullResults()
    {
        var tempDir = NewTempDir();
        var cache = new CompositePinPlanCache(new MockLogger(), tempDir);
        var provider = new FakePlanningResultProvider();
        provider.Add("LocA", CreateResult("pair-a", "head-a.png"));
        provider.Add("LocB", null);
        var service = new CompositePinApplicationService(cache, provider);
        var cacheKey = NewCacheKey();
        var groupKey = NewGroupKey();

        try
        {
            service.SaveIfMissed(
                cacheKey,
                groupKey,
                variantId: "manual-default",
                new[] { "LocA", "LocB", "LocC" });

            var entries = cache.TryLoad(cacheKey);
            var entry = Assert.Single(entries!);
            Assert.Equal("LocA", entry.LocationId);
            Assert.Equal("pair-a", entry.Plan.PairId);
        }
        finally
        {
            DeleteTempDir(tempDir);
        }
    }

    private static string NewCacheKey()
        => "service-testability-" + Guid.NewGuid().ToString("N");

    private static string NewGroupKey()
        => "service-testability-" + Guid.NewGuid().ToString("N");

    private static string NewTempDir()
        => Path.Combine(Path.GetTempPath(), "CompositePinApplicationService_" + Guid.NewGuid().ToString("N"));

    private static string MissingGeometryPath()
        => Path.Combine(Path.GetTempPath(), "missing_geometry_" + Guid.NewGuid().ToString("N") + ".json");

    private static string CreateGeometryFile(string content = "{\"pairs\":{}}")
    {
        var path = Path.Combine(Path.GetTempPath(), "geometry_" + Guid.NewGuid().ToString("N") + ".json");
        File.WriteAllText(path, content);
        return path;
    }

    private static CompositePinApplicationService CreateService(out string tempDir)
    {
        tempDir = NewTempDir();
        return new CompositePinApplicationService(
            new CompositePinPlanCache(new MockLogger(), tempDir),
            new FakePlanningResultProvider());
    }

    private static ManualLayout CreateLayout()
        => new()
        {
            GroupKey = "group-a",
            VariantId = "manual-default",
            Markers = new List<ManualLayoutMarker>
            {
                new("LocA", new Point(10, 20), new Point(40, 20), 90, 30)
            }
        };

    private static void DeleteTempDir(string tempDir)
    {
        if (Directory.Exists(tempDir))
            Directory.Delete(tempDir, recursive: true);
    }

    private static CompositePinPlanningResult CreateResult(string pairId, string headSourcePath)
        => new()
        {
            RenderPlan = CreatePlan(pairId, headSourcePath)
        };

    private static CompositePinRenderPlan CreatePlan(string pairId, string headSourcePath)
        => new()
        {
            PairId = pairId,
            HeadSourcePath = headSourcePath
        };

    private sealed class FakePlanningResultProvider : ICompositePinPlanningResultProvider
    {
        private readonly Dictionary<string, CompositePinPlanningResult?> _results =
            new(StringComparer.Ordinal);

        public void Add(string locationId, CompositePinPlanningResult? result)
        {
            _results[locationId] = result;
        }

        public bool TryGetLastResult(string locationId, out CompositePinPlanningResult? result)
        {
            return _results.TryGetValue(locationId, out result);
        }
    }
}
