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
        public string ShaftColor { get; set; } = "#FFB0B0B0";

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
    }
}