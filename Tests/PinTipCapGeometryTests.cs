using System.Windows;
using System.Windows.Media;
using InteractiveWorldMap.Utilities;
using Xunit;

namespace InteractiveWorldMap.Tests;

/// <summary>
/// Geometry + direction invariants for drawn-pin tip caps. These prove the cap's
/// <em>direction</em> (concave bows toward the shaft — the "stuck-in" read) and width math,
/// not its aesthetics, which are a human visual gate (Phase 4b).
/// </summary>
public class PinTipCapGeometryTests
{
    // -------------------------------------------------------------------------
    // Half-width math
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData(6.0, 0.0, 3.0)]   // outline width 6 → half 3, no extend
    [InlineData(6.0, 2.0, 5.0)]   // + 2px extend each side
    [InlineData(0.0, 0.0, 0.0)]   // degenerate
    public void HalfWidth_IsHalfOutlinePlusExtend(double outlineWidth, double extend, double expected)
    {
        Assert.Equal(expected, PinTipCapGeometry.HalfWidth(outlineWidth, extend), 6);
    }

    // -------------------------------------------------------------------------
    // Horizontal bar
    // -------------------------------------------------------------------------

    [Fact]
    public void BuildHorizontal_RectSpansTipAndExtendsDownward()
    {
        var tip = new Point(100, 200);
        var geom = (RectangleGeometry)PinTipCapGeometry.BuildHorizontal(tip, halfWidth: 4.0, heightPx: 6.0);

        Assert.Equal(96.0, geom.Rect.Left, 6);    // tipX - halfWidth
        Assert.Equal(200.0, geom.Rect.Top, 6);    // at the tip
        Assert.Equal(8.0, geom.Rect.Width, 6);    // 2 * halfWidth
        Assert.Equal(6.0, geom.Rect.Height, 6);   // downward (+Y)
        Assert.Equal(206.0, geom.Rect.Bottom, 6); // extends below the tip
    }

    // -------------------------------------------------------------------------
    // Concave control point — sign / direction
    // -------------------------------------------------------------------------

    [Fact]
    public void ConcaveControlPoint_VerticalStub_LiftsTowardHead()
    {
        var tip = new Point(100, 200);
        var shaftDir = new Vector(0, -1); // toward head (up the screen)

        var control = PinTipCapGeometry.ConcaveControlPoint(tip, shaftDir, arcDepthPx: 3.0);

        Assert.Equal(100.0, control.X, 6);
        Assert.Equal(197.0, control.Y, 6); // tipY - ArcDepthPx (up = smaller Y)
    }

    [Fact]
    public void ConcaveMidpoint_BowsTowardShaft_ForVerticalStub()
    {
        var tip = new Point(100, 200);
        var shaftDir = new Vector(0, -1);

        var mid = PinTipCapGeometry.ConcaveMidpoint(tip, shaftDir, arcDepthPx: 3.0);

        // dot(mid - tip, shaftDir) > 0 → midpoint lies toward the shaft from the baseline.
        var toMid = new Vector(mid.X - tip.X, mid.Y - tip.Y);
        Assert.True(toMid * shaftDir > 0, "Concave midpoint must bow toward the shaft.");
    }

    [Fact]
    public void ConcaveMidpoint_BowsTowardShaft_ForTiltedExtensionLine()
    {
        // Extension-line case: shaft angled up-and-to-the-right toward the head.
        var tip = new Point(300, 400);
        var raw = new Vector(0.6, -0.8); // already unit length
        var shaftDir = raw;

        var mid = PinTipCapGeometry.ConcaveMidpoint(tip, shaftDir, arcDepthPx: 5.0);

        var toMid = new Vector(mid.X - tip.X, mid.Y - tip.Y);
        Assert.True(toMid * shaftDir > 0, "Concave midpoint must bow toward the tilted shaft.");
    }

    [Fact]
    public void ConcaveControlPoint_NormalizesNonUnitDirection()
    {
        var tip = new Point(0, 0);
        var shaftDir = new Vector(0, -10); // non-unit; depth must measure in px, not scaled by length

        var control = PinTipCapGeometry.ConcaveControlPoint(tip, shaftDir, arcDepthPx: 3.0);

        Assert.Equal(0.0, control.X, 6);
        Assert.Equal(-3.0, control.Y, 6);
    }

    // -------------------------------------------------------------------------
    // Concave path structure
    // -------------------------------------------------------------------------

    [Fact]
    public void BuildConcave_ProducesClosedFigureWithBaselineAndCurve()
    {
        var tip = new Point(100, 200);
        var geom = (PathGeometry)PinTipCapGeometry.BuildConcave(tip, new Vector(0, -1), halfWidth: 4.0, arcDepthPx: 3.0);

        Assert.Single(geom.Figures);
        var figure = geom.Figures[0];
        Assert.True(figure.IsClosed);
        Assert.Equal(new Point(96, 200), figure.StartPoint); // tipX - halfWidth
        Assert.Equal(2, figure.Segments.Count);              // line baseline + quadratic close
        Assert.IsType<LineSegment>(figure.Segments[0]);
        Assert.IsType<QuadraticBezierSegment>(figure.Segments[1]);
    }
}
