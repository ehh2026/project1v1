using System.Windows;

namespace InteractiveWorldMap.Models
{
    /// <summary>
    /// Represents the zoom state of the map for navigation purposes.
    /// </summary>
    public class ZoomState
    {
        /// <summary>
        /// Gets or sets the center point of the zoom (cluster center in pixel coordinates).
        /// </summary>
        public Point ZoomCenter { get; set; }

        /// <summary>
        /// Gets or sets the active cluster being viewed (null if viewing full map).
        /// </summary>
        public LocationCluster? ActiveCluster { get; set; }

        /// <summary>
        /// Gets or sets the X scale value.
        /// </summary>
        public double ScaleX { get; set; }

        /// <summary>
        /// Gets or sets the Y scale value.
        /// </summary>
        public double ScaleY { get; set; }

        /// <summary>
        /// Gets or sets the X translation value.
        /// </summary>
        public double TranslateX { get; set; }

        /// <summary>
        /// Gets or sets the Y translation value.
        /// </summary>
        public double TranslateY { get; set; }

        /// <summary>
        /// Creates a ZoomState representing the full map view (no zoom).
        /// </summary>
        public static ZoomState CreateFullMapView()
        {
            return new ZoomState
            {
                ScaleX = 1.0,
                ScaleY = 1.0,
                TranslateX = 0.0,
                TranslateY = 0.0,
                ActiveCluster = null
            };
        }
    }
}
