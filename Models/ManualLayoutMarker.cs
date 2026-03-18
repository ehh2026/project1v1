using System;
using System.Windows;

namespace InteractiveWorldMap.Models
{
    /// <summary>
    /// Represents a manually positioned marker in a saved layout
    /// </summary>
    public class ManualLayoutMarker
    {
        public string LocationName { get; set; } = string.Empty;
        public Point OriginalPosition { get; set; }
        public Point ExtendedPosition { get; set; }
        public double Angle { get; set; }
        public double LineLength { get; set; }

        public ManualLayoutMarker() { }

        public ManualLayoutMarker(string locationName, Point originalPos, Point extendedPos, double angle, double length)
        {
            LocationName = locationName;
            OriginalPosition = originalPos;
            ExtendedPosition = extendedPos;
            Angle = angle;
            LineLength = length;
        }

        /// <summary>
        /// Create from a RadialExtension
        /// </summary>
        public static ManualLayoutMarker FromRadialExtension(RadialExtension extension)
        {
            return new ManualLayoutMarker(
                extension.Location.Name,
                extension.OriginalPosition,
                extension.ExtendedPosition,
                extension.Angle,
                CalculateLength(extension.OriginalPosition, extension.ExtendedPosition)
            );
        }

        private static double CalculateLength(Point p1, Point p2)
        {
            double dx = p2.X - p1.X;
            double dy = p2.Y - p1.Y;
            return Math.Sqrt(dx * dx + dy * dy);
        }
    }
}
