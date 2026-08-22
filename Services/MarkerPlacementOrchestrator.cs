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

            var clusterPlacements = BuildClusterPlacements(
                viewport, containerWidth, containerHeight, visibleClusterCenters, _visualConfig.ClusterMarkerSize);

            if (isAnimating)
            {
                return BuildFallbackResult(
                    MarkerPlacementMode.AnimatingFallback,
                    viewport,
                    containerWidth,
                    containerHeight,
                    visibleIndividuals,
                    clusterPlacements,
                    shouldApplyExtensions: false);
            }

            bool shouldApplyExtensions = ShouldApplyExtensions(viewport);
            var logRadialExtensionCalculation = ShouldLogRadialExtensionCalculation();
            LogExtensionDecision(viewport, shouldApplyExtensions, logRadialExtensionCalculation);

            if (!shouldApplyExtensions)
            {
                return BuildFallbackResult(
                    MarkerPlacementMode.NormalOnly,
                    viewport,
                    containerWidth,
                    containerHeight,
                    visibleIndividuals,
                    clusterPlacements,
                    shouldApplyExtensions: false);
            }

            var extensionPlan = BuildExtensionPlan(
                viewport,
                containerWidth,
                containerHeight,
                visibleIndividuals,
                locationMarkerSize,
                logRadialExtensionCalculation);

            if (!extensionPlan.DenseGroups.Any())
            {
                return BuildFallbackResult(
                    MarkerPlacementMode.NormalOnly,
                    viewport,
                    containerWidth,
                    containerHeight,
                    visibleIndividuals,
                    clusterPlacements,
                    shouldApplyExtensions: true);
            }

            var combinedIndividuals = BuildNonExtensionPlacements(
                viewport,
                containerWidth,
                containerHeight,
                visibleIndividuals,
                extensionPlan.MarkersInGroups,
                extensionPlan.DenseGroups,
                locationMarkerSize);

            return new MarkerPlacementResult(
                MarkerPlacementMode.WithExtensions,
                combinedIndividuals,
                clusterPlacements,
                extensionPlan.ExtensionGroups,
                shouldApplyExtensions: true);
        }

        private bool ShouldApplyExtensions(ViewportState viewport) =>
            _visualConfig.RadialExtension.Enabled &&
            _extensionCalculator != null &&
            viewport.ZoomLevel >= _visualConfig.RadialExtension.ZoomThresholdForExtensions;

        private bool ShouldLogRadialExtensionCalculation() =>
            _visualConfig.EnableDeveloperTools &&
            _visualConfig.Debug.LogRadialExtensionCalculation;

        private void LogExtensionDecision(
            ViewportState viewport,
            bool shouldApplyExtensions,
            bool logRadialExtensionCalculation)
        {
            if (!logRadialExtensionCalculation)
                return;

            _logger.LogInfo(
                $"[MarkerPlacement] ZoomLevel={viewport.ZoomLevel:F2}, " +
                $"Threshold={_visualConfig.RadialExtension.ZoomThresholdForExtensions}, " +
                $"ShouldApply={shouldApplyExtensions}");
        }

        private MarkerPlacementResult BuildFallbackResult(
            MarkerPlacementMode mode,
            ViewportState viewport,
            double containerWidth,
            double containerHeight,
            IReadOnlyList<(Location Location, double PixelX, double PixelY)> visibleIndividuals,
            IReadOnlyList<ClusterScreenPlacement> clusterPlacements,
            bool shouldApplyExtensions)
        {
            var individuals = BuildIndividualPlacements(
                viewport,
                containerWidth,
                containerHeight,
                visibleIndividuals,
                _visualConfig.LocationMarkerSize);

            return new MarkerPlacementResult(
                mode,
                individuals,
                clusterPlacements,
                Array.Empty<DenseMarkerGroup>(),
                shouldApplyExtensions);
        }

        private ExtensionPlacementPlan BuildExtensionPlan(
            ViewportState viewport,
            double containerWidth,
            double containerHeight,
            IReadOnlyList<(Location Location, double PixelX, double PixelY)> visibleIndividuals,
            double locationMarkerSize,
            bool logRadialExtensionCalculation)
        {
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
                return new ExtensionPlacementPlan(denseGroups);
            }

            var markersInGroups = new HashSet<Location>();
            var allExtensions = new List<RadialExtension>();
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
            {
                _adjuster.AdjustExtensions(allExtensions, locationMarkerSize);

                // The adjuster separates overlapping heads by changing line lengths, and it can
                // lengthen as well as shorten. It knows nothing about the canvas, so the bounds
                // the calculator just applied are not binding on its output -- including its own
                // MinimumLineLength floor, which near an edge is a floor on how far off-canvas a
                // head sits. Re-clamp here, where the canvas size is known.
                //
                // Separation loses to bounds when they disagree: two heads slightly too close
                // together are both still visible, and one pushed off the edge is not.
                ClampExtensionsToCanvas(allExtensions, containerWidth, containerHeight);
            }

            var extensionGroups = denseGroups
                .Where(g => g.Extensions.Any())
                .ToList();

            return new ExtensionPlacementPlan(denseGroups, extensionGroups, markersInGroups);
        }

        /// <summary>
        /// Shortens any extension whose head sits outside the canvas until it is back inside,
        /// leaving the angle and the ones already in bounds alone. No minimum length: a minimum
        /// applied here is a minimum distance past the edge, and a head drawn outside the canvas
        /// is not drawn at all.
        /// </summary>
        private static void ClampExtensionsToCanvas(
            List<RadialExtension> extensions, double canvasWidth, double canvasHeight)
        {
            foreach (var ext in extensions)
            {
                var head = ext.ExtendedPosition;
                if (head.X >= 0 && head.X <= canvasWidth && head.Y >= 0 && head.Y <= canvasHeight)
                    continue;

                var toEdge = CoordinateMapper.DistanceToCanvasEdge(
                    ext.OriginalPosition, ext.Angle, canvasWidth, canvasHeight);

                ext.ExtendedPosition = CoordinateMapper.OffsetAtAngle(
                    ext.OriginalPosition,
                    toEdge * RadialExtensionCalculator.CanvasEdgeMargin,
                    ext.Angle);
            }
        }

        private IReadOnlyList<MarkerScreenPlacement> BuildNonExtensionPlacements(
            ViewportState viewport,
            double containerWidth,
            double containerHeight,
            IReadOnlyList<(Location Location, double PixelX, double PixelY)> visibleIndividuals,
            IReadOnlySet<Location> markersInGroups,
            IReadOnlyList<DenseMarkerGroup> denseGroups,
            double locationMarkerSize)
        {
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
            return combinedIndividuals;
        }

        private sealed class ExtensionPlacementPlan
        {
            public ExtensionPlacementPlan(
                IReadOnlyList<DenseMarkerGroup> denseGroups,
                IReadOnlyList<DenseMarkerGroup>? extensionGroups = null,
                IReadOnlySet<Location>? markersInGroups = null)
            {
                DenseGroups = denseGroups;
                ExtensionGroups = extensionGroups ?? Array.Empty<DenseMarkerGroup>();
                MarkersInGroups = markersInGroups ?? new HashSet<Location>();
            }

            public IReadOnlyList<DenseMarkerGroup> DenseGroups { get; }
            public IReadOnlyList<DenseMarkerGroup> ExtensionGroups { get; }
            public IReadOnlySet<Location> MarkersInGroups { get; }
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
