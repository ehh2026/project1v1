namespace InteractiveWorldMap.Models
{
    /// <summary>
    /// Bounds how location content images are decoded. Very large images (e.g. high-megapixel TIFFs)
    /// are expensive to both decode and render, which can make the UI appear to hang. Images are
    /// downscaled at decode time to fit within a target box (aspect ratio preserved); the same event
    /// that reports a downscale drives a non-blocking on-screen notice.
    /// <para>
    /// The defaults describe a 4K UHD display (3840x2160). At runtime the app overrides these with the
    /// actual physical resolution of the display it is running on, so the cap tracks the real screen —
    /// there is never a reason to decode more pixels than the display can show.
    /// </para>
    /// </summary>
    public class ContentImageConfig
    {
        /// <summary>
        /// Maximum decoded pixel width of the target box. <c>0</c> or less on both dimensions means
        /// "no cap" (full-resolution decode).
        /// </summary>
        public int MaxDecodePixelWidth { get; set; } = 3840;

        /// <summary>
        /// Maximum decoded pixel height of the target box. <c>0</c> or less on both dimensions means
        /// "no cap" (full-resolution decode).
        /// </summary>
        public int MaxDecodePixelHeight { get; set; } = 2160;

        /// <summary>
        /// File-size threshold, in bytes, at/above which a content image is flagged as heavy: an
        /// actionable warning is logged and an on-screen notice is shown while it loads (the image is
        /// still displayed, downscaled to the display box). This tracks decode/IO latency — the symptom
        /// seen with large (e.g. 200&#160;MB) TIFFs — independently of pixel dimensions. Default 75&#160;MB;
        /// <c>0</c> disables the notice.
        /// </summary>
        public long LargeImageWarnBytes { get; set; } = 75L * 1024 * 1024;
    }
}
