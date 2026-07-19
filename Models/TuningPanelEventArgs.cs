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
        public ZoomedMapResamplingMode ZoomedMapResamplingMode { get; set; } =
            ZoomedMapResamplingMode.Fant;
        public string ShaftVariant { get; set; } = string.Empty;
        public string HeadVariant { get; set; } = string.Empty;
        public double ClusterThreshold { get; set; }
        public double StubLength { get; set; }
        public double TargetHeadRadiusPx { get; set; }
        public double TargetShaftHalfWidthPx { get; set; }
        public double LocationMarkerSize { get; set; }
        public double ClusterMarkerSize { get; set; }
        public double ClusterBadgeSize { get; set; }
        public double ClusterCountFontSize { get; set; }
        public double ZoomScale { get; set; }
        public int AnimationDurationMs { get; set; }
        public bool PinShadowEnabled { get; set; }
        public double PinShadowOpacity { get; set; }
        public bool ClusterShadowEnabled { get; set; }
        public double ClusterShadowOpacity { get; set; }
        public double DrawnHeadDiameterPx { get; set; }
        public double DrawnShaftWidthPx { get; set; }
        public double DrawnShaftLengthPx { get; set; }
        public double PinHitDiameterPx { get; set; }
        public double ClusterHitDiameterPx { get; set; }

        // Drawn-pin tip cap (PinMarkers.DrawnPinTipCap). Style None disables the cap.
        public DrawnPinTipCapStyle TipCapStyle { get; set; } = DrawnPinTipCapStyle.None;
        public DrawnPinTipCapAlignment TipCapAlignment { get; set; } =
            DrawnPinTipCapAlignment.ScreenHorizontal;
        /// <summary>Total screen-space width of the divot line.</summary>
        public double TipCapWidthPx { get; set; }
        /// <summary>Screen-space thickness of the divot line.</summary>
        public double TipCapLineWeightPx { get; set; }
        /// <summary>Concave arc depth (px) — the curvature knob.</summary>
        public double TipCapArcDepthPx { get; set; }

        // Content popup/caption styling (VisualConfig.ContentWindows). Colors are ARGB/RGB hex
        // strings; opacities are 0..1; sizes/thickness/radius are pixels.
        public string ContentFontFamily { get; set; } = "Segoe UI";
        public string PopupBackgroundColor { get; set; } = "#1E1E1E";
        public double PopupBackgroundOpacity { get; set; }
        public string PopupBorderColor { get; set; } = "#FFFFFFFF";
        public double PopupBorderThickness { get; set; }
        public double PopupCornerRadius { get; set; }
        public string PopupTextColor { get; set; } = "#FFFFFFFF";
        public double PopupHeadingFontSize { get; set; }
        public double PopupBodyFontSize { get; set; }
        public string CaptionBackgroundColor { get; set; } = "#000000";
        public double CaptionBackgroundOpacity { get; set; }
        public string CaptionTopBorderColor { get; set; } = "#66FFFFFF";
        public string CaptionTextColor { get; set; } = "#FFFFFFFF";
        public double CaptionFontSize { get; set; }
    }
}
