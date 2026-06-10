using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using InteractiveWorldMap.Models;

namespace InteractiveWorldMap.Services
{
    /// <summary>
    /// Computes stable, viewport-independent hash strings used as inputs to the
    /// <see cref="CompositePinPlanCache"/> cache key.
    /// All methods are pure and produce hex strings truncated to 16 characters.
    /// </summary>
    public static class CompositePinLayoutContentHasher
    {
        /// <summary>
        /// Hashes the layout-specific inputs that determine which render plan each location receives.
        /// Uses angle + lineLength (not screen positions) so the hash is viewport-independent.
        /// Markers are sorted by name so JSON key order does not affect the result.
        /// </summary>
        public static string ComputeLayoutContentHash(IEnumerable<ManualLayoutMarker> markers)
        {
            var sb = new StringBuilder();
            foreach (var m in markers.OrderBy(m => m.LocationName, System.StringComparer.Ordinal))
            {
                sb.Append(m.LocationName).Append(':')
                  .Append(m.Angle.ToString("F4")).Append(':')
                  .Append(m.LineLength.ToString("F4")).Append(':')
                  .Append(m.PairId ?? string.Empty).Append(':')
                  .Append(m.HeadSourcePath ?? string.Empty).Append(';');
            }
            return Sha256Short(sb.ToString());
        }

        /// <summary>
        /// Hashes the raw bytes of the geometry JSON file so that any edit to
        /// <c>pin_part_geometry.json</c> produces a new cache key (miss) automatically.
        /// Returns a placeholder string if the file is missing.
        /// </summary>
        public static string ComputeGeometryHash(string absoluteGeometryFilePath)
        {
            if (!File.Exists(absoluteGeometryFilePath))
                return "geometry-missing";

            var bytes = File.ReadAllBytes(absoluteGeometryFilePath);
            using var sha = SHA256.Create();
            return HexShort(sha.ComputeHash(bytes));
        }

        /// <summary>
        /// Hashes the <see cref="PinPartConfig"/> fields that affect render-plan output.
        /// Changes to these fields produce a new cache key, forcing a fresh build.
        /// </summary>
        public static string ComputeConfigHash(PinPartConfig config)
        {
            var key = $"{config.SelectionMode}:" +
                      $"{config.MaxResidualRotationDeg:F2}:" +
                      $"{config.MinStretchFactor:F3}:" +
                      $"{config.MaxStretchFactor:F3}:" +
                      $"{config.TargetHeadRadiusPx:F2}:" +
                      $"{config.TargetShaftHalfWidthPx:F2}:" +
                      $"{config.UseLitShafts}:" +
                      $"{config.ShaftAssetVariant}";
            return Sha256Short(key);
        }

        // ─── helpers ─────────────────────────────────────────────────────────

        private static string Sha256Short(string input)
        {
            using var sha = SHA256.Create();
            return HexShort(sha.ComputeHash(Encoding.UTF8.GetBytes(input)));
        }

        private static string HexShort(byte[] bytes) =>
            System.Convert.ToHexString(bytes)[..16];
    }
}
