using System.Linq;
using System.Windows;
using System.Windows.Media;
using InteractiveWorldMap.Utilities;
using Xunit;

namespace InteractiveWorldMap.Tests;

public class PinTipCapGeometryTests
{
    [Fact]
    public void BuildHorizontal_IsOpenLineCenteredOnTip()
    {
        var tip = new Point(100, 200);

        var geometry = Assert.IsType<LineGeometry>(
            PinTipCapGeometry.BuildHorizontal(tip, widthPx: 12));

        Assert.Equal(new Point(94, 200), geometry.StartPoint);
        Assert.Equal(new Point(106, 200), geometry.EndPoint);
    }

    [Theory]
    [InlineData(-1.0, 197.0)]
    [InlineData(1.0, 203.0)]
    public void BuildConcave_FlipsEndpointsAwayFromHead(
        double shaftY,
        double endpointY)
    {
        var tip = new Point(100, 200);

        var geometry = Assert.IsType<PathGeometry>(
            PinTipCapGeometry.BuildConcave(
                tip,
                new Vector(0, shaftY),
                widthPx: 12,
                arcDepthPx: 3));

        var figure = Assert.Single(geometry.Figures);
        var curve = Assert.IsType<QuadraticBezierSegment>(
            Assert.Single(figure.Segments));
        var midpoint = QuadraticPoint(figure.StartPoint, curve.Point1, curve.Point2, 0.5);

        Assert.False(figure.IsClosed);
        Assert.Equal(endpointY, figure.StartPoint.Y, 6);
        Assert.Equal(endpointY, curve.Point2.Y, 6);
        Assert.Equal(tip.X, midpoint.X, 6);
        Assert.Equal(tip.Y, midpoint.Y, 6);
    }

    [Theory]
    [InlineData(0.8, -0.6, 197.0)]
    [InlineData(-0.8, 0.6, 203.0)]
    [InlineData(1.0, 0.00001, 197.0)]
    [InlineData(0.0, 0.0, 197.0)]
    public void BuildConcave_UsesVerticalHeadSideWithStableFallback(
        double shaftX,
        double shaftY,
        double endpointY)
    {
        var geometry = Assert.IsType<PathGeometry>(
            PinTipCapGeometry.BuildConcave(
                new Point(100, 200),
                new Vector(shaftX, shaftY),
                widthPx: 12,
                arcDepthPx: 3));

        Assert.Equal(endpointY, geometry.Figures[0].StartPoint.Y, 6);
    }

    [Fact]
    public void BuildConcave_ClampsNegativeDimensions()
    {
        var geometry = Assert.IsType<PathGeometry>(
            PinTipCapGeometry.BuildConcave(
                new Point(100, 200),
                new Vector(0, -1),
                widthPx: -12,
                arcDepthPx: -3));

        var figure = Assert.Single(geometry.Figures);
        var curve = Assert.IsType<QuadraticBezierSegment>(
            Assert.Single(figure.Segments));

        Assert.Equal(new Point(100, 200), figure.StartPoint);
        Assert.Equal(new Point(100, 200), curve.Point1);
        Assert.Equal(new Point(100, 200), curve.Point2);
    }

    private static Point QuadraticPoint(Point start, Point control, Point end, double t)
    {
        double inverse = 1.0 - t;
        return new Point(
            (inverse * inverse * start.X) + (2.0 * inverse * t * control.X) + (t * t * end.X),
            (inverse * inverse * start.Y) + (2.0 * inverse * t * control.Y) + (t * t * end.Y));
    }
}
