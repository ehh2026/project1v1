using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using InteractiveWorldMap.Models;
using InteractiveWorldMap.Utilities;
using InteractiveWorldMap.Views;

namespace InteractiveWorldMap
{
    public partial class MainWindow
    {
        // Drawn pins shorter than this (stub height or extension-line length, in screen px)
        // do not get a tip cap — there is no room for one to read as anything but a smudge.
        private const double MinShaftLengthForCapPx = 8.0;

        private DrawnPinTipCapRenderer? _pinTipCapRenderer;

        /// <summary>
        /// Resolves one tip cap per visible drawn pin — on the terminus of whichever shaft is
        /// visible (built-in stub or extension line) — and hands the placements to the renderer.
        /// Runs at the end of every placement pass, so caps track hover, drag, and zoom for free.
        /// </summary>
        private void UpdatePinTipCaps()
        {
            _pinTipCapRenderer ??= new DrawnPinTipCapRenderer(MapDisplay.Markers);

            var capConfig = _visualConfig.PinMarkers?.DrawnPinTipCap;
            if (capConfig == null ||
                capConfig.Style == DrawnPinTipCapStyle.None ||
                !_visualConfig.UsePinMarkers)
            {
                _pinTipCapRenderer.Clear();
                return;
            }

            var placements = new List<PinTipCapPlacement>(_individualMarkers.Count);

            foreach (var marker in _individualMarkers)
            {
                if (marker.Visibility != Visibility.Visible)
                    continue;
                if (marker.Content is not PinMarker pin)
                    continue;

                if (_extensionLineRenderer.HasLine(marker))
                {
                    if (TryBuildExtensionPlacement(marker, out var extPlacement))
                        placements.Add(extPlacement);
                }
                else if (pin.IsShaftVisible)
                {
                    if (TryBuildStubPlacement(marker, pin, out var stubPlacement))
                        placements.Add(stubPlacement);
                }
            }

            var geometries = new List<Geometry>(placements.Count);
            foreach (var placement in placements)
            {
                var geometry = BuildCapGeometry(placement, capConfig);
                if (geometry != null)
                    geometries.Add(geometry);
            }

            _pinTipCapRenderer.Sync(geometries, capConfig, _visualConfig.PinMarkers);
        }

        /// <summary>
        /// Builds the screen-space cap geometry for one placement. Lives in the orchestrator
        /// (not the renderer) so the geometry helpers in <c>Utilities</c> stay out of <c>Views</c>.
        /// </summary>
        private static Geometry? BuildCapGeometry(PinTipCapPlacement placement, DrawnPinTipCapConfig config)
        {
            double halfWidth = PinTipCapGeometry.HalfWidth(placement.OutlineWidthPx, config.ExtendPx);
            if (halfWidth <= 0.0)
                return null;

            return config.Style switch
            {
                DrawnPinTipCapStyle.Horizontal =>
                    PinTipCapGeometry.BuildHorizontal(placement.TipScreen, halfWidth, config.HeightPx),
                DrawnPinTipCapStyle.Concave =>
                    PinTipCapGeometry.BuildConcave(placement.TipScreen, placement.ShaftDir, halfWidth, config.ArcDepthPx),
                _ => null
            };
        }

        /// <summary>Cap on the built-in stub tip — the screen-projected, hover-scaled shaft tip.</summary>
        private bool TryBuildStubPlacement(LocationMarker marker, PinMarker pin, out PinTipCapPlacement placement)
        {
            placement = default;

            double left = Canvas.GetLeft(marker);
            double top  = Canvas.GetTop(marker);
            if (double.IsNaN(left) || double.IsNaN(top))
                return false;

            var tipLocal        = pin.GetScaledShaftTipPoint();
            var connectionLocal = pin.GetScaledConnectionPoint();

            // Direction is offset-independent, so compute it from the local points.
            var shaftVec = new Vector(
                connectionLocal.X - tipLocal.X,
                connectionLocal.Y - tipLocal.Y);
            if (shaftVec.Length < MinShaftLengthForCapPx)
                return false;

            var tipScreen = new Point(left + tipLocal.X, top + tipLocal.Y);
            placement = new PinTipCapPlacement(
                marker.Location.Name,
                tipScreen,
                Normalize(shaftVec),
                pin.GetScaledShaftOutlineWidth(),
                PinTipCapShaftKind.BuiltIn);
            return true;
        }

        /// <summary>Cap on the extension-line tip (map anchor / line start), shaft hidden.</summary>
        private bool TryBuildExtensionPlacement(LocationMarker marker, out PinTipCapPlacement placement)
        {
            placement = default;

            if (!_extensionLineRenderer.TryGetLineStart(marker, out var start))
                return false;
            if (!_extensionLineRenderer.TryGetLineEndpoint(marker, out var end))
                return false;

            var shaftVec = new Vector(end.X - start.X, end.Y - start.Y);
            if (shaftVec.Length < MinShaftLengthForCapPx)
                return false;

            placement = new PinTipCapPlacement(
                marker.Location.Name,
                start,
                Normalize(shaftVec),
                GetExtensionLineOutlineWidth(),
                PinTipCapShaftKind.ExtensionLine);
            return true;
        }

        /// <summary>
        /// Outline-inclusive width of an extension-line shaft — mirrors the sizing in
        /// <c>ExtensionLineRenderer.CreatePinLinePair</c> so the cap matches the line.
        /// </summary>
        private double GetExtensionLineOutlineWidth()
        {
            var pinConfig = _visualConfig.PinMarkers;
            double coreWidth    = Math.Max(pinConfig.ShaftWidth, 2.5);
            double outlineExtra = Math.Max(pinConfig.ShaftOutlineThickness, 1.0);
            return coreWidth + (2.0 * outlineExtra);
        }

        private static Vector Normalize(Vector v)
        {
            double len = v.Length;
            return len <= double.Epsilon ? new Vector(0, -1) : new Vector(v.X / len, v.Y / len);
        }
    }
}
