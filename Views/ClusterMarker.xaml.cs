using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using InteractiveWorldMap.Models;

namespace InteractiveWorldMap.Views
{
    /// <summary>
    /// Represents a cluster marker that displays multiple locations grouped together.
    /// Stamp image is supplied by MainWindow via ContentLoader (Views must not build content paths).
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

            double markerSize = 40;
            double badgeSize = 20;
            double fontSize = 12;

            if (Application.Current.MainWindow is MainWindow mainWindow)
            {
                markerSize = mainWindow.ClusterMarkerSize;
                badgeSize = mainWindow.ClusterBadgeSize;
                fontSize = mainWindow.ClusterCountFontSize;
            }

            Width = markerSize;
            Height = markerSize;
            StampImage.Width = markerSize;
            StampImage.Height = markerSize;
            BadgeEllipse.Width = badgeSize;
            BadgeEllipse.Height = badgeSize;
            CountText.FontSize = fontSize;

            SizeChanged += (s, e) =>
            {
                ScaleTransform.CenterX = ActualWidth / 2;
                ScaleTransform.CenterY = ActualHeight / 2;
            };

            _hoverInStoryboard = (Storyboard)Resources["HoverInStoryboard"];
            _hoverOutStoryboard = (Storyboard)Resources["HoverOutStoryboard"];
            _clickStoryboard = (Storyboard)Resources["ClickStoryboard"];

            MouseEnter += OnMouseEnter;
            MouseLeave += OnMouseLeave;
            Loaded += OnLoaded;
        }

        /// <summary>
        /// Applies the cluster stamp image loaded by MainWindow/ContentLoader, or a fallback shape.
        /// </summary>
        public void ApplyStampImage(ImageSource? stampImage)
        {
            if (stampImage != null)
            {
                StampImage.Source = stampImage;
                return;
            }

            var markerSize = Width > 0 ? Width : 40;
            var ellipse = new System.Windows.Shapes.Ellipse
            {
                Width = markerSize,
                Height = markerSize,
                Fill = new SolidColorBrush(Color.FromRgb(33, 150, 243)),
                Stroke = Brushes.White,
                StrokeThickness = 3
            };

            if (Content is Grid grid)
            {
                grid.Children.Remove(StampImage);
                grid.Children.Insert(0, ellipse);
            }
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

            CountText.Text = LocationCount.ToString();

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
