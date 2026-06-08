using System.Collections.Generic;
using System.Windows;
using InteractiveWorldMap.Models;
using InteractiveWorldMap.Services;
using Xunit;

namespace InteractiveWorldMap.Tests;

public class CompositePinShaftMenuModelBuilderTests
{
    [Fact]
    public void BuildMenuItems_LowestScoreFirst_AndCurrentPairMarkedSelected()
    {
        var builder = new CompositePinShaftMenuModelBuilder(new PinPartPlacementCalculator());
        var target = new PinPlacementTarget
        {
            StartScreen = new Point(0, 0),
            EndScreen = new Point(0, -100), // north; angle ≈ 0°
            LocationId = "loc-1",
            GroupId = 0
        };
        var config = new PinPartConfig
        {
            SelectionMode = PinPartSelectionMode.NearestFit,
            MaxResidualRotationDeg = 30.0,
            MinStretchFactor = 0.5,
            MaxStretchFactor = 2.0,
            PartsFolderPath = "Pins_v2/parts"
        };
        var candidates = new Dictionary<string, PinPartGeometryEntry>
        {
            ["pin_a"] = MakeEntry(nativeAngle: 0.0, nativeLength: 100.0),
            ["pin_b"] = MakeEntry(nativeAngle: 45.0, nativeLength: 100.0)
        };

        var items = builder.BuildMenuItems(target, candidates, config, currentPairId: "pin_a");

        Assert.Equal(2, items.Count);
        Assert.Equal("pin_a", items[0].PairId); // best fit — same angle
        Assert.True(items[0].IsSelected);
        Assert.False(items[1].IsSelected);
        Assert.True(items[0].Score < items[1].Score);
    }

    [Fact]
    public void BuildMenuItems_WhenCurrentPairIdNull_NoItemMarkedSelected()
    {
        var builder = new CompositePinShaftMenuModelBuilder(new PinPartPlacementCalculator());
        var target = new PinPlacementTarget
        {
            StartScreen = new Point(0, 0),
            EndScreen = new Point(0, -50),
            LocationId = "loc-2",
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
            ["pin_x"] = MakeEntry(nativeAngle: 10.0, nativeLength: 50.0)
        };

        var items = builder.BuildMenuItems(target, candidates, config, currentPairId: null);

        Assert.Single(items);
        Assert.False(items[0].IsSelected);
    }

    private static PinPartGeometryEntry MakeEntry(double nativeAngle, double nativeLength)
        => new()
        {
            ShaftFile = "shaft.png",
            HeadFile = "head.png",
            Shaft = new PinPartShaftGeometry
            {
                NativeAngleDeg = nativeAngle,
                NativeLength = nativeLength,
                LocalTip = new PinPartPoint { X = 5, Y = 50 },
                LocalJoin = new PinPartPoint { X = 5, Y = 5 }
            }
        };
}
