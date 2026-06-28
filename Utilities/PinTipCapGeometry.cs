using System;
using System.Windows;
using System.Windows.Media;

namespace InteractiveWorldMap.Utilities
{
    /// <summary>
    /// Builds open, screen-space centerlines for drawn-pin divot caps.
    /// </summary>
    public static class PinTipCapGeometry
    {
        private const double VerticalDirectionEpsilon = 0.001;

        public static Geometry BuildHorizontal(Point tip, double widthPx)
        {
            double halfWidth = Math.Max(widthPx, 0.0) / 2.0;
            var geometry = new LineGeometry(
                new Point(tip.X - halfWidth, tip.Y),
                new Point(tip.X + halfWidth, tip.Y));
            geometry.Freeze();
            return geometry;
        }

        /// <summary>
        /// Builds a horizontal quadratic curve whose true midpoint is the shaft tip.
        /// Endpoints sit on the head side, so the curve bows away from the head.
        /// </summary>
        public static Geometry BuildConcave(
            Point tip,
            Vector shaftDir,
            double widthPx,
            double arcDepthPx)
        {
            double halfWidth = Math.Max(widthPx, 0.0) / 2.0;
            double depth = Math.Max(arcDepthPx, 0.0);
            double headSide = shaftDir.Y > VerticalDirectionEpsilon ? 1.0 : -1.0;
            double endpointY = tip.Y + (headSide * depth);
            double controlY = tip.Y - (headSide * depth);

            var figure = new PathFigure
            {
                StartPoint = new Point(tip.X - halfWidth, endpointY),
                IsClosed = false
            };
            figure.Segments.Add(new QuadraticBezierSegment(
                new Point(tip.X, controlY),
                new Point(tip.X + halfWidth, endpointY),
                isStroked: true));

            var geometry = new PathGeometry();
            geometry.Figures.Add(figure);
            geometry.Freeze();
            return geometry;
        }
    }
}
