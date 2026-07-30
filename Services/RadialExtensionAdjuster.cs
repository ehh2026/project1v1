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
        var state = new IterativeAdjustmentState();

        _logger.LogInfo($"[IterativeAdjustment] Starting iterative adjustment for {allExtensions.Count} extensions");

        while (state.NeedsAdjustment && state.Iteration < maxIterations)
        {
            RunAdjustmentIteration(allExtensions, markerSize, state);
        }

        if (state.Iteration >= maxIterations)
            _logger.LogInfo($"[IterativeAdjustment] Reached max iterations ({maxIterations}), accepting current state");

        LogFinalLineSeparation(allExtensions);
    }

    private void RunAdjustmentIteration(
        List<RadialExtension> allExtensions,
        double markerSize,
        IterativeAdjustmentState state)
    {
        state.Iteration++;
        state.NeedsAdjustment = false;

        _logger.LogInfo($"[IterativeAdjustment] === Iteration {state.Iteration} (nudge multiplier: {state.NudgeMultiplier:F2}) ===");

        if (AdjustForMarkerOverlaps(allExtensions, markerSize, state.ProtectedFromOverlapAdjustment))
        {
            state.NeedsAdjustment = true;
            _logger.LogInfo("[IterativeAdjustment] Overlaps were adjusted");
        }

        var intersectingPairs = new List<string>();
        if (FixLineIntersections(allExtensions, intersectingPairs, state.NudgeMultiplier))
        {
            state.NeedsAdjustment = true;
            _logger.LogInfo("[IterativeAdjustment] Intersections were fixed");
            TrackIntersectingPairs(intersectingPairs, state);
        }

        ApplyOscillationLengthSeparation(allExtensions, state);

        if (!state.NeedsAdjustment)
            _logger.LogInfo($"[IterativeAdjustment] System stabilized after {state.Iteration} iterations");
    }

    private static void TrackIntersectingPairs(
        IEnumerable<string> intersectingPairs,
        IterativeAdjustmentState state)
    {
        foreach (var pair in intersectingPairs)
        {
            state.PairAdjustmentCount[pair] = state.PairAdjustmentCount.TryGetValue(pair, out var count)
                ? count + 1
                : 1;

            var names = pair.Split('-');
            state.ProtectedFromOverlapAdjustment.Add(names[0]);
            state.ProtectedFromOverlapAdjustment.Add(names[1]);
        }
    }

    private void ApplyOscillationLengthSeparation(
        List<RadialExtension> allExtensions,
        IterativeAdjustmentState state)
    {
        int maxAdjustments = state.PairAdjustmentCount.Values.Any()
            ? state.PairAdjustmentCount.Values.Max()
            : 0;
        if (maxAdjustments < 3)
            return;

        state.NudgeMultiplier *= 0.5;
        _logger.LogInfo($"[IterativeAdjustment] OSCILLATION DETECTED (max adjustments: {maxAdjustments}), reducing nudge multiplier to {state.NudgeMultiplier:F2}");

        foreach (var kvp in state.PairAdjustmentCount.Where(x => x.Value >= 3))
        {
            ApplyLengthSeparationForPair(allExtensions, kvp.Key, kvp.Value);
        }
    }

    private void ApplyLengthSeparationForPair(
        List<RadialExtension> allExtensions,
        string pairKey,
        int adjustmentCount)
    {
        _logger.LogInfo($"  Problematic pair: {pairKey} (adjusted {adjustmentCount} times) - applying length separation");

        var names = pairKey.Split('-');
        var ext1 = allExtensions.FirstOrDefault(e => e.Location.Name == names[0]);
        var ext2 = allExtensions.FirstOrDefault(e => e.Location.Name == names[1]);

        if (ext1 == null || ext2 == null)
            return;

        double length1 = CalculateCurrentLength(ext1);
        double length2 = CalculateCurrentLength(ext2);
        double minLength = _visualConfig.RadialExtension.MinimumLineLength;
        double maxLength = _visualConfig.RadialExtension.ExtensionLineLength * 1.5;
        double newLength1 = Math.Max(minLength, Math.Min(maxLength, length1 * 0.8));
        double newLength2 = Math.Max(minLength, Math.Min(maxLength, length2 * 1.2));

        SetLengthPreservingAngle(ext1, newLength1);
        SetLengthPreservingAngle(ext2, newLength2);
        _logger.LogInfo($"    Moderate length separation: {names[0]} {length1:F1}->{newLength1:F1}px, {names[1]} {length2:F1}->{newLength2:F1}px");
    }

    private static void SetLengthPreservingAngle(RadialExtension extension, double length)
    {
        double angleRad = extension.Angle * (Math.PI / 180.0);
        extension.ExtendedPosition = new Point(
            extension.OriginalPosition.X + length * Math.Sin(angleRad),
            extension.OriginalPosition.Y - length * Math.Cos(angleRad));
    }

    private void LogFinalLineSeparation(List<RadialExtension> allExtensions)
    {
        _logger.LogInfo("[IterativeAdjustment] === Final Line Separation Analysis ===");
        double globalMinDistance = double.MaxValue;
        string closestPair = "";

        for (int i = 0; i < allExtensions.Count; i++)
        {
            for (int j = i + 1; j < allExtensions.Count; j++)
            {
                var separation = MeasureLineSeparation(allExtensions[i], allExtensions[j]);
                if (separation.Distance < globalMinDistance)
                {
                    globalMinDistance = separation.Distance;
                    closestPair = separation.PairName;
                }

                if (separation.Distance < _visualConfig.LocationMarkerSize)
                    _logger.LogInfo($"  Close pair: {separation.PairName}: {separation.Distance:F1}px");
            }
        }

        _logger.LogInfo($"[IterativeAdjustment] Minimum line separation: {globalMinDistance:F1}px ({closestPair})");
        _logger.LogInfo($"[IterativeAdjustment] Marker size: {_visualConfig.LocationMarkerSize:F1}px (radius: {_visualConfig.LocationMarkerSize / 2.0:F1}px)");
    }

    private static LineSeparation MeasureLineSeparation(RadialExtension ext1, RadialExtension ext2)
    {
        double distance = GeometryMath.CalculateMinimumDistanceBetweenLines(
            ext1.OriginalPosition, ext1.ExtendedPosition,
            ext2.OriginalPosition, ext2.ExtendedPosition);
        return new LineSeparation(distance, $"{ext1.Location.Name} - {ext2.Location.Name}");
    }

    private sealed class IterativeAdjustmentState
    {
        public int Iteration { get; set; }
        public bool NeedsAdjustment { get; set; } = true;
        public double NudgeMultiplier { get; set; } = 1.0;
        public Dictionary<string, int> PairAdjustmentCount { get; } = new();
        public HashSet<string> ProtectedFromOverlapAdjustment { get; } = new();
    }

    private readonly record struct LineSeparation(double Distance, string PairName);
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
        const int maxPasses = 5;
        var context = CreateOverlapContext(markerSize, protectedLocations);
        LogOverlapStart(allExtensions, context);

        do
        {
            context.Pass++;
            context.HadAdjustments = RunOverlapAdjustmentPass(allExtensions, protectedLocations, context);
            LogOverlapPass(context, maxPasses);
        } while (context.HadAdjustments && context.Pass < maxPasses);

        LogOverlapCompletion(allExtensions, context);
        return context.Pass > 1;
    }

    private OverlapAdjustmentContext CreateOverlapContext(
        double markerSize,
        HashSet<string>? protectedLocations) =>
        new(
            markerSize * 2.5,
            _visualConfig.RadialExtension.AngleNudgeThreshold,
            _visualConfig.RadialExtension.AngleNudgeAmount,
            _visualConfig.EnableDeveloperTools && _visualConfig.Debug.LogRadialExtensionAngles,
            _visualConfig.EnableDeveloperTools && _visualConfig.Debug.LogRadialExtensionOverlaps,
            protectedLocations);

    private void LogOverlapStart(
        List<RadialExtension> allExtensions,
        OverlapAdjustmentContext context)
    {
        if (context.ProtectedLocations is { Count: > 0 } && context.LogOverlaps)
            _logger.LogInfo($"[AdjustForMarkerOverlaps] Protected locations (won't adjust angles): {string.Join(", ", context.ProtectedLocations)}");

        if (context.LogOverlaps)
            _logger.LogInfo($"[AdjustForMarkerOverlaps] Checking {allExtensions.Count} extensions for overlaps (minGap={context.MinGap:F1}px, minAngle={context.MinAngleDiff:F1}deg)");

        if (context.LogAngles)
            LogInitialAngles(allExtensions);
    }

    private bool RunOverlapAdjustmentPass(
        List<RadialExtension> allExtensions,
        HashSet<string>? protectedLocations,
        OverlapAdjustmentContext context) =>
        AdjustAnglesWithinGroups(
            allExtensions,
            context.MinAngleDiff,
            context.AngleNudge,
            protectedLocations,
            context.Pass,
            context.LogAngles) |
        AdjustPositionsAcrossExtensions(
            allExtensions,
            context.MinGap,
            context.Pass,
            context.LogOverlaps);

    private void LogOverlapPass(OverlapAdjustmentContext context, int maxPasses)
    {
        if (context.HadAdjustments && context.Pass < maxPasses && context.LogOverlaps)
            _logger.LogInfo($"  Pass {context.Pass} complete, running another pass...");
    }

    private void LogOverlapCompletion(
        List<RadialExtension> allExtensions,
        OverlapAdjustmentContext context)
    {
        if (context.Pass > 1 && context.LogOverlaps)
            _logger.LogInfo($"[AdjustForMarkerOverlaps] Completed {context.Pass} passes");

        if (context.LogAngles)
            LogFinalAngles(allExtensions);
    }

    private sealed class OverlapAdjustmentContext
    {
        public OverlapAdjustmentContext(
            double minGap,
            double minAngleDiff,
            double angleNudge,
            bool logAngles,
            bool logOverlaps,
            HashSet<string>? protectedLocations)
        {
            MinGap = minGap;
            MinAngleDiff = minAngleDiff;
            AngleNudge = angleNudge;
            LogAngles = logAngles;
            LogOverlaps = logOverlaps;
            ProtectedLocations = protectedLocations;
        }

        public double MinGap { get; }
        public double MinAngleDiff { get; }
        public double AngleNudge { get; }
        public bool LogAngles { get; }
        public bool LogOverlaps { get; }
        public HashSet<string>? ProtectedLocations { get; }
        public int Pass { get; set; }
        public bool HadAdjustments { get; set; }
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
        var groupedExtensions = allExtensions.GroupBy(e => e.GroupId).ToList();
        return groupedExtensions.Any(group => AdjustAnglesWithinGroup(
            group.OrderBy(e => e.Angle).ToList(),
            minAngleDiff,
            angleNudge,
            protectedLocations,
            pass,
            logAngles));
    }

    private bool AdjustAnglesWithinGroup(
        IReadOnlyList<RadialExtension> groupExtensions,
        double minAngleDiff,
        double angleNudge,
        HashSet<string>? protectedLocations,
        int pass,
        bool logAngles)
    {
        var hadAdjustments = false;
        for (int i = 0; i < groupExtensions.Count; i++)
        {
            for (int j = i + 1; j < groupExtensions.Count; j++)
            {
                hadAdjustments |= TryAdjustAnglePair(
                    groupExtensions[i],
                    groupExtensions[j],
                    minAngleDiff,
                    angleNudge,
                    protectedLocations,
                    pass,
                    logAngles);
            }
        }
        return hadAdjustments;
    }

    private bool TryAdjustAnglePair(
        RadialExtension ext1,
        RadialExtension ext2,
        double minAngleDiff,
        double angleNudge,
        HashSet<string>? protectedLocations,
        int pass,
        bool logAngles)
    {
        double angleDiff = NormalizeAngleDiff(ext1.Angle, ext2.Angle);
        if (angleDiff >= minAngleDiff)
            return false;

        bool ext1Protected = IsProtected(ext1, protectedLocations);
        bool ext2Protected = IsProtected(ext2, protectedLocations);
        if (ext1Protected && ext2Protected)
        {
            LogBothProtected(ext1, ext2, pass, logAngles);
            return false;
        }

        LogCloseAngles(ext1, ext2, angleDiff, pass, logAngles);
        ApplyAngleNudge(ext1, ext2, angleDiff, angleNudge, ext1Protected, ext2Protected, pass, logAngles);
        ReprojectAfterAngleNudge(ext1, ext1Protected);
        ReprojectAfterAngleNudge(ext2, ext2Protected);
        LogNudgedAngles(ext1, ext2, pass, logAngles);
        return true;
    }

    private static double NormalizeAngleDiff(double angle1, double angle2)
    {
        double angleDiff = angle2 - angle1;
        return angleDiff < 0 ? angleDiff + 360.0 : angleDiff;
    }

    private static bool IsProtected(
        RadialExtension extension,
        HashSet<string>? protectedLocations) =>
        protectedLocations != null && protectedLocations.Contains(extension.Location.Name);

    private void LogBothProtected(
        RadialExtension ext1,
        RadialExtension ext2,
        int pass,
        bool logAngles)
    {
        if (pass == 1 && logAngles)
            _logger.LogInfo($"  Group {ext1.GroupId}: SKIPPING angle adjustment (both protected): {ext1.Location.Name} and {ext2.Location.Name}");
    }

    private void LogCloseAngles(
        RadialExtension ext1,
        RadialExtension ext2,
        double angleDiff,
        int pass,
        bool logAngles)
    {
        if (pass == 1 && logAngles)
            _logger.LogInfo($"  Group {ext1.GroupId}: Close angles: {ext1.Location.Name} ({ext1.Angle:F1}deg) and {ext2.Location.Name} ({ext2.Angle:F1}deg), diff={angleDiff:F1}deg");
    }

    private void ApplyAngleNudge(
        RadialExtension ext1,
        RadialExtension ext2,
        double angleDiff,
        double angleNudge,
        bool ext1Protected,
        bool ext2Protected,
        int pass,
        bool logAngles)
    {
        double nudge = angleDiff < 0.01 ? angleNudge : angleNudge / 2.0;

        if (ext1Protected)
        {
            ext2.Angle += nudge * 2.0;
            LogProtectedNudge(ext1, ext2, pass, logAngles);
        }
        else if (ext2Protected)
        {
            ext1.Angle -= nudge * 2.0;
            LogProtectedNudge(ext2, ext1, pass, logAngles);
        }
        else
        {
            ext1.Angle -= nudge;
            ext2.Angle += nudge;
        }
    }

    private void LogProtectedNudge(
        RadialExtension protectedExtension,
        RadialExtension nudgedExtension,
        int pass,
        bool logAngles)
    {
        if (pass == 1 && logAngles)
            _logger.LogInfo($"    {protectedExtension.Location.Name} protected, only nudging {nudgedExtension.Location.Name}");
    }

    private static void ReprojectAfterAngleNudge(RadialExtension extension, bool isProtected)
    {
        if (isProtected)
            return;

        double length = CalculateCurrentLength(extension);
        double angleRad = extension.Angle * (Math.PI / 180.0);
        extension.ExtendedPosition = new Point(
            extension.OriginalPosition.X + length * Math.Sin(angleRad),
            extension.OriginalPosition.Y - length * Math.Cos(angleRad));
    }

    private void LogNudgedAngles(
        RadialExtension ext1,
        RadialExtension ext2,
        int pass,
        bool logAngles)
    {
        if (pass == 1 && logAngles)
            _logger.LogInfo($"    Nudged angles: {ext1.Location.Name}={ext1.Angle:F1}deg, {ext2.Location.Name}={ext2.Angle:F1}deg");
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
        var totalFixed = 0;
        double markerRadius = _visualConfig.LocationMarkerSize / 2.0;

        for (int i = 0; i < allExtensions.Count; i++)
        {
            for (int j = i + 1; j < allExtensions.Count; j++)
            {
                if (TryFixLineIssue(
                        allExtensions[i],
                        allExtensions[j],
                        allExtensions,
                        intersectingPairs,
                        nudgeMultiplier,
                        markerRadius,
                        ref totalFixed))
                {
                    continue;
                }
            }
        }

        if (totalFixed > 0)
            _logger.LogInfo($"  Fixed {totalFixed} intersections");

        return totalFixed > 0;
    }

    private bool TryFixLineIssue(
        RadialExtension ext1,
        RadialExtension ext2,
        List<RadialExtension> allExtensions,
        List<string> intersectingPairs,
        double nudgeMultiplier,
        double markerRadius,
        ref int totalFixed)
    {
        if (!TryDetectLineIssue(ext1, ext2, markerRadius, out var issueType))
            return false;

        totalFixed++;
        intersectingPairs.Add(BuildPairKey(ext1, ext2));
        _logger.LogInfo($"  [{issueType} #{totalFixed}] {ext1.Location.Name} ({ext1.Angle:F1}deg) and {ext2.Location.Name} ({ext2.Angle:F1}deg)");

        var rotations = FindSafeRotations(ext1, ext2, allExtensions, markerRadius);
        LogSafeRotations(ext1, ext2, rotations);
        ApplyRotationStrategy(ext1, ext2, rotations, 3.0 * nudgeMultiplier);
        ReprojectPair(ext1, ext2);
        _logger.LogInfo($"    Result: {ext1.Location.Name} now at {ext1.Angle:F1}deg, {ext2.Location.Name} now at {ext2.Angle:F1}deg");
        return true;
    }

    private static bool TryDetectLineIssue(
        RadialExtension ext1,
        RadialExtension ext2,
        double markerRadius,
        out string issueType)
    {
        if (GeometryMath.DoLineSegmentsIntersect(
                ext1.OriginalPosition, ext1.ExtendedPosition,
                ext2.OriginalPosition, ext2.ExtendedPosition))
        {
            issueType = "INTERSECTION";
            return true;
        }

        if (GeometryMath.DoesLinePassTooCloseToMarker(
                ext1.OriginalPosition, ext1.ExtendedPosition,
                ext2.ExtendedPosition, markerRadius))
        {
            issueType = "LINE->MARKER";
            return true;
        }

        if (GeometryMath.DoesLinePassTooCloseToMarker(
                ext2.OriginalPosition, ext2.ExtendedPosition,
                ext1.ExtendedPosition, markerRadius))
        {
            issueType = "MARKER<-LINE";
            return true;
        }

        issueType = string.Empty;
        return false;
    }

    private static string BuildPairKey(RadialExtension ext1, RadialExtension ext2) =>
        string.Compare(ext1.Location.Name, ext2.Location.Name, StringComparison.Ordinal) < 0
            ? $"{ext1.Location.Name}-{ext2.Location.Name}"
            : $"{ext2.Location.Name}-{ext1.Location.Name}";

    private static SafeRotations FindSafeRotations(
        RadialExtension ext1,
        RadialExtension ext2,
        List<RadialExtension> allExtensions,
        double markerRadius)
    {
        const double maxTestRotation = 30.0;
        return new SafeRotations(
            GeometryMath.FindSafeAngleRotation(ext1, allExtensions, clockwise: true, maxTestRotation, markerRadius),
            GeometryMath.FindSafeAngleRotation(ext1, allExtensions, clockwise: false, maxTestRotation, markerRadius),
            GeometryMath.FindSafeAngleRotation(ext2, allExtensions, clockwise: true, maxTestRotation, markerRadius),
            GeometryMath.FindSafeAngleRotation(ext2, allExtensions, clockwise: false, maxTestRotation, markerRadius));
    }

    private void LogSafeRotations(RadialExtension ext1, RadialExtension ext2, SafeRotations rotations)
    {
        _logger.LogInfo($"    Safe rotations - {ext1.Location.Name}: CW={rotations.Ext1Clockwise:F1}deg CCW={rotations.Ext1CounterClockwise:F1}deg, {ext2.Location.Name}: CW={rotations.Ext2Clockwise:F1}deg CCW={rotations.Ext2CounterClockwise:F1}deg");
    }

    private void ApplyRotationStrategy(
        RadialExtension ext1,
        RadialExtension ext2,
        SafeRotations rotations,
        double baseNudge)
    {
        if (TryApplyPreferredRotation(ext1, rotations.Ext1Clockwise, baseNudge, clockwise: true, rotations.Ext1CounterClockwise, rotations.Ext2Clockwise, rotations.Ext2CounterClockwise))
            return;
        if (TryApplyPreferredRotation(ext1, rotations.Ext1CounterClockwise, baseNudge, clockwise: false, rotations.Ext2Clockwise, rotations.Ext2CounterClockwise))
            return;
        if (TryApplyPreferredRotation(ext2, rotations.Ext2Clockwise, baseNudge, clockwise: true, rotations.Ext2CounterClockwise))
            return;
        if (TryApplyFallbackRotation(ext2, rotations.Ext2CounterClockwise, baseNudge))
            return;

        ext1.Angle -= baseNudge * 0.5;
        ext2.Angle += baseNudge * 0.5;
        _logger.LogInfo("    Strategy: Minimal nudge apart (no safe rotation space found)");
    }

    private bool TryApplyPreferredRotation(
        RadialExtension extension,
        double safeRotation,
        double baseNudge,
        bool clockwise,
        params double[] competingRotations)
    {
        if (safeRotation <= 5.0 || competingRotations.Any(rotation => safeRotation <= rotation))
            return false;

        double nudge = Math.Min(baseNudge * 4, safeRotation * 0.8);
        extension.Angle += clockwise ? nudge : -nudge;
        _logger.LogInfo($"    Strategy: Rotate {extension.Location.Name} {(clockwise ? "CW" : "CCW")} by {nudge:F1}deg (safe space: {safeRotation:F1}deg)");
        return true;
    }

    private bool TryApplyFallbackRotation(
        RadialExtension extension,
        double safeRotation,
        double baseNudge)
    {
        if (safeRotation <= 5.0)
            return false;

        double nudge = Math.Min(baseNudge * 4, safeRotation * 0.8);
        extension.Angle -= nudge;
        _logger.LogInfo($"    Strategy: Rotate {extension.Location.Name} CCW by {nudge:F1}deg (safe space: {safeRotation:F1}deg)");
        return true;
    }

    private static void ReprojectPair(RadialExtension ext1, RadialExtension ext2)
    {
        ReprojectCurrentLength(ext1);
        ReprojectCurrentLength(ext2);
    }

    private static void ReprojectCurrentLength(RadialExtension extension)
    {
        double angleRad = extension.Angle * (Math.PI / 180.0);
        double length = CalculateCurrentLength(extension);
        extension.ExtendedPosition = new Point(
            extension.OriginalPosition.X + length * Math.Sin(angleRad),
            extension.OriginalPosition.Y - length * Math.Cos(angleRad));
    }

    private readonly record struct SafeRotations(
        double Ext1Clockwise,
        double Ext1CounterClockwise,
        double Ext2Clockwise,
        double Ext2CounterClockwise);
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
