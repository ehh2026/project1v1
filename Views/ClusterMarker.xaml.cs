using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
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
            : this(new VisualConfig())
        {
        }

        public ClusterMarker(IMarkerConfiguration markerConfiguration)
        {
            if (markerConfiguration == null) throw new ArgumentNullException(nameof(markerConfiguration));

            InitializeComponent();

            double markerSize = markerConfiguration.ClusterMarkerSize;
            double badgeSize = markerConfiguration.ClusterBadgeSize;
            double fontSize = markerConfiguration.ClusterCountFontSize;

            Width = markerSize;
            Height = markerSize;
            StampImage.Width = markerSize;
            StampImage.Height = markerSize;
            BadgeEllipse.Width = badgeSize;
            BadgeEllipse.Height = badgeSize;
            CountText.FontSize = fontSize;
            ApplyShadowConfig(markerConfiguration.ClusterMarkerShadow);

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
            MarkerBodyHost.Children.Clear();
            if (stampImage != null)
            {
                StampImage.Source = stampImage;
                MarkerBodyHost.Children.Add(StampImage);
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

            MarkerBodyHost.Children.Add(ellipse);
        }

        public void ApplyShadowConfig(ClusterMarkerShadowConfig config)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));

            MarkerBodyHost.Effect = config.Enabled
                ? CreateShadow(depth: 2, blur: 4, config.Opacity)
                : null;
            BadgeEllipse.Effect = config.Enabled
                ? CreateShadow(depth: 1, blur: 2, config.Opacity)
                : null;
        }

        private static DropShadowEffect CreateShadow(
            double depth, double blur, double opacity) => new()
            {
                Color = Colors.Black,
                Direction = 270,
                ShadowDepth = depth,
                BlurRadius = blur,
                Opacity = opacity
            };

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

        public void AnimateHover(bool isHovered)
        {
            if (isHovered)
            {
                _hoverOutStoryboard?.Stop();
                _hoverInStoryboard?.Begin();
            }
            else
            {
                _hoverInStoryboard?.Stop();
                _hoverOutStoryboard?.Begin();
            }
        }

        private void OnMouseEnter(object sender, MouseEventArgs e)
        {
            AnimateHover(true);
        }

        private void OnMouseLeave(object sender, MouseEventArgs e)
        {
            AnimateHover(false);
        }
    }
}
