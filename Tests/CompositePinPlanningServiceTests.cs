using System.Collections.Generic;
using System.Windows;
using InteractiveWorldMap.Models;
using InteractiveWorldMap.Services;
using Xunit;

namespace InteractiveWorldMap.Tests;

public class CompositePinPlanningServiceTests
{
    [Fact]
    public void BuildPlan_WhenSelectionClampsDiagnostics_RenderPlanStillMatchesExactTargetSegment()
    {
        var service = new CompositePinPlanningService(
            new PinPartPlacementCalculator(),
            new CompositePinRenderPlanBuilder());

        var target = new PinPlacementTarget
        {
            StartScreen = new Point(20, 220),
            EndScreen = new Point(140, 220),
            LocationId = "loc-1",
            GroupId = 1
        };
        var config = new PinPartConfig
        {
            PartsFolderPath = "Pins_v2/parts",
            SelectionMode = PinPartSelectionMode.NearestFit,
            MaxResidualRotationDeg = 5.0,
            MinStretchFactor = 0.95,
            MaxStretchFactor = 1.05
        };
        var candidates = new Dictionary<string, PinPartGeometryEntry>
        {
            ["pin_a"] = new()
            {
                HeadFile = "pin_a_head.png",
                ShaftFile = "pin_a_shaft.png",
                Head = new PinPartHeadGeometry
                {
                    ImageSize = new PinPartImageSize { Width = 80, Height = 80 },
                    LocalCenter = new PinPartPoint { X = 40, Y = 40 },
                    LocalAttach = new PinPartPoint { X = 40, Y = 70 },
                    StubDirectionDeg = 180.0
                },
                Shaft = new PinPartShaftGeometry
                {
                    ImageSize = new PinPartImageSize { Width = 100, Height = 200 },
                    LocalTip = new PinPartPoint { X = 50, Y = 180 },
                    LocalJoin = new PinPartPoint { X = 50, Y = 20 },
                    NativeAngleDeg = 0.0,
                    NativeLength = 160.0,
                    Segmentation = new PinPartShaftSegmentation
                    {
                        TipCapLength = 30.0,
                        HeadCapLength = 30.0,
                        StretchStartDistance = 30.0,
                        StretchEndDistance = 130.0,
                        StretchableLength = 100.0,
                        MinimumMiddleRatio = 0.25
                    }
                }
            }
        };

        var result = service.BuildPlan(target, candidates, config);

        // overallScale = targetLength(120) / nativeLength(160) = 0.75
        // scaledTipCap = 30 * 0.75 = 22.5; scaledHeadCap = 30 * 0.75 = 22.5
        // targetBodyLength = 120 - 22.5 - 22.5 = 75.0
        Assert.True(result.Selection.IsRotationClamped);
        Assert.True(result.Selection.IsStretchClamped);
        Assert.Equal(120.0, Distance(result.RenderPlan.TipAnchorLocal, result.RenderPlan.JoinAnchorLocal), 1);
        Assert.Equal(result.RenderPlan.JoinAnchorLocal, result.RenderPlan.HeadAttachLocal);
        Assert.Equal(75.0, result.RenderPlan.StretchBodyLengthPx, 1);
    }

    // ─── Phase 2: session cache + preferred overrides ────────────────────────

    [Fact]
    public void BuildPlan_PopulatesSessionCache_TryGetLastResultReturnsIt()
    {
        var service = MakeService();
        var (target, candidates, config) = MakeFixture("loc-cache");

        service.BuildPlan(target, candidates, config);

        Assert.True(service.TryGetLastResult("loc-cache", out var cached));
        Assert.NotNull(cached);
        Assert.Equal("pin_a", cached!.RenderPlan.PairId);
    }

    [Fact]
    public void TryGetLastResult_BeforeBuildPlan_ReturnsFalse()
    {
        var service = MakeService();
        Assert.False(service.TryGetLastResult("never-built", out _));
    }

    [Fact]
    public void BuildPlan_WithPreferredPairId_UsesNamedPairIfPresent()
    {
        var service = MakeService();
        var (target, candidates, config) = MakeFixture("loc-pref");

        // Only one candidate ("pin_a") — preferred id must match it.
        var result = service.BuildPlan(target, candidates, config, preferredPairId: "pin_a", preferredHeadSourcePath: null);

        Assert.Equal("pin_a", result.Selection.PairId);
    }

    [Fact]
    public void BuildPlan_WithUnknownPreferredPairId_FallsBackToScorer()
    {
        var service = MakeService();
        var (target, candidates, config) = MakeFixture("loc-fallback");

        // "pin_missing" is not in candidates → scorer should return "pin_a".
        var result = service.BuildPlan(target, candidates, config, preferredPairId: "pin_missing", preferredHeadSourcePath: null);

        Assert.Equal("pin_a", result.Selection.PairId);
    }

    // ─── helpers ──────────────────────────────────────────────────────────────

    private static CompositePinPlanningService MakeService() =>
        new CompositePinPlanningService(new PinPartPlacementCalculator(), new CompositePinRenderPlanBuilder());

    private static (PinPlacementTarget, Dictionary<string, PinPartGeometryEntry>, PinPartConfig) MakeFixture(string locationId)
    {
        var target = new PinPlacementTarget
        {
            StartScreen = new Point(20, 220),
            EndScreen   = new Point(140, 220),
            LocationId  = locationId,
            GroupId     = 0
        };
        var config = new PinPartConfig
        {
            PartsFolderPath        = "Pins_v2/parts",
            SelectionMode          = PinPartSelectionMode.NearestFit,
            MaxResidualRotationDeg = 5.0,
            MinStretchFactor       = 0.95,
            MaxStretchFactor       = 1.05
        };
        var candidates = new Dictionary<string, PinPartGeometryEntry>
        {
            ["pin_a"] = new()
            {
                HeadFile  = "pin_a_head.png",
                ShaftFile = "pin_a_shaft.png",
                Head = new PinPartHeadGeometry
                {
                    ImageSize       = new PinPartImageSize { Width = 80, Height = 80 },
                    LocalCenter     = new PinPartPoint { X = 40, Y = 40 },
                    LocalAttach     = new PinPartPoint { X = 40, Y = 70 },
                    StubDirectionDeg = 180.0
                },
                Shaft = new PinPartShaftGeometry
                {
                    ImageSize   = new PinPartImageSize { Width = 100, Height = 200 },
                    LocalTip    = new PinPartPoint { X = 50, Y = 180 },
                    LocalJoin   = new PinPartPoint { X = 50, Y = 20 },
                    NativeAngleDeg = 0.0,
                    NativeLength   = 160.0,
                    Segmentation   = new PinPartShaftSegmentation
                    {
                        TipCapLength        = 30.0,
                        HeadCapLength       = 30.0,
                        StretchStartDistance = 30.0,
                        StretchEndDistance   = 130.0,
                        StretchableLength    = 100.0,
                        MinimumMiddleRatio   = 0.25
                    }
                }
            }
        };
        return (target, candidates, config);
    }

    private static double Distance(Point a, Point b)
    {
        var dx = b.X - a.X;
        var dy = b.Y - a.Y;
        return System.Math.Sqrt((dx * dx) + (dy * dy));
    }
}
