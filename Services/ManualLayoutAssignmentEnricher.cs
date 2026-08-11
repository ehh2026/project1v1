using System.Collections.Generic;
using InteractiveWorldMap.Models;

namespace InteractiveWorldMap.Services
{
    /// <summary>
    /// Extracts shaft/head assignment data from the planning service's per-session cache and
    /// returns it as a dictionary keyed by location name.  Called by the save handler so that
    /// <see cref="ManualLayoutManager.SaveLayout"/> can persist the assignments.
    /// </summary>
    public class ManualLayoutAssignmentEnricher
    {
        /// <summary>
        /// Returns a mapping from location name → (PairId, HeadSourcePath) for every extension
        /// whose location has a cached plan in <paramref name="planningService"/>.
        /// Extensions with no cached plan are omitted; the save path treats absent entries as
        /// "use scorer/hash fallback on replay".
        /// </summary>
        public IReadOnlyDictionary<string, (string PairId, string HeadSourcePath)> GetAssignments(
            IEnumerable<RadialExtension> extensions,
            ICompositePinPlanningResultProvider planningService)
        {
            var result = new Dictionary<string, (string, string)>(System.StringComparer.Ordinal);

            foreach (var ext in extensions)
            {
                var name = ext.Location.Name;
                if (planningService.TryGetLastResult(name, out var plan) && plan != null)
                {
                    result[name] = (plan.RenderPlan.PairId, plan.RenderPlan.HeadSourcePath);
                }
            }

            return result;
        }
    }
}
