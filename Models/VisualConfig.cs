using System;
using System.IO;
using Newtonsoft.Json;

namespace InteractiveWorldMap.Models
{
    /// <summary>
    /// Configuration for visual appearance of markers and clusters.
    /// </summary>
    public class VisualConfig
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
        /// Configuration for pin marker appearance.
        /// </summary>
        public PinMarkerConfig PinMarkers { get; set; } = new PinMarkerConfig();

        /// <summary>
        /// Configuration for image-based pin markers.
        /// </summary>
        public PinImageConfig PinImages { get; set; } = new PinImageConfig();

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

        /// <summary>
        /// Loads configuration from a JSON file.
        /// </summary>
        public static VisualConfig Load(string filePath)
        {
            try
            {
                if (File.Exists(filePath))
                {
                    string json = File.ReadAllText(filePath);
                    Console.WriteLine($"VisualConfig.Load: Reading from {filePath}");
                    Console.WriteLine($"VisualConfig.Load: JSON content: {json}");
                    System.Diagnostics.Debug.WriteLine($"VisualConfig.Load: Reading from {filePath}");
                    System.Diagnostics.Debug.WriteLine($"VisualConfig.Load: JSON content: {json}");
                    
                    var config = JsonConvert.DeserializeObject<VisualConfig>(json);
                    if (config != null)
                    {
                        Console.WriteLine($"VisualConfig.Load: Successfully deserialized - LocationMarkerSize={config.LocationMarkerSize}, ClusterMarkerSize={config.ClusterMarkerSize}");
                        System.Diagnostics.Debug.WriteLine($"VisualConfig.Load: Successfully deserialized config");
                        return config;
                    }
                    else
                    {
                        Console.WriteLine($"VisualConfig.Load: Deserialization returned null, using defaults");
                        System.Diagnostics.Debug.WriteLine($"VisualConfig.Load: Deserialization returned null, using defaults");
                    }
                }
                else
                {
                    Console.WriteLine($"VisualConfig.Load: File not found at {filePath}, using defaults");
                    System.Diagnostics.Debug.WriteLine($"VisualConfig.Load: File not found at {filePath}, using defaults");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading visual config: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"VisualConfig.Load: Exception - {ex.Message}");
            }

            // Return default config if file doesn't exist or error occurs
            Console.WriteLine($"VisualConfig.Load: Returning default config");
            System.Diagnostics.Debug.WriteLine($"VisualConfig.Load: Returning default config");
            return new VisualConfig();
        }

        /// <summary>
        /// Saves configuration to a JSON file.
        /// </summary>
        public void Save(string filePath)
        {
            try
            {
                string json = JsonConvert.SerializeObject(this, Formatting.Indented);
                File.WriteAllText(filePath, json);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving visual config: {ex.Message}");
            }
        }

        /// <summary>
        /// Creates a default configuration file if it doesn't exist.
        /// </summary>
        public static void EnsureConfigExists(string filePath)
        {
            if (!File.Exists(filePath))
            {
                var defaultConfig = new VisualConfig();
                defaultConfig.Save(filePath);
            }
        }
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
