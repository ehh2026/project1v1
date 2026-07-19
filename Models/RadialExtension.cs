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
        /// Source-image X coordinate of the extended position (optional).
        /// Set at save time from <see cref="ExtendedPosition"/> via <c>viewport.ScreenToSource</c>
        /// so the saved layout re-projects to the correct map position at any window size.
        /// Flows through to <see cref="ManualLayoutMarker.SourceExtendedX"/>.
        /// </summary>
        public double? SourceExtendedX { get; set; }

        /// <summary>
        /// Source-image Y coordinate of the extended position (optional). See <see cref="SourceExtendedX"/>.
        /// </summary>
        public double? SourceExtendedY { get; set; }

        /// <summary>
        /// Angle in degrees from center (0° = north, clockwise).
        /// </summary>
        public double Angle { get; set; }

        /// <summary>
        /// Group identifier to track which dense group this extension belongs to.
        /// Used for angle checking within groups.
        /// </summary>
        public int GroupId { get; set; }
    }
}
