using InteractiveWorldMap.Models;

namespace InteractiveWorldMap.Services
{
    /// <summary>
    /// Supplies the last composite-pin planning result built for a location.
    /// </summary>
    public interface ICompositePinPlanningResultProvider
    {
        /// <summary>
        /// Returns the last plan built for <paramref name="locationId"/> in this session.
        /// Returns false if no plan has been built yet for that location.
        /// </summary>
        bool TryGetLastResult(string locationId, out CompositePinPlanningResult? result);
    }
}
