using System.Windows;
using InteractiveWorldMap.Models;
using InteractiveWorldMap.Utilities;
using Xunit;

namespace InteractiveWorldMap.Tests;

public class MarkerHitTargetGeometryTests
{
    [Fact]
    public void MarkerHitTargetConfig_Defaults_AreTouchFriendly()
    {
        var config = new MarkerHitTargetConfig();

        Assert.Equal(32.0, config.PinDiameterPx);
        Assert.Equal(40.0, config.ClusterDiameterPx);
    }

    [Theory]
    [InlineData(32.0, 14.0, 32.0)]
    [InlineData(10.0, 14.0, 14.0)]
    public void EffectiveDiameter_NeverShrinksBelowVisual(
        double configured,
        double visible,
        double expected)
    {
        Assert.Equal(
            expected,
            MarkerHitTargetGeometry.EffectiveDiameter(configured, visible));
    }

    [Fact]
    public void ToCanvasCenter_OffsetsLocalHeadCenterByMarkerPosition()
    {
        Assert.Equal(
            new Point(112.0, 64.0),
            MarkerHitTargetGeometry.ToCanvasCenter(
                new Point(100.0, 50.0),
                new Point(12.0, 14.0)));
    }
}
