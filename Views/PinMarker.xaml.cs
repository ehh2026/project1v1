using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using InteractiveWorldMap.Models;

namespace InteractiveWorldMap.Views
{
    /// <summary>
    /// Visual representation of a location as a sewing pin with colored ball and metal shaft.
    /// Dimensions and outline colors come from <see cref="PinMarkerConfig"/>.
    /// </summary>
    public partial class PinMarker : UserControl
    {
        private bool _isHovered;
        private static readonly Random _colorRandom = new Random();

        // Saturated hues only — avoids white/gray/black that disappear on the map.
        private static readonly Color[] PinColors = {
            Color.FromRgb(229, 57, 53),   // red
            Color.FromRgb(25, 118, 210),  // blue
            Color.FromRgb(46, 125, 50),   // green
            Color.FromRgb(245, 124, 0),   // orange
            Color.FromRgb(123, 31, 162),  // purple
            Color.FromRgb(194, 24, 91),   // pink
            Color.FromRgb(0, 151, 167),   // cyan
            Color.FromRgb(251, 192, 45),  // amber
            Color.FromRgb(109, 76, 65),   // brown
            Color.FromRgb(0, 105, 92),    // teal
        };

        public static readonly DependencyProperty PinColorProperty =
            DependencyProperty.Register("PinColor", typeof(Color), typeof(PinMarker),
                new PropertyMetadata(Colors.Red, OnPinColorChanged));

        public Color PinColor
        {
            get => (Color)GetValue(PinColorProperty);
            set => SetValue(PinColorProperty, value);
        }

        public Location Location { get; set; } = null!;

        public Point ScreenPosition { get; set; }

        public bool IsHovered
        {
            get => _isHovered;
            set
            {
                if (_isHovered != value)
                {
                    _isHovered = value;
                    AnimateHover(value);
                }
            }
        }

        public PinMarker()
            : this(new VisualConfig())
        {
        }

        public PinMarker(VisualConfig visualConfig)
        {
            if (visualConfig == null) throw new ArgumentNullException(nameof(visualConfig));

            InitializeComponent();

            PinColor = PinColors[_colorRandom.Next(PinColors.Length)];
            ApplyPinDimensions(visualConfig.PinMarkers);

            MouseEnter += (s, e) => IsHovered = true;
            MouseLeave += (s, e) => IsHovered = false;
        }

        internal void ApplyPinDimensions(PinMarkerConfig pinConfig)
        {
            double ballSize = Math.Max(pinConfig.BallSize, 6.0);
            double shaftWidth = Math.Max(pinConfig.ShaftWidth, 2.0);
            double shaftLength = Math.Max(pinConfig.ShaftLength, 12.0);
            double shaftOutline = Math.Max(pinConfig.ShaftOutlineThickness, 0.0);
            double ballOutline = Math.Max(pinConfig.BallOutlineThickness, 0.0);

            PinBall.Width = ballSize;
            PinBall.Height = ballSize;
            PinShaft.Width = shaftWidth;
            PinShaft.Height = shaftLength;
            PinShaftOutline.Width = shaftWidth + (2 * shaftOutline);
            PinShaftOutline.Height = shaftLength;

            var shaftTop = ballSize / 2.0;
            ShaftHost.Margin = new Thickness(0, shaftTop, 0, 0);

            if (TryParseColor(pinConfig.ShaftColor, out var shaftColor))
                PinShaft.Fill = new SolidColorBrush(shaftColor);

            if (TryParseColor(pinConfig.ShaftOutlineColor, out var shaftOutlineColor))
                PinShaftOutline.Fill = new SolidColorBrush(shaftOutlineColor);

            if (TryParseColor(pinConfig.BallOutlineColor, out var ballOutlineColor))
                PinBall.Stroke = new SolidColorBrush(ballOutlineColor);

            PinBall.StrokeThickness = ballOutline;

            ApplyBallFill(PinColor);

            Width = Math.Max(ballSize + (2 * ballOutline), PinShaftOutline.Width);
            Height = shaftTop + shaftLength;
        }

        public void SetPinColor(Color color)
        {
            PinColor = color;
        }

        /// <summary>
        /// Shows or hides the pin's own shaft. Extended markers hide it because the
        /// radial extension line acts as the shaft connecting the head to the map
        /// location — keeping the pin's vertical shaft would draw a second, off-axis
        /// shaft on top of the head.
        /// </summary>
        public void SetShaftVisible(bool visible)
        {
            ShaftHost.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
        }

        public static Color GetRandomPinColor()
        {
            return PinColors[_colorRandom.Next(PinColors.Length)];
        }

        public void AnimateHover(bool isHovered)
        {
            var scaleAnimation = new DoubleAnimation
            {
                To = isHovered ? 1.15 : 1.0,
                Duration = TimeSpan.FromMilliseconds(150),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            };

            PinTransform.BeginAnimation(ScaleTransform.ScaleXProperty, scaleAnimation);
            PinTransform.BeginAnimation(ScaleTransform.ScaleYProperty, scaleAnimation);
        }

        public void AnimateClick()
        {
            var storyboard = new Storyboard();

            var scaleUpX = new DoubleAnimation
            {
                To = 1.3,
                Duration = TimeSpan.FromMilliseconds(50),
                AutoReverse = true
            };

            var scaleUpY = new DoubleAnimation
            {
                To = 1.3,
                Duration = TimeSpan.FromMilliseconds(50),
                AutoReverse = true
            };

            Storyboard.SetTarget(scaleUpX, PinTransform);
            Storyboard.SetTargetProperty(scaleUpX, new PropertyPath(ScaleTransform.ScaleXProperty));

            Storyboard.SetTarget(scaleUpY, PinTransform);
            Storyboard.SetTargetProperty(scaleUpY, new PropertyPath(ScaleTransform.ScaleYProperty));

            storyboard.Children.Add(scaleUpX);
            storyboard.Children.Add(scaleUpY);
            storyboard.Begin();
        }

        public Point GetConnectionPoint()
        {
            return new Point(Width / 2, PinBall.Height / 2);
        }

        public Point GetShaftTipPoint()
        {
            return new Point(Width / 2, Height);
        }

        /// <summary>True when the pin's own (built-in) shaft is currently shown.</summary>
        public bool IsShaftVisible => ShaftHost.Visibility == Visibility.Visible;

        /// <summary>
        /// Shaft tip in local coordinates after the hover <see cref="PinTransform"/> (scale about
        /// the control center) is applied, so a tip cap drawn on the marker canvas tracks the
        /// pin as it scales on hover.
        /// </summary>
        public Point GetScaledShaftTipPoint() => ApplyPinTransform(GetShaftTipPoint());

        /// <summary>Head connection point in local coordinates after the hover transform.</summary>
        public Point GetScaledConnectionPoint() => ApplyPinTransform(GetConnectionPoint());

        /// <summary>Outline-inclusive shaft width at the tip, scaled by the current hover transform.</summary>
        public double GetScaledShaftOutlineWidth() => PinShaftOutline.Width * PinTransform.ScaleX;

        private Point ApplyPinTransform(Point p)
        {
            // PinContainer.RenderTransformOrigin is 0.5,0.5 → scale about the control center.
            var center = new Point(Width / 2.0, Height / 2.0);
            return new Point(
                center.X + (p.X - center.X) * PinTransform.ScaleX,
                center.Y + (p.Y - center.Y) * PinTransform.ScaleY);
        }

        public bool ContainsPoint(Point point)
        {
            var ballCenter = new Point(Width / 2, PinBall.Height / 2);
            var ballRadius = PinBall.Width / 2;
            var distance = Math.Sqrt(Math.Pow(point.X - ballCenter.X, 2) + Math.Pow(point.Y - ballCenter.Y, 2));
            return distance <= ballRadius;
        }

        private static void OnPinColorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is PinMarker pin && e.NewValue is Color color)
                pin.ApplyBallFill(color);
        }

        private void ApplyBallFill(Color color)
        {
            PinBall.Fill = new RadialGradientBrush
            {
                GradientOrigin = new Point(0.35, 0.35),
                Center = new Point(0.35, 0.35),
                RadiusX = 0.85,
                RadiusY = 0.85,
                GradientStops = new GradientStopCollection
                {
                    new GradientStop(Colors.White, 0.0),
                    new GradientStop(Lighten(color, 1.15), 0.35),
                    new GradientStop(color, 1.0)
                }
            };
        }

        private static bool TryParseColor(string? value, out Color color)
        {
            color = default;
            return !string.IsNullOrWhiteSpace(value) &&
                   ColorConverter.ConvertFromString(value) is Color parsed &&
                   (color = parsed).A > 0;
        }

        private static Color Lighten(Color color, double factor)
        {
            factor = Math.Max(factor, 1.0);
            return Color.FromRgb(
                (byte)Math.Min(255, color.R * factor),
                (byte)Math.Min(255, color.G * factor),
                (byte)Math.Min(255, color.B * factor));
        }
    }
}
