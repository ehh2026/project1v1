using System.Windows;
using InteractiveWorldMap.Models;
using InteractiveWorldMap.Services;
using Xunit;

namespace InteractiveWorldMap.Tests;

public class CompositePinPlacementPolicyTests
{
    [Fact]
    public void GetCompositeTopLeft_KeepsTipOnNormalMarkerCenter()
    {
        var placement = new MarkerScreenPlacement("alpha", Left: 90, Top: 140);
        var plan = new CompositePinRenderPlan
        {
            TipAnchorLocal = new Point(12, 30)
        };

        var topLeft = CompositePinPlacementPolicy.GetCompositeTopLeft(
            placement,
            locationMarkerSize: 20,
            plan);

        Assert.Equal(88, topLeft.X, 6);
        Assert.Equal(120, topLeft.Y, 6);
    }

    [Fact]
    public void GetCompositeTopLeft_FromTipScreen_UsesSameAnchorMath()
    {
        var plan = new CompositePinRenderPlan
        {
            TipAnchorLocal = new Point(12, 30)
        };

        var topLeft = CompositePinPlacementPolicy.GetCompositeTopLeft(new Point(100, 200), plan);

        Assert.Equal(88, topLeft.X, 6);
        Assert.Equal(170, topLeft.Y, 6);
    }

    [Fact]
    public void GetCompositeTopLeft_ReprojectsAcrossViewportPlacementsWithoutChangingTipAnchor()
    {
        var plan = new CompositePinRenderPlan
        {
            TipAnchorLocal = new Point(12, 30)
        };

        var first = CompositePinPlacementPolicy.GetCompositeTopLeft(
            new MarkerScreenPlacement("alpha", Left: 90, Top: 140),
            locationMarkerSize: 20,
            plan);
        var second = CompositePinPlacementPolicy.GetCompositeTopLeft(
            new MarkerScreenPlacement("alpha", Left: 290, Top: 340),
            locationMarkerSize: 20,
            plan);

        Assert.Equal(200, second.X - first.X, 6);
        Assert.Equal(200, second.Y - first.Y, 6);
    }

    [Fact]
    public void ShouldRepositionOnly_NullExistingPlan_ReturnsFalse()
    {
        var target = StubTarget(startX: 100, startY: 200, length: 24);

        Assert.False(CompositePinPlacementPolicy.ShouldRepositionOnly(null, target));
    }

    [Fact]
    public void ShouldRepositionOnly_UnchangedStubVectorWithMovedTip_ReturnsTrue()
    {
        var plan = Plan(angleDeg: 0, lengthPx: 24);
        var pannedTarget = StubTarget(startX: 400, startY: 500, length: 24);

        Assert.True(CompositePinPlacementPolicy.ShouldRepositionOnly(plan, pannedTarget));
    }

    [Fact]
    public void ShouldRepositionOnly_ChangedStubLength_ReturnsFalse()
    {
        var plan = Plan(angleDeg: 0, lengthPx: 24);
        var target = StubTarget(startX: 100, startY: 200, length: 30);

        Assert.False(CompositePinPlacementPolicy.ShouldRepositionOnly(plan, target));
    }

    [Fact]
    public void ShouldRepositionOnly_ChangedStubAngle_ReturnsFalse()
    {
        var plan = Plan(angleDeg: 0, lengthPx: 24);
        var target = ExtensionTarget(startX: 100, startY: 200, endX: 120, endY: 176);

        Assert.False(CompositePinPlacementPolicy.ShouldRepositionOnly(plan, target));
    }

    [Fact]
    public void ShouldRepositionOnly_UnchangedExtensionAngleAndLength_ReturnsTrue()
    {
        var plan = Plan(angleDeg: 45, lengthPx: 40);
        var pannedTarget = ExtensionTarget(startX: 300, startY: 300, endX: 328.284, endY: 271.716);

        Assert.True(CompositePinPlacementPolicy.ShouldRepositionOnly(plan, pannedTarget));
    }

    [Fact]
    public void ShouldRepositionOnly_ChangedExtensionLength_ReturnsFalse()
    {
        var plan = Plan(angleDeg: 45, lengthPx: 40);
        var target = ExtensionTarget(startX: 100, startY: 200, endX: 124.748, endY: 175.252);

        Assert.False(CompositePinPlacementPolicy.ShouldRepositionOnly(plan, target));
    }

    [Fact]
    public void ShouldRepositionOnly_PreferredPairMismatch_ReturnsFalse()
    {
        var plan = new CompositePinRenderPlan
        {
            PairId = "pin_a",
            TargetAngleDeg = 0,
            TargetLengthPx = 24
        };
        var target = StubTarget(startX: 100, startY: 200, length: 24);

        Assert.False(CompositePinPlacementPolicy.ShouldRepositionOnly(
            plan, target, preferredPairId: "pin_b"));
    }

    [Fact]
    public void ShouldRepositionOnly_PreferredHeadMismatch_ReturnsFalse()
    {
        var plan = new CompositePinRenderPlan
        {
            HeadSourcePath = "Images&Content/Pins_v2/parts/heads/pin_01.png",
            TargetAngleDeg = 0,
            TargetLengthPx = 24
        };
        var target = StubTarget(startX: 100, startY: 200, length: 24);

        Assert.False(CompositePinPlacementPolicy.ShouldRepositionOnly(
            plan, target, preferredHeadSourcePath: "Images&Content/Pins_v2/parts/heads/pin_02.png"));
    }

    private static CompositePinRenderPlan Plan(double angleDeg, double lengthPx) =>
        new()
        {
            TargetAngleDeg = angleDeg,
            TargetLengthPx = lengthPx
        };

    private static PinPlacementTarget StubTarget(double startX, double startY, double length) =>
        new()
        {
            StartScreen = new Point(startX, startY),
            EndScreen = new Point(startX, startY - length),
            LocationId = "alpha",
            GroupId = 0
        };

    private static PinPlacementTarget ExtensionTarget(double startX, double startY, double endX, double endY) =>
        new()
        {
            StartScreen = new Point(startX, startY),
            EndScreen = new Point(endX, endY),
            LocationId = "alpha",
            GroupId = 0
        };
}
