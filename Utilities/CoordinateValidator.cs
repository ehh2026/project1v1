using System;

namespace InteractiveWorldMap.Utilities
{
    /// <summary>
    /// Validates and constrains geographic coordinates to valid ranges.
    /// </summary>
    public class CoordinateValidator
    {
        /// <summary>
        /// Checks if the provided latitude and longitude are within valid ranges.
        /// </summary>
        /// <param name="latitude">Latitude value to validate (valid range: -90 to 90)</param>
        /// <param name="longitude">Longitude value to validate (valid range: -180 to 180)</param>
        /// <returns>True if both coordinates are within valid ranges, false otherwise</returns>
        public bool IsValid(double latitude, double longitude)
        {
            return latitude >= -90.0 && latitude <= 90.0 &&
                   longitude >= -180.0 && longitude <= 180.0;
        }

        /// <summary>
        /// Constrains latitude and longitude values to valid ranges.
        /// </summary>
        /// <param name="latitude">Latitude value to clamp (will be constrained to -90 to 90)</param>
        /// <param name="longitude">Longitude value to clamp (will be constrained to -180 to 180)</param>
        /// <returns>A tuple containing the clamped latitude and longitude values</returns>
        public (double lat, double lon) Clamp(double latitude, double longitude)
        {
            var clampedLat = Math.Max(-90.0, Math.Min(90.0, latitude));
            var clampedLon = Math.Max(-180.0, Math.Min(180.0, longitude));
            return (clampedLat, clampedLon);
        }
    }
}
