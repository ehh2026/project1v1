using System.Collections.Generic;
using System.Windows;
using InteractiveWorldMap.Models;
using InteractiveWorldMap.Services;
using InteractiveWorldMap.Tests.TestHelpers;
using InteractiveWorldMap.Utilities;
using Xunit;

namespace InteractiveWorldMap.Tests;

/// <summary>
/// Behavioral tests for marker placement during zoom-out animation. These tests
/// verify whether the per-frame marker screen positions returned by the
/// orchestrator match the source-point-to-screen projection at the *current*
/// interpolated viewport. If they match, the orchestrator is tracking. If not,
/// the bug is in the orchestrator's animation path.
/// </summary>
public class ZoomOutTrackingTests
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

    private static MarkerPlacementOrchestrator CreateOrchestrator()
    {
        var cfg = TestConfig();
        var logger = new MockLogger();
        var calc = new RadialExtensionCalculator(cfg.RadialExtension);
        var adjuster = new RadialExtensionAdjuster(logger, cfg);
        return new MarkerPlacementOrchestrator(cfg, logger, calc, adjuster);
    }

    /// <summary>
    /// Builds a realistic zoom-out scenario: from a zoomed-in viewport (centered
    /// on a cluster) back to the full map.
    /// </summary>
    private static (ViewportState start, ViewportState end, ViewportState[] intermediates)
        BuildZoomOutScenario(ViewportCalculator calc, double sourceW, double sourceH,
            double containerW, double containerH, double zoom, double clusterCenterX, double clusterCenterY)
    {
        var start = ViewportState.CreateZoomedView(
            clusterCenterX, clusterCenterY, zoom,
            sourceW, sourceH, containerW, containerH);
        var end = ViewportState.CreateFullMapView(
            sourceW, sourceH, containerW, containerH);

        var progresses = new[] { 0.0, 0.25, 0.5, 0.75, 1.0 };
        var intermediates = new ViewportState[progresses.Length];
        for (var i = 0; i < progresses.Length; i++)
            intermediates[i] = calc.Interpolate(start, end, progresses[i]);

        return (start, end, intermediates);
    }

    [Fact]
    public void ZoomOut_OrchestratorReturnsPlacementsMatchingMapProjection_EveryFrame()
    {
        // Reproduce a realistic zoom-out: a single marker near a cluster center.
        const double sourceW = 8198;
        const double sourceH = 5542;
        const double containerW = 1920;
        const double containerH = 1080;
        const double zoom = 55.0;
        const double clusterCenterX = 1100;
        const double clusterCenterY = 900;

        var marker = new Location { Id = "m1", Name = "M1", PixelX = 1100, PixelY = 900 };

        var calc = new ViewportCalculator();
        var orchestrator = CreateOrchestrator();
        var (_, _, frames) = BuildZoomOutScenario(
            calc, sourceW, sourceH, containerW, containerH, zoom, clusterCenterX, clusterCenterY);

        foreach (var frame in frames)
        {
            var result = orchestrator.Compute(
                frame, containerW, containerH, isAnimating: true,
                new List<(Location, double, double)> { (marker, marker.PixelX, marker.PixelY) },
                new List<Point>());

            var placement = Assert.Single(result.IndividualPlacements);

            // Where the orchestrator says the marker should be.
            var placementCenterX = placement.Left + TestConfig().LocationMarkerSize / 2.0;
            var placementCenterY = placement.Top + TestConfig().LocationMarkerSize / 2.0;

            // Where the map image actually shows the marker's source point.
            var expected = frame.SourceToScreen(marker.PixelX, marker.PixelY, containerW, containerH);

            // If these match, the orchestrator's animation path is correctly
            // tracking the map. Any visual disconnect at runtime must come from
            // a downstream consumer (ApplyIndividualPlacements, manual layout,
            // or visibility changes) failing to honor these positions.
            Assert.Equal(expected.X, placementCenterX, precision: 3);
            Assert.Equal(expected.Y, placementCenterY, precision: 3);
        }
    }

    [Fact]
    public void ZoomOut_OrchestratorPlacementsDifferAcrossFrames()
    {
        // Sanity check: confirm that the placement actually moves as the
        // viewport interpolates. A "frozen" orchestrator would return identical
        // positions across all frames and fail this test.
        const double sourceW = 8198;
        const double sourceH = 5542;
        const double containerW = 1920;
        const double containerH = 1080;
        const double zoom = 55.0;
        const double clusterCenterX = 1100;
        const double clusterCenterY = 900;

        var marker = new Location { Id = "m1", Name = "M1", PixelX = 1100, PixelY = 900 };

        var calc = new ViewportCalculator();
        var orchestrator = CreateOrchestrator();
        var (_, _, frames) = BuildZoomOutScenario(
            calc, sourceW, sourceH, containerW, containerH, zoom, clusterCenterX, clusterCenterY);

        Point? firstCenter = null;
        var seenDifferent = false;

        foreach (var frame in frames)
        {
            var result = orchestrator.Compute(
                frame, containerW, containerH, isAnimating: true,
                new List<(Location, double, double)> { (marker, marker.PixelX, marker.PixelY) },
                new List<Point>());

            var placement = Assert.Single(result.IndividualPlacements);
            var center = new Point(
                placement.Left + TestConfig().LocationMarkerSize / 2.0,
                placement.Top + TestConfig().LocationMarkerSize / 2.0);

            if (firstCenter is null)
            {
                firstCenter = center;
            }
            else if (!seenDifferent && (Math.Abs(center.X - firstCenter.Value.X) > 1.0 ||
                                          Math.Abs(center.Y - firstCenter.Value.Y) > 1.0))
            {
                seenDifferent = true;
            }
        }

        Assert.True(seenDifferent,
            "Orchestrator returned the same screen position across all zoom-out frames; markers would be visually frozen.");
    }
}
