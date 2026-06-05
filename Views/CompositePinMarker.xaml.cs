using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using InteractiveWorldMap.Models;

namespace InteractiveWorldMap.Views
{
    /// <summary>
    /// Visual representation of a pin composed from a shaft and head with segmented shaft rendering.
    /// </summary>
    public partial class CompositePinMarker : UserControl
    {
        private bool _isHovered;

        public Location Location { get; set; } = null!;

        public CompositePinRenderPlan? RenderPlan { get; private set; }

        public bool IsHovered
        {
            get => _isHovered;
            set
            {
                if (_isHovered == value)
                    return;

                _isHovered = value;
                AnimateHover(value);
            }
        }

        public CompositePinMarker()
        {
            InitializeComponent();
            MouseEnter += (_, _) => IsHovered = true;
            MouseLeave += (_, _) => IsHovered = false;
        }

        public void SetCompositeImages(
            BitmapSource shaftImageSource,
            BitmapSource headImageSource,
            CompositePinRenderPlan plan,
            bool showDebugOverlay = false)
        {
            if (shaftImageSource == null)
                throw new ArgumentNullException(nameof(shaftImageSource));
            if (headImageSource == null)
                throw new ArgumentNullException(nameof(headImageSource));
            if (plan == null)
                throw new ArgumentNullException(nameof(plan));

            RenderPlan = plan;
            Width = plan.Width;
            Height = plan.Height;
            RootCanvas.Width = plan.Width;
            RootCanvas.Height = plan.Height;

            ApplyLayer(ShaftTipCapImage, shaftImageSource, plan.ShaftTipCapLayer);
            ApplyLayer(ShaftBodyImage, shaftImageSource, plan.ShaftBodyLayer);
            ApplyLayer(ShaftHeadCapImage, shaftImageSource, plan.ShaftHeadCapLayer);
            ApplyLayer(HeadImage, headImageSource, plan.HeadLayer);
            ApplyDebugOverlay(plan, showDebugOverlay);
        }

        public Point GetTipAnchorPoint()
        {
            return RenderPlan?.TipAnchorLocal ?? new Point(Width / 2, Height / 2);
        }

        public Point GetHeadAttachPoint()
        {
            return RenderPlan?.HeadAttachLocal ?? new Point(Width / 2, Height / 2);
        }

        public void AnimateHover(bool isHovered)
        {
            var scaleAnimation = new DoubleAnimation
            {
                To = isHovered ? 1.08 : 1.0,
                Duration = TimeSpan.FromMilliseconds(150),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            };

            MarkerTransform.BeginAnimation(ScaleTransform.ScaleXProperty, scaleAnimation);
            MarkerTransform.BeginAnimation(ScaleTransform.ScaleYProperty, scaleAnimation);
        }

        public void AnimateClick()
        {
            var storyboard = new Storyboard();

            var scaleX = new DoubleAnimation
            {
                To = 1.14,
                Duration = TimeSpan.FromMilliseconds(60),
                AutoReverse = true
            };
            Storyboard.SetTarget(scaleX, MarkerTransform);
            Storyboard.SetTargetProperty(scaleX, new PropertyPath(ScaleTransform.ScaleXProperty));

            var scaleY = new DoubleAnimation
            {
                To = 1.14,
                Duration = TimeSpan.FromMilliseconds(60),
                AutoReverse = true
            };
            Storyboard.SetTarget(scaleY, MarkerTransform);
            Storyboard.SetTargetProperty(scaleY, new PropertyPath(ScaleTransform.ScaleYProperty));

            storyboard.Children.Add(scaleX);
            storyboard.Children.Add(scaleY);
            storyboard.Begin();
        }

        public bool ContainsPoint(Point point)
        {
            return point.X >= 0 && point.X <= Width && point.Y >= 0 && point.Y <= Height;
        }

        private static void ApplyLayer(Image image, BitmapSource source, CompositePinLayerPlan layer)
        {
            image.Source = source;
            image.Width = layer.SourceWidth;
            image.Height = layer.SourceHeight;
            image.RenderTransform = new MatrixTransform(layer.Transform);
            image.Clip = BuildClipGeometry(layer.ClipPolygon);
        }

        private void ApplyDebugOverlay(CompositePinRenderPlan plan, bool showDebugOverlay)
        {
            DebugOverlayCanvas.Visibility = showDebugOverlay ? Visibility.Visible : Visibility.Collapsed;
            if (!showDebugOverlay)
                return;

            DebugAxisLine.X1 = plan.TipAnchorLocal.X;
            DebugAxisLine.Y1 = plan.TipAnchorLocal.Y;
            DebugAxisLine.X2 = plan.JoinAnchorLocal.X;
            DebugAxisLine.Y2 = plan.JoinAnchorLocal.Y;

            DebugStretchLine.X1 = plan.StretchStartLocal.X;
            DebugStretchLine.Y1 = plan.StretchStartLocal.Y;
            DebugStretchLine.X2 = plan.StretchEndLocal.X;
            DebugStretchLine.Y2 = plan.StretchEndLocal.Y;

            PositionDebugDot(DebugTipDot, plan.TipAnchorLocal);
            PositionDebugDot(DebugJoinDot, plan.JoinAnchorLocal);
            PositionDebugDot(DebugStretchStartDot, plan.StretchStartLocal);
            PositionDebugDot(DebugStretchEndDot, plan.StretchEndLocal);
            PositionDebugDot(DebugHeadCenterDot, plan.HeadCenterLocal);
        }

        private static Geometry? BuildClipGeometry(IReadOnlyList<Point> polygon)
        {
            if (polygon.Count < 3)
                return null;

            var figure = new PathFigure
            {
                StartPoint = polygon[0],
                IsClosed = true,
                IsFilled = true
            };

            for (var i = 1; i < polygon.Count; i++)
            {
                figure.Segments.Add(new LineSegment(polygon[i], true));
            }

            return new PathGeometry(new[] { figure });
        }

        private static void PositionDebugDot(FrameworkElement element, Point point)
        {
            Canvas.SetLeft(element, point.X - (element.Width / 2.0));
            Canvas.SetTop(element, point.Y - (element.Height / 2.0));
        }
    }
}
