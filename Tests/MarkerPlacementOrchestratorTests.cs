using System.Collections.Generic;
using System.Linq;
using System.Windows;
using InteractiveWorldMap.Models;
using InteractiveWorldMap.Services;
using InteractiveWorldMap.Tests.TestHelpers;
using InteractiveWorldMap.Utilities;
using Xunit;

namespace InteractiveWorldMap.Tests;

/// <summary>
/// Unit tests for <see cref="MarkerPlacementOrchestrator"/>.
/// </summary>
public class MarkerPlacementOrchestratorTests
{
    private static VisualConfig TestConfig() => new()
    {
        LocationMarkerSize = 20,
        ClusterMarkerSize = 30,
        RadialExtension = new RadialExtensionConfig
        {
            Enabled = true,
            ProximityThresholdPixels = 50,
            MinLocationsForExtension = 2,
            ExtensionLineLength = 100,
            ZoomThresholdForExtensions = 10.0
        }
    };

    private static ViewportState TestViewport(double zoom = 55.0) =>
        new()
        {
            SourceImageWidth = 8198,
            SourceImageHeight = 5542,
            ViewportX = 1000,
            ViewportY = 800,
            ViewportWidth = 500,
            ViewportHeight = 400,
            ZoomLevel = zoom
        };

    private static MarkerPlacementOrchestrator CreateOrchestrator(VisualConfig? config = null)
    {
        var cfg = config ?? TestConfig();
        var logger = new MockLogger();
        var calc = new RadialExtensionCalculator(cfg.RadialExtension);
        var adjuster = new RadialExtensionAdjuster(logger, cfg);
        return new MarkerPlacementOrchestrator(cfg, logger, calc, adjuster);
    }

    [Fact]
    public void Compute_Animating_ReturnsFallbackModeWithAllIndividuals()
    {
        var locA = new Location { Id = "a", Name = "A", PixelX = 1100, PixelY = 900 };
        var locB = new Location { Id = "b", Name = "B", PixelX = 1120, PixelY = 910 };
        var viewport = TestViewport();
        var orchestrator = CreateOrchestrator();

        var result = orchestrator.Compute(
            viewport,
            1920,
            1080,
            isAnimating: true,
            new List<(Location, double, double)>
            {
                (locA, locA.PixelX, locA.PixelY),
                (locB, locB.PixelX, locB.PixelY)
            },
            new List<Point> { new(1200, 950) });

        Assert.Equal(MarkerPlacementMode.AnimatingFallback, result.Mode);
        Assert.Equal(2, result.IndividualPlacements.Count);
        Assert.Empty(result.ExtensionGroups);
        Assert.Single(result.ClusterPlacements);
    }

    [Fact]
    public void Compute_SparseMarkers_ReturnsNormalOnly()
    {
        var locA = new Location { Id = "a", Name = "A", PixelX = 1100, PixelY = 900 };
        var locB = new Location { Id = "b", Name = "B", PixelX = 5000, PixelY = 4000 };
        var viewport = TestViewport();
        var orchestrator = CreateOrchestrator();

        var result = orchestrator.Compute(
            viewport,
            1920,
            1080,
            isAnimating: false,
            new List<(Location, double, double)>
            {
                (locA, locA.PixelX, locA.PixelY),
                (locB, locB.PixelX, locB.PixelY)
            },
            Array.Empty<Point>());

        Assert.Equal(MarkerPlacementMode.NormalOnly, result.Mode);
        Assert.Equal(2, result.IndividualPlacements.Count);
        Assert.Empty(result.ExtensionGroups);
    }

    [Fact]
    public void Compute_DenseCluster_ReturnsExtensions()
    {
        var locA = new Location { Id = "a", Name = "A", PixelX = 1100, PixelY = 900 };
        var locB = new Location { Id = "b", Name = "B", PixelX = 1120, PixelY = 910 };
        var locC = new Location { Id = "c", Name = "C", PixelX = 1140, PixelY = 920 };
        var viewport = TestViewport();
        var orchestrator = CreateOrchestrator();

        var result = orchestrator.Compute(
            viewport,
            1920,
            1080,
            isAnimating: false,
            new List<(Location, double, double)>
            {
                (locA, locA.PixelX, locA.PixelY),
                (locB, locB.PixelX, locB.PixelY),
                (locC, locC.PixelX, locC.PixelY)
            },
            Array.Empty<Point>());

        Assert.Equal(MarkerPlacementMode.WithExtensions, result.Mode);
        Assert.NotEmpty(result.ExtensionGroups);
        Assert.All(result.ExtensionGroups, g => Assert.NotEmpty(g.Extensions));
    }

    [Fact]
    public void Compute_DenseClusterAgainstTheTopEdge_KeepsEveryHeadOnTheCanvas()
    {
        // The calculator clamps to the canvas, but the adjuster runs afterwards, separates
        // overlapping heads by changing line lengths, and can lengthen. It knows nothing about the
        // canvas and enforces its own MinimumLineLength -- which near an edge is a minimum
        // distance *past* the edge. MinimumLineLength is set well beyond the room available here
        // so that any un-clamped lengthening lands outside the container.
        var config = TestConfig();
        config.RadialExtension.MinimumLineLength = 60;
        config.RadialExtension.ProximityThresholdPixels = 50;

        // A tight knot of markers a couple of source pixels below the top of the crop, so the
        // upward extensions have only a few screen pixels of room.
        var locs = new[]
        {
            new Location { Id = "e1", Name = "E1", PixelX = 1250, PixelY = 801.5 },
            new Location { Id = "e2", Name = "E2", PixelX = 1252, PixelY = 801.6 },
            new Location { Id = "e3", Name = "E3", PixelX = 1248, PixelY = 802.0 },
            new Location { Id = "e4", Name = "E4", PixelX = 1250, PixelY = 803.5 }
        };

        const double containerWidth = 1920, containerHeight = 1080;
        var result = CreateOrchestrator(config).Compute(
            TestViewport(),
            containerWidth,
            containerHeight,
            isAnimating: false,
            locs.Select(l => (l, l.PixelX, l.PixelY)).ToList(),
            Array.Empty<Point>());

        var heads = result.ExtensionGroups.SelectMany(g => g.Extensions).ToList();
        Assert.NotEmpty(heads);

        foreach (var ext in heads)
        {
            Assert.True(
                ext.ExtendedPosition.X >= 0 && ext.ExtendedPosition.X <= containerWidth &&
                ext.ExtendedPosition.Y >= 0 && ext.ExtendedPosition.Y <= containerHeight,
                $"{ext.Location.Name} head at ({ext.ExtendedPosition.X:F2}, " +
                $"{ext.ExtendedPosition.Y:F2}) is outside the {containerWidth}x{containerHeight} " +
                "canvas after adjustment.");
        }
    }

    [Fact]
    public void Compute_BelowZoomThreshold_ReturnsNormalOnly()
    {
        var locA = new Location { Id = "a", Name = "A", PixelX = 1100, PixelY = 900 };
        var locB = new Location { Id = "b", Name = "B", PixelX = 1120, PixelY = 910 };
        var viewport = TestViewport(zoom: 5.0);
        var orchestrator = CreateOrchestrator();

        var result = orchestrator.Compute(
            viewport,
            1920,
            1080,
            isAnimating: false,
            new List<(Location, double, double)>
            {
                (locA, locA.PixelX, locA.PixelY),
                (locB, locB.PixelX, locB.PixelY)
            },
            Array.Empty<Point>());

        Assert.Equal(MarkerPlacementMode.NormalOnly, result.Mode);
        Assert.False(result.ShouldApplyExtensions);
    }

}
