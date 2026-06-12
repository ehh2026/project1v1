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
    /// <summary>
    /// Main window that hosts all UI components and manages application lifecycle.
    /// Uses viewport-based rendering for efficient zoom/pan operations.
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly IContentLoader _contentLoader;
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
        private IExtensionLineRenderer _extensionLineRenderer = null!;
        private RadialExtensionCalculator? _extensionCalculator;
        private RadialExtensionAdjuster? _adjuster;
        private MarkerPlacementOrchestrator _placementOrchestrator = null!;
        private InteractionMode _mode = InteractionMode.Normal;
        
        // Manual layout editor support
        private LayoutEditorController _layoutEditor = null!;
        private LocationMarker? _draggedMarker = null;
        private Point _dragStartPosition;
        private IManualLayoutManager? _layoutManager;
        private LocationCluster? _currentZoomedCluster = null;
        private ManualLayout? _savedLayoutToApply = null;
        private bool _isFullMapLayoutSession = false;
        
        // Map image dimensions
        private const double ImageWidth = 8198.0;
        private const double ImageHeight = 5542.0;
        
        // Visual configuration
        private VisualConfig _visualConfig = new VisualConfig();
        
        private Dictionary<string, PinPartGeometryEntry>? _pinPartGeometry;
        private readonly Dictionary<string, BitmapSource> _pinPartBitmapCache = new Dictionary<string, BitmapSource>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<LocationMarker, MarkerVisualState> _baseMarkerVisuals = new Dictionary<LocationMarker, MarkerVisualState>();
        private readonly CompositePinPlanningService _compositePinPlanningService =
            new CompositePinPlanningService(new PinPartPlacementCalculator(), new CompositePinRenderPlanBuilder());
        private readonly ManualLayoutAssignmentEnricher _assignmentEnricher = new ManualLayoutAssignmentEnricher();
        private readonly CompositePinShaftMenuModelBuilder _shaftMenuModelBuilder =
            new CompositePinShaftMenuModelBuilder(new PinPartPlacementCalculator());
        private readonly ManualLayoutOverrideStore _overrideStore = new ManualLayoutOverrideStore();
        private readonly CompositePinDepthSorter _compositePinDepthSorter = new CompositePinDepthSorter();
        private readonly CompositePinTargetBuilder _compositePinTargetBuilder = new CompositePinTargetBuilder();

        // Phase 4: composite render-plan disk cache
        private CompositePinPlanCache _compositePinPlanCache = null!;
        private CompositePinApplicationService _planApplicationService = null!;
        private string? _pinPartGeometryHash;

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

        private sealed class MarkerVisualState
        {
            public object? Content { get; init; }
            public double Width { get; init; }
            public double Height { get; init; }
        }

        private enum InteractionMode
        {
            Normal,
            Animating,
            Editing
        }

        private bool IsAnimating => _mode == InteractionMode.Animating;

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
                var visualConfigService = new VisualConfigService();
                _visualConfig = visualConfigService.Load(configPath);
                _logger.LogInfo($"Visual config loaded from: {configPath}");

                if (_visualConfig.Debug.WindowedMode)
                {
                    WindowStyle = WindowStyle.SingleBorderWindow;
                    WindowState = WindowState.Normal;
                    Width  = _visualConfig.Debug.WindowedWidth;
                    Height = _visualConfig.Debug.WindowedHeight;
                    _logger.LogInfo($"[Debug] WindowedMode: {Width}x{Height}");
                }

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
                
                _contentLoader = new ContentLoader(_logger);
                _contentLoader.ClusterDistanceThreshold = _visualConfig.ClusterDistanceThreshold;
                _logger.LogInfo("ContentLoader created");
                
                // Initialize radial extension calculator if enabled
                if (_visualConfig.RadialExtension.Enabled)
                {
                    _extensionCalculator = new RadialExtensionCalculator(_visualConfig.RadialExtension);
                    _logger.LogInfo("RadialExtensionCalculator initialized");
                }

                _adjuster = new RadialExtensionAdjuster(_logger, _visualConfig);
                _placementOrchestrator = new MarkerPlacementOrchestrator(
                    _visualConfig, _logger, _extensionCalculator, _adjuster);
                _extensionLineRenderer = new ExtensionLineRenderer(MapDisplay.Markers, _visualConfig, _logger.LogInfo, _logger.LogWarning);

                var configuredLayoutPath = _visualConfig.ManualLayoutEditor.LayoutStoragePath;
                var layoutFilePath = IOPath.IsPathRooted(configuredLayoutPath)
                    ? configuredLayoutPath
                    : IOPath.Combine(AppDomain.CurrentDomain.BaseDirectory, configuredLayoutPath);

                // Initialize manual layout manager if enabled OR if we need to load layouts
                if (_visualConfig.ManualLayoutEditor.Enabled)
                {
                    _layoutManager = new ManualLayoutManager(layoutFilePath, _logger);
                    _logger.LogInfo($"ManualLayoutManager initialized (edit mode enabled) at: {layoutFilePath}");
                }
                else
                {
                    // Always initialize layout manager for loading saved layouts, even if editing is disabled
                    _layoutManager = new ManualLayoutManager(layoutFilePath, _logger);
                    _logger.LogInfo($"ManualLayoutManager initialized (read-only for loading saved layouts) at: {layoutFilePath}");
                }
                
                _layoutEditor = new LayoutEditorController(_layoutManager!, _visualConfig, _logger);
                WireLayoutEditorEvents();

                _navigationService = new MapNavigationService();
                _logger.LogInfo("MapNavigationService created");

                _viewportCalculator = new ViewportCalculator();
                _logger.LogInfo("ViewportCalculator created");
                
                _frameCache = new AnimationFrameCache(_logger);
                _logger.LogInfo("AnimationFrameCache created");

                // Initialize zoomed region cache with the full-resolution source image.
                _zoomedRegionCache = new ZoomedRegionCache(_logger, _contentLoader.GetFullResolutionWorldMapPath());
                _logger.LogInfo("ZoomedRegionCache created");

                // Phase 4: composite render-plan disk cache
                _compositePinPlanCache  = new CompositePinPlanCache(_logger);
                _planApplicationService = new CompositePinApplicationService(_compositePinPlanCache, _compositePinPlanningService);
                _logger.LogInfo("CompositePinPlanCache created");

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
        /// Adds cluster and individual markers to the map canvas.
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
            TryApplyFullMapManualLayout();
            UpdateEditLayoutButtonVisibility();
        }

        /// <summary>
        /// Adds an individual location marker to the canvas using viewport coordinates.
        /// </summary>
        private LocationMarker AddIndividualMarker(Location location)
        {
            LocationMarker marker;
            
            // Use pin markers if enabled in config
            if (_visualConfig.UsePinMarkers)
            {
                marker = CreatePinMarker(location);
                _logger.LogInfo($"  Created PIN marker for '{location.Name}' with size {marker.Width}x{marker.Height}");
            }
            else
            {
                marker = new LocationMarker(_visualConfig) { Location = location };
                _logger.LogInfo($"  Created REGULAR marker for '{location.Name}'");
            }
            
            // Position will be updated by UpdateMarkerPositions()
            Canvas.SetLeft(marker, 0);
            Canvas.SetTop(marker, 0);
            CaptureBaseMarkerVisual(marker);
            
            // Add click handler
            marker.MouseLeftButtonDown += (s, e) =>
            {
                var action = MarkerMouseDownPolicy.GetIndividualMarkerAction(_layoutEditor.IsEditMode);
                if (action == MarkerMouseDownAction.AllowEditDrag)
                {
                    return;
                }
                
                AnimateMarkerClick(marker);
                
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
            var marker = new ClusterMarker(_visualConfig) { Cluster = cluster };
            var stamp = _contentLoader.TryLoadContentBitmap(ContentFileNames.ClusterStampFileName);
            marker.ApplyStampImage(stamp);
            marker.UpdateDisplay();
            
            // Position will be updated by UpdateMarkerPositions()
            Canvas.SetLeft(marker, 0);
            Canvas.SetTop(marker, 0);
            
            // Add click handler
            marker.MouseLeftButtonDown += (s, e) =>
            {
                var action = MarkerMouseDownPolicy.GetClusterMarkerAction(_layoutEditor.IsEditMode);
                if (action == MarkerMouseDownAction.BlockNavigation)
                {
                    ShowEditModeNavigationBlockedStatus();
                    e.Handled = true;
                    return;
                }

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

            if (!IsAnimating)
                _extensionLineRenderer.Clear();

            RestoreBaseMarkerVisuals();

            var visibleIndividuals = _individualMarkers
                .Where(m => m.Visibility == Visibility.Visible)
                .Select(m => (m.Location, m.Location.PixelX, m.Location.PixelY))
                .ToList();

            var visibleClusterCenters = _clusterMarkers
                .Where(m => m.Visibility == Visibility.Visible && m.Cluster != null)
                .Select(m => m.Cluster!.CenterPoint)
                .ToList();

            var plan = _placementOrchestrator.Compute(
                viewport,
                containerWidth,
                containerHeight,
                IsAnimating,
                visibleIndividuals,
                visibleClusterCenters);

            _denseGroups = plan.ExtensionGroups.ToList();

            if (plan.Mode == MarkerPlacementMode.WithExtensions)
            {
                foreach (var group in plan.ExtensionGroups)
                {
                    _extensionLineRenderer.Apply(
                        group,
                        viewport,
                        containerWidth,
                        containerHeight,
                        _individualMarkers,
                        (m, orig, ext) => TryApplyCompositePinMarker(m, orig, ext));
                }
            }

            ApplyIndividualPlacements(plan.IndividualPlacements);
            ApplyClusterPlacements(plan.ClusterPlacements);
            ApplyCompositePinsToNormalPlacements(plan.IndividualPlacements, viewport, containerWidth, containerHeight);
            ApplyCompositePinDepthSort();
        }

        private void ApplyIndividualPlacements(IReadOnlyList<MarkerScreenPlacement> placements)
        {
            foreach (var placement in placements)
            {
                var marker = _individualMarkers.FirstOrDefault(
                    m => string.Equals(m.Location.Name, placement.LocationName, StringComparison.Ordinal));
                if (marker == null)
                    continue;

                Canvas.SetLeft(marker, placement.Left);
                Canvas.SetTop(marker, placement.Top);
            }
        }

        private void ApplyClusterPlacements(IReadOnlyList<ClusterScreenPlacement> placements)
        {
            var visibleClusters = _clusterMarkers
                .Where(m => m.Visibility == Visibility.Visible)
                .ToList();

            for (int i = 0; i < placements.Count && i < visibleClusters.Count; i++)
            {
                var placement = placements[i];
                var marker = visibleClusters[i];
                Canvas.SetLeft(marker, placement.Left);
                Canvas.SetTop(marker, placement.Top);
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
            _baseMarkerVisuals.Clear();
            
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
            if (_layoutEditor.IsEditMode)
            {
                ShowEditModeNavigationBlockedStatus();
                return;
            }

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


        private void OnKeyDown(object sender, KeyEventArgs e)
        {
            // Handle Escape key to close subwindow, exit edit mode, go back, or exit application
            if (e.Key == Key.Escape)
            {
                if (_activeSubwindow != null)
                {
                    CloseActiveSubwindow();
                }
                else if (_layoutEditor.IsEditMode)
                {
                    // Exit edit mode on Escape
                    ExitEditMode();
                    if (_visualConfig.ManualLayoutEditor.Enabled)
                    {
                        EditLayoutButton.Visibility = Visibility.Visible;
                    }
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
            // Handle Ctrl+S to save layout in edit mode
            else if (e.Key == Key.S && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            {
                if (_layoutEditor.IsEditMode)
                {
                    OnSaveLayoutButtonClick(this, new RoutedEventArgs());
                    e.Handled = true;
                }
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
                TryApplyFullMapManualLayout();
                UpdateEditLayoutButtonVisibility();
            }
        }


        private void ShowError(string message)
        {
            MessageBox.Show(message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
