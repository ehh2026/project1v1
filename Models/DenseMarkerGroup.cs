using System.Collections.Generic;
using System.Windows;

namespace InteractiveWorldMap.Models
{
    /// <summary>
    /// Represents a group of markers that are densely packed in screen space.
    /// </summary>
    public class DenseMarkerGroup
    {
        /// <summary>
        /// Locations in this dense group.
        /// </summary>
        public List<Location> Locations { get; set; } = new List<Location>();

        /// <summary>
        /// Geometric center point of the group in screen coordinates.
        /// </summary>
        public Point CenterPoint { get; set; }

        /// <summary>
        /// Calculated radial extensions for each location.
        /// </summary>
        public List<RadialExtension> Extensions { get; set; } = new List<RadialExtension>();

        /// <summary>
        /// Number of locations in this group.
        /// </summary>
        public int Count => Locations.Count;
    }
}
