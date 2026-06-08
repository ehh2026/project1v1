using System.Collections.Generic;
using System.Linq;
using InteractiveWorldMap.Models;
using IOPath = System.IO.Path;

namespace InteractiveWorldMap.Services
{
    /// <summary>
    /// Orchestrates the composite render-plan disk cache: computes the cache key,
    /// attempts a cache load, and saves plans after a cache-miss build pass.
    /// All cache key hashing and file I/O stays in this service and
    /// <see cref="CompositePinPlanCache"/> — MainWindow calls only the three public methods.
    /// </summary>
    public class CompositePinApplicationService
    {
        private readonly CompositePinPlanCache _planCache;
        private readonly CompositePinPlanningService _planningService;

        public CompositePinApplicationService(
            CompositePinPlanCache planCache,
            CompositePinPlanningService planningService)
        {
            _planCache       = planCache;
            _planningService = planningService;
        }

        /// <summary>
        /// Tries to load a full set of render plans from disk for the given layout snapshot.
        /// Returns <c>null</c> on cache miss; also outputs the computed cache key so the
        /// caller can pass it to <see cref="SaveIfMissed"/> after building plans.
        /// </summary>
        /// <param name="layout">The layout whose markers define the content hash.</param>
        /// <param name="config">Pin-part configuration affecting render output.</param>
        /// <param name="groupKey">Layout group key (from <c>LayoutEditorController.CurrentLayoutKey</c>).</param>
        /// <param name="absoluteGeometryPath">Full path to <c>pin_part_geometry.json</c>.</param>
        /// <param name="cacheKey">Computed cache key (use in <see cref="SaveIfMissed"/>).</param>
        public IReadOnlyDictionary<string, CompositePinRenderPlan>? TryCacheLoad(
            ManualLayout layout,
            PinPartConfig config,
            string groupKey,
            string absoluteGeometryPath,
            out string cacheKey)
        {
            var layoutHash   = CompositePinLayoutContentHasher.ComputeLayoutContentHash(layout.Markers);
            var geometryHash = CompositePinLayoutContentHasher.ComputeGeometryHash(absoluteGeometryPath);
            var configHash   = CompositePinLayoutContentHasher.ComputeConfigHash(config);

            cacheKey = _planCache.ComputeCacheKey(groupKey, layout.VariantId, layoutHash, geometryHash, configHash);

            var entries = _planCache.TryLoad(cacheKey);
            return entries?.ToDictionary(e => e.LocationId, e => e.Plan, System.StringComparer.Ordinal);
        }

        /// <summary>
        /// After a cache-miss render pass, collects the plans that were built during this session
        /// (via <see cref="CompositePinPlanningService.TryGetLastResult"/>) and persists them.
        /// Safe to call even when no plans were built — logs and returns silently.
        /// </summary>
        public void SaveIfMissed(
            string cacheKey,
            string groupKey,
            string variantId,
            IEnumerable<string> locationNames)
        {
            var entries = locationNames
                .Select(name =>
                {
                    if (_planningService.TryGetLastResult(name, out var r) && r != null)
                        return new CachedCompositePlanEntry(name, r.RenderPlan);
                    return null;
                })
                .Where(e => e != null)
                .Cast<CachedCompositePlanEntry>()
                .ToList();

            if (entries.Count > 0)
                _planCache.Save(cacheKey, groupKey, variantId, entries);
        }

        /// <summary>
        /// Invalidates all cached plans for <paramref name="groupKey"/>.
        /// Call after a layout is saved so the next <c>ApplyManualLayout</c> builds fresh plans.
        /// </summary>
        public void InvalidateGroup(string groupKey) => _planCache.Invalidate(groupKey);
    }
}
