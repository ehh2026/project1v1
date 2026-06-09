using System;

namespace InteractiveWorldMap.Models
{
    public sealed record ManualLayoutSummary(
        string GroupKey,
        string VariantId,
        string DisplayName,
        ManualLayoutOrigin Origin,
        DateTime UpdatedUtc,
        bool IsDefault,
        bool IsSelected,
        int MarkerCount);
}
