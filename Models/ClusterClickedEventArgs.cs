using System;
using System.Windows;

namespace InteractiveWorldMap.Models
{
    /// <summary>
    /// Event arguments for cluster marker click events.
    /// </summary>
    public class ClusterClickedEventArgs : EventArgs
    {
        /// <summary>
        /// Gets the cluster that was clicked.
        /// </summary>
        public LocationCluster Cluster { get; }

        /// <summary>
        /// Gets the screen position where the click occurred.
        /// </summary>
        public Point ClickPosition { get; }

        public ClusterClickedEventArgs(LocationCluster cluster, Point clickPosition)
        {
            Cluster = cluster ?? throw new ArgumentNullException(nameof(cluster));
            ClickPosition = clickPosition;
        }
    }
}
