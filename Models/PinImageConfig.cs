using System.Collections.Generic;
using System.Windows;

namespace InteractiveWorldMap.Models
{
    /// <summary>
    /// Configuration for individual pin images within the master pin file.
    /// </summary>
    public class PinImageInfo
    {
        /// <summary>
        /// Unique identifier for this pin variant.
        /// </summary>
        public string Id { get; set; } = string.Empty;

        /// <summary>
        /// X coordinate of the top-left corner of this pin in the master image.
        /// </summary>
        public int X { get; set; }

        /// <summary>
        /// Y coordinate of the top-left corner of this pin in the master image.
        /// </summary>
        public int Y { get; set; }

        /// <summary>
        /// Width of this pin in pixels.
        /// </summary>
        public int Width { get; set; }

        /// <summary>
        /// Height of this pin in pixels.
        /// </summary>
        public int Height { get; set; }

        /// <summary>
        /// Optional description of this pin (e.g., "red pin angled left").
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// Gets the rectangle bounds for this pin.
        /// </summary>
        public Rect Bounds => new Rect(X, Y, Width, Height);
    }

    /// <summary>
    /// Configuration for image-based pin markers.
    /// </summary>
    public class PinImageConfig
    {
        /// <summary>
        /// Path to the master pin image file (relative to Images&Content folder).
        /// </summary>
        public string MasterImagePath { get; set; } = "pins.jpg";

        /// <summary>
        /// List of individual pin definitions within the master image.
        /// </summary>
        public List<PinImageInfo> Pins { get; set; } = new List<PinImageInfo>();

        /// <summary>
        /// Whether to randomly select pins for variety.
        /// </summary>
        public bool UseRandomSelection { get; set; } = true;

        /// <summary>
        /// Scale factor to apply to all pins (1.0 = original size).
        /// </summary>
        public double ScaleFactor { get; set; } = 1.0;

        /// <summary>
        /// Whether to enable image-based pins (false = use drawn pins).
        /// </summary>
        public bool Enabled { get; set; } = true;
    }
}