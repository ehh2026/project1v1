using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Controls.Primitives;
using System.Windows.Shapes;
using InteractiveWorldMap.Models;
using InteractiveWorldMap.Services;
using InteractiveWorldMap.Utilities;
using InteractiveWorldMap.Views;
using IOPath = System.IO.Path;

namespace InteractiveWorldMap
{
    public partial class MainWindow
    {
        /// <summary>
        /// Handles marker drag start.
        /// </summary>
        private void OnMarkerDragStart(object sender, MouseButtonEventArgs e)
        {
            if (!_layoutEditor.IsEditMode || sender is not LocationMarker marker)
                return;

            _draggedMarker = marker;
            _dragStartPosition = e.GetPosition(MapDisplay.Markers);

            if (marker.Content is AutoStubPinMarker)
            {
                var viewport = MapDisplay.CurrentViewport;
                if (viewport != null)
                {
                    var tipScreen = viewport.SourceToScreen(
                        marker.Location.PixelX,
                        marker.Location.PixelY,
                        MapDisplay.ActualWidth,
                        MapDisplay.ActualHeight);
                    var headScreen = GetMarkerEndpoint(marker);

                    SetDrawnPinRole(marker, DrawnPinRole.ManualLayout);
                    if (!_extensionLineRenderer.HasLine(marker))
                        _extensionLineRenderer.AddLine(marker, tipScreen, headScreen);
                    _extensionLineRenderer.AnchorExtendedMarker(marker, headScreen);
                }
            }

            marker.CaptureMouse();

            // Highlight the dragged marker
            marker.Opacity = 0.7;

            // Bring marker and its line to front
            Panel.SetZIndex(marker, 2000);
            _extensionLineRenderer.SetLineZIndex(marker, 1999);

            e.Handled = true;
        }

        /// <summary>
        /// Handles marker drag movement.
        /// Phase 4: composite pins are rebuilt so the head follows the mouse while the tip stays fixed.
        /// </summary>
        private void OnMarkerDragMove(object sender, MouseEventArgs e)
        {
            if (!_layoutEditor.IsEditMode || _draggedMarker == null || sender != _draggedMarker)
                return;

            if (e.LeftButton == MouseButtonState.Pressed)
            {
                var currentPosition = e.GetPosition(MapDisplay.Markers);
                var viewport = MapDisplay.CurrentViewport;
                var cw = MapDisplay.ActualWidth;
                var ch = MapDisplay.ActualHeight;

                // Phase 4: composite pin drag — rebuild pin so head follows mouse, tip stays fixed
                if (_draggedMarker.Content is CompositePinMarker)
                {
                    if (viewport == null) return;
                    var originalPos = viewport.SourceToScreen(
                        _draggedMarker.Location.PixelX,
                        _draggedMarker.Location.PixelY,
                        cw, ch);

                    // Constrain to canvas bounds
                    var boundsWidth = MapDisplay.Markers.ActualWidth;
                    var boundsHeight = MapDisplay.Markers.ActualHeight;
                    var mousePos = new Point(
                        Math.Max(0, Math.Min(currentPosition.X, boundsWidth)),
                        Math.Max(0, Math.Min(currentPosition.Y, boundsHeight)));

                    // Rebuild composite pin with new target (always full reapply — angle/length change each tick)
                    ApplyCompositePinToMarker(_draggedMarker, originalPos, mousePos);

                    // Policy A: drive guide line to the clamped rendered head, not raw cursor.
                    // When MaxStretchFactor clamps the shaft, the rendered head stops before the cursor;
                    // aligning the guide line endpoint to the rendered position keeps them coincident.
                    if (_draggedMarker.Content is CompositePinMarker draggedCpm && draggedCpm.RenderPlan != null)
                    {
                        var renderedHead = new Point(
                            Canvas.GetLeft(_draggedMarker) + draggedCpm.RenderPlan.HeadCenterLocal.X,
                            Canvas.GetTop(_draggedMarker)  + draggedCpm.RenderPlan.HeadCenterLocal.Y);

                        if (_extensionLineRenderer.HasLine(_draggedMarker))
                            _extensionLineRenderer.MoveLineEndpoint(_draggedMarker, renderedHead);

                        _overrideStore.RecordEndpoints(_draggedMarker.Location.Name, originalPos, renderedHead);
                        LogDragDebug($"[DRAG] Composite pin '{_draggedMarker.Location.Name}' head at ({renderedHead.X:F1},{renderedHead.Y:F1})");
                    }
                    else
                    {
                        if (_extensionLineRenderer.HasLine(_draggedMarker))
                            _extensionLineRenderer.MoveLineEndpoint(_draggedMarker, mousePos);
                        _overrideStore.RecordEndpoints(_draggedMarker.Location.Name, originalPos, mousePos);
                        LogDragDebug($"[DRAG] Composite pin '{_draggedMarker.Location.Name}' head at ({mousePos.X:F1},{mousePos.Y:F1})");
                    }
                    UpdatePinTipCaps();
                    return;
                }

                // Drawn pin drag: keep tip fixed at Excel location and show a connecting shaft.
                if (viewport == null) return;

                var tipScreen = viewport.SourceToScreen(
                    _draggedMarker.Location.PixelX, _draggedMarker.Location.PixelY, cw, ch);

                var headScreen = new Point(
                    Math.Max(0, Math.Min(currentPosition.X, MapDisplay.Markers.ActualWidth)),
                    Math.Max(0, Math.Min(currentPosition.Y, MapDisplay.Markers.ActualHeight)));

                if (!_extensionLineRenderer.HasLine(_draggedMarker))
                {
                    _extensionLineRenderer.AddLine(_draggedMarker, tipScreen, headScreen);
                    _extensionLineRenderer.SetLineZIndex(_draggedMarker, 1999);
                }
                else
                {
                    _extensionLineRenderer.MoveLineEndpoint(_draggedMarker, headScreen);
                }

                // Anchor the head-only glyph by its connection point, not its bounding-box center.
                _extensionLineRenderer.AnchorExtendedMarker(_draggedMarker, headScreen);
                _overrideStore.RecordEndpoints(_draggedMarker.Location.Name, tipScreen, headScreen);
                UpdatePinTipCaps();

                LogDragDebug($"[DRAG] Drawn pin '{_draggedMarker.Location.Name}' head at ({headScreen.X:F1},{headScreen.Y:F1}), tip at ({tipScreen.X:F1},{tipScreen.Y:F1})");
            }
        }

        private void LogDragDebug(string message)
        {
            if (_visualConfig.Debug.LogRadialExtensionCalculation)
            {
                _logger.LogInfo(message);
            }
        }

        /// <summary>
        /// Handles marker drag end.
        /// </summary>
        private void OnMarkerDragEnd(object sender, MouseButtonEventArgs e)
        {
            if (!_layoutEditor.IsEditMode || _draggedMarker == null)
                return;

            _draggedMarker.ReleaseMouseCapture();

            // Restore marker appearance
            _draggedMarker.Opacity = 1.0;
            Panel.SetZIndex(_draggedMarker, 0);
            _extensionLineRenderer.SetLineZIndex(_draggedMarker, 0);
            ApplyCompositePinDepthSort();
            UpdatePinTipCaps();

            _draggedMarker = null;

            e.Handled = true;
        }
    }
}
