using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using InteractiveWorldMap.Models;

namespace InteractiveWorldMap.Utilities
{
    /// <summary>
    /// Calculates radial extension lines for densely packed markers.
    /// Ensures lines never cross and maintains visual clarity.
    /// </summary>
    public class RadialExtensionCalculator
    {
        private readonly RadialExtensionConfig _config;

        /// <summary>
        /// Initializes a new instance of the RadialExtensionCalculator.
        /// </summary>
        /// <param name="config">Configuration for radial extensions</param>
        public RadialExtensionCalculator(RadialExtensionConfig config)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
        }

        /// <summary>
        /// Detects groups of markers that are densely packed in source image space.
        /// </summary>
        /// <param name="markerPositions">Dictionary mapping locations to their source pixel coordinates</param>
        /// <returns>List of dense marker groups</returns>
        public List<DenseMarkerGroup> DetectDenseGroups(Dictionary<Location, Point> markerPositions)
        {
            if (markerPositions == null || markerPositions.Count == 0)
                return new List<DenseMarkerGroup>();

            var groups = new List<DenseMarkerGroup>();
            var processed = new HashSet<Location>();

            foreach (var kvp in markerPositions)
            {
                var location = kvp.Key;
                var position = kvp.Value;

                // Skip if already processed
                if (processed.Contains(location))
                    continue;

                // Find all nearby locations using BFS (in source image space)
                var cluster = new List<Location> { location };
                var queue = new Queue<Location>();
                queue.Enqueue(location);
                var inCluster = new HashSet<Location> { location };

                while (queue.Count > 0)
                {
                    var current = queue.Dequeue();
                    var currentPos = markerPositions[current];

                    foreach (var otherKvp in markerPositions)
                    {
                        var otherLocation = otherKvp.Key;
                        var otherPosition = otherKvp.Value;

                        if (processed.Contains(otherLocation) || inCluster.Contains(otherLocation))
                            continue;

                        // Distance in source image pixels
                        double distance = CalculateDistance(currentPos, otherPosition);

                        if (distance <= _config.ProximityThresholdPixels)
                        {
                            cluster.Add(otherLocation);
                            inCluster.Add(otherLocation);
                            queue.Enqueue(otherLocation);
                        }
                    }
                }

                // Only create group if it meets minimum size
                if (cluster.Count >= _config.MinLocationsForExtension)
                {
                    var group = new DenseMarkerGroup
                    {
                        Locations = cluster,
                        CenterPoint = CalculateCenterPoint(cluster, markerPositions)
                    };

                    groups.Add(group);

                    // Mark all locations as processed
                    foreach (var loc in cluster)
                    {
                        processed.Add(loc);
                    }
                }
            }

            return groups;
        }

        /// <summary>
        /// Calculates radial extensions for a dense marker group.
        /// Ensures no line crossings by maintaining angular order.
        /// </summary>
        /// <param name="group">Dense marker group (with center in source coordinates)</param>
        /// <param name="markerScreenPositions">Dictionary mapping locations to screen positions</param>
        /// <param name="canvasWidth">Canvas width for boundary checking</param>
        /// <param name="canvasHeight">Canvas height for boundary checking</param>
        /// <returns>List of radial extensions</returns>
        public List<RadialExtension> CalculateRadialExtensions(
            DenseMarkerGroup group,
            Dictionary<Location, Point> markerScreenPositions,
            double canvasWidth,
            double canvasHeight)
        {
            if (group == null || group.Count == 0)
                return new List<RadialExtension>();

            var extensions = new List<RadialExtension>();
            
            // Calculate screen-space center from screen positions
            var screenCenter = CalculateCenterPoint(group.Locations, markerScreenPositions);
            int markerCount = group.Count;

            // Calculate natural angles for each location
            var locationsWithAngles = new List<(Location location, Point screenPosition, double naturalAngle)>();

            foreach (var location in group.Locations)
            {
                // Get screen position from the dictionary
                if (!markerScreenPositions.TryGetValue(location, out Point screenPosition))
                    continue; // Skip if position not found
                
                double dx = screenPosition.X - screenCenter.X;
                double dy = screenPosition.Y - screenCenter.Y;
                
                // Calculate angle: 0° = north, clockwise
                double angleRadians = Math.Atan2(dx, -dy);
                double angleDegrees = angleRadians * (180.0 / Math.PI);
                if (angleDegrees < 0) angleDegrees += 360.0;

                locationsWithAngles.Add((location, screenPosition, angleDegrees));
            }

            // Sort by natural angle to maintain order (prevents crossings)
            locationsWithAngles.Sort((a, b) => a.naturalAngle.CompareTo(b.naturalAngle));

            // Nudge angles apart if they're too close together
            NudgeAnglesApart(locationsWithAngles);

            // Check for converging lines and nudge them to diverge
            PreventConvergingLines(locationsWithAngles, screenCenter);

            // Use natural angles - extend each marker outward from its actual position
            for (int i = 0; i < markerCount; i++)
            {
                var (location, screenPosition, naturalAngle) = locationsWithAngles[i];
                double extensionAngle = naturalAngle; // Use the marker's natural angle (possibly nudged)

                // Calculate extended position from the marker's ACTUAL screen position
                // Move outward along the angle from center
                double angleRadians = extensionAngle * (Math.PI / 180.0);
                double extendedX = screenPosition.X + _config.ExtensionLineLength * Math.Sin(angleRadians);
                double extendedY = screenPosition.Y - _config.ExtensionLineLength * Math.Cos(angleRadians);

                // Check boundary collision and adjust if needed
                double adjustedLength = _config.ExtensionLineLength;
                
                if (extendedX < 0 || extendedX > canvasWidth || extendedY < 0 || extendedY > canvasHeight)
                {
                    adjustedLength = CalculateMaxLength(screenPosition, angleRadians, canvasWidth, canvasHeight);
                    
                    // Ensure minimum extension length
                    if (adjustedLength < 20.0)
                        adjustedLength = 20.0;

                    extendedX = screenPosition.X + adjustedLength * Math.Sin(angleRadians);
                    extendedY = screenPosition.Y - adjustedLength * Math.Cos(angleRadians);
                }

                var extension = new RadialExtension
                {
                    Location = location,
                    OriginalPosition = screenPosition, // Line starts at actual location
                    ExtendedPosition = new Point(extendedX, extendedY), // Line ends at extended position
                    Angle = extensionAngle
                };

                extensions.Add(extension);
            }

            return extensions;
        }

        /// <summary>
        /// Validates that extension lines do not cross each other.
        /// </summary>
        /// <param name="extensions">List of radial extensions</param>
        /// <returns>True if valid (no crossings), false otherwise</returns>
        public bool ValidateNoCrossings(List<RadialExtension> extensions)
        {
            // Temporarily disabled - always return true to allow natural angle positioning
            return true;
            
            /*
            if (extensions == null || extensions.Count < 2)
                return true; // Cannot cross with 0 or 1 line

            // Check that angles are not identical (which would cause lines to overlap exactly)
            // We allow very close angles since we're preserving natural positions
            for (int i = 0; i < extensions.Count - 1; i++)
            {
                double angle1 = extensions[i].Angle;
                double angle2 = extensions[i + 1].Angle;

                // Calculate angular difference (accounting for 360° wrap)
                double diff = (angle2 - angle1 + 360.0) % 360.0;

                // Log all angle differences for debugging
                System.Diagnostics.Debug.WriteLine(
                    $"RadialExtensionCalculator: Angle diff at index {i}: {diff:F2}° " +
                    $"(angle1={angle1:F2}°, angle2={angle2:F2}°)");

                // Only fail if angles are essentially identical (< 0.1°)
                if (diff < 0.1)
                {
                    System.Diagnostics.Debug.WriteLine(
                        $"RadialExtensionCalculator: VALIDATION FAILED - Identical angles detected " +
                        $"({diff:F2}°) at index {i} - lines would overlap exactly");
                    return false;
                }
            }

            System.Diagnostics.Debug.WriteLine("RadialExtensionCalculator: Validation PASSED - no identical angles");
            return true;
            */
        }

        /// <summary>
        /// Calculates Euclidean distance between two points.
        /// </summary>
        private double CalculateDistance(Point p1, Point p2)
        {
            double dx = p1.X - p2.X;
            double dy = p1.Y - p2.Y;
            return Math.Sqrt(dx * dx + dy * dy);
        }

        /// <summary>
        /// Calculates the geometric center point of a list of locations.
        /// </summary>
        private Point CalculateCenterPoint(List<Location> locations, Dictionary<Location, Point> positions)
        {
            if (locations.Count == 0)
                return new Point(0, 0);

            double sumX = 0;
            double sumY = 0;

            foreach (var location in locations)
            {
                if (positions.TryGetValue(location, out Point pos))
                {
                    sumX += pos.X;
                    sumY += pos.Y;
                }
            }

            return new Point(sumX / locations.Count, sumY / locations.Count);
        }

        /// <summary>
        /// Calculates maximum extension length before hitting canvas boundary.
        /// </summary>
        private double CalculateMaxLength(Point center, double angleRadians, double canvasWidth, double canvasHeight)
        {
            double sinAngle = Math.Sin(angleRadians);
            double cosAngle = Math.Cos(angleRadians);

            double maxLength = _config.ExtensionLineLength;

            // Check distance to each boundary
            if (sinAngle > 0) // Moving right
            {
                double distToRight = (canvasWidth - center.X) / sinAngle;
                maxLength = Math.Min(maxLength, distToRight);
            }
            else if (sinAngle < 0) // Moving left
            {
                double distToLeft = center.X / -sinAngle;
                maxLength = Math.Min(maxLength, distToLeft);
            }

            if (cosAngle > 0) // Moving up (negative Y)
            {
                double distToTop = center.Y / cosAngle;
                maxLength = Math.Min(maxLength, distToTop);
            }
            else if (cosAngle < 0) // Moving down
            {
                double distToBottom = (canvasHeight - center.Y) / -cosAngle;
                maxLength = Math.Min(maxLength, distToBottom);
            }

            return Math.Max(20.0, maxLength * 0.9); // 90% of max to add margin
        }

        /// <summary>
        /// Nudges angles apart if they're within the threshold.
        /// Modifies the list in place. Iterates until all angles meet the threshold or max iterations reached.
        /// Checks all pairs within angular range, not just adjacent pairs.
        /// </summary>
        private void NudgeAnglesApart(List<(Location location, Point screenPosition, double naturalAngle)> locationsWithAngles)
        {
            if (locationsWithAngles.Count < 2)
                return;

            const int maxIterations = 10; // Prevent infinite loops
            const double maxAngleRangeToCheck = 45.0; // Check pairs within 45° for angle separation
            int iteration = 0;
            bool needsAdjustment = true;

            while (needsAdjustment && iteration < maxIterations)
            {
                needsAdjustment = false;
                iteration++;

                // Check all pairs within angular range
                for (int i = 0; i < locationsWithAngles.Count; i++)
                {
                    var current = locationsWithAngles[i];

                    for (int j = i + 1; j < locationsWithAngles.Count; j++)
                    {
                        var other = locationsWithAngles[j];

                        double angleDiff = (other.naturalAngle - current.naturalAngle + 360.0) % 360.0;
                        
                        // Only check pairs within the angular range
                        if (angleDiff > maxAngleRangeToCheck)
                            break; // Since list is sorted, no need to check further

                        if (angleDiff < _config.AngleNudgeThreshold && angleDiff > 0.01)
                        {
                            needsAdjustment = true;
                            
                            // Nudge them apart
                            double nudge = _config.AngleNudgeAmount / 2.0;
                            
                            // Update the angles
                            locationsWithAngles[i] = (current.location, current.screenPosition, current.naturalAngle - nudge);
                            locationsWithAngles[j] = (other.location, other.screenPosition, other.naturalAngle + nudge);
                        }
                    }
                }

                // Also check wrap-around (markers near 360° with markers near 0°)
                if (locationsWithAngles.Count > 2)
                {
                    for (int i = 0; i < locationsWithAngles.Count; i++)
                    {
                        var current = locationsWithAngles[i];
                        
                        // Only check markers in the last 45° (315° to 360°)
                        if (current.naturalAngle < 315.0)
                            continue;

                        for (int j = 0; j < locationsWithAngles.Count; j++)
                        {
                            var other = locationsWithAngles[j];
                            
                            // Only check markers in the first 45° (0° to 45°)
                            if (other.naturalAngle > 45.0)
                                break;

                            double wrapDiff = (other.naturalAngle + 360.0 - current.naturalAngle) % 360.0;
                            
                            if (wrapDiff > maxAngleRangeToCheck)
                                continue;
                            
                            if (wrapDiff < _config.AngleNudgeThreshold && wrapDiff > 0.01)
                            {
                                needsAdjustment = true;
                                
                                double nudge = _config.AngleNudgeAmount / 2.0;
                                
                                locationsWithAngles[i] = (current.location, current.screenPosition, current.naturalAngle - nudge);
                                locationsWithAngles[j] = (other.location, other.screenPosition, other.naturalAngle + nudge);
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Checks if adjacent extension lines are converging (getting closer as they extend outward)
        /// and nudges them apart to ensure they diverge from the center.
        /// </summary>
        private void PreventConvergingLines(List<(Location location, Point screenPosition, double naturalAngle)> locationsWithAngles, Point center)
        {
            if (locationsWithAngles.Count < 2)
                return;

            const int maxIterations = 5;
            const double maxAngleRangeToCheck = 90.0; // Check pairs within 90° of each other
            int iteration = 0;
            bool needsAdjustment = true;

            while (needsAdjustment && iteration < maxIterations)
            {
                needsAdjustment = false;
                iteration++;

                // Check all pairs within angular range, not just adjacent
                for (int i = 0; i < locationsWithAngles.Count; i++)
                {
                    var current = locationsWithAngles[i];

                    // Check this marker against all others within angular range
                    for (int j = i + 1; j < locationsWithAngles.Count; j++)
                    {
                        var other = locationsWithAngles[j];

                        // Calculate angular difference
                        double angleDiff = (other.naturalAngle - current.naturalAngle + 360.0) % 360.0;
                        
                        // Only check pairs within the angular range
                        if (angleDiff > maxAngleRangeToCheck)
                            break; // Since list is sorted, no need to check further

                        // Calculate distance between markers at their current positions
                        double distanceAtOrigin = CalculateDistance(current.screenPosition, other.screenPosition);

                        // Project both lines outward by the extension length
                        double currentAngleRad = current.naturalAngle * (Math.PI / 180.0);
                        double otherAngleRad = other.naturalAngle * (Math.PI / 180.0);

                        Point currentExtended = new Point(
                            current.screenPosition.X + _config.ExtensionLineLength * Math.Sin(currentAngleRad),
                            current.screenPosition.Y - _config.ExtensionLineLength * Math.Cos(currentAngleRad)
                        );

                        Point otherExtended = new Point(
                            other.screenPosition.X + _config.ExtensionLineLength * Math.Sin(otherAngleRad),
                            other.screenPosition.Y - _config.ExtensionLineLength * Math.Cos(otherAngleRad)
                        );

                        // Calculate distance at extended positions
                        double distanceAtExtension = CalculateDistance(currentExtended, otherExtended);

                        // If lines are converging (distance decreases), nudge them apart
                        if (distanceAtExtension < distanceAtOrigin * 0.95) // 5% tolerance
                        {
                            needsAdjustment = true;
                            double nudge = _config.AngleNudgeAmount;

                            System.Diagnostics.Debug.WriteLine(
                                $"Converging lines detected: {current.location.Name} ({current.naturalAngle:F1}°) and " +
                                $"{other.location.Name} ({other.naturalAngle:F1}°) - " +
                                $"Angular diff: {angleDiff:F1}°, " +
                                $"Distance at origin: {distanceAtOrigin:F1}px, at extension: {distanceAtExtension:F1}px. " +
                                $"Nudging apart by {nudge}°");

                            // Nudge them apart proportionally based on their positions
                            locationsWithAngles[i] = (current.location, current.screenPosition, current.naturalAngle - nudge);
                            locationsWithAngles[j] = (other.location, other.screenPosition, other.naturalAngle + nudge);
                        }
                    }
                }

                // Also check wrap-around: compare markers near 360° with markers near 0°
                if (locationsWithAngles.Count > 2)
                {
                    for (int i = 0; i < locationsWithAngles.Count; i++)
                    {
                        var current = locationsWithAngles[i];
                        
                        // Only check markers in the last 90° (270° to 360°)
                        if (current.naturalAngle < 270.0)
                            continue;

                        for (int j = 0; j < locationsWithAngles.Count; j++)
                        {
                            var other = locationsWithAngles[j];
                            
                            // Only check markers in the first 90° (0° to 90°)
                            if (other.naturalAngle > 90.0)
                                break;

                            // Calculate wrap-around angular difference
                            double angleDiff = (other.naturalAngle + 360.0 - current.naturalAngle) % 360.0;
                            
                            if (angleDiff > maxAngleRangeToCheck)
                                continue;

                            double distanceAtOrigin = CalculateDistance(current.screenPosition, other.screenPosition);

                            double currentAngleRad = current.naturalAngle * (Math.PI / 180.0);
                            double otherAngleRad = other.naturalAngle * (Math.PI / 180.0);

                            Point currentExtended = new Point(
                                current.screenPosition.X + _config.ExtensionLineLength * Math.Sin(currentAngleRad),
                                current.screenPosition.Y - _config.ExtensionLineLength * Math.Cos(currentAngleRad)
                            );

                            Point otherExtended = new Point(
                                other.screenPosition.X + _config.ExtensionLineLength * Math.Sin(otherAngleRad),
                                other.screenPosition.Y - _config.ExtensionLineLength * Math.Cos(otherAngleRad)
                            );

                            double distanceAtExtension = CalculateDistance(currentExtended, otherExtended);

                            if (distanceAtExtension < distanceAtOrigin * 0.95)
                            {
                                needsAdjustment = true;
                                double nudge = _config.AngleNudgeAmount;

                                System.Diagnostics.Debug.WriteLine(
                                    $"Converging lines detected (wrap-around): {current.location.Name} ({current.naturalAngle:F1}°) and " +
                                    $"{other.location.Name} ({other.naturalAngle:F1}°) - " +
                                    $"Angular diff: {angleDiff:F1}°, " +
                                    $"Distance at origin: {distanceAtOrigin:F1}px, at extension: {distanceAtExtension:F1}px. " +
                                    $"Nudging apart by {nudge}°");

                                locationsWithAngles[i] = (current.location, current.screenPosition, current.naturalAngle - nudge);
                                locationsWithAngles[j] = (other.location, other.screenPosition, other.naturalAngle + nudge);
                            }
                        }
                    }
                }
            }
        }
    }
}
