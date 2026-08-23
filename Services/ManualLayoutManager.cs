using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using InteractiveWorldMap.Models;

namespace InteractiveWorldMap.Services
{
    /// <summary>
    /// Manages saving, loading, and deleting manual layouts, including multi-variant CRUD.
    /// </summary>
    public class ManualLayoutManager : IManualLayoutManager
    {
        private const int ManualVariantCap = 10;

        private static readonly JsonSerializerOptions LayoutJsonOptions = CreateJsonOptions();
        private readonly string _layoutFilePath;
        private readonly ILogger _logger;
        private ManualLayoutCollection? _cachedLayouts;

        public ManualLayoutManager(string layoutFilePath, ILogger logger)
        {
            _layoutFilePath = layoutFilePath;
            _logger = logger;
        }

        // ─── Compatibility wrappers ───────────────────────────────────────────

        /// <summary>Saves current positions as the "manual-default" Manual variant.</summary>
        public bool SaveLayout(string key, List<RadialExtension> extensions,
            IReadOnlyDictionary<string, (string PairId, string HeadSourcePath)>? assignments = null)
        {
            var collection = LoadLayoutCollection();
            string? basedOn = null;
            if (collection.LayoutGroups.TryGetValue(key, out var grp))
                basedOn = grp.Variants.FirstOrDefault(v => v.Origin == ManualLayoutOrigin.AutoSeed && v.IsDefault)?.VariantId;
            return SaveVariant(key, "manual-default", "Manual Layout", ManualLayoutOrigin.Manual,
                extensions, assignments, setAsDefault: true, setAsSelected: true, basedOn);
        }

        /// <summary>
        /// Loads the best variant for the given key, honoring <c>SelectedVariants</c>.
        /// Falls back to compatible-key matching and the flat legacy dictionary.
        /// </summary>
        public ManualLayout? LoadLayout(string key)
        {
            try
            {
                var collection = LoadLayoutCollection();

                if (collection.LayoutGroups.TryGetValue(key, out var exactGroup))
                {
                    var selectedId = GetSelectedVariantIdFromCollection(collection, key);
                    if (selectedId != null && !exactGroup.Variants.Any(v =>
                            string.Equals(v.VariantId, selectedId, StringComparison.OrdinalIgnoreCase)))
                    {
                        _logger.LogWarning($"[ManualLayoutManager] Stale SelectedVariant '{selectedId}' for group '{key}'; clearing");
                        collection.SelectedVariants.Remove(key);
                        selectedId = null;
                    }

                    var layout = SelectPreferredVariant(exactGroup, selectedId);
                    if (layout != null)
                    {
                        _logger.LogInfo($"[ManualLayoutManager] Loaded layout: {key} ({layout.Markers.Count} markers) variant={layout.VariantId} origin={layout.Origin}");
                        return layout;
                    }
                }

                if (collection.Layouts.TryGetValue(key, out var legacyLayout))
                {
                    _logger.LogInfo($"[ManualLayoutManager] Loaded legacy flat layout: {key} ({legacyLayout.Markers.Count} markers)");
                    return legacyLayout;
                }

                var compatibleGroup = FindCompatibleGroup(key, collection);
                if (compatibleGroup != null)
                {
                    var selectedId = GetSelectedVariantIdFromCollection(collection, compatibleGroup.GroupKey);
                    var compatibleLayout = SelectPreferredVariant(compatibleGroup, selectedId);
                    if (compatibleLayout != null)
                    {
                        _logger.LogInfo($"[ManualLayoutManager] Loaded compatible layout for key {key}: {compatibleLayout.GroupKey} variant={compatibleLayout.VariantId} origin={compatibleLayout.Origin} ({compatibleLayout.Markers.Count} markers)");
                        return compatibleLayout;
                    }
                }

                var compatibleLayoutLegacy = FindCompatibleLayout(key, collection);
                if (compatibleLayoutLegacy != null)
                {
                    _logger.LogInfo($"[ManualLayoutManager] Loaded compatible legacy layout for key {key}: {compatibleLayoutLegacy.Key} ({compatibleLayoutLegacy.Markers.Count} markers)");
                    return compatibleLayoutLegacy;
                }

                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError($"[ManualLayoutManager] Failed to load layout: {ex.Message}");
                return null;
            }
        }

        /// <summary>Removes all Manual variants for the group (legacy "delete and recalculate" behavior).</summary>
        public bool DeleteLayout(string key)
        {
            try
            {
                var collection = LoadLayoutCollection();

                // Same resolution as the listing: the confirmation named the variants of whichever
                // group is in play, so this has to remove that group's variants and not a different
                // group that merely shares the session's key.
                var resolved = ResolveGroupKey(key, collection);
                if (resolved != null) key = resolved;

                if (collection.LayoutGroups.TryGetValue(key, out var group))
                {
                    var removedCount = group.Variants.RemoveAll(v => v.Origin == ManualLayoutOrigin.Manual);
                    if (removedCount > 0)
                    {
                        if (group.Variants.Count == 0)
                            collection.LayoutGroups.Remove(key);

                        collection.SelectedVariants.Remove(key);
                        UpdateLegacyLayoutIndex(collection);
                        SaveLayoutCollection(collection);
                        _logger.LogInfo($"[ManualLayoutManager] Deleted {removedCount} manual layout variant(s) for group: {key}");
                        _cachedLayouts = collection;
                        return true;
                    }
                }

                if (collection.Layouts.Remove(key))
                {
                    UpdateLegacyLayoutIndex(collection);
                    SaveLayoutCollection(collection);
                    _logger.LogInfo($"[ManualLayoutManager] Deleted layout: {key}");
                    _cachedLayouts = collection;
                    return true;
                }

                _logger.LogInfo($"[ManualLayoutManager] Layout not found: {key}");
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError($"[ManualLayoutManager] Failed to delete layout: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// True when the group for <paramref name="key"/> holds any Manual variant, whether or not
        /// it is the selected one.
        /// </summary>
        /// <remarks>
        /// Distinct from loading and inspecting <c>Origin</c>: <see cref="LoadLayout"/> returns the
        /// *selected* variant, and a selected AutoSeed would mask a Manual variant sitting beside it
        /// in the same group. Navigation precedence asks "has the user arranged this view at all?",
        /// which is a question about the group, not about the current selection.
        /// </remarks>
        public bool HasManualVariant(string key)
        {
            try
            {
                var collection = LoadLayoutCollection();

                // Resolve the same group LoadLayout would use — exact first, then compatible — and
                // ask whether *that* group holds a Manual variant.
                //
                // Both halves matter, and they answer opposite review findings. Checking the whole
                // group rather than just the selected variant means a selected AutoSeed cannot hide
                // a Manual one beside it. Refusing to look past the group the loader will choose
                // means we never claim a Manual layout that will not actually be displayed: if an
                // exact AutoSeed-only group exists, the loader returns that seed, and reporting
                // "manual exists" from some other compatible group would suppress the full-map
                // fallback while showing neither.
                //
                // The residual gap is size fragmentation — a Manual layout under a different window
                // size is unreachable here because it is unreachable to the loader too. That is
                // issue 6.8/6.9, and the fix belongs in key/lookup consistency, not in making this
                // probe see further than the loader.
                var group = collection.LayoutGroups.TryGetValue(key, out var exact)
                    ? exact
                    : FindCompatibleGroup(key, collection);

                return group != null && GroupHasManual(group);
            }
            catch (Exception ex)
            {
                _logger.LogError($"[ManualLayoutManager] HasManualVariant failed: {ex.Message}");
                return false;
            }
        }

        private static bool GroupHasManual(ManualLayoutGroup group) =>
            group.Variants != null &&
            group.Variants.Any(v => v.Origin == ManualLayoutOrigin.Manual);

        public bool LayoutExists(string key)
        {
            try
            {
                var collection = LoadLayoutCollection();
                return collection.LayoutGroups.ContainsKey(key) ||
                       collection.Layouts.ContainsKey(key) ||
                       FindCompatibleGroup(key, collection) != null ||
                       FindCompatibleLayout(key, collection) != null;
            }
            catch
            {
                return false;
            }
        }

        public List<string> GetAllLayoutKeys()
        {
            try
            {
                var collection = LoadLayoutCollection();
                return collection.LayoutGroups.Keys
                    .Concat(collection.Layouts.Keys)
                    .Distinct(StringComparer.Ordinal)
                    .ToList();
            }
            catch
            {
                return new List<string>();
            }
        }

        public bool ApplyLayout(ManualLayout layout, List<RadialExtension> extensions)
        {
            try
            {
                var markerDict = layout.Markers.ToDictionary(m => m.LocationName);
                int appliedCount = 0;
                foreach (var extension in extensions)
                {
                    if (markerDict.TryGetValue(extension.Location.Name, out var marker))
                    {
                        extension.ExtendedPosition = marker.ExtendedPosition;
                        extension.Angle = marker.Angle;
                        appliedCount++;
                    }
                }
                _logger.LogInfo($"[ManualLayoutManager] Applied layout to {appliedCount}/{extensions.Count} extensions");
                return appliedCount == extensions.Count;
            }
            catch (Exception ex)
            {
                _logger.LogError($"[ManualLayoutManager] Failed to apply layout: {ex.Message}");
                return false;
            }
        }

        // ─── Variant CRUD ─────────────────────────────────────────────────────

        public IReadOnlyList<ManualLayoutSummary> ListVariants(string groupKey)
        {
            try
            {
                var collection = LoadLayoutCollection();

                var resolvedKey = ResolveGroupKey(groupKey, collection);
                if (resolvedKey == null || !collection.LayoutGroups.TryGetValue(resolvedKey, out var group))
                    return Array.Empty<ManualLayoutSummary>();

                if (!string.Equals(resolvedKey, groupKey, StringComparison.Ordinal))
                {
                    _logger.LogInfo(
                        $"[ManualLayoutManager] ListVariants for {groupKey} resolved to compatible group {resolvedKey}");
                }

                var selectedId = GetSelectedVariantIdFromCollection(collection, resolvedKey);
                return group.Variants
                    .Select(v => new ManualLayoutSummary(
                        v.GroupKey,
                        v.VariantId,
                        v.DisplayName,
                        v.Origin,
                        v.UpdatedUtc,
                        v.IsDefault,
                        string.Equals(v.VariantId, selectedId, StringComparison.OrdinalIgnoreCase),
                        v.Markers.Count))
                    .ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError($"[ManualLayoutManager] ListVariants failed: {ex.Message}");
                return Array.Empty<ManualLayoutSummary>();
            }
        }

        public ManualLayout? LoadVariant(string groupKey, string variantId)
        {
            try
            {
                var collection = LoadLayoutCollection();

                var resolvedKey = ResolveGroupKey(groupKey, collection);
                if (resolvedKey == null || !collection.LayoutGroups.TryGetValue(resolvedKey, out var group))
                    return null;

                return group.Variants.FirstOrDefault(v =>
                    string.Equals(v.VariantId, variantId, StringComparison.OrdinalIgnoreCase));
            }
            catch (Exception ex)
            {
                _logger.LogError($"[ManualLayoutManager] LoadVariant failed: {ex.Message}");
                return null;
            }
        }

        public bool SaveVariant(
            string groupKey,
            string variantId,
            string displayName,
            ManualLayoutOrigin origin,
            List<RadialExtension> extensions,
            IReadOnlyDictionary<string, (string PairId, string HeadSourcePath)>? assignments,
            bool setAsDefault,
            bool setAsSelected,
            string? basedOnVariantId = null)
        {
            try
            {
                var markers = extensions.Select(ManualLayoutMarker.FromRadialExtension).ToList();
                if (assignments != null)
                {
                    foreach (var marker in markers)
                    {
                        if (assignments.TryGetValue(marker.LocationName, out var a))
                        {
                            marker.PairId = a.PairId;
                            marker.HeadSourcePath = a.HeadSourcePath;
                        }
                    }
                }

                var collection = LoadLayoutCollection();

                if (!collection.LayoutGroups.TryGetValue(groupKey, out var group))
                {
                    group = new ManualLayoutGroup { GroupKey = groupKey };
                    collection.LayoutGroups[groupKey] = group;
                }

                // Guard: AutoSeed must not overwrite a Manual variant with the same id.
                if (origin == ManualLayoutOrigin.AutoSeed)
                {
                    var existingManual = group.Variants.FirstOrDefault(v =>
                        string.Equals(v.VariantId, variantId, StringComparison.OrdinalIgnoreCase) &&
                        v.Origin == ManualLayoutOrigin.Manual);
                    if (existingManual != null)
                    {
                        _logger.LogWarning($"[ManualLayoutManager] SaveVariant: refused AutoSeed overwrite of Manual variant '{variantId}' in group '{groupKey}'");
                        return false;
                    }
                }

                // Cap: enforce max Manual/Imported variants (AutoSeed variants are uncapped).
                if (origin != ManualLayoutOrigin.AutoSeed)
                {
                    bool alreadyExists = group.Variants.Any(v =>
                        string.Equals(v.VariantId, variantId, StringComparison.OrdinalIgnoreCase));
                    if (!alreadyExists)
                    {
                        int nonSeedCount = group.Variants.Count(v => v.Origin != ManualLayoutOrigin.AutoSeed);
                        if (nonSeedCount >= ManualVariantCap)
                        {
                            _logger.LogWarning($"[ManualLayoutManager] SaveVariant: cap of {ManualVariantCap} Manual/Imported variants reached for group '{groupKey}'");
                            return false;
                        }
                    }
                }

                var existing = group.Variants.FirstOrDefault(v =>
                    string.Equals(v.VariantId, variantId, StringComparison.OrdinalIgnoreCase));

                if (existing != null)
                {
                    existing.Markers = markers;
                    existing.LocationCount = markers.Count;
                    existing.UpdatedUtc = DateTime.UtcNow;
                    existing.Timestamp = existing.UpdatedUtc;
                    existing.DisplayName = displayName;
                    existing.GroupKey = groupKey;
                    existing.Key = groupKey;
                    if (setAsDefault) SetDefaultForOriginClass(group, variantId, origin);
                }
                else
                {
                    var layout = new ManualLayout(groupKey, markers)
                    {
                        GroupKey = groupKey,
                        VariantId = variantId,
                        DisplayName = displayName,
                        Origin = origin,
                        IsDefault = setAsDefault,
                        BasedOnVariantId = basedOnVariantId
                    };
                    group.Variants.Add(layout);
                    if (setAsDefault) SetDefaultForOriginClass(group, variantId, origin);
                }

                if (setAsSelected)
                    collection.SelectedVariants[groupKey] = variantId;

                UpdateLegacyLayoutIndex(collection);
                SaveLayoutCollection(collection);
                _logger.LogInfo($"[ManualLayoutManager] SaveVariant: {groupKey}/{variantId} ({markers.Count} markers)");
                _cachedLayouts = collection;
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError($"[ManualLayoutManager] SaveVariant failed: {ex.Message}");
                return false;
            }
        }

        public bool DeleteVariant(string groupKey, string variantId)
        {
            try
            {
                var collection = LoadLayoutCollection();

                // Resolve first: the variant the user is looking at may live under a compatible key,
                // and deleting "this one" has to remove the one that was listed and shown.
                var resolvedKey = ResolveGroupKey(groupKey, collection);
                if (resolvedKey == null || !collection.LayoutGroups.TryGetValue(resolvedKey, out var group))
                    return false;
                groupKey = resolvedKey;

                var target = group.Variants.FirstOrDefault(v =>
                    string.Equals(v.VariantId, variantId, StringComparison.OrdinalIgnoreCase));
                if (target == null) return false;

                // Service rejects AutoSeed deletion without an explicit future flag.
                if (target.Origin == ManualLayoutOrigin.AutoSeed)
                {
                    _logger.LogWarning($"[ManualLayoutManager] DeleteVariant: cannot delete AutoSeed variant '{variantId}'");
                    return false;
                }

                // Must not remove the last remaining variant in a group.
                if (group.Variants.Count <= 1)
                {
                    _logger.LogWarning($"[ManualLayoutManager] DeleteVariant: cannot delete last variant in group '{groupKey}'");
                    return false;
                }

                group.Variants.Remove(target);

                // If we just deleted the selected variant, move selection to the next preferred.
                if (collection.SelectedVariants.TryGetValue(groupKey, out var sel) &&
                    string.Equals(sel, variantId, StringComparison.OrdinalIgnoreCase))
                {
                    var next = SelectPreferredVariant(group, null);
                    if (next != null)
                        collection.SelectedVariants[groupKey] = next.VariantId;
                    else
                        collection.SelectedVariants.Remove(groupKey);
                }

                if (group.Variants.Count == 0)
                    collection.LayoutGroups.Remove(groupKey);

                UpdateLegacyLayoutIndex(collection);
                SaveLayoutCollection(collection);
                _logger.LogInfo($"[ManualLayoutManager] DeleteVariant: {groupKey}/{variantId}");
                _cachedLayouts = collection;
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError($"[ManualLayoutManager] DeleteVariant failed: {ex.Message}");
                return false;
            }
        }

        public bool SetDefaultVariant(string groupKey, string variantId)
        {
            try
            {
                var collection = LoadLayoutCollection();
                if (!collection.LayoutGroups.TryGetValue(groupKey, out var group)) return false;
                var target = group.Variants.FirstOrDefault(v =>
                    string.Equals(v.VariantId, variantId, StringComparison.OrdinalIgnoreCase));
                if (target == null) return false;
                SetDefaultForOriginClass(group, variantId, target.Origin);
                UpdateLegacyLayoutIndex(collection);
                SaveLayoutCollection(collection);
                _cachedLayouts = collection;
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError($"[ManualLayoutManager] SetDefaultVariant failed: {ex.Message}");
                return false;
            }
        }

        // ─── Selected-variant persistence ─────────────────────────────────────

        public string? GetSelectedVariantId(string groupKey)
        {
            return GetSelectedVariantIdFromCollection(LoadLayoutCollection(), groupKey);
        }

        public bool SetSelectedVariantId(string groupKey, string variantId)
        {
            try
            {
                var collection = LoadLayoutCollection();
                if (!collection.LayoutGroups.TryGetValue(groupKey, out var group)) return false;
                if (!group.Variants.Any(v => string.Equals(v.VariantId, variantId, StringComparison.OrdinalIgnoreCase)))
                    return false;
                collection.SelectedVariants[groupKey] = variantId;
                UpdateLegacyLayoutIndex(collection);
                SaveLayoutCollection(collection);
                _cachedLayouts = collection;
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError($"[ManualLayoutManager] SetSelectedVariantId failed: {ex.Message}");
                return false;
            }
        }

        // ─── Private helpers ──────────────────────────────────────────────────

        private ManualLayoutCollection LoadLayoutCollection()
        {
            if (_cachedLayouts != null)
                return _cachedLayouts;

            if (!File.Exists(_layoutFilePath))
            {
                _cachedLayouts = new ManualLayoutCollection();
                return _cachedLayouts;
            }

            try
            {
                var json = File.ReadAllText(_layoutFilePath);
                var collection = JsonSerializer.Deserialize<ManualLayoutCollection>(json, LayoutJsonOptions);
                _cachedLayouts = NormalizeCollection(collection ?? new ManualLayoutCollection());
            }
            catch (Exception ex)
            {
                // A corrupt or schema-incompatible layout file must never crash the app. Preserve
                // the bad file for inspection and continue with an empty layout set; subsequent
                // saves write a fresh, valid file.
                _logger.LogWarning(
                    $"[ManualLayoutManager] Could not read layouts from {_layoutFilePath}: {ex.Message}. " +
                    "Starting with an empty layout set.");
                TryBackupUnreadableFile();
                _cachedLayouts = new ManualLayoutCollection();
            }

            return _cachedLayouts;
        }

        private void TryBackupUnreadableFile()
        {
            try
            {
                var backupPath = _layoutFilePath + ".corrupt";
                File.Copy(_layoutFilePath, backupPath, overwrite: true);
                _logger.LogWarning($"[ManualLayoutManager] Backed up unreadable layout file to {backupPath}");
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"[ManualLayoutManager] Could not back up unreadable layout file: {ex.Message}");
            }
        }

        private void SaveLayoutCollection(ManualLayoutCollection collection)
        {
            var directory = Path.GetDirectoryName(_layoutFilePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            TryBackupBeforeOverwrite();

            var json = JsonSerializer.Serialize(collection, LayoutJsonOptions);
            File.WriteAllText(_layoutFilePath, json);
            _cachedLayouts = null;
        }

        /// <summary>
        /// Keeps a copy of the previous layout file next to it, so a save that turns out to be
        /// destructive can be recovered by hand. Deliberately a single rolling <c>.bak</c> rather
        /// than timestamped copies: it is bounded, needs no cleanup policy, and covers the case
        /// that matters — the save that just ran.
        /// </summary>
        private void TryBackupBeforeOverwrite()
        {
            try
            {
                if (File.Exists(_layoutFilePath))
                    File.Copy(_layoutFilePath, _layoutFilePath + ".bak", overwrite: true);
            }
            catch (Exception ex)
            {
                // A failed backup must never block the save the user asked for.
                _logger.LogWarning($"[ManualLayoutManager] Could not back up layouts before save: {ex.Message}");
            }
        }

        private static ManualLayoutCollection NormalizeCollection(ManualLayoutCollection collection)
        {
            collection.LayoutGroups ??= new Dictionary<string, ManualLayoutGroup>();
            collection.Layouts ??= new Dictionary<string, ManualLayout>();
            collection.SelectedVariants ??= new Dictionary<string, string>(StringComparer.Ordinal);

            foreach (var entry in collection.LayoutGroups.ToList())
            {
                entry.Value.GroupKey = string.IsNullOrWhiteSpace(entry.Value.GroupKey) ? entry.Key : entry.Value.GroupKey;
                entry.Value.Variants ??= new List<ManualLayout>();
                foreach (var variant in entry.Value.Variants)
                    NormalizeVariant(entry.Value.GroupKey, variant);
            }

            if (collection.LayoutGroups.Count == 0 && collection.Layouts.Count > 0)
            {
                foreach (var entry in collection.Layouts)
                {
                    var variant = entry.Value;
                    NormalizeVariant(entry.Key, variant);
                    variant.Origin = variant.Origin == 0 ? ManualLayoutOrigin.Manual : variant.Origin;
                    collection.LayoutGroups[entry.Key] = new ManualLayoutGroup
                    {
                        GroupKey = entry.Key,
                        Variants = new List<ManualLayout> { variant }
                    };
                }
            }

            UpdateLegacyLayoutIndex(collection);
            return collection;
        }

        private static void NormalizeVariant(string groupKey, ManualLayout variant)
        {
            variant.Key = string.IsNullOrWhiteSpace(variant.Key) ? groupKey : variant.Key;
            variant.GroupKey = string.IsNullOrWhiteSpace(variant.GroupKey) ? groupKey : variant.GroupKey;
            variant.VariantId = string.IsNullOrWhiteSpace(variant.VariantId)
                ? (variant.Origin == ManualLayoutOrigin.AutoSeed ? "seed-default" : "manual-default")
                : variant.VariantId;
            variant.DisplayName = string.IsNullOrWhiteSpace(variant.DisplayName)
                ? GetDefaultDisplayName(variant.Origin)
                : variant.DisplayName;
            variant.Markers ??= new List<ManualLayoutMarker>();
            variant.LocationCount = variant.LocationCount > 0 ? variant.LocationCount : variant.Markers.Count;
            if (variant.Timestamp == default) variant.Timestamp = DateTime.UtcNow;
            if (variant.CreatedUtc == default) variant.CreatedUtc = variant.Timestamp;
            if (variant.UpdatedUtc == default) variant.UpdatedUtc = variant.Timestamp;
        }

        private static void UpdateLegacyLayoutIndex(ManualLayoutCollection collection)
        {
            collection.Layouts = collection.LayoutGroups
                .Select(group => new
                {
                    group.Key,
                    PreferredVariant = SelectPreferredVariant(
                        group.Value,
                        collection.SelectedVariants?.GetValueOrDefault(group.Key))
                })
                .Where(e => e.PreferredVariant != null)
                .ToDictionary(e => e.Key, e => e.PreferredVariant!, StringComparer.Ordinal);
        }

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

        private static void SetDefaultForOriginClass(ManualLayoutGroup group, string variantId, ManualLayoutOrigin origin)
        {
            foreach (var v in group.Variants.Where(v => v.Origin == origin))
                v.IsDefault = string.Equals(v.VariantId, variantId, StringComparison.OrdinalIgnoreCase);
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

        private static string GetDefaultDisplayName(ManualLayoutOrigin origin) => origin switch
        {
            ManualLayoutOrigin.AutoSeed => "Generated Seed",
            ManualLayoutOrigin.Imported => "Imported Layout",
            _ => "Manual Layout"
        };

        private static JsonSerializerOptions CreateJsonOptions()
        {
            var options = new JsonSerializerOptions { WriteIndented = true };
            options.Converters.Add(new JsonStringEnumConverter());
            return options;
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
