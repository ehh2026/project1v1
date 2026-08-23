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
        /// <summary>
        /// Collapses cluster groups that differ only by the viewport size that used to be baked into
        /// their key, so a cluster laid out on one monitor stops being a separate group from the same
        /// cluster on another.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Runs on load, so files written before the size was dropped migrate themselves. Without it
        /// the old groups keep working — <see cref="LayoutKeyGenerator.AreKeysCompatible"/> never looked
        /// at the size, so they still resolve — but every save would add another group beside them and
        /// the duplication would never go away.
        /// </para>
        /// <para>
        /// Merging can collide: each sized group has its own <c>manual-default</c>. Hand-made work is
        /// never dropped for a collision — the loser keeps its layout under a suffixed id, so the worst
        /// case is a variant with an awkward name rather than a layout that quietly disappeared.
        /// Generated seeds are the exception: they are reproducible from the coordinate source, and
        /// keeping four of them would leave the picker as cluttered as the groups were.
        /// </para>
        /// </remarks>
        private void MergeSizedClusterGroups(ManualLayoutCollection collection)
        {
            var sized = collection.LayoutGroups.Keys
                .Where(k => !k.StartsWith("fullmap", StringComparison.Ordinal))
                .Where(k => StripSizeComponent(k) != k)
                .ToList();

            if (sized.Count == 0) return;

            foreach (var oldKey in sized)
            {
                var newKey = StripSizeComponent(oldKey);
                var group = collection.LayoutGroups[oldKey];
                collection.LayoutGroups.Remove(oldKey);

                collection.SelectedVariants.TryGetValue(oldKey, out var selected);
                collection.SelectedVariants.Remove(oldKey);

                if (!collection.LayoutGroups.TryGetValue(newKey, out var target))
                {
                    group.GroupKey = newKey;
                    foreach (var variant in group.Variants)
                    {
                        variant.GroupKey = newKey;
                        variant.Key = newKey;
                    }
                    collection.LayoutGroups[newKey] = group;
                    CarrySelectionOver(collection, newKey, selected);
                    continue;
                }

                // Merge first, then carry the selection, because merging can rename the variant the
                // selection points at. Copying the id across beforehand leaves it naming whatever
                // else in the target group already holds that id -- a different layout, silently
                // selected, with nothing to show anything went wrong.
                string? selectedAfterMerge = null;
                foreach (var variant in group.Variants)
                {
                    var wasSelected = selected != null
                        && string.Equals(variant.VariantId, selected, StringComparison.OrdinalIgnoreCase);

                    var finalId = MergeVariantInto(target, variant, newKey, oldKey);

                    if (wasSelected) selectedAfterMerge = finalId;
                }

                CarrySelectionOver(collection, newKey, selectedAfterMerge);
            }

            _logger.LogInfo(
                $"[ManualLayoutManager] Merged {sized.Count} size-keyed cluster group(s) into " +
                $"size-independent keys");
        }

        /// <summary>
        /// Adds <paramref name="variant"/> to <paramref name="target"/> and returns the variant id
        /// it ended up under, which is not always the one it arrived with, or null when it was
        /// discarded. The caller needs that to keep a selection pointing at the layout the user
        /// actually chose.
        /// </summary>
        private string? MergeVariantInto(
            ManualLayoutGroup target, ManualLayout variant, string newKey, string oldKey)
        {
            variant.GroupKey = newKey;
            variant.Key = newKey;

            var clash = target.Variants.FirstOrDefault(v =>
                string.Equals(v.VariantId, variant.VariantId, StringComparison.OrdinalIgnoreCase));

            if (clash == null)
            {
                target.Variants.Add(variant);
                return variant.VariantId;
            }

            if (variant.Origin == ManualLayoutOrigin.AutoSeed)
            {
                // Only a seed may replace a seed. A hand-made or imported variant that happens to
                // share an id -- "manual-default" is the obvious way for that to happen -- is the
                // user's work, and the seed arriving beside it is regenerable. Discarding the seed
                // costs a rerun of the generator; discarding the variant costs whatever was laid
                // out by hand.
                if (clash.Origin != ManualLayoutOrigin.AutoSeed)
                {
                    _logger.LogInfo(
                        $"[ManualLayoutManager] Discarded seed '{variant.VariantId}' from {oldKey}: " +
                        $"a {clash.Origin} variant already holds that id in {newKey}");

                    // The surviving variant carries the id the selection named, so a selection
                    // pointing at the discarded seed still resolves rather than dangling.
                    return clash.VariantId;
                }

                // One seed per cluster is the whole point of dropping the size from the key.
                if (variant.UpdatedUtc > clash.UpdatedUtc)
                {
                    target.Variants.Remove(clash);
                    target.Variants.Add(variant);
                    return variant.VariantId;
                }

                return clash.VariantId;
            }

            // Hand-made: keep it, under a name that says where it came from.
            variant.VariantId = MakeUniqueVariantId(target, variant.VariantId, oldKey);
            variant.DisplayName = $"{variant.DisplayName} (from {DescribeSize(oldKey)})";
            variant.IsDefault = false;
            target.Variants.Add(variant);

            _logger.LogInfo(
                $"[ManualLayoutManager] Kept colliding manual variant as '{variant.VariantId}' " +
                $"while merging {oldKey} into {newKey}");

            return variant.VariantId;
        }

        /// <summary>
        /// Records the surviving selection for a merged group, if there is one and the group does
        /// not already have one.
        /// </summary>
        /// <remarks>
        /// First writer wins. Arbitrary between two equally valid choices, but the alternative lets
        /// whichever group happens to be merged last silently override a selection made under
        /// another window size. A null id means the selected variant did not survive the merge;
        /// leaving the group unselected hands it to origin-priority fallback, which is what an
        /// unselected group has always done.
        /// </remarks>
        private static void CarrySelectionOver(
            ManualLayoutCollection collection, string newKey, string? variantId)
        {
            if (variantId == null) return;
            if (collection.SelectedVariants.ContainsKey(newKey)) return;

            collection.SelectedVariants[newKey] = variantId;
        }

        private static string MakeUniqueVariantId(ManualLayoutGroup target, string variantId, string oldKey)
        {
            var candidate = $"{variantId}-{DescribeSize(oldKey)}";
            var suffix = 2;
            while (target.Variants.Any(v => string.Equals(v.VariantId, candidate, StringComparison.OrdinalIgnoreCase)))
                candidate = $"{variantId}-{DescribeSize(oldKey)}-{suffix++}";

            return candidate;
        }

        /// <summary>The <c>s{W}x{H}</c> component of a key, or "unsized" when it has none.</summary>
        private static string DescribeSize(string key) =>
            key.Split('_').FirstOrDefault(IsSizeComponent)?.Substring(1) ?? "unsized";

        /// <summary>
        /// The key with its <c>s{W}x{H}</c> component removed; unchanged when there is none.
        /// </summary>
        private static string StripSizeComponent(string key) =>
            string.Join("_", key.Split('_').Where(part => !IsSizeComponent(part)));

        /// <summary>
        /// True for the viewport-size part, e.g. <c>s161x101</c>. Deliberately strict: the centre part
        /// is also numeric, and a loose match would strip coordinates out of the key.
        /// </summary>
        private static bool IsSizeComponent(string part) =>
            part.Length > 1
            && part[0] == 's'
            && part.Count(c => c == 'x') == 1
            && part.Substring(1).Split('x') is { Length: 2 } halves
            && halves.All(h => h.Length > 0 && h.All(char.IsDigit));

    }
}
