using System;
using System.Windows.Media.Imaging;

namespace InteractiveWorldMap.Models
{
    /// <summary>
    /// Single source of truth for map image dimensions used by placement, navigation,
    /// and coordinate validation.
    /// </summary>
    /// <remarks>
    /// Excel location coordinates and marker placement use <see cref="DisplayWidth"/> /
    /// <see cref="DisplayHeight"/> (half-resolution display map space). Optional full-res
    /// fields document the high-quality crop source; <c>ZoomedRegionCache</c> still measures
    /// bitmaps at runtime for crop scale.
    /// </remarks>
    public sealed class MapMetadata
    {
        /// <summary>
        /// Default display width matching <c>World Map Extra Large.jpg</c> (documented asset default).
        /// </summary>
        public const double DefaultDisplayWidth = 8198;

        /// <summary>
        /// Default display height matching <c>World Map Extra Large.jpg</c> (documented asset default).
        /// </summary>
        public const double DefaultDisplayHeight = 5542;

        /// <summary>
        /// Default full-resolution width matching <c>World Map 1976.jpg</c> (documented asset default).
        /// </summary>
        public const double DefaultFullResWidth = 16397;

        /// <summary>
        /// Default full-resolution height matching <c>World Map 1976.jpg</c> (documented asset default).
        /// </summary>
        public const double DefaultFullResHeight = 11085;

        /// <summary>
        /// Display / placement map width in pixels (Excel half-size coordinate space).
        /// </summary>
        public double DisplayWidth { get; }

        /// <summary>
        /// Display / placement map height in pixels (Excel half-size coordinate space).
        /// </summary>
        public double DisplayHeight { get; }

        /// <summary>
        /// Optional full-resolution map width (crop source documentation / sanity checks).
        /// </summary>
        public double? FullResWidth { get; }

        /// <summary>
        /// Optional full-resolution map height (crop source documentation / sanity checks).
        /// </summary>
        public double? FullResHeight { get; }

        /// <summary>
        /// Creates map metadata with the given display size and optional full-res ceilings.
        /// </summary>
        /// <param name="displayWidth">Display map width in pixels; must be positive.</param>
        /// <param name="displayHeight">Display map height in pixels; must be positive.</param>
        /// <param name="fullResWidth">Optional full-res width.</param>
        /// <param name="fullResHeight">Optional full-res height.</param>
        public MapMetadata(
            double displayWidth,
            double displayHeight,
            double? fullResWidth = null,
            double? fullResHeight = null)
        {
            if (displayWidth <= 0)
                throw new ArgumentOutOfRangeException(nameof(displayWidth), displayWidth, "Display width must be positive.");
            if (displayHeight <= 0)
                throw new ArgumentOutOfRangeException(nameof(displayHeight), displayHeight, "Display height must be positive.");
            if (fullResWidth is <= 0)
                throw new ArgumentOutOfRangeException(nameof(fullResWidth), fullResWidth, "Full-res width must be positive when set.");
            if (fullResHeight is <= 0)
                throw new ArgumentOutOfRangeException(nameof(fullResHeight), fullResHeight, "Full-res height must be positive when set.");

            DisplayWidth = displayWidth;
            DisplayHeight = displayHeight;
            FullResWidth = fullResWidth;
            FullResHeight = fullResHeight;
        }

        /// <summary>
        /// Returns metadata matching the checked-in display and full-res map assets.
        /// </summary>
        public static MapMetadata CreateDefault() =>
            new(
                DefaultDisplayWidth,
                DefaultDisplayHeight,
                DefaultFullResWidth,
                DefaultFullResHeight);

        /// <summary>
        /// Builds metadata from a loaded display bitmap's pixel size, preserving full-res
        /// values from <paramref name="defaults"/> (or <see cref="CreateDefault"/>).
        /// Falls back to <paramref name="defaults"/> when the bitmap has non-positive size.
        /// </summary>
        /// <param name="bitmap">Loaded display map bitmap.</param>
        /// <param name="defaults">Fallback / full-res source; defaults to <see cref="CreateDefault"/>.</param>
        public static MapMetadata FromDisplayBitmap(BitmapSource bitmap, MapMetadata? defaults = null)
        {
            if (bitmap == null)
                throw new ArgumentNullException(nameof(bitmap));

            var baseline = defaults ?? CreateDefault();
            if (bitmap.PixelWidth <= 0 || bitmap.PixelHeight <= 0)
                return baseline;

            return new MapMetadata(
                bitmap.PixelWidth,
                bitmap.PixelHeight,
                baseline.FullResWidth,
                baseline.FullResHeight);
        }

        /// <summary>
        /// True when <paramref name="pixelX"/> / <paramref name="pixelY"/> lie in display space
        /// inclusive bounds used by startup coordinate warnings.
        /// </summary>
        public bool IsValidDisplayCoordinate(double pixelX, double pixelY) =>
            pixelX >= 0 && pixelX <= DisplayWidth &&
            pixelY >= 0 && pixelY <= DisplayHeight;
    }
}
