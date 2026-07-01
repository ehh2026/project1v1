using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using InteractiveWorldMap.Models;

namespace InteractiveWorldMap.Views
{
    public partial class ManualLayoutPinMarker : UserControl
    {
        public ManualLayoutPinMarker()
            : this(new VisualConfig())
        {
        }

        public ManualLayoutPinMarker(VisualConfig visualConfig)
        {
            InitializeComponent();
            PinHead.PinColor = DrawnPinColorPalette.GetRandom();
            PinHead.ApplyConfig(visualConfig.PinMarkers);
            Width = PinHead.Width;
            Height = PinHead.Height;
            MouseEnter += (_, _) => AnimateHover(true);
            MouseLeave += (_, _) => AnimateHover(false);
        }

        public Color PinColor
        {
            get => PinHead.PinColor;
            set => PinHead.PinColor = value;
        }

        public void SetPinColor(Color color) => PinColor = color;

        public void ApplyConfig(PinMarkerConfig pinConfig)
        {
            PinHead.ApplyConfig(pinConfig);
            Width = PinHead.Width;
            Height = PinHead.Height;
        }

        public Point GetConnectionPoint() => new(Width / 2.0, Height / 2.0);

        public double GetHeadDiameter() => Math.Max(PinHead.Width, PinHead.Height);

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
    }
}
