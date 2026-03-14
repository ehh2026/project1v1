using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using InteractiveWorldMap.Models;
using InteractiveWorldMap.Services;
using InteractiveWorldMap.Views;

namespace InteractiveWorldMap
{
    /// <summary>
    /// Main window that hosts all UI components and manages application lifecycle.
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly ContentLoader _contentLoader;
        private readonly ILogger _logger;
        private readonly MapNavigationService _navigationService;
        private ContentSubwindow? _activeSubwindow;
        private List<LocationCluster> _clusters = new List<LocationCluster>();
        
        // Collections to track markers
        private readonly List<LocationMarker> _individualMarkers = new List<LocationMarker>();
        private readonly List<ClusterMarker> _clusterMarkers = new List<ClusterMarker>();
        
        // Map image dimensions
        private const double ImageWidth = 16397.0;
        private const double ImageHeight = 11085.0;
        
        // Zoom configuration
        private const double ZoomScale = 3.5; // 3.5x magnification when zoomed
        private const int AnimationDurationMs = 400;

        /// <summary>
        /// Gets the active content subwindow, if any.
        /// </summary>
        public ContentSubwindow? ActiveSubwindow => _activeSubwindow;

        public MainWindow()
        {
            try
            {
                InitializeComponent();

                // Initialize services
                _logger = new FileLogger();
                _logger.LogInfo("=== MainWindow Constructor Started ===");
                
                _contentLoader = new ContentLoader(_logger);
                _logger.LogInfo("ContentLoader created");
                
                _navigationService = new MapNavigationService();
                _logger.LogInfo("MapNavigationService created");

                // Wire up events
                Loaded += OnWindowLoaded;
                _logger.LogInfo("Loaded event wired");
                
                KeyDown += OnKeyDown;
                _logger.LogInfo("KeyDown event wired");
                
                PreviewMouseLeftButtonDown += OnPreviewMouseLeftButtonDown;
                _logger.LogInfo("PreviewMouseLeftButtonDown event wired");
                
                SizeChanged += OnSizeChanged;
                _logger.LogInfo("SizeChanged event wired");
                
                _logger.LogInfo("=== MainWindow Constructor Completed ===");
            }
            catch (Exception ex)
            {
                var logger = new FileLogger();
                logger.LogError($"FATAL ERROR in MainWindow constructor: {ex.Message}\n{ex.StackTrace}");
                throw;
            }
        }

        /// <summary>
        /// Initializes the application by loading map and locations.
        /// </summary>
        public async Task InitializeAsync()
        {
            try
            {
                _logger.LogInfo("=== InitializeAsync Started ===");
                _logger.LogInfo("Initializing Interactive World Map application");
                _logger.LogInfo($"Base directory: {AppDomain.CurrentDomain.BaseDirectory}");
                _logger.LogInfo($"Content folder path: {_contentLoader.ContentFolderPath}");

                // Validate content folder
                _logger.LogInfo("Step 1: Validating content folder");
                if (!_contentLoader.ValidateContentFolder())
                {
                    var errorMsg = $"Content folder validation failed.\nExpected path: {_contentLoader.ContentFolderPath}\nPlease ensure the Images&Content folder exists with the required files.";
                    _logger.LogError(errorMsg);
                    ShowError(errorMsg);
                    return;
                }
                _logger.LogInfo("Content folder validation passed");

                // Load map image
                _logger.LogInfo("Step 2: Loading world map image");
                var mapImage = await _contentLoader.LoadMapImageAsync();
                _logger.LogInfo("Map image loaded, calling MapDisplay.LoadMapImage");
                
                MapDisplay.LoadMapImage(mapImage);
                _logger.LogInfo("MapDisplay.LoadMapImage completed");

                // Wait for layout to complete
                _logger.LogInfo("Step 3: Waiting for layout");
                await Task.Delay(100);

                // Load and cluster locations
                _logger.LogInfo("Step 4: Loading and clustering location data");
                _clusters = await _contentLoader.LoadClustersAsync();
                _logger.LogInfo($"Loaded {_clusters.Count} clusters");
                
                if (_clusters.Any())
                {
                    _logger.LogInfo("Step 5: Adding cluster markers to map");
                    AddClustersToMap(_clusters);
                    _logger.LogInfo($"Added markers for {_clusters.Count} clusters");
                }
                else
                {
                    _logger.LogWarning("No clusters found to display");
                }

                _logger.LogInfo("=== Application initialization complete ===");
            }
            catch (Exception ex)
            {
                var errorMsg = $"Failed to initialize application: {ex.Message}\n\nStack trace:\n{ex.StackTrace}";
                _logger.LogError(errorMsg);
                ShowError(errorMsg);
            }
        }

        /// <summary>
        /// Shows content for the specified location in a subwindow.
        /// </summary>
        public async void ShowContentForLocation(Location location)
        {
            try
            {
                _logger.LogInfo($"Opening content for location: {location.Name}");

                // Close existing subwindow if any and wait for it to complete
                if (_activeSubwindow != null)
                {
                    await CloseActiveSubwindowAsync();
                }

                // Load content
                object content;
                var imageContent = await _contentLoader.LoadLocationContentAsync(location);
                
                if (imageContent == null)
                {
                    // Show text message if content not available
                    content = $"Content not available for {location.Name}";
                }
                else
                {
                    content = imageContent;
                }

                // Create and show subwindow
                _activeSubwindow = new ContentSubwindow
                {
                    AssociatedLocation = location,
                    Owner = this
                };

                var markerPosition = MapDisplay.GetMapPosition(location.PixelX, location.PixelY, 16397, 11085);
                _activeSubwindow.ShowContent(content, location.Name, markerPosition);

                _logger.LogInfo($"Content subwindow opened for: {location.Name}");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to show content for location {location.Name}: {ex.Message}");
            }
        }

        /// <summary>
        /// Closes the active content subwindow.
        /// </summary>
        public void CloseActiveSubwindow()
        {
            if (_activeSubwindow != null)
            {
                _logger.LogInfo("Closing active subwindow");
                var windowToClose = _activeSubwindow;
                _activeSubwindow = null;
                
                windowToClose.AnimateClose(() =>
                {
                    Focus(); // Return focus to main window
                });
            }
        }

        /// <summary>
        /// Closes the active content subwindow asynchronously and waits for completion.
        /// </summary>
        private Task CloseActiveSubwindowAsync()
        {
            if (_activeSubwindow == null)
                return Task.CompletedTask;

            _logger.LogInfo("Closing active subwindow (async)");
            
            var tcs = new TaskCompletionSource<bool>();
            var windowToClose = _activeSubwindow;
            _activeSubwindow = null;

            windowToClose.AnimateClose(() =>
            {
                Focus(); // Return focus to main window
                tcs.SetResult(true);
            });

            return tcs.Task;
        }

        /// <summary>
        /// Adds clusters to the map canvas.
        /// </summary>
        private void AddClustersToMap(List<LocationCluster> clusters)
        {
            _logger.LogInfo($"[AddClustersToMap] Adding {clusters.Count} clusters");
            
            var mapBounds = MapDisplay.MapBounds;
            var canvas = MapDisplay.Markers;
            
            // Set canvas size to match map bounds (it's centered, so no need to set position)
            canvas.Width = mapBounds.Width;
            canvas.Height = mapBounds.Height;
            
            _logger.LogInfo($"  Canvas size: {mapBounds.Width:F2} x {mapBounds.Height:F2}");
            _logger.LogInfo($"  Map bounds: {mapBounds}");
            
            foreach (var cluster in clusters)
            {
                if (cluster.IsSingleLocation)
                {
                    // Add individual marker
                    AddIndividualMarker(cluster.Locations[0], mapBounds);
                }
                else
                {
                    // Add cluster marker
                    AddClusterMarker(cluster, mapBounds);
                }
            }
            
            _logger.LogInfo($"[AddClustersToMap] Complete - {_individualMarkers.Count} individual, {_clusterMarkers.Count} cluster markers");
            
            // Log initial screen positions after layout
            Dispatcher.BeginInvoke(new Action(() =>
            {
                _logger.LogInfo("  Initial screen positions:");
                foreach (var marker in _clusterMarkers.Where(m => m.Visibility == Visibility.Visible))
                {
                    try
                    {
                        var canvasPos = new Point(Canvas.GetLeft(marker) + marker.Width / 2, Canvas.GetTop(marker) + marker.Height / 2);
                        var screenPos = marker.TransformToAncestor(this).Transform(new Point(marker.Width / 2, marker.Height / 2));
                        _logger.LogInfo($"    Cluster ({marker.Cluster.Count}): canvas({canvasPos.X:F2}, {canvasPos.Y:F2}) -> screen({screenPos.X:F2}, {screenPos.Y:F2})");
                    }
                    catch { }
                }
                foreach (var marker in _individualMarkers.Where(m => m.Visibility == Visibility.Visible))
                {
                    try
                    {
                        var canvasPos = new Point(Canvas.GetLeft(marker) + marker.Width / 2, Canvas.GetTop(marker) + marker.Height / 2);
                        var screenPos = marker.TransformToAncestor(this).Transform(new Point(marker.Width / 2, marker.Height / 2));
                        _logger.LogInfo($"    Individual '{marker.Location.Name}': canvas({canvasPos.X:F2}, {canvasPos.Y:F2}) -> screen({screenPos.X:F2}, {screenPos.Y:F2})");
                    }
                    catch { }
                }
            }), System.Windows.Threading.DispatcherPriority.Loaded);
        }

        /// <summary>
        /// Adds an individual location marker to the canvas.
        /// </summary>
        private void AddIndividualMarker(Location location, Rect mapBounds)
        {
            var marker = new LocationMarker { Location = location };
            
            // Calculate position in canvas coordinates (0 to canvas width/height)
            var normalizedX = location.PixelX / ImageWidth;
            var normalizedY = location.PixelY / ImageHeight;
            
            var x = normalizedX * mapBounds.Width;
            var y = normalizedY * mapBounds.Height;
            
            // Position marker (centered on point)
            Canvas.SetLeft(marker, x - marker.Width / 2);
            Canvas.SetTop(marker, y - marker.Height / 2);
            
            // Add click handler
            marker.MouseLeftButtonDown += (s, e) =>
            {
                marker.AnimateClick();
                ShowContentForLocation(location);
                e.Handled = true;
            };
            
            _individualMarkers.Add(marker);
            MapDisplay.Markers.Children.Add(marker);
            
            // Log coordinates
            _logger.LogInfo($"  Individual marker '{location.Name}':");
            _logger.LogInfo($"    Image coords: ({location.PixelX}, {location.PixelY})");
            _logger.LogInfo($"    Normalized: ({normalizedX:F4}, {normalizedY:F4})");
            _logger.LogInfo($"    Canvas pos: ({x:F2}, {y:F2})");
        }

        /// <summary>
        /// Adds a cluster marker to the canvas.
        /// </summary>
        private void AddClusterMarker(LocationCluster cluster, Rect mapBounds)
        {
            var marker = new ClusterMarker { Cluster = cluster };
            marker.UpdateDisplay();
            
            // Calculate position in canvas coordinates (0 to canvas width/height)
            var normalizedX = cluster.CenterPoint.X / ImageWidth;
            var normalizedY = cluster.CenterPoint.Y / ImageHeight;
            
            var x = normalizedX * mapBounds.Width;
            var y = normalizedY * mapBounds.Height;
            
            // Position marker (centered on point)
            Canvas.SetLeft(marker, x - marker.Width / 2);
            Canvas.SetTop(marker, y - marker.Height / 2);
            
            // Add click handler
            marker.MouseLeftButtonDown += (s, e) =>
            {
                marker.AnimateClick();
                OnClusterClicked(cluster);
                e.Handled = true;
            };
            
            _clusterMarkers.Add(marker);
            MapDisplay.Markers.Children.Add(marker);
            
            // Log coordinates
            _logger.LogInfo($"  Cluster marker ({cluster.Count} locations):");
            _logger.LogInfo($"    Image coords: ({cluster.CenterPoint.X:F2}, {cluster.CenterPoint.Y:F2})");
            _logger.LogInfo($"    Normalized: ({normalizedX:F4}, {normalizedY:F4})");
            _logger.LogInfo($"    Canvas pos: ({x:F2}, {y:F2})");
        }

        /// <summary>
        /// Clears all markers from the canvas.
        /// </summary>
        private void ClearAllMarkers()
        {
            _logger.LogInfo($"[ClearAllMarkers] Clearing {_individualMarkers.Count} individual and {_clusterMarkers.Count} cluster markers");
            
            foreach (var marker in _individualMarkers)
            {
                MapDisplay.Markers.Children.Remove(marker);
            }
            _individualMarkers.Clear();
            
            foreach (var marker in _clusterMarkers)
            {
                MapDisplay.Markers.Children.Remove(marker);
            }
            _clusterMarkers.Clear();
        }

        /// <summary>
        /// Shows only cluster markers (hides individual markers).
        /// </summary>
        private void ShowOnlyClusterMarkers()
        {
            _logger.LogInfo("[ShowOnlyClusterMarkers]");
            
            // Show all individual markers that are single-location clusters
            foreach (var marker in _individualMarkers)
            {
                // Check if this individual marker is from a single-location cluster
                var isSingleCluster = _clusters.Any(c => c.IsSingleLocation && c.Locations[0] == marker.Location);
                marker.Visibility = isSingleCluster ? Visibility.Visible : Visibility.Collapsed;
            }
            
            // Show all cluster markers
            foreach (var marker in _clusterMarkers)
            {
                marker.Visibility = Visibility.Visible;
            }
        }

        /// <summary>
        /// Counter-scales all markers to maintain their visual size when the map is zoomed.
        /// </summary>
        /// <param name="scale">The scale factor to apply (1/zoomScale to counter the zoom)</param>
        private void CounterScaleMarkers(double scale)
        {
            _logger.LogInfo($"[CounterScaleMarkers] Applying scale: {scale:F2}");
            
            var scaleTransform = new ScaleTransform(scale, scale);
            
            foreach (var marker in _individualMarkers)
            {
                marker.RenderTransform = scaleTransform;
                marker.RenderTransformOrigin = new Point(0.5, 0.5);
            }
            
            foreach (var marker in _clusterMarkers)
            {
                marker.RenderTransform = scaleTransform;
                marker.RenderTransformOrigin = new Point(0.5, 0.5);
            }
        }

        /// <summary>
        /// Shows only individual markers for a specific cluster (hides cluster markers).
        /// </summary>
        private void ShowOnlyIndividualMarkers(LocationCluster cluster)
        {
            _logger.LogInfo($"[ShowOnlyIndividualMarkers] Showing markers for cluster with {cluster.Count} locations");
            
            // Hide all cluster markers
            foreach (var marker in _clusterMarkers)
            {
                marker.Visibility = Visibility.Collapsed;
            }
            
            var mapBounds = MapDisplay.MapBounds;
            
            // For each location in the cluster, ensure we have an individual marker
            foreach (var location in cluster.Locations)
            {
                // Check if marker already exists
                var existingMarker = _individualMarkers.FirstOrDefault(m => m.Location == location);
                
                if (existingMarker != null)
                {
                    // Show existing marker
                    existingMarker.Visibility = Visibility.Visible;
                    _logger.LogInfo($"  Showing existing marker: {location.Name}");
                }
                else
                {
                    // Create new individual marker for this location
                    _logger.LogInfo($"  Creating new marker for: {location.Name}");
                    AddIndividualMarker(location, mapBounds);
                }
            }
            
            // Hide all other individual markers not in this cluster
            foreach (var marker in _individualMarkers)
            {
                if (!cluster.Locations.Contains(marker.Location))
                {
                    marker.Visibility = Visibility.Collapsed;
                }
            }
        }

        /// <summary>
        /// Handles clicks outside the subwindow.
        /// </summary>
        public void HandleOutsideClick(Point clickPosition)
        {
            if (_activeSubwindow != null)
            {
                var screenPoint = PointToScreen(clickPosition);
                if (!_activeSubwindow.ContainsPoint(screenPoint))
                {
                    CloseActiveSubwindow();
                }
            }
        }

        private void UpdateMarkerLayerBounds()
        {
            // No longer needed - markers are in the map canvas
        }

        private async void OnWindowLoaded(object sender, RoutedEventArgs e)
        {
            await InitializeAsync();
        }

        private void OnClusterClicked(LocationCluster cluster)
        {
            _logger.LogInfo($"Cluster clicked: {cluster.Count} locations");
            AnimateZoomToCluster(cluster);
            
            // Show Back button
            BackButton.Visibility = Visibility.Visible;
        }

        private void OnMapMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // Handle clicks on the map (for closing subwindow)
            if (_activeSubwindow != null)
            {
                var position = e.GetPosition(this);
                HandleOutsideClick(position);
            }
        }

        private void OnMapMouseMove(object sender, MouseEventArgs e)
        {
            // Can be used for hover effects in the future
        }

        /// <summary>
        /// Animates zooming into a cluster.
        /// </summary>
        private void AnimateZoomToCluster(LocationCluster cluster)
        {
            try
            {
                _logger.LogInfo("=== AnimateZoomToCluster START ===");
                _logger.LogInfo($"  Cluster: {cluster.Count} locations");
                _logger.LogInfo($"  Cluster center: ({cluster.CenterPoint.X:F2}, {cluster.CenterPoint.Y:F2})");

                // Save current state before zooming
                var currentState = ZoomState.CreateFullMapView();
                _navigationService.PushState(currentState);
                _logger.LogInfo("  Current state saved to navigation stack");

                // Calculate the center point in screen coordinates
                var mapBounds = MapDisplay.MapBounds;
                var imageWidth = 16397.0;
                var imageHeight = 11085.0;

                _logger.LogInfo($"  Map bounds: {mapBounds}");
                _logger.LogInfo($"  Image size: {imageWidth} x {imageHeight}");

                var normalizedX = cluster.CenterPoint.X / imageWidth;
                var normalizedY = cluster.CenterPoint.Y / imageHeight;

                _logger.LogInfo($"  Normalized center: ({normalizedX:F4}, {normalizedY:F4})");

                var centerX = mapBounds.Left + (normalizedX * mapBounds.Width);
                var centerY = mapBounds.Top + (normalizedY * mapBounds.Height);

                _logger.LogInfo($"  Screen center: ({centerX:F2}, {centerY:F2})");

                // Calculate the center of the screen
                var screenCenterX = ActualWidth / 2;
                var screenCenterY = ActualHeight / 2;

                _logger.LogInfo($"  Window center: ({screenCenterX:F2}, {screenCenterY:F2})");

                // Calculate translation needed to center the cluster
                var translateX = screenCenterX - (centerX * ZoomScale);
                var translateY = screenCenterY - (centerY * ZoomScale);

                _logger.LogInfo($"  Calculated translation: ({translateX:F2}, {translateY:F2})");
                _logger.LogInfo($"  Zoom scale: {ZoomScale}");

                // Create animations with smoother easing
                var duration = new Duration(TimeSpan.FromMilliseconds(AnimationDurationMs));
                var easing = new CubicEase { EasingMode = EasingMode.EaseInOut };

                var scaleXAnim = new DoubleAnimation(ZoomScale, duration) 
                { 
                    EasingFunction = easing,
                    FillBehavior = FillBehavior.HoldEnd
                };
                var scaleYAnim = new DoubleAnimation(ZoomScale, duration) 
                { 
                    EasingFunction = easing,
                    FillBehavior = FillBehavior.HoldEnd
                };
                var translateXAnim = new DoubleAnimation(translateX, duration) 
                { 
                    EasingFunction = easing,
                    FillBehavior = FillBehavior.HoldEnd
                };
                var translateYAnim = new DoubleAnimation(translateY, duration) 
                { 
                    EasingFunction = easing,
                    FillBehavior = FillBehavior.HoldEnd
                };

                // Handle animation completion
                scaleXAnim.Completed += (s, e) =>
                {
                    _logger.LogInfo("=== Zoom animation COMPLETED ===");
                    _logger.LogInfo($"  Final scale: ({MapDisplay.ScaleTransform.ScaleX:F2}, {MapDisplay.ScaleTransform.ScaleY:F2})");
                    _logger.LogInfo($"  Final translate: ({MapDisplay.TranslateTransform.X:F2}, {MapDisplay.TranslateTransform.Y:F2})");
                    
                    // Stop all animations and set final values explicitly
                    MapDisplay.ScaleTransform.BeginAnimation(ScaleTransform.ScaleXProperty, null);
                    MapDisplay.ScaleTransform.BeginAnimation(ScaleTransform.ScaleYProperty, null);
                    MapDisplay.TranslateTransform.BeginAnimation(TranslateTransform.XProperty, null);
                    MapDisplay.TranslateTransform.BeginAnimation(TranslateTransform.YProperty, null);
                    
                    MapDisplay.ScaleTransform.ScaleX = ZoomScale;
                    MapDisplay.ScaleTransform.ScaleY = ZoomScale;
                    MapDisplay.TranslateTransform.X = translateX;
                    MapDisplay.TranslateTransform.Y = translateY;
                    
                    _logger.LogInfo($"  Values set to: scale({MapDisplay.ScaleTransform.ScaleX:F2}, {MapDisplay.ScaleTransform.ScaleY:F2}), translate({MapDisplay.TranslateTransform.X:F2}, {MapDisplay.TranslateTransform.Y:F2})");
                    
                    // Counter-scale all markers to keep them the same visual size
                    CounterScaleMarkers(1.0 / ZoomScale);
                    
                    ShowZoomedView(cluster);
                };

                // Apply animations
                MapDisplay.ScaleTransform.BeginAnimation(ScaleTransform.ScaleXProperty, scaleXAnim);
                MapDisplay.ScaleTransform.BeginAnimation(ScaleTransform.ScaleYProperty, scaleYAnim);
                MapDisplay.TranslateTransform.BeginAnimation(TranslateTransform.XProperty, translateXAnim);
                MapDisplay.TranslateTransform.BeginAnimation(TranslateTransform.YProperty, translateYAnim);

                _logger.LogInfo("=== Zoom animations STARTED ===");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error zooming to cluster: {ex.Message}\n{ex.StackTrace}");
            }
        }

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

                _logger.LogInfo($"=== ShowClusterView COMPLETE ===");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error showing cluster view: {ex.Message}\n{ex.StackTrace}");
            }
        }

        /// <summary>
        /// Shows individual markers for the zoomed cluster.
        /// </summary>
        private void ShowZoomedView(LocationCluster cluster)
        {
            try
            {
                _logger.LogInfo("=== ShowZoomedView START ===");
                _logger.LogInfo($"  Cluster has {cluster.Count} locations");
                _logger.LogInfo($"  Current transform: scale({MapDisplay.ScaleTransform.ScaleX:F2}, {MapDisplay.ScaleTransform.ScaleY:F2}), translate({MapDisplay.TranslateTransform.X:F2}, {MapDisplay.TranslateTransform.Y:F2})");

                // Show only individual markers for this cluster
                ShowOnlyIndividualMarkers(cluster);
                
                // Log screen coordinates of visible markers
                _logger.LogInfo("  Visible marker screen positions:");
                foreach (var marker in _individualMarkers.Where(m => m.Visibility == Visibility.Visible))
                {
                    try
                    {
                        var canvasPos = new Point(Canvas.GetLeft(marker) + marker.Width / 2, Canvas.GetTop(marker) + marker.Height / 2);
                        var screenPos = marker.TransformToAncestor(this).Transform(new Point(marker.Width / 2, marker.Height / 2));
                        _logger.LogInfo($"    {marker.Location.Name}: canvas({canvasPos.X:F2}, {canvasPos.Y:F2}) -> screen({screenPos.X:F2}, {screenPos.Y:F2})");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning($"    {marker.Location.Name}: Could not calculate screen position - {ex.Message}");
                    }
                }

                _logger.LogInfo($"=== ShowZoomedView COMPLETE ===");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error showing zoomed view: {ex.Message}\n{ex.StackTrace}");
            }
        }

        private void OnKeyDown(object sender, KeyEventArgs e)
        {
            // Handle Escape key to close subwindow, go back, or exit application
            if (e.Key == Key.Escape)
            {
                if (_activeSubwindow != null)
                {
                    CloseActiveSubwindow();
                }
                else if (_navigationService.CanGoBack)
                {
                    AnimateZoomOut();
                }
                else
                {
                    _logger.LogInfo("Application closing via Escape key");
                    Close();
                }
            }
        }

        private void OnBackButtonClick(object sender, RoutedEventArgs e)
        {
            AnimateZoomOut();
        }

        /// <summary>
        /// Animates zooming out to the full map view.
        /// </summary>
        private void AnimateZoomOut()
        {
            if (!_navigationService.CanGoBack)
            {
                _logger.LogWarning("Cannot go back - navigation stack is empty");
                return;
            }

            try
            {
                _logger.LogInfo("=== AnimateZoomOut START ===");
                _logger.LogInfo($"  Current scale: ({MapDisplay.ScaleTransform.ScaleX:F2}, {MapDisplay.ScaleTransform.ScaleY:F2})");
                _logger.LogInfo($"  Current translate: ({MapDisplay.TranslateTransform.X:F2}, {MapDisplay.TranslateTransform.Y:F2})");

                // Pop the previous state
                var previousState = _navigationService.PopState();
                if (previousState == null)
                {
                    _logger.LogWarning("Previous state is null");
                    return;
                }

                _logger.LogInfo("  Previous state popped from navigation stack");

                // Create animations to return to full map view with smoother easing
                var duration = new Duration(TimeSpan.FromMilliseconds(AnimationDurationMs));
                var easing = new CubicEase { EasingMode = EasingMode.EaseInOut };

                var scaleXAnim = new DoubleAnimation(1.0, duration) 
                { 
                    EasingFunction = easing,
                    FillBehavior = FillBehavior.HoldEnd
                };
                var scaleYAnim = new DoubleAnimation(1.0, duration) 
                { 
                    EasingFunction = easing,
                    FillBehavior = FillBehavior.HoldEnd
                };
                var translateXAnim = new DoubleAnimation(0.0, duration) 
                { 
                    EasingFunction = easing,
                    FillBehavior = FillBehavior.HoldEnd
                };
                var translateYAnim = new DoubleAnimation(0.0, duration) 
                { 
                    EasingFunction = easing,
                    FillBehavior = FillBehavior.HoldEnd
                };

                _logger.LogInfo("  Target: scale(1.0, 1.0), translate(0.0, 0.0)");

                // Handle animation completion
                scaleXAnim.Completed += (s, e) =>
                {
                    _logger.LogInfo("=== Zoom-out animation COMPLETED ===");
                    _logger.LogInfo($"  Final scale: ({MapDisplay.ScaleTransform.ScaleX:F2}, {MapDisplay.ScaleTransform.ScaleY:F2})");
                    _logger.LogInfo($"  Final translate: ({MapDisplay.TranslateTransform.X:F2}, {MapDisplay.TranslateTransform.Y:F2})");
                    
                    // Stop all animations and set final values explicitly
                    MapDisplay.ScaleTransform.BeginAnimation(ScaleTransform.ScaleXProperty, null);
                    MapDisplay.ScaleTransform.BeginAnimation(ScaleTransform.ScaleYProperty, null);
                    MapDisplay.TranslateTransform.BeginAnimation(TranslateTransform.XProperty, null);
                    MapDisplay.TranslateTransform.BeginAnimation(TranslateTransform.YProperty, null);
                    
                    MapDisplay.ScaleTransform.ScaleX = 1.0;
                    MapDisplay.ScaleTransform.ScaleY = 1.0;
                    MapDisplay.TranslateTransform.X = 0.0;
                    MapDisplay.TranslateTransform.Y = 0.0;
                    
                    _logger.LogInfo($"  Values set to: scale({MapDisplay.ScaleTransform.ScaleX:F2}, {MapDisplay.ScaleTransform.ScaleY:F2}), translate({MapDisplay.TranslateTransform.X:F2}, {MapDisplay.TranslateTransform.Y:F2})");
                    
                    // Reset marker scales to normal
                    CounterScaleMarkers(1.0);
                    
                    ShowClusterView();
                    
                    // Hide Back button if at root level
                    if (!_navigationService.CanGoBack)
                    {
                        _logger.LogInfo("  Hiding Back button (at root level)");
                        BackButton.Visibility = Visibility.Collapsed;
                    }
                };

                // Apply animations
                MapDisplay.ScaleTransform.BeginAnimation(ScaleTransform.ScaleXProperty, scaleXAnim);
                MapDisplay.ScaleTransform.BeginAnimation(ScaleTransform.ScaleYProperty, scaleYAnim);
                MapDisplay.TranslateTransform.BeginAnimation(TranslateTransform.XProperty, translateXAnim);
                MapDisplay.TranslateTransform.BeginAnimation(TranslateTransform.YProperty, translateYAnim);

                _logger.LogInfo("=== Zoom-out animations STARTED ===");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error zooming out: {ex.Message}\n{ex.StackTrace}");
            }
        }

        private void OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // Only handle if there's an active subwindow
            if (_activeSubwindow == null)
                return;

            // Get the click position relative to the window
            var position = e.GetPosition(this);
            var screenPoint = PointToScreen(position);

            // Check if click is outside the subwindow
            if (!_activeSubwindow.ContainsPoint(screenPoint))
            {
                // Check if the click was on a marker (which will open a new subwindow)
                var markerPosition = e.GetPosition(MapDisplay.Markers);
                var hitResult = VisualTreeHelper.HitTest(MapDisplay.Markers, markerPosition);
                
                // Only close if not clicking on any marker
                if (hitResult == null)
                {
                    CloseActiveSubwindow();
                    e.Handled = true; // Prevent further processing
                }
            }
        }

        private void OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            // No longer needed - markers transform automatically with map
        }

        private void ShowError(string message)
        {
            MessageBox.Show(message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
