namespace InteractiveWorldMap.Models
{
    /// <summary>
    /// Represents a geographic location with associated content.
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
        /// Latitude coordinate (-90 to +90 degrees).
        /// </summary>
        public double Latitude { get; set; }

        /// <summary>
        /// Longitude coordinate (-180 to +180 degrees).
        /// </summary>
        public double Longitude { get; set; }

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
