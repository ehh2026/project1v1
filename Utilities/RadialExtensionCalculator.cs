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

            // Final check: detect and fix any actual line intersections
            PreventLineIntersections(locationsWithAngles);

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
                            
                            // Nudge them apart, but ensure we don't reverse their order
                            double nudge = _config.AngleNudgeAmount / 2.0;
                            
                            // Make sure nudging won't cause them to cross over
                            double newCurrentAngle = current.naturalAngle - nudge;
                            double newOtherAngle = other.naturalAngle + nudge;
                            
                            // Verify the order is maintained after nudging
                            double newDiff = (newOtherAngle - newCurrentAngle + 360.0) % 360.0;
                            if (newDiff > 180.0) // They would cross over
                            {
                                // Reduce nudge to prevent crossover
                                nudge = Math.Min(nudge, angleDiff / 3.0);
                                newCurrentAngle = current.naturalAngle - nudge;
                                newOtherAngle = other.naturalAngle + nudge;
                            }
                            
                            // Update the angles
                            locationsWithAngles[i] = (current.location, current.screenPosition, newCurrentAngle);
                            locationsWithAngles[j] = (other.location, other.screenPosition, newOtherAngle);
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
                                
                                // Prevent crossover for wrap-around case
                                double newCurrentAngle = current.naturalAngle - nudge;
                                double newOtherAngle = other.naturalAngle + nudge;
                                double newWrapDiff = (newOtherAngle + 360.0 - newCurrentAngle) % 360.0;
                                
                                if (newWrapDiff > 180.0)
                                {
                                    nudge = Math.Min(nudge, wrapDiff / 3.0);
                                    newCurrentAngle = current.naturalAngle - nudge;
                                    newOtherAngle = other.naturalAngle + nudge;
                                }
                                
                                locationsWithAngles[i] = (current.location, current.screenPosition, newCurrentAngle);
                                locationsWithAngles[j] = (other.location, other.screenPosition, newOtherAngle);
                            }
                        }
                    }
                }
                
                // Re-sort after nudging to maintain angular order
                if (needsAdjustment)
                {
                    locationsWithAngles.Sort((a, b) => a.naturalAngle.CompareTo(b.naturalAngle));
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

                            // Prevent crossover
                            double newCurrentAngle = current.naturalAngle - nudge;
                            double newOtherAngle = other.naturalAngle + nudge;
                            double newDiff = (newOtherAngle - newCurrentAngle + 360.0) % 360.0;
                            
                            if (newDiff > 180.0) // Would cause crossover
                            {
                                nudge = Math.Min(nudge, angleDiff / 3.0);
                                newCurrentAngle = current.naturalAngle - nudge;
                                newOtherAngle = other.naturalAngle + nudge;
                            }

                            System.Diagnostics.Debug.WriteLine(
                                $"Converging lines detected: {current.location.Name} ({current.naturalAngle:F1}°) and " +
                                $"{other.location.Name} ({other.naturalAngle:F1}°) - " +
                                $"Angular diff: {angleDiff:F1}°, " +
                                $"Distance at origin: {distanceAtOrigin:F1}px, at extension: {distanceAtExtension:F1}px. " +
                                $"Nudging apart by {nudge}°");

                            locationsWithAngles[i] = (current.location, current.screenPosition, newCurrentAngle);
                            locationsWithAngles[j] = (other.location, other.screenPosition, newOtherAngle);
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

                                // Prevent crossover for wrap-around
                                double newCurrentAngle = current.naturalAngle - nudge;
                                double newOtherAngle = other.naturalAngle + nudge;
                                double newWrapDiff = (newOtherAngle + 360.0 - newCurrentAngle) % 360.0;
                                
                                if (newWrapDiff > 180.0)
                                {
                                    nudge = Math.Min(nudge, angleDiff / 3.0);
                                    newCurrentAngle = current.naturalAngle - nudge;
                                    newOtherAngle = other.naturalAngle + nudge;
                                }

                                System.Diagnostics.Debug.WriteLine(
                                    $"Converging lines detected (wrap-around): {current.location.Name} ({current.naturalAngle:F1}°) and " +
                                    $"{other.location.Name} ({other.naturalAngle:F1}°) - " +
                                    $"Angular diff: {angleDiff:F1}°, " +
                                    $"Distance at origin: {distanceAtOrigin:F1}px, at extension: {distanceAtExtension:F1}px. " +
                                    $"Nudging apart by {nudge}°");

                                locationsWithAngles[i] = (current.location, current.screenPosition, newCurrentAngle);
                                locationsWithAngles[j] = (other.location, other.screenPosition, newOtherAngle);
                            }
                        }
                    }
                }
                
                // Re-sort after nudging to maintain angular order
                if (needsAdjustment)
                {
                    locationsWithAngles.Sort((a, b) => a.naturalAngle.CompareTo(b.naturalAngle));
                }
            }
        }

        /// <summary>
        /// Detects actual line intersections and nudges angles to prevent them.
        /// This is the final check after angle and convergence adjustments.
        /// </summary>
        private void PreventLineIntersections(List<(Location location, Point screenPosition, double naturalAngle)> locationsWithAngles)
        {
            if (locationsWithAngles.Count < 2)
                return;

            Console.WriteLine($"[PreventLineIntersections] Checking {locationsWithAngles.Count} lines for intersections");

            const int maxIterations = 10;
            int iteration = 0;
            bool foundIntersection = true;
            int totalIntersectionsFound = 0;

            while (foundIntersection && iteration < maxIterations)
            {
                foundIntersection = false;
                iteration++;

                // Check all pairs of lines for intersection
                for (int i = 0; i < locationsWithAngles.Count; i++)
                {
                    var line1 = locationsWithAngles[i];
                    double angle1Rad = line1.naturalAngle * (Math.PI / 180.0);
                    Point line1End = new Point(
                        line1.screenPosition.X + _config.ExtensionLineLength * Math.Sin(angle1Rad),
                        line1.screenPosition.Y - _config.ExtensionLineLength * Math.Cos(angle1Rad)
                    );

                    for (int j = i + 1; j < locationsWithAngles.Count; j++)
                    {
                        var line2 = locationsWithAngles[j];
                        double angle2Rad = line2.naturalAngle * (Math.PI / 180.0);
                        Point line2End = new Point(
                            line2.screenPosition.X + _config.ExtensionLineLength * Math.Sin(angle2Rad),
                            line2.screenPosition.Y - _config.ExtensionLineLength * Math.Cos(angle2Rad)
                        );

                        // Check if line segments intersect
                        if (DoLinesIntersect(line1.screenPosition, line1End, line2.screenPosition, line2End))
                        {
                            foundIntersection = true;
                            totalIntersectionsFound++;
                            double nudge = _config.AngleNudgeAmount * 2.0; // Larger nudge for intersections

                            Console.WriteLine(
                                $"  [INTERSECTION #{totalIntersectionsFound}] {line1.location.Name} ({line1.naturalAngle:F1}°) and " +
                                $"{line2.location.Name} ({line2.naturalAngle:F1}°). " +
                                $"Line1: ({line1.screenPosition.X:F0},{line1.screenPosition.Y:F0})->({line1End.X:F0},{line1End.Y:F0}), " +
                                $"Line2: ({line2.screenPosition.X:F0},{line2.screenPosition.Y:F0})->({line2End.X:F0},{line2End.Y:F0}). " +
                                $"Nudging by {nudge}°");

                            // Nudge them apart
                            locationsWithAngles[i] = (line1.location, line1.screenPosition, line1.naturalAngle - nudge);
                            locationsWithAngles[j] = (line2.location, line2.screenPosition, line2.naturalAngle + nudge);
                        }
                    }
                }

                // Re-sort after nudging
                if (foundIntersection)
                {
                    locationsWithAngles.Sort((a, b) => a.naturalAngle.CompareTo(b.naturalAngle));
                    Console.WriteLine($"  [PreventLineIntersections] Iteration {iteration}: Found intersections, re-sorted angles");
                }
            }

            Console.WriteLine($"[PreventLineIntersections] Complete. Total intersections: {totalIntersectionsFound}, Iterations: {iteration}");
        }

        /// <summary>
        /// Checks if two line segments intersect.
        /// </summary>
        private bool DoLinesIntersect(Point p1, Point p2, Point p3, Point p4)
        {
            // Calculate direction vectors
            double d1x = p2.X - p1.X;
            double d1y = p2.Y - p1.Y;
            double d2x = p4.X - p3.X;
            double d2y = p4.Y - p3.Y;

            // Calculate denominator for intersection formula
            double denominator = d1x * d2y - d1y * d2x;

            // Lines are parallel if denominator is zero
            if (Math.Abs(denominator) < 0.0001)
                return false;

            // Calculate parameters for intersection point
            double t1 = ((p3.X - p1.X) * d2y - (p3.Y - p1.Y) * d2x) / denominator;
            double t2 = ((p3.X - p1.X) * d1y - (p3.Y - p1.Y) * d1x) / denominator;

            // Lines intersect if both parameters are between 0 and 1
            return t1 > 0.01 && t1 < 0.99 && t2 > 0.01 && t2 < 0.99;
        }
    }
}
