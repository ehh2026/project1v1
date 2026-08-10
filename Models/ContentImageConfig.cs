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
    }
}
