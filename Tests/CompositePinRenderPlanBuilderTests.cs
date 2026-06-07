using System.Collections.Generic;
using System.Windows;
using InteractiveWorldMap.Models;
using InteractiveWorldMap.Services;
using Xunit;

namespace InteractiveWorldMap.Tests;

public class CompositePinRenderPlanBuilderTests
{
    [Fact]
    public void BuildPlan_WhenSegmentIsLongEnough_AnchorsTipAndJoinAndStretchesOnlyBody()
    {
        var builder = new CompositePinRenderPlanBuilder();
        var target = new PinPlacementTarget
        {
            StartScreen = new Point(100, 320),
            EndScreen = new Point(100, 100),
            LocationId = "loc-1",
            GroupId = 1
        };
        var config = new PinPartConfig
        {
            PartsFolderPath = "Pins_v2/parts"
        };
        var geometry = CreateVerticalGeometry();
        var placement = new PinPartPlacementResult
        {
            PairId = "pin_a",
            PairGeometry = geometry,
            TargetAngleDeg = 0.0,
            TargetLengthPx = 220.0,
            RequestedRotationDeg = 0.0,
            RequestedStretchFactor = 1.375
        };

        var plan = builder.BuildPlan(target, placement, config);

        // overallScale = targetLength(220) / nativeLength(160) = 1.375
        // scaledTipCap = 30 * 1.375 = 41.25
        // scaledHeadCap = 30 * 1.375 = 41.25
        // targetBodyLength = 220 - 41.25 - 41.25 = 137.5
        // bodyStretch = 137.5 / stretchableLength(100) = 1.375
        Assert.Equal("pin_a", plan.PairId);
        Assert.Equal(220.0, Distance(plan.TipAnchorLocal, plan.JoinAnchorLocal), 1);
        Assert.Equal(plan.JoinAnchorLocal, plan.HeadAttachLocal);
        Assert.Equal(41.25, Distance(plan.TipAnchorLocal, plan.StretchStartLocal), 1);
        Assert.Equal(178.75, Distance(plan.TipAnchorLocal, plan.StretchEndLocal), 1);
        Assert.Equal(137.5, plan.StretchBodyLengthPx, 1);
        Assert.Equal(1.375, plan.BodyStretchFactor, 3);
        Assert.Equal(@"Pins_v2/parts\pin_a_shaft.png", plan.ShaftSourcePath);
        Assert.Equal(@"Pins_v2/parts\pin_a_head.png", plan.HeadSourcePath);
        Assert.True(plan.ShaftTipCapLayer.ClipPolygon.Count >= 3);
        Assert.True(plan.ShaftBodyLayer.ClipPolygon.Count >= 3);
        Assert.True(plan.ShaftHeadCapLayer.ClipPolygon.Count >= 3);
    }

    [Fact]
    public void BuildPlan_RotatesHeadSoAttachToCenterAlignsWithTargetSegment()
    {
        var builder = new CompositePinRenderPlanBuilder();
        var target = new PinPlacementTarget
        {
            StartScreen = new Point(50, 50),
            EndScreen = new Point(170, 50),
            LocationId = "loc-2",
            GroupId = 3
        };
        var geometry = CreateVerticalGeometry();
        var placement = new PinPartPlacementResult
        {
            PairId = "pin_b",
            PairGeometry = geometry,
            TargetAngleDeg = 90.0,
            TargetLengthPx = 120.0,
            RequestedRotationDeg = 90.0,
            RequestedStretchFactor = 0.75
        };

        var plan = builder.BuildPlan(target, placement, new PinPartConfig());

        Assert.Equal(90.0, plan.HeadRotationDeg, 1);
        Assert.Equal(90.0, plan.TargetAngleDeg, 1);
    }

    private static PinPartGeometryEntry CreateVerticalGeometry()
    {
        return new PinPartGeometryEntry
        {
            HeadFile = "pin_a_head.png",
            ShaftFile = "pin_a_shaft.png",
            Head = new PinPartHeadGeometry
            {
                ImageSize = new PinPartImageSize
                {
                    Width = 80,
                    Height = 80
                },
                LocalCenter = new PinPartPoint { X = 40, Y = 40 },
                LocalAttach = new PinPartPoint { X = 40, Y = 70 },
                StubDirectionDeg = 180.0
            },
            Shaft = new PinPartShaftGeometry
            {
                ImageSize = new PinPartImageSize
                {
                    Width = 100,
                    Height = 200
                },
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
        };
    }

    // -------------------------------------------------------------------------
    // Guard-clause tests (ValidateInputs)
    // -------------------------------------------------------------------------

    [Fact]
    public void BuildPlan_NullTarget_ThrowsArgumentNullException()
    {
        var builder   = new CompositePinRenderPlanBuilder();
        var placement = new PinPartPlacementResult
        {
            PairId = "x", PairGeometry = CreateVerticalGeometry(),
            TargetLengthPx = 220.0
        };
        Assert.Throws<ArgumentNullException>(() =>
            builder.BuildPlan(null!, placement, new PinPartConfig()));
    }

    [Fact]
    public void BuildPlan_NullPlacement_ThrowsArgumentNullException()
    {
        var builder = new CompositePinRenderPlanBuilder();
        var target  = new PinPlacementTarget
        {
            StartScreen = new Point(100, 320), EndScreen = new Point(100, 100)
        };
        Assert.Throws<ArgumentNullException>(() =>
            builder.BuildPlan(target, null!, new PinPartConfig()));
    }

    [Fact]
    public void BuildPlan_NullPairGeometry_ThrowsArgumentException()
    {
        var builder   = new CompositePinRenderPlanBuilder();
        var target    = new PinPlacementTarget
        {
            StartScreen = new Point(100, 320), EndScreen = new Point(100, 100)
        };
        var placement = new PinPartPlacementResult
        {
            PairId = "x", PairGeometry = null!, TargetLengthPx = 220.0
        };
        Assert.Throws<ArgumentException>(() =>
            builder.BuildPlan(target, placement, new PinPartConfig()));
    }

    [Fact]
    public void BuildPlan_NullConfig_ThrowsArgumentNullException()
    {
        var builder   = new CompositePinRenderPlanBuilder();
        var target    = new PinPlacementTarget
        {
            StartScreen = new Point(100, 320), EndScreen = new Point(100, 100)
        };
        var placement = new PinPartPlacementResult
        {
            PairId = "x", PairGeometry = CreateVerticalGeometry(), TargetLengthPx = 220.0
        };
        Assert.Throws<ArgumentNullException>(() =>
            builder.BuildPlan(target, placement, null!));
    }

    [Fact]
    public void BuildPlan_TargetTooShortForCaps_ThrowsInvalidOperationException()
    {
        // TipCapLength(30) + HeadCapLength(30) = 60 px caps.
        // A zero-distance target (StartScreen == EndScreen) → targetBodyLength = -60 → throws.
        var builder   = new CompositePinRenderPlanBuilder();
        var target    = new PinPlacementTarget
        {
            StartScreen = new Point(100, 100), EndScreen = new Point(100, 100)
        };
        var placement = new PinPartPlacementResult
        {
            PairId = "x", PairGeometry = CreateVerticalGeometry(), TargetLengthPx = 0.0
        };
        Assert.Throws<InvalidOperationException>(() =>
            builder.BuildPlan(target, placement, new PinPartConfig()));
    }

    [Fact]
    public void BuildPlan_ValidInput_CanvasDimensionsArePositive()
    {
        var builder   = new CompositePinRenderPlanBuilder();
        var target    = new PinPlacementTarget
        {
            StartScreen = new Point(100, 320), EndScreen = new Point(100, 100)
        };
        var placement = new PinPartPlacementResult
        {
            PairId         = "pin_a",
            PairGeometry   = CreateVerticalGeometry(),
            TargetAngleDeg = 0.0,
            TargetLengthPx = 220.0
        };

        var plan = builder.BuildPlan(target, placement, new PinPartConfig());

        Assert.True(plan.Width  > 0, "Canvas width must be positive");
        Assert.True(plan.Height > 0, "Canvas height must be positive");
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static double Distance(Point a, Point b)
    {
        var dx = b.X - a.X;
        var dy = b.Y - a.Y;
        return System.Math.Sqrt((dx * dx) + (dy * dy));
    }
}
