using System;
using System.Collections.Generic;
using System.Windows;
using InteractiveWorldMap.Models;

namespace InteractiveWorldMap.Utilities;

/// <summary>
/// Marker-layout geometry helpers used by the radial extension system.
/// Preserves intentional behavior:
///   - Line intersection ignores endpoint touches via IntersectionEndpointMargin.
///   - Marker proximity uses a markerRadius + 2 px buffer.
///   - Threshold equality is treated as non-intersecting / non-overlapping.
/// </summary>
public static class GeometryMath
{
    /// <summary>
    /// Epsilon for denominator comparisons (parallel-line detection).
    /// </summary>
    public const double GeometryEpsilon = 0.0001;

    /// <summary>
    /// Fractional margin applied to parametric intersection parameters to ignore endpoint touches.
    /// Intersection is detected only when both t1 and t2 are strictly inside (Margin, 1-Margin).
    /// </summary>
    public const double IntersectionEndpointMargin = 0.01;

    /// <summary>
    /// Tolerance for treating two screen points as the same point.
    /// </summary>
    public const double CoincidentPointEpsilon = 0.001;

    /// <summary>
    /// True when two points are the same to within <see cref="CoincidentPointEpsilon"/>.
    /// </summary>
    /// <remarks>
    /// Used to detect a zero-length extension: a pin head sitting exactly on its own anchor.
    /// </remarks>
    public static bool ArePointsCoincident(Point a, Point b) =>
        Math.Abs(a.X - b.X) <= CoincidentPointEpsilon &&
        Math.Abs(a.Y - b.Y) <= CoincidentPointEpsilon;

    /// <summary>
    /// Checks whether two line segments intersect using parametric line intersection.
    /// Endpoint touches are ignored via <see cref="IntersectionEndpointMargin"/>.
    /// </summary>
    public static bool DoLineSegmentsIntersect(Point p1, Point p2, Point p3, Point p4)
    {
        double d1x = p2.X - p1.X;
        double d1y = p2.Y - p1.Y;
        double d2x = p4.X - p3.X;
        double d2y = p4.Y - p3.Y;

        double denominator = d1x * d2y - d1y * d2x;

        // Parallel lines
        if (Math.Abs(denominator) < GeometryEpsilon)
            return false;

        double t1 = ((p3.X - p1.X) * d2y - (p3.Y - p1.Y) * d2x) / denominator;
        double t2 = ((p3.X - p1.X) * d1y - (p3.Y - p1.Y) * d1x) / denominator;

        // Intersection occurs if both parameters are strictly inside (Margin, 1-Margin)
        return t1 > IntersectionEndpointMargin && t1 < 1.0 - IntersectionEndpointMargin
            && t2 > IntersectionEndpointMargin && t2 < 1.0 - IntersectionEndpointMargin;
    }

    /// <summary>
    /// Returns the minimum distance from <paramref name="point"/> to the line segment
    /// defined by <paramref name="lineStart"/> and <paramref name="lineEnd"/>.
    /// </summary>
    public static double PointToLineSegmentDistance(Point point, Point lineStart, Point lineEnd)
    {
        double dx = lineEnd.X - lineStart.X;
        double dy = lineEnd.Y - lineStart.Y;
        double lengthSquared = dx * dx + dy * dy;

        if (lengthSquared < GeometryEpsilon)
        {
            // Degenerate segment — treat as a point
            double ptDistX = point.X - lineStart.X;
            double ptDistY = point.Y - lineStart.Y;
            return Math.Sqrt(ptDistX * ptDistX + ptDistY * ptDistY);
        }

        double t = ((point.X - lineStart.X) * dx + (point.Y - lineStart.Y) * dy) / lengthSquared;
        t = Math.Max(0, Math.Min(1, t));

        double closestX = lineStart.X + t * dx;
        double closestY = lineStart.Y + t * dy;

        double deltaX = point.X - closestX;
        double deltaY = point.Y - closestY;
        return Math.Sqrt(deltaX * deltaX + deltaY * deltaY);
    }

    /// <summary>
    /// Returns whether the line segment from <paramref name="lineStart"/> to
    /// <paramref name="lineEnd"/> passes within <c>markerRadius + 2</c> pixels of
    /// <paramref name="markerPos"/>. The 2 px buffer is intentional.
    /// </summary>
    public static bool DoesLinePassTooCloseToMarker(
        Point lineStart, Point lineEnd, Point markerPos, double markerRadius)
    {
        double dx = lineEnd.X - lineStart.X;
        double dy = lineEnd.Y - lineStart.Y;
        double lengthSquared = dx * dx + dy * dy;

        if (lengthSquared < GeometryEpsilon)
            return false; // Degenerate segment — no length to be close to

        double t = ((markerPos.X - lineStart.X) * dx + (markerPos.Y - lineStart.Y) * dy) / lengthSquared;
        t = Math.Max(0, Math.Min(1, t));

        double closestX = lineStart.X + t * dx;
        double closestY = lineStart.Y + t * dy;

        double distX = markerPos.X - closestX;
        double distY = markerPos.Y - closestY;
        double distance = Math.Sqrt(distX * distX + distY * distY);

        return distance < markerRadius + 2.0; // 2 px buffer
    }

    /// <summary>
    /// Returns the minimum separation between two line segments by checking all four
    /// endpoint-to-segment distances.
    /// </summary>
    public static double CalculateMinimumDistanceBetweenLines(
        Point line1Start, Point line1End,
        Point line2Start, Point line2End)
    {
        double dist1 = PointToLineSegmentDistance(line1Start, line2Start, line2End);
        double dist2 = PointToLineSegmentDistance(line1End, line2Start, line2End);
        double dist3 = PointToLineSegmentDistance(line2Start, line1Start, line1End);
        double dist4 = PointToLineSegmentDistance(line2End, line1Start, line1End);
        return Math.Min(Math.Min(dist1, dist2), Math.Min(dist3, dist4));
    }

    /// <summary>
    /// Returns the angular gap (in degrees) between <paramref name="ext"/> and its
    /// nearest neighbour in <paramref name="sortedExtensions"/> in the requested direction.
    /// Returns 30° when the extension is not found in the list.
    /// </summary>
    public static double CalculateAngularSpace(
        RadialExtension ext,
        List<RadialExtension> sortedExtensions,
        bool clockwise)
    {
        int index = sortedExtensions.FindIndex(e => e.Location.Name == ext.Location.Name);
        if (index == -1) return 30.0;

        if (clockwise)
        {
            int nextIndex = (index + 1) % sortedExtensions.Count;
            double nextAngle = sortedExtensions[nextIndex].Angle;
            return (nextAngle - ext.Angle + 360.0) % 360.0;
        }
        else
        {
            int prevIndex = (index - 1 + sortedExtensions.Count) % sortedExtensions.Count;
            double prevAngle = sortedExtensions[prevIndex].Angle;
            return (ext.Angle - prevAngle + 360.0) % 360.0;
        }
    }

    /// <summary>
    /// Finds the maximum safe rotation (in degrees) for <paramref name="ext"/> in the
    /// requested direction without causing intersections with any other extension.
    /// Tests in 1° increments up to <paramref name="maxRotation"/>.
    /// </summary>
    /// <param name="markerRadius">Half the visual marker size in screen pixels.</param>
    public static double FindSafeAngleRotation(
        RadialExtension ext,
        List<RadialExtension> allExtensions,
        bool clockwise,
        double maxRotation,
        double markerRadius)
    {
        const double testIncrement = 1.0;
        double safeRotation = 0.0;

        double dx = ext.ExtendedPosition.X - ext.OriginalPosition.X;
        double dy = ext.ExtendedPosition.Y - ext.OriginalPosition.Y;
        double lineLength = Math.Sqrt(dx * dx + dy * dy);

        for (double testRotation = testIncrement; testRotation <= maxRotation; testRotation += testIncrement)
        {
            double testAngle = ext.Angle + (clockwise ? testRotation : -testRotation);
            double testAngleRad = testAngle * (Math.PI / 180.0);

            Point testExtendedPos = new(
                ext.OriginalPosition.X + lineLength * Math.Sin(testAngleRad),
                ext.OriginalPosition.Y - lineLength * Math.Cos(testAngleRad));

            bool wouldIntersect = false;
            foreach (var other in allExtensions)
            {
                if (other.Location.Name == ext.Location.Name)
                    continue;

                if (DoLineSegmentsIntersect(
                        ext.OriginalPosition, testExtendedPos,
                        other.OriginalPosition, other.ExtendedPosition)
                    || DoesLinePassTooCloseToMarker(
                        ext.OriginalPosition, testExtendedPos,
                        other.ExtendedPosition, markerRadius)
                    || DoesLinePassTooCloseToMarker(
                        other.OriginalPosition, other.ExtendedPosition,
                        testExtendedPos, markerRadius))
                {
                    wouldIntersect = true;
                    break;
                }
            }

            if (wouldIntersect)
                break;

            safeRotation = testRotation;
        }

        return safeRotation;
    }
}
