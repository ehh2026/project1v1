namespace InteractiveWorldMap.Models
{
    /// <summary>
    /// A single entry in the composite-pin render-plan disk cache.
    /// Pairs a location identifier with its pre-built render plan so that
    /// <see cref="Services.CompositePinPlanCache"/> can persist and restore
    /// plans without re-running the expensive planning pipeline.
    /// </summary>
    public sealed record CachedCompositePlanEntry(string LocationId, CompositePinRenderPlan Plan);
}
