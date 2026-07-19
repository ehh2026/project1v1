using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using InteractiveWorldMap.Models;
using InteractiveWorldMap.Utilities;

namespace InteractiveWorldMap.Services;

/// <summary>
/// Iteratively adjusts radial extension lines to resolve marker overlaps and line
/// intersections. Pure data-manipulation service — no UI dependencies.
/// All adjustments mutate <see cref="RadialExtension.Angle"/> and
/// <see cref="RadialExtension.ExtendedPosition"/> in place.
/// </summary>
public class RadialExtensionAdjuster
{
    private readonly ILogger _logger;
    private readonly VisualConfig _visualConfig;

    public RadialExtensionAdjuster(ILogger logger, VisualConfig visualConfig)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _visualConfig = visualConfig ?? throw new ArgumentNullException(nameof(visualConfig));
    }

    /// <summary>
    /// Iteratively adjusts extensions for both marker overlaps and line intersections
    /// until the system stabilises or the maximum iteration count is reached.
    /// Detects oscillation and reduces adjustment amounts to aid convergence.
    /// </summary>
    public void AdjustExtensions(List<RadialExtension> allExtensions, double markerSize)
    {
        const int maxIterations = 5;
        int iteration = 0;
        bool needsAdjustment = true;

        var pairAdjustmentCount = new Dictionary<string, int>();
        double nudgeMultiplier = 1.0;
        var protectedFromOverlapAdjustment = new HashSet<string>();

        _logger.LogInfo($"[IterativeAdjustment] Starting iterative adjustment for {allExtensions.Count} extensions");

        while (needsAdjustment && iteration < maxIterations)
        {
            iteration++;
            needsAdjustment = false;

            _logger.LogInfo($"[IterativeAdjustment] === Iteration {iteration} (nudge multiplier: {nudgeMultiplier:F2}) ===");

            bool hadOverlaps = AdjustForMarkerOverlaps(allExtensions, markerSize, protectedFromOverlapAdjustment);
            if (hadOverlaps)
            {
                needsAdjustment = true;
                _logger.LogInfo("[IterativeAdjustment] Overlaps were adjusted");
            }

            var intersectingPairs = new List<string>();
            bool hadIntersections = FixLineIntersections(allExtensions, intersectingPairs, nudgeMultiplier);
            if (hadIntersections)
            {
                needsAdjustment = true;
                _logger.LogInfo("[IterativeAdjustment] Intersections were fixed");

                foreach (var pair in intersectingPairs)
                {
                    if (!pairAdjustmentCount.ContainsKey(pair))
                        pairAdjustmentCount[pair] = 0;
                    pairAdjustmentCount[pair]++;

                    var names = pair.Split('-');
                    protectedFromOverlapAdjustment.Add(names[0]);
                    protectedFromOverlapAdjustment.Add(names[1]);
                }
            }

            int maxAdjustments = pairAdjustmentCount.Values.Any() ? pairAdjustmentCount.Values.Max() : 0;
            if (maxAdjustments >= 3)
            {
                nudgeMultiplier *= 0.5;
                _logger.LogInfo($"[IterativeAdjustment] OSCILLATION DETECTED (max adjustments: {maxAdjustments}), reducing nudge multiplier to {nudgeMultiplier:F2}");

                foreach (var kvp in pairAdjustmentCount.Where(x => x.Value >= 3))
                {
                    _logger.LogInfo($"  Problematic pair: {kvp.Key} (adjusted {kvp.Value} times) - applying length separation");

                    var names = kvp.Key.Split('-');
                    var ext1 = allExtensions.FirstOrDefault(e => e.Location.Name == names[0]);
                    var ext2 = allExtensions.FirstOrDefault(e => e.Location.Name == names[1]);

                    if (ext1 != null && ext2 != null)
                    {
                        double dx1 = ext1.ExtendedPosition.X - ext1.OriginalPosition.X;
                        double dy1 = ext1.ExtendedPosition.Y - ext1.OriginalPosition.Y;
                        double length1 = Math.Sqrt(dx1 * dx1 + dy1 * dy1);

                        double dx2 = ext2.ExtendedPosition.X - ext2.OriginalPosition.X;
                        double dy2 = ext2.ExtendedPosition.Y - ext2.OriginalPosition.Y;
                        double length2 = Math.Sqrt(dx2 * dx2 + dy2 * dy2);

                        double minLength = _visualConfig.RadialExtension.MinimumLineLength;
                        double maxLength = _visualConfig.RadialExtension.ExtensionLineLength * 1.5;

                        double newLength1 = Math.Max(minLength, Math.Min(maxLength, length1 * 0.8));
                        double newLength2 = Math.Max(minLength, Math.Min(maxLength, length2 * 1.2));

                        double angle1Rad = ext1.Angle * (Math.PI / 180.0);
                        double angle2Rad = ext2.Angle * (Math.PI / 180.0);

                        ext1.ExtendedPosition = new Point(
                            ext1.OriginalPosition.X + newLength1 * Math.Sin(angle1Rad),
                            ext1.OriginalPosition.Y - newLength1 * Math.Cos(angle1Rad));

                        ext2.ExtendedPosition = new Point(
                            ext2.OriginalPosition.X + newLength2 * Math.Sin(angle2Rad),
                            ext2.OriginalPosition.Y - newLength2 * Math.Cos(angle2Rad));

                        _logger.LogInfo($"    Moderate length separation: {names[0]} {length1:F1}→{newLength1:F1}px, {names[1]} {length2:F1}→{newLength2:F1}px");
                    }
                }
            }

            if (!needsAdjustment)
                _logger.LogInfo($"[IterativeAdjustment] System stabilized after {iteration} iterations");
        }

        if (iteration >= maxIterations)
            _logger.LogInfo($"[IterativeAdjustment] Reached max iterations ({maxIterations}), accepting current state");

        // Log minimum distances between all line pairs
        _logger.LogInfo("[IterativeAdjustment] === Final Line Separation Analysis ===");
        double globalMinDistance = double.MaxValue;
        string closestPair = "";

        for (int i = 0; i < allExtensions.Count; i++)
        {
            for (int j = i + 1; j < allExtensions.Count; j++)
            {
                var ext1 = allExtensions[i];
                var ext2 = allExtensions[j];

                double distance = GeometryMath.CalculateMinimumDistanceBetweenLines(
                    ext1.OriginalPosition, ext1.ExtendedPosition,
                    ext2.OriginalPosition, ext2.ExtendedPosition);

                if (distance < globalMinDistance)
                {
                    globalMinDistance = distance;
                    closestPair = $"{ext1.Location.Name} - {ext2.Location.Name}";
                }

                if (distance < _visualConfig.LocationMarkerSize)
                    _logger.LogInfo($"  Close pair: {ext1.Location.Name} - {ext2.Location.Name}: {distance:F1}px");
            }
        }

        _logger.LogInfo($"[IterativeAdjustment] Minimum line separation: {globalMinDistance:F1}px ({closestPair})");
        _logger.LogInfo($"[IterativeAdjustment] Marker size: {_visualConfig.LocationMarkerSize:F1}px (radius: {_visualConfig.LocationMarkerSize / 2.0:F1}px)");
    }

    // -------------------------------------------------------------------------
    // Overlap adjustment
    // -------------------------------------------------------------------------

    /// <summary>
    /// Adjusts extension angles and lengths to prevent marker overlaps.
    /// Returns true if any adjustments were made.
    /// </summary>
    private bool AdjustForMarkerOverlaps(
        List<RadialExtension> allExtensions,
        double markerSize,
        HashSet<string>? protectedLocations = null)
    {
        double minGap      = markerSize * 2.5;
        double minAngleDiff = _visualConfig.RadialExtension.AngleNudgeThreshold;
        double angleNudge  = _visualConfig.RadialExtension.AngleNudgeAmount;
        const int maxPasses = 5;
        int pass = 0;
        bool hadAdjustments;

        bool logAngles  = _visualConfig.EnableDeveloperTools && _visualConfig.Debug.LogRadialExtensionAngles;
        bool logOverlaps = _visualConfig.EnableDeveloperTools && _visualConfig.Debug.LogRadialExtensionOverlaps;

        if (protectedLocations != null && protectedLocations.Count > 0 && logOverlaps)
            _logger.LogInfo($"[AdjustForMarkerOverlaps] Protected locations (won't adjust angles): {string.Join(", ", protectedLocations)}");

        if (logOverlaps)
            _logger.LogInfo($"[AdjustForMarkerOverlaps] Checking {allExtensions.Count} extensions for overlaps (minGap={minGap:F1}px, minAngle={minAngleDiff:F1}°)");

        if (logAngles)
            LogInitialAngles(allExtensions);

        do
        {
            pass++;
            hadAdjustments = false;

            hadAdjustments |= AdjustAnglesWithinGroups(allExtensions, minAngleDiff, angleNudge, protectedLocations, pass, logAngles);
            hadAdjustments |= AdjustPositionsAcrossExtensions(allExtensions, minGap, pass, logOverlaps);

            if (hadAdjustments && pass < maxPasses && logOverlaps)
                _logger.LogInfo($"  Pass {pass} complete, running another pass...");

        } while (hadAdjustments && pass < maxPasses);

        if (pass > 1 && logOverlaps)
            _logger.LogInfo($"[AdjustForMarkerOverlaps] Completed {pass} passes");

        if (logAngles)
            LogFinalAngles(allExtensions);

        return pass > 1;
    }

    /// <summary>
    /// Nudges angles apart within each group when pairs are too close angularly.
    /// Returns true if any angle was changed.
    /// </summary>
    private bool AdjustAnglesWithinGroups(
        List<RadialExtension> allExtensions,
        double minAngleDiff,
        double angleNudge,
        HashSet<string>? protectedLocations,
        int pass,
        bool logAngles)
    {
        bool hadAdjustments = false;

        var groupedExtensions = allExtensions.GroupBy(e => e.GroupId).ToList();

        foreach (var group in groupedExtensions)
        {
            var groupExtensions = group.OrderBy(e => e.Angle).ToList();

            for (int i = 0; i < groupExtensions.Count; i++)
            {
                for (int j = i + 1; j < groupExtensions.Count; j++)
                {
                    var ext1 = groupExtensions[i];
                    var ext2 = groupExtensions[j];

                    double angleDiff = ext2.Angle - ext1.Angle;
                    if (angleDiff < 0) angleDiff += 360.0;

                    if (angleDiff >= minAngleDiff) continue;

                    bool ext1Protected = protectedLocations != null && protectedLocations.Contains(ext1.Location.Name);
                    bool ext2Protected = protectedLocations != null && protectedLocations.Contains(ext2.Location.Name);

                    if (ext1Protected && ext2Protected)
                    {
                        if (pass == 1 && logAngles)
                            _logger.LogInfo($"  Group {ext1.GroupId}: SKIPPING angle adjustment (both protected): {ext1.Location.Name} and {ext2.Location.Name}");
                        continue;
                    }

                    if (pass == 1 && logAngles)
                        _logger.LogInfo($"  Group {ext1.GroupId}: Close angles: {ext1.Location.Name} ({ext1.Angle:F1}°) and {ext2.Location.Name} ({ext2.Angle:F1}°), diff={angleDiff:F1}°");

                    hadAdjustments = true;
                    double nudge = (angleDiff < 0.01) ? angleNudge : (angleNudge / 2.0);

                    if (ext1Protected)
                    {
                        ext2.Angle += nudge * 2.0;
                        if (pass == 1 && logAngles)
                            _logger.LogInfo($"    {ext1.Location.Name} protected, only nudging {ext2.Location.Name}");
                    }
                    else if (ext2Protected)
                    {
                        ext1.Angle -= nudge * 2.0;
                        if (pass == 1 && logAngles)
                            _logger.LogInfo($"    {ext2.Location.Name} protected, only nudging {ext1.Location.Name}");
                    }
                    else
                    {
                        ext1.Angle -= nudge;
                        ext2.Angle += nudge;
                    }

                    double length1 = CalculateCurrentLength(ext1);
                    double length2 = CalculateCurrentLength(ext2);

                    double angle1Rad = ext1.Angle * (Math.PI / 180.0);
                    double angle2Rad = ext2.Angle * (Math.PI / 180.0);

                    if (!ext1Protected)
                        ext1.ExtendedPosition = new Point(
                            ext1.OriginalPosition.X + length1 * Math.Sin(angle1Rad),
                            ext1.OriginalPosition.Y - length1 * Math.Cos(angle1Rad));

                    if (!ext2Protected)
                        ext2.ExtendedPosition = new Point(
                            ext2.OriginalPosition.X + length2 * Math.Sin(angle2Rad),
                            ext2.OriginalPosition.Y - length2 * Math.Cos(angle2Rad));

                    if (pass == 1 && logAngles)
                        _logger.LogInfo($"    Nudged angles: {ext1.Location.Name}={ext1.Angle:F1}°, {ext2.Location.Name}={ext2.Angle:F1}°");
                }
            }
        }

        return hadAdjustments;
    }

    /// <summary>
    /// Adjusts line lengths to resolve position overlaps between extended markers.
    /// Returns true if any lengths were changed.
    /// </summary>
    private bool AdjustPositionsAcrossExtensions(
        List<RadialExtension> allExtensions,
        double minGap,
        int pass,
        bool logOverlaps)
    {
        bool hadAdjustments = false;
        double minLineLength = _visualConfig.RadialExtension.MinimumLineLength;

        for (int i = 0; i < allExtensions.Count; i++)
        {
            for (int j = i + 1; j < allExtensions.Count; j++)
            {
                var ext1 = allExtensions[i];
                var ext2 = allExtensions[j];

                double dx = ext2.ExtendedPosition.X - ext1.ExtendedPosition.X;
                double dy = ext2.ExtendedPosition.Y - ext1.ExtendedPosition.Y;
                double distance = Math.Sqrt(dx * dx + dy * dy);

                if (distance >= minGap) continue;

                if (pass == 1 && logOverlaps)
                    _logger.LogInfo($"  Found overlap: {ext1.Location.Name} (Group {ext1.GroupId}) and {ext2.Location.Name} (Group {ext2.GroupId}), distance={distance:F1}px");

                hadAdjustments = true;

                double neededSeparation = minGap - distance;
                double angle1 = ext1.Angle * (Math.PI / 180.0);
                double angle2 = ext2.Angle * (Math.PI / 180.0);
                double currentLength1 = CalculateCurrentLength(ext1);
                double currentLength2 = CalculateCurrentLength(ext2);

                double angleDiff = Math.Abs(ext1.Angle - ext2.Angle);
                if (angleDiff > 180) angleDiff = 360 - angleDiff;

                double newLength1, newLength2;
                if (angleDiff < 90)
                {
                    if (currentLength1 > currentLength2)
                    {
                        newLength1 = currentLength1 + neededSeparation * 0.7;
                        newLength2 = Math.Max(minLineLength, currentLength2 - neededSeparation * 0.3);
                    }
                    else
                    {
                        newLength1 = Math.Max(minLineLength, currentLength1 - neededSeparation * 0.3);
                        newLength2 = currentLength2 + neededSeparation * 0.7;
                    }
                }
                else
                {
                    double adjustmentPerMarker = neededSeparation / 2.0;
                    newLength1 = Math.Max(minLineLength, currentLength1 - adjustmentPerMarker);
                    newLength2 = Math.Max(minLineLength, currentLength2 - adjustmentPerMarker);
                }

                if (pass == 1 && logOverlaps)
                    _logger.LogInfo($"    Pass {pass}: Adjusting lengths: {currentLength1:F1}→{newLength1:F1}, {currentLength2:F1}→{newLength2:F1} (angleDiff={angleDiff:F1}°)");

                ext1.ExtendedPosition = new Point(
                    ext1.OriginalPosition.X + newLength1 * Math.Sin(angle1),
                    ext1.OriginalPosition.Y - newLength1 * Math.Cos(angle1));

                ext2.ExtendedPosition = new Point(
                    ext2.OriginalPosition.X + newLength2 * Math.Sin(angle2),
                    ext2.OriginalPosition.Y - newLength2 * Math.Cos(angle2));
            }
        }

        return hadAdjustments;
    }

    // -------------------------------------------------------------------------
    // Intersection fixing
    // -------------------------------------------------------------------------

    /// <summary>
    /// Fixes line intersections by rotating angles into available space.
    /// Returns true if any intersections were found and fixed.
    /// </summary>
    private bool FixLineIntersections(
        List<RadialExtension> allExtensions,
        List<string> intersectingPairs,
        double nudgeMultiplier)
    {
        bool foundAny = false;
        int totalFixed = 0;
        double markerRadius = _visualConfig.LocationMarkerSize / 2.0;

        for (int i = 0; i < allExtensions.Count; i++)
        {
            var ext1 = allExtensions[i];

            for (int j = i + 1; j < allExtensions.Count; j++)
            {
                var ext2 = allExtensions[j];

                bool hasIssue = false;
                string issueType = "";

                if (GeometryMath.DoLineSegmentsIntersect(
                        ext1.OriginalPosition, ext1.ExtendedPosition,
                        ext2.OriginalPosition, ext2.ExtendedPosition))
                {
                    hasIssue = true; issueType = "INTERSECTION";
                }
                else if (GeometryMath.DoesLinePassTooCloseToMarker(
                        ext1.OriginalPosition, ext1.ExtendedPosition,
                        ext2.ExtendedPosition, markerRadius))
                {
                    hasIssue = true; issueType = "LINE→MARKER";
                }
                else if (GeometryMath.DoesLinePassTooCloseToMarker(
                        ext2.OriginalPosition, ext2.ExtendedPosition,
                        ext1.ExtendedPosition, markerRadius))
                {
                    hasIssue = true; issueType = "MARKER←LINE";
                }

                if (!hasIssue) continue;

                foundAny = true;
                totalFixed++;

                string pairKey = string.Compare(ext1.Location.Name, ext2.Location.Name) < 0
                    ? $"{ext1.Location.Name}-{ext2.Location.Name}"
                    : $"{ext2.Location.Name}-{ext1.Location.Name}";
                intersectingPairs.Add(pairKey);

                _logger.LogInfo($"  [{issueType} #{totalFixed}] {ext1.Location.Name} ({ext1.Angle:F1}°) and {ext2.Location.Name} ({ext2.Angle:F1}°)");

                double maxTestRotation = 30.0;
                double safeRotation1CW  = GeometryMath.FindSafeAngleRotation(ext1, allExtensions, clockwise: true,  maxTestRotation, markerRadius);
                double safeRotation1CCW = GeometryMath.FindSafeAngleRotation(ext1, allExtensions, clockwise: false, maxTestRotation, markerRadius);
                double safeRotation2CW  = GeometryMath.FindSafeAngleRotation(ext2, allExtensions, clockwise: true,  maxTestRotation, markerRadius);
                double safeRotation2CCW = GeometryMath.FindSafeAngleRotation(ext2, allExtensions, clockwise: false, maxTestRotation, markerRadius);

                _logger.LogInfo($"    Safe rotations - {ext1.Location.Name}: CW={safeRotation1CW:F1}° CCW={safeRotation1CCW:F1}°, {ext2.Location.Name}: CW={safeRotation2CW:F1}° CCW={safeRotation2CCW:F1}°");

                double baseNudge = 3.0 * nudgeMultiplier;
                bool rotationApplied = false;

                if (safeRotation1CW > 5.0 && safeRotation1CW > safeRotation1CCW && safeRotation1CW > safeRotation2CW && safeRotation1CW > safeRotation2CCW)
                {
                    double nudge = Math.Min(baseNudge * 4, safeRotation1CW * 0.8);
                    ext1.Angle += nudge;
                    _logger.LogInfo($"    Strategy: Rotate {ext1.Location.Name} CW by {nudge:F1}° (safe space: {safeRotation1CW:F1}°)");
                    rotationApplied = true;
                }
                else if (safeRotation1CCW > 5.0 && safeRotation1CCW > safeRotation2CW && safeRotation1CCW > safeRotation2CCW)
                {
                    double nudge = Math.Min(baseNudge * 4, safeRotation1CCW * 0.8);
                    ext1.Angle -= nudge;
                    _logger.LogInfo($"    Strategy: Rotate {ext1.Location.Name} CCW by {nudge:F1}° (safe space: {safeRotation1CCW:F1}°)");
                    rotationApplied = true;
                }
                else if (safeRotation2CW > 5.0 && safeRotation2CW > safeRotation2CCW)
                {
                    double nudge = Math.Min(baseNudge * 4, safeRotation2CW * 0.8);
                    ext2.Angle += nudge;
                    _logger.LogInfo($"    Strategy: Rotate {ext2.Location.Name} CW by {nudge:F1}° (safe space: {safeRotation2CW:F1}°)");
                    rotationApplied = true;
                }
                else if (safeRotation2CCW > 5.0)
                {
                    double nudge = Math.Min(baseNudge * 4, safeRotation2CCW * 0.8);
                    ext2.Angle -= nudge;
                    _logger.LogInfo($"    Strategy: Rotate {ext2.Location.Name} CCW by {nudge:F1}° (safe space: {safeRotation2CCW:F1}°)");
                    rotationApplied = true;
                }

                if (!rotationApplied)
                {
                    ext1.Angle -= baseNudge * 0.5;
                    ext2.Angle += baseNudge * 0.5;
                    _logger.LogInfo("    Strategy: Minimal nudge apart (no safe rotation space found)");
                }

                double angle1Rad = ext1.Angle * (Math.PI / 180.0);
                double angle2Rad = ext2.Angle * (Math.PI / 180.0);

                double dx1 = ext1.ExtendedPosition.X - ext1.OriginalPosition.X;
                double dy1 = ext1.ExtendedPosition.Y - ext1.OriginalPosition.Y;
                double length1 = Math.Sqrt(dx1 * dx1 + dy1 * dy1);

                double dx2 = ext2.ExtendedPosition.X - ext2.OriginalPosition.X;
                double dy2 = ext2.ExtendedPosition.Y - ext2.OriginalPosition.Y;
                double length2 = Math.Sqrt(dx2 * dx2 + dy2 * dy2);

                ext1.ExtendedPosition = new Point(
                    ext1.OriginalPosition.X + length1 * Math.Sin(angle1Rad),
                    ext1.OriginalPosition.Y - length1 * Math.Cos(angle1Rad));

                ext2.ExtendedPosition = new Point(
                    ext2.OriginalPosition.X + length2 * Math.Sin(angle2Rad),
                    ext2.OriginalPosition.Y - length2 * Math.Cos(angle2Rad));

                _logger.LogInfo($"    Result: {ext1.Location.Name} now at {ext1.Angle:F1}°, {ext2.Location.Name} now at {ext2.Angle:F1}°");
            }
        }

        if (foundAny)
            _logger.LogInfo($"  Fixed {totalFixed} intersections");

        return foundAny;
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static double CalculateCurrentLength(RadialExtension extension)
    {
        double dx = extension.ExtendedPosition.X - extension.OriginalPosition.X;
        double dy = extension.ExtendedPosition.Y - extension.OriginalPosition.Y;
        return Math.Sqrt(dx * dx + dy * dy);
    }

    private void LogInitialAngles(List<RadialExtension> allExtensions)
    {
        var groupedExtensions = allExtensions.GroupBy(e => e.GroupId).ToList();
        foreach (var group in groupedExtensions)
        {
            var groupExtensions = group.OrderBy(e => e.Angle).ToList();
            _logger.LogInfo($"  Group {group.Key} initial angles:");
            for (int i = 0; i < groupExtensions.Count; i++)
            {
                var ext = groupExtensions[i];
                double nextAngleDiff = (i < groupExtensions.Count - 1)
                    ? groupExtensions[i + 1].Angle - ext.Angle
                    : 0;
                _logger.LogInfo($"    {ext.Location.Name}: {ext.Angle:F2}° (next diff: {nextAngleDiff:F2}°)");
            }
        }
    }

    private void LogFinalAngles(List<RadialExtension> allExtensions)
    {
        _logger.LogInfo("[AdjustForMarkerOverlaps] Final angles:");
        var groupedExtensions = allExtensions.GroupBy(e => e.GroupId).ToList();
        foreach (var group in groupedExtensions)
        {
            var groupExtensions = group.OrderBy(e => e.Angle).ToList();
            double minAngleInGroup = 360.0;
            for (int i = 0; i < groupExtensions.Count - 1; i++)
            {
                double diff = groupExtensions[i + 1].Angle - groupExtensions[i].Angle;
                if (diff < minAngleInGroup) minAngleInGroup = diff;
            }
            _logger.LogInfo($"  Group {group.Key}: {groupExtensions.Count} markers, smallest angle separation: {minAngleInGroup:F2}°");
        }
    }
}
