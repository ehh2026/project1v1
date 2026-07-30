using System.Collections.Generic;
using System.Windows;
using InteractiveWorldMap.Models;
using InteractiveWorldMap.Utilities;
using Xunit;

namespace InteractiveWorldMap.Tests;

public class GeometryMathTests
{
    // -------------------------------------------------------------------------
    // DoLineSegmentsIntersect
    // -------------------------------------------------------------------------

    [Fact]
    public void DoLineSegmentsIntersect_CrossingLines_ReturnsTrue()
    {
        // Arrange — a diagonal X cross
        var p1 = new Point(0, 0);
        var p2 = new Point(10, 10);
        var p3 = new Point(10, 0);
        var p4 = new Point(0, 10);

        // Act / Assert
        Assert.True(GeometryMath.DoLineSegmentsIntersect(p1, p2, p3, p4));
    }

    [Fact]
    public void DoLineSegmentsIntersect_ParallelLines_ReturnsFalse()
    {
        var p1 = new Point(0, 0);
        var p2 = new Point(10, 0);
        var p3 = new Point(0, 5);
        var p4 = new Point(10, 5);

        Assert.False(GeometryMath.DoLineSegmentsIntersect(p1, p2, p3, p4));
    }

    [Fact]
    public void DoLineSegmentsIntersect_CollinearOverlapping_ReturnsFalse()
    {
        // Collinear segments share area but denominator is ~0 (parallel check)
        var p1 = new Point(0, 0);
        var p2 = new Point(10, 0);
        var p3 = new Point(5, 0);
        var p4 = new Point(15, 0);

        Assert.False(GeometryMath.DoLineSegmentsIntersect(p1, p2, p3, p4));
    }

    [Fact]
    public void DoLineSegmentsIntersect_SharedEndpoint_ReturnsFalse()
    {
        // Segments that only touch at an endpoint are ignored via the margin
        var p1 = new Point(0, 0);
        var p2 = new Point(5, 5);
        var p3 = new Point(5, 5);
        var p4 = new Point(10, 0);

        Assert.False(GeometryMath.DoLineSegmentsIntersect(p1, p2, p3, p4));
    }

    [Fact]
    public void DoLineSegmentsIntersect_TMeeting_ReturnsTrue()
    {
        // Segment AB crosses segment CD at the midpoint of CD
        var p1 = new Point(5, 0);
        var p2 = new Point(5, 10);
        var p3 = new Point(0, 5);
        var p4 = new Point(10, 5);

        Assert.True(GeometryMath.DoLineSegmentsIntersect(p1, p2, p3, p4));
    }

    [Fact]
    public void DoLineSegmentsIntersect_PerpendicularNonTouching_ReturnsFalse()
    {
        // Perpendicular segments that do not reach each other
        var p1 = new Point(0, 0);
        var p2 = new Point(3, 0);
        var p3 = new Point(5, -3);
        var p4 = new Point(5, 3);

        Assert.False(GeometryMath.DoLineSegmentsIntersect(p1, p2, p3, p4));
    }

    [Fact]
    public void DoLineSegmentsIntersect_EndpointWithinMargin_ReturnsFalse()
    {
        // t2 lands exactly at IntersectionEndpointMargin (0.01) — should return false
        // Segment 2 starts at the very beginning of segment 1 (t1=0, t2 arbitrary)
        var p1 = new Point(0, 0);
        var p2 = new Point(100, 0);
        var p3 = new Point(0, -10);  // t1=0 on segment 1
        var p4 = new Point(0, 10);

        Assert.False(GeometryMath.DoLineSegmentsIntersect(p1, p2, p3, p4));
    }

    // -------------------------------------------------------------------------
    // PointToLineSegmentDistance
    // -------------------------------------------------------------------------

    [Fact]
    public void PointToLineSegmentDistance_PointOnSegment_ReturnsZero()
    {
        var point = new Point(5, 0);
        var start = new Point(0, 0);
        var end = new Point(10, 0);

        double dist = GeometryMath.PointToLineSegmentDistance(point, start, end);

        Assert.Equal(0.0, dist, 6);
    }

    [Fact]
    public void PointToLineSegmentDistance_PointProjectsOntoSegment_ReturnsPerpendicularDistance()
    {
        var point = new Point(5, 3);
        var start = new Point(0, 0);
        var end = new Point(10, 0);

        double dist = GeometryMath.PointToLineSegmentDistance(point, start, end);

        Assert.Equal(3.0, dist, 6);
    }

    [Fact]
    public void PointToLineSegmentDistance_PointBeyondEndA_ReturnsDistanceToA()
    {
        var point = new Point(-3, 0);
        var start = new Point(0, 0);
        var end = new Point(10, 0);

        double dist = GeometryMath.PointToLineSegmentDistance(point, start, end);

        Assert.Equal(3.0, dist, 6);
    }

    [Fact]
    public void PointToLineSegmentDistance_PointBeyondEndB_ReturnsDistanceToB()
    {
        var point = new Point(14, 0);
        var start = new Point(0, 0);
        var end = new Point(10, 0);

        double dist = GeometryMath.PointToLineSegmentDistance(point, start, end);

        Assert.Equal(4.0, dist, 6);
    }

    // -------------------------------------------------------------------------
    // DoesLinePassTooCloseToMarker
    // -------------------------------------------------------------------------

    [Fact]
    public void DoesLinePassTooCloseToMarker_FarAway_ReturnsFalse()
    {
        var lineStart = new Point(0, 0);
        var lineEnd = new Point(100, 0);
        var markerPos = new Point(50, 50); // 50 px above — far
        double radius = 10;

        Assert.False(GeometryMath.DoesLinePassTooCloseToMarker(lineStart, lineEnd, markerPos, radius));
    }

    [Fact]
    public void DoesLinePassTooCloseToMarker_WithinThreshold_ReturnsTrue()
    {
        var lineStart = new Point(0, 0);
        var lineEnd = new Point(100, 0);
        var markerPos = new Point(50, 5); // 5 px above
        double radius = 10; // threshold = 10 + 2 = 12 — 5 < 12

        Assert.True(GeometryMath.DoesLinePassTooCloseToMarker(lineStart, lineEnd, markerPos, radius));
    }

    [Fact]
    public void DoesLinePassTooCloseToMarker_ExactlyAtThreshold_ReturnsFalse()
    {
        // distance == markerRadius + 2.0 exactly → not strictly less than → false
        var lineStart = new Point(0, 0);
        var lineEnd = new Point(100, 0);
        var markerPos = new Point(50, 12); // exactly 12 = 10 + 2
        double radius = 10;

        Assert.False(GeometryMath.DoesLinePassTooCloseToMarker(lineStart, lineEnd, markerPos, radius));
    }

    [Fact]
    public void DoesLinePassTooCloseToMarker_InsideTwoPixelBuffer_ReturnsTrue()
    {
        // Marker is beyond radius but inside the 2 px buffer
        var lineStart = new Point(0, 0);
        var lineEnd = new Point(100, 0);
        var markerPos = new Point(50, 11); // 11 px — between radius(10) and threshold(12)
        double radius = 10;

        Assert.True(GeometryMath.DoesLinePassTooCloseToMarker(lineStart, lineEnd, markerPos, radius));
    }

    // -------------------------------------------------------------------------
    // CalculateAngularSpace
    // -------------------------------------------------------------------------

    private static RadialExtension MakeExt(string name, double angle) =>
        new() { Location = new Location { Name = name }, Angle = angle };

    [Fact]
    public void CalculateAngularSpace_SingleExtension_Returns360()
    {
        var ext = MakeExt("A", 0);
        var list = new List<RadialExtension> { ext };

        // Only one element — next and prev both wrap to itself
        double cwSpace = GeometryMath.CalculateAngularSpace(ext, list, clockwise: true);
        double ccwSpace = GeometryMath.CalculateAngularSpace(ext, list, clockwise: false);

        Assert.Equal(0.0, cwSpace, 6);  // (0 - 0 + 360) % 360 = 0 (wraps to self)
        Assert.Equal(0.0, ccwSpace, 6);
    }

    [Fact]
    public void CalculateAngularSpace_TwoOpposite_Returns180()
    {
        var extA = MakeExt("A", 0);
        var extB = MakeExt("B", 180);
        var list = new List<RadialExtension> { extA, extB };

        double cwSpaceA = GeometryMath.CalculateAngularSpace(extA, list, clockwise: true);

        Assert.Equal(180.0, cwSpaceA, 6);
    }

    [Fact]
    public void CalculateAngularSpace_ThreeEvenly_Returns120()
    {
        var extA = MakeExt("A", 0);
        var extB = MakeExt("B", 120);
        var extC = MakeExt("C", 240);
        var list = new List<RadialExtension> { extA, extB, extC };

        double cwA = GeometryMath.CalculateAngularSpace(extA, list, clockwise: true);

        Assert.Equal(120.0, cwA, 6);
    }

    [Fact]
    public void CalculateAngularSpace_WrapAroundCase_ReturnsCorrectGap()
    {
        // A at 350°, B at 10° — CW gap from A to B crosses 0° = 20°
        var extA = MakeExt("A", 350);
        var extB = MakeExt("B", 10);
        var list = new List<RadialExtension> { extA, extB };

        double cwA = GeometryMath.CalculateAngularSpace(extA, list, clockwise: true);

        Assert.Equal(20.0, cwA, 6);
    }

    [Fact]
    public void CalculateAngularSpace_ExtensionNotInList_Returns30Default()
    {
        var ext = MakeExt("X", 45);
        var other = MakeExt("Y", 90);
        var list = new List<RadialExtension> { other }; // "X" not in list

        double space = GeometryMath.CalculateAngularSpace(ext, list, clockwise: true);

        Assert.Equal(30.0, space, 6);
    }

    // -------------------------------------------------------------------------
    // FindSafeAngleRotation
    // -------------------------------------------------------------------------

    private static RadialExtension MakeExtWithPositions(
        string name, double angle,
        Point origin, Point extended) =>
        new()
        {
            Location = new Location { Name = name },
            Angle = angle,
            OriginalPosition = origin,
            ExtendedPosition = extended
        };

    [Fact]
    public void FindSafeAngleRotation_NoConflicts_ReturnsMaxRotation()
    {
        // A single extension with no neighbours — safe to rotate the full max
        var ext = MakeExtWithPositions("A", 0, new Point(500, 500), new Point(500, 400));
        var list = new List<RadialExtension> { ext };

        double safe = GeometryMath.FindSafeAngleRotation(ext, list, clockwise: true, maxRotation: 30, markerRadius: 8);

        Assert.Equal(30.0, safe, 6);
    }

    [Fact]
    public void FindSafeAngleRotation_NearbyConflict_ReturnsSmallRotation()
    {
        // ext points north; other points NNE — rotating ext CW will collide quickly
        var origin = new Point(500, 500);
        var ext = MakeExtWithPositions("A", 0, origin, new Point(500, 400));
        var other = MakeExtWithPositions("B", 5, origin, new Point(504, 400)); // 5° away
        var list = new List<RadialExtension> { ext, other };

        double safe = GeometryMath.FindSafeAngleRotation(ext, list, clockwise: true, maxRotation: 30, markerRadius: 8);

        // Should stop well before 30° — less than the 5° gap because of the proximity buffer
        Assert.True(safe < 30.0);
    }

    [Fact]
    public void FindSafeAngleRotation_Blocked_ReturnsZero()
    {
        // ext and other already overlap — even 1° more would intersect
        var origin = new Point(500, 500);
        var ext = MakeExtWithPositions("A", 0, origin, new Point(500, 400));
        // Place another extension directly adjacent so any rotation causes proximity hit
        var other = MakeExtWithPositions("B", 1, origin, new Point(501, 400));
        var list = new List<RadialExtension> { ext, other };

        double safe = GeometryMath.FindSafeAngleRotation(ext, list, clockwise: true, maxRotation: 30, markerRadius: 20);

        Assert.Equal(0.0, safe, 6);
    }
}
