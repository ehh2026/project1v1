using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using InteractiveWorldMap.Models;
using InteractiveWorldMap.Utilities;
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
        private readonly ICompositePinPlanningResultProvider _planningService;

        public CompositePinApplicationService(
            CompositePinPlanCache planCache,
            ICompositePinPlanningResultProvider planningService)
        {
            _planCache = planCache;
            _planningService = planningService;
        }

        /// <summary>
        /// Tries to load a full set of render plans from disk for the given layout snapshot.
        /// Returns <c>null</c> on cache miss; also outputs the computed cache key so the
        /// caller can pass it to <see cref="SaveIfMissed"/> after building plans.
        /// </summary>
        /// <param name="layout">The layout whose markers define the content hash.</param>
        /// <param name="config">Pin-part configuration affecting render output.</param>
        /// <param name="groupKey">Layout group key, passed explicitly by the caller.</param>
        /// <param name="absoluteGeometryPath">Full path to <c>pin_part_geometry.json</c>.</param>
        /// <param name="cacheKey">Computed cache key (use in <see cref="SaveIfMissed"/>).</param>
        public IReadOnlyDictionary<string, CompositePinRenderPlan>? TryCacheLoad(
            ManualLayout layout,
            PinPartConfig config,
            string groupKey,
            string absoluteGeometryPath,
            out string cacheKey)
        {
            var layoutHash = CompositePinLayoutContentHasher.ComputeLayoutContentHash(layout.Markers);
            var geometryHash = CompositePinLayoutContentHasher.ComputeGeometryHash(absoluteGeometryPath);
            var configHash = CompositePinLayoutContentHasher.ComputeConfigHash(config);

            cacheKey = _planCache.ComputeCacheKey(groupKey, layout.VariantId, layoutHash, geometryHash, configHash);

            var entries = _planCache.TryLoad(cacheKey);
            return entries?.ToDictionary(e => e.LocationId, e => e.Plan, System.StringComparer.Ordinal);
        }

        /// <summary>
        /// After a cache-miss render pass, collects the plans that were built during this session
        /// (via <see cref="ICompositePinPlanningResultProvider.TryGetLastResult"/>) and persists them.
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

        /// <summary>
        /// Builds per-marker screen positions and optional cached render plans for manual layout replay.
        /// Does not load bitmaps or mutate WPF markers.
        /// </summary>
        public ManualLayoutApplyResult BuildApplyInstructions(
            ManualLayout layout,
            IReadOnlyList<LayoutEditorController.LayoutMarkerApplication> applications,
            IReadOnlyDictionary<string, (double PixelX, double PixelY)> markerSourceCoords,
            ViewportState? viewport,
            double containerWidth,
            double containerHeight,
            PinPartConfig config,
            string groupKey,
            string absoluteGeometryPath,
            bool canUseCompositePins,
            ViewportState? fullMapViewport = null)
        {
            IReadOnlyDictionary<string, CompositePinRenderPlan>? cachedPlans = null;
            string cacheKey = string.Empty;
            bool cacheAttempted = !string.IsNullOrEmpty(groupKey) && canUseCompositePins;

            if (cacheAttempted)
            {
                cachedPlans = TryCacheLoad(
                    layout, config, groupKey, absoluteGeometryPath, out cacheKey);
            }

            var instructions = new List<ManualLayoutApplyInstruction>(applications.Count);
            bool hasViewport = viewport != null && containerWidth > 0 && containerHeight > 0;

            foreach (var application in applications)
            {
                instructions.Add(BuildInstruction(
                    application,
                    markerSourceCoords,
                    viewport,
                    fullMapViewport,
                    containerWidth,
                    containerHeight,
                    hasViewport,
                    cachedPlans));
            }

            return new ManualLayoutApplyResult
            {
                Instructions = instructions,
                CacheKey = cacheKey,
                ShouldSaveToCache = cacheAttempted && cachedPlans == null && !string.IsNullOrEmpty(cacheKey)
            };
        }

        private static ManualLayoutApplyInstruction BuildInstruction(
            LayoutEditorController.LayoutMarkerApplication application,
            IReadOnlyDictionary<string, (double PixelX, double PixelY)> markerSourceCoords,
            ViewportState? viewport,
            ViewportState? fullMapViewport,
            double containerWidth,
            double containerHeight,
            bool hasViewport,
            IReadOnlyDictionary<string, CompositePinRenderPlan>? cachedPlans)
        {
            (double PixelX, double PixelY) source = default;
            bool haveSource = hasViewport
                && markerSourceCoords.TryGetValue(application.LocationName, out source);

            var originalPos = ResolveOriginalPosition(
                application,
                viewport,
                containerWidth,
                containerHeight,
                haveSource,
                source);
            var extendedPos = ResolveExtendedPosition(
                application,
                viewport,
                fullMapViewport,
                containerWidth,
                containerHeight,
                haveSource,
                source,
                originalPos);
            var requiresExtensionLine =
                ManualLayoutPlacementPolicy.RequiresExtensionLine(originalPos, extendedPos);

            return new ManualLayoutApplyInstruction(
                application.LocationName,
                originalPos,
                extendedPos,
                requiresExtensionLine,
                application.PairId,
                application.HeadSourcePath,
                ResolveCachedPlan(cachedPlans, application.LocationName));
        }

        private static Point ResolveOriginalPosition(
            LayoutEditorController.LayoutMarkerApplication application,
            ViewportState? viewport,
            double containerWidth,
            double containerHeight,
            bool haveSource,
            (double PixelX, double PixelY) source)
        {
            return haveSource
                ? viewport!.SourceToScreen(source.PixelX, source.PixelY, containerWidth, containerHeight)
                : application.OriginalPosition;
        }

        private static Point ResolveExtendedPosition(
            LayoutEditorController.LayoutMarkerApplication application,
            ViewportState? viewport,
            ViewportState? fullMapViewport,
            double containerWidth,
            double containerHeight,
            bool haveSource,
            (double PixelX, double PixelY) source,
            Point originalPos)
        {
            if (application.SourceExtendedX.HasValue && application.SourceExtendedY.HasValue && haveSource)
            {
                var projected = ProjectSourceExtendedPosition(
                    application,
                    viewport,
                    fullMapViewport,
                    containerWidth,
                    containerHeight,
                    source,
                    originalPos);

                // A layout authored zoomed in stores a head offset that is tiny in source space —
                // a 59-screen-pixel drag at zoom 55 is about one source pixel. Re-projected at the
                // full-map reference scale that becomes a fraction of a pixel, landing under the
                // extension threshold so the pin replays as an auto stub. When the marker was saved
                // with a real extension, trust the saved screen geometry instead of the collapsed
                // projection. Full-map layouts are unaffected: their source offsets are large
                // enough to survive the projection, so this branch never fires for them.
                if (!ManualLayoutPlacementPolicy.RequiresExtensionLine(originalPos, projected) &&
                    application.LineLength > ManualLayoutPlacementPolicy.ExtensionLineThreshold)
                {
                    return ProjectAngledExtendedPosition(application, originalPos);
                }

                return projected;
            }

            return ProjectAngledExtendedPosition(application, originalPos);
        }

        private static Point ProjectSourceExtendedPosition(
            LayoutEditorController.LayoutMarkerApplication application,
            ViewportState? viewport,
            ViewportState? fullMapViewport,
            double containerWidth,
            double containerHeight,
            (double PixelX, double PixelY) source,
            Point originalPos)
        {
            // Keep the head offset at the full-map reference scale so zoom replay preserves screen length.
            var refViewport = fullMapViewport ?? viewport!;
            var refAnchor = refViewport.SourceToScreen(
                source.PixelX, source.PixelY, containerWidth, containerHeight);
            var refHead = refViewport.SourceToScreen(
                application.SourceExtendedX!.Value,
                application.SourceExtendedY!.Value,
                containerWidth,
                containerHeight);

            return new Point(
                originalPos.X + (refHead.X - refAnchor.X),
                originalPos.Y + (refHead.Y - refAnchor.Y));
        }

        private static Point ProjectAngledExtendedPosition(
            LayoutEditorController.LayoutMarkerApplication application,
            Point originalPos)
        {
            var rad = application.Angle * Math.PI / 180.0;
            return new Point(
                originalPos.X + application.LineLength * Math.Sin(rad),
                originalPos.Y - application.LineLength * Math.Cos(rad));
        }

        private static CompositePinRenderPlan? ResolveCachedPlan(
            IReadOnlyDictionary<string, CompositePinRenderPlan>? cachedPlans,
            string locationName)
        {
            return cachedPlans != null && cachedPlans.TryGetValue(locationName, out var plan)
                ? plan
                : null;
        }
    }
}
