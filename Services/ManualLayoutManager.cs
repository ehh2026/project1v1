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
    /// Manages saving, loading, and deleting manual layouts
    /// </summary>
    public class ManualLayoutManager
    {
        private static readonly JsonSerializerOptions LayoutJsonOptions = CreateJsonOptions();
        private readonly string _layoutFilePath;
        private readonly ILogger _logger;
        private ManualLayoutCollection? _cachedLayouts;

        public ManualLayoutManager(string layoutFilePath, ILogger logger)
        {
            _layoutFilePath = layoutFilePath;
            _logger = logger;
        }

        /// <summary>
        /// Save a manual layout
        /// </summary>
        public bool SaveLayout(string key, List<RadialExtension> extensions)
        {
            try
            {
                var markers = extensions.Select(ManualLayoutMarker.FromRadialExtension).ToList();
                var collection = LoadLayoutCollection();

                if (!collection.LayoutGroups.TryGetValue(key, out var group))
                {
                    group = new ManualLayoutGroup { GroupKey = key };
                    collection.LayoutGroups[key] = group;
                }

                var existingManualVariant = group.Variants.FirstOrDefault(v =>
                    v.Origin == ManualLayoutOrigin.Manual &&
                    string.Equals(v.VariantId, "manual-default", StringComparison.OrdinalIgnoreCase));

                if (existingManualVariant != null)
                {
                    existingManualVariant.Markers = markers;
                    existingManualVariant.LocationCount = markers.Count;
                    existingManualVariant.Timestamp = DateTime.UtcNow;
                    existingManualVariant.UpdatedUtc = existingManualVariant.Timestamp;
                    existingManualVariant.IsDefault = true;
                    existingManualVariant.DisplayName = "Manual Layout";
                    existingManualVariant.GroupKey = key;
                    existingManualVariant.Key = key;
                }
                else
                {
                    var layout = new ManualLayout(key, markers)
                    {
                        GroupKey = key,
                        VariantId = "manual-default",
                        DisplayName = "Manual Layout",
                        Origin = ManualLayoutOrigin.Manual,
                        IsDefault = true
                    };

                    var defaultAutoSeed = group.Variants.FirstOrDefault(v =>
                        v.Origin == ManualLayoutOrigin.AutoSeed &&
                        v.IsDefault);
                    if (defaultAutoSeed != null)
                    {
                        layout.BasedOnKey = defaultAutoSeed.Key;
                        layout.BasedOnVariantId = defaultAutoSeed.VariantId;
                    }

                    group.Variants.Add(layout);
                }

                UpdateLegacyLayoutIndex(collection);
                SaveLayoutCollection(collection);

                _logger.LogInfo($"[ManualLayoutManager] Saved layout: {key} ({markers.Count} markers)");
                _cachedLayouts = collection;
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError($"[ManualLayoutManager] Failed to save layout: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Load a manual layout by key
        /// </summary>
        public ManualLayout? LoadLayout(string key)
        {
            try
            {
                var collection = LoadLayoutCollection();

                if (collection.LayoutGroups.TryGetValue(key, out var exactGroup))
                {
                    var layout = SelectPreferredVariant(exactGroup);
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
                    var compatibleLayout = SelectPreferredVariant(compatibleGroup);
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

        /// <summary>
        /// Delete a manual layout by key
        /// </summary>
        public bool DeleteLayout(string key)
        {
            try
            {
                var collection = LoadLayoutCollection();

                if (collection.LayoutGroups.TryGetValue(key, out var group))
                {
                    var removedCount = group.Variants.RemoveAll(v => v.Origin == ManualLayoutOrigin.Manual);
                    if (removedCount > 0)
                    {
                        if (group.Variants.Count == 0)
                        {
                            collection.LayoutGroups.Remove(key);
                        }

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
        /// Check if a layout exists for the given key
        /// </summary>
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

        /// <summary>
        /// Get all saved layout keys
        /// </summary>
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

        /// <summary>
        /// Apply a saved layout to extensions
        /// </summary>
        public bool ApplyLayout(ManualLayout layout, List<RadialExtension> extensions)
        {
            try
            {
                // Create a dictionary for quick lookup
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

        private ManualLayoutCollection LoadLayoutCollection()
        {
            if (_cachedLayouts != null)
                return _cachedLayouts;

            if (!File.Exists(_layoutFilePath))
            {
                _cachedLayouts = new ManualLayoutCollection();
                return _cachedLayouts;
            }

            var json = File.ReadAllText(_layoutFilePath);
            var collection = JsonSerializer.Deserialize<ManualLayoutCollection>(json, LayoutJsonOptions);

            _cachedLayouts = NormalizeCollection(collection ?? new ManualLayoutCollection());
            return _cachedLayouts;
        }

        private void SaveLayoutCollection(ManualLayoutCollection collection)
        {
            var directory = Path.GetDirectoryName(_layoutFilePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var json = JsonSerializer.Serialize(collection, LayoutJsonOptions);

            File.WriteAllText(_layoutFilePath, json);

            _cachedLayouts = null;
        }

        private static ManualLayoutCollection NormalizeCollection(ManualLayoutCollection collection)
        {
            collection.LayoutGroups ??= new Dictionary<string, ManualLayoutGroup>();
            collection.Layouts ??= new Dictionary<string, ManualLayout>();

            foreach (var entry in collection.LayoutGroups.ToList())
            {
                entry.Value.GroupKey = string.IsNullOrWhiteSpace(entry.Value.GroupKey) ? entry.Key : entry.Value.GroupKey;
                entry.Value.Variants ??= new List<ManualLayout>();
                foreach (var variant in entry.Value.Variants)
                {
                    NormalizeVariant(entry.Value.GroupKey, variant);
                }
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
            if (variant.Timestamp == default)
            {
                variant.Timestamp = DateTime.UtcNow;
            }

            if (variant.CreatedUtc == default)
            {
                variant.CreatedUtc = variant.Timestamp;
            }

            if (variant.UpdatedUtc == default)
            {
                variant.UpdatedUtc = variant.Timestamp;
            }
        }

        private static void UpdateLegacyLayoutIndex(ManualLayoutCollection collection)
        {
            collection.Layouts = collection.LayoutGroups
                .Select(group => new
                {
                    group.Key,
                    PreferredVariant = SelectPreferredVariant(group.Value)
                })
                .Where(entry => entry.PreferredVariant != null)
                .ToDictionary(
                    entry => entry.Key,
                    entry => entry.PreferredVariant!,
                    StringComparer.Ordinal);
        }

        private static ManualLayout? SelectPreferredVariant(ManualLayoutGroup group)
        {
            return group.Variants
                .OrderByDescending(GetVariantPriority)
                .ThenByDescending(variant => variant.UpdatedUtc)
                .ThenByDescending(variant => variant.Timestamp)
                .FirstOrDefault();
        }

        private static int GetVariantPriority(ManualLayout variant)
        {
            if (variant.Origin == ManualLayoutOrigin.Manual && variant.IsDefault)
                return 6;
            if (variant.Origin == ManualLayoutOrigin.AutoSeed && variant.IsDefault)
                return 5;
            if (variant.Origin == ManualLayoutOrigin.Manual)
                return 4;
            if (variant.Origin == ManualLayoutOrigin.AutoSeed)
                return 3;
            if (variant.IsDefault)
                return 2;
            return 1;
        }

        private static ManualLayout? FindCompatibleLayout(string key, ManualLayoutCollection collection)
        {
            var compatibleLayouts = collection.Layouts
                .Where(entry => LayoutKeyGenerator.AreKeysCompatible(entry.Key, key))
                .Select(entry => new
                {
                    Layout = entry.Value,
                    ZoomDifference = Math.Abs(ExtractZoomLevel(entry.Key) - ExtractZoomLevel(key))
                })
                .OrderBy(candidate => candidate.ZoomDifference)
                .ThenByDescending(candidate => candidate.Layout.Timestamp)
                .Select(candidate => candidate.Layout)
                .ToList();

            return compatibleLayouts.FirstOrDefault();
        }

        private static ManualLayoutGroup? FindCompatibleGroup(string key, ManualLayoutCollection collection)
        {
            return collection.LayoutGroups
                .Where(entry => LayoutKeyGenerator.AreKeysCompatible(entry.Key, key))
                .Select(entry => new
                {
                    Group = entry.Value,
                    ZoomDifference = Math.Abs(ExtractZoomLevel(entry.Key) - ExtractZoomLevel(key)),
                    PreferredVariant = SelectPreferredVariant(entry.Value)
                })
                .OrderBy(candidate => candidate.ZoomDifference)
                .ThenByDescending(candidate => candidate.PreferredVariant?.UpdatedUtc ?? DateTime.MinValue)
                .ThenByDescending(candidate => candidate.PreferredVariant?.Timestamp ?? DateTime.MinValue)
                .Select(candidate => candidate.Group)
                .FirstOrDefault();
        }

        private static string GetDefaultDisplayName(ManualLayoutOrigin origin)
        {
            return origin switch
            {
                ManualLayoutOrigin.AutoSeed => "Generated Seed",
                ManualLayoutOrigin.Imported => "Imported Layout",
                _ => "Manual Layout"
            };
        }

        private static JsonSerializerOptions CreateJsonOptions()
        {
            var options = new JsonSerializerOptions
            {
                WriteIndented = true
            };
            options.Converters.Add(new JsonStringEnumConverter());
            return options;
        }

        private static double ExtractZoomLevel(string key)
        {
            var parts = key.Split('_');
            if (parts.Length > 1 && parts[1].StartsWith("z") && double.TryParse(parts[1].Substring(1), out var zoom))
            {
                return zoom;
            }

            return 0;
        }
    }
}
