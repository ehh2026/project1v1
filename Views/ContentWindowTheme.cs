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
            return TryParseColor(value, out var parsed) ? parsed : fallback;
        }

        /// <summary>
        /// Attempts to parse an ARGB/RGB hex color. Returns false for null/blank/malformed input
        /// (used to validate user-entered or config-supplied colors without throwing).
        /// </summary>
        public static bool TryParseColor(string? value, out Color color)
        {
            color = default;
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            try
            {
                if (ColorConverter.ConvertFromString(value) is Color parsed)
                {
                    color = parsed;
                    return true;
                }
            }
            catch (FormatException)
            {
                // Malformed color string; treat as unparseable rather than crash.
            }

            return false;
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
