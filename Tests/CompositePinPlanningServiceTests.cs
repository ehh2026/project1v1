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

        Assert.True(result.Selection.IsRotationClamped);
        Assert.True(result.Selection.IsStretchClamped);
        Assert.Equal(120.0, Distance(result.RenderPlan.TipAnchorLocal, result.RenderPlan.JoinAnchorLocal), 1);
        Assert.Equal(result.RenderPlan.JoinAnchorLocal, result.RenderPlan.HeadAttachLocal);
        Assert.Equal(60.0, result.RenderPlan.StretchBodyLengthPx, 1);
    }

    private static double Distance(Point a, Point b)
    {
        var dx = b.X - a.X;
        var dy = b.Y - a.Y;
        return System.Math.Sqrt((dx * dx) + (dy * dy));
    }
}
