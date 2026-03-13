using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace InteractiveWorldMap.Views;

/// <summary>
/// Renders the world map image with proper scaling and aspect ratio preservation.
/// </summary>
public partial class MapDisplayControl : UserControl
{
    /// <summary>
    /// Gets the actual size of the rendered map on screen.
    /// </summary>
    public Size ActualMapSize
    {
        get
        {
            if (MapImage.Source == null)
                return Size.Empty;

            var imageWidth = MapImage.Source.Width;
            var imageHeight = MapImage.Source.Height;
            var containerWidth = ActualWidth;
            var containerHeight = ActualHeight;

            // Calculate the size with Uniform stretch
            var scaleX = containerWidth / imageWidth;
            var scaleY = containerHeight / imageHeight;
            var scale = Math.Min(scaleX, scaleY);

            return new Size(imageWidth * scale, imageHeight * scale);
        }
    }

    /// <summary>
    /// Gets the bounds of the map within the control.
    /// </summary>
    public Rect MapBounds
    {
        get
        {
            var mapSize = ActualMapSize;
            if (mapSize.IsEmpty)
                return Rect.Empty;

            // Calculate offset to center the map
            var offsetX = (ActualWidth - mapSize.Width) / 2;
            var offsetY = (ActualHeight - mapSize.Height) / 2;

            return new Rect(offsetX, offsetY, mapSize.Width, mapSize.Height);
        }
    }

    /// <summary>
    /// Gets the scale transform applied to the map.
    /// </summary>
    public ScaleTransform ScaleTransform => MapScaleTransform;

    /// <summary>
    /// Gets the translate transform applied to the map.
    /// </summary>
    public TranslateTransform TranslateTransform => MapTranslateTransform;

    /// <summary>
    /// Gets whether the map is currently zoomed in.
    /// </summary>
    public bool IsZoomed => Math.Abs(MapScaleTransform.ScaleX - 1.0) > 0.01;

    public MapDisplayControl()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Loads a map image from the specified path.
    /// </summary>
    /// <param name="imageSource">The image source to display</param>
    public void LoadMapImage(ImageSource imageSource)
    {
        if (imageSource == null)
            throw new ArgumentNullException(nameof(imageSource));

        MapImage.Source = imageSource;
    }

    /// <summary>
    /// Converts pixel coordinates on the original image to screen position on the map.
    /// </summary>
    /// <param name="pixelX">X pixel coordinate on the original image</param>
    /// <param name="pixelY">Y pixel coordinate on the original image</param>
    /// <param name="imageWidth">Width of the original image in pixels</param>
    /// <param name="imageHeight">Height of the original image in pixels</param>
    /// <returns>Screen position as a Point</returns>
    public Point GetMapPosition(double pixelX, double pixelY, double imageWidth, double imageHeight)
    {
        var bounds = MapBounds;
        if (bounds.IsEmpty)
            return new Point(0, 0);

        // Normalize pixel coordinates to [0, 1]
        var normalizedX = pixelX / imageWidth;
        var normalizedY = pixelY / imageHeight;

        // Map to screen coordinates within MapBounds
        var x = bounds.Left + (normalizedX * bounds.Width);
        var y = bounds.Top + (normalizedY * bounds.Height);

        return new Point(x, y);
    }

    /// <summary>
    /// Checks if a screen point is within the map bounds.
    /// </summary>
    /// <param name="screenPoint">Screen point to check</param>
    /// <returns>True if the point is on the map, false otherwise</returns>
    public bool IsPointOnMap(Point screenPoint)
    {
        var bounds = MapBounds;
        return bounds.Contains(screenPoint);
    }
}
