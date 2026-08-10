using System;

namespace InteractiveWorldMap.Utilities
{
    /// <summary>
    /// Pure helpers for deciding how large content images should be decoded. Kept free of any WPF
    /// imaging types so the downscale policy can be unit-tested without decoding real bitmaps.
    /// </summary>
    public static class ImageDecodeMath
    {
        /// <summary>
        /// Returns the value to assign to <c>BitmapImage.DecodePixelWidth</c> so an image of native
        /// size <paramref name="sourceWidth"/>x<paramref name="sourceHeight"/> is downscaled to fit
        /// within the <paramref name="maxWidth"/>x<paramref name="maxHeight"/> box while preserving
        /// aspect ratio. WPF scales height proportionally when only the width is set, so bounding the
        /// width by the more constraining dimension bounds both.
        /// <para>
        /// Returns <c>0</c> ("leave native — do not override") when there is no cap, the source size is
        /// unknown, or the image already fits inside the box. Images are only ever downscaled, never
        /// upscaled.
        /// </para>
        /// </summary>
        public static int ComputeDecodePixelWidth(int sourceWidth, int sourceHeight, int maxWidth, int maxHeight)
        {
            if (sourceWidth <= 0 || sourceHeight <= 0)
                return 0;

            // A non-positive cap on a dimension means "unbounded" there.
            var widthScale = maxWidth > 0 ? (double)maxWidth / sourceWidth : double.PositiveInfinity;
            var heightScale = maxHeight > 0 ? (double)maxHeight / sourceHeight : double.PositiveInfinity;
            var scale = Math.Min(widthScale, heightScale);

            // No cap at all, or the image already fits: decode at native resolution.
            if (double.IsInfinity(scale) || scale >= 1.0)
                return 0;

            // Round to at least 1px so a valid DecodePixelWidth is always produced.
            return Math.Max(1, (int)Math.Round(sourceWidth * scale, MidpointRounding.AwayFromZero));
        }
    }
}
