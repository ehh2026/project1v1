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
        /// When true, loads the _lit variant of each shaft image (e.g. pin_01_shaft_lit.png).
        /// </summary>
        public bool UseLitShafts { get; set; } = false;

        /// <summary>
        /// Target head radius in screen pixels. Each head image is scaled so its
        /// local_radius maps to exactly this many screen pixels, giving all heads a
        /// consistent size regardless of their native image dimensions.
        /// Set to 0 to fall back to proportional-shaft scaling.
        /// </summary>
        public double TargetHeadRadiusPx { get; set; } = 14.0;
    }
}
