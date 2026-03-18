namespace InteractiveWorldMap.Models
{
    /// <summary>
    /// Configuration for radial extension lines that spread dense markers.
    /// </summary>
    public class RadialExtensionConfig
    {
        /// <summary>
        /// Master toggle for radial extension feature.
        /// </summary>
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// Minimum number of locations within proximity threshold to trigger extension.
        /// </summary>
        public int MinLocationsForExtension { get; set; } = 3;

        /// <summary>
        /// Distance threshold in screen pixels to consider locations densely packed.
        /// </summary>
        public double ProximityThresholdPixels { get; set; } = 10.0;

        /// <summary>
        /// Length of extension lines in screen pixels.
        /// </summary>
        public double ExtensionLineLength { get; set; } = 40.0;

        /// <summary>
        /// Preferred minimum degrees between adjacent extension lines.
        /// This is a target value; actual separation may be smaller for large groups.
        /// </summary>
        public double MinimumAngleSeparation { get; set; } = 15.0;

        /// <summary>
        /// Absolute minimum degrees between adjacent extension lines.
        /// Below this value, visual overlap occurs.
        /// </summary>
        public double HardMinimumAngleSeparation { get; set; } = 5.0;

        /// <summary>
        /// Color of extension lines in ARGB hex format.
        /// </summary>
        public string LineColor { get; set; } = "#80808080";

        /// <summary>
        /// Thickness of extension lines in pixels.
        /// </summary>
        public double LineThickness { get; set; } = 1.5;

        /// <summary>
        /// Whether to animate lines extending outward.
        /// </summary>
        public bool AnimateExtension { get; set; } = true;

        /// <summary>
        /// Duration of extension animation in milliseconds.
        /// </summary>
        public int ExtensionAnimationMs { get; set; } = 250;

        /// <summary>
        /// Minimum zoom level to enable radial extensions.
        /// Below this zoom level, extensions are not shown.
        /// </summary>
        public double ZoomThresholdForExtensions { get; set; } = 10.0;
    }
}
