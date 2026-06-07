using System;
using System.Collections.Generic;
using System.Linq;
using InteractiveWorldMap.Models;

namespace InteractiveWorldMap.Services
{
    /// <summary>
    /// Selects a composite pin pair and builds the exact render plan for the target segment.
    /// </summary>
    public class CompositePinPlanningService
    {
        private readonly PinPartPlacementCalculator _placementCalculator;
        private readonly CompositePinRenderPlanBuilder _renderPlanBuilder;

        public CompositePinPlanningService(
            PinPartPlacementCalculator placementCalculator,
            CompositePinRenderPlanBuilder renderPlanBuilder)
        {
            _placementCalculator = placementCalculator ?? throw new ArgumentNullException(nameof(placementCalculator));
            _renderPlanBuilder = renderPlanBuilder ?? throw new ArgumentNullException(nameof(renderPlanBuilder));
        }

        public CompositePinPlanningResult BuildPlan(
            PinPlacementTarget target,
            IReadOnlyDictionary<string, PinPartGeometryEntry> candidates,
            PinPartConfig config)
        {
            var selection    = _placementCalculator.CalculatePlacement(target, candidates, config);
            var headGeometry = SelectHeadForLocation(candidates, target.LocationId);
            var renderPlan   = _renderPlanBuilder.BuildPlan(target, selection, config, headGeometry);

            return new CompositePinPlanningResult
            {
                Selection  = selection,
                RenderPlan = renderPlan
            };
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
