using System;
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

    /// <summary>
    /// Returns a screen point offset from <paramref name="origin"/> at the supplied angle,
    /// where 0 degrees points up and angles increase clockwise.
    /// </summary>
    public static Point OffsetAtAngle(Point origin, double distance, double angleDegrees)
    {
        var angleRadians = angleDegrees * Math.PI / 180.0;
        return new Point(
            origin.X + distance * Math.Sin(angleRadians),
            origin.Y - distance * Math.Cos(angleRadians));
    }

    /// <summary>
    /// Returns how far a ray leaving <paramref name="origin"/> at the supplied angle can travel
    /// before it crosses the edge of a <paramref name="canvasWidth"/> x <paramref name="canvasHeight"/>
    /// canvas. Same angle convention as <see cref="OffsetAtAngle"/>: 0 degrees points up, angles
    /// increase clockwise.
    ///
    /// This exists so that everything shortening a line to fit the canvas agrees on where the edge
    /// is. Two answers to that question is one more than the number of edges.
    ///
    /// Returns 0 for an origin already outside the canvas, rather than a negative distance -- a
    /// negative one would send the caller backwards along the ray, past its own starting point.
    /// </summary>
    public static double DistanceToCanvasEdge(
        Point origin, double angleDegrees, double canvasWidth, double canvasHeight)
    {
        var angleRadians = angleDegrees * Math.PI / 180.0;
        var sin = Math.Sin(angleRadians);
        var cos = Math.Cos(angleRadians);

        // The ray crosses at most two of the four edges; take the nearer. An axis the ray does not
        // move along (sin or cos of 0) cannot be crossed, so it constrains nothing.
        var distance = double.PositiveInfinity;

        if (sin > 0) distance = Math.Min(distance, (canvasWidth - origin.X) / sin);
        else if (sin < 0) distance = Math.Min(distance, origin.X / -sin);

        if (cos > 0) distance = Math.Min(distance, origin.Y / cos);
        else if (cos < 0) distance = Math.Min(distance, (canvasHeight - origin.Y) / -cos);

        // sin and cos cannot both be zero for a real angle, so the only way nothing constrained
        // the ray is a NaN angle, where every comparison above is false.
        if (double.IsPositiveInfinity(distance) || double.IsNaN(distance)) return 0.0;
        return Math.Max(0.0, distance);
    }

    /// <summary>
    /// Returns the Euclidean distance between two screen points.
    /// </summary>
    public static double DistanceBetween(Point first, Point second)
    {
        var deltaX = second.X - first.X;
        var deltaY = second.Y - first.Y;
        return Math.Sqrt((deltaX * deltaX) + (deltaY * deltaY));
    }

    /// <summary>
    /// Normalizes an angle to the range [0, 360).
    /// </summary>
    public static double NormalizeAngle(double angleDegrees)
    {
        var normalized = angleDegrees % 360.0;
        return normalized < 0 ? normalized + 360.0 : normalized;
    }

    /// <summary>
    /// Returns the clockwise angle from <paramref name="fromAngleDegrees"/> to
    /// <paramref name="toAngleDegrees"/> in the range [0, 360).
    /// </summary>
    public static double ClockwiseAngleDistance(double fromAngleDegrees, double toAngleDegrees) =>
        NormalizeAngle(toAngleDegrees - fromAngleDegrees);

    /// <summary>
    /// Returns the smallest circular separation between two angles in degrees.
    /// </summary>
    public static double CircularAngleDistance(double firstAngleDegrees, double secondAngleDegrees)
    {
        var clockwiseDistance = ClockwiseAngleDistance(firstAngleDegrees, secondAngleDegrees);
        return Math.Min(clockwiseDistance, 360.0 - clockwiseDistance);
    }
}
