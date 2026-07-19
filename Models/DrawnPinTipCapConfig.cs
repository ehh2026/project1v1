using System;
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
        /// A shallow concave arc that bows <em>away from</em> the pin head, so the
        /// shaft appears to enter a divot in the map surface.
        /// </summary>
        Concave
    }

    [JsonConverter(typeof(StringEnumConverter))]
    public enum DrawnPinTipCapAlignment
    {
        ScreenHorizontal,
        ShaftAligned
    }

    /// <summary>
    /// Configuration for the opt-in drawn-pin tip cap. A single instance drives caps on
    /// both built-in auto-stub tips and extension-line tips.
    /// </summary>
    public class DrawnPinTipCapConfig
    {
        /// <summary>Cap shape. <see cref="DrawnPinTipCapStyle.None"/> draws nothing.</summary>
        public DrawnPinTipCapStyle Style { get; set; } = DrawnPinTipCapStyle.None;

        /// <summary>How the cap width axis is oriented in screen space.</summary>
        public DrawnPinTipCapAlignment Alignment { get; set; } =
            DrawnPinTipCapAlignment.ScreenHorizontal;

        /// <summary>Total screen-space width of the cap stroke.</summary>
        public double? WidthPx { get; set; }

        /// <summary>Screen-space thickness of the cap stroke.</summary>
        public double? LineWeightPx { get; set; }

        /// <summary>
        /// Vertical distance (in screen px) from the concave center to its endpoints.
        /// </summary>
        public double ArcDepthPx { get; set; } = 3.0;

        /// <summary>Cap stroke color (ARGB hex).</summary>
        public string? Color { get; set; } = "#FF111111";

        // Legacy filled-cap settings remain readable so older config files migrate
        // deterministically, but ShouldSerialize methods keep them out of future saves.
        public double HeightPx { get; set; } = 6.0;
        public double ExtendPx { get; set; } = 0.0;
        public bool UseOutlineRing { get; set; } = true;

        public bool ShouldSerializeHeightPx() => false;
        public bool ShouldSerializeExtendPx() => false;
        public bool ShouldSerializeUseOutlineRing() => false;

        public double ResolveWidthPx(double outlineWidthPx)
        {
            double legacyWidth = Math.Max(outlineWidthPx, 0.0) +
                                 (2.0 * Math.Max(ExtendPx, 0.0));
            return Math.Max(WidthPx ?? legacyWidth, 0.0);
        }

        public double ResolveLineWeightPx(double shaftOutlineThicknessPx)
        {
            return Math.Max(LineWeightPx ?? Math.Max(shaftOutlineThicknessPx, 1.0), 0.0);
        }
    }
}
