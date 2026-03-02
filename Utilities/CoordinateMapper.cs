using System.Windows;

namespace InteractiveWorldMap.Utilities;

/// <summary>
/// Converts geographic coordinates to screen pixel positions using Equirectangular projection.
/// </summary>
public class CoordinateMapper
{
    /// <summary>
    /// Gets or sets the bounds of the map on the screen.
    /// </summary>
    public Rect MapBounds { get; set; }

    /// <summary>
    /// Gets or sets the size of the screen.
    /// </summary>
    public Size ScreenSize { get; set; }

    /// <summary>
    /// Converts latitude and longitude coordinates to screen pixel position.
    /// Uses Equirectangular projection (simple linear mapping).
    /// </summary>
    /// <param name="latitude">Latitude in degrees (-90 to +90)</param>
    /// <param name="longitude">Longitude in degrees (-180 to +180)</param>
    /// <returns>Screen position as a Point</returns>
    public Point LatLongToScreen(double latitude, double longitude)
    {
        // Normalize latitude from [-90, 90] to [0, 1]
        // Note: Latitude increases from bottom to top, but screen Y increases from top to bottom
        var normalizedLat = (90.0 - latitude) / 180.0;

        // Normalize longitude from [-180, 180] to [0, 1]
        var normalizedLon = (longitude + 180.0) / 360.0;

        // Map to screen coordinates within MapBounds
        var x = MapBounds.Left + (normalizedLon * MapBounds.Width);
        var y = MapBounds.Top + (normalizedLat * MapBounds.Height);

        return new Point(x, y);
    }

    /// <summary>
    /// Converts screen pixel position to latitude and longitude coordinates.
    /// Reverse of LatLongToScreen method.
    /// </summary>
    /// <param name="screenPoint">Screen position as a Point</param>
    /// <returns>Tuple containing latitude and longitude in degrees</returns>
    public (double lat, double lon) ScreenToLatLong(Point screenPoint)
    {
        // Normalize screen coordinates to [0, 1] within MapBounds
        var normalizedLon = (screenPoint.X - MapBounds.Left) / MapBounds.Width;
        var normalizedLat = (screenPoint.Y - MapBounds.Top) / MapBounds.Height;

        // Convert back to geographic coordinates
        var longitude = (normalizedLon * 360.0) - 180.0;
        var latitude = 90.0 - (normalizedLat * 180.0);

        return (latitude, longitude);
    }

    /// <summary>
    /// Updates the projection parameters when the window is resized or map bounds change.
    /// </summary>
    /// <param name="newMapBounds">New map bounds after resize</param>
    public void UpdateProjection(Rect newMapBounds)
    {
        MapBounds = newMapBounds;
    }
}
