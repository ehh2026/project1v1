using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Windows;
using InteractiveWorldMap.Models;

namespace InteractiveWorldMap.Services
{
    /// <summary>
    /// Persists clustering results to disk, keyed by a hash of location data.
    /// Cache is invalidated automatically when locations change.
    /// </summary>
    public class ClusterCache
    {
        private readonly string _cachePath;
        private readonly ILogger _logger;

        public ClusterCache(ILogger logger)
        {
            _logger = logger;
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            _cachePath = Path.Combine(appData, "InteractiveWorldMap", "cluster_cache.json");
        }

        /// <summary>
        /// Tries to load cached clusters. Returns null if cache is missing or stale.
        /// </summary>
        public List<LocationCluster>? TryLoad(List<Location> locations, double threshold)
        {
            try
            {
                if (!File.Exists(_cachePath))
                {
                    _logger.LogInfo("[ClusterCache] No cache file found");
                    return null;
                }

                var json = File.ReadAllText(_cachePath);
                var cached = JsonSerializer.Deserialize<CacheFile>(json);

                if (cached == null)
                    return null;

                var currentHash = ComputeHash(locations, threshold);
                if (cached.LocationHash != currentHash)
                {
                    _logger.LogInfo($"[ClusterCache] Cache stale (hash mismatch), will recompute");
                    return null;
                }

                // Rebuild LocationCluster objects, resolving Location references by name+coords
                var locationLookup = locations.ToDictionary(l => LocationKey(l));
                var clusters = new List<LocationCluster>();

                foreach (var entry in cached.Clusters)
                {
                    var cluster = new LocationCluster
                    {
                        Id = entry.Id,
                        CenterPoint = new Point(entry.CenterX, entry.CenterY)
                    };

                    foreach (var key in entry.LocationKeys)
                    {
                        if (locationLookup.TryGetValue(key, out var loc))
                            cluster.Locations.Add(loc);
                        else
                        {
                            _logger.LogWarning($"[ClusterCache] Could not resolve location key '{key}', discarding cache");
                            return null;
                        }
                    }

                    clusters.Add(cluster);
                }

                _logger.LogInfo($"[ClusterCache] Loaded {clusters.Count} clusters from cache");
                return clusters;
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"[ClusterCache] Failed to load cache: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Saves clustering results to disk.
        /// </summary>
        public void Save(List<Location> locations, List<LocationCluster> clusters, double threshold)
        {
            try
            {
                var cacheFile = new CacheFile
                {
                    LocationHash = ComputeHash(locations, threshold),
                    Clusters = clusters.Select(c => new CachedCluster
                    {
                        Id = c.Id,
                        CenterX = c.CenterPoint.X,
                        CenterY = c.CenterPoint.Y,
                        LocationKeys = c.Locations.Select(LocationKey).ToList()
                    }).ToList()
                };

                var json = JsonSerializer.Serialize(cacheFile, new JsonSerializerOptions { WriteIndented = true });
                Directory.CreateDirectory(Path.GetDirectoryName(_cachePath)!);
                File.WriteAllText(_cachePath, json);

                _logger.LogInfo($"[ClusterCache] Saved {clusters.Count} clusters to cache");
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"[ClusterCache] Failed to save cache: {ex.Message}");
            }
        }

        /// <summary>
        /// Computes a hash of location names, coordinates, and threshold to detect data changes.
        /// </summary>
        private static string ComputeHash(List<Location> locations, double threshold)
        {
            // Sort by name so order doesn't matter
            var sorted = locations.OrderBy(l => l.Name).ThenBy(l => l.PixelX).ThenBy(l => l.PixelY);
            var sb = new StringBuilder();
            sb.Append($"threshold:{threshold:F1};");
            foreach (var l in sorted)
                sb.Append($"{l.Name}:{l.PixelX:F2},{l.PixelY:F2};");

            using var sha = SHA256.Create();
            var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(sb.ToString()));
            return Convert.ToHexString(bytes)[..16]; // First 16 chars is plenty
        }

        private static string LocationKey(Location l) => $"{l.Name}:{l.PixelX:F2},{l.PixelY:F2}";

        private class CacheFile
        {
            public string LocationHash { get; set; } = string.Empty;
            public List<CachedCluster> Clusters { get; set; } = new();
        }

        private class CachedCluster
        {
            public string Id { get; set; } = string.Empty;
            public double CenterX { get; set; }
            public double CenterY { get; set; }
            public List<string> LocationKeys { get; set; } = new();
        }
    }
}
