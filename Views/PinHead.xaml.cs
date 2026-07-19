using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using InteractiveWorldMap.Models;

namespace InteractiveWorldMap.Views
{
    public partial class PinHead : UserControl
    {
        public static readonly DependencyProperty PinColorProperty =
            DependencyProperty.Register(
                nameof(PinColor),
                typeof(Color),
                typeof(PinHead),
                new PropertyMetadata(Colors.Red, OnPinColorChanged));

        public Color PinColor
        {
            get => (Color)GetValue(PinColorProperty);
            set => SetValue(PinColorProperty, value);
        }

        public PinHead()
            : this(new VisualConfig())
        {
        }

        public PinHead(VisualConfig visualConfig)
        {
            InitializeComponent();
            ApplyConfig(visualConfig.PinMarkers);
        }

        public void ApplyConfig(PinMarkerConfig config)
        {
            var ballSize = Math.Max(config.BallSize, 6.0);
            var ballOutline = Math.Max(config.BallOutlineThickness, 0.0);

            PinBall.Width = ballSize;
            PinBall.Height = ballSize;
            PinBall.StrokeThickness = ballOutline;

            if (TryParseColor(config.BallOutlineColor, out var outline))
                PinBall.Stroke = new SolidColorBrush(outline);

            Width = ballSize + (2 * ballOutline);
            Height = ballSize + (2 * ballOutline);
            PinBall.Effect = config.ShowShadow
                ? new DropShadowEffect
                {
                    Color = Colors.Black,
                    Direction = 315,
                    ShadowDepth = 1.5,
                    BlurRadius = 2.5,
                    Opacity = config.ShadowOpacity
                }
                : null;
            ApplyBallFill(PinColor);
        }

        public Point GetConnectionPoint() => new(Width / 2.0, Height / 2.0);

        private static void OnPinColorChanged(
            DependencyObject dependencyObject,
            DependencyPropertyChangedEventArgs args)
        {
            if (dependencyObject is PinHead head && args.NewValue is Color color)
                head.ApplyBallFill(color);
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
                    new(Colors.White, 0.0),
                    new(Lighten(color, 1.15), 0.35),
                    new(color, 1.0)
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
