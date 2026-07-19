using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using InteractiveWorldMap.Models;

namespace InteractiveWorldMap.Services
{
    /// <summary>
    /// Generates unique keys for manual layouts based on all relevant configuration factors
    /// </summary>
    public class LayoutKeyGenerator
    {
        /// <summary>
        /// Generate a unique key for a layout based on locations, viewport, and configuration
        /// </summary>
        public static string GenerateKey(
            List<Location> locations,
            ViewportState viewport,
            RadialExtensionConfig config)
        {
            // Sort location names for consistent hashing.
            // Use StringComparer.Ordinal so the order is locale-independent and matches
            // the PowerShell seed generator which calls List<string>.Sort(StringComparer.Ordinal).
            var locationNames = locations.Select(l => l.Name).OrderBy(n => n, StringComparer.Ordinal).ToList();
            var locationHash = ComputeHash(string.Join("|", locationNames));

            // Calculate viewport center
            var centerX = viewport.ViewportX + (viewport.ViewportWidth / 2.0);
            var centerY = viewport.ViewportY + (viewport.ViewportHeight / 2.0);

            // Build key from all relevant factors
            var keyParts = new List<string>
            {
                locationHash,
                $"z{viewport.ZoomLevel:F2}",
                $"c{centerX:F2}_{centerY:F2}",
                $"s{viewport.ViewportWidth:F0}x{viewport.ViewportHeight:F0}",
                $"m{config.MinLocationsForExtension}",
                $"p{config.ProximityThresholdPixels:F1}",
                $"l{config.ExtensionLineLength:F1}",
                $"n{config.MinimumLineLength:F1}"
            };

            return string.Join("_", keyParts);
        }

        /// <summary>
        /// Generate a shorter, more readable key (for display purposes)
        /// </summary>
        public static string GenerateShortKey(List<Location> locations, ViewportState viewport)
        {
            var locationNames = locations.Select(l => l.Name).OrderBy(n => n, StringComparer.Ordinal).ToList();
            var locationHash = ComputeHash(string.Join("|", locationNames)).Substring(0, 8);
            
            return $"{locationHash}_z{viewport.ZoomLevel:F1}";
        }

        /// <summary>
        /// Generate the manual layout group key for full-map layouts.
        /// Size-independent: there is only ever one "whole map" layout, and marker positions
        /// re-project from source space (<see cref="Models.ManualLayoutMarker.SourceExtendedX"/>),
        /// so the canvas size must not be part of the key (a resize would otherwise orphan it).
        /// Legacy sized keys (<c>fullmap_s{W}x{H}</c>) still resolve via <see cref="AreKeysCompatible"/>.
        /// </summary>
        public static string GenerateFullMapGroupKey()
        {
            return "fullmap";
        }

        /// <summary>
        /// Compute SHA256 hash of a string
        /// </summary>
        private static string ComputeHash(string input)
        {
            using (var sha256 = SHA256.Create())
            {
                byte[] bytes = Encoding.UTF8.GetBytes(input);
                byte[] hash = sha256.ComputeHash(bytes);
                return BitConverter.ToString(hash).Replace("-", "").ToLower().Substring(0, 16);
            }
        }

        /// <summary>
        /// Check if two keys are compatible (same locations, close enough viewport)
        /// </summary>
        public static bool AreKeysCompatible(string key1, string key2, double zoomTolerance = 0.1)
        {
            // Full-map layouts are size-independent: any two full-map keys (including legacy
            // sized `fullmap_s{W}x{H}` forms) describe the same "whole map" layout and are
            // compatible. A full-map key is never compatible with a cluster key.
            bool fullMap1 = IsFullMapKey(key1);
            bool fullMap2 = IsFullMapKey(key2);
            if (fullMap1 || fullMap2)
                return fullMap1 && fullMap2;

            var parts1 = key1.Split('_');
            var parts2 = key2.Split('_');

            if (parts1.Length < 2 || parts2.Length < 2)
                return false;

            // Check if location hash matches
            if (parts1[0] != parts2[0])
                return false;

            // Check if zoom levels are close
            if (parts1.Length > 1 && parts2.Length > 1)
            {
                var zoom1 = ExtractZoomLevel(parts1[1]);
                var zoom2 = ExtractZoomLevel(parts2[1]);
                
                if (Math.Abs(zoom1 - zoom2) > zoomTolerance)
                    return false;
            }

            return true;
        }

        private static double ExtractZoomLevel(string zoomPart)
        {
            if (zoomPart.StartsWith("z") && double.TryParse(zoomPart.Substring(1), out double zoom))
                return zoom;
            return 0;
        }

        private static bool IsFullMapKey(string key)
        {
            // Matches both the current constant key ("fullmap") and legacy sized keys ("fullmap_s…").
            return key.StartsWith("fullmap", StringComparison.Ordinal);
        }
    }
}
