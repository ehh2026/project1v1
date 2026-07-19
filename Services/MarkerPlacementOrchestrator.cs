using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using InteractiveWorldMap.Models;
using InteractiveWorldMap.Utilities;

namespace InteractiveWorldMap.Services
{
    /// <summary>
    /// Computes marker screen positions and radial extension groups without WPF canvas mutations.
    /// MainWindow applies the returned <see cref="MarkerPlacementResult"/>.
    /// </summary>
    public sealed class MarkerPlacementOrchestrator
    {
        private readonly RadialExtensionCalculator? _extensionCalculator;
        private readonly RadialExtensionAdjuster? _adjuster;
        private readonly VisualConfig _visualConfig;
        private readonly ILogger _logger;

        public MarkerPlacementOrchestrator(
            VisualConfig visualConfig,
            ILogger logger,
            RadialExtensionCalculator? extensionCalculator,
            RadialExtensionAdjuster? adjuster)
        {
            _visualConfig = visualConfig ?? throw new ArgumentNullException(nameof(visualConfig));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _extensionCalculator = extensionCalculator;
            _adjuster = adjuster;
        }

        /// <summary>
        /// Builds a placement plan for visible individual and cluster markers.
        /// </summary>
        /// <param name="viewport">Current map viewport.</param>
        /// <param name="containerWidth">Map canvas width.</param>
        /// <param name="containerHeight">Map canvas height.</param>
        /// <param name="isAnimating">When true, skips extension logic.</param>
        /// <param name="visibleIndividuals">Visible location markers (name + source pixel coords).</param>
        /// <param name="visibleClusterCenters">Cluster center points in source space.</param>
        public MarkerPlacementResult Compute(
            ViewportState viewport,
            double containerWidth,
            double containerHeight,
            bool isAnimating,
            IReadOnlyList<(Location Location, double PixelX, double PixelY)> visibleIndividuals,
            IReadOnlyList<Point> visibleClusterCenters)
        {
            var locationMarkerSize = _visualConfig.LocationMarkerSize;
            var clusterMarkerSize  = _visualConfig.ClusterMarkerSize;

            var clusterPlacements = BuildClusterPlacements(
                viewport, containerWidth, containerHeight, visibleClusterCenters, clusterMarkerSize);

            if (isAnimating)
            {
                var animatingIndividuals = BuildIndividualPlacements(
                    viewport, containerWidth, containerHeight, visibleIndividuals, locationMarkerSize);
                return new MarkerPlacementResult(
                    MarkerPlacementMode.AnimatingFallback,
                    animatingIndividuals,
                    clusterPlacements,
                    Array.Empty<DenseMarkerGroup>(),
                    shouldApplyExtensions: false);
            }

            bool shouldApplyExtensions = _visualConfig.RadialExtension.Enabled &&
                                         _extensionCalculator != null &&
                                         viewport.ZoomLevel >= _visualConfig.RadialExtension.ZoomThresholdForExtensions;
            var logRadialExtensionCalculation =
                _visualConfig.EnableDeveloperTools &&
                _visualConfig.Debug.LogRadialExtensionCalculation;

            if (logRadialExtensionCalculation)
            {
                _logger.LogInfo(
                    $"[MarkerPlacement] ZoomLevel={viewport.ZoomLevel:F2}, " +
                    $"Threshold={_visualConfig.RadialExtension.ZoomThresholdForExtensions}, " +
                    $"ShouldApply={shouldApplyExtensions}");
            }

            if (!shouldApplyExtensions)
            {
                var normalIndividuals = BuildIndividualPlacements(
                    viewport, containerWidth, containerHeight, visibleIndividuals, locationMarkerSize);
                return new MarkerPlacementResult(
                    MarkerPlacementMode.NormalOnly,
                    normalIndividuals,
                    clusterPlacements,
                    Array.Empty<DenseMarkerGroup>(),
                    shouldApplyExtensions: false);
            }

            var markerSourcePositions = visibleIndividuals.ToDictionary(
                t => t.Location,
                t => new Point(t.PixelX, t.PixelY));

            var markerScreenPositions = visibleIndividuals.ToDictionary(
                t => t.Location,
                t => viewport.SourceToScreen(t.PixelX, t.PixelY, containerWidth, containerHeight));

            if (logRadialExtensionCalculation)
                _logger.LogInfo($"[MarkerPlacement] Calculated {markerScreenPositions.Count} marker positions");

            var denseGroups = _extensionCalculator!.DetectDenseGroups(markerSourcePositions);

            if (logRadialExtensionCalculation)
                _logger.LogInfo($"[MarkerPlacement] Detected {denseGroups.Count} dense groups");

            if (!denseGroups.Any())
            {
                if (logRadialExtensionCalculation)
                    _logger.LogInfo("[MarkerPlacement] No dense groups detected, using normal positioning");

                var normalIndividuals = BuildIndividualPlacements(
                    viewport, containerWidth, containerHeight, visibleIndividuals, locationMarkerSize);
                return new MarkerPlacementResult(
                    MarkerPlacementMode.NormalOnly,
                    normalIndividuals,
                    clusterPlacements,
                    Array.Empty<DenseMarkerGroup>(),
                    shouldApplyExtensions: true);
            }

            var markersInGroups = new HashSet<Location>();
            var allExtensions   = new List<RadialExtension>();
            int groupId = 0;

            foreach (var group in denseGroups)
            {
                if (logRadialExtensionCalculation)
                {
                    _logger.LogInfo(
                        $"  Processing group {groupId} with {group.Count} locations at center " +
                        $"({group.CenterPoint.X:F2}, {group.CenterPoint.Y:F2})");
                }

                var extensions = _extensionCalculator.CalculateRadialExtensions(
                    group,
                    markerScreenPositions,
                    containerWidth,
                    containerHeight);

                if (logRadialExtensionCalculation)
                    _logger.LogInfo($"  Calculated {extensions.Count} extensions");

                foreach (var ext in extensions)
                    ext.GroupId = groupId;

                if (_extensionCalculator.ValidateNoCrossings(extensions))
                {
                    if (logRadialExtensionCalculation)
                        _logger.LogInfo("  Validation passed");

                    group.Extensions = extensions;
                    allExtensions.AddRange(extensions);

                    foreach (var loc in group.Locations)
                        markersInGroups.Add(loc);
                }
                else
                {
                    _logger.LogWarning(
                        $"Line crossings detected for group with {group.Count} markers, using normal positioning");

                    group.Extensions = new List<RadialExtension>();
                    foreach (var loc in group.Locations)
                        markersInGroups.Add(loc);
                }

                groupId++;
            }

            if (allExtensions.Any() && _adjuster != null)
                _adjuster.AdjustExtensions(allExtensions, locationMarkerSize);

            var extensionGroups = denseGroups
                .Where(g => g.Extensions.Any())
                .ToList();

            var outsideGroupIndividuals = visibleIndividuals
                .Where(t => !markersInGroups.Contains(t.Location))
                .ToList();

            var normalForOutside = BuildIndividualPlacements(
                viewport, containerWidth, containerHeight, outsideGroupIndividuals, locationMarkerSize);

            var fallbackNormals = denseGroups
                .Where(g => !g.Extensions.Any() && g.Locations.Any())
                .SelectMany(g => g.Locations)
                .Select(loc =>
                {
                    var entry = visibleIndividuals.First(t => t.Location == loc);
                    var screenPos = viewport.SourceToScreen(
                        entry.PixelX, entry.PixelY, containerWidth, containerHeight);
                    return new MarkerScreenPlacement(
                        loc.Name,
                        screenPos.X - locationMarkerSize / 2,
                        screenPos.Y - locationMarkerSize / 2);
                })
                .ToList();

            var combinedIndividuals = normalForOutside
                .Concat(fallbackNormals)
                .ToList();

            return new MarkerPlacementResult(
                MarkerPlacementMode.WithExtensions,
                combinedIndividuals,
                clusterPlacements,
                extensionGroups,
                shouldApplyExtensions: true);
        }

        private static IReadOnlyList<MarkerScreenPlacement> BuildIndividualPlacements(
            ViewportState viewport,
            double containerWidth,
            double containerHeight,
            IReadOnlyList<(Location Location, double PixelX, double PixelY)> markers,
            double markerSize)
        {
            var result = new List<MarkerScreenPlacement>(markers.Count);
            foreach (var (location, pixelX, pixelY) in markers)
            {
                var screenPos = viewport.SourceToScreen(pixelX, pixelY, containerWidth, containerHeight);
                result.Add(new MarkerScreenPlacement(
                    location.Name,
                    screenPos.X - markerSize / 2,
                    screenPos.Y - markerSize / 2));
            }
            return result;
        }

        private static IReadOnlyList<ClusterScreenPlacement> BuildClusterPlacements(
            ViewportState viewport,
            double containerWidth,
            double containerHeight,
            IReadOnlyList<Point> clusterCenters,
            double markerSize)
        {
            var result = new List<ClusterScreenPlacement>(clusterCenters.Count);
            foreach (var center in clusterCenters)
            {
                var screenPos = viewport.SourceToScreen(
                    center.X, center.Y, containerWidth, containerHeight);
                result.Add(new ClusterScreenPlacement(
                    screenPos.X - markerSize / 2,
                    screenPos.Y - markerSize / 2,
                    markerSize));
            }
            return result;
        }
    }
}
