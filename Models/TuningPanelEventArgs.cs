namespace InteractiveWorldMap.Models
{
    public class TuningPanelEventArgs : System.EventArgs
    {
        public bool PinPartsEnabled { get; set; }
        public bool UseComposite { get; set; }
        public bool UsePrerasterize { get; set; }
        public bool ShowDebugOverlay { get; set; }
        public bool UseLitShafts { get; set; }
        public string ShaftVariant { get; set; } = string.Empty;
        public string HeadVariant { get; set; } = string.Empty;
        public double ClusterThreshold { get; set; }
        public double StubLength { get; set; }
        public double TargetHeadRadiusPx { get; set; }
        public double TargetShaftHalfWidthPx { get; set; }
        public double LocationMarkerSize { get; set; }
        public double ClusterMarkerSize { get; set; }
    }
}
