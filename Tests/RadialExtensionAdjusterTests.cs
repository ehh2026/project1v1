using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using InteractiveWorldMap.Models;
using InteractiveWorldMap.Services;
using InteractiveWorldMap.Tests.TestHelpers;
using InteractiveWorldMap.Utilities;
using Xunit;

namespace InteractiveWorldMap.Tests;

public class RadialExtensionAdjusterTests
{
    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static VisualConfig MakeConfig() => new VisualConfig();

    private static MockLogger NewLogger() => new MockLogger();

    private static RadialExtensionAdjuster NewAdjuster() =>
        new RadialExtensionAdjuster(NewLogger(), MakeConfig());

    private static RadialExtension MakeExt(
        string name, double angle, int groupId,
        Point origin, double length) =>
        new()
        {
            Location = new Location { Name = name },
            Angle = angle,
            GroupId = groupId,
            OriginalPosition = origin,
            ExtendedPosition = new Point(
                origin.X + length * Math.Sin(angle * Math.PI / 180.0),
                origin.Y - length * Math.Cos(angle * Math.PI / 180.0))
        };

    private static double Distance(Point a, Point b)
    {
        double dx = b.X - a.X, dy = b.Y - a.Y;
        return Math.Sqrt(dx * dx + dy * dy);
    }

    // -------------------------------------------------------------------------
    // Constructor guards
    // -------------------------------------------------------------------------

    [Fact]
    public void Constructor_NullLogger_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new RadialExtensionAdjuster(null!, MakeConfig()));
    }

    [Fact]
    public void Constructor_NullConfig_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new RadialExtensionAdjuster(NewLogger(), null!));
    }

    // -------------------------------------------------------------------------
    // Trivial / no-op cases
    // -------------------------------------------------------------------------

    [Fact]
    public void AdjustExtensions_EmptyList_DoesNotThrow()
    {
        // Should complete silently with no extensions to adjust
        NewAdjuster().AdjustExtensions(new List<RadialExtension>(), 12.0);
    }

    [Fact]
    public void AdjustExtensions_SingleExtension_AngleAndPositionUnchanged()
    {
        var ext = MakeExt("A", 45.0, 1, new Point(500, 500), 40.0);
        double originalAngle = ext.Angle;
        Point originalPos = ext.ExtendedPosition;

        NewAdjuster().AdjustExtensions(new List<RadialExtension> { ext }, 12.0);

        Assert.Equal(originalAngle, ext.Angle, 6);
        Assert.Equal(originalPos, ext.ExtendedPosition);
    }

    // -------------------------------------------------------------------------
    // Angle nudging within groups
    // -------------------------------------------------------------------------

    [Fact]
    public void AdjustExtensions_SameGroupIdenticalAngles_AnglesGetSeparated()
    {
        var origin = new Point(500, 500);
        var ext1 = MakeExt("A", 45.0, 1, origin, 40.0);
        var ext2 = MakeExt("B", 45.0, 1, origin, 40.0);

        NewAdjuster().AdjustExtensions(new List<RadialExtension> { ext1, ext2 }, 12.0);

        double angleDiff = Math.Abs(ext2.Angle - ext1.Angle);
        Assert.True(angleDiff > 0.0,
            "Extensions with identical angles in the same group should be nudged apart");
    }

    [Fact]
    public void AdjustExtensions_SameGroupWellSeparatedAngles_AnglesUnchanged()
    {
        var origin = new Point(500, 500);
        // 90° gap — far above AngleNudgeThreshold (2°)
        var ext1 = MakeExt("A", 0.0, 1, origin, 40.0);
        var ext2 = MakeExt("B", 90.0, 1, origin, 40.0);

        NewAdjuster().AdjustExtensions(new List<RadialExtension> { ext1, ext2 }, 12.0);

        Assert.Equal(0.0, ext1.Angle, 6);
        Assert.Equal(90.0, ext2.Angle, 6);
    }

    [Fact]
    public void AdjustExtensions_SameGroupThreeIdenticalAngles_AllAnglesDiffer()
    {
        var origin = new Point(500, 500);
        var ext1 = MakeExt("A", 0.0, 1, origin, 40.0);
        var ext2 = MakeExt("B", 0.0, 1, origin, 40.0);
        var ext3 = MakeExt("C", 0.0, 1, origin, 40.0);
        var list = new List<RadialExtension> { ext1, ext2, ext3 };

        NewAdjuster().AdjustExtensions(list, 12.0);

        var angles = list.Select(e => e.Angle).OrderBy(a => a).ToList();
        for (int i = 0; i < angles.Count; i++)
            for (int j = i + 1; j < angles.Count; j++)
                Assert.True(
                    Math.Abs(angles[i] - angles[j]) > 0.0,
                    $"Extension pair [{i},{j}] should have distinct angles after nudging");
    }

    // -------------------------------------------------------------------------
    // Position overlap adjustment
    // -------------------------------------------------------------------------

    [Fact]
    public void AdjustExtensions_OverlappingExtendedPositions_PositionsMoveApart()
    {
        // Two extensions from opposing origins whose tips meet at the same point.
        // The lines are collinear, so no intersection or proximity issues are triggered;
        // only AdjustPositionsAcrossExtensions fires and separates the tips.
        double markerSize = 12.0;
        double minGap = markerSize * 2.5; // 30 px

        var ext1 = new RadialExtension
        {
            Location = new Location { Name = "A" },
            GroupId = 1,
            Angle = 90.0,
            OriginalPosition = new Point(100, 500),
            ExtendedPosition = new Point(300, 500)  // 200 px east of origin
        };
        var ext2 = new RadialExtension
        {
            Location = new Location { Name = "B" },
            GroupId = 2,
            Angle = 270.0,
            OriginalPosition = new Point(500, 500),
            ExtendedPosition = new Point(300, 500)  // 200 px west of origin — same tip
        };

        NewAdjuster().AdjustExtensions(new List<RadialExtension> { ext1, ext2 }, markerSize);

        double finalDist = Distance(ext1.ExtendedPosition, ext2.ExtendedPosition);
        Assert.True(finalDist >= minGap,
            $"Extended positions should be ≥ {minGap:F0}px apart after adjustment; got {finalDist:F1}px");
    }

    [Fact]
    public void AdjustExtensions_WellSeparatedPositions_PositionsUnchanged()
    {
        // Two extensions 400 px apart — no possible interaction
        var ext1 = MakeExt("A", 0.0, 1, new Point(100, 500), 40.0);
        var ext2 = MakeExt("B", 0.0, 2, new Point(500, 500), 40.0);
        Point pos1Before = ext1.ExtendedPosition;
        Point pos2Before = ext2.ExtendedPosition;

        NewAdjuster().AdjustExtensions(new List<RadialExtension> { ext1, ext2 }, 12.0);

        Assert.Equal(pos1Before, ext1.ExtendedPosition);
        Assert.Equal(pos2Before, ext2.ExtendedPosition);
    }

    // -------------------------------------------------------------------------
    // Intersection fixing
    // -------------------------------------------------------------------------

    [Fact]
    public void AdjustExtensions_CrossingLines_AtLeastOneAngleChanges()
    {
        // Two lines forming an X: both verified to intersect at t1=t2=1/3
        // ext1: (490,500)→(520,470), ext2: (510,500)→(480,470)
        var ext1 = new RadialExtension
        {
            Location = new Location { Name = "A" },
            GroupId = 1,
            Angle = 45.0,
            OriginalPosition = new Point(490, 500),
            ExtendedPosition = new Point(520, 470)
        };
        var ext2 = new RadialExtension
        {
            Location = new Location { Name = "B" },
            GroupId = 2,
            Angle = 315.0,
            OriginalPosition = new Point(510, 500),
            ExtendedPosition = new Point(480, 470)
        };

        double angle1Before = ext1.Angle;
        double angle2Before = ext2.Angle;

        NewAdjuster().AdjustExtensions(new List<RadialExtension> { ext1, ext2 }, 12.0);

        Assert.True(
            ext1.Angle != angle1Before || ext2.Angle != angle2Before,
            "Crossing lines should trigger angle rotation by FixLineIntersections");
    }

    [Fact]
    public void AdjustExtensions_NonCrossingParallelLines_AnglesUnchanged()
    {
        // Both point north, 400 px apart — no overlap, no intersection, no proximity
        var ext1 = MakeExt("A", 0.0, 1, new Point(100, 500), 40.0);
        var ext2 = MakeExt("B", 0.0, 2, new Point(500, 500), 40.0);

        NewAdjuster().AdjustExtensions(new List<RadialExtension> { ext1, ext2 }, 12.0);

        Assert.Equal(0.0, ext1.Angle, 6);
        Assert.Equal(0.0, ext2.Angle, 6);
    }

    // -------------------------------------------------------------------------
    // Idempotency
    // -------------------------------------------------------------------------

    [Fact]
    public void AdjustExtensions_AlreadyStable_SecondCallProducesNoChange()
    {
        var origin = new Point(500, 500);
        // Opposite angles — well separated, no intersections possible
        var ext1 = MakeExt("A", 0.0, 1, origin, 40.0);
        var ext2 = MakeExt("B", 180.0, 1, origin, 40.0);
        var list = new List<RadialExtension> { ext1, ext2 };

        var adjuster = NewAdjuster();
        adjuster.AdjustExtensions(list, 12.0);

        double angle1After = ext1.Angle;
        double angle2After = ext2.Angle;
        Point pos1After = ext1.ExtendedPosition;
        Point pos2After = ext2.ExtendedPosition;

        adjuster.AdjustExtensions(list, 12.0);

        Assert.Equal(angle1After, ext1.Angle, 6);
        Assert.Equal(angle2After, ext2.Angle, 6);
        Assert.Equal(pos1After, ext1.ExtendedPosition);
        Assert.Equal(pos2After, ext2.ExtendedPosition);
    }

    // -------------------------------------------------------------------------
    // Logging
    // -------------------------------------------------------------------------

    [Fact]
    public void AdjustExtensions_Always_LogsIterationProgress()
    {
        var logger = NewLogger();
        var adjuster = new RadialExtensionAdjuster(logger, MakeConfig());
        var ext = MakeExt("A", 0.0, 1, new Point(500, 500), 40.0);

        adjuster.AdjustExtensions(new List<RadialExtension> { ext }, 12.0);

        Assert.Contains(logger.InfoMessages, m => m.Contains("[IterativeAdjustment]"));
    }

    // -------------------------------------------------------------------------
    // Line-near-marker detection
    // -------------------------------------------------------------------------

    [Fact]
    public void AdjustExtensions_WithLineNearMarker_MovesLineAway()
    {
        // LINE->MARKER detection uses the other marker's ExtendedPosition (head), not its
        // origin, and compares against VisualConfig.LocationMarkerSize/2 + 2px (default 10px).
        // The markerSize argument only affects overlap minGap, not this check.
        //
        // A runs north along x=500; B's head sits 6px east of that shaft so detection fires.
        var logger = NewLogger();
        var adjuster = new RadialExtensionAdjuster(logger, MakeConfig());

        var ext1 = new RadialExtension
        {
            Location = new Location { Name = "A" },
            GroupId = 1,
            Angle = 0.0,
            OriginalPosition = new Point(500, 500),
            ExtendedPosition = new Point(500, 400)
        };
        var ext2 = new RadialExtension
        {
            Location = new Location { Name = "B" },
            GroupId = 2,
            Angle = 270.0,
            OriginalPosition = new Point(530, 450),
            ExtendedPosition = new Point(506, 450)
        };

        double angle1Before = ext1.Angle;
        double angle2Before = ext2.Angle;

        adjuster.AdjustExtensions(new List<RadialExtension> { ext1, ext2 }, 12.0);

        Assert.Contains(
            logger.InfoMessages,
            m => m.Contains("LINE->MARKER", StringComparison.Ordinal) ||
                 m.Contains("MARKER<-LINE", StringComparison.Ordinal));
        Assert.True(
            ext1.Angle != angle1Before || ext2.Angle != angle2Before,
            "Line passing near another marker head should trigger angle adjustment");
    }

    // -------------------------------------------------------------------------
    // Intersecting lines — reduce count
    // -------------------------------------------------------------------------

    [Fact]
    public void AdjustExtensions_WithIntersectingLines_ReducesIntersections()
    {
        // Two X-crossing lines plus a distant parallel line that does not intersect them.
        var ext1 = new RadialExtension
        {
            Location = new Location { Name = "A" },
            GroupId = 1,
            Angle = 45.0,
            OriginalPosition = new Point(480, 500),
            ExtendedPosition = new Point(520, 460)
        };
        var ext2 = new RadialExtension
        {
            Location = new Location { Name = "B" },
            GroupId = 2,
            Angle = 315.0,
            OriginalPosition = new Point(520, 500),
            ExtendedPosition = new Point(480, 460)
        };
        var ext3 = new RadialExtension
        {
            Location = new Location { Name = "C" },
            GroupId = 3,
            Angle = 0.0,
            OriginalPosition = new Point(400, 500),
            ExtendedPosition = new Point(400, 460)
        };
        var list = new List<RadialExtension> { ext1, ext2, ext3 };

        int intersectionsBefore = CountIntersections(list);

        NewAdjuster().AdjustExtensions(list, 12.0);

        int intersectionsAfter = CountIntersections(list);
        Assert.True(
            intersectionsAfter < intersectionsBefore,
            $"Expected intersections to decrease from {intersectionsBefore} but got {intersectionsAfter}");
    }

    private static int CountIntersections(List<RadialExtension> extensions)
    {
        int count = 0;
        for (int i = 0; i < extensions.Count; i++)
        {
            for (int j = i + 1; j < extensions.Count; j++)
            {
                if (GeometryMath.DoLineSegmentsIntersect(
                        extensions[i].OriginalPosition, extensions[i].ExtendedPosition,
                        extensions[j].OriginalPosition, extensions[j].ExtendedPosition))
                {
                    count++;
                }
            }
        }

        return count;
    }

    // -------------------------------------------------------------------------
    // Angle wrap-around near 0°/360°
    // -------------------------------------------------------------------------

    [Fact]
    public void AdjustExtensions_WithAngleNearZero_WrapsWithoutCrossing()
    {
        // Angles 1° apart across 0°/360° with a shared origin. Within-group angle nudging
        // uses a non-circular forward diff (so wrap pairs may not nudge that way), but the
        // nearly coincident heads still trip LINE->MARKER and must resolve without crossing.
        var origin = new Point(500, 500);
        var ext1 = MakeExt("A", 359.5, 1, origin, 40.0);
        var ext2 = MakeExt("B", 0.5, 1, origin, 40.0);

        NewAdjuster().AdjustExtensions(new List<RadialExtension> { ext1, ext2 }, 12.0);

        double angleDiff = Math.Abs(ext1.Angle - ext2.Angle);
        double normalizedDiff = angleDiff > 180 ? 360 - angleDiff : angleDiff;
        Assert.True(
            normalizedDiff >= MakeConfig().RadialExtension.AngleNudgeThreshold - 0.01,
            $"Angles near wrap-around should end at least ~threshold apart (got {normalizedDiff:F2}°)");

        bool intersects = GeometryMath.DoLineSegmentsIntersect(
            ext1.OriginalPosition, ext1.ExtendedPosition,
            ext2.OriginalPosition, ext2.ExtendedPosition);
        Assert.False(intersects, "Lines should not cross after wrap-around angle adjustment");
    }

    // -------------------------------------------------------------------------
    // Protected location (unobservable without a seam)
    // -------------------------------------------------------------------------

    [Fact(Skip =
        "ProtectedFromOverlapAdjustment is private per-call state inside AdjustExtensions; " +
        "a second AdjustExtensions call starts a fresh IterativeAdjustmentState, so protection " +
        "cannot be seeded or observed from tests without a production seam.")]
    public void AdjustExtensions_WithProtectedLocation_DoesNotMoveProtectedExtension()
    {
        // Characterization placeholder for Phase 4.3 naming. When a seam exists, seed an
        // intersecting pair within one AdjustExtensions call and assert protected names are
        // not further angle-nudged by AdjustForMarkerOverlaps on later iterations.
        Assert.True(false, "Unreachable while skipped — implement when a protection seam exists.");
    }
}
