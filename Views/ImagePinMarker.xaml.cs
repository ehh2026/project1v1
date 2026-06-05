using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using InteractiveWorldMap.Models;

namespace InteractiveWorldMap.Views
{
    /// <summary>
    /// Visual representation of a location using an image-based pin from the master pin file.
    /// </summary>
    public partial class ImagePinMarker : UserControl
    {
        private bool _isHovered;
        private static readonly Random _random = new Random();

        /// <summary>
        /// Gets or sets the location associated with this pin marker.
        /// </summary>
        public Location Location { get; set; } = null!;

        /// <summary>
        /// Gets or sets the screen position of this pin marker.
        /// </summary>
        public Point ScreenPosition { get; set; }

        /// <summary>
        /// Gets or sets the pin image info used for this marker.
        /// </summary>
        public PinImageInfo PinInfo { get; set; } = null!;

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

        public ImagePinMarker()
        {
            InitializeComponent();
            
            // Wire up mouse events
            MouseEnter += (s, e) => IsHovered = true;
            MouseLeave += (s, e) => IsHovered = false;
        }

        /// <summary>
        /// Sets the pin image from a cropped bitmap source.
        /// </summary>
        public void SetPinImage(BitmapSource pinImageSource, PinImageInfo pinInfo, double scaleFactor)
        {
            PinInfo = pinInfo;
            PinImage.Source = pinImageSource;
            
            // Set size based on the original pin dimensions and scale factor
            Width = pinInfo.Width * scaleFactor;
            Height = pinInfo.Height * scaleFactor;
            
            Console.WriteLine($"ImagePinMarker: Set pin '{pinInfo.Id}' with size {Width:F1}x{Height:F1} (scale: {scaleFactor:F2})");
        }

        /// <summary>
        /// Creates a cropped bitmap from the master image for this specific pin.
        /// </summary>
        public static BitmapSource CropPinFromMaster(BitmapSource masterImage, PinImageInfo pinInfo)
        {
            try
            {
                // Create cropped bitmap for this pin
                var croppedBitmap = new CroppedBitmap(masterImage, new Int32Rect(
                    pinInfo.X, pinInfo.Y, pinInfo.Width, pinInfo.Height));
                
                // Freeze for performance
                croppedBitmap.Freeze();
                
                return croppedBitmap;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error cropping pin '{pinInfo.Id}': {ex.Message}");
                return masterImage; // Fallback to full image
            }
        }

        /// <summary>
        /// Selects a random pin from the available pin configurations.
        /// </summary>
        public static PinImageInfo SelectRandomPin(PinImageConfig config)
        {
            if (config.Pins.Count == 0)
            {
                // Return a default pin info if none configured
                return new PinImageInfo
                {
                    Id = "default",
                    X = 0,
                    Y = 0,
                    Width = 100,
                    Height = 200,
                    Description = "Default pin"
                };
            }

            return config.Pins[_random.Next(config.Pins.Count)];
        }

        /// <summary>
        /// Animates the pin marker on hover state change.
        /// </summary>
        public void AnimateHover(bool isHovered)
        {
            var scaleAnimation = new DoubleAnimation
            {
                To = isHovered ? 1.1 : 1.0,
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
            var storyboard = new Storyboard();

            var scaleUpX = new DoubleAnimation
            {
                To = 1.2,
                Duration = TimeSpan.FromMilliseconds(50),
                AutoReverse = true
            };

            var scaleUpY = new DoubleAnimation
            {
                To = 1.2,
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
        /// Gets the connection point for extension lines (center bottom of pin).
        /// </summary>
        public Point GetConnectionPoint()
        {
            // For pins, the connection point is typically at the bottom center (where the pin enters the map)
            return new Point(Width / 2, Height * 0.9);
        }

        /// <summary>
        /// Checks if a point is within the pin marker bounds.
        /// </summary>
        public bool ContainsPoint(Point point)
        {
            return point.X >= 0 && point.X <= Width && point.Y >= 0 && point.Y <= Height;
        }
    }
}