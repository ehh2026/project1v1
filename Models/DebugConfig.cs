namespace InteractiveWorldMap.Models
{
    /// <summary>
    /// Configuration for debug logging flags.
    /// </summary>
    public class DebugConfig
    {
        /// <summary>
        /// Log radial extension calculation details (group detection, extension creation).
        /// </summary>
        public bool LogRadialExtensionCalculation { get; set; } = false;

        /// <summary>
        /// Log angle calculations and adjustments for radial extensions.
        /// </summary>
        public bool LogRadialExtensionAngles { get; set; } = false;

        /// <summary>
        /// Log overlap detection and resolution for radial extensions.
        /// </summary>
        public bool LogRadialExtensionOverlaps { get; set; } = false;

        /// <summary>
        /// Log marker positioning and viewport calculations.
        /// </summary>
        public bool LogMarkerPositioning { get; set; } = false;

        /// <summary>
        /// Log animation frame rendering details.
        /// </summary>
        public bool LogAnimationFrames { get; set; } = false;

        /// <summary>
        /// Show composite-pin anchor and stretch overlays for visual validation.
        /// </summary>
        public bool ShowCompositePinDebugOverlay { get; set; } = false;
    }
}
