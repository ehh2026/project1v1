using System;
using System.Collections.Generic;

namespace InteractiveWorldMap.Models
{
    /// <summary>
    /// Represents a saved manual layout configuration
    /// </summary>
    public class ManualLayout
    {
        public string Key { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public int LocationCount { get; set; }
        public List<ManualLayoutMarker> Markers { get; set; } = new List<ManualLayoutMarker>();

        public ManualLayout() 
        {
            Timestamp = DateTime.UtcNow;
        }

        public ManualLayout(string key, List<ManualLayoutMarker> markers)
        {
            Key = key;
            Markers = markers;
            LocationCount = markers.Count;
            Timestamp = DateTime.UtcNow;
        }
    }

    /// <summary>
    /// Container for all saved layouts
    /// </summary>
    public class ManualLayoutCollection
    {
        public Dictionary<string, ManualLayout> Layouts { get; set; } = new Dictionary<string, ManualLayout>();
    }
}
