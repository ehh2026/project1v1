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
}
