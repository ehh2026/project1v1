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
    private readonly IManualLayoutManager _layoutManager;
    private readonly VisualConfig _visualConfig;
    private readonly ILogger _logger;

    // ─── Observable state ────────────────────────────────────────────────────

    public bool IsEditMode { get; private set; }
    public bool IsManualLayoutActive { get; private set; }

    /// <summary>
    /// True when the user has unloaded the saved layout for this session (see
    /// <see cref="UnloadManualLayout"/>). Auto-apply paths must skip applying while this is set so
    /// pins stay at their auto-placed positions. Session-scoped: cleared whenever a layout next
    /// becomes active (re-edit) and not persisted, so a restart restores normal auto-apply.
    /// </summary>
    /// <summary>
    /// True when the user unloaded the layout for this view. Cleared only by
    /// <see cref="SetManualLayoutActive"/> with <c>true</c> — saving, or another explicit
    /// reactivation. Re-entering the editor does not clear it; see
    /// <see cref="UnloadManualLayout"/>.
    /// </summary>
    public bool IsManualLayoutSuppressed { get; private set; }

    /// <summary>VariantId of the variant that is currently loaded into the editor.</summary>
    public string? ActiveVariantId { get; private set; }
    /// <summary>Origin of the currently-active variant (null when no variant is loaded).</summary>
    public ManualLayoutOrigin? ActiveVariantOrigin { get; private set; }
    /// <summary>Display name of the currently-active variant.</summary>
    public string? ActiveVariantDisplayName { get; private set; }

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

    /// <summary>Fired whenever the variant list for the active edit session changes (save, save-as, delete).</summary>
    public event Action<IReadOnlyList<ManualLayoutSummary>>? VariantsChanged;

    // ─── Constructor ─────────────────────────────────────────────────────────

    public LayoutEditorController(
        IManualLayoutManager layoutManager,
        VisualConfig visualConfig,
        ILogger logger)
    {
        _layoutManager = layoutManager ?? throw new ArgumentNullException(nameof(layoutManager));
        _visualConfig = visualConfig ?? throw new ArgumentNullException(nameof(visualConfig));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    // ─── State transitions ───────────────────────────────────────────────────

    /// <summary>
    /// The scope of the edit session in progress, or null when not editing.
    /// </summary>
    /// <remarks>
    /// Captured once on entry and never mutated — this is the only record of what an edit is
    /// scoped to. There is deliberately no shared "current layout key" alongside it: when there
    /// was, navigation wrote it during zoom animations and full-map probes, so an in-progress edit
    /// could silently change which layout it would overwrite. Display and replay resolve their own
    /// keys locally instead. See <see cref="LayoutEditSession"/>.
    /// </remarks>
    public LayoutEditSession? ActiveSession { get; private set; }

    /// <summary>
    /// Starts an edit session with a fixed scope. Replaces any session already in progress.
    /// </summary>
    public void BeginEditSession(LayoutEditSession session)
    {
        ActiveSession = session ?? throw new ArgumentNullException(nameof(session));

        // Variant ids are unique only within a group, so identity from a previous session must not
        // survive into this one — that leak is how a save once landed in another layout's variant.
        // Scope can only change by beginning a session, so clearing here makes the leak
        // structurally impossible rather than something a setter has to remember to check.
        ClearActiveVariant();

        _logger.LogInfo(
            $"[LayoutEditorController] Edit session begun: scope={session.Scope} key={session.LayoutKey}");
    }

    /// <summary>Ends the current edit session, if any.</summary>
    public void EndEditSession()
    {
        ActiveSession = null;
        ClearActiveVariant();
    }

    /// <summary>
    /// The layout key the in-progress edit session reads and writes, or null when not editing.
    /// </summary>
    /// <remarks>
    /// Every save, delete and variant operation resolves its key through here. Navigation cannot
    /// write it, which is the whole point of the session.
    /// </remarks>
    private string? EditKey => ActiveSession?.LayoutKey;

    private void ClearActiveVariant()
    {
        ActiveVariantId = null;
        ActiveVariantOrigin = null;
        ActiveVariantDisplayName = null;
    }

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
    /// </summary>
    /// <remarks>
    /// The suppression lasts the rest of the run. It is deliberately not cleared by re-entering the
    /// editor — opening a view to work on it says nothing about wanting the unloaded layout back,
    /// and treating it as a request made Unload unable to survive the next click. Saving does clear
    /// it, through <see cref="SetManualLayoutActive"/>, because a save is an explicit statement
    /// about what this view should look like. Restarting the app also restores it: the file was
    /// never touched, and the flag lives only in memory.
    /// </remarks>
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
    public ManualLayout? TryLoad(string key) => _layoutManager.LoadLayout(key);

    /// <summary>
    /// Records the loaded layout's variant as the one the edit session is working on.
    /// </summary>
    /// <remarks>
    /// Split out of <see cref="TryLoad"/>, which used to do this for any key it was handed. That
    /// made probe loads during navigation — which pass a key that is not the edit scope — silently
    /// rewrite the editor's variant identity. Loading and adopting are now separate decisions, so
    /// only the editor adopts.
    /// </remarks>
    public void AdoptVariantIdentity(ManualLayout? layout)
    {
        if (layout == null) return;

        ActiveVariantId = layout.VariantId;
        ActiveVariantOrigin = layout.Origin;
        ActiveVariantDisplayName = layout.DisplayName;
    }

    /// <summary>
    /// Loads the layout for the active edit session and adopts its variant identity.
    /// </summary>
    public ManualLayout? LoadForEditSession()
    {
        if (EditKey == null) return null;

        var layout = TryLoad(EditKey);
        AdoptVariantIdentity(layout);
        return layout;
    }

    /// <summary>
    /// True when a user-made (<see cref="ManualLayoutOrigin.Manual"/>) layout exists for the key.
    /// </summary>
    /// <remarks>
    /// Deliberately side-effect free, unlike <see cref="TryLoad"/>, which updates the active-variant
    /// fields from whatever key it is handed. This exists to answer "has the user deliberately
    /// arranged this view?" during navigation, where mutating editor state would be wrong.
    /// Auto-generated seeds return false: they are a starting point, not a decision.
    /// </remarks>
    public bool HasManualLayout(string? key)
    {
        if (string.IsNullOrEmpty(key)) return false;

        // Asks the group, not the current selection. LoadLayout returns the *selected* variant, so
        // a selected AutoSeed would hide a Manual variant beside it and precedence would wrongly
        // fall back to the full-map layout.
        return _layoutManager.HasManualVariant(key!);
    }

    /// <summary>
    /// Loads a specific variant by id, sets it as the persisted selection,
    /// and updates active-variant state.
    /// </summary>
    public ManualLayout? SwitchToVariant(string variantId)
    {
        if (EditKey == null) return null;
        var layout = _layoutManager.LoadVariant(EditKey, variantId);
        if (layout == null) return null;
        _layoutManager.SetSelectedVariantId(EditKey, variantId);
        ActiveVariantId = variantId;
        ActiveVariantOrigin = layout.Origin;
        ActiveVariantDisplayName = layout.DisplayName;
        return layout;
    }

    /// <summary>Returns the variant list for <see cref="EditKey"/>.</summary>
    public IReadOnlyList<ManualLayoutSummary> GetVariants()
    {
        if (EditKey == null) return Array.Empty<ManualLayoutSummary>();
        return _layoutManager.ListVariants(EditKey);
    }

    /// <summary>
    /// Returns exactly the variants <see cref="TryDelete"/> would destroy: the hand-made ones.
    /// AutoSeed and Imported variants survive it, so a confirmation counting anything wider would
    /// promise to delete work that is still there afterwards.
    /// </summary>
    /// <remarks>
    /// This lives here rather than in the click handler so that the set shown to the user and the
    /// set actually removed cannot drift apart: both are defined by <see cref="ManualLayoutOrigin.Manual"/>.
    /// </remarks>
    public IReadOnlyList<ManualLayoutSummary> GetDeletableVariants() =>
        GetVariants().Where(v => v.Origin == ManualLayoutOrigin.Manual).ToList();

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

            bool requiresExtensionLine = ManualLayoutPlacementPolicy.RequiresExtensionLine(
                layoutMarker.OriginalPosition,
                layoutMarker.ExtendedPosition);

            applications.Add(new LayoutMarkerApplication(
                layoutMarker.LocationName,
                layoutMarker.OriginalPosition,
                layoutMarker.ExtendedPosition,
                requiresExtensionLine)
            {
                SourceExtendedX = layoutMarker.SourceExtendedX,
                SourceExtendedY = layoutMarker.SourceExtendedY,
                Angle = layoutMarker.Angle,
                LineLength = layoutMarker.LineLength,
                PairId = layoutMarker.PairId,
                HeadSourcePath = layoutMarker.HeadSourcePath
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
            double dx = markerCenter.X - originalScreen.X;
            double dy = markerCenter.Y - originalScreen.Y;
            double angle = Math.Atan2(dx, -dy) * (180.0 / Math.PI);
            extensions.Add(new RadialExtension
            {
                Location = location,
                OriginalPosition = originalScreen,
                ExtendedPosition = markerCenter,
                Angle = angle,
                GroupId = 0
            });
        }
        return extensions;
    }

    /// <summary>
    /// Returns the names of markers whose coordinates are not finite (NaN or infinity).
    /// </summary>
    /// <remarks>
    /// <c>Canvas.GetLeft</c>/<c>GetTop</c> return NaN when a position was never explicitly set, so
    /// a marker that has not been laid out yields NaN coordinates. Persisting those produces a
    /// layout file that cannot be read back as geometry, so a save carrying any of them is refused.
    /// <para>
    /// Note that a *zero-length* extension is not an error: a pin with no radial extension sits
    /// exactly on its anchor, so <c>MarkerCenter == OriginalScreen</c> is legitimate. Whether the
    /// endpoint could be resolved at all is tracked separately, at the point the endpoint is read.
    /// </para>
    /// </remarks>
    public static IReadOnlyList<string> FindNonFiniteMarkers(
        IEnumerable<(Location Location, Point MarkerCenter, Point OriginalScreen)> markerData)
    {
        if (markerData == null) throw new ArgumentNullException(nameof(markerData));

        var bad = new List<string>();
        foreach (var (location, markerCenter, originalScreen) in markerData)
        {
            if (!IsFinite(markerCenter) || !IsFinite(originalScreen))
                bad.Add(location?.Name ?? "(unnamed)");
        }
        return bad;
    }

    private static bool IsFinite(Point p) =>
        !double.IsNaN(p.X) && !double.IsNaN(p.Y) &&
        !double.IsInfinity(p.X) && !double.IsInfinity(p.Y);

    /// <summary>
    /// True when every marker that the placement rules say should carry a radial extension has
    /// instead collapsed onto its own anchor — the signature of lost endpoints.
    /// </summary>
    /// <param name="markerData">Markers being captured for saving.</param>
    /// <param name="expectedExtendedLocations">
    /// Names of locations belonging to a dense group, i.e. the ones the renderer would extend.
    /// Obtain these from <c>RadialExtensionCalculator.DetectDenseGroups</c> so this uses the same
    /// rule as placement rather than a second guess at it.
    /// </param>
    /// <remarks>
    /// A zero-length extension is <em>not</em> in itself wrong. A pin with no radial extension is
    /// drawn as a default stub, and a sparsely populated view where no pins are close enough to
    /// group is legitimately all stubs — refusing that would block valid saves. What cannot happen
    /// legitimately is a <em>dense</em> group, which the renderer always extends, arriving here
    /// with every member on its anchor.
    /// </remarks>
    public static bool IsCollapsedLayout(
        IEnumerable<(Location Location, Point MarkerCenter, Point OriginalScreen)> markerData,
        ISet<string> expectedExtendedLocations)
    {
        if (markerData == null) throw new ArgumentNullException(nameof(markerData));
        if (expectedExtendedLocations == null || expectedExtendedLocations.Count == 0)
            return false;

        int considered = 0;
        foreach (var (location, markerCenter, originalScreen) in markerData)
        {
            if (location?.Name == null || !expectedExtendedLocations.Contains(location.Name))
                continue;

            considered++;
            if (!GeometryMath.ArePointsCoincident(markerCenter, originalScreen))
                return false;
        }

        return considered > 0;
    }

    /// <summary>
    /// Overload that works out the expected-extension set itself, using the same dense-group
    /// detection the renderer uses so the two cannot drift apart.
    /// </summary>
    public bool IsCollapsedLayout(
        IReadOnlyList<(Location Location, Point MarkerCenter, Point OriginalScreen)> markerData)
    {
        if (markerData == null) throw new ArgumentNullException(nameof(markerData));

        return IsCollapsedLayout(markerData, FindExpectedExtendedLocations(markerData));
    }

    /// <summary>
    /// Names of the locations the renderer would give a radial extension. Pins outside any dense
    /// group are drawn as default stubs by design, so they must never be judged as collapsed.
    /// </summary>
    public ISet<string> FindExpectedExtendedLocations(
        IReadOnlyList<(Location Location, Point MarkerCenter, Point OriginalScreen)> markerData)
    {
        if (markerData == null) throw new ArgumentNullException(nameof(markerData));

        // Source-image coordinates, not screen. DetectDenseGroups applies
        // ProximityThresholdPixels directly to whatever it is given, and
        // MarkerPlacementOrchestrator feeds it Location.PixelX/PixelY. Passing projected screen
        // positions instead would scale the threshold with the current zoom, so the guard would
        // disagree with the placement it claims to mirror.
        var positions = new Dictionary<Location, Point>();
        foreach (var (location, _, _) in markerData)
        {
            if (location != null)
                positions[location] = new Point(location.PixelX, location.PixelY);
        }

        var calculator = new RadialExtensionCalculator(_visualConfig.RadialExtension);
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var group in calculator.DetectDenseGroups(positions))
        {
            foreach (var location in group.Locations)
            {
                if (location?.Name != null)
                    names.Add(location.Name);
            }
        }

        return names;
    }

    /// <summary>
    /// Validates a layout for intersecting lines and overlapping markers.
    /// Returns human-readable issue descriptions (empty list = no issues).
    /// </summary>
    public List<string> ValidateLayout(List<RadialExtension> extensions)
    {
        if (extensions == null) throw new ArgumentNullException(nameof(extensions));

        var issues = new List<string>();
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

                double dx = e1.ExtendedPosition.X - e2.ExtendedPosition.X;
                double dy = e1.ExtendedPosition.Y - e2.ExtendedPosition.Y;
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
        if (EditKey == null)
        {
            _logger.LogWarning("LayoutEditorController.TrySave: no active edit session — nothing saved");
            return false;
        }

        string targetVariantId = "manual-default";
        string targetDisplayName = "Manual Layout";
        bool setAsDefault = true;

        if (!string.IsNullOrEmpty(ActiveVariantId) && ActiveVariantOrigin == ManualLayoutOrigin.Manual)
        {
            targetVariantId = ActiveVariantId;
            targetDisplayName = ActiveVariantDisplayName ?? "Manual Layout";
            setAsDefault = targetVariantId == "manual-default";
        }

        bool ok = _layoutManager.SaveVariant(EditKey, targetVariantId, targetDisplayName,
            ManualLayoutOrigin.Manual, extensions, assignments,
            setAsDefault: setAsDefault, setAsSelected: true);
        if (ok)
        {
            ActiveVariantId = targetVariantId;
            ActiveVariantOrigin = ManualLayoutOrigin.Manual;
            ActiveVariantDisplayName = targetDisplayName;
            SetManualLayoutActive(true);
            NotifyVariantsChanged();
            _logger.LogInfo($"Saved manual layout variant '{targetVariantId}': {extensions.Count} markers, key={EditKey}");
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
        if (EditKey == null)
        {
            _logger.LogWarning("LayoutEditorController.TrySaveAsVariant: no active edit session");
            return false;
        }
        if (string.IsNullOrWhiteSpace(displayName)) return false;

        var variantId = MakeVariantId(displayName);
        var basedOnId = ActiveVariantId;

        bool ok = _layoutManager.SaveVariant(EditKey, variantId, displayName,
            ManualLayoutOrigin.Manual, extensions, assignments,
            setAsDefault: false, setAsSelected: true, basedOnVariantId: basedOnId);
        if (ok)
        {
            ActiveVariantId = variantId;
            ActiveVariantOrigin = ManualLayoutOrigin.Manual;
            ActiveVariantDisplayName = displayName;
            SetManualLayoutActive(true);
            NotifyVariantsChanged();
            _logger.LogInfo($"SaveAs variant '{variantId}' ({displayName}) for key={EditKey}");
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
        if (EditKey == null || string.IsNullOrEmpty(ActiveVariantId))
        {
            _logger.LogWarning("LayoutEditorController.TryDeleteActiveVariant: no active variant to delete");
            return false;
        }

        bool ok = _layoutManager.DeleteVariant(EditKey, ActiveVariantId);
        if (ok)
        {
            var remaining = _layoutManager.ListVariants(EditKey);
            var next = remaining.FirstOrDefault(s => s.Origin == ManualLayoutOrigin.Manual)
                    ?? remaining.FirstOrDefault();
            ActiveVariantId = next?.VariantId;
            ActiveVariantOrigin = next?.Origin;
            ActiveVariantDisplayName = next?.DisplayName;
            bool hasManual = remaining.Any(s => s.Origin == ManualLayoutOrigin.Manual);
            SetManualLayoutActive(hasManual);
            NotifyVariantsChanged();
            _logger.LogInfo($"Deleted variant, key={EditKey}");
        }
        return ok;
    }

    /// <summary>
    /// Deletes the manual layout variant for <see cref="EditKey"/> (all Manual variants).
    /// Sets <see cref="IsManualLayoutActive"/> to false on success.
    /// </summary>
    public bool TryDelete()
    {
        if (EditKey == null)
        {
            _logger.LogWarning("LayoutEditorController.TryDelete: no active edit session — nothing deleted");
            return false;
        }

        bool ok = _layoutManager.DeleteLayout(EditKey);
        if (ok)
        {
            ActiveVariantId = null;
            ActiveVariantOrigin = null;
            ActiveVariantDisplayName = null;
            SetManualLayoutActive(false);
            NotifyVariantsChanged();
            _logger.LogInfo($"Deleted manual layout, key={EditKey}");
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
