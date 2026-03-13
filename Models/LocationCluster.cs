using System.Collections.Generic;
using System.Windows;

namespace InteractiveWorldMap.Models
{
    /// <summary>
    /// Represents a cluster of locations that are close together.
    /// </summary>
    public class LocationCluster
    {
        /// <summary>
        /// Unique identifier for the cluster.
        /// </summary>
        public string Id { get; set; } = string.Empty;

        /// <summary>
        /// List of locations in this cluster.
        /// </summary>
        public List<Location> Locations { get; set; } = new List<Location>();

        /// <summary>
        /// Center point of the cluster in pixel coordinates.
        /// </summary>
        public Point CenterPoint { get; set; }

        /// <summary>
        /// Number of locations in the cluster.
        /// </summary>
        public int Count => Locations.Count;

        /// <summary>
        /// Whether this is a single-location cluster (should display as regular marker).
        /// </summary>
        public bool IsSingleLocation => Locations.Count == 1;
    }
}
