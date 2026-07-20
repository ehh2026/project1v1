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

        private void PrepareMarkerVisualsForPlacementUpdate()
        {
            if (CanUseCompositePins())
                return;

            RestoreBaseMarkerVisuals();
        }

        private bool RestoreDrawnFallbackForCompositeFailure(LocationMarker marker)
        {
            RestoreBaseMarkerVisual(marker);
            return false;
        }

        private bool RestoreDrawnFallbackForCompositeFailure(LocationMarker marker, MarkerScreenPlacement placement)
        {
            RestoreBaseMarkerVisual(marker);
            if (!TryPlaceDrawnPinAtMapPoint(marker, placement))
            {
                Canvas.SetLeft(marker, placement.Left);
                Canvas.SetTop(marker, placement.Top);
            }
            return false;
        }

        private bool TryPlaceDrawnPinAtMapPoint(LocationMarker marker, MarkerScreenPlacement placement)
        {
            if (marker.Content is not AutoStubPinMarker autoStub)
                return false;

            var mapPoint = GetMarkerMapPoint(placement);
            var shaftTip = autoStub.GetShaftTipPoint();
            Canvas.SetLeft(marker, mapPoint.X - shaftTip.X);
            Canvas.SetTop(marker, mapPoint.Y - shaftTip.Y);
            return true;
        }

        private Point GetMarkerMapPoint(MarkerScreenPlacement placement)
        {
            var locationMarkerRadius = _visualConfig.LocationMarkerSize / 2.0;
            return new Point(
                placement.Left + locationMarkerRadius,
                placement.Top + locationMarkerRadius);
        }

        private void AnimateMarkerClick(LocationMarker marker)
        {
            switch (marker.Content)
            {
                case AutoStubPinMarker autoStub:
                    autoStub.AnimateClick();
                    _logger.LogInfo($"Animated PIN marker click for '{marker.Location.Name}'");
                    break;
                case ManualLayoutPinMarker manual:
                    manual.AnimateClick();
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
                    _contentLoader.ResolvePinPartPath(_visualConfig.PinParts.GeometryMetadataPath));
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

            var target = new PinPlacementTarget
            {
                StartScreen = originalScreenPos,
                EndScreen = extendedScreenPos,
                LocationId = marker.Location.Name,
                GroupId = 0
            };

            var ok = TryApplyCompositePinAtTarget(marker, target, preferredPairId, preferredHeadSourcePath);
            // Phase 4: extension line as drag guide + endpoint source in edit mode
            if (ok && _layoutEditor.IsEditMode)
                _extensionLineRenderer.AddLine(marker, originalScreenPos, extendedScreenPos);
            return ok;
        }

        /// <summary>
        /// Applies or repositions a composite pin for a built target. Reposition-only when the
        /// segment vector and assignment are unchanged (Phase 7).
        /// </summary>
        private bool TryApplyCompositePinAtTarget(LocationMarker marker, PinPlacementTarget target,
            string? preferredPairId = null, string? preferredHeadSourcePath = null)
        {
            if (marker.Content is CompositePinMarker compositeMarker &&
                compositeMarker.RenderPlan != null &&
                CompositePinPlacementPolicy.ShouldRepositionOnly(
                    compositeMarker.RenderPlan, target, preferredPairId, preferredHeadSourcePath))
            {
                RepositionCompositePinMarker(marker, target, compositeMarker.RenderPlan);
                return true;
            }

            return ApplyCompositePinTargetToMarker(marker, target, preferredPairId, preferredHeadSourcePath);
        }

        private void RepositionCompositePinMarker(
            LocationMarker marker,
            PinPlacementTarget target,
            CompositePinRenderPlan plan)
        {
            var topLeft = CompositePinPlacementPolicy.GetCompositeTopLeft(target.StartScreen, plan);
            Canvas.SetLeft(marker, topLeft.X);
            Canvas.SetTop(marker, topLeft.Y);
            _overrideStore.RecordEndpoints(marker.Location.Name, target.StartScreen, target.EndScreen);
            RefreshMarkerHitTargets();
        }

        /// <summary>
        /// Core composite-pin apply logic. Used by drag, reassign, and override paths.
        /// </summary>
        private bool ApplyCompositePinToMarker(LocationMarker marker, Point originalScreenPos, Point extendedScreenPos,
            string? preferredPairId = null, string? preferredHeadSourcePath = null)
        {
            var target = new PinPlacementTarget
            {
                StartScreen = originalScreenPos,
                EndScreen = extendedScreenPos,
                LocationId = marker.Location.Name,
                GroupId = 0
            };

            return ApplyCompositePinTargetToMarker(marker, target, preferredPairId, preferredHeadSourcePath);
        }

        private bool ApplyCompositePinTargetToMarker(LocationMarker marker, PinPlacementTarget target,
            string? preferredPairId = null, string? preferredHeadSourcePath = null)
        {
            if (!EnsurePinPartGeometryLoaded() || _pinPartGeometry == null)
                return RestoreDrawnFallbackForCompositeFailure(marker);

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
                    _logger.LogWarning($"Composite pin assets missing for '{marker.Location.Name}', leaving drawn pin fallback.");
                    return RestoreDrawnFallbackForCompositeFailure(marker);
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
                return RestoreDrawnFallbackForCompositeFailure(marker);
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
            compositeMarker.ApplyHeadShadow(
                _visualConfig.PinMarkers.ShowShadow,
                _visualConfig.PinMarkers.ShadowOpacity);
            compositeMarker.SetCompositeImages(
                shaftImage,
                headImage,
                plan,
                AreDeveloperToolsEnabled() && _visualConfig.Debug.ShowCompositePinDebugOverlay,
                _visualConfig.PinParts.UsePrerasterizedRendering);
            compositeMarker.ShaftOverrideRequested += locName => OnShaftOverrideRequested(marker, locName);
            _overrideStore.RecordEndpoints(marker.Location.Name, originalScreenPos, extendedScreenPos);
            marker.Content = compositeMarker;
            marker.Width   = compositeMarker.Width;
            marker.Height  = compositeMarker.Height;
            Panel.SetZIndex(marker, 2000);
            var topLeft = CompositePinPlacementPolicy.GetCompositeTopLeft(originalScreenPos, plan);
            Canvas.SetLeft(marker, topLeft.X);
            Canvas.SetTop(marker, topLeft.Y);
            RefreshMarkerHitTargets();
        }

        private static bool IsPinStyleMarkerBase(object? content)
        {
            return content is AutoStubPinMarker or CompositePinMarker;
        }

        private bool TryGetCompositeAnchoredPlacement(LocationMarker marker, MarkerScreenPlacement placement, out Point topLeft)
        {
            topLeft = default;

            if (!CanUseCompositePins() ||
                marker.Content is not CompositePinMarker compositeMarker ||
                compositeMarker.RenderPlan == null)
            {
                return false;
            }

            topLeft = CompositePinPlacementPolicy.GetCompositeTopLeft(
                placement,
                _visualConfig.LocationMarkerSize,
                compositeMarker.RenderPlan);
            return true;
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

        /// <summary>
        /// Applies composite pins to normal (non-extension) placements when composite mode is on.
        /// During <see cref="InteractionMode.Animating"/>, this method returns early; tip reposition
        /// for existing composites is handled by <c>ApplyIndividualPlacements</c> until settled state.
        /// </summary>
        private void ApplyCompositePinsToNormalPlacements(
            IReadOnlyList<MarkerScreenPlacement> placements,
            ViewportState viewport,
            double containerWidth,
            double containerHeight,
            IReadOnlyDictionary<string, LocationMarker> markerByName)
        {
            if (!CanUseCompositePins() || IsAnimating)
                return;

            foreach (var placement in placements)
            {
                if (!markerByName.TryGetValue(placement.LocationName, out var marker)
                    || marker.Visibility != Visibility.Visible
                    || _extensionLineRenderer.HasLine(marker))
                    continue;

                var target = _compositePinTargetBuilder.Build(
                    marker.Location,
                    viewport,
                    containerWidth,
                    containerHeight,
                    _visualConfig.PinParts);
                if (TryApplyCompositePinAtTarget(marker, target))
                {
                    // Phase 4: stub line as drag guide + endpoint source in edit mode
                    if (_layoutEditor.IsEditMode)
                        _extensionLineRenderer.AddLine(marker, target.StartScreen, target.EndScreen);
                    continue;
                }

                RestoreDrawnFallbackForCompositeFailure(marker, placement);
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
            // Overrides record composite head/shaft choices, so replaying them applies a composite
            // pin (ApplyCompositePinToMarker bypasses the mode check). In drawn-pin mode that would
            // leak a composite pin onto the overridden marker — visible e.g. only when zooming into
            // that single location, where ApplyManualLayout triggers this replay. Never apply
            // composite overrides unless composite rendering is actually active.
            if (!CanUseCompositePins())
                return;

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
            var autoStub =
                (AutoStubPinMarker)_drawnPinFactory.Create(DrawnPinRole.AutoStub);

            var pinConfig = _visualConfig.PinMarkers;
            if (!pinConfig.UseRandomColors)
            {
                if (ColorConverter.ConvertFromString(pinConfig.DefaultBallColor) is Color defaultColor)
                    autoStub.PinColor = defaultColor;
            }

            var marker = new LocationMarker(_visualConfig) { Location = location };
            marker.Content = autoStub;
            marker.Width = autoStub.Width;
            marker.Height = autoStub.Height;
            marker.Tag = autoStub;
            return marker;
        }

        private void RefreshDrawnPinVisuals()
        {
            foreach (var marker in _individualMarkers)
            {
                ApplyDrawnPinConfig(marker, marker.Content);

                if (!_baseMarkerVisuals.TryGetValue(marker, out var state))
                    continue;

                ApplyDrawnPinConfig(marker, state.Content, updateMarkerBounds: false);
                if (state.Content is FrameworkElement baseContent)
                {
                    _baseMarkerVisuals[marker] = new MarkerVisualState
                    {
                        Content = state.Content,
                        Width = baseContent.Width,
                        Height = baseContent.Height
                    };
                }
            }
        }

        private void ApplyDrawnPinConfig(
            LocationMarker marker,
            object? content,
            bool updateMarkerBounds = true)
        {
            FrameworkElement? drawnContent = content switch
            {
                AutoStubPinMarker autoStub => ApplyAutoStubConfig(autoStub),
                ManualLayoutPinMarker manual => ApplyManualPinConfig(manual),
                _ => null
            };

            if (!updateMarkerBounds || drawnContent == null ||
                !ReferenceEquals(marker.Content, content))
                return;

            marker.Width = drawnContent.Width;
            marker.Height = drawnContent.Height;
        }

        private FrameworkElement ApplyAutoStubConfig(AutoStubPinMarker marker)
        {
            marker.ApplyConfig(_visualConfig.PinMarkers);
            return marker;
        }

        private FrameworkElement ApplyManualPinConfig(ManualLayoutPinMarker marker)
        {
            marker.ApplyConfig(_visualConfig.PinMarkers);
            return marker;
        }

    }
}
