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
        private readonly IContentSetResolver _contentSetResolver;
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
        private readonly Dictionary<LocationMarker, Vector> _animationOffsets = new();
        // 2.2: visible-marker projections are constant across a single zoom animation, so they are
        // cached here for its duration and rebuilt (and cleared) on the first non-animating pass.
        private List<(Location Location, double PixelX, double PixelY)>? _animVisibleIndividuals;
        private List<Point>? _animVisibleClusterCenters;
        private RadialExtensionCalculator? _extensionCalculator;
        private RadialExtensionAdjuster? _adjuster;
        private MarkerPlacementOrchestrator _placementOrchestrator = null!;
        private InteractionMode _mode = InteractionMode.Normal;
        private Location? _autoOpenLocation = null;

        // Manual layout editor support
        private LayoutEditorController _layoutEditor = null!;
        private LocationMarker? _draggedMarker = null;
        private Point _dragStartPosition;
        private IManualLayoutManager? _layoutManager;
        private LocationCluster? _currentZoomedCluster = null;
        private ManualLayout? _savedLayoutToApply = null;
        private bool _isFullMapLayoutSession = false;

        // Map image dimensions — single source of truth via MapMetadata (display space).
        private MapMetadata _mapMetadata = MapMetadata.CreateDefault();
        private double ImageWidth => _mapMetadata.DisplayWidth;
        private double ImageHeight => _mapMetadata.DisplayHeight;

        // Visual configuration
        private VisualConfig _visualConfig = new VisualConfig();
        private DrawnPinMarkerFactory _drawnPinFactory = null!;
        private readonly VisualConfigService _configService;
        private readonly string _configPath;
        private readonly string _defaultConfigPath;

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
        private bool AreDeveloperToolsEnabled() => _visualConfig.EnableDeveloperTools;

        public MainWindow()
        {
            try
            {
                InitializeComponent();

                // Initialize services
                _logger = new FileLogger();
                _configService = new VisualConfigService(message => _logger.LogWarning(message));
                _logger.LogInfo("=== MainWindow Constructor Started ===");

                // Load visual configuration: user file overlaid on shipped defaults so local
                // tuning survives updates (see docs/guides/VISUAL_CONFIG.md).
                _configPath = IOPath.Combine(AppDomain.CurrentDomain.BaseDirectory, "visual-config.json");
                _defaultConfigPath = IOPath.Combine(AppDomain.CurrentDomain.BaseDirectory, "visual-config.default.json");
                _visualConfig = _configService.Load(_configPath, _defaultConfigPath);
                _drawnPinFactory = new DrawnPinMarkerFactory(_visualConfig);
                _logger.LogInfo($"Visual config loaded from: {_configPath}");

                if (AreDeveloperToolsEnabled() && _visualConfig.Debug.WindowedMode)
                {
                    WindowStyle = WindowStyle.SingleBorderWindow;
                    WindowState = WindowState.Normal;
                    Width = _visualConfig.Debug.WindowedWidth;
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

                _contentSetResolver = new ContentSetResolver();
                _contentLoader = new ContentLoader(_logger, _contentSetResolver);
                _contentLoader.ClusterDistanceThreshold = _visualConfig.ClusterDistanceThreshold;
                _contentLoader.MaxCachedLocations = _visualConfig.MaxCachedLocations;
                _contentLoader.MaxDecodePixelWidth = _visualConfig.ContentImages.MaxDecodePixelWidth;
                _contentLoader.MaxDecodePixelHeight = _visualConfig.ContentImages.MaxDecodePixelHeight;
                _contentLoader.LargeImageWarnBytes = _visualConfig.ContentImages.LargeImageWarnBytes;
                _contentLoader.EnableImageDiagnostics =
                    AreDeveloperToolsEnabled() && _visualConfig.Debug.LogContentImageDiagnostics;
                _contentLoader.LargeImageDetected += OnLargeContentImageDetected;
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
                var layoutFilePath = ResolveLayoutStoragePath(configuredLayoutPath);

                // Initialize manual layout manager if enabled OR if we need to load layouts
                if (AreDeveloperToolsEnabled() && _visualConfig.ManualLayoutEditor.Enabled)
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
                _zoomedRegionCache = new ZoomedRegionCache(
                    _logger,
                    _contentLoader.GetFullResolutionWorldMapPath(),
                    _contentLoader.GetWorldMapPath());
                _logger.LogInfo("ZoomedRegionCache created");

                // Phase 4: composite render-plan disk cache
                _compositePinPlanCache = new CompositePinPlanCache(_logger);
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

                SetupTuningPanel();

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
                    var errorMsg = $"Content folder validation failed.\nExpected path: {_contentLoader.ContentFolderPath}\nPlease ensure Images&Content/Demo-Content (or Production-Content) exists with a coordinate source (locations.json or 'Coordinates for map.xlsx') and that Images&Content/Assets/ contains the world map image.";
                    _logger.LogError(errorMsg);
                    ShowError(errorMsg);
                    return;
                }
                _logger.LogInfo("Content folder validation passed");

                // Load map image
                _logger.LogInfo("Step 2: Loading world map image");
                var mapImage = await _contentLoader.LoadMapImageAsync();
                _mapMetadata = MapMetadata.FromDisplayBitmap(mapImage);
                _logger.LogInfo(
                    $"Map image loaded ({_mapMetadata.DisplayWidth}x{_mapMetadata.DisplayHeight}), calling MapDisplay.LoadMapImage");

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

            if (!_visualConfig.UsePinMarkers)
            {
                marker.MouseLeftButtonDown += (_, e) =>
                {
                    HandleIndividualMarkerPrimaryAction(marker);
                    e.Handled = true;
                };
            }

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

            _clusterMarkers.Add(marker);
            MapDisplay.Markers.Children.Add(marker);

            _logger.LogInfo($"  Cluster marker ({cluster.Count} locations) added at source ({cluster.CenterPoint.X:F2}, {cluster.CenterPoint.Y:F2})");
        }

        /// <summary>
        /// Resolves the read-write layout store path. A rooted configured path is honored as-is.
        /// A relative path is treated as the bundled (app-folder) seed; user layouts then live in a
        /// stable per-user location (<c>%AppData%/InteractiveWorldMap</c>) so that rebuilding or
        /// cleaning the app output never discards saved layouts. The bundled seed is copied in once
        /// when no user file exists yet. Falls back to the bundled path if the user location is
        /// unavailable, so startup never fails over layout storage.
        /// </summary>
        private string ResolveLayoutStoragePath(string configuredLayoutPath)
        {
            if (IOPath.IsPathRooted(configuredLayoutPath))
                return configuredLayoutPath;

            var activeSetPath = _contentLoader.ActiveContentSetPath;
            var bundledPath = IOPath.Combine(activeSetPath, "manual-layouts.json");
            var userDir = IOPath.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "InteractiveWorldMap");

            var setSuffix = _contentLoader.ActiveContentSetKind.ToSuffix();
            var userFileName = _visualConfig.ManualLayoutEditor.SetAwareStorage
                ? $"manual-layouts.{setSuffix}.json"
                : "manual-layouts.json";

            var userPath = IOPath.Combine(userDir, userFileName);

            try
            {
                System.IO.Directory.CreateDirectory(userDir);

                if (_visualConfig.ManualLayoutEditor.SetAwareStorage && setSuffix == "demo")
                {
                    var oldUserPath = IOPath.Combine(userDir, "manual-layouts.json");
                    if (System.IO.File.Exists(oldUserPath) && !System.IO.File.Exists(userPath))
                    {
                        System.IO.File.Copy(oldUserPath, userPath);
                        _logger.LogInfo($"Migrated un-namespaced layout file to namespaced demo layout file: {userPath}");
                    }
                }

                if (!System.IO.File.Exists(userPath) && System.IO.File.Exists(bundledPath))
                {
                    System.IO.File.Copy(bundledPath, userPath);
                    _logger.LogInfo($"Seeded user layout store from bundled layouts: {userPath}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    $"Could not prepare user layout store ({ex.Message}); using bundled path {bundledPath}. " +
                    "Layouts may not persist across a rebuild.");
                return bundledPath;
            }

            return userPath;
        }

        /// <summary>
        /// Handles clicks outside the subwindow.
        /// </summary>
        public void HandleOutsideClick(Point clickPosition)
        {
            if (!HasActiveContentWindows())
                return;

            var screenPoint = PointToScreen(clickPosition);
            if (!IsInsideActiveContentWindow(screenPoint))
                CloseActiveSubwindow();
        }

        private async void OnWindowLoaded(object sender, RoutedEventArgs e)
        {
            ApplyDisplayBasedImageDecodeCap();
            await InitializeAsync();
        }

        // Safety floor for the content-image decode box (4K UHD). Used when neither the display metrics
        // nor the config supply a positive cap, so a very large image can never be decoded unbounded
        // (which is what hung the UI on big TIFFs).
        private const int FallbackDecodePixelWidth = 3840;
        private const int FallbackDecodePixelHeight = 2160;

        /// <summary>
        /// Sizes the content-image decode box from the configured caps and the display's physical
        /// resolution: per dimension it takes the smaller of the two (so an operator's smaller cap is
        /// honored, but the box never exceeds what the screen can show), and floors each dimension to
        /// a 4K bound so it is never left unbounded — even if display detection fails or a config leaves
        /// one dimension at 0. Runs once the window is loaded, when a valid DPI context exists.
        /// <para>
        /// Multi-monitor: the full-screen window's own size (<see cref="FrameworkElement.ActualWidth"/>/
        /// <see cref="FrameworkElement.ActualHeight"/> in DIPs) reflects whichever monitor it is on, so
        /// the box tracks the actual gallery display rather than assuming the primary screen. It falls
        /// back to the primary screen only if the window has not been sized yet.
        /// </para>
        /// </summary>
        private void ApplyDisplayBasedImageDecodeCap()
        {
            int displayWidth = 0, displayHeight = 0;
            try
            {
                var dpi = VisualTreeHelper.GetDpi(this);

                // Prefer the window's actual size (the monitor it is displayed on) over the primary
                // screen, so a kiosk running on a secondary display is sized correctly.
                var dipWidth = ActualWidth > 0 ? ActualWidth : SystemParameters.PrimaryScreenWidth;
                var dipHeight = ActualHeight > 0 ? ActualHeight : SystemParameters.PrimaryScreenHeight;

                if (PhysicalPixelSizeCalculator.TryCalculate(
                        dipWidth,
                        dipHeight,
                        dpi.DpiScaleX,
                        dpi.DpiScaleY,
                        out displayWidth,
                        out displayHeight))
                {
                    _logger.LogInfo($"Display physical size for content decode box: {displayWidth}x{displayHeight}");
                }
                else
                {
                    _logger.LogWarning("Display size unavailable; content decode box uses config/floor only.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"Could not read display size for content decode box ({ex.Message}); using config/floor.");
            }

            var config = _visualConfig.ContentImages;
            _contentLoader.MaxDecodePixelWidth =
                ImageDecodeMath.ResolveDecodeCap(config.MaxDecodePixelWidth, displayWidth, FallbackDecodePixelWidth);
            _contentLoader.MaxDecodePixelHeight =
                ImageDecodeMath.ResolveDecodeCap(config.MaxDecodePixelHeight, displayHeight, FallbackDecodePixelHeight);
            _logger.LogInfo(
                $"Content image decode box: {_contentLoader.MaxDecodePixelWidth}x{_contentLoader.MaxDecodePixelHeight}");
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
            if (HasActiveContentWindows())
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
            // Escape: content subwindow → edit mode → tuning panel → zoom back → exit app
            if (e.Key == Key.Escape)
            {
                if (HasActiveContentWindows())
                {
                    CloseActiveSubwindow();
                    e.Handled = true;
                }
                else if (_layoutEditor.IsEditMode)
                {
                    ExitEditMode();
                    if (AreDeveloperToolsEnabled() && _visualConfig.ManualLayoutEditor.Enabled)
                    {
                        EditLayoutButton.Visibility = Visibility.Visible;
                    }
                    e.Handled = true;
                }
                else if (IsTuningPanelVisible)
                {
                    HideTuningPanel();
                    e.Handled = true;
                }
                else if (_navigationService.CanGoBack)
                {
                    AnimateZoomOut();
                    e.Handled = true;
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
            else if (e.Key == Key.F12 && AreDeveloperToolsEnabled() && _visualConfig.Debug.EnableTuningPanel)
            {
                if (IsTuningPanelVisible)
                    HideTuningPanel();
                else
                    OnTuningPanelToggleClick(this, new RoutedEventArgs());
                e.Handled = true;
            }
        }



        private void OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (!HasActiveContentWindows())
                return;

            var position = e.GetPosition(this);
            var screenPoint = PointToScreen(position);

            if (IsInsideActiveContentWindow(screenPoint))
                return;

            if (IsClickOnMarkerTarget(e))
                return;

            CloseActiveSubwindow();
            e.Handled = true;
        }

        private bool HasActiveContentWindows() =>
            _activeSubwindow != null ||
            _activeThumbnailBrowser != null ||
            _activeDidacticWindow != null;

        private bool IsInsideActiveContentWindow(Point screenPoint) =>
            IsPointInsideVisibleWindow(_activeSubwindow, screenPoint) ||
            IsPointInsideVisibleWindow(_activeThumbnailBrowser, screenPoint) ||
            IsPointInsideVisibleWindow(_activeDidacticWindow, screenPoint);

        private static bool IsPointInsideVisibleWindow(Window? window, Point screenPoint)
        {
            if (window?.IsVisible != true)
                return false;

            var width = window.ActualWidth > 0 ? window.ActualWidth : window.Width;
            var height = window.ActualHeight > 0 ? window.ActualHeight : window.Height;
            if (!double.IsFinite(width) || !double.IsFinite(height) || width <= 0 || height <= 0)
                return false;

            return new Rect(window.Left, window.Top, width, height).Contains(screenPoint);
        }

        private bool IsClickOnMarkerTarget(MouseButtonEventArgs e)
        {
            var targetPosition = e.GetPosition(MapDisplay.MarkerInteractions);
            if (VisualTreeHelper.HitTest(MapDisplay.MarkerInteractions, targetPosition) != null)
                return true;

            var markerPosition = e.GetPosition(MapDisplay.Markers);
            var hitResult = VisualTreeHelper.HitTest(MapDisplay.Markers, markerPosition);
            return IsMarkerVisual(hitResult?.VisualHit);
        }

        private static bool IsMarkerVisual(DependencyObject? visual)
        {
            while (visual != null)
            {
                if (visual is LocationMarker or ClusterMarker)
                    return true;

                visual = VisualTreeHelper.GetParent(visual);
            }

            return false;
        }

        private void OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            // Viewport handles size changes automatically in MapDisplayControl
            // Just update marker positions if we have a viewport
            if (MapDisplay.CurrentViewport != null)
            {
                // Never re-place markers mid-edit. UpdateMarkerPositions clears the renderer's
                // marker-to-line map, which is the only record of where each pin actually points;
                // a save landing before it refills would persist every pin as a stub. Zoom is
                // already blocked during edit, so a resize is the one way in.
                if (_layoutEditor.IsEditMode)
                {
                    UpdateEditLayoutButtonVisibility();
                    return;
                }

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
