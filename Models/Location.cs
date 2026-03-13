namespace InteractiveWorldMap.Models
{
    /// <summary>
    /// Represents a location on the map with associated content.
    /// </summary>
    public class Location
    {
        /// <summary>
        /// Unique identifier for the location.
        /// </summary>
        public string Id { get; set; } = string.Empty;

        /// <summary>
        /// Display name of the location.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// X pixel coordinate on the map image.
        /// </summary>
        public double PixelX { get; set; }

        /// <summary>
        /// Y pixel coordinate on the map image.
        /// </summary>
        public double PixelY { get; set; }

        /// <summary>
        /// Path to the content file associated with this location.
        /// </summary>
        public string ContentFilePath { get; set; } = string.Empty;

        /// <summary>
        /// Type of content (Image or Text).
        /// </summary>
        public LocationContentType ContentType { get; set; }
    }
}
