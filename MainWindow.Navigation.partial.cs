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
                    return;
                }

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

                AnimateViewportTransition(startViewport, targetViewport, "Zoom animation", () =>
                {
                    ShowZoomedView(cluster);
                    BackButton.Visibility = Visibility.Visible;
                });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error zooming to cluster: {ex.Message}\n{ex.StackTrace}");
            }
        }


        /// <summary>
        /// Phase 7: single-location zoom must replay the full-map manual layout (angle/length/assignment)
        /// so the composite stub matches the unzoomed appearance. Cluster layout keys are not used
        /// when a full-map entry exists for that location.
        /// </summary>
        private bool TryApplyFullMapLayoutForZoomedSingle(LocationCluster cluster)
        {
            if (!cluster.IsSingleLocation)
                return false;

            var locationName = cluster.Locations[0].Name;
            var key = GenerateCurrentFullMapGroupKey();
            var layout = _layoutEditor.TryLoad(key);
            if (layout == null)
                return false;

            if (!FullMapLayoutContainsLocation(layout, locationName))
                return false;

            _layoutEditor.SetLayoutKey(key);
            _logger.LogInfo(
                $"[TryApplyFullMapLayoutForZoomedSingle] Replaying full-map layout for '{locationName}' at key={key}");
            ApplyManualLayout(layout);
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
                        if (cluster.IsSingleLocation)
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
                            _layoutEditor.SetLayoutKey(LayoutKeyGenerator.GenerateKey(
                                cluster.Locations,
                                viewport,
                                _visualConfig.RadialExtension));

                            _logger.LogInfo($"  Generated layout key: {_layoutEditor.CurrentLayoutKey}");

                            // Try to load saved layout
                            var savedLayout = _layoutEditor.TryLoad(_layoutEditor.CurrentLayoutKey!);
                            if (savedLayout != null)
                            {
                                _logger.LogInfo($"  Found saved manual layout with {savedLayout.Markers.Count} markers");
                                _savedLayoutToApply = savedLayout; // Store for later application
                            }
                            else
                            {
                                _logger.LogInfo($"  No saved layout found for key: {_layoutEditor.CurrentLayoutKey}");
                            }
                        }
                    }
                    
                    // Load or generate high-quality zoomed region
                    var centerX = cluster.CenterPoint.X;
                    var centerY = cluster.CenterPoint.Y;
                    var displayWidth = (int)MapDisplay.ActualWidth;
                    var displayHeight = (int)MapDisplay.ActualHeight;
                    
                    var cachedRegion = _zoomedRegionCache.TryLoadRegion(centerX, centerY, ZoomScale, displayWidth, displayHeight);
                    
                    if (cachedRegion != null)
                    {
                        _logger.LogInfo("  Loaded high-quality zoomed region from cache");
                        MapDisplay.DisplayImage.Source = cachedRegion;
                    }
                    else
                    {
                        _logger.LogInfo("  Generating high-quality zoomed region...");
                        var sourceRect = viewport.GetSourceRect();
                        var sourceImage = MapDisplay.SourceImage;
                        
                        if (sourceImage != null)
                        {
                            var highQualityRegion = _zoomedRegionCache.GenerateAndCacheRegion(
                                sourceImage, sourceRect, centerX, centerY, ZoomScale, displayWidth, displayHeight);
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

                    // Apply saved cluster manual layout if one was found
                    if (_savedLayoutToApply != null)
                    {
                        ApplyManualLayout(_savedLayoutToApply);
                        _layoutEditor.SetManualLayoutActive(true);
                        _savedLayoutToApply = null; // Clear after applying

                        _logger.LogInfo("Manual layout applied after high-res region loaded");
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
        private void AnimateZoomOut()
        {
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

            var key = GenerateCurrentFullMapGroupKey();
            _layoutEditor.SetLayoutKey(key);

            var layout = _layoutEditor.TryLoad(key);
            if (layout != null)
                _logger.LogInfo($"  Loaded full-map manual layout for zoom-out animation: {key}");

            return layout;
        }

        private void ApplyManualLayoutDuringAnimation(ManualLayout? layout)
        {
            if (layout == null)
                return;

            _layoutEditor.SetLayoutKey(string.IsNullOrWhiteSpace(layout.GroupKey)
                ? GenerateCurrentFullMapGroupKey()
                : layout.GroupKey);
            ApplyManualLayout(layout);
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

            var animStart = DateTime.Now;
            var frameCount = 0;
            var lastFrameTime = animStart;

            EventHandler? renderHandler = null;
            renderHandler = (s, e) =>
            {
                frameCount++;
                var now = DateTime.Now;
                var frameDelta = (now - lastFrameTime).TotalMilliseconds;
                lastFrameTime = now;

                var elapsed = (now - animStart).TotalMilliseconds;
                var progress = Math.Min(1.0, elapsed / AnimationDurationMs);

                // Find the pre-rendered keyframe closest to the current progress
                int frameIndex = 0;
                double minDiff = double.MaxValue;
                for (int i = 0; i < keyframeCount; i++)
                {
                    double diff = Math.Abs(keyframeProgress[i] - progress);
                    if (diff < minDiff) { minDiff = diff; frameIndex = i; }
                }

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
