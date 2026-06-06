using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using InteractiveWorldMap.Models;
using InteractiveWorldMap.Utilities;

namespace InteractiveWorldMap.Services;

/// <summary>
/// Manages manual-layout editing state and data operations.
/// All WPF/UI manipulation (cursor changes, panel visibility, Canvas positioning)
/// remains in the caller (MainWindow).
/// </summary>
public sealed class LayoutEditorController
{
    private const double ExtensionLineThreshold = 5.0;

    private readonly IManualLayoutManager _layoutManager;
    private readonly VisualConfig        _visualConfig;
    private readonly ILogger             _logger;

    // ─── Observable state ────────────────────────────────────────────────────

    public bool    IsEditMode          { get; private set; }
    public bool    IsManualLayoutActive { get; private set; }
    public string? CurrentLayoutKey    { get; private set; }

    public sealed record LayoutMarkerApplication(
        string LocationName,
        Point OriginalPosition,
        Point ExtendedPosition,
        bool RequiresExtensionLine)
    {
        /// <summary>
        /// Source-image coordinates of the extended position (from <see cref="ManualLayoutMarker"/>).
        /// When set, the caller should re-project via <c>viewport.SourceToScreen</c> instead of
        /// using <see cref="ExtendedPosition"/> directly, so seeds work at any window size.
        /// </summary>
        public double? SourceExtendedX { get; init; }
        public double? SourceExtendedY { get; init; }
    }

    public event Action? EditModeEntered;
    public event Action? EditModeExited;
    public event Action<bool>? ManualLayoutActivityChanged;

    // ─── Constructor ─────────────────────────────────────────────────────────

    public LayoutEditorController(
        IManualLayoutManager layoutManager,
        VisualConfig        visualConfig,
        ILogger             logger)
    {
        _layoutManager = layoutManager ?? throw new ArgumentNullException(nameof(layoutManager));
        _visualConfig  = visualConfig  ?? throw new ArgumentNullException(nameof(visualConfig));
        _logger        = logger        ?? throw new ArgumentNullException(nameof(logger));
    }

    // ─── State transitions ───────────────────────────────────────────────────

    public void EnterEditMode()
    {
        IsEditMode = true;
        EditModeEntered?.Invoke();
    }

    public void ExitEditMode()
    {
        IsEditMode = false;
        EditModeExited?.Invoke();
    }

    public void SetLayoutKey(string? key)    => CurrentLayoutKey = key;

    public void SetManualLayoutActive(bool active)
    {
        IsManualLayoutActive = active;
        ManualLayoutActivityChanged?.Invoke(active);
    }

    // ─── Data operations ─────────────────────────────────────────────────────

    /// <summary>Loads the preferred layout variant for <paramref name="key"/>, or null if none exists.</summary>
    public ManualLayout? TryLoad(string key) => _layoutManager.LoadLayout(key);

    /// <summary>
    /// Creates UI-neutral instructions for applying a saved layout to currently visible markers.
    /// The caller owns WPF marker lookup, Canvas positioning, and extension-line rendering.
    /// </summary>
    public List<LayoutMarkerApplication> CreateLayoutApplications(
        ManualLayout layout,
        IEnumerable<string> visibleLocationNames)
    {
        if (layout == null) throw new ArgumentNullException(nameof(layout));
        if (visibleLocationNames == null) throw new ArgumentNullException(nameof(visibleLocationNames));

        var visibleNames = new HashSet<string>(visibleLocationNames, StringComparer.Ordinal);
        var applications = new List<LayoutMarkerApplication>();

        foreach (var layoutMarker in layout.Markers)
        {
            if (!visibleNames.Contains(layoutMarker.LocationName))
            {
                _logger.LogWarning($"  Marker not found for location: {layoutMarker.LocationName}");
                continue;
            }

            double dx = layoutMarker.ExtendedPosition.X - layoutMarker.OriginalPosition.X;
            double dy = layoutMarker.ExtendedPosition.Y - layoutMarker.OriginalPosition.Y;
            double distance = Math.Sqrt(dx * dx + dy * dy);

            applications.Add(new LayoutMarkerApplication(
                layoutMarker.LocationName,
                layoutMarker.OriginalPosition,
                layoutMarker.ExtendedPosition,
                distance > ExtensionLineThreshold)
            {
                SourceExtendedX = layoutMarker.SourceExtendedX,
                SourceExtendedY = layoutMarker.SourceExtendedY
            });

            _logger.LogInfo($"  Applied layout for: {layoutMarker.LocationName}");
        }

        return applications;
    }

    /// <summary>
    /// Builds a <see cref="RadialExtension"/> list from per-marker screen coordinates.
    /// The caller is responsible for extracting Canvas positions (WPF concern).
    /// </summary>
    public static List<RadialExtension> BuildExtensions(
        IEnumerable<(Location Location, Point MarkerCenter, Point OriginalScreen)> markerData)
    {
        if (markerData == null) throw new ArgumentNullException(nameof(markerData));

        var extensions = new List<RadialExtension>();
        foreach (var (location, markerCenter, originalScreen) in markerData)
        {
            double dx    = markerCenter.X - originalScreen.X;
            double dy    = markerCenter.Y - originalScreen.Y;
            double angle = Math.Atan2(dy, dx) * (180.0 / Math.PI);
            extensions.Add(new RadialExtension
            {
                Location         = location,
                OriginalPosition = originalScreen,
                ExtendedPosition = markerCenter,
                Angle            = angle,
                GroupId          = 0
            });
        }
        return extensions;
    }

    /// <summary>
    /// Validates a layout for intersecting lines and overlapping markers.
    /// Returns human-readable issue descriptions (empty list = no issues).
    /// </summary>
    public List<string> ValidateLayout(List<RadialExtension> extensions)
    {
        if (extensions == null) throw new ArgumentNullException(nameof(extensions));

        var issues       = new List<string>();
        var markerRadius = _visualConfig.LocationMarkerSize / 2.0;

        for (int i = 0; i < extensions.Count; i++)
        {
            for (int j = i + 1; j < extensions.Count; j++)
            {
                var e1 = extensions[i];
                var e2 = extensions[j];

                if (GeometryMath.DoLineSegmentsIntersect(
                        e1.OriginalPosition, e1.ExtendedPosition,
                        e2.OriginalPosition, e2.ExtendedPosition))
                    issues.Add($"Lines intersect: {e1.Location.Name} ↔ {e2.Location.Name}");

                if (GeometryMath.DoesLinePassTooCloseToMarker(
                        e1.OriginalPosition, e1.ExtendedPosition,
                        e2.ExtendedPosition, markerRadius + 2))
                    issues.Add($"Line too close to marker: {e1.Location.Name} → {e2.Location.Name}");

                if (GeometryMath.DoesLinePassTooCloseToMarker(
                        e2.OriginalPosition, e2.ExtendedPosition,
                        e1.ExtendedPosition, markerRadius + 2))
                    issues.Add($"Line too close to marker: {e2.Location.Name} → {e1.Location.Name}");

                double dx       = e1.ExtendedPosition.X - e2.ExtendedPosition.X;
                double dy       = e1.ExtendedPosition.Y - e2.ExtendedPosition.Y;
                double distance = Math.Sqrt(dx * dx + dy * dy);
                if (distance < _visualConfig.LocationMarkerSize)
                    issues.Add($"Markers overlap: {e1.Location.Name} ↔ {e2.Location.Name} ({distance:F1}px apart)");
            }
        }

        return issues;
    }

    /// <summary>
    /// Saves extensions under <see cref="CurrentLayoutKey"/>.
    /// Sets <see cref="IsManualLayoutActive"/> to true on success.
    /// Returns false if <see cref="CurrentLayoutKey"/> is null or the underlying save fails.
    /// </summary>
    public bool TrySave(List<RadialExtension> extensions)
    {
        if (extensions == null) throw new ArgumentNullException(nameof(extensions));
        if (CurrentLayoutKey == null)
        {
            _logger.LogWarning("LayoutEditorController.TrySave: CurrentLayoutKey is null — nothing saved");
            return false;
        }

        bool ok = _layoutManager.SaveLayout(CurrentLayoutKey, extensions);
        if (ok)
        {
            SetManualLayoutActive(true);
            _logger.LogInfo($"Saved manual layout: {extensions.Count} markers, key={CurrentLayoutKey}");
        }
        return ok;
    }

    /// <summary>
    /// Deletes the manual layout variant for <see cref="CurrentLayoutKey"/>.
    /// Sets <see cref="IsManualLayoutActive"/> to false on success.
    /// Returns false if <see cref="CurrentLayoutKey"/> is null or the underlying delete fails.
    /// </summary>
    public bool TryDelete()
    {
        if (CurrentLayoutKey == null)
        {
            _logger.LogWarning("LayoutEditorController.TryDelete: CurrentLayoutKey is null — nothing deleted");
            return false;
        }

        bool ok = _layoutManager.DeleteLayout(CurrentLayoutKey);
        if (ok)
        {
            SetManualLayoutActive(false);
            _logger.LogInfo($"Deleted manual layout, key={CurrentLayoutKey}");
        }
        return ok;
    }
}
