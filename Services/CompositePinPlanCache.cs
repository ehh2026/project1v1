using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using InteractiveWorldMap.Models;

namespace InteractiveWorldMap.Services
{
    /// <summary>
    /// Persists composite render-plan lists to disk, keyed by a SHA-256 hash of the
    /// layout group key, variant, layout content, geometry, and config inputs.
    /// Follows the same pattern as <see cref="ClusterCache"/>.
    ///
    /// Storage: <c>%AppData%\InteractiveWorldMap\composite_pin_plan_cache\{key}.json</c>
    /// </summary>
    public class CompositePinPlanCache
    {
        private const int CacheVersion = 1;

        private readonly string _cacheDirectory;
        private readonly ILogger _logger;

        private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

        public CompositePinPlanCache(ILogger logger, string? cacheDirectory = null)
        {
            _logger = logger;
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            _cacheDirectory = cacheDirectory
                ?? Path.Combine(appData, "InteractiveWorldMap", "composite_pin_plan_cache");
        }

        // ─── Key ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Produces the filename-safe cache key from the five discriminating inputs.
        /// All inputs are already short hashes / stable strings, so this is a final
        /// SHA-256 over their concatenation.
        /// </summary>
        public string ComputeCacheKey(
            string groupKey,
            string variantId,
            string layoutContentHash,
            string geometryHash,
            string configHash)
        {
            var input = $"v{CacheVersion}:{groupKey}:{variantId}:{layoutContentHash}:{geometryHash}:{configHash}";
            using var sha = SHA256.Create();
            var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(input));
            return Convert.ToHexString(bytes)[..32];
        }

        // ─── Load ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Returns cached entries for <paramref name="cacheKey"/>, or <c>null</c> on miss.
        /// Logs hit/miss at Info level for debug visibility.
        /// </summary>
        public List<CachedCompositePlanEntry>? TryLoad(string cacheKey)
        {
            try
            {
                var path = CachePath(cacheKey);
                if (!File.Exists(path))
                {
                    _logger.LogInfo($"[CompositePinPlanCache] Miss: {Abbrev(cacheKey)}");
                    return null;
                }

                var json = File.ReadAllText(path);
                var file = JsonSerializer.Deserialize<CacheFile>(json, JsonOptions);

                if (file == null || file.Version != CacheVersion)
                {
                    _logger.LogInfo($"[CompositePinPlanCache] Stale (version): {Abbrev(cacheKey)}");
                    return null;
                }

                var entries = file.Entries
                    .Select(e => new CachedCompositePlanEntry(e.LocationId, e.Plan))
                    .ToList();
                _logger.LogInfo($"[CompositePinPlanCache] Hit: {entries.Count} plans, key={Abbrev(cacheKey)}");
                return entries;
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"[CompositePinPlanCache] Load failed: {ex.Message}");
                return null;
            }
        }

        // ─── Save ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Persists <paramref name="entries"/> to disk under <paramref name="cacheKey"/>.
        /// <paramref name="groupKey"/> and <paramref name="variantId"/> are stored in the file
        /// so that <see cref="Invalidate"/> can locate entries without decoding the key.
        /// </summary>
        public void Save(
            string cacheKey,
            string groupKey,
            string variantId,
            IEnumerable<CachedCompositePlanEntry> entries)
        {
            try
            {
                Directory.CreateDirectory(_cacheDirectory);

                var file = new CacheFile
                {
                    Version = CacheVersion,
                    GroupKey = groupKey,
                    VariantId = variantId,
                    Entries = entries
                        .Select(e => new CacheFileEntry { LocationId = e.LocationId, Plan = e.Plan })
                        .ToList()
                };

                File.WriteAllText(CachePath(cacheKey), JsonSerializer.Serialize(file, JsonOptions));
                _logger.LogInfo($"[CompositePinPlanCache] Saved {file.Entries.Count} plans, key={Abbrev(cacheKey)}");
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"[CompositePinPlanCache] Save failed: {ex.Message}");
            }
        }

        // ─── Invalidation ────────────────────────────────────────────────────

        /// <summary>
        /// Deletes all cached files whose stored <c>GroupKey</c> matches <paramref name="groupKey"/>.
        /// Called after a layout is saved so the next render builds fresh plans.
        /// </summary>
        public void Invalidate(string groupKey)
        {
            try
            {
                if (!Directory.Exists(_cacheDirectory))
                    return;

                int removed = 0;
                foreach (var file in Directory.GetFiles(_cacheDirectory, "*.json"))
                {
                    try
                    {
                        var json = File.ReadAllText(file);
                        var cached = JsonSerializer.Deserialize<CacheFile>(json, JsonOptions);
                        if (string.Equals(cached?.GroupKey, groupKey, StringComparison.Ordinal))
                        {
                            File.Delete(file);
                            removed++;
                        }
                    }
                    catch { /* skip unreadable files */ }
                }

                _logger.LogInfo($"[CompositePinPlanCache] Invalidated {removed} file(s) for groupKey={groupKey}");
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"[CompositePinPlanCache] Invalidate failed: {ex.Message}");
            }
        }

        /// <summary>Deletes every file in the cache directory.</summary>
        public void ClearAll()
        {
            try
            {
                if (!Directory.Exists(_cacheDirectory))
                    return;

                int removed = 0;
                foreach (var f in Directory.GetFiles(_cacheDirectory, "*.json"))
                {
                    File.Delete(f);
                    removed++;
                }
                _logger.LogInfo($"[CompositePinPlanCache] Cleared {removed} cache file(s)");
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"[CompositePinPlanCache] ClearAll failed: {ex.Message}");
            }
        }

        // ─── Helpers ─────────────────────────────────────────────────────────

        private string CachePath(string key) =>
            Path.Combine(_cacheDirectory, $"{key}.json");

        private static string Abbrev(string key) =>
            key.Length >= 8 ? key[..8] + "…" : key;

        private static JsonSerializerOptions CreateJsonOptions()
        {
            var opts = new JsonSerializerOptions { WriteIndented = false };
            opts.Converters.Add(new PointConverter());
            opts.Converters.Add(new MatrixConverter());
            return opts;
        }

        // ─── Serialisation DTOs ──────────────────────────────────────────────

        private sealed class CacheFile
        {
            public int Version { get; set; }
            public string GroupKey { get; set; } = string.Empty;
            public string VariantId { get; set; } = string.Empty;
            public List<CacheFileEntry> Entries { get; set; } = new();
        }

        private sealed class CacheFileEntry
        {
            public string LocationId { get; set; } = string.Empty;
            public CompositePinRenderPlan Plan { get; set; } = new();
        }

        // ─── JSON converters for WPF geometry types ──────────────────────────

        /// <summary>Serialises <see cref="System.Windows.Point"/> as <c>{X, Y}</c>.</summary>
        private sealed class PointConverter : JsonConverter<System.Windows.Point>
        {
            public override System.Windows.Point Read(
                ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            {
                double x = 0, y = 0;
                while (reader.Read())
                {
                    if (reader.TokenType == JsonTokenType.EndObject)
                        break;
                    if (reader.TokenType != JsonTokenType.PropertyName)
                        continue;
                    var prop = reader.GetString();
                    reader.Read();
                    if (prop == "X") x = reader.GetDouble();
                    else if (prop == "Y") y = reader.GetDouble();
                }
                return new System.Windows.Point(x, y);
            }

            public override void Write(
                Utf8JsonWriter writer, System.Windows.Point value, JsonSerializerOptions options)
            {
                writer.WriteStartObject();
                writer.WriteNumber("X", value.X);
                writer.WriteNumber("Y", value.Y);
                writer.WriteEndObject();
            }
        }

        /// <summary>
        /// Serialises <see cref="System.Windows.Media.Matrix"/> as the six affine coefficients,
        /// skipping computed read-only properties (Determinant, HasInverse, IsIdentity).
        /// </summary>
        private sealed class MatrixConverter : JsonConverter<System.Windows.Media.Matrix>
        {
            public override System.Windows.Media.Matrix Read(
                ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            {
                double m11 = 1, m12 = 0, m21 = 0, m22 = 1, ox = 0, oy = 0;
                while (reader.Read())
                {
                    if (reader.TokenType == JsonTokenType.EndObject)
                        break;
                    if (reader.TokenType != JsonTokenType.PropertyName)
                        continue;
                    var prop = reader.GetString();
                    reader.Read();
                    switch (prop)
                    {
                        case "M11": m11 = reader.GetDouble(); break;
                        case "M12": m12 = reader.GetDouble(); break;
                        case "M21": m21 = reader.GetDouble(); break;
                        case "M22": m22 = reader.GetDouble(); break;
                        case "OffsetX": ox = reader.GetDouble(); break;
                        case "OffsetY": oy = reader.GetDouble(); break;
                        default: reader.Skip(); break;
                    }
                }
                return new System.Windows.Media.Matrix(m11, m12, m21, m22, ox, oy);
            }

            public override void Write(
                Utf8JsonWriter writer, System.Windows.Media.Matrix value, JsonSerializerOptions options)
            {
                writer.WriteStartObject();
                writer.WriteNumber("M11", value.M11);
                writer.WriteNumber("M12", value.M12);
                writer.WriteNumber("M21", value.M21);
                writer.WriteNumber("M22", value.M22);
                writer.WriteNumber("OffsetX", value.OffsetX);
                writer.WriteNumber("OffsetY", value.OffsetY);
                writer.WriteEndObject();
            }
        }
    }
}
