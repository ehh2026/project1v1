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
using System.Windows.Shapes;
using InteractiveWorldMap.Models;
using InteractiveWorldMap.Services;
using InteractiveWorldMap.Utilities;
using InteractiveWorldMap.Views;
using IOPath = System.IO.Path;

namespace InteractiveWorldMap
{
    /// <summary>
    /// Main window that hosts all UI components and manages application lifecycle.
    /// Uses viewport-based rendering for efficient zoom/pan operations.
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly ContentLoader _contentLoader;
        private readonly ILogger _logger;
        private readonly MapNavigationService _navigationService;
        private readonly ViewportCalculator _viewportCalculator;
        private readonly AnimationFrameCache _frameCache;
        private readonly ZoomedRegionCache _zoomedRegionCache;
        private ContentSubwindow? _activeSubwindow;
        private ThumbnailBrowserWindow? _activeThumbnailBrowser;
        private DidacticTextWindow? _activeDidacticWindow;
        private List<LocationCluster> _clusters = new List<LocationCluster>();
        
        // Collections to track markers
        private readonly List<LocationMarker> _individualMarkers = new List<LocationMarker>();
        private readonly List<ClusterMarker> _clusterMarkers = new List<ClusterMarker>();
        
        // Radial extension support
        private List<DenseMarkerGroup> _denseGroups = new List<DenseMarkerGroup>();
        private List<Line> _extensionLines = new List<Line>();
        private Dictionary<LocationMarker, Line> _markerToLineMap = new Dictionary<LocationMarker, Line>();
        private RadialExtensionCalculator? _extensionCalculator;
        private bool _isAnimating = false; // Track if we're in an animation
        
        // Map image dimensions
        private const double ImageWidth = 8198.0;
        private const double ImageHeight = 5542.0;
        
        // Visual configuration
        private VisualConfig _visualConfig = new VisualConfig();
        
        // Expose config properties for marker access
        public double LocationMarkerSize => _visualConfig.LocationMarkerSize;
        public double ClusterMarkerSize => _visualConfig.ClusterMarkerSize;
        public double ClusterBadgeSize => _visualConfig.ClusterBadgeSize;
        public double ClusterCountFontSize => _visualConfig.ClusterCountFontSize;
        
        // Zoom configuration from config
        private double ZoomScale => _visualConfig.ZoomScale;
        private int AnimationDurationMs => _visualConfig.AnimationDurationMs;

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
                
                // Load visual configuration
                var configPath = IOPath.Combine(AppDomain.CurrentDomain.BaseDirectory, "visual-config.json");
                VisualConfig.EnsureConfigExists(configPath);
                _visualConfig = VisualConfig.Load(configPath);
                _logger.LogInfo($"Visual config loaded from: {configPath}");
                _logger.LogInfo($"  ClusterDistanceThreshold: {_visualConfig.ClusterDistanceThreshold}");
                _logger.LogInfo($"  LocationMarkerSize: {_visualConfig.LocationMarkerSize}");
                _logger.LogInfo($"  ClusterMarkerSize: {_visualConfig.ClusterMarkerSize}");
                _logger.LogInfo($"  ClusterBadgeSize: {_visualConfig.ClusterBadgeSize}");
                _logger.LogInfo($"  ClusterCountFontSize: {_visualConfig.ClusterCountFontSize}");
                _logger.LogInfo($"  ZoomScale: {_visualConfig.ZoomScale}");
                _logger.LogInfo($"  AnimationDurationMs: {_visualConfig.AnimationDurationMs}");
                _logger.LogInfo($"  RadialExtension.Enabled: {_visualConfig.RadialExtension.Enabled}");
                _logger.LogInfo($"  RadialExtension.MinLocationsForExtension: {_visualConfig.RadialExtension.MinLocationsForExtension}");
                _logger.LogInfo($"  RadialExtension.ProximityThresholdPixels: {_visualConfig.RadialExtension.ProximityThresholdPixels}");
                _logger.LogInfo($"  RadialExtension.ExtensionLineLength: {_visualConfig.RadialExtension.ExtensionLineLength}");
                
                Console.WriteLine($"=== Visual Config Loaded ===");
                Console.WriteLine($"Config Path: {configPath}");
                Console.WriteLine($"ClusterDistanceThreshold: {_visualConfig.ClusterDistanceThreshold}");
                Console.WriteLine($"LocationMarkerSize: {_visualConfig.LocationMarkerSize}");
                Console.WriteLine($"ClusterMarkerSize: {_visualConfig.ClusterMarkerSize}");
                Console.WriteLine($"ClusterBadgeSize: {_visualConfig.ClusterBadgeSize}");
                Console.WriteLine($"ClusterCountFontSize: {_visualConfig.ClusterCountFontSize}");
                Console.WriteLine($"ZoomScale: {_visualConfig.ZoomScale}");
                Console.WriteLine($"AnimationDurationMs: {_visualConfig.AnimationDurationMs}");
                Console.WriteLine($"===========================");
                
                System.Diagnostics.Debug.WriteLine($"=== Visual Config Loaded ===");
                System.Diagnostics.Debug.WriteLine($"Config Path: {configPath}");
                System.Diagnostics.Debug.WriteLine($"ClusterDistanceThreshold: {_visualConfig.ClusterDistanceThreshold}");
                System.Diagnostics.Debug.WriteLine($"LocationMarkerSize: {_visualConfig.LocationMarkerSize}");
                System.Diagnostics.Debug.WriteLine($"ClusterMarkerSize: {_visualConfig.ClusterMarkerSize}");
                System.Diagnostics.Debug.WriteLine($"ClusterBadgeSize: {_visualConfig.ClusterBadgeSize}");
                System.Diagnostics.Debug.WriteLine($"ClusterCountFontSize: {_visualConfig.ClusterCountFontSize}");
                System.Diagnostics.Debug.WriteLine($"ZoomScale: {_visualConfig.ZoomScale}");
                System.Diagnostics.Debug.WriteLine($"AnimationDurationMs: {_visualConfig.AnimationDurationMs}");
                System.Diagnostics.Debug.WriteLine($"===========================");
                
                _contentLoader = new ContentLoader(_logger);
                _contentLoader.ClusterDistanceThreshold = _visualConfig.ClusterDistanceThreshold;
                _logger.LogInfo("ContentLoader created");
                
                // Initialize radial extension calculator if enabled
                if (_visualConfig.RadialExtension.Enabled)
                {
                    _extensionCalculator = new RadialExtensionCalculator(_visualConfig.RadialExtension);
                    _logger.LogInfo("RadialExtensionCalculator initialized");
                }
                
                _navigationService = new MapNavigationService();
                _logger.LogInfo("MapNavigationService created");

                _viewportCalculator = new ViewportCalculator();
                _logger.LogInfo("ViewportCalculator created");
                
                _frameCache = new AnimationFrameCache(_logger);
                _logger.LogInfo("AnimationFrameCache created");

                // Initialize zoomed region cache with full-res image path
                var fullResPath = IOPath.Combine(AppDomain.CurrentDomain.BaseDirectory, "Images&Content", "World Map 1976.jpg");
                _zoomedRegionCache = new ZoomedRegionCache(_logger, fullResPath);
                _logger.LogInfo("ZoomedRegionCache created");

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
                _logger.LogInfo("MapDisplay.LoadMapImage completed - viewport initialized");

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

                // Close existing subwindow and thumbnail browser if any
                if (_activeSubwindow != null)
                {
                    await CloseActiveSubwindowAsync();
                }
                
                if (_activeThumbnailBrowser != null)
                {
                    _activeThumbnailBrowser.Close();
                    _activeThumbnailBrowser = null;
                }
                
                if (_activeDidacticWindow != null)
                {
                    _activeDidacticWindow.Close();
                    _activeDidacticWindow = null;
                }

                // Load all images with translations for this location
                var allImagesWithTranslations = await _contentLoader.LoadAllLocationImagesWithTranslationsAsync(location);
                
                if (allImagesWithTranslations.Length == 0)
                {
                    // Show text message if no content available
                    var content = $"Content not available for {location.Name}";
                    
                    _activeSubwindow = new ContentSubwindow
                    {
                        AssociatedLocation = location,
                        Owner = this
                    };
                    
                    var markerPosition = MapDisplay.GetMapPosition(location.PixelX, location.PixelY, ImageWidth, ImageHeight);
                    _activeSubwindow.ShowContent(content, location.Name, markerPosition);
                }
                else
                {
                    // Show first image
                    await ShowImageAtIndexAsync(location, allImagesWithTranslations, 0);
                }

                _logger.LogInfo($"Content subwindow opened for: {location.Name}");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to show content for location {location.Name}: {ex.Message}");
            }
        }

        /// <summary>
        /// Shows a specific image from a location's image collection.
        /// </summary>
        private async Task ShowImageAtIndexAsync(Location location, (BitmapImage Image, string? TranslationText)[] allImagesWithTranslations, int index)
        {
            var (image, translationText) = allImagesWithTranslations[index];
            
            // Create and show content subwindow
            _activeSubwindow = new ContentSubwindow
            {
                AssociatedLocation = location,
                Owner = this
            };

            var markerPosition = MapDisplay.GetMapPosition(location.PixelX, location.PixelY, ImageWidth, ImageHeight);
            _activeSubwindow.ShowContent(image, location.Name, markerPosition, translationText);

            // Load and show didactic text if available
            var didacticText = await _contentLoader.LoadDidacticTextAsync(location);
            if (!string.IsNullOrEmpty(didacticText))
            {
                _activeDidacticWindow = new DidacticTextWindow
                {
                    Owner = this
                };
                
                _activeDidacticWindow.SetContent(didacticText);
                _activeDidacticWindow.PositionRelativeTo(_activeSubwindow);
                _activeDidacticWindow.Show();
                _activeDidacticWindow.AnimateOpen();
                
                _logger.LogInfo($"Didactic text window opened for location: {location.Name}");
            }

            // If there are multiple images, show thumbnail browser
            if (allImagesWithTranslations.Length > 1)
            {
                var images = allImagesWithTranslations.Select(x => x.Image).ToArray();
                
                _activeThumbnailBrowser = new ThumbnailBrowserWindow
                {
                    Owner = this
                };
                
                _activeThumbnailBrowser.LoadThumbnails(images, index);
                
                // Position thumbnail browser to the right of content window
                // If didactic window exists, it's already on the left
                _activeThumbnailBrowser.PositionRelativeTo(_activeSubwindow);
                
                // Handle thumbnail selection
                _activeThumbnailBrowser.ThumbnailSelected += (s, selectedIndex) =>
                {
                    if (_activeSubwindow != null)
                    {
                        var (selectedImage, selectedTranslation) = allImagesWithTranslations[selectedIndex];
                        var newMarkerPosition = MapDisplay.GetMapPosition(location.PixelX, location.PixelY, ImageWidth, ImageHeight);
                        _activeSubwindow.ShowContent(selectedImage, location.Name, newMarkerPosition, selectedTranslation);
                        _activeThumbnailBrowser?.SetSelectedIndex(selectedIndex);
                    }
                };
                
                _activeThumbnailBrowser.Show();
                _activeThumbnailBrowser.AnimateOpen();
                
                _logger.LogInfo($"Thumbnail browser opened with {allImagesWithTranslations.Length} images");
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
            
            // Also close thumbnail browser
            if (_activeThumbnailBrowser != null)
            {
                _activeThumbnailBrowser.Close();
                _activeThumbnailBrowser = null;
            }
            
            // Also close didactic window
            if (_activeDidacticWindow != null)
            {
                _activeDidacticWindow.Close();
                _activeDidacticWindow = null;
            }
        }

        /// <summary>
        /// Closes the active content subwindow asynchronously and waits for completion.
        /// </summary>
        private Task CloseActiveSubwindowAsync()
        {
            if (_activeSubwindow == null && _activeThumbnailBrowser == null && _activeDidacticWindow == null)
                return Task.CompletedTask;

            _logger.LogInfo("Closing active subwindow (async)");
            
            var tcs = new TaskCompletionSource<bool>();
            var windowToClose = _activeSubwindow;
            _activeSubwindow = null;

            // Close thumbnail browser immediately
            if (_activeThumbnailBrowser != null)
            {
                _activeThumbnailBrowser.Close();
                _activeThumbnailBrowser = null;
            }
            
            // Close didactic window immediately
            if (_activeDidacticWindow != null)
            {
                _activeDidacticWindow.Close();
                _activeDidacticWindow = null;
            }

            if (windowToClose != null)
            {
                windowToClose.AnimateClose(() =>
                {
                    Focus(); // Return focus to main window
                    tcs.SetResult(true);
                });
            }
            else
            {
                tcs.SetResult(true);
            }

            return tcs.Task;
        }

        /// <summary>
        /// Adds clusters to the map canvas using viewport coordinates.
        /// </summary>
        private void AddClustersToMap(List<LocationCluster> clusters)
        {
            _logger.LogInfo($"[AddClustersToMap] Adding {clusters.Count} clusters");
            
            var canvas = MapDisplay.Markers;
            var viewport = MapDisplay.CurrentViewport;
            
            if (viewport == null)
            {
                _logger.LogError("Current viewport is null");
                return;
            }
            
            _logger.LogInfo($"  Viewport: ({viewport.ViewportX:F2}, {viewport.ViewportY:F2}) {viewport.ViewportWidth:F2}x{viewport.ViewportHeight:F2}");
            
            foreach (var cluster in clusters)
            {
                if (cluster.IsSingleLocation)
                {
                    // Add individual marker
                    AddIndividualMarker(cluster.Locations[0]);
                }
                else
                {
                    // Add cluster marker
                    AddClusterMarker(cluster);
                }
            }
            
            _logger.LogInfo($"[AddClustersToMap] Complete - {_individualMarkers.Count} individual, {_clusterMarkers.Count} cluster markers");
            
            // Update marker positions based on current viewport
            UpdateMarkerPositions();
        }

        /// <summary>
        /// Adds an individual location marker to the canvas using viewport coordinates.
        /// </summary>
        private LocationMarker AddIndividualMarker(Location location)
        {
            var marker = new LocationMarker { Location = location };
            
            // Position will be updated by UpdateMarkerPositions()
            Canvas.SetLeft(marker, 0);
            Canvas.SetTop(marker, 0);
            
            // Add click handler
            marker.MouseLeftButtonDown += (s, e) =>
            {
                marker.AnimateClick();
                
                // If we're at full map view (not zoomed), zoom to this location
                // Otherwise, show content
                var viewport = MapDisplay.CurrentViewport;
                if (viewport != null && viewport.ZoomLevel <= 1.0)
                {
                    // Create a single-location cluster and zoom to it
                    var singleCluster = new LocationCluster
                    {
                        Locations = new List<Location> { location },
                        CenterPoint = new Point(location.PixelX, location.PixelY)
                    };
                    OnClusterClicked(singleCluster);
                }
                else
                {
                    // Already zoomed, show content
                    ShowContentForLocation(location);
                }
                
                e.Handled = true;
            };
            
            _individualMarkers.Add(marker);
            MapDisplay.Markers.Children.Add(marker);
            
            _logger.LogInfo($"  Individual marker '{location.Name}' added at source ({location.PixelX}, {location.PixelY})");
            
            return marker;
        }

        /// <summary>
        /// Adds a cluster marker to the canvas using viewport coordinates.
        /// </summary>
        private void AddClusterMarker(LocationCluster cluster)
        {
            var marker = new ClusterMarker { Cluster = cluster };
            marker.UpdateDisplay();
            
            // Position will be updated by UpdateMarkerPositions()
            Canvas.SetLeft(marker, 0);
            Canvas.SetTop(marker, 0);
            
            // Add click handler
            marker.MouseLeftButtonDown += (s, e) =>
            {
                marker.AnimateClick();
                OnClusterClicked(cluster);
                e.Handled = true;
            };
            
            _clusterMarkers.Add(marker);
            MapDisplay.Markers.Children.Add(marker);
            
            _logger.LogInfo($"  Cluster marker ({cluster.Count} locations) added at source ({cluster.CenterPoint.X:F2}, {cluster.CenterPoint.Y:F2})");
        }

        /// <summary>
        /// Updates all marker positions based on the current viewport.
        /// Applies radial extensions for dense marker groups when enabled.
        /// </summary>
        private void UpdateMarkerPositions()
        {
            var viewport = MapDisplay.CurrentViewport;
            if (viewport == null)
                return;

            var containerWidth = MapDisplay.ActualWidth;
            var containerHeight = MapDisplay.ActualHeight;

            // Clear existing extensions only when not animating
            if (!_isAnimating)
            {
                ClearExtensionLines();
            }

            // Skip radial extension logic entirely during animation
            // Extensions will be applied after animation completes
            if (_isAnimating)
            {
                // During animation, just position markers normally
                foreach (var marker in _individualMarkers.Where(m => m.Visibility == Visibility.Visible))
                {
                    PositionMarkerNormally(marker, viewport, containerWidth, containerHeight);
                }

                foreach (var marker in _clusterMarkers.Where(m => m.Visibility == Visibility.Visible))
                {
                    var screenPos = viewport.SourceToScreen(
                        marker.Cluster.CenterPoint.X,
                        marker.Cluster.CenterPoint.Y,
                        containerWidth,
                        containerHeight);

                    var markerSize = _visualConfig.ClusterMarkerSize;
                    Canvas.SetLeft(marker, screenPos.X - markerSize / 2);
                    Canvas.SetTop(marker, screenPos.Y - markerSize / 2);
                }
                return;
            }

            // Check if we should apply radial extensions (only after animation completes)
            bool shouldApplyExtensions = _visualConfig.RadialExtension.Enabled &&
                                          _extensionCalculator != null &&
                                          viewport.ZoomLevel >= _visualConfig.RadialExtension.ZoomThresholdForExtensions;

            _logger.LogInfo($"[UpdateMarkerPositions] ZoomLevel={viewport.ZoomLevel:F2}, Threshold={_visualConfig.RadialExtension.ZoomThresholdForExtensions}, ShouldApply={shouldApplyExtensions}");

            if (shouldApplyExtensions)
            {
                // Calculate SOURCE positions for dense group detection (in image pixel space)
                var markerSourcePositions = CalculateMarkerSourcePositions();
                
                // Calculate SCREEN positions for rendering (in viewport space)
                var markerScreenPositions = CalculateMarkerScreenPositions(viewport, containerWidth, containerHeight);

                _logger.LogInfo($"[UpdateMarkerPositions] Calculated {markerScreenPositions.Count} marker positions");

                // Detect dense groups using SOURCE positions (proximity in image space)
                _denseGroups = _extensionCalculator.DetectDenseGroups(markerSourcePositions);

                _logger.LogInfo($"[UpdateMarkerPositions] Detected {_denseGroups.Count} dense groups");

                if (_denseGroups.Any())
                {
                    _logger.LogInfo($"Detected {_denseGroups.Count} dense marker groups");

                    var markersInGroups = new HashSet<Location>();
                    var allExtensions = new List<RadialExtension>();

                    // First pass: Calculate extensions for all groups
                    int groupId = 0;
                    foreach (var group in _denseGroups)
                    {
                        _logger.LogInfo($"  Processing group {groupId} with {group.Count} locations at center ({group.CenterPoint.X:F2}, {group.CenterPoint.Y:F2})");
                        
                        // Calculate extensions using SCREEN positions (for rendering)
                        var extensions = _extensionCalculator.CalculateRadialExtensions(
                            group,
                            markerScreenPositions,
                            containerWidth,
                            containerHeight);

                        _logger.LogInfo($"  Calculated {extensions.Count} extensions");

                        // Assign group ID to all extensions
                        foreach (var ext in extensions)
                        {
                            ext.GroupId = groupId;
                        }

                        // Validate no crossings
                        if (_extensionCalculator.ValidateNoCrossings(extensions))
                        {
                            _logger.LogInfo($"  Validation passed");
                            group.Extensions = extensions;
                            allExtensions.AddRange(extensions);
                            
                            // Track which markers are in groups
                            foreach (var loc in group.Locations)
                            {
                                markersInGroups.Add(loc);
                            }
                        }
                        else
                        {
                            _logger.LogWarning($"Line crossings detected for group with {group.Count} markers, using normal positioning");
                            // Fall back to normal positioning for this group
                            ApplyNormalPositioning(group.Locations, viewport, containerWidth, containerHeight);
                            
                            foreach (var loc in group.Locations)
                            {
                                markersInGroups.Add(loc);
                            }
                        }

                        groupId++;
                    }

                    // Second pass: Adjust for marker overlaps across all groups
                    if (allExtensions.Any())
                    {
                        AdjustForMarkerOverlaps(allExtensions, _visualConfig.LocationMarkerSize);
                    }

                    // Third pass: Apply the extensions (now with adjusted lengths)
                    foreach (var group in _denseGroups.Where(g => g.Extensions.Any()))
                    {
                        ApplyRadialExtensions(group, viewport, containerWidth, containerHeight);
                    }

                    // Position markers not in dense groups normally
                    foreach (var marker in _individualMarkers.Where(m => m.Visibility == Visibility.Visible))
                    {
                        if (!markersInGroups.Contains(marker.Location))
                        {
                            PositionMarkerNormally(marker, viewport, containerWidth, containerHeight);
                        }
                    }

                    // Update cluster markers normally
                    foreach (var marker in _clusterMarkers.Where(m => m.Visibility == Visibility.Visible))
                    {
                        var screenPos = viewport.SourceToScreen(
                            marker.Cluster.CenterPoint.X,
                            marker.Cluster.CenterPoint.Y,
                            containerWidth,
                            containerHeight);

                        var markerSize = _visualConfig.ClusterMarkerSize;
                        Canvas.SetLeft(marker, screenPos.X - markerSize / 2);
                        Canvas.SetTop(marker, screenPos.Y - markerSize / 2);
                    }

                    return;
                }
                else
                {
                    _logger.LogInfo("[UpdateMarkerPositions] No dense groups detected, using normal positioning");
                }
            }

            // Normal positioning (no extensions)
            foreach (var marker in _individualMarkers.Where(m => m.Visibility == Visibility.Visible))
            {
                var screenPos = viewport.SourceToScreen(
                    marker.Location.PixelX,
                    marker.Location.PixelY,
                    containerWidth,
                    containerHeight);

                // Use config size for centering (ActualWidth/Height may be 0 before layout)
                var markerSize = _visualConfig.LocationMarkerSize;
                Canvas.SetLeft(marker, screenPos.X - markerSize / 2);
                Canvas.SetTop(marker, screenPos.Y - markerSize / 2);
            }

            // Update cluster markers
            foreach (var marker in _clusterMarkers.Where(m => m.Visibility == Visibility.Visible))
            {
                var screenPos = viewport.SourceToScreen(
                    marker.Cluster.CenterPoint.X,
                    marker.Cluster.CenterPoint.Y,
                    containerWidth,
                    containerHeight);

                // Use config size for centering (ActualWidth/Height may be 0 before layout)
                var markerSize = _visualConfig.ClusterMarkerSize;
                Canvas.SetLeft(marker, screenPos.X - markerSize / 2);
                Canvas.SetTop(marker, screenPos.Y - markerSize / 2);
            }
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

            // Update positions for visible markers
            UpdateMarkerPositions();
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
                    var newMarker = AddIndividualMarker(location);
                    newMarker.Visibility = Visibility.Visible;
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

            // Update positions for visible markers
            UpdateMarkerPositions();
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

        private async void OnWindowLoaded(object sender, RoutedEventArgs e)
        {
            await InitializeAsync();
        }

        private void OnClusterClicked(LocationCluster cluster)
        {
            _logger.LogInfo($"Cluster clicked: {cluster.Count} locations");
            AnimateZoomToCluster(cluster);
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
        /// Animates zooming into a cluster using viewport-based rendering.
        /// </summary>
        private void AnimateZoomToCluster(LocationCluster cluster)
                {
                    try
                    {
                        _logger.LogInfo("=== AnimateZoomToCluster START (Viewport) ===");
                        _logger.LogInfo($"  Cluster: {cluster.Count} locations");
                        _logger.LogInfo($"  Cluster center: ({cluster.CenterPoint.X:F2}, {cluster.CenterPoint.Y:F2})");

                        // Set animation flag to prevent clearing radial extensions during animation
                        _isAnimating = true;

                        // Save current state before zooming
                        var currentState = ZoomState.CreateFullMapView();
                        _navigationService.PushState(currentState);
                        _logger.LogInfo("  Current state saved to navigation stack");

                        // Get current viewport
                        var startViewport = MapDisplay.CurrentViewport;
                        if (startViewport == null)
                        {
                            _logger.LogError("Current viewport is null");
                            return;
                        }

                        // Calculate target viewport centered on cluster
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

                        // Pre-render keyframes with caching - more frames = smoother animation
                        const int keyframeCount = 30;
                        var prerenderedFrames = PreRenderKeyframes(startViewport, targetViewport, keyframeCount, out var keyframeProgress);

                        // Display first frame immediately to avoid delay
                        MapDisplay.DisplayImage.Source = prerenderedFrames[0];
                        MapDisplay.SetCurrentViewport(startViewport);
                        UpdateMarkerPositions();

                        // Start animation timer AFTER pre-rendering
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

                            // Calculate current progress (0.0 to 1.0)
                            var elapsed = (now - animStart).TotalMilliseconds;
                            var progress = Math.Min(1.0, elapsed / AnimationDurationMs);

                            // Find closest pre-rendered frame
                            int frameIndex = 0;
                            double minDiff = double.MaxValue;
                            for (int i = 0; i < keyframeCount; i++)
                            {
                                double diff = Math.Abs(keyframeProgress[i] - progress);
                                if (diff < minDiff)
                                {
                                    minDiff = diff;
                                    frameIndex = i;
                                }
                            }

                            // Display pre-rendered frame
                            MapDisplay.DisplayImage.Source = prerenderedFrames[frameIndex];

                            // Update viewport state for marker positioning
                            var currentViewport = _viewportCalculator.Interpolate(startViewport, targetViewport, keyframeProgress[frameIndex]);
                            MapDisplay.SetCurrentViewport(currentViewport);

                            // Update marker positions
                            UpdateMarkerPositions();

                            if (frameCount <= 3 || frameCount % 3 == 0)
                            {
                                var centerX = currentViewport.ViewportX + (currentViewport.ViewportWidth / 2.0);
                                var centerY = currentViewport.ViewportY + (currentViewport.ViewportHeight / 2.0);
                                _logger.LogInfo($"  [FRAME {frameCount}] +{elapsed:F0}ms, delta={frameDelta:F1}ms, progress={progress:F3}, keyframe={frameIndex}, center=({centerX:F1},{centerY:F1}), zoom={currentViewport.ZoomLevel:F2}");
                            }

                            // Check if animation is complete
                            if (progress >= 1.0)
                            {
                                CompositionTarget.Rendering -= renderHandler;
                                _isAnimating = false; // Animation complete, allow radial extensions to be cleared if needed
                                _logger.LogInfo($"  [FRAMES TOTAL] {frameCount} frames in {elapsed:F0}ms");
                                _logger.LogInfo("=== Zoom animation COMPLETED (Viewport) ===");

                                // Ensure final viewport is set
                                MapDisplay.UpdateViewport(targetViewport);
                                UpdateMarkerPositions();

                                ShowZoomedView(cluster);

                                // Show Back button
                                BackButton.Visibility = Visibility.Visible;
                            }
                        };

                        CompositionTarget.Rendering += renderHandler;
                        _logger.LogInfo("=== Zoom animations STARTED (Viewport) ===");
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
        /// Shows individual markers for the zoomed cluster and displays high-quality zoomed image.
        /// </summary>
        private void ShowZoomedView(LocationCluster cluster)
        {
            try
            {
                _logger.LogInfo("=== ShowZoomedView START ===");
                _logger.LogInfo($"  Cluster has {cluster.Count} locations");
                
                var viewport = MapDisplay.CurrentViewport;
                if (viewport != null)
                {
                    _logger.LogInfo($"  Current viewport: ({viewport.ViewportX:F2}, {viewport.ViewportY:F2}) {viewport.ViewportWidth:F2}x{viewport.ViewportHeight:F2}, zoom={viewport.ZoomLevel:F2}");
                    
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

                // Show only individual markers for this cluster
                ShowOnlyIndividualMarkers(cluster);
                
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
        /// Animates zooming out to the full map view using viewport-based rendering.
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
                _logger.LogInfo("=== AnimateZoomOut START (Viewport) ===");

                // Clear radial extension lines before starting zoom-out animation
                ClearExtensionLines();
                _logger.LogInfo("  Cleared radial extension lines");

                // Set animation flag to prevent clearing radial extensions during animation
                _isAnimating = true;

                // Get current viewport
                var startViewport = MapDisplay.CurrentViewport;
                if (startViewport == null)
                {
                    _logger.LogError("Current viewport is null");
                    return;
                }

                _logger.LogInfo($"  Current viewport: ({startViewport.ViewportX:F2}, {startViewport.ViewportY:F2}) {startViewport.ViewportWidth:F2}x{startViewport.ViewportHeight:F2}, zoom={startViewport.ZoomLevel:F2}");

                // Pop the previous state
                var previousState = _navigationService.PopState();
                if (previousState == null)
                {
                    _logger.LogWarning("Previous state is null");
                    return;
                }

                _logger.LogInfo("  Previous state popped from navigation stack");

                // Calculate target viewport (full map view)
                var targetViewport = ViewportState.CreateFullMapView(
                    ImageWidth,
                    ImageHeight,
                    MapDisplay.ActualWidth,
                    MapDisplay.ActualHeight);

                _logger.LogInfo($"  Target viewport: ({targetViewport.ViewportX:F2}, {targetViewport.ViewportY:F2}) {targetViewport.ViewportWidth:F2}x{targetViewport.ViewportHeight:F2}");

                // Pre-render keyframes with caching - more frames = smoother animation
                const int keyframeCount = 30;
                var prerenderedFrames = PreRenderKeyframes(startViewport, targetViewport, keyframeCount, out var keyframeProgress);

                // Display first frame immediately to avoid delay
                MapDisplay.DisplayImage.Source = prerenderedFrames[0];
                MapDisplay.SetCurrentViewport(startViewport);
                UpdateMarkerPositions();

                // Start animation timer AFTER pre-rendering
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

                    // Calculate current progress (0.0 to 1.0)
                    var elapsed = (now - animStart).TotalMilliseconds;
                    var progress = Math.Min(1.0, elapsed / AnimationDurationMs);

                    // Find closest pre-rendered frame
                    int frameIndex = 0;
                    double minDiff = double.MaxValue;
                    for (int i = 0; i < keyframeCount; i++)
                    {
                        double diff = Math.Abs(keyframeProgress[i] - progress);
                        if (diff < minDiff)
                        {
                            minDiff = diff;
                            frameIndex = i;
                        }
                    }
                    
                    // Display pre-rendered frame
                    MapDisplay.DisplayImage.Source = prerenderedFrames[frameIndex];
                    
                    // Update viewport state for marker positioning
                    var currentViewport = _viewportCalculator.Interpolate(startViewport, targetViewport, keyframeProgress[frameIndex]);
                    MapDisplay.SetCurrentViewport(currentViewport);

                    // Update marker positions
                    UpdateMarkerPositions();

                    if (frameCount <= 3 || frameCount % 3 == 0)
                    {
                        var centerX = currentViewport.ViewportX + (currentViewport.ViewportWidth / 2.0);
                        var centerY = currentViewport.ViewportY + (currentViewport.ViewportHeight / 2.0);
                        _logger.LogInfo($"  [FRAME {frameCount}] +{elapsed:F0}ms, delta={frameDelta:F1}ms, progress={progress:F3}, keyframe={frameIndex}, center=({centerX:F1},{centerY:F1}), zoom={currentViewport.ZoomLevel:F2}");
                    }

                    // Check if animation is complete
                    if (progress >= 1.0)
                    {
                        CompositionTarget.Rendering -= renderHandler;
                        _isAnimating = false; // Animation complete, allow radial extensions to be cleared if needed
                        _logger.LogInfo($"  [FRAMES TOTAL] {frameCount} frames in {elapsed:F0}ms");
                        _logger.LogInfo("=== Zoom-out animation COMPLETED (Viewport) ===");

                        // Ensure final viewport is set
                        MapDisplay.UpdateViewport(targetViewport);
                        UpdateMarkerPositions();

                        ShowClusterView();

                        if (!_navigationService.CanGoBack)
                        {
                            _logger.LogInfo("  Hiding Back button (at root level)");
                            BackButton.Visibility = Visibility.Collapsed;
                        }
                    }
                };

                CompositionTarget.Rendering += renderHandler;
                _logger.LogInfo("=== Zoom-out animations STARTED (Viewport) ===");
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
            // Viewport handles size changes automatically in MapDisplayControl
            // Just update marker positions if we have a viewport
            if (MapDisplay.CurrentViewport != null)
            {
                UpdateMarkerPositions();
            }
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

        private void ShowError(string message)
        {
            MessageBox.Show(message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }

        #region Radial Extension Methods

        /// <summary>
        /// Calculates source image positions for all visible markers.
        /// Used for dense group detection in image pixel space.
        /// </summary>
        private Dictionary<Location, Point> CalculateMarkerSourcePositions()
        {
            var positions = new Dictionary<Location, Point>();

            foreach (var marker in _individualMarkers.Where(m => m.Visibility == Visibility.Visible))
            {
                var sourcePos = new Point(marker.Location.PixelX, marker.Location.PixelY);
                positions[marker.Location] = sourcePos;
            }

            return positions;
        }

        /// <summary>
        /// Calculates screen positions for all visible markers.
        /// Used for rendering radial extensions in viewport space.
        /// </summary>
        private Dictionary<Location, Point> CalculateMarkerScreenPositions(
            ViewportState viewport,
            double containerWidth,
            double containerHeight)
        {
            var positions = new Dictionary<Location, Point>();

            foreach (var marker in _individualMarkers.Where(m => m.Visibility == Visibility.Visible))
            {
                var screenPos = viewport.SourceToScreen(
                    marker.Location.PixelX,
                    marker.Location.PixelY,
                    containerWidth,
                    containerHeight);

                positions[marker.Location] = screenPos;
            }

            return positions;
        }

        /// <summary>
        /// Clears all extension lines from the canvas.
        /// </summary>
        private void ClearExtensionLines()
        {
            foreach (var line in _extensionLines)
            {
                MapDisplay.Markers.Children.Remove(line);
            }
            _extensionLines.Clear();
            _markerToLineMap.Clear();
        }

        /// <summary>
        /// Applies radial extensions to a dense marker group.
        /// </summary>
        private void ApplyRadialExtensions(DenseMarkerGroup group, ViewportState viewport, double containerWidth, double containerHeight)
        {
            bool logCalculation = _visualConfig.Debug.LogRadialExtensionCalculation;
            
            if (logCalculation)
            {
                _logger.LogInfo($"[ApplyRadialExtensions] Applying {group.Extensions.Count} extensions");
                _logger.LogInfo($"[ApplyRadialExtensions] Canvas children before: {MapDisplay.Markers.Children.Count}");
            }
            
            foreach (var extension in group.Extensions)
            {
                // Convert extension positions from source to screen coordinates
                var originalScreenPos = viewport.SourceToScreen(
                    extension.Location.PixelX,
                    extension.Location.PixelY,
                    containerWidth,
                    containerHeight);

                var extendedScreenPos = extension.ExtendedPosition;

                // Calculate actual rendered slope/angle
                double dx = extendedScreenPos.X - originalScreenPos.X;
                double dy = extendedScreenPos.Y - originalScreenPos.Y;
                double length = Math.Sqrt(dx * dx + dy * dy);
                
                // Calculate angle: 0° = north, clockwise (same as our angle system)
                double angleRadians = Math.Atan2(dx, -dy);
                double angleDegrees = angleRadians * (180.0 / Math.PI);
                if (angleDegrees < 0) angleDegrees += 360.0;

                if (logCalculation)
                {
                    _logger.LogInfo($"  Extension: {extension.Location.Name} from ({originalScreenPos.X:F1},{originalScreenPos.Y:F1}) to ({extendedScreenPos.X:F1},{extendedScreenPos.Y:F1})");
                    _logger.LogInfo($"    Length: {length:F1}px, Angle: {angleDegrees:F2}° (stored: {extension.Angle:F2}°)");
                }

                // Create extension line
                var line = CreateExtensionLine(originalScreenPos, extendedScreenPos);
                _extensionLines.Add(line);
                MapDisplay.Markers.Children.Add(line);
                
                if (logCalculation)
                {
                    _logger.LogInfo($"    Line added to canvas, total lines: {_extensionLines.Count}, canvas children: {MapDisplay.Markers.Children.Count}");
                }

                // Position marker at extended location
                var marker = FindMarkerForLocation(extension.Location);
                if (marker != null)
                {
                    // Map marker to its extension line for hover highlighting
                    _markerToLineMap[marker] = line;
                    
                    // Wire up hover events for line highlighting
                    marker.MouseEnter += OnMarkerMouseEnter;
                    marker.MouseLeave += OnMarkerMouseLeave;
                    
                    Panel.SetZIndex(marker, 2000); // Markers on top of lines
                    Canvas.SetLeft(marker, extendedScreenPos.X - marker.Width / 2);
                    Canvas.SetTop(marker, extendedScreenPos.Y - marker.Height / 2);
                    
                    if (logCalculation)
                    {
                        _logger.LogInfo($"    Marker positioned at ({extendedScreenPos.X:F1},{extendedScreenPos.Y:F1}), ZIndex=2000");
                    }
                }
                else
                {
                    _logger.LogWarning($"    Marker not found for location: {extension.Location.Name}");
                }
            }

            if (logCalculation)
            {
                _logger.LogInfo($"[ApplyRadialExtensions] Canvas children after: {MapDisplay.Markers.Children.Count}");
                _logger.LogInfo($"[ApplyRadialExtensions] Total extension lines in list: {_extensionLines.Count}");
            }

            // Animate if configured
            if (_visualConfig.RadialExtension.AnimateExtension)
            {
                var linesToAnimate = _extensionLines.Skip(_extensionLines.Count - group.Extensions.Count).ToList();
                
                if (logCalculation)
                {
                    _logger.LogInfo($"[ApplyRadialExtensions] Animating {linesToAnimate.Count} lines");
                }
                
                AnimateExtensionLines(linesToAnimate);
            }
        }

        /// <summary>
        /// Creates a visual extension line.
        /// </summary>
        private Line CreateExtensionLine(Point start, Point end)
        {
            var line = new Line
            {
                X1 = start.X,
                Y1 = start.Y,
                X2 = end.X,
                Y2 = end.Y,
                Stroke = new SolidColorBrush(Colors.Red), // Bright red for debugging
                StrokeThickness = 3.0, // Thicker for visibility
                Opacity = 1.0, // Full opacity for debugging
                IsHitTestVisible = false
            };

            // Add subtle shadow
            line.Effect = new DropShadowEffect
            {
                Color = Colors.Black,
                Direction = 270,
                ShadowDepth = 1,
                BlurRadius = 2,
                Opacity = 0.3
            };
            
            Panel.SetZIndex(line, 1000); // Ensure lines are on top

            _logger.LogInfo($"    Created line: ({start.X:F1},{start.Y:F1}) to ({end.X:F1},{end.Y:F1}), Stroke=Red, Thickness=3");

            return line;
        }

        /// <summary>
        /// Finds the LocationMarker for a given Location.
        /// </summary>
        private LocationMarker? FindMarkerForLocation(Location location)
        {
            return _individualMarkers.FirstOrDefault(m => m.Location == location);
        }

        /// <summary>
        /// Positions a marker at its normal (non-extended) location.
        /// </summary>
        private void PositionMarkerNormally(
            LocationMarker marker,
            ViewportState viewport,
            double containerWidth,
            double containerHeight)
        {
            var screenPos = viewport.SourceToScreen(
                marker.Location.PixelX,
                marker.Location.PixelY,
                containerWidth,
                containerHeight);

            var markerSize = _visualConfig.LocationMarkerSize;
            Canvas.SetLeft(marker, screenPos.X - markerSize / 2);
            Canvas.SetTop(marker, screenPos.Y - markerSize / 2);
        }

        /// <summary>
        /// Applies normal positioning to a list of locations (fallback).
        /// </summary>
        private void ApplyNormalPositioning(
            List<Location> locations,
            ViewportState viewport,
            double containerWidth,
            double containerHeight)
        {
            foreach (var location in locations)
            {
                var marker = FindMarkerForLocation(location);
                if (marker != null)
                {
                    PositionMarkerNormally(marker, viewport, containerWidth, containerHeight);
                }
            }
        }

        /// <summary>
        /// Adjusts extension line lengths to prevent marker overlaps.
        /// Checks all pairs of extended marker positions and adjusts lengths if they would overlap.
        /// Uses multiple passes to handle cascading adjustments.
        /// </summary>
        private void AdjustForMarkerOverlaps(List<RadialExtension> allExtensions, double markerSize)
        {
            double minGap = markerSize * 2.5; // Minimum gap: 2.5x marker width for comfortable spacing
            double minAngleDiff = _visualConfig.RadialExtension.AngleNudgeThreshold; // Minimum angle separation
            double angleNudge = _visualConfig.RadialExtension.AngleNudgeAmount; // How much to nudge angles
            int maxPasses = 5; // Maximum number of adjustment passes
            int pass = 0;
            bool hadAdjustments;

            bool logAngles = _visualConfig.Debug.LogRadialExtensionAngles;
            bool logOverlaps = _visualConfig.Debug.LogRadialExtensionOverlaps;

            if (logOverlaps)
            {
                _logger.LogInfo($"[AdjustForMarkerOverlaps] Checking {allExtensions.Count} extensions for overlaps (minGap={minGap:F1}px, minAngle={minAngleDiff:F1}°)");
            }

            // Log initial angles for each group
            if (logAngles)
            {
                var groupedExtensions = allExtensions.GroupBy(e => e.GroupId).ToList();
                foreach (var group in groupedExtensions)
                {
                    var groupExtensions = group.OrderBy(e => e.Angle).ToList();
                    _logger.LogInfo($"  Group {group.Key} initial angles:");
                    for (int i = 0; i < groupExtensions.Count; i++)
                    {
                        var ext = groupExtensions[i];
                        double nextAngleDiff = 0;
                        if (i < groupExtensions.Count - 1)
                        {
                            nextAngleDiff = groupExtensions[i + 1].Angle - ext.Angle;
                        }
                        _logger.LogInfo($"    {ext.Location.Name}: {ext.Angle:F2}° (next diff: {nextAngleDiff:F2}°)");
                    }
                }
            }

            do
            {
                pass++;
                hadAdjustments = false;

                // First pass: Check and adjust angles within each group
                // Group extensions by GroupId
                var groupedExtensions = allExtensions.GroupBy(e => e.GroupId).ToList();
                
                foreach (var group in groupedExtensions)
                {
                    var groupExtensions = group.OrderBy(e => e.Angle).ToList();
                    
                    // Check all pairs within this group
                    for (int i = 0; i < groupExtensions.Count; i++)
                    {
                        for (int j = i + 1; j < groupExtensions.Count; j++)
                        {
                            var ext1 = groupExtensions[i];
                            var ext2 = groupExtensions[j];

                            // Calculate angle difference
                            double angleDiff = ext2.Angle - ext1.Angle;
                            if (angleDiff < 0) angleDiff += 360.0;

                            // If angles are too close, nudge them apart
                            // Special handling for exactly equal angles (0.0°)
                            if (angleDiff < minAngleDiff)
                            {
                                if (pass == 1 && logAngles)
                                {
                                    _logger.LogInfo($"  Group {ext1.GroupId}: Close angles: {ext1.Location.Name} ({ext1.Angle:F1}°) and {ext2.Location.Name} ({ext2.Angle:F1}°), diff={angleDiff:F1}°");
                                }

                                hadAdjustments = true;

                                // For exactly equal angles, use a larger nudge
                                double nudge = (angleDiff < 0.01) ? angleNudge : (angleNudge / 2.0);
                                ext1.Angle -= nudge;
                                ext2.Angle += nudge;

                                // Recalculate extended positions with new angles
                                double length1 = CalculateCurrentLength(ext1);
                                double length2 = CalculateCurrentLength(ext2);
                                
                                double angle1Rad = ext1.Angle * (Math.PI / 180.0);
                                double angle2Rad = ext2.Angle * (Math.PI / 180.0);

                                ext1.ExtendedPosition = new Point(
                                    ext1.OriginalPosition.X + length1 * Math.Sin(angle1Rad),
                                    ext1.OriginalPosition.Y - length1 * Math.Cos(angle1Rad)
                                );

                                ext2.ExtendedPosition = new Point(
                                    ext2.OriginalPosition.X + length2 * Math.Sin(angle2Rad),
                                    ext2.OriginalPosition.Y - length2 * Math.Cos(angle2Rad)
                                );

                                if (pass == 1 && logAngles)
                                {
                                    _logger.LogInfo($"    Nudged angles: {ext1.Angle:F1}° and {ext2.Angle:F1}° (nudge={nudge:F1}°)");
                                }
                            }
                        }
                    }
                }

                // Second pass: Check all pairs for position overlaps (across all groups)
                for (int i = 0; i < allExtensions.Count; i++)
                {
                    for (int j = i + 1; j < allExtensions.Count; j++)
                    {
                        var ext1 = allExtensions[i];
                        var ext2 = allExtensions[j];

                        // Calculate distance between extended positions
                        double dx = ext2.ExtendedPosition.X - ext1.ExtendedPosition.X;
                        double dy = ext2.ExtendedPosition.Y - ext1.ExtendedPosition.Y;
                        double distance = Math.Sqrt(dx * dx + dy * dy);

                        // If markers would overlap or be too close
                        if (distance < minGap)
                        {
                            if (pass == 1 && logOverlaps)
                            {
                                _logger.LogInfo($"  Found overlap: {ext1.Location.Name} (Group {ext1.GroupId}) and {ext2.Location.Name} (Group {ext2.GroupId}), distance={distance:F1}px");
                            }

                            hadAdjustments = true;

                            // Calculate how much we need to separate them
                            double neededSeparation = minGap - distance;

                            // Calculate angles from original positions
                            double angle1 = ext1.Angle * (Math.PI / 180.0);
                            double angle2 = ext2.Angle * (Math.PI / 180.0);

                            // Get current lengths
                            double currentLength1 = CalculateCurrentLength(ext1);
                            double currentLength2 = CalculateCurrentLength(ext2);

                            // Strategy: Try to lengthen one and shorten the other for better separation
                            // This works better than shortening both equally
                            double newLength1, newLength2;
                            
                            // Calculate angle between the two lines
                            double angleDiff = Math.Abs(ext1.Angle - ext2.Angle);
                            if (angleDiff > 180) angleDiff = 360 - angleDiff;

                            // If lines are pointing in similar directions (< 90 degrees apart),
                            // lengthen one and shorten the other
                            double minLineLength = _visualConfig.RadialExtension.MinimumLineLength;
                            if (angleDiff < 90)
                            {
                                // Lengthen the longer one, shorten the shorter one
                                if (currentLength1 > currentLength2)
                                {
                                    newLength1 = currentLength1 + neededSeparation * 0.7;
                                    newLength2 = Math.Max(minLineLength, currentLength2 - neededSeparation * 0.3);
                                }
                                else
                                {
                                    newLength1 = Math.Max(minLineLength, currentLength1 - neededSeparation * 0.3);
                                    newLength2 = currentLength2 + neededSeparation * 0.7;
                                }
                            }
                            else
                            {
                                // Lines pointing in opposite directions - shorten both
                                double adjustmentPerMarker = neededSeparation / 2.0;
                                newLength1 = Math.Max(minLineLength, currentLength1 - adjustmentPerMarker);
                                newLength2 = Math.Max(minLineLength, currentLength2 - adjustmentPerMarker);
                            }

                            if (pass == 1 && logOverlaps)
                            {
                                _logger.LogInfo($"    Pass {pass}: Adjusting lengths: {currentLength1:F1}→{newLength1:F1}, {currentLength2:F1}→{newLength2:F1} (angleDiff={angleDiff:F1}°)");
                            }

                            // Recalculate extended positions
                            ext1.ExtendedPosition = new Point(
                                ext1.OriginalPosition.X + newLength1 * Math.Sin(angle1),
                                ext1.OriginalPosition.Y - newLength1 * Math.Cos(angle1)
                            );

                            ext2.ExtendedPosition = new Point(
                                ext2.OriginalPosition.X + newLength2 * Math.Sin(angle2),
                                ext2.OriginalPosition.Y - newLength2 * Math.Cos(angle2)
                            );
                        }
                    }
                }

                if (hadAdjustments && pass < maxPasses && logOverlaps)
                {
                    _logger.LogInfo($"  Pass {pass} complete, running another pass...");
                }

            } while (hadAdjustments && pass < maxPasses);

            if (pass > 1 && logOverlaps)
            {
                _logger.LogInfo($"[AdjustForMarkerOverlaps] Completed {pass} passes");
            }

            // Log final angles for each group
            if (logAngles)
            {
                _logger.LogInfo($"[AdjustForMarkerOverlaps] Final angles:");
                var groupedExtensions = allExtensions.GroupBy(e => e.GroupId).ToList();
                foreach (var group in groupedExtensions)
                {
                    var groupExtensions = group.OrderBy(e => e.Angle).ToList();
                    double minAngleInGroup = 360.0;
                    for (int i = 0; i < groupExtensions.Count - 1; i++)
                    {
                        double diff = groupExtensions[i + 1].Angle - groupExtensions[i].Angle;
                        if (diff < minAngleInGroup) minAngleInGroup = diff;
                    }
                    _logger.LogInfo($"  Group {group.Key}: {groupExtensions.Count} markers, smallest angle separation: {minAngleInGroup:F2}°");
                }
            }
        }

        /// <summary>
        /// Calculates the current length of an extension line.
        /// </summary>
        private double CalculateCurrentLength(RadialExtension extension)
        {
            double dx = extension.ExtendedPosition.X - extension.OriginalPosition.X;
            double dy = extension.ExtendedPosition.Y - extension.OriginalPosition.Y;
            return Math.Sqrt(dx * dx + dy * dy);
        }

        /// <summary>
        /// Animates extension lines growing from center.
        /// </summary>
        private void AnimateExtensionLines(List<Line> lines)
        {
            var duration = TimeSpan.FromMilliseconds(_visualConfig.RadialExtension.ExtensionAnimationMs);
            var easing = new QuadraticEase { EasingMode = EasingMode.EaseOut };

            for (int i = 0; i < lines.Count; i++)
            {
                var line = lines[i];

                // Store final positions
                var finalX2 = line.X2;
                var finalY2 = line.Y2;

                // Set initial positions (line starts at zero length)
                line.X2 = line.X1;
                line.Y2 = line.Y1;

                // Create animations
                var animX2 = new DoubleAnimation
                {
                    From = line.X1,
                    To = finalX2,
                    Duration = duration,
                    EasingFunction = easing,
                    BeginTime = TimeSpan.FromMilliseconds(i * 10) // Stagger by 10ms
                };

                var animY2 = new DoubleAnimation
                {
                    From = line.Y1,
                    To = finalY2,
                    Duration = duration,
                    EasingFunction = easing,
                    BeginTime = TimeSpan.FromMilliseconds(i * 10)
                };

                // Apply animations
                line.BeginAnimation(Line.X2Property, animX2);
                line.BeginAnimation(Line.Y2Property, animY2);
            }
        }

        #endregion

        #region Extension Line Hover Highlighting

        /// <summary>
        /// Highlights the extension line when mouse enters a marker.
        /// </summary>
        private void OnMarkerMouseEnter(object sender, MouseEventArgs e)
        {
            if (sender is LocationMarker marker && _markerToLineMap.TryGetValue(marker, out Line? line))
            {
                // Highlight the line - make it thicker and change to lighter red
                line.StrokeThickness = 5.0;
                line.Stroke = new SolidColorBrush(Color.FromRgb(255, 100, 100)); // Light red
                Panel.SetZIndex(line, 1999); // Just below markers but above other lines
            }
        }

        /// <summary>
        /// Restores the extension line to normal when mouse leaves a marker.
        /// </summary>
        private void OnMarkerMouseLeave(object sender, MouseEventArgs e)
        {
            if (sender is LocationMarker marker && _markerToLineMap.TryGetValue(marker, out Line? line))
            {
                // Restore normal appearance
                line.StrokeThickness = 3.0;
                line.Stroke = new SolidColorBrush(Colors.Red);
                Panel.SetZIndex(line, 0); // Back to default layer
            }
        }

        #endregion
    }
}
