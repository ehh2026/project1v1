using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
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
        private ContentSubwindow? _activeSubwindow;

        /// <summary>
        /// Gets the active content subwindow, if any.
        /// </summary>
        public ContentSubwindow? ActiveSubwindow => _activeSubwindow;

        public MainWindow()
        {
            InitializeComponent();

            // Initialize services
            _logger = new FileLogger();
            _contentLoader = new ContentLoader(_logger);

            // Wire up events
            MarkerLayer.MarkerClicked += OnMarkerClicked;
            Loaded += OnWindowLoaded;
            KeyDown += OnKeyDown;
            MouseLeftButtonDown += OnMouseLeftButtonDown;
            SizeChanged += OnSizeChanged;
        }

        /// <summary>
        /// Initializes the application by loading map and locations.
        /// </summary>
        public async Task InitializeAsync()
        {
            try
            {
                _logger.LogInfo("Initializing Interactive World Map application");
                _logger.LogInfo($"Base directory: {AppDomain.CurrentDomain.BaseDirectory}");
                _logger.LogInfo($"Content folder path: {_contentLoader.ContentFolderPath}");

                // Validate content folder
                if (!_contentLoader.ValidateContentFolder())
                {
                    var errorMsg = $"Content folder validation failed.\nExpected path: {_contentLoader.ContentFolderPath}\nPlease ensure the Images&Content folder exists with the required files.";
                    _logger.LogError(errorMsg);
                    ShowError(errorMsg);
                    return;
                }

                // Load map image
                _logger.LogInfo("Loading world map image");
                var mapImage = await _contentLoader.LoadMapImageAsync();
                MapDisplay.LoadMapImage(mapImage);

                // Wait for layout to complete
                await Task.Delay(100);

                // Update marker layer with map bounds
                UpdateMarkerLayerBounds();

                // Load locations
                _logger.LogInfo("Loading location data");
                var locations = await _contentLoader.LoadLocationsAsync();
                
                if (locations.Any())
                {
                    foreach (var location in locations)
                    {
                        MarkerLayer.AddMarker(location);
                    }
                    _logger.LogInfo($"Added {locations.Count} location markers");
                }
                else
                {
                    _logger.LogWarning("No locations found to display");
                }

                _logger.LogInfo("Application initialization complete");
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

                // Close existing subwindow if any
                CloseActiveSubwindow();

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

                var markerPosition = MapDisplay.GetMapPosition(location.Latitude, location.Longitude);
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
                _activeSubwindow.AnimateClose(() =>
                {
                    _activeSubwindow = null;
                    Focus(); // Return focus to main window
                });
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
            ShowContentForLocation(e.Location);
        }

        private void OnKeyDown(object sender, KeyEventArgs e)
        {
            // Handle Escape key to close subwindow or exit application
            if (e.Key == Key.Escape)
            {
                if (_activeSubwindow != null)
                {
                    CloseActiveSubwindow();
                }
                else
                {
                    _logger.LogInfo("Application closing via Escape key");
                    Close();
                }
            }
        }

        private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            var position = e.GetPosition(this);
            HandleOutsideClick(position);
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
