using System;

namespace InteractiveWorldMap.Models
{
    public class CompositePinPlanningResult
    {
        public PinPartPlacementResult Selection { get; set; } = new PinPartPlacementResult();
        public CompositePinRenderPlan RenderPlan { get; set; } = new CompositePinRenderPlan();
    }
}
