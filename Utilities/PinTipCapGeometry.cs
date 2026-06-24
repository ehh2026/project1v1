using System;
using System.Windows;
using System.Windows.Media;

namespace InteractiveWorldMap.Utilities
{
    /// <summary>
    /// Pure geometry builders for drawn-pin tip caps. All math is in screen space; the
    /// caller supplies the tip point, a unit shaft direction (tip → head), and widths.
    /// Isolated here so the concave curve can be re-tuned (depth, control point, even
    /// quadratic → cubic) without touching the renderer.
    /// </summary>
    public static class PinTipCapGeometry
    {
        /// <summary>
        /// Half-width of a cap: half the visible shaft's outline-inclusive width, plus the
        /// configured extra extension on each side.
        /// </summary>
        public static double HalfWidth(double outlineWidthPx, double extendPx)
        {
            return (Math.Max(outlineWidthPx, 0.0) / 2.0) + Math.Max(extendPx, 0.0);
        }

        /// <summary>
        /// Horizontal bar through the tip, extending downward (+Y) by <paramref name="heightPx"/>.
        /// Drawn on top of the shaft terminus (no geometric clip).
        /// </summary>
        public static Geometry BuildHorizontal(Point tip, double halfWidth, double heightPx)
        {
            var rect = new Rect(tip.X - halfWidth, tip.Y, halfWidth * 2.0, Math.Max(heightPx, 0.0));
            var geometry = new RectangleGeometry(rect);
            geometry.Freeze();
            return geometry;
        }

        /// <summary>
        /// Control point of the concave arc: offset from the tip <em>toward the shaft/head</em>
        /// along <paramref name="shaftDir"/> by <paramref name="arcDepthPx"/>. Positive depth
        /// bows the arc up around the entry point (the "stuck-in" read).
        /// </summary>
        public static Point ConcaveControlPoint(Point tip, Vector shaftDir, double arcDepthPx)
        {
            var dir = Normalize(shaftDir);
            return new Point(tip.X + dir.X * arcDepthPx, tip.Y + dir.Y * arcDepthPx);
        }

        /// <summary>
        /// On-curve midpoint (t = 0.5) of the concave arc, for the direction invariant test.
        /// For a symmetric horizontal baseline this reduces to <c>tip + shaftDir * (arcDepth / 2)</c>.
        /// </summary>
        public static Point ConcaveMidpoint(Point tip, Vector shaftDir, double arcDepthPx)
        {
            var dir = Normalize(shaftDir);
            return new Point(tip.X + dir.X * (arcDepthPx / 2.0), tip.Y + dir.Y * (arcDepthPx / 2.0));
        }

        /// <summary>
        /// Closed concave figure: a horizontal baseline (left → right through the tip) closed by a
        /// quadratic Bezier back to the start, whose control point bows toward the shaft.
        /// </summary>
        public static Geometry BuildConcave(Point tip, Vector shaftDir, double halfWidth, double arcDepthPx)
        {
            var start = new Point(tip.X - halfWidth, tip.Y);
            var end = new Point(tip.X + halfWidth, tip.Y);
            var control = ConcaveControlPoint(tip, shaftDir, arcDepthPx);

            var figure = new PathFigure { StartPoint = start, IsClosed = true };
            figure.Segments.Add(new LineSegment(end, isStroked: true));
            figure.Segments.Add(new QuadraticBezierSegment(control, start, isStroked: true));

            var geometry = new PathGeometry { FillRule = FillRule.Nonzero };
            geometry.Figures.Add(figure);
            geometry.Freeze();
            return geometry;
        }

        private static Vector Normalize(Vector v)
        {
            double len = v.Length;
            if (len <= double.Epsilon)
                return new Vector(0, -1); // default: straight up (vertical stub)
            return new Vector(v.X / len, v.Y / len);
        }
    }
}
