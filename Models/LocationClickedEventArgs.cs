using System;
using System.Windows;

namespace InteractiveWorldMap.Models
{
    /// <summary>
    /// Event arguments for location marker click events.
    /// </summary>
    public class LocationClickedEventArgs : EventArgs
    {
        /// <summary>
        /// The location that was clicked.
        /// </summary>
        public Location Location { get; set; }

        /// <summary>
        /// The screen position where the click occurred.
        /// </summary>
        public Point ClickPosition { get; set; }

        /// <summary>
        /// Initializes a new instance of LocationClickedEventArgs.
        /// </summary>
        public LocationClickedEventArgs(Location location, Point clickPosition)
        {
            Location = location;
            ClickPosition = clickPosition;
        }
    }
}
