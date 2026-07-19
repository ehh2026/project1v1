using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using InteractiveWorldMap.Models;

namespace InteractiveWorldMap.Views
{
    /// <summary>
    /// Draws unfilled tip-cap strokes above extension lines and below pin heads.
    /// Paths are pooled so placement ticks do not reallocate steady-state visuals.
    /// </summary>
    public sealed class DrawnPinTipCapRenderer
    {
        private const int CapZIndex = 1501;
        private readonly Panel _canvas;
        private readonly List<Path> _pool = new();
        private Brush _strokeBrush = MakeBrush(null);
        private string? _strokeKey;

        public DrawnPinTipCapRenderer(Panel canvas)
        {
            _canvas = canvas ?? throw new ArgumentNullException(nameof(canvas));
        }

        public int VisibleCapCount { get; private set; }

        public void Clear()
        {
            foreach (var path in _pool)
                _canvas.Children.Remove(path);

            _pool.Clear();
            VisibleCapCount = 0;
        }

        public void Sync(
            IReadOnlyList<Geometry> geometries,
            DrawnPinTipCapConfig capConfig,
            PinMarkerConfig pinConfig)
        {
            if (capConfig == null ||
                pinConfig == null ||
                capConfig.Style == DrawnPinTipCapStyle.None)
            {
                HideFrom(0);
                VisibleCapCount = 0;
                return;
            }

            RefreshBrush(capConfig.Color);
            double lineWeight = capConfig.ResolveLineWeightPx(
                pinConfig.ShaftOutlineThickness);

            int visibleCount = 0;
            foreach (var geometry in geometries)
            {
                if (geometry == null)
                    continue;

                var path = EnsurePoolEntry(visibleCount);
                path.Data = geometry;
                path.Fill = null;
                path.Stroke = _strokeBrush;
                path.StrokeThickness = lineWeight;
                path.Visibility = Visibility.Visible;
                visibleCount++;
            }

            HideFrom(visibleCount);
            VisibleCapCount = visibleCount;
        }

        private Path EnsurePoolEntry(int index)
        {
            if (index < _pool.Count)
                return _pool[index];

            var path = new Path
            {
                IsHitTestVisible = false,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round,
                StrokeLineJoin = PenLineJoin.Round
            };
            Panel.SetZIndex(path, CapZIndex);
            _canvas.Children.Add(path);
            _pool.Add(path);
            return path;
        }

        private void HideFrom(int index)
        {
            for (int i = index; i < _pool.Count; i++)
                _pool[i].Visibility = Visibility.Collapsed;
        }

        private void RefreshBrush(string? color)
        {
            string key = color ?? string.Empty;
            if (key == _strokeKey)
                return;

            _strokeBrush = MakeBrush(color);
            _strokeKey = key;
        }

        private static Brush MakeBrush(string? value)
        {
            Color color = Color.FromRgb(17, 17, 17);
            if (!string.IsNullOrWhiteSpace(value) &&
                ColorConverter.ConvertFromString(value) is Color parsed)
            {
                color = parsed;
            }

            var brush = new SolidColorBrush(color);
            brush.Freeze();
            return brush;
        }
    }
}
