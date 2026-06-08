using System;
using System.Collections.Generic;
using System.Linq;
using InteractiveWorldMap.Models;

namespace InteractiveWorldMap.Services
{
    /// <summary>
    /// Selects a composite pin pair and builds the exact render plan for the target segment.
    /// Also maintains a per-session last-result cache used by <see cref="ManualLayoutAssignmentEnricher"/>
    /// to capture shaft/head assignments at save time.
    /// </summary>
    public class CompositePinPlanningService
    {
        private readonly PinPartPlacementCalculator _placementCalculator;
        private readonly CompositePinRenderPlanBuilder _renderPlanBuilder;

        // Keyed by LocationId (== Location.Name == ManualLayoutMarker.LocationName).
        private readonly Dictionary<string, CompositePinPlanningResult> _lastResultsByLocation
            = new Dictionary<string, CompositePinPlanningResult>(StringComparer.Ordinal);

        public CompositePinPlanningService(
            PinPartPlacementCalculator placementCalculator,
            CompositePinRenderPlanBuilder renderPlanBuilder)
        {
            _placementCalculator = placementCalculator ?? throw new ArgumentNullException(nameof(placementCalculator));
            _renderPlanBuilder = renderPlanBuilder ?? throw new ArgumentNullException(nameof(renderPlanBuilder));
        }

        /// <summary>Standard plan build — scores all candidates.</summary>
        public CompositePinPlanningResult BuildPlan(
            PinPlacementTarget target,
            IReadOnlyDictionary<string, PinPartGeometryEntry> candidates,
            PinPartConfig config)
            => BuildPlanCore(target, candidates, config, preferredPairId: null, preferredHeadSourcePath: null);

        /// <summary>
        /// Plan build with optional saved-assignment overrides.
        /// When <paramref name="preferredPairId"/> is non-null and present in candidates the
        /// scorer is bypassed for shaft selection.
        /// When <paramref name="preferredHeadSourcePath"/> is non-null and resolves to a geometry
        /// entry the hash-based head selection is bypassed.
        /// Absent or unresolvable overrides fall back to the normal scoring/hash path.
        /// </summary>
        public CompositePinPlanningResult BuildPlan(
            PinPlacementTarget target,
            IReadOnlyDictionary<string, PinPartGeometryEntry> candidates,
            PinPartConfig config,
            string? preferredPairId,
            string? preferredHeadSourcePath)
            => BuildPlanCore(target, candidates, config, preferredPairId, preferredHeadSourcePath);

        /// <summary>
        /// Returns the last plan built for <paramref name="locationId"/> in this session.
        /// Returns false if no plan has been built yet for that location.
        /// </summary>
        public bool TryGetLastResult(string locationId, out CompositePinPlanningResult? result)
            => _lastResultsByLocation.TryGetValue(locationId, out result);

        // ─── Core implementation ─────────────────────────────────────────────

        private CompositePinPlanningResult BuildPlanCore(
            PinPlacementTarget target,
            IReadOnlyDictionary<string, PinPartGeometryEntry> candidates,
            PinPartConfig config,
            string? preferredPairId,
            string? preferredHeadSourcePath)
        {
            var selection    = _placementCalculator.CalculatePlacement(target, candidates, config, preferredPairId);
            var headGeometry = ResolveHeadGeometry(candidates, target.LocationId, config, preferredHeadSourcePath);
            var renderPlan   = _renderPlanBuilder.BuildPlan(target, selection, config, headGeometry);

            var result = new CompositePinPlanningResult
            {
                Selection  = selection,
                RenderPlan = renderPlan
            };

            _lastResultsByLocation[target.LocationId] = result;
            return result;
        }

        private static PinPartGeometryEntry ResolveHeadGeometry(
            IReadOnlyDictionary<string, PinPartGeometryEntry> candidates,
            string locationId,
            PinPartConfig config,
            string? preferredHeadSourcePath)
        {
            // Honour saved path when it still resolves to a known entry.
            // HeadSourcePath in the render plan == Path.Combine(config.PartsFolderPath, entry.HeadFile).
            if (preferredHeadSourcePath != null)
            {
                var match = candidates.Values.FirstOrDefault(e =>
                    string.Equals(
                        System.IO.Path.Combine(config.PartsFolderPath, e.HeadFile),
                        preferredHeadSourcePath,
                        StringComparison.OrdinalIgnoreCase));
                if (match != null)
                    return match;
            }

            return SelectHeadForLocation(candidates, locationId);
        }

        /// <summary>
        /// Deterministically selects a head geometry for a location by hashing the location ID.
        /// This decouples the head image from the shaft selection so neighbouring pins
        /// with the same best-fit shaft get different coloured heads.
        /// </summary>
        private static PinPartGeometryEntry SelectHeadForLocation(
            IReadOnlyDictionary<string, PinPartGeometryEntry> candidates,
            string locationId)
        {
            var keys  = candidates.Keys.OrderBy(k => k, StringComparer.Ordinal).ToList();
            var index = Math.Abs(locationId.GetHashCode()) % keys.Count;
            return candidates[keys[index]];
        }
    }
}
