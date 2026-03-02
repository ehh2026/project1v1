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
    /// Converts geographic coordinates to screen position on the map.
    /// </summary>
    /// <param name="latitude">Latitude in degrees (-90 to 90)</param>
    /// <param name="longitude">Longitude in degrees (-180 to 180)</param>
    /// <returns>Screen position as a Point</returns>
    public Point GetMapPosition(double latitude, double longitude)
    {
        var bounds = MapBounds;
        if (bounds.IsEmpty)
            return new Point(0, 0);

        // Normalize latitude from [-90, 90] to [0, 1]
        var normalizedLat = (90.0 - latitude) / 180.0;

        // Normalize longitude from [-180, 180] to [0, 1]
        var normalizedLon = (longitude + 180.0) / 360.0;

        // Map to screen coordinates within MapBounds
        var x = bounds.Left + (normalizedLon * bounds.Width);
        var y = bounds.Top + (normalizedLat * bounds.Height);

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
