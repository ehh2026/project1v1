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
        #region Radial Extension Methods

        private void CaptureBaseMarkerVisual(LocationMarker marker)
        {
            if (_baseMarkerVisuals.ContainsKey(marker))
                return;

            _baseMarkerVisuals[marker] = new MarkerVisualState
            {
                Content = marker.Content,
                Width = marker.Width,
                Height = marker.Height
            };
        }

        private void RestoreBaseMarkerVisuals()
        {
            foreach (var marker in _individualMarkers)
            {
                RestoreBaseMarkerVisual(marker);
            }
        }

        private void RestoreBaseMarkerVisual(LocationMarker marker)
        {
            if (!_baseMarkerVisuals.TryGetValue(marker, out var state))
                return;

            if (!ReferenceEquals(marker.Content, state.Content))
            {
                marker.Content = state.Content;
                marker.Width = state.Width;
                marker.Height = state.Height;
            }
        }

        private void AnimateMarkerClick(LocationMarker marker)
        {
            switch (marker.Content)
            {
                case PinMarker pinMarker:
                    pinMarker.AnimateClick();
                    _logger.LogInfo($"Animated PIN marker click for '{marker.Location.Name}'");
                    break;
                case CompositePinMarker compositePinMarker:
                    compositePinMarker.AnimateClick();
                    _logger.LogInfo($"Animated COMPOSITE PIN marker click for '{marker.Location.Name}'");
                    break;
                default:
                    marker.AnimateClick();
                    _logger.LogInfo($"Animated REGULAR marker click for '{marker.Location.Name}'");
                    break;
            }
        }

        private bool CanUseCompositePins()
        {
            return _visualConfig.UsePinMarkers &&
                   _visualConfig.PinParts.Enabled &&
                   _visualConfig.PinParts.UseCompositeRendering;
        }

        private bool EnsurePinPartGeometryLoaded()
        {
            if (_pinPartGeometry != null && _pinPartGeometry.Count > 0)
                return true;

            try
            {
                _pinPartGeometry = _contentLoader.LoadPinPartGeometry(_visualConfig.PinParts.GeometryMetadataPath);
                // Phase 4: cache hash of geometry file for composite plan cache key
                _pinPartGeometryHash = CompositePinLayoutContentHasher.ComputeGeometryHash(
                    IOPath.Combine(AppDomain.CurrentDomain.BaseDirectory, _visualConfig.PinParts.GeometryMetadataPath));
                return _pinPartGeometry.Count > 0;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to load pin-part geometry metadata: {ex.Message}");
                _pinPartGeometry = null;
                return false;
            }
        }

        private BitmapSource? LoadPinPartBitmap(string relativePath)
        {
            if (_pinPartBitmapCache.TryGetValue(relativePath, out var cached))
            {
                return cached;
            }

            var bitmap = _contentLoader.TryLoadContentBitmap(relativePath);
            if (bitmap != null)
            {
                _pinPartBitmapCache[relativePath] = bitmap;
            }

            return bitmap;
        }

        private bool TryApplyCompositePinMarker(LocationMarker marker, Point originalScreenPos, Point extendedScreenPos,
            string? preferredPairId = null, string? preferredHeadSourcePath = null)
        {
            if (!CanUseCompositePins())
                return false;
            var ok = ApplyCompositePinToMarker(marker, originalScreenPos, extendedScreenPos, preferredPairId, preferredHeadSourcePath);
            // Phase 4: extension line as drag guide + endpoint source in edit mode
            if (ok && _layoutEditor.IsEditMode)
                _extensionLineRenderer.AddLine(marker, originalScreenPos, extendedScreenPos);
            return ok;
        }

        /// <summary>
        /// Core composite-pin apply logic. Used both by the normal (non-edit) path via
        /// <see cref="TryApplyCompositePinMarker"/> and by Reassign Pins which bypasses the
        /// edit-mode gate in <see cref="CanUseCompositePins"/>.
        /// </summary>
        private bool ApplyCompositePinToMarker(LocationMarker marker, Point originalScreenPos, Point extendedScreenPos,
            string? preferredPairId = null, string? preferredHeadSourcePath = null)
        {
            var target = _compositePinTargetBuilder.Build(
                marker.Location,
                new ViewportState(),
                containerWidth: 0,
                containerHeight: 0,
                _visualConfig.PinParts,
                new RadialExtension
                {
                    Location = marker.Location,
                    OriginalPosition = originalScreenPos,
                    ExtendedPosition = extendedScreenPos,
                    GroupId = 0
                });

            return ApplyCompositePinTargetToMarker(marker, target, preferredPairId, preferredHeadSourcePath);
        }

        private bool ApplyCompositePinTargetToMarker(LocationMarker marker, PinPlacementTarget target,
            string? preferredPairId = null, string? preferredHeadSourcePath = null)
        {
            if (!EnsurePinPartGeometryLoaded() || _pinPartGeometry == null)
                return false;

            if (!_baseMarkerVisuals.TryGetValue(marker, out var baseState) || !IsPinStyleMarkerBase(baseState.Content))
                return false;

            try
            {
                var planning = _compositePinPlanningService.BuildPlan(
                    target, _pinPartGeometry, _visualConfig.PinParts,
                    preferredPairId, preferredHeadSourcePath);
                var shaftImage = LoadPinPartBitmap(planning.RenderPlan.ShaftSourcePath);
                var headImage = LoadPinPartBitmap(planning.RenderPlan.HeadSourcePath);
                if (shaftImage == null || headImage == null)
                {
                    _logger.LogWarning($"Composite pin assets missing for '{marker.Location.Name}', falling back to legacy extension rendering.");
                    return false;
                }

                ApplyRenderPlanToMarker(marker, target.StartScreen, target.EndScreen, planning.RenderPlan, shaftImage, headImage);
                _logger.LogInfo(
                    $"Applied composite pin '{planning.Selection.PairId}' for '{marker.Location.Name}' " +
                    $"targetLength={planning.Selection.TargetLengthPx:F1}px score={planning.Selection.Score:F2}");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"Failed to apply composite pin for '{marker.Location.Name}': {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Applies a pre-built render plan to a marker — shared by the normal build path and
        /// the Phase 4 cache-hit path.  Does NOT call BuildPlan.
        /// </summary>
        private void ApplyRenderPlanToMarker(
            LocationMarker marker,
            Point originalScreenPos,
            Point extendedScreenPos,
            CompositePinRenderPlan plan,
            BitmapSource shaftImage,
            BitmapSource headImage)
        {
            var compositeMarker = new CompositePinMarker { Location = marker.Location };
            compositeMarker.SetCompositeImages(
                shaftImage,
                headImage,
                plan,
                _visualConfig.Debug.ShowCompositePinDebugOverlay,
                _visualConfig.PinParts.UsePrerasterizedRendering);
            compositeMarker.ShaftOverrideRequested += locName => OnShaftOverrideRequested(marker, locName);
            _overrideStore.RecordEndpoints(marker.Location.Name, originalScreenPos, extendedScreenPos);
            marker.Content = compositeMarker;
            marker.Width   = compositeMarker.Width;
            marker.Height  = compositeMarker.Height;
            Panel.SetZIndex(marker, 2000);
            Canvas.SetLeft(marker, originalScreenPos.X - plan.TipAnchorLocal.X);
            Canvas.SetTop(marker, originalScreenPos.Y - plan.TipAnchorLocal.Y);
        }

        private static bool IsPinStyleMarkerBase(object? content)
        {
            return content is PinMarker or CompositePinMarker;
        }

        private void ApplyCompositePinDepthSort()
        {
            var markerById = new Dictionary<string, LocationMarker>(StringComparer.Ordinal);
            var depthItems = new List<CompositePinDepthItem>();

            foreach (var marker in _individualMarkers.Where(m => m.Visibility == Visibility.Visible))
            {
                if (marker.Content is not CompositePinMarker compositeMarker || compositeMarker.RenderPlan == null)
                    continue;

                var markerId = marker.Location.Name;
                if (markerById.ContainsKey(markerId))
                    continue;

                var plan = compositeMarker.RenderPlan;
                var left = Canvas.GetLeft(marker);
                var top = Canvas.GetTop(marker);
                if (double.IsNaN(left) || double.IsNaN(top))
                    continue;

                markerById[markerId] = marker;
                depthItems.Add(new CompositePinDepthItem(
                    markerId,
                    new Point(left + plan.TipAnchorLocal.X, top + plan.TipAnchorLocal.Y),
                    plan.JoinAnchorLocal - plan.TipAnchorLocal));
            }

            if (depthItems.Count == 0)
                return;

            var sorted = _compositePinDepthSorter.Sort(depthItems);
            for (var i = 0; i < sorted.Count; i++)
            {
                if (markerById.TryGetValue(sorted[i].MarkerId, out var marker))
                    Panel.SetZIndex(marker, 2000 + i);
            }
        }

        private void ApplyCompositePinsToNormalPlacements(
            IReadOnlyList<MarkerScreenPlacement> placements,
            ViewportState viewport,
            double containerWidth,
            double containerHeight)
        {
            if (!CanUseCompositePins() || IsAnimating)
                return;

            foreach (var placement in placements)
            {
                var marker = _individualMarkers.FirstOrDefault(
                    m => m.Visibility == Visibility.Visible
                         && string.Equals(m.Location.Name, placement.LocationName, StringComparison.Ordinal));
                if (marker == null || _extensionLineRenderer.HasLine(marker))
                    continue;

                var target = _compositePinTargetBuilder.Build(
                    marker.Location,
                    viewport,
                    containerWidth,
                    containerHeight,
                    _visualConfig.PinParts);
                if (ApplyCompositePinTargetToMarker(marker, target))
                {
                    // Phase 4: stub line as drag guide + endpoint source in edit mode
                    if (_layoutEditor.IsEditMode)
                        _extensionLineRenderer.AddLine(marker, target.StartScreen, target.EndScreen);
                }
            }
        }

        /// <summary>
        /// Handles Reassign Pins button click — re-runs composite shaft/head selection on the
        /// current canvas endpoints without saving or exiting edit mode.
        /// </summary>
        private void OnReassignPinsButtonClick(object sender, RoutedEventArgs e)
        {
            if (!EnsurePinPartGeometryLoaded() || _pinPartGeometry == null)
            {
                _logger.LogWarning("[ReassignPins] Pin part geometry not loaded, cannot reassign.");
                return;
            }

            var viewport = MapDisplay.CurrentViewport;
            if (viewport == null) return;

            var cw = MapDisplay.ActualWidth;
            var ch = MapDisplay.ActualHeight;
            int applied = 0;

            foreach (var marker in _individualMarkers.Where(m => m.Visibility == Visibility.Visible && _extensionLineRenderer.HasLine(m)))
            {
                var originalPos = viewport.SourceToScreen(marker.Location.PixelX, marker.Location.PixelY, cw, ch);

                if (!_extensionLineRenderer.TryGetLineEndpoint(marker, out var extendedPos))
                    continue;

                if (ApplyCompositePinToMarker(marker, originalPos, extendedPos))
                    applied++;
            }

            ApplyCompositePinDepthSort();
            _logger.LogInfo($"[ReassignPins] Applied composite pins to {applied} markers.");
        }

        /// <summary>
        /// Called when the user right-clicks a composite pin to request a shaft change.
        /// Builds a context menu from ranked candidates and re-renders on selection.
        /// </summary>
        private void OnShaftOverrideRequested(LocationMarker marker, string locationName)
        {
            if (_layoutEditor.IsEditMode) return;
            if (!EnsurePinPartGeometryLoaded() || _pinPartGeometry == null) return;
            if (!_overrideStore.TryGetEndpoints(locationName, out var originalPos, out var extendedPos)) return;

            var currentPairId = (marker.Content as CompositePinMarker)?.RenderPlan?.PairId;
            var target = new PinPlacementTarget
            {
                StartScreen = originalPos,
                EndScreen = extendedPos,
                LocationId = locationName,
                GroupId = 0
            };

            var items = _shaftMenuModelBuilder.BuildMenuItems(target, _pinPartGeometry, _visualConfig.PinParts, currentPairId);

            var menu = new ContextMenu();
            menu.Items.Add(new MenuItem { Header = "Change shaft", IsEnabled = false, FontWeight = FontWeights.Bold });
            menu.Items.Add(new Separator());

            foreach (var item in items)
            {
                var capturedPairId = item.PairId;
                var menuItem = new MenuItem { Header = item.Label, IsChecked = item.IsSelected };
                menuItem.Click += (_, _) =>
                {
                    _overrideStore.SetOverride(locationName, capturedPairId);
                    ApplyCompositePinToMarker(marker, originalPos, extendedPos, capturedPairId);
                    ApplyCompositePinDepthSort();
                    UpdateOverrideIndicator();
                };
                menu.Items.Add(menuItem);
            }

            menu.Placement = PlacementMode.Mouse;
            menu.IsOpen = true;
        }

        /// <summary>
        /// Re-applies all pending shaft overrides after a layout replay (e.g., on exit edit mode).
        /// Only runs in non-edit mode; relies on endpoints recorded at last composite apply time.
        /// </summary>
        private void ReapplyPendingOverrides()
        {
            foreach (var kvp in _overrideStore.GetAllOverrides())
            {
                var locationName = kvp.Key;
                var (pairId, headSourcePath) = kvp.Value;
                var marker = _individualMarkers.FirstOrDefault(m =>
                    string.Equals(m.Location.Name, locationName, StringComparison.Ordinal));
                if (marker == null) continue;
                if (!_overrideStore.TryGetEndpoints(locationName, out var originalPos, out var extendedPos)) continue;
                ApplyCompositePinToMarker(marker, originalPos, extendedPos, pairId, headSourcePath);
            }
        }

        /// <summary>
        /// Shows or hides the unsaved overrides indicator based on current state.
        /// </summary>
        private void UpdateOverrideIndicator()
        {
            OverridePendingIndicator.Visibility =
                _overrideStore.HasPendingOverrides && !_layoutEditor.IsEditMode
                    ? Visibility.Visible
                    : Visibility.Collapsed;
        }

        #endregion

        /// <summary>
        /// Creates a pin-style marker for a location.
        /// </summary>
        private LocationMarker CreatePinMarker(Location location)
        {
            return CreateDrawnPinMarker(location);
        }

        /// <summary>
        /// Creates a drawn pin marker as fallback.
        /// </summary>
        private LocationMarker CreateDrawnPinMarker(Location location)
        {
            var pinMarker = new PinMarker(_visualConfig) { Location = location };

            var pinConfig = _visualConfig.PinMarkers;
            if (!pinConfig.UseRandomColors)
            {
                if (ColorConverter.ConvertFromString(pinConfig.DefaultBallColor) is Color defaultColor)
                    pinMarker.SetPinColor(defaultColor);
            }

            var marker = new LocationMarker(_visualConfig) { Location = location };
            marker.Content = pinMarker;
            marker.Width = pinMarker.Width;
            marker.Height = pinMarker.Height;
            marker.Tag = pinMarker;
            return marker;
        }

    }
}
