using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Media;
using InteractiveWorldMap.Models;
using InteractiveWorldMap.Services;
using InteractiveWorldMap.Tests.TestHelpers;
using Xunit;

namespace InteractiveWorldMap.Tests;

public class CompositePinPlanCacheTests
{
    // ─── Helpers ─────────────────────────────────────────────────────────────

    private static CompositePinPlanCache CreateCache(string dir) =>
        new CompositePinPlanCache(new MockLogger());

    /// <summary>
    /// Creates a cache instance that writes into a temp sub-directory so tests
    /// do not pollute the real AppData cache folder.
    /// We rely on the cache's own AppData path, but write test entries via
    /// Save() and then read via TryLoad() — fully round-trip.
    /// </summary>
    private static (CompositePinPlanCache cache, string groupKey, string cacheKey) SetupCache()
    {
        var cache    = new CompositePinPlanCache(new MockLogger());
        var groupKey = "test_group_" + Guid.NewGuid().ToString("N")[..8];
        var key      = cache.ComputeCacheKey(groupKey, "manual-default", "layoutHash", "geoHash", "cfgHash");
        return (cache, groupKey, key);
    }

    private static CompositePinRenderPlan MakePlan(string pairId, double angleDeg) =>
        new CompositePinRenderPlan
        {
            PairId           = pairId,
            ShaftSourcePath  = $"Pins_v2/parts/{pairId}_shaft.png",
            HeadSourcePath   = $"Pins_v2/parts/{pairId}_head.png",
            Width            = 40,
            Height           = 80,
            TargetAngleDeg   = angleDeg,
            TargetLengthPx   = 55,
            HeadRotationDeg  = angleDeg,
            BodyStretchFactor = 1.0,
            StretchBodyLengthPx = 30,
            TipAnchorLocal   = new Point(20, 4),
            JoinAnchorLocal  = new Point(20, 34),
            StretchStartLocal = new Point(20, 10),
            StretchEndLocal  = new Point(20, 34),
            HeadAttachLocal  = new Point(20, 34),
            HeadCenterLocal  = new Point(20, 55),
            ShaftTipCapLayer = new CompositePinLayerPlan
            {
                SourcePath   = $"Pins_v2/parts/{pairId}_tip.png",
                SourceWidth  = 40,
                SourceHeight = 20,
                ClipPolygon  = new List<Point> { new(0, 0), new(40, 0), new(40, 20), new(0, 20) },
                Transform    = Matrix.Identity
            },
            ShaftBodyLayer    = new CompositePinLayerPlan { SourcePath = "body.png" },
            ShaftHeadCapLayer = new CompositePinLayerPlan { SourcePath = "headcap.png" },
            HeadLayer         = new CompositePinLayerPlan { SourcePath = "head.png",
                Transform = new Matrix(1, 0, 0, 1, 5, 10) }
        };

    [Fact]
    public void BuildApplyInstructions_ReclassifiesUsingFinalProjectedEndpoints()
    {
        var cache = new CompositePinPlanCache(new MockLogger());
        var planning = new CompositePinPlanningService(
            new PinPartPlacementCalculator(),
            new CompositePinRenderPlanBuilder());
        var service = new CompositePinApplicationService(cache, planning);
        var layout = new ManualLayout { GroupKey = "g1", VariantId = "seed-default" };

        var applications = new List<LayoutEditorController.LayoutMarkerApplication>
        {
            new("LocA", new Point(100, 100), new Point(150, 100), true)
            {
                Angle = 0,
                LineLength = 0
            },
            new("LocB", new Point(200, 200), new Point(200, 200), false)
            {
                Angle = 90,
                LineLength = 24
            }
        };

        var result = service.BuildApplyInstructions(
            layout,
            applications,
            new Dictionary<string, (double PixelX, double PixelY)>(),
            null,
            0,
            0,
            new PinPartConfig(),
            "group-key",
            Path.Combine(Path.GetTempPath(), "missing_geometry.json"),
            false);

        Assert.Collection(
            result.Instructions,
            first =>
            {
                Assert.Equal("LocA", first.LocationName);
                Assert.False(first.RequiresExtensionLine);
            },
            second =>
            {
                Assert.Equal("LocB", second.LocationName);
                Assert.True(second.RequiresExtensionLine);
            });
    }

    // ─── Cache miss ──────────────────────────────────────────────────────────

    [Fact]
    public void TryLoad_WhenNoFileExists_ReturnsNull()
    {
        var (cache, _, key) = SetupCache();

        var result = cache.TryLoad(key);

        Assert.Null(result);
    }

    // ─── Cache hit round-trip ─────────────────────────────────────────────────

    [Fact]
    public void TryLoad_AfterSave_ReturnsSameEntries()
    {
        var (cache, groupKey, key) = SetupCache();
        var entries = new List<CachedCompositePlanEntry>
        {
            new("LocationA", MakePlan("pin_01", 30.0)),
            new("LocationB", MakePlan("pin_02", 120.0))
        };

        cache.Save(key, groupKey, "manual-default", entries);
        var loaded = cache.TryLoad(key);

        try
        {
            Assert.NotNull(loaded);
            Assert.Equal(2, loaded!.Count);

            var a = loaded.Find(e => e.LocationId == "LocationA");
            Assert.NotNull(a);
            Assert.Equal("pin_01", a!.Plan.PairId);
            Assert.Equal(30.0, a.Plan.TargetAngleDeg, precision: 4);

            var b = loaded.Find(e => e.LocationId == "LocationB");
            Assert.NotNull(b);
            Assert.Equal("pin_02", b!.Plan.PairId);
            Assert.Equal(120.0, b.Plan.TargetAngleDeg, precision: 4);
        }
        finally
        {
            cache.Invalidate(groupKey);
        }
    }

    [Fact]
    public void TryLoad_AfterSave_PreservesMatrixCoefficients()
    {
        var (cache, groupKey, key) = SetupCache();
        var plan = MakePlan("pin_03", 45.0);
        plan.HeadLayer.Transform = new Matrix(2.5, 0.1, -0.1, 2.5, 7.0, 14.0);

        cache.Save(key, groupKey, "manual-default", new[] { new CachedCompositePlanEntry("Loc", plan) });
        var loaded = cache.TryLoad(key);

        try
        {
            Assert.NotNull(loaded);
            var m = loaded![0].Plan.HeadLayer.Transform;
            Assert.Equal(2.5, m.M11, precision: 6);
            Assert.Equal(0.1, m.M12, precision: 6);
            Assert.Equal(-0.1, m.M21, precision: 6);
            Assert.Equal(2.5, m.M22, precision: 6);
            Assert.Equal(7.0, m.OffsetX, precision: 6);
            Assert.Equal(14.0, m.OffsetY, precision: 6);
        }
        finally
        {
            cache.Invalidate(groupKey);
        }
    }

    [Fact]
    public void TryLoad_AfterSave_PreservesClipPolygonPoints()
    {
        var (cache, groupKey, key) = SetupCache();
        var plan = MakePlan("pin_04", 90.0);
        plan.ShaftTipCapLayer.ClipPolygon = new List<Point>
        {
            new(1.5, 2.5), new(38.5, 2.5), new(38.5, 17.5), new(1.5, 17.5)
        };

        cache.Save(key, groupKey, "manual-default", new[] { new CachedCompositePlanEntry("Loc", plan) });
        var loaded = cache.TryLoad(key);

        try
        {
            Assert.NotNull(loaded);
            var poly = loaded![0].Plan.ShaftTipCapLayer.ClipPolygon;
            Assert.Equal(4, poly.Count);
            Assert.Equal(1.5, poly[0].X, precision: 6);
            Assert.Equal(2.5, poly[0].Y, precision: 6);
            Assert.Equal(38.5, poly[2].X, precision: 6);
        }
        finally
        {
            cache.Invalidate(groupKey);
        }
    }

    // ─── Invalidation ─────────────────────────────────────────────────────────

    [Fact]
    public void TryLoad_AfterInvalidate_ReturnsNull()
    {
        var (cache, groupKey, key) = SetupCache();
        cache.Save(key, groupKey, "manual-default", new[] { new CachedCompositePlanEntry("Loc", MakePlan("pin_05", 0)) });

        cache.Invalidate(groupKey);
        var result = cache.TryLoad(key);

        Assert.Null(result);
    }

    [Fact]
    public void Invalidate_DoesNotDeleteEntriesForDifferentGroupKey()
    {
        var cache = new CompositePinPlanCache(new MockLogger());
        var groupA = "group_a_" + Guid.NewGuid().ToString("N")[..8];
        var groupB = "group_b_" + Guid.NewGuid().ToString("N")[..8];
        var keyA   = cache.ComputeCacheKey(groupA, "v1", "lh", "gh", "ch");
        var keyB   = cache.ComputeCacheKey(groupB, "v1", "lh", "gh", "ch");

        cache.Save(keyA, groupA, "v1", new[] { new CachedCompositePlanEntry("LocA", MakePlan("pin_06", 10)) });
        cache.Save(keyB, groupB, "v1", new[] { new CachedCompositePlanEntry("LocB", MakePlan("pin_07", 20)) });

        cache.Invalidate(groupA);

        try
        {
            Assert.Null(cache.TryLoad(keyA));
            Assert.NotNull(cache.TryLoad(keyB));
        }
        finally
        {
            cache.Invalidate(groupB);
        }
    }

    // ─── Key uniqueness ───────────────────────────────────────────────────────

    [Fact]
    public void ComputeCacheKey_DifferentLayoutContentHash_ProducesDifferentKeys()
    {
        var cache = new CompositePinPlanCache(new MockLogger());
        var k1 = cache.ComputeCacheKey("group", "v1", "hash1", "geo", "cfg");
        var k2 = cache.ComputeCacheKey("group", "v1", "hash2", "geo", "cfg");

        Assert.NotEqual(k1, k2);
    }

    [Fact]
    public void ComputeCacheKey_SameInputs_ProducesSameKey()
    {
        var cache = new CompositePinPlanCache(new MockLogger());
        var k1 = cache.ComputeCacheKey("group", "v1", "layoutHash", "geoHash", "cfgHash");
        var k2 = cache.ComputeCacheKey("group", "v1", "layoutHash", "geoHash", "cfgHash");

        Assert.Equal(k1, k2);
    }

    // ─── Content hasher ───────────────────────────────────────────────────────

    [Fact]
    public void LayoutContentHasher_SameMarkers_SameHash()
    {
        var markers = new List<ManualLayoutMarker>
        {
            new("B", new Point(5, 5), new Point(10, 20), 45.0, 30.0) { PairId = "pin_01" },
            new("A", new Point(1, 1), new Point(2, 3),  30.0, 20.0)
        };
        var h1 = CompositePinLayoutContentHasher.ComputeLayoutContentHash(markers);
        var h2 = CompositePinLayoutContentHasher.ComputeLayoutContentHash(markers);

        Assert.Equal(h1, h2);
    }

    [Fact]
    public void LayoutContentHasher_DifferentAngle_DifferentHash()
    {
        var m1 = new List<ManualLayoutMarker>
            { new("A", new Point(1,1), new Point(2,2), 45.0, 30.0) };
        var m2 = new List<ManualLayoutMarker>
            { new("A", new Point(1,1), new Point(2,2), 46.0, 30.0) };

        Assert.NotEqual(
            CompositePinLayoutContentHasher.ComputeLayoutContentHash(m1),
            CompositePinLayoutContentHasher.ComputeLayoutContentHash(m2));
    }

    [Fact]
    public void LayoutContentHasher_OrderIndependent()
    {
        var m1 = new List<ManualLayoutMarker>
        {
            new("A", new Point(1,1), new Point(2,2), 10.0, 20.0),
            new("B", new Point(3,3), new Point(4,4), 20.0, 30.0)
        };
        var m2 = new List<ManualLayoutMarker>
        {
            new("B", new Point(3,3), new Point(4,4), 20.0, 30.0),
            new("A", new Point(1,1), new Point(2,2), 10.0, 20.0)
        };

        Assert.Equal(
            CompositePinLayoutContentHasher.ComputeLayoutContentHash(m1),
            CompositePinLayoutContentHasher.ComputeLayoutContentHash(m2));
    }

    [Fact]
    public void GeometryHasher_MissingFile_ReturnsPlaceholder()
    {
        var path = Path.Combine(Path.GetTempPath(), "missing_geo_" + Guid.NewGuid().ToString("N") + ".json");
        var hash = CompositePinLayoutContentHasher.ComputeGeometryHash(path);

        Assert.Equal("geometry-missing", hash);
    }

    [Fact]
    public void GeometryHasher_ExistingFile_ReturnsHexString()
    {
        var path = Path.Combine(Path.GetTempPath(), "test_geo_" + Guid.NewGuid().ToString("N") + ".json");
        File.WriteAllText(path, "{\"pairs\":{}}", Encoding.UTF8);

        try
        {
            var hash = CompositePinLayoutContentHasher.ComputeGeometryHash(path);
            Assert.NotNull(hash);
            Assert.Equal(16, hash.Length);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void GeometryHasher_ChangedFileContent_DifferentHash()
    {
        var path = Path.Combine(Path.GetTempPath(), "test_geo2_" + Guid.NewGuid().ToString("N") + ".json");
        File.WriteAllText(path, "{\"v\":1}", Encoding.UTF8);
        var h1 = CompositePinLayoutContentHasher.ComputeGeometryHash(path);
        File.WriteAllText(path, "{\"v\":2}", Encoding.UTF8);
        var h2 = CompositePinLayoutContentHasher.ComputeGeometryHash(path);

        try { Assert.NotEqual(h1, h2); }
        finally { File.Delete(path); }
    }

    [Fact]
    public void ConfigHasher_SameConfig_SameHash()
    {
        var cfg = new PinPartConfig { MaxResidualRotationDeg = 20.0, MinStretchFactor = 0.75, MaxStretchFactor = 1.35 };
        Assert.Equal(
            CompositePinLayoutContentHasher.ComputeConfigHash(cfg),
            CompositePinLayoutContentHasher.ComputeConfigHash(cfg));
    }

    [Fact]
    public void ConfigHasher_DifferentStretchFactor_DifferentHash()
    {
        var c1 = new PinPartConfig { MinStretchFactor = 0.75, MaxStretchFactor = 1.35 };
        var c2 = new PinPartConfig { MinStretchFactor = 0.75, MaxStretchFactor = 1.50 };
        Assert.NotEqual(
            CompositePinLayoutContentHasher.ComputeConfigHash(c1),
            CompositePinLayoutContentHasher.ComputeConfigHash(c2));
    }

    [Fact]
    public void BuildApplyInstructions_ReconstructsExtendedFromAngleAndLength()
    {
        var cache    = new CompositePinPlanCache(new MockLogger());
        var planning = new CompositePinPlanningService(
            new PinPartPlacementCalculator(),
            new CompositePinRenderPlanBuilder());
        var service = new CompositePinApplicationService(cache, planning);

        var layout = new ManualLayout
        {
            GroupKey  = "g1",
            VariantId = "manual-default",
            Markers   = new List<ManualLayoutMarker>()
        };

        var applications = new List<LayoutEditorController.LayoutMarkerApplication>
        {
            new("LocA", new Point(100, 200), new Point(150, 180), true)
            {
                Angle      = 90,
                LineLength = 50
            }
        };

        var sourceCoords = new Dictionary<string, (double PixelX, double PixelY)>
        {
            ["LocA"] = (1100, 900)
        };

        var viewport = new ViewportState
        {
            SourceImageWidth  = 8198,
            SourceImageHeight = 5542,
            ViewportX         = 1000,
            ViewportY         = 800,
            ViewportWidth     = 500,
            ViewportHeight    = 400,
            ZoomLevel         = 55
        };

        var result = service.BuildApplyInstructions(
            layout,
            applications,
            sourceCoords,
            viewport,
            1920,
            1080,
            new PinPartConfig(),
            "group-key",
            Path.Combine(Path.GetTempPath(), "missing_geometry.json"),
            canUseCompositePins: false);

        var instruction = Assert.Single(result.Instructions);
        Assert.NotEqual(applications[0].OriginalPosition, instruction.OriginalScreen);
        Assert.Equal(instruction.OriginalScreen.X + 50, instruction.ExtendedScreen.X, 3);
        Assert.False(result.ShouldSaveToCache);
    }

    [Fact]
    public void BuildApplyInstructions_SourceExtendedHead_ShaftLengthIsZoomInvariant()
    {
        // Regression (2026-06-23): a source-space pin head was re-projected through the current
        // (zoomed) viewport, so the shaft grew with the zoom factor. With a full-map reference
        // viewport the tip→head screen distance must stay constant across zoom levels.
        var cache    = new CompositePinPlanCache(new MockLogger());
        var planning = new CompositePinPlanningService(
            new PinPartPlacementCalculator(),
            new CompositePinRenderPlanBuilder());
        var service = new CompositePinApplicationService(cache, planning);

        const double imageW = 8198, imageH = 5542;
        const double containerW = 1920, containerH = 1080;

        var layout = new ManualLayout
        {
            GroupKey  = "g1",
            VariantId = "manual-default",
            Markers   = new List<ManualLayoutMarker>()
        };

        // Head sits 50 source-px right of the location; angle/length are irrelevant on this path.
        var applications = new List<LayoutEditorController.LayoutMarkerApplication>
        {
            new("LocA", new Point(100, 200), new Point(150, 200), true)
            {
                SourceExtendedX = 1150,
                SourceExtendedY = 900,
                Angle           = 90,
                LineLength      = 50
            }
        };
        var sourceCoords = new Dictionary<string, (double PixelX, double PixelY)>
        {
            ["LocA"] = (1100, 900)
        };

        var fullMap = ViewportState.CreateFullMapView(imageW, imageH, containerW, containerH);

        double ShaftLength(double zoomLevel)
        {
            var viewport = ViewportState.CreateZoomedView(
                1100, 900, zoomLevel, imageW, imageH, containerW, containerH);
            var result = service.BuildApplyInstructions(
                layout, applications, sourceCoords, viewport,
                containerW, containerH, new PinPartConfig(), "group-key",
                Path.Combine(Path.GetTempPath(), "missing_geometry.json"),
                canUseCompositePins: false, fullMapViewport: fullMap);
            var ins = Assert.Single(result.Instructions);
            var dx = ins.ExtendedScreen.X - ins.OriginalScreen.X;
            var dy = ins.ExtendedScreen.Y - ins.OriginalScreen.Y;
            return Math.Sqrt(dx * dx + dy * dy);
        }

        var lengthAt10x = ShaftLength(10);
        var lengthAt55x = ShaftLength(55);

        // Same screen-space shaft length regardless of zoom — and it equals the full-map length.
        Assert.Equal(lengthAt10x, lengthAt55x, 3);
        var fmScale = containerW / fullMap.GetSourceRect().Width;
        Assert.Equal(50 * fmScale, lengthAt10x, 3);
    }
}
