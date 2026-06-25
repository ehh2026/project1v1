namespace InteractiveWorldMap.Models
{
    /// <summary>
    /// Configuration for visual appearance of markers and clusters.
    /// </summary>
    public class VisualConfig : IMarkerConfiguration
    {
        /// <summary>
        /// Distance threshold in pixels for clustering locations together.
        /// </summary>
        public double ClusterDistanceThreshold { get; set; } = 300.0;

        /// <summary>
        /// Configuration for radial extension lines.
        /// </summary>
        public RadialExtensionConfig RadialExtension { get; set; } = new RadialExtensionConfig();

        /// <summary>
        /// Configuration for manual layout editor.
        /// </summary>
        public ManualLayoutEditorConfig ManualLayoutEditor { get; set; } = new ManualLayoutEditorConfig();

        /// <summary>
        /// Configuration for debug logging.
        /// </summary>
        public DebugConfig Debug { get; set; } = new DebugConfig();

        /// <summary>
        /// Size of individual location markers in pixels.
        /// </summary>
        public double LocationMarkerSize { get; set; } = 16.0;

        /// <summary>
        /// Whether to use pin-style markers instead of circular markers.
        /// </summary>
        public bool UsePinMarkers { get; set; } = true;

        /// <summary>
        /// Whether clicking a standalone full-map pin should open its content automatically after zooming in.
        /// </summary>
        public bool AutoOpenSingleLocationContentAfterZoom { get; set; } = false;

        /// <summary>
        /// Configuration for pin marker appearance.
        /// </summary>
        public PinMarkerConfig PinMarkers { get; set; } = new PinMarkerConfig();

        /// <summary>
        /// Configuration for part-based composite pins.
        /// </summary>
        public PinPartConfig PinParts { get; set; } = new PinPartConfig();

        /// <summary>
        /// Size of cluster markers in pixels.
        /// </summary>
        public double ClusterMarkerSize { get; set; } = 40.0;

        /// <summary>
        /// Size of the count badge on cluster markers in pixels.
        /// </summary>
        public double ClusterBadgeSize { get; set; } = 20.0;

        /// <summary>
        /// Font size for the count text on cluster markers.
        /// </summary>
        public double ClusterCountFontSize { get; set; } = 12.0;

        /// <summary>
        /// Zoom magnification level when zoomed in on a cluster.
        /// Higher values = more magnification (e.g., 30.0 = 30x zoom).
        /// </summary>
        public double ZoomScale { get; set; } = 30.0;

        /// <summary>
        /// Duration of zoom animation in milliseconds.
        /// </summary>
        public int AnimationDurationMs { get; set; } = 390;

    }
}

    /// <summary>
    /// Configuration for the manual layout editor feature.
    /// </summary>
    public class ManualLayoutEditorConfig
    {
        /// <summary>
        /// Whether the manual layout editor feature is enabled.
        /// </summary>
        public bool Enabled { get; set; } = false;

        /// <summary>
        /// Whether to show the "Edit Layout" button in the UI.
        /// </summary>
        public bool ShowEditButton { get; set; } = true;

        /// <summary>
        /// Path to the file where manual layouts are stored.
        /// </summary>
        public string LayoutStoragePath { get; set; } = "Images&Content/manual-layouts.json";

        /// <summary>
        /// Whether to enable snap-to-grid when dragging markers.
        /// </summary>
        public bool EnableSnapToGrid { get; set; } = false;

        /// <summary>
        /// Grid size in pixels for snap-to-grid feature.
        /// </summary>
        public double GridSize { get; set; } = 5.0;

        /// <summary>
        /// Whether to show an indicator when a manual layout is active.
        /// </summary>
        public bool ShowLayoutIndicator { get; set; } = true;
    }
