using System;
using System.Collections.Generic;
using System.Linq;
using InteractiveWorldMap.Models;

namespace InteractiveWorldMap.Services
{
    /// <summary>
    /// Which group and which variant a layout key actually means.
    /// </summary>
    /// <remarks>
    /// Split out because these answer one question and every group-scoped operation depends on
    /// getting the same answer. They used not to: loading fell back to a compatible key while
    /// listing, selecting and deleting matched exactly, so the map could show a layout the rest of
    /// the editor could not find. Keeping the resolution rules in one file is the point.
    /// </remarks>
    public partial class ManualLayoutManager
    {
        /// <summary>
        /// Returns the best variant, honouring an explicit <paramref name="selectedVariantId"/> first,
        /// then falling back to priority (Manual IsDefault > AutoSeed IsDefault > …) + recency.
        /// </summary>
        private static ManualLayout? SelectPreferredVariant(ManualLayoutGroup group, string? selectedVariantId = null)
        {
            if (!string.IsNullOrEmpty(selectedVariantId))
            {
                var selected = group.Variants.FirstOrDefault(v =>
                    string.Equals(v.VariantId, selectedVariantId, StringComparison.OrdinalIgnoreCase));
                if (selected != null) return selected;
                // Stale id — fall through to priority order.
            }

            return group.Variants
                .OrderByDescending(GetVariantPriority)
                .ThenByDescending(v => v.UpdatedUtc)
                .ThenByDescending(v => v.Timestamp)
                .FirstOrDefault();
        }

        private static int GetVariantPriority(ManualLayout variant)
        {
            if (variant.Origin == ManualLayoutOrigin.Manual && variant.IsDefault) return 6;
            if (variant.Origin == ManualLayoutOrigin.AutoSeed && variant.IsDefault) return 5;
            if (variant.Origin == ManualLayoutOrigin.Manual) return 4;
            if (variant.Origin == ManualLayoutOrigin.AutoSeed) return 3;
            if (variant.IsDefault) return 2;
            return 1;
        }

        private static string? GetSelectedVariantIdFromCollection(ManualLayoutCollection collection, string groupKey)
        {
            if (collection.SelectedVariants == null) return null;
            collection.SelectedVariants.TryGetValue(groupKey, out var sel);
            return string.IsNullOrEmpty(sel) ? null : sel;
        }

        private static ManualLayout? FindCompatibleLayout(string key, ManualLayoutCollection collection)
        {
            return collection.Layouts
                .Where(entry => LayoutKeyGenerator.AreKeysCompatible(entry.Key, key))
                .Select(entry => new
                {
                    Layout = entry.Value,
                    ZoomDifference = Math.Abs(ExtractZoomLevel(entry.Key) - ExtractZoomLevel(key))
                })
                .OrderBy(c => c.ZoomDifference)
                .ThenByDescending(c => c.Layout.Timestamp)
                .Select(c => c.Layout)
                .FirstOrDefault();
        }

        /// <summary>
        /// The key of the group that <paramref name="key"/> actually resolves to: itself when a
        /// group exists under it, otherwise the compatible group <see cref="LoadLayout"/> would
        /// fall back to. Null when nothing matches.
        /// </summary>
        /// <remarks>
        /// Everything scoped to a layout group has to agree about this. Loading fell back to a
        /// compatible key while listing, selecting and deleting matched exactly, so outside the
        /// seeded window sizes the map showed a layout the dropdown could not list — and had it
        /// been listed, the delete and select paths would have been operating on a different group
        /// than the one on screen. The disagreement is the bug, not the fallback.
        /// </remarks>
        private static string? ResolveGroupKey(string key, ManualLayoutCollection collection)
        {
            if (collection.LayoutGroups.ContainsKey(key)) return key;

            var compatible = FindCompatibleGroup(key, collection);
            return compatible?.GroupKey;
        }

        private static ManualLayoutGroup? FindCompatibleGroup(string key, ManualLayoutCollection collection)
        {
            return collection.LayoutGroups
                .Where(entry => LayoutKeyGenerator.AreKeysCompatible(entry.Key, key))
                .Select(entry => new
                {
                    Group = entry.Value,
                    ZoomDifference = Math.Abs(ExtractZoomLevel(entry.Key) - ExtractZoomLevel(key)),
                    PreferredVariant = SelectPreferredVariant(
                        entry.Value,
                        collection.SelectedVariants?.GetValueOrDefault(entry.Key))
                })
                .OrderBy(c => c.ZoomDifference)
                .ThenByDescending(c => c.PreferredVariant?.UpdatedUtc ?? DateTime.MinValue)
                .ThenByDescending(c => c.PreferredVariant?.Timestamp ?? DateTime.MinValue)
                .Select(c => c.Group)
                .FirstOrDefault();
        }

        private static double ExtractZoomLevel(string key)
        {
            var parts = key.Split('_');
            if (parts.Length > 1 && parts[1].StartsWith("z") && double.TryParse(parts[1].Substring(1), out var zoom))
                return zoom;
            return 0;
        }
    }
}
