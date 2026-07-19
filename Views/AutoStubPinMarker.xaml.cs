using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using InteractiveWorldMap.Models;

namespace InteractiveWorldMap.Views
{
    public partial class AutoStubPinMarker : UserControl
    {
        public AutoStubPinMarker()
            : this(new VisualConfig())
        {
        }

        public AutoStubPinMarker(VisualConfig visualConfig)
        {
            InitializeComponent();
            PinHead.PinColor = DrawnPinColorPalette.GetRandom();
            ApplyConfig(visualConfig.PinMarkers);
            MouseEnter += (_, _) => AnimateHover(true);
            MouseLeave += (_, _) => AnimateHover(false);
        }

        public Color PinColor
        {
            get => PinHead.PinColor;
            set => PinHead.PinColor = value;
        }

        public void ApplyConfig(PinMarkerConfig pinConfig)
        {
            PinHead.ApplyConfig(pinConfig);

            var shaftWidth = Math.Max(pinConfig.ShaftWidth, 2.0);
            var shaftLength = Math.Max(pinConfig.ShaftLength, 12.0);
            var shaftOutline = Math.Max(pinConfig.ShaftOutlineThickness, 0.0);

            PinShaft.Width = shaftWidth;
            PinShaft.Height = shaftLength;
            PinShaftOutline.Width = shaftWidth + (2 * shaftOutline);
            PinShaftOutline.Height = shaftLength;
            ShaftHost.Margin = new Thickness(0, PinHead.GetConnectionPoint().Y, 0, 0);

            if (TryParseColor(pinConfig.ShaftColor, out var shaftColor))
                PinShaft.Fill = new SolidColorBrush(shaftColor);

            if (TryParseColor(pinConfig.ShaftOutlineColor, out var outlineColor))
                PinShaftOutline.Fill = new SolidColorBrush(outlineColor);

            Width = Math.Max(PinHead.Width, PinShaftOutline.Width);
            Height = PinHead.GetConnectionPoint().Y + shaftLength;
        }

        public void SetPinColor(Color color) => PinColor = color;

        public Point GetConnectionPoint() => new(Width / 2.0, PinHead.Height / 2.0);

        public double GetHeadDiameter() => Math.Max(PinHead.Width, PinHead.Height);

        public Point GetShaftTipPoint() => new(Width / 2.0, Height);

        public Point GetScaledShaftTipPoint() => ApplyPinTransform(GetShaftTipPoint());

        public Point GetScaledConnectionPoint() => ApplyPinTransform(GetConnectionPoint());

        public double GetScaledShaftOutlineWidth() => PinShaftOutline.Width * PinTransform.ScaleX;

        public void AnimateHover(bool isHovered)
        {
            var animation = new DoubleAnimation
            {
                To = isHovered ? 1.15 : 1.0,
                Duration = TimeSpan.FromMilliseconds(150),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            };
            PinTransform.BeginAnimation(ScaleTransform.ScaleXProperty, animation);
            PinTransform.BeginAnimation(ScaleTransform.ScaleYProperty, animation);
        }

        public void AnimateClick()
        {
            var storyboard = new Storyboard();
            var scaleX = CreateClickAnimation();
            var scaleY = CreateClickAnimation();
            Storyboard.SetTarget(scaleX, PinTransform);
            Storyboard.SetTargetProperty(scaleX, new PropertyPath(ScaleTransform.ScaleXProperty));
            Storyboard.SetTarget(scaleY, PinTransform);
            Storyboard.SetTargetProperty(scaleY, new PropertyPath(ScaleTransform.ScaleYProperty));
            storyboard.Children.Add(scaleX);
            storyboard.Children.Add(scaleY);
            storyboard.Begin();
        }

        private static DoubleAnimation CreateClickAnimation() => new()
        {
            To = 1.3,
            Duration = TimeSpan.FromMilliseconds(50),
            AutoReverse = true
        };

        private Point ApplyPinTransform(Point point)
        {
            var center = new Point(Width / 2.0, Height / 2.0);
            return new Point(
                center.X + ((point.X - center.X) * PinTransform.ScaleX),
                center.Y + ((point.Y - center.Y) * PinTransform.ScaleY));
        }

        private static bool TryParseColor(string? value, out Color color)
        {
            color = default;
            return !string.IsNullOrWhiteSpace(value) &&
                   ColorConverter.ConvertFromString(value) is Color parsed &&
                   (color = parsed).A > 0;
        }
    }
}
