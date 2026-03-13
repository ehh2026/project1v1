using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Animation;
using InteractiveWorldMap.Models;

namespace InteractiveWorldMap.Views
{
    /// <summary>
    /// Represents a cluster marker that displays multiple locations grouped together.
    /// </summary>
    public partial class ClusterMarker : UserControl
    {
        private Storyboard? _hoverInStoryboard;
        private Storyboard? _hoverOutStoryboard;
        private Storyboard? _clickStoryboard;

        /// <summary>
        /// Gets or sets the location cluster associated with this marker.
        /// </summary>
        public LocationCluster? Cluster { get; set; }

        /// <summary>
        /// Gets or sets the screen position of this marker.
        /// </summary>
        public Point ScreenPosition { get; set; }

        /// <summary>
        /// Gets the number of locations in the cluster.
        /// </summary>
        public int LocationCount => Cluster?.Count ?? 0;

        public ClusterMarker()
        {
            InitializeComponent();
            
            // Load storyboards
            _hoverInStoryboard = (Storyboard)Resources["HoverInStoryboard"];
            _hoverOutStoryboard = (Storyboard)Resources["HoverOutStoryboard"];
            _clickStoryboard = (Storyboard)Resources["ClickStoryboard"];
            
            // Wire up events
            MouseEnter += OnMouseEnter;
            MouseLeave += OnMouseLeave;
            
            Loaded += OnLoaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            UpdateDisplay();
        }

        /// <summary>
        /// Updates the visual display based on the cluster data.
        /// </summary>
        public void UpdateDisplay()
        {
            if (Cluster == null)
                return;

            // Update count text
            CountText.Text = LocationCount.ToString();
            
            // Update tooltip with location names
            var locationNames = string.Join("\n", Cluster.Locations.Select(l => l.Name));
            ToolTip = $"{LocationCount} locations:\n{locationNames}";
        }

        /// <summary>
        /// Animates the marker when clicked.
        /// </summary>
        public void AnimateClick()
        {
            _clickStoryboard?.Begin();
        }

        private void OnMouseEnter(object sender, MouseEventArgs e)
        {
            _hoverOutStoryboard?.Stop();
            _hoverInStoryboard?.Begin();
        }

        private void OnMouseLeave(object sender, MouseEventArgs e)
        {
            _hoverInStoryboard?.Stop();
            _hoverOutStoryboard?.Begin();
        }
    }
}
