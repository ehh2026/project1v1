using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using InteractiveWorldMap.Models;

namespace InteractiveWorldMap.Views
{
    /// <summary>
    /// Visual representation of a location as a sewing pin with colored ball.
    /// </summary>
    public partial class PinMarker : UserControl
    {
        private bool _isHovered;
        private static readonly Random _colorRandom = new Random();
        
        // Predefined pin colors similar to sewing pins
        private static readonly Color[] PinColors = {
            Colors.Red, Colors.Blue, Colors.Green, Colors.Yellow, Colors.Orange,
            Colors.Purple, Colors.Pink, Colors.Cyan, Colors.Magenta, Colors.Lime,
            Colors.Brown, Colors.Gray, Colors.Black, Colors.White, Colors.Maroon,
            Colors.Navy, Colors.Olive, Colors.Teal, Colors.Silver, Colors.Gold
        };

        /// <summary>
        /// Dependency property for pin color
        /// </summary>
        public static readonly DependencyProperty PinColorProperty =
            DependencyProperty.Register("PinColor", typeof(Color), typeof(PinMarker), 
                new PropertyMetadata(Colors.Red));

        /// <summary>
        /// Gets or sets the color of the pin ball.
        /// </summary>
        public Color PinColor
        {
            get => (Color)GetValue(PinColorProperty);
            set => SetValue(PinColorProperty, value);
        }

        /// <summary>
        /// Gets or sets the location associated with this pin marker.
        /// </summary>
        public Location Location { get; set; } = null!;

        /// <summary>
        /// Gets or sets the screen position of this pin marker.
        /// </summary>
        public Point ScreenPosition { get; set; }

        /// <summary>
        /// Gets or sets whether the pin marker is currently hovered.
        /// </summary>
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
        {
            InitializeComponent();
            
            // Set random pin color
            PinColor = PinColors[_colorRandom.Next(PinColors.Length)];
            
            // Set size from config
            var mainWindow = Application.Current.MainWindow as MainWindow;
            if (mainWindow != null)
            {
                // Pin markers are taller than they are wide due to the shaft
                Width = mainWindow.LocationMarkerSize;
                Height = mainWindow.LocationMarkerSize * 2; // Taller for the shaft
                
                // Scale the pin ball and shaft based on marker size
                double scale = mainWindow.LocationMarkerSize / 16.0; // 16 is default size
                PinBall.Width = 8 * scale;
                PinBall.Height = 8 * scale;
                PinShaft.Width = 1.5 * scale;
                PinShaft.Height = 20 * scale;
                PinShaft.Margin = new Thickness(0, 8 * scale, 0, 0);
            }
            else
            {
                // Fallback to default
                Width = 16;
                Height = 32;
            }
            
            // Wire up mouse events
            MouseEnter += (s, e) => IsHovered = true;
            MouseLeave += (s, e) => IsHovered = false;
        }

        /// <summary>
        /// Sets a specific color for this pin (useful for manual assignment).
        /// </summary>
        public void SetPinColor(Color color)
        {
            PinColor = color;
        }

        /// <summary>
        /// Gets a random pin color from the predefined palette.
        /// </summary>
        public static Color GetRandomPinColor()
        {
            return PinColors[_colorRandom.Next(PinColors.Length)];
        }

        /// <summary>
        /// Animates the pin marker on hover state change.
        /// </summary>
        public void AnimateHover(bool isHovered)
        {
            var scaleAnimation = new DoubleAnimation
            {
                To = isHovered ? 1.2 : 1.0,
                Duration = TimeSpan.FromMilliseconds(150),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            };

            PinTransform.BeginAnimation(ScaleTransform.ScaleXProperty, scaleAnimation);
            PinTransform.BeginAnimation(ScaleTransform.ScaleYProperty, scaleAnimation);
        }

        /// <summary>
        /// Animates the pin marker on click.
        /// </summary>
        public void AnimateClick()
        {
            var duration = TimeSpan.FromMilliseconds(100);

            // Pulse effect: scale up briefly then back to normal
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

        /// <summary>
        /// Gets the connection point for extension lines (center of pin ball).
        /// </summary>
        public Point GetConnectionPoint()
        {
            return new Point(Width / 2, PinBall.Height / 2);
        }

        /// <summary>
        /// Checks if a point is within the pin marker bounds.
        /// </summary>
        public bool ContainsPoint(Point point)
        {
            // Check if point is within the pin ball (circular hit test)
            var ballCenter = new Point(Width / 2, PinBall.Height / 2);
            var ballRadius = PinBall.Width / 2;
            var distance = Math.Sqrt(Math.Pow(point.X - ballCenter.X, 2) + Math.Pow(point.Y - ballCenter.Y, 2));
            return distance <= ballRadius;
        }
    }
}