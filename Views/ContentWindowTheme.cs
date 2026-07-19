using System;
using System.Windows.Media;

namespace InteractiveWorldMap.Views
{
    /// <summary>
    /// Turns <see cref="Models.ContentWindowConfig"/> color/opacity strings into WPF brushes for
    /// the content popup windows. Mirrors the color-parsing idiom used by the pin renderers
    /// (see <see cref="ExtensionLineRenderer"/> and <see cref="PinHead"/>).
    /// </summary>
    public static class ContentWindowTheme
    {
        /// <summary>
        /// Parses an ARGB/RGB hex color, returning <paramref name="fallback"/> if it cannot be read.
        /// </summary>
        public static Color ParseColor(string? value, Color fallback)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return fallback;
            }

            try
            {
                return ColorConverter.ConvertFromString(value) is Color parsed ? parsed : fallback;
            }
            catch (FormatException)
            {
                // User-edited config may contain a malformed color; fall back rather than crash.
                return fallback;
            }
        }

        /// <summary>
        /// Builds a frozen solid brush from an ARGB/RGB hex color (alpha taken from the string).
        /// </summary>
        public static SolidColorBrush ToBrush(string? argbHex, Color fallback)
        {
            var brush = new SolidColorBrush(ParseColor(argbHex, fallback));
            brush.Freeze();
            return brush;
        }

        /// <summary>
        /// Builds a frozen solid brush from an RGB hex color plus a separate opacity (0..1),
        /// which overrides any alpha channel present in the color string.
        /// </summary>
        public static SolidColorBrush ToBrush(string? rgbHex, double opacity, Color fallback)
        {
            var color = ParseColor(rgbHex, fallback);
            var alpha = (byte)Math.Round(Math.Clamp(opacity, 0.0, 1.0) * 255.0);
            var brush = new SolidColorBrush(Color.FromArgb(alpha, color.R, color.G, color.B));
            brush.Freeze();
            return brush;
        }
    }
}
