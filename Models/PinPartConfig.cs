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
    }
}
