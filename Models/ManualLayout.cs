using System;
using System.Collections.Generic;

namespace InteractiveWorldMap.Models
{
    public enum ManualLayoutOrigin
    {
        Manual,
        AutoSeed,
        Imported
    }

    /// <summary>
    /// Represents a saved manual layout configuration
    /// </summary>
    public class ManualLayout
    {
        public string Key { get; set; } = string.Empty;
        public string GroupKey { get; set; } = string.Empty;
        public string VariantId { get; set; } = "manual-default";
        public string DisplayName { get; set; } = "Manual Layout";
        public ManualLayoutOrigin Origin { get; set; } = ManualLayoutOrigin.Manual;
        public bool IsDefault { get; set; } = true;
        public DateTime Timestamp { get; set; }
        public DateTime CreatedUtc { get; set; }
        public DateTime UpdatedUtc { get; set; }
        public string? BasedOnKey { get; set; }
        public string? BasedOnVariantId { get; set; }
        public string? GeneratorVersion { get; set; }
        public int LocationCount { get; set; }
        public List<ManualLayoutMarker> Markers { get; set; } = new List<ManualLayoutMarker>();

        public ManualLayout() 
        {
            Timestamp = DateTime.UtcNow;
            CreatedUtc = Timestamp;
            UpdatedUtc = Timestamp;
        }

        public ManualLayout(string key, List<ManualLayoutMarker> markers)
        {
            Key = key;
            GroupKey = key;
            Markers = markers;
            LocationCount = markers.Count;
            Timestamp = DateTime.UtcNow;
            CreatedUtc = Timestamp;
            UpdatedUtc = Timestamp;
        }
    }

    /// <summary>
    /// Represents a logical layout group keyed by cluster/view identity with one or more variants.
    /// </summary>
    public class ManualLayoutGroup
    {
        public string GroupKey { get; set; } = string.Empty;
        public List<ManualLayout> Variants { get; set; } = new List<ManualLayout>();
    }

    /// <summary>
    /// Container for all saved layouts
    /// </summary>
    public class ManualLayoutCollection
    {
        public Dictionary<string, ManualLayout> Layouts { get; set; } = new Dictionary<string, ManualLayout>();
        public Dictionary<string, ManualLayoutGroup> LayoutGroups { get; set; } = new Dictionary<string, ManualLayoutGroup>();
    }
}
