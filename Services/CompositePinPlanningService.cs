using System;
using System.Collections.Generic;
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
            var selection = _placementCalculator.CalculatePlacement(target, candidates, config);
            var renderPlan = _renderPlanBuilder.BuildPlan(target, selection, config);

            return new CompositePinPlanningResult
            {
                Selection = selection,
                RenderPlan = renderPlan
            };
        }
    }
}
