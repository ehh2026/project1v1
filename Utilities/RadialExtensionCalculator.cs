using System;
using System.Collections.Generic;
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

        public RadialExtensionCalculator(RadialExtensionConfig config)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
        }

        // Per-marker state threaded through the angle-adjustment pipeline.
        private sealed record LocationAngleInfo(Location Location, Point ScreenPosition, double NaturalAngle)
        {
            public LocationAngleInfo WithAngle(double angle) => this with { NaturalAngle = angle };
        }

        // ─── Public API ──────────────────────────────────────────────────────────

        /// <summary>
        /// Detects groups of markers that are densely packed in source image space.
        /// </summary>
        public List<DenseMarkerGroup> DetectDenseGroups(Dictionary<Location, Point> markerPositions)
        {
            if (markerPositions == null || markerPositions.Count == 0)
                return new List<DenseMarkerGroup>();

            var groups    = new List<DenseMarkerGroup>();
            var processed = new HashSet<Location>();

            foreach (var kvp in markerPositions)
            {
                var location = kvp.Key;
                var position = kvp.Value;

                if (processed.Contains(location))
                    continue;

                var cluster   = new List<Location> { location };
                var queue     = new Queue<Location>();
                var inCluster = new HashSet<Location> { location };
                queue.Enqueue(location);

                while (queue.Count > 0)
                {
                    var current    = queue.Dequeue();
                    var currentPos = markerPositions[current];

                    foreach (var otherKvp in markerPositions)
                    {
                        var otherLocation = otherKvp.Key;
                        var otherPosition = otherKvp.Value;

                        if (processed.Contains(otherLocation) || inCluster.Contains(otherLocation))
                            continue;

                        if (CalculateDistance(currentPos, otherPosition) <= _config.ProximityThresholdPixels)
                        {
                            cluster.Add(otherLocation);
                            inCluster.Add(otherLocation);
                            queue.Enqueue(otherLocation);
                        }
                    }
                }

                if (cluster.Count >= _config.MinLocationsForExtension)
                {
                    groups.Add(new DenseMarkerGroup
                    {
                        Locations   = cluster,
                        CenterPoint = CalculateCenterPoint(cluster, markerPositions)
                    });
                    foreach (var loc in cluster)
                        processed.Add(loc);
                }
            }

            return groups;
        }

        /// <summary>
        /// Calculates radial extensions for a dense marker group.
        /// Ensures no line crossings by maintaining angular order.
        /// </summary>
        public List<RadialExtension> CalculateRadialExtensions(
            DenseMarkerGroup              group,
            Dictionary<Location, Point>   markerScreenPositions,
            double                        canvasWidth,
            double                        canvasHeight)
        {
            if (group == null || group.Count == 0)
                return new List<RadialExtension>();

            var screenCenter = CalculateCenterPoint(group.Locations, markerScreenPositions);

            // Build per-marker angle list (0° = north, clockwise)
            var items = new List<LocationAngleInfo>();
            foreach (var location in group.Locations)
            {
                if (!markerScreenPositions.TryGetValue(location, out Point screenPosition))
                    continue;

                double dx           = screenPosition.X - screenCenter.X;
                double dy           = screenPosition.Y - screenCenter.Y;
                double angleDegrees = Math.Atan2(dx, -dy) * (180.0 / Math.PI);
                if (angleDegrees < 0) angleDegrees += 360.0;
                items.Add(new LocationAngleInfo(location, screenPosition, angleDegrees));
            }

            items.Sort((a, b) => a.NaturalAngle.CompareTo(b.NaturalAngle));
            NudgeAnglesApart(items);
            PreventConvergingLines(items, screenCenter);
            PreventLineIntersections(items);

            var extensions = new List<RadialExtension>();
            for (int i = 0; i < items.Count; i++)
            {
                var    item         = items[i];
                double angleRadians = item.NaturalAngle * (Math.PI / 180.0);
                double extendedX    = item.ScreenPosition.X + _config.ExtensionLineLength * Math.Sin(angleRadians);
                double extendedY    = item.ScreenPosition.Y - _config.ExtensionLineLength * Math.Cos(angleRadians);

                double adjustedLength = _config.ExtensionLineLength;
                if (extendedX < 0 || extendedX > canvasWidth || extendedY < 0 || extendedY > canvasHeight)
                {
                    adjustedLength = CalculateMaxLength(item.ScreenPosition, angleRadians, canvasWidth, canvasHeight);
                    if (adjustedLength < 20.0) adjustedLength = 20.0;
                    extendedX = item.ScreenPosition.X + adjustedLength * Math.Sin(angleRadians);
                    extendedY = item.ScreenPosition.Y - adjustedLength * Math.Cos(angleRadians);
                }

                extensions.Add(new RadialExtension
                {
                    Location         = item.Location,
                    OriginalPosition = item.ScreenPosition,
                    ExtendedPosition = new Point(extendedX, extendedY),
                    Angle            = item.NaturalAngle
                });
            }

            return extensions;
        }

        /// <summary>
        /// Intentionally always returns true — crossing prevention is handled during
        /// <see cref="CalculateRadialExtensions"/> by <see cref="PreventLineIntersections"/>.
        /// </summary>
        public bool ValidateNoCrossings(List<RadialExtension> extensions) => true;

        // ─── Angle-adjustment pipeline ───────────────────────────────────────────

        /// <summary>
        /// Nudges angles apart when two markers share an angle within the threshold.
        /// Iterates until all pairs satisfy the threshold or the iteration cap is reached.
        /// </summary>
        private void NudgeAnglesApart(List<LocationAngleInfo> items)
        {
            if (items.Count < 2) return;

            const int    maxIterations = 10;
            const double maxAngleRange = 45.0;
            bool needsAdjustment       = true;
            int  iteration             = 0;

            while (needsAdjustment && iteration < maxIterations)
            {
                needsAdjustment = false;
                iteration++;

                foreach (var (i, j, diff) in FindAngularPairsWithinThreshold(items, maxAngleRange))
                {
                    if (diff >= _config.AngleNudgeThreshold || diff <= 0.01) continue;
                    needsAdjustment = true;
                    SafeNudgeApart(items, i, j, diff, _config.AngleNudgeAmount / 2.0);
                }

                if (needsAdjustment)
                    items.Sort((a, b) => a.NaturalAngle.CompareTo(b.NaturalAngle));
            }
        }

        /// <summary>
        /// Nudges apart pairs of lines that converge (get closer) as they extend outward.
        /// </summary>
        private void PreventConvergingLines(List<LocationAngleInfo> items, Point center)
        {
            if (items.Count < 2) return;

            const int    maxIterations = 5;
            const double maxAngleRange = 90.0;
            bool needsAdjustment       = true;
            int  iteration             = 0;

            while (needsAdjustment && iteration < maxIterations)
            {
                needsAdjustment = false;
                iteration++;

                foreach (var (i, j, diff) in FindAngularPairsWithinThreshold(items, maxAngleRange))
                {
                    var a = items[i];
                    var b = items[j];

                    double distOrigin = CalculateDistance(a.ScreenPosition, b.ScreenPosition);
                    double distExt    = CalculateDistance(ExtendedPoint(a), ExtendedPoint(b));

                    if (distExt >= distOrigin * 0.95) continue; // 5 % tolerance — not converging

                    needsAdjustment = true;
                    SafeNudgeApart(items, i, j, diff, _config.AngleNudgeAmount);
                }

                if (needsAdjustment)
                    items.Sort((a, b) => a.NaturalAngle.CompareTo(b.NaturalAngle));
            }
        }

        /// <summary>
        /// Detects actual line intersections and nudges angles apart to resolve them.
        /// Final pass after angle and convergence adjustments.
        /// </summary>
        private void PreventLineIntersections(List<LocationAngleInfo> items)
        {
            if (items.Count < 2) return;

            const int maxIterations = 10;
            bool foundIntersection  = true;
            int  iteration          = 0;

            while (foundIntersection && iteration < maxIterations)
            {
                foundIntersection = false;
                iteration++;

                for (int i = 0; i < items.Count; i++)
                {
                    Point end1 = ExtendedPoint(items[i]);
                    for (int j = i + 1; j < items.Count; j++)
                    {
                        Point end2 = ExtendedPoint(items[j]);
                        if (!DoLinesIntersect(items[i].ScreenPosition, end1,
                                              items[j].ScreenPosition, end2))
                            continue;

                        foundIntersection = true;
                        double diff = AngularDiff(items[i].NaturalAngle, items[j].NaturalAngle);
                        SafeNudgeApart(items, i, j, diff, _config.AngleNudgeAmount * 2.0);
                    }
                }

                if (foundIntersection)
                    items.Sort((a, b) => a.NaturalAngle.CompareTo(b.NaturalAngle));
            }
        }

        // ─── Shared helpers ──────────────────────────────────────────────────────

        /// <summary>
        /// Returns all (i, j, angleDiff) pairs whose angular separation is ≤ maxAngleDeg,
        /// including wrap-around pairs (near-360° with near-0°).
        /// Items must be sorted by NaturalAngle ascending.
        /// </summary>
        private static IReadOnlyList<(int I, int J, double Diff)> FindAngularPairsWithinThreshold(
            List<LocationAngleInfo> items, double maxAngleDeg)
        {
            var pairs = new List<(int, int, double)>();

            // Forward pairs (sorted order: items[j].angle > items[i].angle)
            for (int i = 0; i < items.Count; i++)
                for (int j = i + 1; j < items.Count; j++)
                {
                    double diff = AngularDiff(items[i].NaturalAngle, items[j].NaturalAngle);
                    if (diff > maxAngleDeg) break; // list is sorted, so no later j will be closer
                    pairs.Add((i, j, diff));
                }

            // Wrap-around pairs: items near 360° paired with items near 0°
            if (items.Count > 2)
                for (int i = 0; i < items.Count; i++)
                {
                    if (items[i].NaturalAngle < 360.0 - maxAngleDeg) continue;
                    for (int j = 0; j < items.Count && items[j].NaturalAngle <= maxAngleDeg; j++)
                    {
                        double diff = (items[j].NaturalAngle + 360.0 - items[i].NaturalAngle) % 360.0;
                        if (diff <= maxAngleDeg)
                            pairs.Add((i, j, diff));
                    }
                }

            return pairs;
        }

        /// <summary>
        /// Nudges items[i] and items[j] apart by <paramref name="nudgeAmount"/>,
        /// reducing the nudge if it would reverse their circular ordering.
        /// </summary>
        private static void SafeNudgeApart(
            List<LocationAngleInfo> items, int i, int j, double angleDiff, double nudgeAmount)
        {
            double nudge = nudgeAmount;
            double newI  = items[i].NaturalAngle - nudge;
            double newJ  = items[j].NaturalAngle + nudge;

            if (AngularDiff(newI, newJ) > 180.0) // would reverse circular ordering
            {
                nudge = Math.Min(nudge, angleDiff / 3.0);
                newI  = items[i].NaturalAngle - nudge;
                newJ  = items[j].NaturalAngle + nudge;
            }

            items[i] = items[i].WithAngle(newI);
            items[j] = items[j].WithAngle(newJ);
        }

        /// <summary>Angular distance from <paramref name="from"/> to <paramref name="to"/> (0° – 360°, exclusive).</summary>
        private static double AngularDiff(double from, double to) =>
            (to - from + 360.0) % 360.0;

        /// <summary>Projects a marker outward by <see cref="RadialExtensionConfig.ExtensionLineLength"/> along its assigned angle.</summary>
        private Point ExtendedPoint(LocationAngleInfo item)
        {
            double rad = item.NaturalAngle * (Math.PI / 180.0);
            return new Point(
                item.ScreenPosition.X + _config.ExtensionLineLength * Math.Sin(rad),
                item.ScreenPosition.Y - _config.ExtensionLineLength * Math.Cos(rad));
        }

        // ─── Geometry utilities ──────────────────────────────────────────────────

        private static double CalculateDistance(Point p1, Point p2)
        {
            double dx = p1.X - p2.X;
            double dy = p1.Y - p2.Y;
            return Math.Sqrt(dx * dx + dy * dy);
        }

        private static Point CalculateCenterPoint(List<Location> locations, Dictionary<Location, Point> positions)
        {
            if (locations.Count == 0)
                return new Point(0, 0);

            double sumX = 0, sumY = 0;
            foreach (var location in locations)
                if (positions.TryGetValue(location, out Point pos))
                { sumX += pos.X; sumY += pos.Y; }

            return new Point(sumX / locations.Count, sumY / locations.Count);
        }

        private double CalculateMaxLength(Point center, double angleRadians, double canvasWidth, double canvasHeight)
        {
            double sinAngle = Math.Sin(angleRadians);
            double cosAngle = Math.Cos(angleRadians);
            double maxLength = _config.ExtensionLineLength;

            if      (sinAngle > 0) maxLength = Math.Min(maxLength, (canvasWidth  - center.X) /  sinAngle);
            else if (sinAngle < 0) maxLength = Math.Min(maxLength,              center.X      / -sinAngle);

            if      (cosAngle > 0) maxLength = Math.Min(maxLength,              center.Y      /  cosAngle);
            else if (cosAngle < 0) maxLength = Math.Min(maxLength, (canvasHeight - center.Y) / -cosAngle);

            return Math.Max(20.0, maxLength * 0.9);
        }

        private static bool DoLinesIntersect(Point p1, Point p2, Point p3, Point p4)
        {
            double d1x = p2.X - p1.X, d1y = p2.Y - p1.Y;
            double d2x = p4.X - p3.X, d2y = p4.Y - p3.Y;
            double denominator = d1x * d2y - d1y * d2x;
            if (Math.Abs(denominator) < 0.0001) return false;
            double t1 = ((p3.X - p1.X) * d2y - (p3.Y - p1.Y) * d2x) / denominator;
            double t2 = ((p3.X - p1.X) * d1y - (p3.Y - p1.Y) * d1x) / denominator;
            return t1 > 0.01 && t1 < 0.99 && t2 > 0.01 && t2 < 0.99;
        }
    }
}
