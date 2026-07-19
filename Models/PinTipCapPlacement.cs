using System.Windows;

namespace InteractiveWorldMap.Models
{
    /// <summary>
    /// Which shaft a tip cap sits on. The cap always follows the <em>visible</em> shaft.
    /// </summary>
    public enum PinTipCapShaftKind
    {
        /// <summary>Built-in vertical pin shaft (auto-stub). Tip = screen projection of the shaft tip.</summary>
        BuiltIn,

        /// <summary>Radial / manual-layout / drag extension line. Tip = line start (map anchor).</summary>
        ExtensionLine
    }

    /// <summary>
    /// Resolved per-marker placement data for one drawn-pin tip cap. Computed by
    /// <c>MainWindow</c> and handed to <c>DrawnPinTipCapRenderer</c> — the renderer never
    /// reaches back into marker visuals or the extension-line renderer.
    /// </summary>
    /// <param name="LocationName">Owning location name (diagnostics / pooling identity).</param>
    /// <param name="TipScreen">Screen-space tip point the cap is centered on.</param>
    /// <param name="ShaftDir">Unit vector from the tip toward the head (the cap bows along this for concave).</param>
    /// <param name="OutlineWidthPx">Outer (outline-inclusive) width of the visible shaft at the tip.</param>
    /// <param name="ShaftKind">Whether the cap sits on the built-in stub or an extension line.</param>
    public readonly record struct PinTipCapPlacement(
        string LocationName,
        Point TipScreen,
        Vector ShaftDir,
        double OutlineWidthPx,
        PinTipCapShaftKind ShaftKind);
}
