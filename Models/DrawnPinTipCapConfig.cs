using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace InteractiveWorldMap.Models
{
    /// <summary>
    /// Cap shape drawn at the visible terminus of a drawn pin shaft so the tip reads
    /// as the pin being stuck <em>into</em> the map surface rather than a flat cut end
    /// resting on top of it.
    /// </summary>
    [JsonConverter(typeof(StringEnumConverter))]
    public enum DrawnPinTipCapStyle
    {
        /// <summary>No cap is drawn (default).</summary>
        None,

        /// <summary>A firm horizontal bar through the tip, in screen space.</summary>
        Horizontal,

        /// <summary>
        /// A shallow concave arc that bows <em>toward</em> the shaft/head, so the map
        /// surface appears to pucker up around the pin's entry point.
        /// </summary>
        Concave
    }

    /// <summary>
    /// Configuration for the opt-in drawn-pin tip cap. A single instance drives caps on
    /// both built-in auto-stub tips and extension-line tips.
    /// </summary>
    public class DrawnPinTipCapConfig
    {
        /// <summary>Cap shape. <see cref="DrawnPinTipCapStyle.None"/> draws nothing.</summary>
        public DrawnPinTipCapStyle Style { get; set; } = DrawnPinTipCapStyle.None;

        /// <summary>Height (in screen px) of the horizontal bar, extending downward from the tip.</summary>
        public double HeightPx { get; set; } = 6.0;

        /// <summary>
        /// Concave arc depth (in screen px) offset along the shaft direction. Primary visual
        /// tuning knob; positive values bow the arc toward the shaft/head (the "stuck-in" read).
        /// </summary>
        public double ArcDepthPx { get; set; } = 3.0;

        /// <summary>Extra half-width (in screen px) added on each side beyond the shaft outline.</summary>
        public double ExtendPx { get; set; } = 0.0;

        /// <summary>Cap core color (ARGB hex). When null, the shaft color is used.</summary>
        public string? Color { get; set; } = null;

        /// <summary>When true, the cap pairs a dark outline ring behind the core to match the shaft outline.</summary>
        public bool UseOutlineRing { get; set; } = true;
    }
}
