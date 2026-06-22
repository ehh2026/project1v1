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

        private readonly List<Line>                       _lines            = new();
        private readonly Dictionary<LocationMarker, Line> _markerToLine     = new();
        private readonly Dictionary<LocationMarker, PinExtensionLines> _markerToPinLines = new();
        private readonly Dictionary<Line, LineStyle>      _lineStyles       = new();

        private sealed class PinExtensionLines
        {
            public Line Outline { get; init; } = null!;
            public Line Core { get; init; } = null!;
        }

        private sealed class LineStyle
        {
            public Brush Stroke { get; init; } = Brushes.Gray;
            public double StrokeThickness { get; init; } = 2.5;
            public int ZIndex { get; init; }
        }

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
            _markerToPinLines.Clear();
            _lineStyles.Clear();
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
                        if (log)
                            _logInfo($"    Composite marker positioned with tip anchor at ({originalScreenPos.X:F1},{originalScreenPos.Y:F1})");

                        continue;
                    }

                    AddLine(marker, originalScreenPos, extendedScreenPos);

                    if (log)
                        _logInfo($"    Line added to canvas, total lines: {_lines.Count}, canvas children: {_canvas.Children.Count}");

                    AnchorExtendedMarker(marker, extendedScreenPos);

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
        // Extended-marker anchoring
        // -------------------------------------------------------------------------

        public void AnchorExtendedMarker(LocationMarker marker, Point extendedScreenPos)
        {
            // Head must sit above the extension line (lines live at z ~999/1000).
            Panel.SetZIndex(marker, 2000);

            // Drawn pins: anchor the head on the extension endpoint and hide the pin's
            // own shaft. The extension line becomes the single shaft, so there is no
            // off-axis duplicate shaft drawn on top of the head.
            if (marker.Content is PinMarker pin)
            {
                pin.SetShaftVisible(false);
                var connection = pin.GetConnectionPoint();
                Canvas.SetLeft(marker, extendedScreenPos.X - connection.X);
                Canvas.SetTop(marker,  extendedScreenPos.Y - connection.Y);
                return;
            }

            // Other marker types stay center-anchored on the extension endpoint.
            Canvas.SetLeft(marker, extendedScreenPos.X - marker.Width / 2);
            Canvas.SetTop(marker,  extendedScreenPos.Y - marker.Height / 2);
        }

        // -------------------------------------------------------------------------
        // AddLine — also used by ApplyManualLayout in MainWindow
        // -------------------------------------------------------------------------

        public void AddLine(LocationMarker marker, Point start, Point end)
        {
            if (_visualConfig.UsePinMarkers)
            {
                var pair = CreatePinLinePair(start, end);
                AddPinLines(marker, pair, wireHoverHandlers: true);
                return;
            }

            var line = CreateDebugLine(start, end);
            RememberLineStyle(line);
            _canvas.Children.Add(line);
            _lines.Add(line);
            _markerToLine[marker] = line;
            marker.MouseEnter += OnMouseEnter;
            marker.MouseLeave += OnMouseLeave;
        }

        private void AddPinLines(LocationMarker marker, PinExtensionLines pair, bool wireHoverHandlers)
        {
            RememberLineStyle(pair.Outline);
            RememberLineStyle(pair.Core);

            _canvas.Children.Add(pair.Outline);
            _canvas.Children.Add(pair.Core);
            _lines.Add(pair.Outline);
            _lines.Add(pair.Core);
            _markerToPinLines[marker] = pair;
            _markerToLine[marker] = pair.Core;

            if (wireHoverHandlers)
            {
                marker.MouseEnter += OnMouseEnter;
                marker.MouseLeave += OnMouseLeave;
            }
        }

        // -------------------------------------------------------------------------
        // Drag support
        // -------------------------------------------------------------------------

        public void MoveLineEndpoint(LocationMarker marker, Point newEndpoint)
        {
            if (!_markerToLine.TryGetValue(marker, out var oldLine))
                return;

            if (_markerToPinLines.TryGetValue(marker, out var oldPair))
            {
                var zIndex = Panel.GetZIndex(oldPair.Core);
                RemoveLine(oldPair.Outline);
                RemoveLine(oldPair.Core);
                _markerToPinLines.Remove(marker);

                var newPair = CreatePinLinePair(
                    new Point(oldPair.Core.X1, oldPair.Core.Y1),
                    newEndpoint);
                Panel.SetZIndex(newPair.Outline, zIndex - 1);
                Panel.SetZIndex(newPair.Core, zIndex);
                AddPinLines(marker, newPair, wireHoverHandlers: false);
                return;
            }

            var singleZIndex = Panel.GetZIndex(oldLine);
            RemoveLine(oldLine);

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
            Panel.SetZIndex(newLine, singleZIndex);
            _lines.Add(newLine);
            _markerToLine[marker] = newLine;
            if (_lineStyles.TryGetValue(oldLine, out var style))
                _lineStyles[newLine] = style;
            _lineStyles.Remove(oldLine);
        }

        private void RemoveLine(Line line)
        {
            _canvas.Children.Remove(line);
            _lines.Remove(line);
            _lineStyles.Remove(line);
        }

        public void SetLineZIndex(LocationMarker marker, int zIndex)
        {
            if (_markerToPinLines.TryGetValue(marker, out var pair))
            {
                Panel.SetZIndex(pair.Outline, zIndex - 1);
                Panel.SetZIndex(pair.Core, zIndex);
                return;
            }

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
            if (sender is not LocationMarker marker || !_markerToLine.TryGetValue(marker, out var line))
                return;

            ApplyHoverHighlight(line, hovered: true);

            if (_markerToPinLines.TryGetValue(marker, out var pair))
                ApplyHoverHighlight(pair.Outline, hovered: true, isOutline: true);
        }

        public void OnMouseLeave(object sender, MouseEventArgs e)
        {
            if (sender is not LocationMarker marker || !_markerToLine.TryGetValue(marker, out var line))
                return;

            ApplyHoverHighlight(line, hovered: false);

            if (_markerToPinLines.TryGetValue(marker, out var pair))
                ApplyHoverHighlight(pair.Outline, hovered: false, isOutline: true);
        }

        private void ApplyHoverHighlight(Line line, bool hovered, bool isOutline = false)
        {
            var resting = GetLineStyle(line);

            if (hovered)
            {
                // Brighten and thicken slightly so the shaft reads as a metallic highlight
                // (the dark outline keeps its color, just grows with the core).
                line.StrokeThickness = resting.StrokeThickness + 1.0;
                if (!isOutline)
                    line.Stroke = new SolidColorBrush(Brighten(GetBrushColor(resting.Stroke, Colors.Gainsboro), 0.3));
                Panel.SetZIndex(line, 1999);
                return;
            }

            line.StrokeThickness = resting.StrokeThickness;
            line.Stroke          = resting.Stroke;
            Panel.SetZIndex(line, resting.ZIndex);
        }

        private static Color GetBrushColor(Brush brush, Color fallback) =>
            brush is SolidColorBrush solid ? solid.Color : fallback;

        private static Color Brighten(Color c, double amount)
        {
            amount = Math.Clamp(amount, 0.0, 1.0);
            return Color.FromRgb(
                (byte)(c.R + (255 - c.R) * amount),
                (byte)(c.G + (255 - c.G) * amount),
                (byte)(c.B + (255 - c.B) * amount));
        }

        // -------------------------------------------------------------------------
        // Private: line factory (merged from CreateExtensionLine + CreatePinExtensionLine)
        // -------------------------------------------------------------------------

        private PinExtensionLines CreatePinLinePair(Point start, Point end)
        {
            var pinConfig = _visualConfig.PinMarkers;
            var shaftColor = ParseColor(pinConfig.ShaftColor, Colors.Gainsboro);
            var outlineColor = ParseColor(pinConfig.ShaftOutlineColor, Color.FromRgb(26, 26, 26));
            double coreWidth = Math.Max(pinConfig.ShaftWidth, 2.5);
            double outlineExtra = Math.Max(pinConfig.ShaftOutlineThickness, 1.0);

            var outline = new Line
            {
                X1               = start.X,
                Y1               = start.Y,
                X2               = end.X,
                Y2               = end.Y,
                Stroke           = new SolidColorBrush(outlineColor),
                StrokeThickness  = coreWidth + (2 * outlineExtra),
                Opacity          = 1.0,
                IsHitTestVisible = false
            };

            var core = new Line
            {
                X1               = start.X,
                Y1               = start.Y,
                X2               = end.X,
                Y2               = end.Y,
                Stroke           = new SolidColorBrush(shaftColor),
                StrokeThickness  = coreWidth,
                Opacity          = 1.0,
                IsHitTestVisible = false
            };

            if (pinConfig.ShowShadow)
            {
                var shadow = new DropShadowEffect
                {
                    Color       = Colors.Black,
                    Direction   = 270,
                    ShadowDepth = 1,
                    BlurRadius  = 2,
                    Opacity     = Math.Max(pinConfig.ShadowOpacity, 0.45)
                };
                outline.Effect = shadow;
                core.Effect = shadow;
            }

            Panel.SetZIndex(outline, 999);
            Panel.SetZIndex(core, 1000);
            _logInfo($"    Created pin extension line pair: ({start.X:F1},{start.Y:F1}) to ({end.X:F1},{end.Y:F1}), core={coreWidth:F1}px");
            return new PinExtensionLines { Outline = outline, Core = core };
        }

        private static Color ParseColor(string? value, Color fallback)
        {
            return !string.IsNullOrWhiteSpace(value) &&
                   ColorConverter.ConvertFromString(value) is Color parsed
                ? parsed
                : fallback;
        }

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

        private void RememberLineStyle(Line line)
        {
            _lineStyles[line] = new LineStyle
            {
                Stroke          = line.Stroke,
                StrokeThickness = line.StrokeThickness,
                ZIndex          = Panel.GetZIndex(line)
            };
        }

        private LineStyle GetLineStyle(Line line) =>
            _lineStyles.TryGetValue(line, out var style)
                ? style
                : new LineStyle();

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
