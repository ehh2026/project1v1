using System;
using System.Collections.Generic;
using System.Linq;
using InteractiveWorldMap.Models;
using InteractiveWorldMap.Utilities;
using Xunit;

namespace InteractiveWorldMap.Tests;

/// <summary>
/// Unit tests for <see cref="SpatialGrid"/> cell bucketing and 3×3 neighbor queries.
/// </summary>
public class SpatialGridTests
{
    [Fact]
    public void Constructor_NonPositiveCellSize_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new SpatialGrid(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new SpatialGrid(-10));
    }

    [Fact]
    public void Insert_Null_Throws()
    {
        var grid = new SpatialGrid(100);
        Assert.Throws<ArgumentNullException>(() => grid.Insert(null!));
    }

    [Fact]
    public void GetCellKey_FloorsIntoUniformCells()
    {
        var grid = new SpatialGrid(100);

        Assert.Equal((0, 0), grid.GetCellKey(0, 0));
        Assert.Equal((0, 0), grid.GetCellKey(99.9, 50));
        Assert.Equal((1, 0), grid.GetCellKey(100, 0));
        Assert.Equal((1, 2), grid.GetCellKey(150, 250));
        Assert.Equal((-1, 0), grid.GetCellKey(-1, 10));
    }

    [Fact]
    public void Insert_PlacesLocationInExpectedCell()
    {
        var grid = new SpatialGrid(100);
        var a = new Location { Id = "a", PixelX = 50, PixelY = 50 };
        var b = new Location { Id = "b", PixelX = 150, PixelY = 50 };
        grid.Insert(a);
        grid.Insert(b);

        Assert.Equal(2, grid.OccupiedCellCount);
        var fromA = grid.GetCandidatesInNeighborhood(a).Select(l => l.Id).ToHashSet();
        Assert.Contains("a", fromA);
        Assert.Contains("b", fromA); // adjacent cell in 3×3
    }

    [Fact]
    public void GetCandidatesInNeighborhood_IncludesSameCellAndMooreNeighbors()
    {
        var grid = new SpatialGrid(100);
        // Center cell (1,1): points in [100,200) x [100,200)
        var center = new Location { Id = "center", PixelX = 150, PixelY = 150 };
        var sameCell = new Location { Id = "same", PixelX = 120, PixelY = 180 };
        var diagNeighbor = new Location { Id = "diag", PixelX = 250, PixelY = 250 }; // cell (2,2)
        var farAway = new Location { Id = "far", PixelX = 1000, PixelY = 1000 }; // outside 3×3

        grid.InsertAll(new[] { center, sameCell, diagNeighbor, farAway });

        var ids = grid.GetCandidatesInNeighborhood(center).Select(l => l.Id).ToHashSet();
        Assert.Contains("center", ids);
        Assert.Contains("same", ids);
        Assert.Contains("diag", ids);
        Assert.DoesNotContain("far", ids);
    }

    [Fact]
    public void GetCandidatesInNeighborhood_Null_Throws()
    {
        var grid = new SpatialGrid(50);
        Assert.Throws<ArgumentNullException>(() => grid.GetCandidatesInNeighborhood(null!).ToList());
    }

    [Fact]
    public void InsertAll_Null_Throws()
    {
        var grid = new SpatialGrid(50);
        Assert.Throws<ArgumentNullException>(() => grid.InsertAll(null!));
    }
}
