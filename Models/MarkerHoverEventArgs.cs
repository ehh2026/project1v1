using System;

namespace InteractiveWorldMap.Models
{
    /// <summary>
    /// Event arguments for marker hover events.
    /// </summary>
    public class MarkerHoverEventArgs : EventArgs
    {
        /// <summary>
        /// The location marker that is being hovered over.
        /// Note: LocationMarker type will be defined in the Views layer.
        /// Using object type here to avoid circular dependency.
        /// </summary>
        public object Marker { get; set; }

        /// <summary>
        /// Indicates whether the mouse is entering (true) or leaving (false) the marker.
        /// </summary>
        public bool IsEntering { get; set; }

        /// <summary>
        /// Initializes a new instance of MarkerHoverEventArgs.
        /// </summary>
        public MarkerHoverEventArgs(object marker, bool isEntering)
        {
            Marker = marker;
            IsEntering = isEntering;
        }
    }
}
