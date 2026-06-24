using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
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

    public bool    IsEditMode           { get; private set; }
    public bool    IsManualLayoutActive { get; private set; }
    public string? CurrentLayoutKey     { get; private set; }

    /// <summary>
    /// True when the user has unloaded the saved layout for this session (see
    /// <see cref="UnloadManualLayout"/>). Auto-apply paths must skip applying while this is set so
    /// pins stay at their auto-placed positions. Session-scoped: cleared whenever a layout next
    /// becomes active (re-edit) and not persisted, so a restart restores normal auto-apply.
    /// </summary>
    public bool    IsManualLayoutSuppressed { get; private set; }

    /// <summary>VariantId of the variant that is currently loaded into the editor.</summary>
    public string?             ActiveVariantId      { get; private set; }
    /// <summary>Origin of the currently-active variant (null when no variant is loaded).</summary>
    public ManualLayoutOrigin? ActiveVariantOrigin  { get; private set; }
    /// <summary>Display name of the currently-active variant.</summary>
    public string?             ActiveVariantDisplayName { get; private set; }

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

        /// <summary>Extension angle in degrees (same convention as <see cref="ManualLayoutMarker.Angle"/>).</summary>
        public double Angle { get; init; }
        /// <summary>Extension line length in screen pixels at save time.</summary>
        public double LineLength { get; init; }

        /// <summary>Saved shaft pair id. Null → scorer chooses on replay.</summary>
        public string? PairId { get; init; }
        /// <summary>Saved head asset path. Null → location-hash fallback on replay.</summary>
        public string? HeadSourcePath { get; init; }
    }

    public event Action? EditModeEntered;
    public event Action? EditModeExited;
    public event Action<bool>? ManualLayoutActivityChanged;

    /// <summary>Fired whenever the variant list for <see cref="CurrentLayoutKey"/> changes (save, save-as, delete).</summary>
    public event Action<IReadOnlyList<ManualLayoutSummary>>? VariantsChanged;

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

    public void SetLayoutKey(string? key)
    {
        CurrentLayoutKey = key;
        if (key == null)
        {
            ActiveVariantId          = null;
            ActiveVariantOrigin      = null;
            ActiveVariantDisplayName = null;
        }
    }

    public void SetManualLayoutActive(bool active)
    {
        IsManualLayoutActive = active;
        // A layout becoming active re-engages the workflow, so clear any prior session unload.
        if (active)
            IsManualLayoutSuppressed = false;
        ManualLayoutActivityChanged?.Invoke(active);
    }

    /// <summary>
    /// Non-destructively unloads the active manual layout: flags it suppressed so the auto-apply
    /// paths skip it (markers revert to auto-placement) while leaving the saved JSON on disk intact.
    /// The layout returns when it next becomes active — re-entering the editor or restarting the app.
    /// </summary>
    public void UnloadManualLayout()
    {
        IsManualLayoutSuppressed = true;
        SetManualLayoutActive(false);
    }

    // ─── Data operations ─────────────────────────────────────────────────────

    /// <summary>
    /// Loads the preferred layout variant for <paramref name="key"/>, or null if none exists.
    /// Updates <see cref="ActiveVariantId"/> and <see cref="ActiveVariantOrigin"/> on success.
    /// </summary>
    public ManualLayout? TryLoad(string key)
    {
        var layout = _layoutManager.LoadLayout(key);
        if (layout != null)
        {
            ActiveVariantId          = layout.VariantId;
            ActiveVariantOrigin      = layout.Origin;
            ActiveVariantDisplayName = layout.DisplayName;
        }
        return layout;
    }

    /// <summary>
    /// Loads a specific variant by id, sets it as the persisted selection,
    /// and updates active-variant state.
    /// </summary>
    public ManualLayout? SwitchToVariant(string variantId)
    {
        if (CurrentLayoutKey == null) return null;
        var layout = _layoutManager.LoadVariant(CurrentLayoutKey, variantId);
        if (layout == null) return null;
        _layoutManager.SetSelectedVariantId(CurrentLayoutKey, variantId);
        ActiveVariantId          = variantId;
        ActiveVariantOrigin      = layout.Origin;
        ActiveVariantDisplayName = layout.DisplayName;
        return layout;
    }

    /// <summary>Returns the variant list for <see cref="CurrentLayoutKey"/>.</summary>
    public IReadOnlyList<ManualLayoutSummary> GetVariants()
    {
        if (CurrentLayoutKey == null) return Array.Empty<ManualLayoutSummary>();
        return _layoutManager.ListVariants(CurrentLayoutKey);
    }

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
                // Expected: a saved full-map layout carries every location, but only a subset is
                // visible in the current view (e.g. a single-location zoom). Skipping the rest is
                // by design — not an error — so log at info level to avoid alarming warn spam.
                _logger.LogInfo($"  Skipping layout marker not visible in current view: {layoutMarker.LocationName}");
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
                SourceExtendedY = layoutMarker.SourceExtendedY,
                Angle           = layoutMarker.Angle,
                LineLength      = layoutMarker.LineLength,
                PairId          = layoutMarker.PairId,
                HeadSourcePath  = layoutMarker.HeadSourcePath
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
            double angle = Math.Atan2(dx, -dy) * (180.0 / Math.PI);
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
                        e2.ExtendedPosition, markerRadius))
                    issues.Add($"Line too close to marker: {e1.Location.Name} → {e2.Location.Name}");

                if (GeometryMath.DoesLinePassTooCloseToMarker(
                        e2.OriginalPosition, e2.ExtendedPosition,
                        e1.ExtendedPosition, markerRadius))
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
    /// Saves extensions to the currently-active Manual variant (or "manual-default" if none).
    /// If the active variant is AutoSeed, callers should redirect to <see cref="TrySaveAsVariant"/> instead.
    /// Sets <see cref="IsManualLayoutActive"/> to true on success.
    /// </summary>
    public bool TrySave(List<RadialExtension> extensions,
        IReadOnlyDictionary<string, (string PairId, string HeadSourcePath)>? assignments = null)
    {
        if (extensions == null) throw new ArgumentNullException(nameof(extensions));
        if (CurrentLayoutKey == null)
        {
            _logger.LogWarning("LayoutEditorController.TrySave: CurrentLayoutKey is null — nothing saved");
            return false;
        }

        string targetVariantId   = "manual-default";
        string targetDisplayName = "Manual Layout";
        bool   setAsDefault      = true;

        if (!string.IsNullOrEmpty(ActiveVariantId) && ActiveVariantOrigin == ManualLayoutOrigin.Manual)
        {
            targetVariantId   = ActiveVariantId;
            targetDisplayName = ActiveVariantDisplayName ?? "Manual Layout";
            setAsDefault      = targetVariantId == "manual-default";
        }

        bool ok = _layoutManager.SaveVariant(CurrentLayoutKey, targetVariantId, targetDisplayName,
            ManualLayoutOrigin.Manual, extensions, assignments,
            setAsDefault: setAsDefault, setAsSelected: true);
        if (ok)
        {
            ActiveVariantId          = targetVariantId;
            ActiveVariantOrigin      = ManualLayoutOrigin.Manual;
            ActiveVariantDisplayName = targetDisplayName;
            SetManualLayoutActive(true);
            NotifyVariantsChanged();
            _logger.LogInfo($"Saved manual layout variant '{targetVariantId}': {extensions.Count} markers, key={CurrentLayoutKey}");
        }
        return ok;
    }

    /// <summary>
    /// Creates a new named Manual variant from current marker positions.
    /// The slug + 8-char guid suffix becomes the <c>variantId</c>.
    /// Sets the new variant as selected and fires <see cref="VariantsChanged"/>.
    /// </summary>
    public bool TrySaveAsVariant(string displayName, List<RadialExtension> extensions,
        IReadOnlyDictionary<string, (string PairId, string HeadSourcePath)>? assignments = null)
    {
        if (extensions == null) throw new ArgumentNullException(nameof(extensions));
        if (CurrentLayoutKey == null)
        {
            _logger.LogWarning("LayoutEditorController.TrySaveAsVariant: CurrentLayoutKey is null");
            return false;
        }
        if (string.IsNullOrWhiteSpace(displayName)) return false;

        var variantId    = MakeVariantId(displayName);
        var basedOnId    = ActiveVariantId;

        bool ok = _layoutManager.SaveVariant(CurrentLayoutKey, variantId, displayName,
            ManualLayoutOrigin.Manual, extensions, assignments,
            setAsDefault: false, setAsSelected: true, basedOnVariantId: basedOnId);
        if (ok)
        {
            ActiveVariantId          = variantId;
            ActiveVariantOrigin      = ManualLayoutOrigin.Manual;
            ActiveVariantDisplayName = displayName;
            SetManualLayoutActive(true);
            NotifyVariantsChanged();
            _logger.LogInfo($"SaveAs variant '{variantId}' ({displayName}) for key={CurrentLayoutKey}");
        }
        return ok;
    }

    /// <summary>
    /// Deletes the currently-active variant (Manual only; AutoSeed rejection is enforced by the service).
    /// Updates <see cref="ActiveVariantId"/> to the next preferred variant after deletion.
    /// Sets <see cref="IsManualLayoutActive"/> to false if no Manual variants remain.
    /// </summary>
    public bool TryDeleteActiveVariant()
    {
        if (CurrentLayoutKey == null || string.IsNullOrEmpty(ActiveVariantId))
        {
            _logger.LogWarning("LayoutEditorController.TryDeleteActiveVariant: no active variant to delete");
            return false;
        }

        bool ok = _layoutManager.DeleteVariant(CurrentLayoutKey, ActiveVariantId);
        if (ok)
        {
            var remaining = _layoutManager.ListVariants(CurrentLayoutKey);
            var next = remaining.FirstOrDefault(s => s.Origin == ManualLayoutOrigin.Manual)
                    ?? remaining.FirstOrDefault();
            ActiveVariantId          = next?.VariantId;
            ActiveVariantOrigin      = next?.Origin;
            ActiveVariantDisplayName = next?.DisplayName;
            bool hasManual = remaining.Any(s => s.Origin == ManualLayoutOrigin.Manual);
            SetManualLayoutActive(hasManual);
            NotifyVariantsChanged();
            _logger.LogInfo($"Deleted variant, key={CurrentLayoutKey}");
        }
        return ok;
    }

    /// <summary>
    /// Deletes the manual layout variant for <see cref="CurrentLayoutKey"/> (all Manual variants).
    /// Sets <see cref="IsManualLayoutActive"/> to false on success.
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
            ActiveVariantId          = null;
            ActiveVariantOrigin      = null;
            ActiveVariantDisplayName = null;
            SetManualLayoutActive(false);
            NotifyVariantsChanged();
            _logger.LogInfo($"Deleted manual layout, key={CurrentLayoutKey}");
        }
        return ok;
    }

    // ─── Private helpers ──────────────────────────────────────────────────────

    private void NotifyVariantsChanged() => VariantsChanged?.Invoke(GetVariants());

    private static string MakeVariantId(string displayName)
    {
        var slug = Regex.Replace(displayName.ToLowerInvariant(), @"[^a-z0-9]+", "-").Trim('-');
        if (slug.Length > 20) slug = slug.Substring(0, 20);
        return slug + "-" + Guid.NewGuid().ToString("N").Substring(0, 8);
    }
}
