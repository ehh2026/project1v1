namespace InteractiveWorldMap.Models
{
    public class TuningPanelEventArgs : System.EventArgs
    {
        public bool PinPartsEnabled { get; set; }
        public bool UseComposite { get; set; }
        public bool UsePrerasterize { get; set; }
        public bool ShowDebugOverlay { get; set; }
        public bool UseLitShafts { get; set; }
        public bool AutoOpenSingleLocationContentAfterZoom { get; set; }
        public string ShaftVariant { get; set; } = string.Empty;
        public string HeadVariant { get; set; } = string.Empty;
        public double ClusterThreshold { get; set; }
        public double StubLength { get; set; }
        public double TargetHeadRadiusPx { get; set; }
        public double TargetShaftHalfWidthPx { get; set; }
        public double LocationMarkerSize { get; set; }
        public double ClusterMarkerSize { get; set; }

        // Drawn-pin tip cap (PinMarkers.DrawnPinTipCap). Style None disables the cap.
        public DrawnPinTipCapStyle TipCapStyle { get; set; } = DrawnPinTipCapStyle.None;
        /// <summary>Total screen-space width of the divot line.</summary>
        public double TipCapWidthPx { get; set; }
        /// <summary>Screen-space thickness of the divot line.</summary>
        public double TipCapLineWeightPx { get; set; }
        /// <summary>Concave arc depth (px) — the curvature knob.</summary>
        public double TipCapArcDepthPx { get; set; }
    }
}
