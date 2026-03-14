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
            
            // Load stamp image from file
            try
            {
                var basePath = AppDomain.CurrentDomain.BaseDirectory;
                var imagePath = System.IO.Path.Combine(basePath, "Images&Content", "stamp_demo.png");
                
                if (System.IO.File.Exists(imagePath))
                {
                    var bitmap = new System.Windows.Media.Imaging.BitmapImage();
                    bitmap.BeginInit();
                    bitmap.UriSource = new Uri(imagePath, UriKind.Absolute);
                    bitmap.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                    bitmap.EndInit();
                    StampImage.Source = bitmap;
                }
                else
                {
                    // Fallback: create a blue circle if stamp image not found
                    var ellipse = new System.Windows.Shapes.Ellipse
                    {
                        Width = 40,
                        Height = 40,
                        Fill = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(33, 150, 243)),
                        Stroke = System.Windows.Media.Brushes.White,
                        StrokeThickness = 3
                    };
                    
                    // Replace the image with the ellipse
                    var grid = (Grid)Content;
                    grid.Children.Remove(StampImage);
                    grid.Children.Insert(0, ellipse);
                }
            }
            catch
            {
                // If anything fails, the image will just be empty
            }
            
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
