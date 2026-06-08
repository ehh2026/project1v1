using System.Collections.Generic;
using System.Windows;
using InteractiveWorldMap.Models;
using InteractiveWorldMap.Services;
using Xunit;

namespace InteractiveWorldMap.Tests;

public class PinPartPlacementCalculatorTests
{
    [Fact]
    public void CalculatePlacement_WhenOnePairIsCloser_SelectsNearestPair()
    {
        var calculator = new PinPartPlacementCalculator();
        var target = new PinPlacementTarget
        {
            StartScreen = new Point(100, 200),
            EndScreen = new Point(130, 260),
            LocationId = "loc-1",
            GroupId = 1
        };
        var config = new PinPartConfig
        {
            SelectionMode = PinPartSelectionMode.NearestFit,
            MaxResidualRotationDeg = 20.0,
            MinStretchFactor = 0.75,
            MaxStretchFactor = 1.35
        };
        var candidates = new Dictionary<string, PinPartGeometryEntry>
        {
            ["pin_a"] = new()
            {
                HeadFile = "pin_a_head.png",
                ShaftFile = "pin_a_shaft.png",
                Shaft = new PinPartShaftGeometry
                {
                    NativeAngleDeg = 300.0,
                    NativeLength = 120.0,
                    LocalTip = new PinPartPoint { X = 5, Y = 50 },
                    LocalJoin = new PinPartPoint { X = 10, Y = 10 }
                }
            },
            ["pin_b"] = new()
            {
                HeadFile = "pin_b_head.png",
                ShaftFile = "pin_b_shaft.png",
                Shaft = new PinPartShaftGeometry
                {
                    NativeAngleDeg = 333.0,
                    NativeLength = 67.0,
                    LocalTip = new PinPartPoint { X = 5, Y = 50 },
                    LocalJoin = new PinPartPoint { X = 10, Y = 10 }
                }
            }
        };

        var placement = calculator.CalculatePlacement(target, candidates, config);

        Assert.Equal("pin_b", placement.PairId);
        Assert.Equal(333.0, placement.NativeAngleDeg);
    }

    [Fact]
    public void ScoreAll_ReturnsAllCandidatesOrderedByScore()
    {
        var calculator = new PinPartPlacementCalculator();
        var target = new PinPlacementTarget
        {
            StartScreen = new Point(0, 0),
            EndScreen = new Point(0, -100), // north; angle ≈ 0°
            LocationId = "loc-score",
            GroupId = 0
        };
        var config = new PinPartConfig
        {
            SelectionMode = PinPartSelectionMode.NearestFit,
            MaxResidualRotationDeg = 30.0,
            MinStretchFactor = 0.5,
            MaxStretchFactor = 2.0
        };
        var candidates = new Dictionary<string, PinPartGeometryEntry>
        {
            ["pin_best"] = new()
            {
                HeadFile = "pin_best_head.png",
                ShaftFile = "pin_best_shaft.png",
                Shaft = new PinPartShaftGeometry
                {
                    NativeAngleDeg = 0.0,
                    NativeLength = 100.0,
                    LocalTip = new PinPartPoint { X = 5, Y = 50 },
                    LocalJoin = new PinPartPoint { X = 5, Y = 5 }
                }
            },
            ["pin_ok"] = new()
            {
                HeadFile = "pin_ok_head.png",
                ShaftFile = "pin_ok_shaft.png",
                Shaft = new PinPartShaftGeometry
                {
                    NativeAngleDeg = 20.0,
                    NativeLength = 100.0,
                    LocalTip = new PinPartPoint { X = 5, Y = 50 },
                    LocalJoin = new PinPartPoint { X = 5, Y = 5 }
                }
            }
        };

        var results = calculator.ScoreAll(target, candidates, config);

        Assert.Equal(2, results.Count);
        Assert.Equal("pin_best", results[0].PairId);
        Assert.True(results[0].Score < results[1].Score);
    }

    [Fact]
    public void ScoreAll_WhenNoCandidates_ReturnsEmpty()
    {
        var calculator = new PinPartPlacementCalculator();
        var target = new PinPlacementTarget
        {
            StartScreen = new Point(0, 0),
            EndScreen = new Point(0, -100),
            LocationId = "loc-empty",
            GroupId = 0
        };
        var config = new PinPartConfig
        {
            SelectionMode = PinPartSelectionMode.NearestFit,
            MaxResidualRotationDeg = 30.0,
            MinStretchFactor = 0.5,
            MaxStretchFactor = 2.0
        };

        var results = calculator.ScoreAll(target, new Dictionary<string, PinPartGeometryEntry>(), config);

        Assert.Empty(results);
    }

    [Fact]
    public void CalculatePlacement_WhenNearestFit_ClampsResidualTransform()
    {
        var calculator = new PinPartPlacementCalculator();
        var target = new PinPlacementTarget
        {
            StartScreen = new Point(0, 0),
            EndScreen = new Point(120, 0),
            LocationId = "loc-2",
            GroupId = 2
        };
        var config = new PinPartConfig
        {
            SelectionMode = PinPartSelectionMode.NearestFit,
            MaxResidualRotationDeg = 10.0,
            MinStretchFactor = 0.90,
            MaxStretchFactor = 1.10
        };
        var candidates = new Dictionary<string, PinPartGeometryEntry>
        {
            ["pin_c"] = new()
            {
                HeadFile = "pin_c_head.png",
                ShaftFile = "pin_c_shaft.png",
                Shaft = new PinPartShaftGeometry
                {
                    NativeAngleDeg = 180.0,
                    NativeLength = 80.0,
                    LocalTip = new PinPartPoint { X = 0, Y = 80 },
                    LocalJoin = new PinPartPoint { X = 0, Y = 0 }
                }
            }
        };

        var placement = calculator.CalculatePlacement(target, candidates, config);

        Assert.True(placement.IsRotationClamped);
        Assert.True(placement.IsStretchClamped);
        Assert.Equal(-10.0, placement.AppliedRotationDeg);
        Assert.Equal(1.10, placement.AppliedStretchFactor);
    }
}
