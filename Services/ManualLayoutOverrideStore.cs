using System;
using System.Collections.Generic;
using System.Windows;

namespace InteractiveWorldMap.Services
{
    /// <summary>
    /// Tracks in-memory shaft/head pair overrides and composite pin endpoints.
    /// Overrides are cleared when flushed to a saved layout or when navigating away.
    /// </summary>
    public class ManualLayoutOverrideStore
    {
        private readonly Dictionary<string, (string PairId, string? HeadSourcePath)> _pendingOverrides
            = new(StringComparer.Ordinal);

        private readonly Dictionary<string, (Point OriginalPos, Point ExtendedPos)> _endpoints
            = new(StringComparer.Ordinal);

        /// <summary>True when any overrides are waiting to be saved.</summary>
        public bool HasPendingOverrides => _pendingOverrides.Count > 0;

        /// <summary>Records the current screen endpoints for a composite pin marker.</summary>
        public void RecordEndpoints(string locationName, Point originalPos, Point extendedPos)
            => _endpoints[locationName] = (originalPos, extendedPos);

        /// <summary>Returns the last recorded endpoints for <paramref name="locationName"/>.</summary>
        public bool TryGetEndpoints(string locationName, out Point originalPos, out Point extendedPos)
        {
            if (_endpoints.TryGetValue(locationName, out var pair))
            {
                originalPos = pair.OriginalPos;
                extendedPos = pair.ExtendedPos;
                return true;
            }
            originalPos = default;
            extendedPos = default;
            return false;
        }

        /// <summary>Stores a pending shaft override for <paramref name="locationName"/>.</summary>
        public void SetOverride(string locationName, string pairId, string? headSourcePath = null)
            => _pendingOverrides[locationName] = (pairId, headSourcePath);

        /// <summary>Returns the pending override for <paramref name="locationName"/>, if any.</summary>
        public bool TryGetOverride(string locationName, out string? pairId, out string? headSourcePath)
        {
            if (_pendingOverrides.TryGetValue(locationName, out var o))
            {
                pairId = o.PairId;
                headSourcePath = o.HeadSourcePath;
                return true;
            }
            pairId = null;
            headSourcePath = null;
            return false;
        }

        /// <summary>Returns all pending overrides (for merge into assignments at save time).</summary>
        public IReadOnlyDictionary<string, (string PairId, string? HeadSourcePath)> GetAllOverrides()
            => _pendingOverrides;

        /// <summary>Clears pending overrides (call after successful save).</summary>
        public void ClearOverrides() => _pendingOverrides.Clear();

        /// <summary>Clears all state (call when navigating away from a zoomed cluster).</summary>
        public void ClearAll()
        {
            _pendingOverrides.Clear();
            _endpoints.Clear();
        }
    }
}
