namespace InteractiveWorldMap.Models
{
    /// <summary>
    /// Appearance settings shared by the content popup windows (image/text subwindow, didactic
    /// text window, and thumbnail browser). Colors use RGB hex with a separate opacity so a future
    /// tuning-panel slider can adjust translucency independently of hue.
    /// Defaults mirror the values previously hard-coded in the popup XAML.
    /// </summary>
    public class ContentWindowConfig
    {
        /// <summary>
        /// Font family applied to all popup text. Per-role font sizes remain independent.
        /// </summary>
        public string FontFamily { get; set; } = "Segoe UI";

        /// <summary>
        /// Style of the popup body panel (background, border, and text).
        /// </summary>
        public PopupStyle Popup { get; set; } = new PopupStyle();

        /// <summary>
        /// Style of the optional image caption pane at the bottom of the content subwindow.
        /// </summary>
        public CaptionStyle Caption { get; set; } = new CaptionStyle();
    }

    /// <summary>
    /// Background, border, and text styling for the popup body panel.
    /// </summary>
    public class PopupStyle
    {
        /// <summary>Body background color in RGB hex (opacity applied separately).</summary>
        public string BackgroundColor { get; set; } = "#1E1E1E";

        /// <summary>Body background opacity from 0.0 through 1.0.</summary>
        public double BackgroundOpacity { get; set; } = 0.70;

        /// <summary>Border color in ARGB hex.</summary>
        public string BorderColor { get; set; } = "#FFFFFFFF";

        /// <summary>Border thickness in pixels.</summary>
        public double BorderThickness { get; set; } = 2.0;

        /// <summary>Corner radius in pixels.</summary>
        public double CornerRadius { get; set; } = 12.0;

        /// <summary>Foreground color for popup text in ARGB hex.</summary>
        public string TextColor { get; set; } = "#FFFFFFFF";

        /// <summary>Font size for popup headings/titles.</summary>
        public double HeadingFontSize { get; set; } = 18.0;

        /// <summary>Font size for popup body text.</summary>
        public double BodyFontSize { get; set; } = 14.0;
    }

    /// <summary>
    /// Background, border, and text styling for the image caption pane.
    /// </summary>
    public class CaptionStyle
    {
        /// <summary>Caption background color in RGB hex (opacity applied separately).</summary>
        public string BackgroundColor { get; set; } = "#000000";

        /// <summary>Caption background opacity from 0.0 through 1.0.</summary>
        public double BackgroundOpacity { get; set; } = 0.85;

        /// <summary>Color of the top divider border in ARGB hex.</summary>
        public string TopBorderColor { get; set; } = "#66FFFFFF";

        /// <summary>Foreground color for caption text in ARGB hex.</summary>
        public string TextColor { get; set; } = "#FFFFFFFF";

        /// <summary>Font size for caption text.</summary>
        public double FontSize { get; set; } = 13.0;
    }
}
