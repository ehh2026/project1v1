using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
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
                MarkerLayer.MarkerClicked += OnMarkerClicked;
                _logger.LogInfo("MarkerClicked event wired");
                
                MarkerLayer.ClusterClicked += OnClusterClicked;
                _logger.LogInfo("ClusterClicked event wired");
                
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

                // Update marker layer with map bounds
                _logger.LogInfo("Step 4: Updating marker layer bounds");
                UpdateMarkerLayerBounds();
                _logger.LogInfo("Marker layer bounds updated");

                // Load and cluster locations
                _logger.LogInfo("Step 5: Loading and clustering location data");
                _clusters = await _contentLoader.LoadClustersAsync();
                _logger.LogInfo($"Loaded {_clusters.Count} clusters");
                
                if (_clusters.Any())
                {
                    _logger.LogInfo("Step 6: Adding cluster markers");
                    MarkerLayer.AddClusters(_clusters);
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
            var mapBounds = MapDisplay.MapBounds;
            if (!mapBounds.IsEmpty)
            {
                MarkerLayer.UpdateMapBounds(mapBounds);
                _logger.LogInfo($"Updated marker layer bounds: {mapBounds}");
            }
        }

        private async void OnWindowLoaded(object sender, RoutedEventArgs e)
        {
            await InitializeAsync();
        }

        private void OnMarkerClicked(object? sender, LocationClickedEventArgs e)
        {
            _logger.LogInfo($"Individual marker clicked: {e.Location.Name}");
            ShowContentForLocation(e.Location);
        }

        private void OnClusterClicked(object? sender, ClusterClickedEventArgs e)
        {
            _logger.LogInfo($"Cluster clicked: {e.Cluster.Count} locations");
            AnimateZoomToCluster(e.Cluster);
            
            // Show Back button
            BackButton.Visibility = Visibility.Visible;
        }

        /// <summary>
        /// Animates zooming into a cluster.
        /// </summary>
        private void AnimateZoomToCluster(LocationCluster cluster)
        {
            try
            {
                _logger.LogInfo($"Zooming to cluster with {cluster.Count} locations");

                // Save current state before zooming
                var currentState = ZoomState.CreateFullMapView();
                _navigationService.PushState(currentState);

                // Calculate the center point in screen coordinates
                var mapBounds = MapDisplay.MapBounds;
                var imageWidth = 16397.0;
                var imageHeight = 11085.0;

                var normalizedX = cluster.CenterPoint.X / imageWidth;
                var normalizedY = cluster.CenterPoint.Y / imageHeight;

                var centerX = mapBounds.Left + (normalizedX * mapBounds.Width);
                var centerY = mapBounds.Top + (normalizedY * mapBounds.Height);

                // Calculate the center of the screen
                var screenCenterX = ActualWidth / 2;
                var screenCenterY = ActualHeight / 2;

                // Calculate translation needed to center the cluster
                var translateX = screenCenterX - (centerX * ZoomScale);
                var translateY = screenCenterY - (centerY * ZoomScale);

                // Create animations
                var duration = new Duration(TimeSpan.FromMilliseconds(AnimationDurationMs));
                var easing = new QuadraticEase { EasingMode = EasingMode.EaseInOut };

                var scaleXAnim = new DoubleAnimation(ZoomScale, duration) { EasingFunction = easing };
                var scaleYAnim = new DoubleAnimation(ZoomScale, duration) { EasingFunction = easing };
                var translateXAnim = new DoubleAnimation(translateX, duration) { EasingFunction = easing };
                var translateYAnim = new DoubleAnimation(translateY, duration) { EasingFunction = easing };

                // Handle animation completion
                scaleXAnim.Completed += (s, e) =>
                {
                    _logger.LogInfo("Zoom animation completed");
                    ShowZoomedView(cluster);
                };

                // Apply animations
                MapDisplay.ScaleTransform.BeginAnimation(ScaleTransform.ScaleXProperty, scaleXAnim);
                MapDisplay.ScaleTransform.BeginAnimation(ScaleTransform.ScaleYProperty, scaleYAnim);
                MapDisplay.TranslateTransform.BeginAnimation(TranslateTransform.XProperty, translateXAnim);
                MapDisplay.TranslateTransform.BeginAnimation(TranslateTransform.YProperty, translateYAnim);

                _logger.LogInfo($"Zoom animation started: scale={ZoomScale}, translate=({translateX}, {translateY})");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error zooming to cluster: {ex.Message}");
            }
        }

        /// <summary>
        /// Shows cluster markers and hides individual markers (full map view).
        /// </summary>
        private void ShowClusterView()
        {
            try
            {
                _logger.LogInfo("Showing cluster view (full map)");

                // Clear all individual markers
                MarkerLayer.ClearMarkers();

                // Clear existing cluster markers
                MarkerLayer.ClearClusterMarkers();

                // Add cluster markers
                MarkerLayer.AddClusters(_clusters);

                // Update positions
                MarkerLayer.UpdateMarkerPositions();

                _logger.LogInfo($"Cluster view displayed with {_clusters.Count} clusters");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error showing cluster view: {ex.Message}");
            }
        }

        /// <summary>
        /// Shows individual markers for the zoomed cluster and hides cluster markers.
        /// </summary>
        private void ShowZoomedView(LocationCluster cluster)
        {
            try
            {
                _logger.LogInfo($"Showing zoomed view for cluster with {cluster.Count} locations");

                // Hide all cluster markers
                MarkerLayer.ClearClusterMarkers();

                // Clear any existing individual markers
                MarkerLayer.ClearMarkers();

                // Show individual markers for this cluster's locations
                foreach (var location in cluster.Locations)
                {
                    MarkerLayer.AddMarker(location);
                }

                // Update marker positions with current transform
                MarkerLayer.UpdateMarkerPositions();

                _logger.LogInfo($"Zoomed view displayed with {cluster.Count} individual markers");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error showing zoomed view: {ex.Message}");
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
                _logger.LogInfo("Zooming out to full map view");

                // Pop the previous state
                var previousState = _navigationService.PopState();
                if (previousState == null)
                {
                    _logger.LogWarning("Previous state is null");
                    return;
                }

                // Create animations to return to full map view
                var duration = new Duration(TimeSpan.FromMilliseconds(AnimationDurationMs));
                var easing = new QuadraticEase { EasingMode = EasingMode.EaseInOut };

                var scaleXAnim = new DoubleAnimation(1.0, duration) { EasingFunction = easing };
                var scaleYAnim = new DoubleAnimation(1.0, duration) { EasingFunction = easing };
                var translateXAnim = new DoubleAnimation(0.0, duration) { EasingFunction = easing };
                var translateYAnim = new DoubleAnimation(0.0, duration) { EasingFunction = easing };

                // Handle animation completion
                scaleXAnim.Completed += (s, e) =>
                {
                    _logger.LogInfo("Zoom-out animation completed");
                    ShowClusterView();
                    
                    // Hide Back button if at root level
                    if (!_navigationService.CanGoBack)
                    {
                        BackButton.Visibility = Visibility.Collapsed;
                    }
                };

                // Apply animations
                MapDisplay.ScaleTransform.BeginAnimation(ScaleTransform.ScaleXProperty, scaleXAnim);
                MapDisplay.ScaleTransform.BeginAnimation(ScaleTransform.ScaleYProperty, scaleYAnim);
                MapDisplay.TranslateTransform.BeginAnimation(TranslateTransform.XProperty, translateXAnim);
                MapDisplay.TranslateTransform.BeginAnimation(TranslateTransform.YProperty, translateYAnim);

                _logger.LogInfo("Zoom-out animation started");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error zooming out: {ex.Message}");
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
                var markerPosition = e.GetPosition(MarkerLayer);
                var clickedObject = MarkerLayer.HitTest(markerPosition);
                
                // Only close if not clicking on any marker
                if (clickedObject == null)
                {
                    CloseActiveSubwindow();
                    e.Handled = true; // Prevent further processing
                }
            }
        }

        private void OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            // Update marker positions when window is resized
            UpdateMarkerLayerBounds();
        }

        private void ShowError(string message)
        {
            MessageBox.Show(message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
