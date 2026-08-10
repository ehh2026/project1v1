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

        /// <summary>
        /// Surface content-image diagnostics: the on-screen "large image" notice and the heavy-file /
        /// downscale log warnings. Off by default so gallery visitors never see them and logs stay quiet;
        /// image downscaling itself always runs regardless of this flag.
        /// </summary>
        public bool LogContentImageDiagnostics { get; set; } = false;

        /// <summary>
        /// Shows the developer-only runtime tuning panel and F12 toggle.
        /// </summary>
        public bool EnableTuningPanel { get; set; } = false;

        /// <summary>
        /// Launch in a normal resizable window instead of borderless maximized kiosk mode.
        /// Useful for debugging at a known viewport size.
        /// </summary>
        public bool WindowedMode { get; set; } = false;

        /// <summary>Width of the debug window when WindowedMode is true.</summary>
        public int WindowedWidth { get; set; } = 1280;

        /// <summary>Height of the debug window when WindowedMode is true.</summary>
        public int WindowedHeight { get; set; } = 800;
    }
}
