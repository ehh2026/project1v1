using System.Collections.Generic;
using InteractiveWorldMap.Models;

namespace InteractiveWorldMap.Services;

public interface IManualLayoutManager
{
    // ─── Compatibility wrappers (flat key API) ────────────────────────────────
    bool SaveLayout(string key, List<RadialExtension> extensions,
        IReadOnlyDictionary<string, (string PairId, string HeadSourcePath)>? assignments = null);
    ManualLayout? LoadLayout(string key);
    bool DeleteLayout(string key);
    bool LayoutExists(string key);
    List<string> GetAllLayoutKeys();
    bool ApplyLayout(ManualLayout layout, List<RadialExtension> extensions);

    // ─── Variant CRUD ─────────────────────────────────────────────────────────
    IReadOnlyList<ManualLayoutSummary> ListVariants(string groupKey);
    ManualLayout? LoadVariant(string groupKey, string variantId);
    bool SaveVariant(
        string groupKey,
        string variantId,
        string displayName,
        ManualLayoutOrigin origin,
        List<RadialExtension> extensions,
        IReadOnlyDictionary<string, (string PairId, string HeadSourcePath)>? assignments,
        bool setAsDefault,
        bool setAsSelected,
        string? basedOnVariantId = null);
    bool DeleteVariant(string groupKey, string variantId);
    bool SetDefaultVariant(string groupKey, string variantId);

    // ─── Selected-variant persistence ────────────────────────────────────────
    string? GetSelectedVariantId(string groupKey);
    bool SetSelectedVariantId(string groupKey, string variantId);
}
