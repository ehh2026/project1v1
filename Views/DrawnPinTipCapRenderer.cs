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
    /// Draws drawn-pin tip caps onto the marker canvas at a Z-index above extension lines
    /// (999/1000) and below marker heads (2000). Caps are siblings of the markers, never
    /// children of a pin's visual tree, so they stay horizontal in screen space and ignore
    /// hover/rotation transforms applied to the pin. Visual objects are pooled by index so a
    /// steady set of caps does not reallocate each placement tick.
    ///
    /// Cap <em>geometry</em> is built by the orchestrator (which may use <c>Utilities</c>) and
    /// passed in, keeping this Views type dependent on Models only.
    /// </summary>
    public sealed class DrawnPinTipCapRenderer
    {
        private const int OutlineZIndex = 1500;
        private const int CoreZIndex    = 1501;

        private readonly Panel _canvas;

        private sealed class CapVisual
        {
            public Path Outline { get; init; } = null!;
            public Path Core    { get; init; } = null!;
        }

        private readonly List<CapVisual> _pool = new();

        // Cached brushes so an unchanged color does not reallocate every tick.
        private Brush _coreBrush    = Brushes.Gainsboro;
        private Brush _outlineBrush = Brushes.Black;
        private string? _coreKey;
        private string? _outlineKey;

        public DrawnPinTipCapRenderer(Panel canvas)
        {
            _canvas = canvas ?? throw new ArgumentNullException(nameof(canvas));
        }

        /// <summary>Number of currently visible caps (for tests / diagnostics).</summary>
        public int VisibleCapCount { get; private set; }

        /// <summary>Removes all cap visuals from the canvas and clears the pool.</summary>
        public void Clear()
        {
            foreach (var cap in _pool)
            {
                _canvas.Children.Remove(cap.Outline);
                _canvas.Children.Remove(cap.Core);
            }
            _pool.Clear();
            VisibleCapCount = 0;
        }

        /// <summary>
        /// Rebuilds the cap overlay from the given (already-built) geometries — one per cap.
        /// When the style is <see cref="DrawnPinTipCapStyle.None"/> the overlay is emptied.
        /// </summary>
        public void Sync(
            IReadOnlyList<Geometry> geometries,
            DrawnPinTipCapConfig capConfig,
            PinMarkerConfig pinConfig)
        {
            if (capConfig == null || pinConfig == null || capConfig.Style == DrawnPinTipCapStyle.None)
            {
                HideFrom(0);
                VisibleCapCount = 0;
                return;
            }

            RefreshBrushes(capConfig, pinConfig);
            double outlineExtra = Math.Max(pinConfig.ShaftOutlineThickness, 0.0);
            bool useOutline = capConfig.UseOutlineRing && outlineExtra > 0.0;

            int i = 0;
            for (; i < geometries.Count; i++)
            {
                var geometry = geometries[i];
                if (geometry == null)
                    continue;

                var cap = EnsurePoolEntry(i);

                cap.Core.Data = geometry;
                cap.Core.Fill = _coreBrush;
                cap.Core.Visibility = Visibility.Visible;

                if (useOutline)
                {
                    cap.Outline.Data = geometry;
                    cap.Outline.Fill = _outlineBrush;
                    cap.Outline.Stroke = _outlineBrush;
                    cap.Outline.StrokeThickness = outlineExtra * 2.0;
                    cap.Outline.Visibility = Visibility.Visible;
                }
                else
                {
                    cap.Outline.Visibility = Visibility.Collapsed;
                }
            }

            HideFrom(i);
            VisibleCapCount = i;
        }

        private CapVisual EnsurePoolEntry(int index)
        {
            if (index < _pool.Count)
                return _pool[index];

            var outline = new Path
            {
                IsHitTestVisible = false,
                StrokeLineJoin   = PenLineJoin.Round
            };
            var core = new Path
            {
                IsHitTestVisible = false
            };

            Panel.SetZIndex(outline, OutlineZIndex);
            Panel.SetZIndex(core, CoreZIndex);

            _canvas.Children.Add(outline);
            _canvas.Children.Add(core);

            var cap = new CapVisual { Outline = outline, Core = core };
            _pool.Add(cap);
            return cap;
        }

        private void HideFrom(int index)
        {
            for (int j = index; j < _pool.Count; j++)
            {
                _pool[j].Outline.Visibility = Visibility.Collapsed;
                _pool[j].Core.Visibility    = Visibility.Collapsed;
            }
        }

        private void RefreshBrushes(DrawnPinTipCapConfig capConfig, PinMarkerConfig pinConfig)
        {
            string coreKey = capConfig.Color ?? pinConfig.ShaftColor ?? string.Empty;
            if (coreKey != _coreKey)
            {
                _coreBrush = MakeBrush(coreKey, Colors.Gainsboro);
                _coreKey   = coreKey;
            }

            string outlineKey = pinConfig.ShaftOutlineColor ?? string.Empty;
            if (outlineKey != _outlineKey)
            {
                _outlineBrush = MakeBrush(outlineKey, Color.FromRgb(26, 26, 26));
                _outlineKey   = outlineKey;
            }
        }

        private static Brush MakeBrush(string? value, Color fallback)
        {
            Color color = fallback;
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
