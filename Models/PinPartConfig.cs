namespace InteractiveWorldMap.Models
{
    public enum PinPartSelectionMode
    {
        NearestFit,
        ExactFit
    }

    /// <summary>
    /// Configuration for part-based composite pin rendering.
    /// Paths are relative to the Images&Content folder unless absolute.
    /// </summary>
    public class PinPartConfig
    {
        public bool Enabled { get; set; } = false;
        public string PartsFolderPath { get; set; } = "Pins_v2/parts";
        public string GeometryMetadataPath { get; set; } = "Pins_v2/parts/pin_part_geometry.json";
        public PinPartSelectionMode SelectionMode { get; set; } = PinPartSelectionMode.NearestFit;
        public double MaxResidualRotationDeg { get; set; } = 20.0;
        public double MinStretchFactor { get; set; } = 0.75;
        public double MaxStretchFactor { get; set; } = 1.35;
        public bool UseCompositeRendering { get; set; } = false;

        /// <summary>
        /// When true, flattens each composite pin's transformed shaft/head layers to a
        /// single bitmap inside CompositePinMarker. Default is false so the live
        /// layered renderer remains the baseline unless visual review needs this.
        /// </summary>
        public bool UsePrerasterizedRendering { get; set; } = false;

        /// <summary>
        /// When true, loads the _lit variant of each shaft image (e.g. pin_01_shaft_lit.png).
        /// </summary>
        public bool UseLitShafts { get; set; } = false;

        /// <summary>
        /// Optional shaft-only asset variant folder under PartsFolderPath/shaft_variants.
        /// When empty, the renderer uses the existing UseLitShafts/base shaft filename behavior.
        /// Example: "outline_dark" resolves pin_01_shaft.png to
        /// Pins_v2/parts/shaft_variants/outline_dark/pin_01_shaft.png.
        /// </summary>
        public string ShaftAssetVariant { get; set; } = string.Empty;

        /// <summary>
        /// Optional head-only asset variant folder under PartsFolderPath/head_variants.
        /// When empty, the renderer uses the base head filename from the part geometry.
        /// Example: "outline_black_6px" resolves pin_01_head.png to
        /// Pins_v2/parts/head_variants/outline_black_6px/pin_01_head.png.
        /// </summary>
        public string HeadAssetVariant { get; set; } = string.Empty;

        /// <summary>
        /// Target head radius in screen pixels. Each head image is scaled so its
        /// local_radius maps to exactly this many screen pixels, giving all heads a
        /// consistent size regardless of their native image dimensions.
        /// Set to 0 to fall back to proportional-shaft scaling.
        /// </summary>
        public double TargetHeadRadiusPx { get; set; } = 14.0;

        /// <summary>
        /// Target shaft half-width in screen pixels. Each shaft image is scaled in the
        /// normal (cross-section) direction so its native_shaft_half_width_px maps to
        /// exactly this many screen pixels, keeping shaft thickness constant regardless
        /// of pin length. Set to 0 to fall back to proportional scaling (normalScale = S).
        /// </summary>
        public double TargetShaftHalfWidthPx { get; set; } = 0.0;

        /// <summary>
        /// Default stub shaft length in screen pixels for non-extended individual markers
        /// (Option A policy). Stub runs from the map anchor upward in screen space.
        /// Set to 0 for head-only placement.
        /// </summary>
        public double DefaultStubLengthPixels { get; set; } = 24.0;
    }
}
