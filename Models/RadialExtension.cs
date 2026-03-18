using System.Windows;

namespace InteractiveWorldMap.Models
{
    /// <summary>
    /// Represents a single radial extension line with marker positioning.
    /// </summary>
    public class RadialExtension
    {
        /// <summary>
        /// The location being extended.
        /// </summary>
        public Location Location { get; set; } = null!;

        /// <summary>
        /// Original position in screen coordinates (where marker would normally be).
        /// </summary>
        public Point OriginalPosition { get; set; }

        /// <summary>
        /// Extended position in screen coordinates (where marker will be placed).
        /// </summary>
        public Point ExtendedPosition { get; set; }

        /// <summary>
        /// Angle in degrees from center (0° = north, clockwise).
        /// </summary>
        public double Angle { get; set; }
    }
}
