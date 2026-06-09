using System.Collections.Generic;
using System.Windows;

namespace InteractiveWorldMap.Models
{
    /// <summary>Per-marker apply decision for manual layout replay (no WPF types beyond Point).</summary>
    public sealed record ManualLayoutApplyInstruction(
        string LocationName,
        Point OriginalScreen,
        Point ExtendedScreen,
        bool RequiresExtensionLine,
        string? PairId,
        string? HeadSourcePath,
        CompositePinRenderPlan? CachedPlan);

    /// <summary>
    /// Full manual-layout apply plan from <see cref="Services.CompositePinApplicationService.BuildApplyInstructions"/>.
    /// </summary>
    public sealed class ManualLayoutApplyResult
    {
        public IReadOnlyList<ManualLayoutApplyInstruction> Instructions { get; init; }
            = System.Array.Empty<ManualLayoutApplyInstruction>();

        public string CacheKey { get; init; } = string.Empty;

        /// <summary>True when cache was missed and built plans should be persisted after apply.</summary>
        public bool ShouldSaveToCache { get; init; }
    }
}
