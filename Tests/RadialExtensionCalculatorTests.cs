using System.Collections.Generic;
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
        ExtensionLineLength      = 100,
        AngleNudgeThreshold      = 5.0,
        AngleNudgeAmount         = 2.0
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
            [locA] = new Point(0,   0),
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
            [locA] = new Point(0,  0),
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
            [locA] = new Point(0,  0),
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
            [locA] = new Point(0,   0),
            [locB] = new Point(20,  0),   // cluster 1
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
        var calc  = new RadialExtensionCalculator(DefaultConfig());
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
            Locations   = new List<Location> { locA, locB },
            CenterPoint = new Point(100, 100)
        };
        var screenPositions = new Dictionary<Location, Point>
        {
            [locA] = new Point(95,  100),  // slightly left of center
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
            Locations   = new List<Location> { locA, locB },
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
            double dx = ext.ExtendedPosition.X - ext.OriginalPosition.X;
            double dy = ext.ExtendedPosition.Y - ext.OriginalPosition.Y;
            double length = System.Math.Sqrt(dx * dx + dy * dy);
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
            Locations   = new List<Location> { locA, locB },
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

    // ─── Constructor guard ───────────────────────────────────────────────────

    [Fact]
    public void Constructor_NullConfig_Throws()
    {
        Assert.Throws<System.ArgumentNullException>(() => new RadialExtensionCalculator(null!));
    }
}
