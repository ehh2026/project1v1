using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using InteractiveWorldMap.Models;

namespace InteractiveWorldMap.Services
{
    /// <summary>
    /// Manages saving, loading, and deleting manual layouts
    /// </summary>
    public class ManualLayoutManager
    {
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
                // Convert extensions to markers
                var markers = extensions.Select(ManualLayoutMarker.FromRadialExtension).ToList();
                var layout = new ManualLayout(key, markers);

                // Load existing layouts
                var collection = LoadLayoutCollection();
                
                // Add or update layout
                collection.Layouts[key] = layout;

                // Save to file
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
                
                if (collection.Layouts.TryGetValue(key, out var layout))
                {
                    _logger.LogInfo($"[ManualLayoutManager] Loaded layout: {key} ({layout.Markers.Count} markers)");
                    return layout;
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
                
                if (collection.Layouts.Remove(key))
                {
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
                return collection.Layouts.ContainsKey(key);
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
                return collection.Layouts.Keys.ToList();
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
            // Return cached if available
            if (_cachedLayouts != null)
                return _cachedLayouts;

            // Check if file exists
            if (!File.Exists(_layoutFilePath))
            {
                _cachedLayouts = new ManualLayoutCollection();
                return _cachedLayouts;
            }

            // Load from file
            var json = File.ReadAllText(_layoutFilePath);
            var collection = JsonSerializer.Deserialize<ManualLayoutCollection>(json);
            
            _cachedLayouts = collection ?? new ManualLayoutCollection();
            return _cachedLayouts;
        }

        private void SaveLayoutCollection(ManualLayoutCollection collection)
        {
            // Ensure directory exists
            var directory = Path.GetDirectoryName(_layoutFilePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            // Serialize with indentation for readability
            var options = new JsonSerializerOptions
            {
                WriteIndented = true
            };
            var json = JsonSerializer.Serialize(collection, options);
            
            // Write to file
            File.WriteAllText(_layoutFilePath, json);
            
            // Invalidate cache
            _cachedLayouts = null;
        }
    }
}
