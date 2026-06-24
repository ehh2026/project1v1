using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Controls.Primitives;
using System.Windows.Shapes;
using InteractiveWorldMap.Models;
using InteractiveWorldMap.Services;
using InteractiveWorldMap.Utilities;
using InteractiveWorldMap.Views;
using IOPath = System.IO.Path;

namespace InteractiveWorldMap
{
    public partial class MainWindow
    {
        /// <summary>
        /// Updates all marker positions based on the current viewport.
        /// Applies radial extensions for dense marker groups when enabled.
        /// </summary>
        private void UpdateMarkerPositions()
        {
            var viewport = MapDisplay.CurrentViewport;
            if (viewport == null)
                return;

            var containerWidth = MapDisplay.ActualWidth;
            var containerHeight = MapDisplay.ActualHeight;

            if (!IsAnimating)
                _extensionLineRenderer.Clear();

            PrepareMarkerVisualsForPlacementUpdate();

            // 2.2: marker visibility and source coordinates do not change during a zoom animation,
            // so reuse the cached projections across frames instead of rebuilding them each frame.
            // The non-animating branch always rebuilds and clears the cache, so stale data can never
            // leak into normal placement.
            List<(Location Location, double PixelX, double PixelY)> visibleIndividuals;
            List<Point> visibleClusterCenters;
            if (IsAnimating && _animVisibleIndividuals != null && _animVisibleClusterCenters != null)
            {
                visibleIndividuals = _animVisibleIndividuals;
                visibleClusterCenters = _animVisibleClusterCenters;
            }
            else
            {
                visibleIndividuals = _individualMarkers
                    .Where(m => m.Visibility == Visibility.Visible)
                    .Select(m => (m.Location, m.Location.PixelX, m.Location.PixelY))
                    .ToList();

                visibleClusterCenters = _clusterMarkers
                    .Where(m => m.Visibility == Visibility.Visible && m.Cluster != null)
                    .Select(m => m.Cluster!.CenterPoint)
                    .ToList();

                _animVisibleIndividuals = IsAnimating ? visibleIndividuals : null;
                _animVisibleClusterCenters = IsAnimating ? visibleClusterCenters : null;
            }

            var plan = _placementOrchestrator.Compute(
                viewport,
                containerWidth,
                containerHeight,
                IsAnimating,
                visibleIndividuals,
                visibleClusterCenters);

            _denseGroups = plan.ExtensionGroups.ToList();

            if (plan.Mode == MarkerPlacementMode.WithExtensions)
            {
                foreach (var group in plan.ExtensionGroups)
                {
                    _extensionLineRenderer.Apply(
                        group,
                        viewport,
                        containerWidth,
                        containerHeight,
                        _individualMarkers,
                        (m, orig, ext) => TryApplyCompositePinMarker(m, orig, ext));
                }
            }

            // 2.3: build a name->marker index once per pass so the per-placement loops below are
            // O(1) lookups instead of O(n) FirstOrDefault scans (O(n^2) per frame).
            var markerByName = BuildIndividualMarkerIndex();

            ApplyIndividualPlacements(plan.IndividualPlacements, markerByName);
            ApplyClusterPlacements(plan.ClusterPlacements);
            ApplyCompositePinsToNormalPlacements(plan.IndividualPlacements, viewport, containerWidth, containerHeight, markerByName);
            ApplyCompositePinDepthSort();

            UpdatePinTipCaps();
        }

        /// <summary>
        /// Builds a name-&gt;marker lookup for the individual markers. First marker per name wins,
        /// preserving the previous <c>FirstOrDefault</c>-by-name behavior (names are unique in practice).
        /// </summary>
        private Dictionary<string, LocationMarker> BuildIndividualMarkerIndex()
        {
            var index = new Dictionary<string, LocationMarker>(_individualMarkers.Count, StringComparer.Ordinal);
            foreach (var marker in _individualMarkers)
            {
                if (!index.ContainsKey(marker.Location.Name))
                    index[marker.Location.Name] = marker;
            }
            return index;
        }

        private void ApplyIndividualPlacements(
            IReadOnlyList<MarkerScreenPlacement> placements,
            IReadOnlyDictionary<string, LocationMarker> markerByName)
        {
            foreach (var placement in placements)
            {
                if (!markerByName.TryGetValue(placement.LocationName, out var marker))
                    continue;

                if (IsAnimating && _animationOffsets.TryGetValue(marker, out var offset))
                {
                    var mapPoint = GetMarkerMapPoint(placement);
                    Point anchor;
                    if (marker.Content is PinMarker pin)
                        anchor = pin.GetShaftTipPoint();
                    else if (marker.Content is CompositePinMarker composite)
                        anchor = composite.GetTipAnchorPoint();
                    else
                        anchor = new Point(0, 0);

                    Canvas.SetLeft(marker, mapPoint.X - anchor.X + offset.X);
                    Canvas.SetTop(marker, mapPoint.Y - anchor.Y + offset.Y);
                    continue;
                }

                // Non-extended drawn pins keep their own shaft; the extended path
                // (ExtensionLineRenderer) hides it, so restore it here.
                if (TryPlaceDrawnPinAtMapPoint(marker, placement))
                    continue;

                if (TryGetCompositeAnchoredPlacement(marker, placement, out var compositeTopLeft))
                {
                    Canvas.SetLeft(marker, compositeTopLeft.X);
                    Canvas.SetTop(marker, compositeTopLeft.Y);
                    continue;
                }

                Canvas.SetLeft(marker, placement.Left);
                Canvas.SetTop(marker, placement.Top);
            }
        }

        private void ApplyClusterPlacements(IReadOnlyList<ClusterScreenPlacement> placements)
        {
            var visibleClusters = _clusterMarkers
                .Where(m => m.Visibility == Visibility.Visible)
                .ToList();

            for (int i = 0; i < placements.Count && i < visibleClusters.Count; i++)
            {
                var placement = placements[i];
                var marker = visibleClusters[i];
                Canvas.SetLeft(marker, placement.Left);
                Canvas.SetTop(marker, placement.Top);
            }
        }

        /// <summary>
        /// Clears all markers from the canvas.
        /// </summary>
        private void ClearAllMarkers()
        {
            _logger.LogInfo($"[ClearAllMarkers] Clearing {_individualMarkers.Count} individual and {_clusterMarkers.Count} cluster markers");

            foreach (var marker in _individualMarkers)
            {
                MapDisplay.Markers.Children.Remove(marker);
            }
            _individualMarkers.Clear();
            _baseMarkerVisuals.Clear();

            foreach (var marker in _clusterMarkers)
            {
                MapDisplay.Markers.Children.Remove(marker);
            }
            _clusterMarkers.Clear();

            _pinTipCapRenderer?.Clear();
        }

        /// <summary>
        /// Shows only cluster markers (hides individual markers).
        /// </summary>
        private void ShowOnlyClusterMarkers()
        {
            _logger.LogInfo("[ShowOnlyClusterMarkers]");

            // Show all individual markers that are single-location clusters
            foreach (var marker in _individualMarkers)
            {
                // Check if this individual marker is from a single-location cluster
                var isSingleCluster = _clusters.Any(c => c.IsSingleLocation && c.Locations[0] == marker.Location);
                marker.Visibility = isSingleCluster ? Visibility.Visible : Visibility.Collapsed;
            }

            // Show all cluster markers
            foreach (var marker in _clusterMarkers)
            {
                marker.Visibility = Visibility.Visible;
            }

            // Update positions for visible markers
            UpdateMarkerPositions();
        }

        /// <summary>
        /// Shows only individual markers for a specific cluster (hides cluster markers).
        /// </summary>
        private void ShowOnlyIndividualMarkers(LocationCluster cluster)
        {
            _logger.LogInfo($"[ShowOnlyIndividualMarkers] Showing markers for cluster with {cluster.Count} locations");

            // Hide all cluster markers
            foreach (var marker in _clusterMarkers)
            {
                marker.Visibility = Visibility.Collapsed;
            }

            // For each location in the cluster, ensure we have an individual marker
            foreach (var location in cluster.Locations)
            {
                // Check if marker already exists
                var existingMarker = _individualMarkers.FirstOrDefault(m => m.Location == location);

                if (existingMarker != null)
                {
                    // Show existing marker
                    existingMarker.Visibility = Visibility.Visible;
                    _logger.LogInfo($"  Showing existing marker: {location.Name}");
                }
                else
                {
                    // Create new individual marker for this location
                    _logger.LogInfo($"  Creating new marker for: {location.Name}");
                    var newMarker = AddIndividualMarker(location);
                    newMarker.Visibility = Visibility.Visible;
                }
            }

            // Hide all other individual markers not in this cluster
            foreach (var marker in _individualMarkers)
            {
                if (!cluster.Locations.Contains(marker.Location))
                {
                    marker.Visibility = Visibility.Collapsed;
                }
            }

            // Placement runs from ShowZoomedView after visibility is set.
        }
    }
}
