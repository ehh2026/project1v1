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
        /// Animates zooming into a cluster using viewport-based rendering.
        /// </summary>
        private void AnimateZoomToCluster(LocationCluster cluster)
        {
            if (_layoutEditor.IsEditMode)
            {
                ShowEditModeNavigationBlockedStatus();
                return;
            }

            try
            {
                _logger.LogInfo("=== AnimateZoomToCluster START (Viewport) ===");
                _logger.LogInfo($"  Cluster: {cluster.Count} locations");
                _logger.LogInfo($"  Cluster center: ({cluster.CenterPoint.X:F2}, {cluster.CenterPoint.Y:F2})");

                var currentState = ZoomState.CreateFullMapView();
                _navigationService.PushState(currentState);
                _logger.LogInfo("  Current state saved to navigation stack");

                var startViewport = MapDisplay.CurrentViewport;
                if (startViewport == null)
                {
                    _logger.LogError("Current viewport is null");
                    _autoOpenLocation = null;
                    return;
                }

                // Phase 1: capture settled-state pin-to-map offsets so markers track
                // the map during the animation instead of freezing in place.
                // Must run while _mode == Normal so the orchestrator produces
                // authoritative placements (MarkerPlacementMode.WithExtensions).
                {
                    UpdateMarkerPositions();

                    // UpdateMarkerPositions() above recomputes *default* placements: it rebuilds
                    // single-location pins as default stubs (composite) or drops the manual
                    // extension line (drawn), discarding the edited appearance an active full-map
                    // manual layout applied. Re-apply that layout before capturing offsets so the
                    // pins tracked through the zoom animation match the settled edited appearance
                    // instead of reverting to default stubs for the duration of the zoom.
                    if (_layoutEditor.IsManualLayoutActive)
                        TryApplyFullMapManualLayout();

                    var containerWidth = MapDisplay.ActualWidth;
                    var containerHeight = MapDisplay.ActualHeight;

                    foreach (var marker in _individualMarkers.Where(m => m.Visibility == Visibility.Visible))
                    {
                        var pMap = startViewport.SourceToScreen(
                            marker.Location.PixelX,
                            marker.Location.PixelY,
                            containerWidth,
                            containerHeight);

                        var pCanvas = new Point(Canvas.GetLeft(marker), Canvas.GetTop(marker));
                        if (double.IsNaN(pCanvas.X) || double.IsNaN(pCanvas.Y))
                            continue;

                        Point anchor;
                        if (marker.Content is AutoStubPinMarker autoStub)
                            anchor = autoStub.GetShaftTipPoint();
                        else if (marker.Content is ManualLayoutPinMarker manual)
                            anchor = manual.GetConnectionPoint();
                        else if (marker.Content is CompositePinMarker composite)
                            anchor = composite.GetTipAnchorPoint();
                        else
                            anchor = new Point(0, 0);

                        _animationOffsets[marker] = new Vector(
                            pCanvas.X + anchor.X - pMap.X,
                            pCanvas.Y + anchor.Y - pMap.Y);
                    }

                    _extensionLineRenderer.Clear();
                    _logger.LogInfo($"  Captured animation offsets for {_animationOffsets.Count} markers");
                }

                // Drawn-pin manual layouts render their shaft as a separate extension line that
                // the offset system above just cleared, and AnchorExtendedMarker hides the pin's
                // own shaft — so without per-frame replay only the head would show during zoom-in.
                // Mirror zoom-out: replay the layout each frame so the shaft tracks the map. Returns
                // null in composite mode (CanUseCompositePins), where the offset path already keeps
                // the whole composite pin — head and shaft — together.
                var animationLayout = TryLoadFullMapManualLayoutForAnimation();

                _mode = InteractionMode.Animating;

                var targetViewport = ViewportState.CreateZoomedView(
                    cluster.CenterPoint.X,
                    cluster.CenterPoint.Y,
                    ZoomScale,
                    ImageWidth,
                    ImageHeight,
                    MapDisplay.ActualWidth,
                    MapDisplay.ActualHeight);

                _logger.LogInfo($"  Start viewport: ({startViewport.ViewportX:F2}, {startViewport.ViewportY:F2}) {startViewport.ViewportWidth:F2}x{startViewport.ViewportHeight:F2}");
                _logger.LogInfo($"  Target viewport: ({targetViewport.ViewportX:F2}, {targetViewport.ViewportY:F2}) {targetViewport.ViewportWidth:F2}x{targetViewport.ViewportHeight:F2}");

                AnimateViewportTransition(
                    startViewport,
                    targetViewport,
                    "Zoom animation",
                    () =>
                    {
                        var toOpen = _autoOpenLocation;
                        _autoOpenLocation = null;

                        _animationOffsets.Clear();
                        ShowZoomedView(cluster);
                        BackButton.Visibility = Visibility.Visible;

                        if (toOpen != null)
                        {
                            ShowContentForLocation(toOpen);
                        }
                    },
                    () => ApplyManualLayoutDuringAnimation(animationLayout));
            }
            catch (Exception ex)
            {
                _autoOpenLocation = null;
                _logger.LogError($"Error zooming to cluster: {ex.Message}\n{ex.StackTrace}");
            }
        }


        /// <summary>
        /// Phase 7: single-location zoom replays the full-map manual layout (angle/length/assignment)
        /// so the composite stub matches the unzoomed appearance — unless the user has deliberately
        /// arranged this zoomed view, in which case that more specific choice wins.
        /// </summary>
        /// <remarks>
        /// Precedence is by origin, not by scope: a Manual zoomed layout beats a Manual full-map
        /// layout, which beats auto-generated seeds. Scope alone is the wrong test, because every
        /// cluster has a seed and a seed must never override work the user did by hand.
        /// </remarks>
        private bool TryApplyFullMapLayoutForZoomedSingle(LocationCluster cluster)
        {
            if (!cluster.IsSingleLocation)
                return false;

            // A hand-made layout for this zoomed view is the more specific deliberate choice.
            if (HasManualLayoutForZoomedView(cluster))
                return false;

            // Honor a session unload: a zoomed single location stays at its auto-placed position.
            if (_layoutEditor.IsManualLayoutSuppressed)
                return false;

            var locationName = cluster.Locations[0].Name;
            var key = GenerateCurrentFullMapGroupKey();
            var layout = _layoutEditor.TryLoad(key);
            if (layout == null)
                return false;

            if (!FullMapLayoutContainsLocation(layout, locationName))
                return false;

            _logger.LogInfo(
                $"[TryApplyFullMapLayoutForZoomedSingle] Replaying full-map layout for '{locationName}' at key={key}");
            ApplyManualLayout(layout, key);
            _layoutEditor.SetManualLayoutActive(true);
            return true;
        }

        private static bool FullMapLayoutContainsLocation(ManualLayout layout, string locationName) =>
            layout.Markers.Any(m =>
                string.Equals(m.LocationName, locationName, StringComparison.Ordinal));

        /// <summary>
        /// Shows the cluster view (full map with cluster markers).
        /// </summary>
        private void ShowClusterView()
        {
            try
            {
                _logger.LogInfo("=== ShowClusterView START ===");
                _logger.LogInfo($"  Total clusters: {_clusters.Count}");

                // Show cluster markers, hide individual markers
                ShowOnlyClusterMarkers();
                TryApplyFullMapManualLayout();
                UpdateEditLayoutButtonVisibility();

                _logger.LogInfo($"=== ShowClusterView COMPLETE ===");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error showing cluster view: {ex.Message}\n{ex.StackTrace}");
            }
        }

        /// <summary>
        /// Shows individual markers for the zoomed cluster and displays high-quality zoomed image.
        /// </summary>
        private void ShowZoomedView(LocationCluster cluster)
        {
            try
            {
                _logger.LogInfo("=== ShowZoomedView START ===");
                _logger.LogInfo($"  Cluster has {cluster.Count} locations");

                // Track current cluster for edit mode
                ClearFullMapLayoutSession();
                _currentZoomedCluster = cluster;

                var viewport = MapDisplay.CurrentViewport;
                if (viewport != null)
                {
                    _logger.LogInfo($"  Current viewport: ({viewport.ViewportX:F2}, {viewport.ViewportY:F2}) {viewport.ViewportWidth:F2}x{viewport.ViewportHeight:F2}, zoom={viewport.ZoomLevel:F2}");

                    // Generate layout key and try to load saved layout
                    if (_layoutManager != null && _visualConfig.RadialExtension.Enabled)
                    {
                        var preferFullMapLayout = false;
                        if (cluster.IsSingleLocation && !HasManualLayoutForZoomedView(cluster))
                        {
                            var fullMapKey = GenerateCurrentFullMapGroupKey();
                            var fullMapLayout = _layoutEditor.TryLoad(fullMapKey);
                            preferFullMapLayout = fullMapLayout != null &&
                                FullMapLayoutContainsLocation(fullMapLayout, cluster.Locations[0].Name);
                            if (preferFullMapLayout)
                            {
                                _logger.LogInfo(
                                    "  Single-location zoom: full-map manual layout takes precedence over cluster layout");
                            }
                        }

                        if (!preferFullMapLayout)
                        {
                            // Local, not stored: this is the *display* key for the cluster being
                            // shown. The editor derives its own key when it opens, so nothing here
                            // needs to leave a value behind for it to find.
                            var clusterKey = LayoutKeyGenerator.GenerateKey(
                                cluster.Locations,
                                viewport,
                                _visualConfig.RadialExtension);

                            _logger.LogInfo($"  Generated layout key: {clusterKey}");

                            // A session unload means we do not stage the layout for auto-apply —
                            // the cluster reverts to auto-placement.
                            var savedLayout = _layoutEditor.TryLoad(clusterKey);
                            if (savedLayout != null && !_layoutEditor.IsManualLayoutSuppressed)
                            {
                                _logger.LogInfo($"  Found saved manual layout with {savedLayout.Markers.Count} markers");
                                _savedLayoutToApply = savedLayout; // Store for later application
                            }
                            else
                            {
                                _logger.LogInfo($"  No saved layout found for key: {clusterKey}");
                            }
                        }
                    }

                    // Load or generate high-quality zoomed region
                    var centerX = cluster.CenterPoint.X;
                    var centerY = cluster.CenterPoint.Y;
                    var request = TryCreateZoomedRegionRenderRequest(viewport, centerX, centerY);
                    var cachedRegion = request == null ? null : _zoomedRegionCache.TryLoadRegion(request);

                    if (cachedRegion != null)
                    {
                        _logger.LogInfo("  Loaded high-quality zoomed region from cache");
                        MapDisplay.DisplayImage.Source = cachedRegion;
                    }
                    else
                    {
                        _logger.LogInfo("  Generating high-quality zoomed region...");
                        var sourceImage = MapDisplay.SourceImage;

                        if (sourceImage != null && request != null)
                        {
                            var highQualityRegion = _zoomedRegionCache.GenerateAndCacheRegion(sourceImage, request);
                            MapDisplay.DisplayImage.Source = highQualityRegion;
                            _logger.LogInfo("  High-quality zoomed region generated and cached");
                        }
                    }
                }

                // Show only individual markers for this cluster (visibility only — placement below)
                ShowOnlyIndividualMarkers(cluster);

                if (cluster.IsSingleLocation && TryApplyFullMapLayoutForZoomedSingle(cluster))
                {
                    _logger.LogInfo("Full-map manual layout applied for single-location zoom");
                }
                else
                {
                    UpdateMarkerPositions();

                    // Apply saved cluster manual layout if one was found and not unloaded this session.
                    if (_savedLayoutToApply != null && !_layoutEditor.IsManualLayoutSuppressed)
                    {
                        ApplyManualLayout(_savedLayoutToApply);
                        _layoutEditor.SetManualLayoutActive(true);
                        _savedLayoutToApply = null; // Clear after applying

                        _logger.LogInfo("Manual layout applied after high-res region loaded");
                    }
                    else
                    {
                        _savedLayoutToApply = null; // Drop any stale/suppressed staged layout.
                    }
                }

                UpdateEditLayoutButtonVisibility();

                _logger.LogInfo($"=== ShowZoomedView COMPLETE ===");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error showing zoomed view: {ex.Message}\n{ex.StackTrace}");
            }
        }
        private void OnBackButtonClick(object sender, RoutedEventArgs e)
        {
            if (_layoutEditor.IsEditMode)
            {
                ShowEditModeNavigationBlockedStatus();
                return;
            }

            AnimateZoomOut();
        }

        /// <summary>
        /// Animates zooming out to the full map view using viewport-based rendering.
        /// </summary>
        private void AnimateZoomOut()
        {
            _autoOpenLocation = null;

            if (_layoutEditor.IsEditMode)
            {
                ShowEditModeNavigationBlockedStatus();
                return;
            }

            if (!_navigationService.CanGoBack)
            {
                _logger.LogWarning("Cannot go back - navigation stack is empty");
                return;
            }

            try
            {
                _logger.LogInfo("=== AnimateZoomOut START (Viewport) ===");

                // Backing out of a zoomed view always dismisses any open content popup (subwindow,
                // thumbnail browser, didactic window) — including the one auto-opened from a
                // single-location unzoomed click. Otherwise it lingers over the full map.
                CloseActiveSubwindow();

                var animationLayout = TryLoadFullMapManualLayoutForAnimation();

                _extensionLineRenderer.Clear();
                _logger.LogInfo("  Cleared radial extension lines");

                var startViewport = MapDisplay.CurrentViewport;
                if (startViewport == null)
                {
                    _logger.LogError("Current viewport is null");
                    return;
                }

                _logger.LogInfo($"  Current viewport: ({startViewport.ViewportX:F2}, {startViewport.ViewportY:F2}) {startViewport.ViewportWidth:F2}x{startViewport.ViewportHeight:F2}, zoom={startViewport.ZoomLevel:F2}");

                // By design, zoom-out always returns to the full-map view: the navigation stack is a
                // depth gate (CanGoBack), not a viewport history, so previousState's payload is unused.
                var previousState = _navigationService.PopState();
                if (previousState == null)
                {
                    _logger.LogWarning("Previous state is null");
                    return;
                }

                _logger.LogInfo("  Previous state popped from navigation stack");

                _mode = InteractionMode.Animating;

                var targetViewport = ViewportState.CreateFullMapView(
                    ImageWidth,
                    ImageHeight,
                    MapDisplay.ActualWidth,
                    MapDisplay.ActualHeight);

                _logger.LogInfo($"  Target viewport: ({targetViewport.ViewportX:F2}, {targetViewport.ViewportY:F2}) {targetViewport.ViewportWidth:F2}x{targetViewport.ViewportHeight:F2}");

                AnimateViewportTransition(
                    startViewport,
                    targetViewport,
                    "Zoom-out animation",
                    () =>
                    {
                        _currentZoomedCluster = null;
                        ClearFullMapLayoutSession();
                        ShowClusterView();

                        if (!_navigationService.CanGoBack)
                        {
                            _logger.LogInfo("  Hiding Back button (at root level)");
                            BackButton.Visibility = Visibility.Collapsed;
                        }

                        _overrideStore.ClearAll();
                        EditModePanel.Visibility = Visibility.Collapsed;
                        OverridePendingIndicator.Visibility = Visibility.Collapsed;
                        UpdateEditLayoutButtonVisibility();
                    },
                    () => ApplyManualLayoutDuringAnimation(animationLayout));
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error zooming out: {ex.Message}\n{ex.StackTrace}");
            }
        }

        private ManualLayout? TryLoadFullMapManualLayoutForAnimation()
        {
            if (CanUseCompositePins())
                return null;

            // Honor a session unload: do not replay the saved layout through the zoom animation.
            if (_layoutEditor.IsManualLayoutSuppressed)
                return null;

            // Do not claim the key speculatively: this runs at the start of every zoom-in and
            // zoom-out, so writing "fullmap" here leaves a cluster edit session pointed at the
            // full-map layout whenever ShowZoomedView's cluster-key branch does not run.
            // ApplyManualLayoutDuringAnimation sets the key if the layout is actually applied.
            var key = GenerateCurrentFullMapGroupKey();

            var layout = _layoutEditor.TryLoad(key);
            if (layout != null)
                _logger.LogInfo($"  Loaded full-map manual layout for zoom animation: {key}");

            return layout;
        }

        private void ApplyManualLayoutDuringAnimation(ManualLayout? layout)
        {
            if (layout == null)
                return;

            // Resolved per call, not stored. This runs on the zoom-animation path, so it must stay
            // a cheap string choice — no key derivation, no hashing.
            var animationKey = string.IsNullOrWhiteSpace(layout.GroupKey)
                ? GenerateCurrentFullMapGroupKey()
                : layout.GroupKey;

            ApplyManualLayout(layout, animationKey);
            _layoutEditor.SetManualLayoutActive(true);
        }

        /// <summary>
        /// Runs the shared viewport-interpolation animation loop. Displays pre-rendered keyframes,
        /// updates the viewport and marker positions each frame, and calls
        /// <paramref name="onAnimationComplete"/> exactly once when progress reaches 1.0.
        /// Callers must switch to <see cref="InteractionMode.Animating"/> before calling this method.
        /// </summary>
        private void AnimateViewportTransition(
            ViewportState startViewport,
            ViewportState targetViewport,
            string animationLabel,
            Action onAnimationComplete,
            Action? onFrameUpdated = null)
        {
            const int keyframeCount = 30;
            var prerenderedFrames = PreRenderKeyframes(startViewport, targetViewport, keyframeCount, out var keyframeProgress);

            // Display first frame immediately to avoid visible delay before the loop starts
            MapDisplay.DisplayImage.Source = prerenderedFrames[0];
            MapDisplay.SetCurrentViewport(startViewport);
            UpdateMarkerPositions();
            onFrameUpdated?.Invoke();

            // Stopwatch (monotonic, sub-ms) instead of DateTime.Now (~15.6 ms resolution), which
            // quantized progress at a 16 ms frame budget and caused visible stutter.
            var animClock = System.Diagnostics.Stopwatch.StartNew();
            var frameCount = 0;
            var lastFrameMs = 0.0;

            EventHandler? renderHandler = null;
            renderHandler = (s, e) =>
            {
                frameCount++;
                var elapsed = animClock.Elapsed.TotalMilliseconds;
                var frameDelta = elapsed - lastFrameMs;
                lastFrameMs = elapsed;

                var progress = Math.Min(1.0, elapsed / AnimationDurationMs);

                // keyframeProgress is linear/monotonic (i / (count-1)), so the nearest keyframe is a
                // direct index — no per-frame search needed.
                int frameIndex = Math.Min(keyframeCount - 1, (int)Math.Round(progress * (keyframeCount - 1)));

                MapDisplay.DisplayImage.Source = prerenderedFrames[frameIndex];

                var currentViewport = _viewportCalculator.Interpolate(startViewport, targetViewport, keyframeProgress[frameIndex]);
                MapDisplay.SetCurrentViewport(currentViewport);
                UpdateMarkerPositions();
                onFrameUpdated?.Invoke();

                if (frameCount <= 3 || frameCount % 3 == 0)
                {
                    var centerX = currentViewport.ViewportX + (currentViewport.ViewportWidth / 2.0);
                    var centerY = currentViewport.ViewportY + (currentViewport.ViewportHeight / 2.0);
                    _logger.LogInfo($"  [FRAME {frameCount}] +{elapsed:F0}ms, delta={frameDelta:F1}ms, progress={progress:F3}, keyframe={frameIndex}, center=({centerX:F1},{centerY:F1}), zoom={currentViewport.ZoomLevel:F2}");
                }

                if (progress >= 1.0)
                {
                    CompositionTarget.Rendering -= renderHandler;
                    _mode = InteractionMode.Normal;
                    _logger.LogInfo($"  [FRAMES TOTAL] {frameCount} frames in {elapsed:F0}ms");
                    _logger.LogInfo($"=== {animationLabel} COMPLETED (Viewport) ===");

                    MapDisplay.UpdateViewport(targetViewport);
                    UpdateMarkerPositions();
                    onFrameUpdated?.Invoke();

                    onAnimationComplete();
                }
            };

            CompositionTarget.Rendering += renderHandler;
            _logger.LogInfo($"=== {animationLabel} STARTED (Viewport) ===");
        }
        /// <summary>
        /// Pre-renders animation keyframes with caching support.
        /// </summary>
        private WriteableBitmap[] PreRenderKeyframes(ViewportState startViewport, ViewportState targetViewport,
                                                      int keyframeCount, out double[] keyframeProgress)
        {
            var prerenderedFrames = new WriteableBitmap[keyframeCount];
            keyframeProgress = new double[keyframeCount];

            _logger.LogInfo($"  Pre-rendering {keyframeCount} keyframes...");
            var sourceImage = MapDisplay.SourceImage;

            if (sourceImage == null)
            {
                _logger.LogError("Source image is null, cannot pre-render");
                return prerenderedFrames;
            }

            int displayWidth = (int)MapDisplay.ActualWidth;
            int displayHeight = (int)MapDisplay.ActualHeight;
            int cachedCount = 0;

            for (int i = 0; i < keyframeCount; i++)
            {
                // Linear interpolation - no easing for smooth consistent motion
                keyframeProgress[i] = i / (double)(keyframeCount - 1);

                var viewport = _viewportCalculator.Interpolate(startViewport, targetViewport, keyframeProgress[i]);
                var sourceRect = viewport.GetSourceRect();

                // Try to load from cache first
                var cachedFrame = _frameCache.TryLoadFrame(
                    startViewport.ViewportX, startViewport.ViewportY, startViewport.ViewportWidth, startViewport.ViewportHeight,
                    targetViewport.ViewportX, targetViewport.ViewportY, targetViewport.ViewportWidth, targetViewport.ViewportHeight,
                    displayWidth, displayHeight, i);

                if (cachedFrame != null)
                {
                    prerenderedFrames[i] = new WriteableBitmap(cachedFrame);
                    prerenderedFrames[i].Freeze();
                    cachedCount++;
                }
                else
                {
                    // Create pre-rendered bitmap
                    var croppedBitmap = new CroppedBitmap(sourceImage, sourceRect);
                    var scaledBitmap = new TransformedBitmap(croppedBitmap,
                        new ScaleTransform(displayWidth / (double)sourceRect.Width, displayHeight / (double)sourceRect.Height));
                    RenderOptions.SetBitmapScalingMode(scaledBitmap, BitmapScalingMode.Linear);

                    prerenderedFrames[i] = new WriteableBitmap(scaledBitmap);
                    prerenderedFrames[i].Freeze();

                    // Save to cache for next time
                    _frameCache.SaveFrame(prerenderedFrames[i],
                        startViewport.ViewportX, startViewport.ViewportY, startViewport.ViewportWidth, startViewport.ViewportHeight,
                        targetViewport.ViewportX, targetViewport.ViewportY, targetViewport.ViewportWidth, targetViewport.ViewportHeight,
                        displayWidth, displayHeight, i);
                }
            }
            _logger.LogInfo($"  Pre-rendering complete ({cachedCount}/{keyframeCount} from cache)");

            return prerenderedFrames;
        }

    }
}
