namespace InteractiveWorldMap.Models
{
    /// <summary>
    /// Configuration for pin-style markers that look like sewing pins.
    /// </summary>
    public class PinMarkerConfig
    {
        /// <summary>
        /// Size of the pin ball (colored sphere) in pixels.
        /// </summary>
        public double BallSize { get; set; } = 8.0;

        /// <summary>
        /// Width of the pin shaft (metal part) in pixels.
        /// </summary>
        public double ShaftWidth { get; set; } = 1.5;

        /// <summary>
        /// Length of the pin shaft in pixels.
        /// </summary>
        public double ShaftLength { get; set; } = 20.0;

        /// <summary>
        /// Color of the pin shaft in ARGB hex format.
        /// </summary>
        public string ShaftColor { get; set; } = "#FFD8D8D8";

        /// <summary>
        /// Outline color around the pin shaft (ARGB hex). Drawn as a wider rect behind the core.
        /// </summary>
        public string ShaftOutlineColor { get; set; } = "#FF1A1A1A";

        /// <summary>
        /// Extra pixels added on each side of the shaft for the dark outline halo.
        /// </summary>
        public double ShaftOutlineThickness { get; set; } = 1.25;

        /// <summary>
        /// Outline color around the pin ball (ARGB hex).
        /// </summary>
        public string BallOutlineColor { get; set; } = "#FF000000";

        /// <summary>
        /// Stroke thickness of the pin ball outline in pixels.
        /// </summary>
        public double BallOutlineThickness { get; set; } = 1.5;

        /// <summary>
        /// Whether to use random colors for pin balls.
        /// </summary>
        public bool UseRandomColors { get; set; } = true;

        /// <summary>
        /// Default pin ball color when not using random colors (ARGB hex format).
        /// </summary>
        public string DefaultBallColor { get; set; } = "#FFE53935";

        /// <summary>
        /// Whether to show drop shadow on pin balls.
        /// </summary>
        public bool ShowShadow { get; set; } = true;

        /// <summary>
        /// Shadow opacity (0.0 to 1.0).
        /// </summary>
        public double ShadowOpacity { get; set; } = 0.4;

        /// <summary>
        /// Whether pins should be manually editable (draggable).
        /// </summary>
        public bool EnableManualEditing { get; set; } = true;

        /// <summary>
        /// Opt-in cap drawn at the visible shaft terminus so the tip reads as the pin
        /// being stuck into the map surface. Defaults to <see cref="DrawnPinTipCapStyle.None"/>.
        /// </summary>
        public DrawnPinTipCapConfig DrawnPinTipCap { get; set; } = new DrawnPinTipCapConfig();
    }
}
