using System.Collections.Generic;
using System.Linq;
using System.Windows;
using InteractiveWorldMap.Models;
using InteractiveWorldMap.Utilities;
using Xunit;

namespace InteractiveWorldMap.Tests;

/// <summary>
/// Unit tests for <see cref="RadialExtensionCalculator"/>.
/// </summary>
public class RadialExtensionCalculatorTests
{
    private static RadialExtensionConfig DefaultConfig() => new()
    {
        ProximityThresholdPixels = 50,
        MinLocationsForExtension = 2,
        ExtensionLineLength = 100,
        AngleNudgeThreshold = 5.0,
        AngleNudgeAmount = 2.0
    };

    // ─── DetectDenseGroups ───────────────────────────────────────────────────

    [Fact]
    public void DetectDenseGroups_NullInput_ReturnsEmpty()
    {
        var calc = new RadialExtensionCalculator(DefaultConfig());
        Assert.Empty(calc.DetectDenseGroups(null!));
    }

    [Fact]
    public void DetectDenseGroups_EmptyInput_ReturnsEmpty()
    {
        var calc = new RadialExtensionCalculator(DefaultConfig());
        Assert.Empty(calc.DetectDenseGroups(new Dictionary<Location, Point>()));
    }

    [Fact]
    public void DetectDenseGroups_AllFarApart_ReturnsEmpty()
    {
        var calc = new RadialExtensionCalculator(DefaultConfig());
        var locA = new Location { Id = "a", Name = "A" };
        var locB = new Location { Id = "b", Name = "B" };
        var positions = new Dictionary<Location, Point>
        {
            [locA] = new Point(0, 0),
            [locB] = new Point(500, 0)   // 500px apart > threshold 50px
        };

        Assert.Empty(calc.DetectDenseGroups(positions));
    }

    [Fact]
    public void DetectDenseGroups_TwoCloseMarkers_ReturnsOneGroup()
    {
        var calc = new RadialExtensionCalculator(DefaultConfig());
        var locA = new Location { Id = "a", Name = "A" };
        var locB = new Location { Id = "b", Name = "B" };
        var positions = new Dictionary<Location, Point>
        {
            [locA] = new Point(0, 0),
            [locB] = new Point(30, 0)   // 30px apart < threshold 50px
        };

        var groups = calc.DetectDenseGroups(positions);

        Assert.Single(groups);
        Assert.Equal(2, groups[0].Count);
    }

    [Fact]
    public void DetectDenseGroups_ThreeCloseMarkers_ReturnsSingleGroup()
    {
        var calc = new RadialExtensionCalculator(DefaultConfig());
        var locA = new Location { Id = "a", Name = "A" };
        var locB = new Location { Id = "b", Name = "B" };
        var locC = new Location { Id = "c", Name = "C" };
        var positions = new Dictionary<Location, Point>
        {
            [locA] = new Point(0, 0),
            [locB] = new Point(20, 0),
            [locC] = new Point(40, 0)
        };

        var groups = calc.DetectDenseGroups(positions);

        Assert.Single(groups);
        Assert.Equal(3, groups[0].Count);
    }

    [Fact]
    public void DetectDenseGroups_TwoSeparateClusters_ReturnsTwoGroups()
    {
        var calc = new RadialExtensionCalculator(DefaultConfig());
        var locA = new Location { Id = "a", Name = "A" };
        var locB = new Location { Id = "b", Name = "B" };
        var locC = new Location { Id = "c", Name = "C" };
        var locD = new Location { Id = "d", Name = "D" };
        var positions = new Dictionary<Location, Point>
        {
            [locA] = new Point(0, 0),
            [locB] = new Point(20, 0),   // cluster 1
            [locC] = new Point(500, 0),
            [locD] = new Point(520, 0)    // cluster 2
        };

        var groups = calc.DetectDenseGroups(positions);

        Assert.Equal(2, groups.Count);
    }

    // ─── CalculateRadialExtensions ───────────────────────────────────────────

    [Fact]
    public void CalculateRadialExtensions_NullGroup_ReturnsEmpty()
    {
        var calc = new RadialExtensionCalculator(DefaultConfig());
        Assert.Empty(calc.CalculateRadialExtensions(null!, new Dictionary<Location, Point>(), 800, 600));
    }

    [Fact]
    public void CalculateRadialExtensions_EmptyGroup_ReturnsEmpty()
    {
        var calc = new RadialExtensionCalculator(DefaultConfig());
        var group = new DenseMarkerGroup { Locations = new System.Collections.Generic.List<Location>() };
        Assert.Empty(calc.CalculateRadialExtensions(group, new Dictionary<Location, Point>(), 800, 600));
    }

    [Fact]
    public void CalculateRadialExtensions_TwoMarkers_ReturnsTwoExtensions()
    {
        var calc = new RadialExtensionCalculator(DefaultConfig());
        var locA = new Location { Id = "a", Name = "A" };
        var locB = new Location { Id = "b", Name = "B" };
        var group = new DenseMarkerGroup
        {
            Locations = new List<Location> { locA, locB },
            CenterPoint = new Point(100, 100)
        };
        var screenPositions = new Dictionary<Location, Point>
        {
            [locA] = new Point(95, 100),  // slightly left of center
            [locB] = new Point(105, 100)   // slightly right of center
        };

        var extensions = calc.CalculateRadialExtensions(group, screenPositions, 800, 600);

        Assert.Equal(2, extensions.Count);
    }

    [Fact]
    public void CalculateRadialExtensions_ExtensionsHaveNonZeroLength()
    {
        var calc = new RadialExtensionCalculator(DefaultConfig());
        var locA = new Location { Id = "a", Name = "A" };
        var locB = new Location { Id = "b", Name = "B" };
        var group = new DenseMarkerGroup
        {
            Locations = new List<Location> { locA, locB },
            CenterPoint = new Point(200, 200)
        };
        var screenPositions = new Dictionary<Location, Point>
        {
            [locA] = new Point(190, 200),
            [locB] = new Point(210, 200)
        };

        var extensions = calc.CalculateRadialExtensions(group, screenPositions, 800, 600);

        foreach (var ext in extensions)
        {
            double length = CoordinateMapper.DistanceBetween(ext.OriginalPosition, ext.ExtendedPosition);
            Assert.True(length > 0, $"Extension for {ext.Location.Name} has zero length");
        }
    }

    [Fact]
    public void CalculateRadialExtensions_OppositeMarkers_AnglesRoughlyOpposite()
    {
        var calc = new RadialExtensionCalculator(DefaultConfig());
        var locA = new Location { Id = "a", Name = "A" };
        var locB = new Location { Id = "b", Name = "B" };
        var group = new DenseMarkerGroup
        {
            Locations = new List<Location> { locA, locB },
            CenterPoint = new Point(200, 200)
        };
        // Place markers directly north and south of center
        var screenPositions = new Dictionary<Location, Point>
        {
            [locA] = new Point(200, 190),  // north of center
            [locB] = new Point(200, 210)   // south of center
        };

        var extensions = calc.CalculateRadialExtensions(group, screenPositions, 800, 600);

        Assert.Equal(2, extensions.Count);
        double angleDiff = System.Math.Abs(extensions[0].Angle - extensions[1].Angle);
        // Angles should be roughly opposite (~180° apart); nudging only applies for CLOSE angles
        Assert.True(angleDiff > 100, $"Expected roughly opposite angles but got diff={angleDiff:F1}°");
    }

    // ─── ValidateNoCrossings ─────────────────────────────────────────────────

    [Fact]
    public void ValidateNoCrossings_AlwaysReturnsTrue()
    {
        var calc = new RadialExtensionCalculator(DefaultConfig());
        Assert.True(calc.ValidateNoCrossings(new List<RadialExtension>()));
        Assert.True(calc.ValidateNoCrossings(null!));
    }

    // ─── Slice B2: Dense cluster distinct angles ────────────────────────────

    [Fact]
    public void CalculateExtensions_WithDenseCluster_ReturnsDistinctAngles()
    {
        var calc = new RadialExtensionCalculator(DefaultConfig());
        var locs = Enumerable.Range(0, 5)
            .Select(i => new Location { Id = $"l{i}", Name = $"L{i}" })
            .ToList();
        var group = new DenseMarkerGroup
        {
            Locations = locs,
            CenterPoint = new Point(200, 200)
        };
        // Place 5 markers in a tight cluster around center
        var screenPositions = new Dictionary<Location, Point>
        {
            [locs[0]] = new Point(198, 198),
            [locs[1]] = new Point(202, 199),
            [locs[2]] = new Point(199, 203),
            [locs[3]] = new Point(204, 201),
            [locs[4]] = new Point(197, 202)
        };

        var extensions = calc.CalculateRadialExtensions(group, screenPositions, 800, 600);

        Assert.Equal(5, extensions.Count);
        var angles = extensions.Select(e => e.Angle).OrderBy(a => a).ToList();
        for (int i = 1; i < angles.Count; i++)
        {
            double gap = angles[i] - angles[i - 1];
            Assert.True(gap > 0.5,
                $"Angles too close: {angles[i - 1]:F2}° and {angles[i]:F2}° (gap={gap:F2}°)");
        }
    }

    // ─── Slice B2: Canvas bounds enforcement ────────────────────────────────

    [Fact]
    public void CalculateExtensions_WithCanvasBounds_KeepsHeadsInsideBounds()
    {
        // Use a small canvas and place markers near the top-left corner
        // so extensions would overshoot without clamping.
        //
        // This was skipped for a while: the clamp computed the distance to the edge correctly
        // and then raised it back to a 20px minimum, so a marker 5px from the top still put its
        // head at Y=-6.09. The floor is gone -- see CalculateMaxLength.
        var config = DefaultConfig();
        config.ExtensionLineLength = 200;
        var calc = new RadialExtensionCalculator(config);

        var locA = new Location { Id = "a", Name = "A" };
        var locB = new Location { Id = "b", Name = "B" };
        var locC = new Location { Id = "c", Name = "C" };
        var group = new DenseMarkerGroup
        {
            Locations = new List<Location> { locA, locB, locC },
            CenterPoint = new Point(10, 10)
        };
        var screenPositions = new Dictionary<Location, Point>
        {
            [locA] = new Point(5, 5),
            [locB] = new Point(15, 5),
            [locC] = new Point(10, 15)
        };

        double canvasW = 100, canvasH = 100;
        var extensions = calc.CalculateRadialExtensions(group, screenPositions, canvasW, canvasH);

        Assert.Equal(3, extensions.Count);
        foreach (var ext in extensions)
        {
            Assert.True(ext.ExtendedPosition.X >= 0 && ext.ExtendedPosition.X <= canvasW,
                $"X={ext.ExtendedPosition.X} out of bounds [0,{canvasW}] for {ext.Location.Name}");
            Assert.True(ext.ExtendedPosition.Y >= 0 && ext.ExtendedPosition.Y <= canvasH,
                $"Y={ext.ExtendedPosition.Y} out of bounds [0,{canvasH}] for {ext.Location.Name}");
        }
    }

    [Fact]
    public void CalculateExtensions_MarkerCloserToTheEdgeThanTheOldFloor_DrawsAShortLineRatherThanLeaveTheCanvas()
    {
        // The specific shape of the old bug: the clamp worked out that only 5px of room remained
        // and then lengthened the line back to a 20px minimum, putting the head outside the
        // canvas. A minimum length is a minimum distance past the edge, so there is none -- the
        // line here has to come out shorter than 20px, not merely inside the bounds.
        var calc = new RadialExtensionCalculator(DefaultConfig());

        var top = new Location { Id = "t", Name = "Top" };
        var bottom = new Location { Id = "b", Name = "Bottom" };
        var group = new DenseMarkerGroup
        {
            Locations = new List<Location> { top, bottom },
            CenterPoint = new Point(50, 32.5)
        };
        var screenPositions = new Dictionary<Location, Point>
        {
            [top] = new Point(50, 5),      // 5px below the top edge, extending north
            [bottom] = new Point(50, 60)
        };

        var extensions = calc.CalculateRadialExtensions(group, screenPositions, 100, 100);

        var topExt = extensions.Single(e => e.Location.Id == "t");
        Assert.True(topExt.ExtendedPosition.Y >= 0,
            $"Head at Y={topExt.ExtendedPosition.Y} is above the canvas.");

        var length = CoordinateMapper.DistanceBetween(topExt.OriginalPosition, topExt.ExtendedPosition);
        Assert.True(length < 20.0,
            $"Length {length:F2} means a minimum length is still being applied; 5px of room " +
            "cannot produce a 20px line without the head leaving the canvas.");
        Assert.True(length > 0, "The line should still be drawn, just short.");
    }

    // ─── Slice B2: Wrap-around angle spacing ────────────────────────────────

    [Fact]
    public void CalculateExtensions_WithWrapAroundAngles_PreservesMinimumSpacing()
    {
        var calc = new RadialExtensionCalculator(DefaultConfig());
        // Four markers at angles roughly 350°, 355°, 5°, 10° — straddling 0°/360°
        var locs = new[]
        {
            new Location { Id = "w1", Name = "W1" },
            new Location { Id = "w2", Name = "W2" },
            new Location { Id = "w3", Name = "W3" },
            new Location { Id = "w4", Name = "W4" }
        };
        var group = new DenseMarkerGroup
        {
            Locations = locs.ToList(),
            CenterPoint = new Point(200, 200)
        };
        // 350° ≈ sin=-0.174, cos=0.985 → dx negative, dy negative (up-left)
        // 355° ≈ sin=-0.087, cos=0.996
        //   5° ≈ sin=0.087,  cos=0.996
        //  10° ≈ sin=0.174,  cos=0.985
        double r = 30;
        var screenPositions = new Dictionary<Location, Point>
        {
            [locs[0]] = CoordinateMapper.OffsetAtAngle(group.CenterPoint, r, -10),
            [locs[1]] = CoordinateMapper.OffsetAtAngle(group.CenterPoint, r, -5),
            [locs[2]] = CoordinateMapper.OffsetAtAngle(group.CenterPoint, r, 5),
            [locs[3]] = CoordinateMapper.OffsetAtAngle(group.CenterPoint, r, 10)
        };

        var extensions = calc.CalculateRadialExtensions(group, screenPositions, 800, 600);

        Assert.Equal(4, extensions.Count);
        var sorted = extensions.Select(e => CoordinateMapper.NormalizeAngle(e.Angle)).OrderBy(a => a).ToList();
        double minGap = double.MaxValue;
        for (int i = 0; i < sorted.Count; i++)
        {
            double next = sorted[(i + 1) % sorted.Count];
            double gap = CoordinateMapper.ClockwiseAngleDistance(sorted[i], next);
            minGap = Math.Min(minGap, gap);
        }
        Assert.True(minGap >= DefaultConfig().AngleNudgeThreshold,
            $"Minimum angular gap {minGap:F2}° is below threshold {DefaultConfig().AngleNudgeThreshold}°");
    }

    // ─── Slice B2: Extra edge cases ─────────────────────────────────────────

    [Fact]
    public void CalculateExtensions_SingleMarker_ReturnsOneExtension()
    {
        var calc = new RadialExtensionCalculator(DefaultConfig());
        var loc = new Location { Id = "s", Name = "S" };
        var group = new DenseMarkerGroup
        {
            Locations = new List<Location> { loc },
            CenterPoint = new Point(100, 100)
        };
        var screenPositions = new Dictionary<Location, Point>
        {
            [loc] = new Point(100, 100)
        };

        var extensions = calc.CalculateRadialExtensions(group, screenPositions, 800, 600);

        Assert.Single(extensions);
        Assert.Equal(loc, extensions[0].Location);
    }

    [Fact]
    public void CalculateExtensions_AllMarkersAtSamePosition_AllAnglesEqual()
    {
        var calc = new RadialExtensionCalculator(DefaultConfig());
        var locs = Enumerable.Range(0, 3)
            .Select(i => new Location { Id = $"c{i}", Name = $"C{i}" })
            .ToList();
        var group = new DenseMarkerGroup
        {
            Locations = locs,
            CenterPoint = new Point(200, 200)
        };
        // All markers at the exact same position as center → all get angle 180°
        // (atan2(0, -0) = π). Nudge pipeline skips diff<=0.01 so they stay equal.
        var screenPositions = new Dictionary<Location, Point>
        {
            [locs[0]] = new Point(200, 200),
            [locs[1]] = new Point(200, 200),
            [locs[2]] = new Point(200, 200)
        };

        var extensions = calc.CalculateRadialExtensions(group, screenPositions, 800, 600);

        Assert.Equal(3, extensions.Count);
        // All angles should be identical (nudge pipeline cannot spread diff=0)
        var angles = extensions.Select(e => e.Angle).ToList();
        Assert.All(angles, a => Assert.Equal(angles[0], a));
        // Extensions should still have non-zero length
        foreach (var ext in extensions)
        {
            double length = CoordinateMapper.DistanceBetween(ext.OriginalPosition, ext.ExtendedPosition);
            Assert.True(length > 0, $"Extension for {ext.Location.Name} has zero length");
        }
    }

    [Fact]
    public void CalculateExtensions_MarkersNearRightEdge_ClampsLength()
    {
        var config = DefaultConfig();
        config.ExtensionLineLength = 500;
        var calc = new RadialExtensionCalculator(config);

        var locA = new Location { Id = "r1", Name = "R1" };
        var locB = new Location { Id = "r2", Name = "R2" };
        var group = new DenseMarkerGroup
        {
            Locations = new List<Location> { locA, locB },
            CenterPoint = new Point(745, 300)
        };
        // Horizontally separated so one natural angle is rightward (~90°) and
        // ExtensionLineLength 500 requires CalculateMaxLength clamping.
        var screenPositions = new Dictionary<Location, Point>
        {
            [locA] = new Point(750, 300),
            [locB] = new Point(740, 300)
        };

        var extensions = calc.CalculateRadialExtensions(group, screenPositions, 800, 600);

        Assert.Equal(2, extensions.Count);
        Assert.Contains(extensions, ext => ext.ExtendedPosition.X > ext.OriginalPosition.X);
        foreach (var ext in extensions)
        {
            Assert.True(ext.ExtendedPosition.X <= 800,
                $"X={ext.ExtendedPosition.X} exceeds canvas width for {ext.Location.Name}");
        }
    }

    // ─── Constructor guard ───────────────────────────────────────────────────

    [Fact]
    public void Constructor_NullConfig_Throws()
    {
        Assert.Throws<System.ArgumentNullException>(() => new RadialExtensionCalculator(null!));
    }
}
