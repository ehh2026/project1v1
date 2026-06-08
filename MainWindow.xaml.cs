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
        private InteractionMode _mode = InteractionMode.Normal;
        
        // Manual layout editor support
        private LayoutEditorController _layoutEditor = null!;
        private LocationMarker? _draggedMarker = null;
        private Point _dragStartPosition;
        private IManualLayoutManager? _layoutManager;
        private LocationCluster? _currentZoomedCluster = null;
        private ManualLayout? _savedLayoutToApply = null;
        
        // Map image dimensions
        private const double ImageWidth = 8198.0;
        private const double ImageHeight = 5542.0;
        
        // Visual configuration
        private VisualConfig _visualConfig = new VisualConfig();
        
        // Master pin image for image-based pins
        private BitmapSource? _masterPinImage;
        private Dictionary<string, PinPartGeometryEntry>? _pinPartGeometry;
        private readonly Dictionary<string, BitmapSource> _pinPartBitmapCache = new Dictionary<string, BitmapSource>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<LocationMarker, MarkerVisualState> _baseMarkerVisuals = new Dictionary<LocationMarker, MarkerVisualState>();
        private readonly CompositePinPlanningService _compositePinPlanningService =
            new CompositePinPlanningService(new PinPartPlacementCalculator(), new CompositePinRenderPlanBuilder());
        private readonly ManualLayoutAssignmentEnricher _assignmentEnricher = new ManualLayoutAssignmentEnricher();
        private readonly CompositePinShaftMenuModelBuilder _shaftMenuModelBuilder =
            new CompositePinShaftMenuModelBuilder(new PinPartPlacementCalculator());
        private readonly ManualLayoutOverrideStore _overrideStore = new ManualLayoutOverrideStore();

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

        private void WireLayoutEditorEvents()
        {
            _layoutEditor.EditModeEntered += () =>
            {
                _mode = InteractionMode.Editing;
                EditLayoutButton.Visibility = Visibility.Collapsed;
                EditModePanel.Visibility = Visibility.Visible;
                UpdateOverrideIndicator(); // hide indicator while in edit mode
            };

            _layoutEditor.EditModeExited += () =>
            {
                if (_mode == InteractionMode.Editing)
                {
                    _mode = InteractionMode.Normal;
                }

                EditModePanel.Visibility = Visibility.Collapsed;
                if (_visualConfig.ManualLayoutEditor.Enabled)
                {
                    EditLayoutButton.Visibility = Visibility.Visible;
                }
                UpdateOverrideIndicator(); // re-evaluate indicator now that edit mode is off
            };

            _layoutEditor.ManualLayoutActivityChanged += isActive =>
            {
                ManualLayoutIndicator.Visibility =
                    isActive && _visualConfig.ManualLayoutEditor.ShowLayoutIndicator
                        ? Visibility.Visible
                        : Visibility.Collapsed;
            };
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
                // Don't handle clicks in edit mode (dragging takes precedence)
                if (_layoutEditor.IsEditMode)
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
        /// Creates a pin-style marker for a location.
        /// </summary>
        private LocationMarker CreatePinMarker(Location location)
        {
            // Check if image-based pins are enabled and available
            if (_visualConfig.PinImages.Enabled && _visualConfig.PinImages.Pins.Count > 0)
            {
                return CreateImagePinMarker(location);
            }
            
            // Fallback to drawn pin markers
            return CreateDrawnPinMarker(location);
        }

        /// <summary>
        /// Creates an image-based pin marker for a location.
        /// </summary>
        private LocationMarker CreateImagePinMarker(Location location)
        {
            try
            {
                // Load master image if not already loaded
                if (_masterPinImage == null)
                {
                    LoadMasterPinImage();
                }

                if (_masterPinImage == null)
                {
                    _logger.LogError("Failed to load master pin image, falling back to drawn pins");
                    return CreateDrawnPinMarker(location);
                }

                // Select a random pin variant
                var pinInfo = ImagePinMarker.SelectRandomPin(_visualConfig.PinImages);
                
                // Create cropped image for this pin
                var croppedPin = ImagePinMarker.CropPinFromMaster(_masterPinImage, pinInfo);
                
                // Create the image pin marker
                var imagePinMarker = new ImagePinMarker { Location = location };
                imagePinMarker.SetPinImage(croppedPin, pinInfo, _visualConfig.PinImages.ScaleFactor);
                
                // Create LocationMarker wrapper
                var marker = new LocationMarker(_visualConfig) { Location = location };
                marker.Content = imagePinMarker;
                marker.Width = imagePinMarker.Width;
                marker.Height = imagePinMarker.Height;
                marker.Tag = imagePinMarker; // Store reference for later access
                
                _logger.LogInfo($"Created image pin marker '{pinInfo.Id}' for location '{location.Name}'");
                
                return marker;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error creating image pin marker: {ex.Message}");
                return CreateDrawnPinMarker(location);
            }
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
                {
                    pinMarker.SetPinColor(defaultColor);
                }
            }
            
            // Create LocationMarker wrapper
            var marker = new LocationMarker(_visualConfig) { Location = location };
            marker.Content = pinMarker;
            marker.Width = pinMarker.Width;
            marker.Height = pinMarker.Height;
            marker.Tag = pinMarker; // Store reference for later access
            
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
            if (!IsAnimating)
            {
                _extensionLineRenderer.Clear();
            }

            // Skip radial extension logic entirely during animation
            // Extensions will be applied after animation completes
            if (IsAnimating)
            {
                RestoreBaseMarkerVisuals();

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

            RestoreBaseMarkerVisuals();

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

                    // Second pass: Iteratively adjust for overlaps and intersections until stable
                    if (allExtensions.Any())
                    {
                        _adjuster!.AdjustExtensions(allExtensions, _visualConfig.LocationMarkerSize);
                    }

                    // Third pass: Apply the extensions (now with adjusted lengths)
                    foreach (var group in _denseGroups.Where(g => g.Extensions.Any()))
                    {
                        _extensionLineRenderer.Apply(group, viewport, containerWidth, containerHeight, _individualMarkers,
                            (m, orig, ext) => TryApplyCompositePinMarker(m, orig, ext));
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
                
                // Track current cluster for edit mode
                _currentZoomedCluster = cluster;
                
                var viewport = MapDisplay.CurrentViewport;
                if (viewport != null)
                {
                    _logger.LogInfo($"  Current viewport: ({viewport.ViewportX:F2}, {viewport.ViewportY:F2}) {viewport.ViewportWidth:F2}x{viewport.ViewportHeight:F2}, zoom={viewport.ZoomLevel:F2}");
                    
                    // Generate layout key and try to load saved layout
                    if (_layoutManager != null && _visualConfig.RadialExtension.Enabled)
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

                // Show only individual markers for this cluster (calculated positions)
                ShowOnlyIndividualMarkers(cluster);
                
                // Apply saved manual layout if one was found
                if (_savedLayoutToApply != null)
                {
                    ApplyManualLayout(_savedLayoutToApply);
                    _layoutEditor.SetManualLayoutActive(true);
                    _savedLayoutToApply = null; // Clear after applying
                    
                    _logger.LogInfo("Manual layout applied after high-res region loaded");
                }
                
                // Show Edit button if enabled and no manual layout is active
                if (_visualConfig.ManualLayoutEditor.Enabled && !_layoutEditor.IsManualLayoutActive)
                {
                    EditLayoutButton.Visibility = Visibility.Visible;
                }
                else if (_visualConfig.ManualLayoutEditor.Enabled && _layoutEditor.IsManualLayoutActive)
                {
                    // Show edit button even when manual layout is active
                    EditLayoutButton.Visibility = Visibility.Visible;
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

                AnimateViewportTransition(startViewport, targetViewport, "Zoom-out animation", () =>
                {
                    ShowClusterView();

                    if (!_navigationService.CanGoBack)
                    {
                        _logger.LogInfo("  Hiding Back button (at root level)");
                        BackButton.Visibility = Visibility.Collapsed;
                    }

                    _layoutEditor.ExitEditMode();
                    _layoutEditor.SetManualLayoutActive(false);
                    _layoutEditor.SetLayoutKey(null);
                    _currentZoomedCluster = null;
                    _overrideStore.ClearAll();
                    EditLayoutButton.Visibility = Visibility.Collapsed;
                    EditModePanel.Visibility = Visibility.Collapsed;
                    ManualLayoutIndicator.Visibility = Visibility.Collapsed;
                    OverridePendingIndicator.Visibility = Visibility.Collapsed;
                });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error zooming out: {ex.Message}\n{ex.StackTrace}");
            }
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
            Action onAnimationComplete)
        {
            const int keyframeCount = 30;
            var prerenderedFrames = PreRenderKeyframes(startViewport, targetViewport, keyframeCount, out var keyframeProgress);

            // Display first frame immediately to avoid visible delay before the loop starts
            MapDisplay.DisplayImage.Source = prerenderedFrames[0];
            MapDisplay.SetCurrentViewport(startViewport);
            UpdateMarkerPositions();

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

                    onAnimationComplete();
                }
            };

            CompositionTarget.Rendering += renderHandler;
            _logger.LogInfo($"=== {animationLabel} STARTED (Viewport) ===");
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
        /// Finds the LocationMarker for a given Location.
        /// </summary>
        private LocationMarker? FindMarkerForLocation(Location location)
        {
            return _individualMarkers.FirstOrDefault(m => m.Location == location);
        }

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
                case ImagePinMarker imagePinMarker:
                    imagePinMarker.AnimateClick();
                    _logger.LogInfo($"Animated IMAGE PIN marker click for '{marker.Location.Name}'");
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
                   _visualConfig.PinImages.Enabled &&
                   _visualConfig.PinParts.Enabled &&
                   _visualConfig.PinParts.UseCompositeRendering &&
                   !_layoutEditor.IsEditMode;
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
            return ApplyCompositePinToMarker(marker, originalScreenPos, extendedScreenPos, preferredPairId, preferredHeadSourcePath);
        }

        /// <summary>
        /// Core composite-pin apply logic. Used both by the normal (non-edit) path via
        /// <see cref="TryApplyCompositePinMarker"/> and by Reassign Pins which bypasses the
        /// edit-mode gate in <see cref="CanUseCompositePins"/>.
        /// </summary>
        private bool ApplyCompositePinToMarker(LocationMarker marker, Point originalScreenPos, Point extendedScreenPos,
            string? preferredPairId = null, string? preferredHeadSourcePath = null)
        {
            if (!EnsurePinPartGeometryLoaded() || _pinPartGeometry == null)
                return false;

            if (!_baseMarkerVisuals.TryGetValue(marker, out var baseState) || baseState.Content is not ImagePinMarker)
                return false;

            try
            {
                var target = new PinPlacementTarget
                {
                    StartScreen = originalScreenPos,
                    EndScreen = extendedScreenPos,
                    LocationId = marker.Location.Name,
                    GroupId = 0
                };

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

                ApplyRenderPlanToMarker(marker, originalScreenPos, extendedScreenPos, planning.RenderPlan, shaftImage, headImage);
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
            compositeMarker.SetCompositeImages(shaftImage, headImage, plan, _visualConfig.Debug.ShowCompositePinDebugOverlay);
            compositeMarker.ShaftOverrideRequested += locName => OnShaftOverrideRequested(marker, locName);
            _overrideStore.RecordEndpoints(marker.Location.Name, originalScreenPos, extendedScreenPos);
            marker.Content = compositeMarker;
            marker.Width   = compositeMarker.Width;
            marker.Height  = compositeMarker.Height;
            Panel.SetZIndex(marker, 2000);
            Canvas.SetLeft(marker, originalScreenPos.X - plan.TipAnchorLocal.X);
            Canvas.SetTop(marker, originalScreenPos.Y - plan.TipAnchorLocal.Y);
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

        #endregion

        #region Manual Layout Editor Methods

        /// <summary>
        /// Handles Edit Layout button click - enters edit mode.
        /// </summary>
        private void OnEditLayoutButtonClick(object sender, RoutedEventArgs e)
        {
            _layoutEditor.EnterEditMode();

            // If a manual layout is saved, restore those positions for draggable editing.
            // CanUseCompositePins() is false in edit mode, so ApplyManualLayout falls through
            // to the legacy ImagePinMarker + extension-line path.
            bool loadedSaved = false;
            if (_layoutEditor.IsManualLayoutActive && _layoutEditor.CurrentLayoutKey != null)
            {
                var layout = _layoutEditor.TryLoad(_layoutEditor.CurrentLayoutKey);
                if (layout != null)
                {
                    RestoreBaseMarkerVisuals();
                    _extensionLineRenderer.Clear();
                    ApplyManualLayout(layout);
                    _logger.LogInfo($"[OnEditLayoutButtonClick] Restored saved layout for key={_layoutEditor.CurrentLayoutKey}");
                    loadedSaved = true;
                }
            }
            if (!loadedSaved)
                UpdateMarkerPositions();

            var visibleMarkers = _individualMarkers.Count(m => m.Visibility == Visibility.Visible);
            _logger.LogInfo($"[OnEditLayoutButtonClick] Entering edit mode");
            _logger.LogInfo($"  Visible markers: {visibleMarkers}");
            _logger.LogInfo($"  Extension lines: {_extensionLineRenderer.LineCount}");
            _logger.LogInfo($"  Marker-to-line mappings: {_extensionLineRenderer.MarkerMappingCount}");

            // Enable dragging on all visible markers
            foreach (var marker in _individualMarkers.Where(m => m.Visibility == Visibility.Visible))
            {
                marker.Cursor = Cursors.Hand;
                marker.MouseLeftButtonDown += OnMarkerDragStart;
                marker.MouseMove           += OnMarkerDragMove;
                marker.MouseLeftButtonUp   += OnMarkerDragEnd;

                _logger.LogInfo(_extensionLineRenderer.HasLine(marker)
                    ? $"    Marker '{marker.Location.Name}' has line"
                    : $"    Marker '{marker.Location.Name}' has NO line");
            }

            _logger.LogInfo("Edit mode activated");
        }

        /// <summary>
        /// Handles Save Layout button click - saves current marker positions.
        /// </summary>
        private async void OnSaveLayoutButtonClick(object sender, RoutedEventArgs e)
        {
            if (_layoutEditor.CurrentLayoutKey == null || _currentZoomedCluster == null)
            {
                _logger.LogWarning("Cannot save layout - key or cluster is null");
                return;
            }

            try
            {
                var viewport = MapDisplay.CurrentViewport;
                if (viewport == null)
                {
                    _logger.LogWarning("Cannot save layout - viewport is null");
                    return;
                }

                // Collect current marker positions and delegate extension-building to controller.
                // Use the extension line endpoint as the authoritative MarkerCenter: after "Auto Assign
                // Pins" the marker's Canvas position is offset to the tip anchor, not the endpoint.
                var markerSize = _visualConfig.LocationMarkerSize;
                var markerData = _individualMarkers
                    .Where(m => m.Visibility == Visibility.Visible)
                    .Select(m =>
                    {
                        var center = _extensionLineRenderer.TryGetLineEndpoint(m, out var lineEnd)
                            ? lineEnd
                            : new Point(Canvas.GetLeft(m) + markerSize / 2, Canvas.GetTop(m) + markerSize / 2);
                        return (
                            m.Location,
                            MarkerCenter: center,
                            OriginalScreen: viewport.SourceToScreen(m.Location.PixelX, m.Location.PixelY, MapDisplay.ActualWidth, MapDisplay.ActualHeight));
                    });
                var extensions = LayoutEditorController.BuildExtensions(markerData);

                // Validate layout before saving
                var validationIssues = _layoutEditor.ValidateLayout(extensions);
                if (validationIssues.Count > 0)
                {
                    _logger.LogWarning($"Layout validation found {validationIssues.Count} issues:");
                    foreach (var issue in validationIssues)
                        _logger.LogWarning($"  - {issue}");

                    // Show warning but allow save
                    EditModeStatusText.Text       = $"⚠ {validationIssues.Count} Issues Found";
                    EditModeStatusText.Foreground = new SolidColorBrush(Color.FromRgb(255, 165, 0));

                }

                // Capture shaft/head assignments from the session plan cache before saving.
                var assignments = _assignmentEnricher.GetAssignments(extensions, _compositePinPlanningService);

                // Save (controller sets IsManualLayoutActive and logs)
                _layoutEditor.TrySave(extensions, assignments);

                // Phase 4: invalidate cached plans so next render builds fresh ones.
                if (_layoutEditor.CurrentLayoutKey != null)
                    _planApplicationService.InvalidateGroup(_layoutEditor.CurrentLayoutKey);

                // Pending overrides are now persisted — clear them and hide the indicator.
                _overrideStore.ClearOverrides();
                UpdateOverrideIndicator();

                // Show confirmation (unless we just showed a warning)
                if (validationIssues.Count == 0)
                {
                    EditModeStatusText.Text       = "✓ LAYOUT SAVED";
                    EditModeStatusText.Foreground = new SolidColorBrush(Color.FromRgb(50, 205, 50));

                }

                await ResetEditModeStatusAfterDelayAsync(validationIssues.Count > 0 ? 3000 : 2000);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to save layout: {ex.Message}");
                EditModeStatusText.Text       = "✗ SAVE FAILED";
                EditModeStatusText.Foreground = new SolidColorBrush(Colors.Red);
            }
        }

        /// <summary>
        /// Handles Delete & Recalculate button click - removes saved layout and recalculates.
        /// </summary>
        private void OnDeleteLayoutButtonClick(object sender, RoutedEventArgs e)
        {
            if (_layoutEditor.CurrentLayoutKey == null || _currentZoomedCluster == null)
            {
                _logger.LogWarning("Cannot delete layout - key or cluster is null");
                return;
            }

            try
            {
                // Delete saved layout (controller sets IsManualLayoutActive and logs)
                _layoutEditor.TryDelete();

                // Clear any pending overrides — layout is gone.
                _overrideStore.ClearAll();
                UpdateOverrideIndicator();

                // Exit edit mode
                ExitEditMode();
                
                // Recalculate positions
                ShowZoomedView(_currentZoomedCluster);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to delete layout: {ex.Message}");
            }
        }

        /// <summary>
        /// Handles Exit Edit Mode button click - exits edit mode without saving.
        /// </summary>
        private void OnExitEditModeButtonClick(object sender, RoutedEventArgs e)
        {
            ExitEditMode();
        }

        /// <summary>
        /// Exits edit mode and restores normal interaction.
        /// </summary>
        private void ExitEditMode()
        {
            _layoutEditor.ExitEditMode();

            // Disable dragging on all markers
            foreach (var marker in _individualMarkers)
            {
                marker.Cursor = Cursors.Arrow;
                marker.MouseLeftButtonDown -= OnMarkerDragStart;
                marker.MouseMove -= OnMarkerDragMove;
                marker.MouseLeftButtonUp -= OnMarkerDragEnd;
            }

            _draggedMarker = null;

            _logger.LogInfo("Edit mode deactivated");

            // If a manual layout is active, replay it so composite pins appear at the saved positions.
            if (_layoutEditor.IsManualLayoutActive && _layoutEditor.CurrentLayoutKey != null)
            {
                var layout = _layoutEditor.TryLoad(_layoutEditor.CurrentLayoutKey);
                if (layout != null)
                {
                    _logger.LogInfo($"[ExitEditMode] Replaying manual layout for key={_layoutEditor.CurrentLayoutKey}");
                    ApplyManualLayout(layout);
                    return;
                }
            }

            // Auto path: no manual layout saved yet (or load failed).
            UpdateMarkerPositions();
        }

        /// <summary>
        /// Applies a saved manual layout to the current view.
        /// Phase 4: checks the composite render-plan disk cache before building plans;
        /// saves plans to cache on a miss.
        /// </summary>
        private void ApplyManualLayout(ManualLayout layout)
        {
            _logger.LogInfo($"[ApplyManualLayout] Applying layout with {layout.Markers.Count} markers");

            // Phase 4: try cache before starting the per-marker build loop.
            var groupKey = _layoutEditor.CurrentLayoutKey ?? layout.GroupKey;
            IReadOnlyDictionary<string, CompositePinRenderPlan>? cachedPlans = null;
            string planCacheKey = string.Empty;
            if (!string.IsNullOrEmpty(groupKey) && _pinPartGeometryHash != null && CanUseCompositePins())
            {
                cachedPlans = _planApplicationService.TryCacheLoad(
                    layout, _visualConfig.PinParts, groupKey,
                    IOPath.Combine(AppDomain.CurrentDomain.BaseDirectory, _visualConfig.PinParts.GeometryMetadataPath),
                    out planCacheKey);
            }

            // Clear existing extension lines
            _extensionLineRenderer.Clear();

            var visibleMarkers = _individualMarkers
                .Where(m => m.Visibility == Visibility.Visible)
                .GroupBy(m => m.Location.Name)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);

            // When source-space extended coords are available (populated by the seed generator),
            // re-project to the current viewport so positions are correct at any window size.
            var viewport = MapDisplay.CurrentViewport;
            var cw = MapDisplay.ActualWidth;
            var ch = MapDisplay.ActualHeight;

            foreach (var application in _layoutEditor.CreateLayoutApplications(layout, visibleMarkers.Keys))
            {
                if (!visibleMarkers.TryGetValue(application.LocationName, out var marker))
                    continue;

                // Re-project original position from source coords (matches ApplyRadialExtensions path).
                var originalPos = viewport != null && cw > 0 && ch > 0
                    ? viewport.SourceToScreen(marker.Location.PixelX, marker.Location.PixelY, cw, ch)
                    : application.OriginalPosition;

                // Re-project extended position from source coords when available (seed-generated layouts).
                // Otherwise reconstruct from originalPos + saved angle/length so the composite pin is
                // always the correct size regardless of which viewport the layout was originally saved at.
                Point extendedPos;
                if (application.SourceExtendedX.HasValue && application.SourceExtendedY.HasValue
                    && viewport != null && cw > 0 && ch > 0)
                {
                    extendedPos = viewport.SourceToScreen(application.SourceExtendedX.Value, application.SourceExtendedY.Value, cw, ch);
                }
                else
                {
                    var rad = application.Angle * Math.PI / 180.0;
                    extendedPos = new Point(
                        originalPos.X + application.LineLength * Math.Sin(rad),
                        originalPos.Y - application.LineLength * Math.Cos(rad));
                }

                // Phase 4: use cached plan when available (skips BuildPlan for cache hits).
                if (cachedPlans != null
                    && cachedPlans.TryGetValue(application.LocationName, out var cachedPlan)
                    && _baseMarkerVisuals.TryGetValue(marker, out var bs) && bs.Content is ImagePinMarker)
                {
                    var si = LoadPinPartBitmap(cachedPlan.ShaftSourcePath);
                    var hi = LoadPinPartBitmap(cachedPlan.HeadSourcePath);
                    if (si != null && hi != null)
                    {
                        ApplyRenderPlanToMarker(marker, originalPos, extendedPos, cachedPlan, si, hi);
                        continue;
                    }
                }

                // Try composite pin first; falls back to legacy if disabled or assets missing.
                // Pass saved shaft/head IDs so replay honours the visual from save time.
                if (TryApplyCompositePinMarker(marker, originalPos, extendedPos, application.PairId, application.HeadSourcePath))
                    continue;

                // Legacy fallback: position marker at extended location + draw extension line.
                var markerSize = _visualConfig.LocationMarkerSize;
                Canvas.SetLeft(marker, extendedPos.X - (markerSize / 2));
                Canvas.SetTop(marker, extendedPos.Y - (markerSize / 2));

                if (application.RequiresExtensionLine)
                {
                    _extensionLineRenderer.AddLine(marker, originalPos, extendedPos);
                }
            }

            // Phase 4: save plans built during this pass to disk cache (only on cache miss).
            if (cachedPlans == null && !string.IsNullOrEmpty(planCacheKey) && !string.IsNullOrEmpty(groupKey))
            {
                _planApplicationService.SaveIfMissed(
                    planCacheKey, groupKey, layout.VariantId, layout.Markers.Select(m => m.LocationName));
            }

            // Re-apply any pending shaft overrides on top of the restored layout positions.
            // Only valid in non-edit mode; endpoints were just refreshed by composite apply above.
            if (_overrideStore.HasPendingOverrides && !_layoutEditor.IsEditMode)
                ReapplyPendingOverrides();
        }

        private async Task ResetEditModeStatusAfterDelayAsync(int delayMs)
        {
            await Task.Delay(delayMs);
            EditModeStatusText.Text = "EDIT MODE ACTIVE";
            EditModeStatusText.Foreground = new SolidColorBrush(Color.FromRgb(255, 215, 0));
        }

        /// <summary>
        /// Handles marker drag start.
        /// </summary>
        private void OnMarkerDragStart(object sender, MouseButtonEventArgs e)
        {
            if (!_layoutEditor.IsEditMode || sender is not LocationMarker marker)
                return;

            _draggedMarker = marker;
            _dragStartPosition = e.GetPosition(MapDisplay.Markers);
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
        /// </summary>
        private void OnMarkerDragMove(object sender, MouseEventArgs e)
        {
            if (!_layoutEditor.IsEditMode || _draggedMarker == null || sender != _draggedMarker)
                return;

            if (e.LeftButton == MouseButtonState.Pressed)
            {
                var currentPosition = e.GetPosition(MapDisplay.Markers);
                var markerSize = _visualConfig.LocationMarkerSize;
                
                LogDragDebug($"[DRAG] Mouse position: ({currentPosition.X:F1}, {currentPosition.Y:F1}), MarkerSize: {markerSize}");
                
                // Calculate new position (centered on cursor)
                var newX = currentPosition.X - (markerSize / 2);
                var newY = currentPosition.Y - (markerSize / 2);
                
                LogDragDebug($"[DRAG] Calculated position before bounds: ({newX:F1}, {newY:F1})");
                
                // Constrain to canvas bounds
                var canvasWidth = MapDisplay.Markers.ActualWidth;
                var canvasHeight = MapDisplay.Markers.ActualHeight;
                newX = Math.Max(0, Math.Min(newX, canvasWidth - markerSize));
                newY = Math.Max(0, Math.Min(newY, canvasHeight - markerSize));
                
                LogDragDebug($"[DRAG] Final position after bounds ({canvasWidth:F0}x{canvasHeight:F0}): ({newX:F1}, {newY:F1})");
                
                // Get current marker position for comparison
                var currentMarkerX = Canvas.GetLeft(_draggedMarker);
                var currentMarkerY = Canvas.GetTop(_draggedMarker);
                LogDragDebug($"[DRAG] Current marker position: ({currentMarkerX:F1}, {currentMarkerY:F1})");
                
                // Update marker position
                Canvas.SetLeft(_draggedMarker, newX);
                Canvas.SetTop(_draggedMarker, newY);
                
                // Verify marker position was updated
                var updatedMarkerX = Canvas.GetLeft(_draggedMarker);
                var updatedMarkerY = Canvas.GetTop(_draggedMarker);
                LogDragDebug($"[DRAG] Updated marker position: ({updatedMarkerX:F1}, {updatedMarkerY:F1})");
                
                // Update line if it exists
                if (_extensionLineRenderer.HasLine(_draggedMarker))
                {
                    var markerCenterX = newX + (markerSize / 2);
                    var markerCenterY = newY + (markerSize / 2);
                    _extensionLineRenderer.MoveLineEndpoint(_draggedMarker, new Point(markerCenterX, markerCenterY));
                    LogDragDebug($"[DRAG] Recreated line for {_draggedMarker.Location.Name} to ({markerCenterX:F1}, {markerCenterY:F1})");
                }
                else
                {
                    LogDragDebug($"[OnMarkerDragMove] No line found for marker: {_draggedMarker.Location.Name}");
                }
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

            _draggedMarker = null;
            
            e.Handled = true;
        }

        #endregion

        #region Master Pin Image Loading

        /// <summary>
        /// Loads the master pin image from the configured path.
        /// </summary>
        private void LoadMasterPinImage()
        {
            try
            {
                if (!_visualConfig.PinImages.Enabled)
                {
                    _logger.LogInfo("Image-based pins are disabled in config");
                    return;
                }

                var bitmap = _contentLoader.TryLoadContentBitmap(_visualConfig.PinImages.MasterImagePath);
                if (bitmap == null)
                {
                    _logger.LogError(
                        $"Master pin image not found at: {_contentLoader.ResolveContentFilePath(_visualConfig.PinImages.MasterImagePath)}");
                    return;
                }

                _masterPinImage = bitmap;
                _logger.LogInfo(
                    $"Master pin image loaded: {_contentLoader.ResolveContentFilePath(_visualConfig.PinImages.MasterImagePath)} ({bitmap.PixelWidth}x{bitmap.PixelHeight})");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to load master pin image: {ex.Message}");
                _masterPinImage = null;
            }
        }

        #endregion
    }
}
