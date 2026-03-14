using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using InteractiveWorldMap.Models;

namespace InteractiveWorldMap.Views;

/// <summary>
/// Renders the world map using viewport-based cropping for efficient zoom/pan.
/// </summary>
public partial class MapDisplayControl : UserControl
{
    private BitmapSource? _sourceImage;
    private ViewportState? _currentViewport;

    /// <summary>
    /// Gets the marker canvas where markers should be added.
    /// </summary>
    public Canvas Markers => MarkerCanvas;
    
    /// <summary>
    /// Gets the source image for pre-rendering.
    /// </summary>
    public BitmapSource? SourceImage => _sourceImage;
    
    /// <summary>
    /// Gets the MapImage control for direct access during pre-rendered animations.
    /// </summary>
    public Image DisplayImage => MapImage;
    
    /// <summary>
    /// Sets the current viewport state without rendering (for pre-rendered frames).
    /// </summary>
    public void SetCurrentViewport(ViewportState viewport)
    {
        _currentViewport = viewport;
    }

    /// <summary>
    /// Gets the current viewport state.
    /// </summary>
    public ViewportState? CurrentViewport => _currentViewport;

    /// <summary>
    /// Gets whether the map is currently zoomed in.
    /// </summary>
    public bool IsZoomed => _currentViewport != null && Math.Abs(_currentViewport.ZoomLevel - 1.0) > 0.01;

    public MapDisplayControl()
    {
        InitializeComponent();
        SizeChanged += OnSizeChanged;
    }

    /// <summary>
    /// Loads a map image and initializes the viewport to show the full map.
    /// </summary>
    public void LoadMapImage(ImageSource imageSource)
    {
        if (imageSource == null)
            throw new ArgumentNullException(nameof(imageSource));

        if (imageSource is not BitmapSource bitmapSource)
            throw new ArgumentException("Image source must be a BitmapSource", nameof(imageSource));

        _sourceImage = bitmapSource;

        // Initialize viewport to show full map
        if (ActualWidth > 0 && ActualHeight > 0)
        {
            _currentViewport = ViewportState.CreateFullMapView(
                _sourceImage.PixelWidth,
                _sourceImage.PixelHeight,
                ActualWidth,
                ActualHeight);

            UpdateViewport(_currentViewport);
        }
    }

    /// <summary>
    /// Updates the displayed viewport to show a specific region of the source image.
    /// </summary>
    public void UpdateViewport(ViewportState viewport)
    {
        if (_sourceImage == null)
            return;

        _currentViewport = viewport;

        try
        {
            // Get the source rectangle to crop
            var sourceRect = viewport.GetSourceRect();

            // Create cropped bitmap with NearestNeighbor for better performance
            var croppedBitmap = new CroppedBitmap(_sourceImage, sourceRect);
            
            // Set rendering options for better performance
            RenderOptions.SetBitmapScalingMode(MapImage, BitmapScalingMode.NearestNeighbor);
            RenderOptions.SetCachingHint(MapImage, CachingHint.Cache);

            // Display the cropped region
            MapImage.Source = croppedBitmap;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error updating viewport: {ex.Message}");
        }
    }

    /// <summary>
    /// Converts pixel coordinates on the original image to screen position.
    /// </summary>
    public Point GetMapPosition(double pixelX, double pixelY, double imageWidth, double imageHeight)
    {
        if (_currentViewport == null)
            return new Point(0, 0);

        return _currentViewport.SourceToScreen(pixelX, pixelY, ActualWidth, ActualHeight);
    }

    /// <summary>
    /// Converts screen coordinates to source image coordinates.
    /// </summary>
    public Point GetSourcePosition(double screenX, double screenY)
    {
        if (_currentViewport == null)
            return new Point(0, 0);

        return _currentViewport.ScreenToSource(screenX, screenY, ActualWidth, ActualHeight);
    }

    /// <summary>
    /// Gets the bounds of the map within the control (always fills the control with viewport approach).
    /// </summary>
    public Rect MapBounds => new Rect(0, 0, ActualWidth, ActualHeight);

    private void OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        // Recreate viewport when control size changes
        if (_sourceImage != null && e.NewSize.Width > 0 && e.NewSize.Height > 0)
        {
            if (_currentViewport == null || Math.Abs(_currentViewport.ZoomLevel - 1.0) < 0.01)
            {
                // If at full map view, recreate full map viewport
                _currentViewport = ViewportState.CreateFullMapView(
                    _sourceImage.PixelWidth,
                    _sourceImage.PixelHeight,
                    e.NewSize.Width,
                    e.NewSize.Height);

                UpdateViewport(_currentViewport);
            }
            // If zoomed, keep the same center point but adjust viewport size
            else if (_currentViewport != null)
            {
                var centerX = _currentViewport.ViewportX + (_currentViewport.ViewportWidth / 2.0);
                var centerY = _currentViewport.ViewportY + (_currentViewport.ViewportHeight / 2.0);

                _currentViewport = ViewportState.CreateZoomedView(
                    centerX, centerY, _currentViewport.ZoomLevel,
                    _sourceImage.PixelWidth,
                    _sourceImage.PixelHeight,
                    e.NewSize.Width,
                    e.NewSize.Height);

                UpdateViewport(_currentViewport);
            }
        }
    }

    /// <summary>
    /// Checks if a screen point is within the map bounds (always true with viewport approach).
    /// </summary>
    public bool IsPointOnMap(Point screenPoint)
    {
        return screenPoint.X >= 0 && screenPoint.X <= ActualWidth &&
               screenPoint.Y >= 0 && screenPoint.Y <= ActualHeight;
    }
}
