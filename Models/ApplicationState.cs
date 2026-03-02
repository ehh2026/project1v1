using System.Collections.Generic;
using System.Windows.Media;

namespace InteractiveWorldMap.Models
{
    /// <summary>
    /// Manages the application state including loaded content and active location.
    /// </summary>
    public class ApplicationState
    {
        /// <summary>
        /// The loaded world map image.
        /// </summary>
        public ImageSource? MapImage { get; set; }

        /// <summary>
        /// List of all locations loaded from the configuration.
        /// </summary>
        public List<Location> Locations { get; set; } = new List<Location>();

        /// <summary>
        /// The currently active location (if a subwindow is open).
        /// </summary>
        public Location? ActiveLocation { get; set; }

        /// <summary>
        /// Indicates whether a content subwindow is currently open.
        /// </summary>
        public bool IsSubwindowOpen { get; set; }

        /// <summary>
        /// Cache for loaded content to avoid repeated disk I/O.
        /// Key: content file path, Value: loaded content object.
        /// </summary>
        public Dictionary<string, object> ContentCache { get; set; } = new Dictionary<string, object>();
    }
}
