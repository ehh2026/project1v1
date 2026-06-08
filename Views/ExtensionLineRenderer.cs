using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using InteractiveWorldMap.Models;

namespace InteractiveWorldMap.Views
{
    /// <summary>
    /// Creates, tracks, animates, and removes radial extension lines on the map canvas.
    /// Owns the extension-line collection and marker-to-line mapping previously held
    /// by MainWindow.
    /// </summary>
    public sealed class ExtensionLineRenderer : IExtensionLineRenderer
    {
        private readonly Panel          _canvas;
        private readonly VisualConfig   _visualConfig;
        private readonly Action<string> _logInfo;
        private readonly Action<string> _logWarning;

        private readonly List<Line>                       _lines        = new();
        private readonly Dictionary<LocationMarker, Line> _markerToLine = new();

        public ExtensionLineRenderer(Panel canvas, VisualConfig visualConfig,
            Action<string> logInfo, Action<string> logWarning)
        {
            _canvas     = canvas       ?? throw new ArgumentNullException(nameof(canvas));
            _visualConfig = visualConfig ?? throw new ArgumentNullException(nameof(visualConfig));
            _logInfo    = logInfo      ?? throw new ArgumentNullException(nameof(logInfo));
            _logWarning = logWarning   ?? throw new ArgumentNullException(nameof(logWarning));
        }

        // -------------------------------------------------------------------------
        // State queries
        // -------------------------------------------------------------------------

        public int  LineCount          => _lines.Count;
        public int  MarkerMappingCount => _markerToLine.Count;
        public bool HasLine(LocationMarker marker) => _markerToLine.ContainsKey(marker);

        // -------------------------------------------------------------------------
        // Clear
        // -------------------------------------------------------------------------

        public void Clear()
        {
            foreach (var line in _lines)
                _canvas.Children.Remove(line);

            _lines.Clear();
            _markerToLine.Clear();
        }

        // -------------------------------------------------------------------------
        // Apply (main radial extension rendering — extracted from ApplyRadialExtensions)
        // -------------------------------------------------------------------------

        public void Apply(
            DenseMarkerGroup              group,
            ViewportState                 viewport,
            double                        containerWidth,
            double                        containerHeight,
            IReadOnlyList<LocationMarker> markers,
            Func<LocationMarker, Point, Point, bool> tryCompositePinApplier)
        {
            bool log           = _visualConfig.Debug.LogRadialExtensionCalculation;
            int  linesBefore   = _lines.Count;

            if (log)
            {
                _logInfo($"[ApplyRadialExtensions] Applying {group.Extensions.Count} extensions");
                _logInfo($"[ApplyRadialExtensions] Canvas children before: {_canvas.Children.Count}");
            }

            foreach (var extension in group.Extensions)
            {
                var originalScreenPos = viewport.SourceToScreen(
                    extension.Location.PixelX,
                    extension.Location.PixelY,
                    containerWidth,
                    containerHeight);

                var extendedScreenPos = extension.ExtendedPosition;

                if (log)
                {
                    double dx           = extendedScreenPos.X - originalScreenPos.X;
                    double dy           = extendedScreenPos.Y - originalScreenPos.Y;
                    double length       = Math.Sqrt(dx * dx + dy * dy);
                    double angleDegrees = Math.Atan2(dx, -dy) * (180.0 / Math.PI);
                    if (angleDegrees < 0) angleDegrees += 360.0;

                    _logInfo($"  Extension: {extension.Location.Name} from ({originalScreenPos.X:F1},{originalScreenPos.Y:F1}) to ({extendedScreenPos.X:F1},{extendedScreenPos.Y:F1})");
                    _logInfo($"    Length: {length:F1}px, Angle: {angleDegrees:F2}° (stored: {extension.Angle:F2}°)");
                }

                var marker = markers.FirstOrDefault(m => m.Location == extension.Location);
                if (marker != null)
                {
                    if (tryCompositePinApplier(marker, originalScreenPos, extendedScreenPos))
                    {
                        _markerToLine.Remove(marker);

                        if (log)
                            _logInfo($"    Composite marker positioned with tip anchor at ({originalScreenPos.X:F1},{originalScreenPos.Y:F1})");

                        continue;
                    }

                    AddLine(marker, originalScreenPos, extendedScreenPos);

                    if (log)
                        _logInfo($"    Line added to canvas, total lines: {_lines.Count}, canvas children: {_canvas.Children.Count}");

                    Panel.SetZIndex(marker, 2000);
                    Canvas.SetLeft(marker, extendedScreenPos.X - marker.Width / 2);
                    Canvas.SetTop(marker,  extendedScreenPos.Y - marker.Height / 2);

                    if (log)
                        _logInfo($"    Marker positioned at ({extendedScreenPos.X:F1},{extendedScreenPos.Y:F1}), ZIndex=2000");
                }
                else
                {
                    _logWarning($"    Marker not found for location: {extension.Location.Name}");
                }
            }

            if (log)
            {
                _logInfo($"[ApplyRadialExtensions] Canvas children after: {_canvas.Children.Count}");
                _logInfo($"[ApplyRadialExtensions] Total extension lines in list: {_lines.Count}");
            }

            if (_visualConfig.RadialExtension.AnimateExtension)
            {
                var linesToAnimate = _lines.Skip(linesBefore).ToList();
                if (log)
                    _logInfo($"[ApplyRadialExtensions] Animating {linesToAnimate.Count} lines");
                Animate(linesToAnimate);
            }
        }

        // -------------------------------------------------------------------------
        // AddLine — also used by ApplyManualLayout in MainWindow
        // -------------------------------------------------------------------------

        public void AddLine(LocationMarker marker, Point start, Point end)
        {
            var line = CreateLine(start, end);
            _canvas.Children.Add(line);
            _lines.Add(line);
            _markerToLine[marker] = line;
            marker.MouseEnter += OnMouseEnter;
            marker.MouseLeave += OnMouseLeave;
        }

        // -------------------------------------------------------------------------
        // Drag support
        // -------------------------------------------------------------------------

        public void MoveLineEndpoint(LocationMarker marker, Point newEndpoint)
        {
            if (!_markerToLine.TryGetValue(marker, out var oldLine))
                return;

            var zIndex = Panel.GetZIndex(oldLine);

            _canvas.Children.Remove(oldLine);
            _lines.Remove(oldLine);

            var newLine = new Line
            {
                X1              = oldLine.X1,
                Y1              = oldLine.Y1,
                X2              = newEndpoint.X,
                Y2              = newEndpoint.Y,
                Stroke          = oldLine.Stroke,
                StrokeThickness = oldLine.StrokeThickness
            };

            _canvas.Children.Add(newLine);
            Panel.SetZIndex(newLine, zIndex);
            _lines.Add(newLine);
            _markerToLine[marker] = newLine;
        }

        public void SetLineZIndex(LocationMarker marker, int zIndex)
        {
            if (_markerToLine.TryGetValue(marker, out var line))
                Panel.SetZIndex(line, zIndex);
        }

        public bool TryGetLineEndpoint(LocationMarker marker, out Point endpoint)
        {
            if (_markerToLine.TryGetValue(marker, out var line))
            {
                endpoint = new Point(line.X2, line.Y2);
                return true;
            }
            endpoint = default;
            return false;
        }

        // -------------------------------------------------------------------------
        // Hover highlighting (moved from MainWindow.OnMarkerMouseEnter/Leave)
        // -------------------------------------------------------------------------

        public void OnMouseEnter(object sender, MouseEventArgs e)
        {
            if (sender is LocationMarker marker && _markerToLine.TryGetValue(marker, out var line))
            {
                line.StrokeThickness = 5.0;
                line.Stroke          = new SolidColorBrush(Color.FromRgb(255, 100, 100));
                Panel.SetZIndex(line, 1999);
            }
        }

        public void OnMouseLeave(object sender, MouseEventArgs e)
        {
            if (sender is LocationMarker marker && _markerToLine.TryGetValue(marker, out var line))
            {
                line.StrokeThickness = 3.0;
                line.Stroke          = new SolidColorBrush(Colors.Red);
                Panel.SetZIndex(line, 0);
            }
        }

        // -------------------------------------------------------------------------
        // Private: line factory (merged from CreateExtensionLine + CreatePinExtensionLine)
        // -------------------------------------------------------------------------

        private Line CreateLine(Point start, Point end) =>
            _visualConfig.UsePinMarkers
                ? CreatePinLine(start, end)
                : CreateDebugLine(start, end);

        private Line CreateDebugLine(Point start, Point end)
        {
            var line = new Line
            {
                X1              = start.X,
                Y1              = start.Y,
                X2              = end.X,
                Y2              = end.Y,
                Stroke          = new SolidColorBrush(Colors.Red),
                StrokeThickness = 3.0,
                Opacity         = 1.0,
                IsHitTestVisible = false
            };

            line.Effect = new DropShadowEffect
            {
                Color       = Colors.Black,
                Direction   = 270,
                ShadowDepth = 1,
                BlurRadius  = 2,
                Opacity     = 0.3
            };

            Panel.SetZIndex(line, 1000);
            _logInfo($"    Created line: ({start.X:F1},{start.Y:F1}) to ({end.X:F1},{end.Y:F1}), Stroke=Red, Thickness=3");
            return line;
        }

        private Line CreatePinLine(Point start, Point end)
        {
            var pinConfig  = _visualConfig.PinMarkers;
            Color shaftColor = Colors.Gray;
            if (ColorConverter.ConvertFromString(pinConfig.ShaftColor) is Color configColor)
                shaftColor = configColor;

            var line = new Line
            {
                X1              = start.X,
                Y1              = start.Y,
                X2              = end.X,
                Y2              = end.Y,
                Stroke          = new SolidColorBrush(shaftColor),
                StrokeThickness = pinConfig.ShaftWidth,
                Opacity         = 0.9,
                IsHitTestVisible = false
            };

            if (pinConfig.ShowShadow)
            {
                line.Effect = new DropShadowEffect
                {
                    Color       = Colors.Black,
                    Direction   = 270,
                    ShadowDepth = 0.5,
                    BlurRadius  = 1,
                    Opacity     = pinConfig.ShadowOpacity
                };
            }

            Panel.SetZIndex(line, 1000);
            _logInfo($"    Created pin extension line: ({start.X:F1},{start.Y:F1}) to ({end.X:F1},{end.Y:F1}), Shaft color, Thickness={pinConfig.ShaftWidth}");
            return line;
        }

        // -------------------------------------------------------------------------
        // Private: animation (extracted from AnimateExtensionLines)
        // -------------------------------------------------------------------------

        private void Animate(List<Line> lines)
        {
            var duration = TimeSpan.FromMilliseconds(_visualConfig.RadialExtension.ExtensionAnimationMs);
            var easing   = new QuadraticEase { EasingMode = EasingMode.EaseOut };

            for (int i = 0; i < lines.Count; i++)
            {
                var line    = lines[i];
                var finalX2 = line.X2;
                var finalY2 = line.Y2;

                line.X2 = line.X1;
                line.Y2 = line.Y1;

                var delay = TimeSpan.FromMilliseconds(i * 10);

                var animX2 = new DoubleAnimation
                {
                    From          = line.X1,
                    To            = finalX2,
                    Duration      = duration,
                    EasingFunction = easing,
                    BeginTime     = delay
                };
                var animY2 = new DoubleAnimation
                {
                    From          = line.Y1,
                    To            = finalY2,
                    Duration      = duration,
                    EasingFunction = easing,
                    BeginTime     = delay
                };

                line.BeginAnimation(Line.X2Property, animX2);
                line.BeginAnimation(Line.Y2Property, animY2);
            }
        }
    }
}
