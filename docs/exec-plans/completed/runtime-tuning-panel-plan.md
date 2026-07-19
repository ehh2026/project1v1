---
status: completed
owner: agent
started: 2026-06-21
completed: 2026-06-21
---

# Runtime Tuning Panel Plan

## Completion Summary
Implemented 2026-06-21.

- Added `Views/DeveloperTuningPanel.xaml` with validation, apply/save/reload events, and invariant-culture numeric parsing.
- Added `Debug.EnableTuningPanel`, `Models/TuningPanelEventArgs`, and local `visual-config.json` opt-in while preserving model default `false`.
- Added `MainWindow.DeveloperTuning.partial.cs` for runtime apply/save/reload, reference-stable `_visualConfig` mutation, cache clearing, full marker recreation, and edit/animation guards.
- Added `HeadAssetVariant` to `CompositePinLayoutContentHasher.ComputeConfigHash`.
- Added focused tests for config defaults, hash behavior, threshold-sensitive clustering, view dependency boundaries, and recluster ordering.

## Objective
Add an in-app developer tuning panel (`Views/DeveloperTuningPanel.xaml`) to adjust visual and layout configuration parameters on the fly. This prevents hand-editing `visual-config.json` and restarting the application while tuning marker aesthetics or debugging composite pin logic.

## Scope
1. Add a toggle button in `MainWindow.xaml`, gated by `DebugConfig.EnableTuningPanel`.
2. Add an encapsulated UserControl (`Views/DeveloperTuningPanel.xaml`) that exposes tuning options through primitive event args.
3. Add `Models/TuningPanelEventArgs.cs` with model-layer primitives only, so `Views/` does not reference `Services/`.
4. Add `MainWindow.DeveloperTuning.partial.cs` for runtime apply/save/reload logic while preserving `_visualConfig` reference stability.
5. Keep `MainWindow.xaml.cs` changes limited to config-service fields, constructor wiring, a `SetupTuningPanel()` call, and the F12 hotkey branch.

## Current-Code Findings To Respect
- `MainWindow.xaml.cs` is currently 776 lines. The file is under the 800-line limit but close enough that new behavior must live in a partial. `MainWindow.LayoutEditor.partial.cs` is also near the limit at 780 lines; do not add tuning code there.
- Current partial line-count baseline: `MainWindow.xaml.cs` 776, `MainWindow.CompositePins.partial.cs` 518, `MainWindow.Content.partial.cs` 229, `MainWindow.LayoutEditor.partial.cs` 780, `MainWindow.Navigation.partial.cs` 502. `MainWindow.DeveloperTuning.partial.cs` and `Views/DeveloperTuningPanel.xaml.cs` do not exist yet.
- `_visualConfig` is passed by reference to services such as `MarkerPlacementOrchestrator` and `RadialExtensionAdjuster`. Reload must mutate the existing `_visualConfig` instance, not reassign it.
- `ContentLoader.ClusterDistanceThreshold` is copied from `_visualConfig.ClusterDistanceThreshold` only during construction today. Any runtime threshold change must also update `_contentLoader.ClusterDistanceThreshold` before `LoadClustersAsync()`.
- `ClearAllMarkers()` clears marker lists and `_baseMarkerVisuals`, but not extension lines or tuning-specific state. Full marker recreation should explicitly clear `_extensionLineRenderer` and `_overrideStore` before rebuilding markers.
- `CompositePinLayoutContentHasher.ComputeConfigHash` currently includes `ShaftAssetVariant` but does not include `HeadAssetVariant`. Fix this before or during implementation by adding `HeadAssetVariant` to the hash and adding a regression test; keep explicit `_compositePinPlanCache.ClearAll()` in the tuning apply path as a runtime safety fallback for asset/path changes.
- The plan should block apply/reload while `_layoutEditor.IsEditMode` or `IsAnimating`; full recreation during an edit session can discard unsaved layout state.

## Initial Tuning Surface
Keep the first slice focused on values already present in `VisualConfig`:

- `Debug.ShowCompositePinDebugOverlay`
- `PinParts.Enabled`
- `PinParts.UseCompositeRendering`
- `PinParts.UsePrerasterizedRendering`
- `PinParts.UseLitShafts`
- `PinParts.ShaftAssetVariant`
- `PinParts.HeadAssetVariant`
- `PinParts.DefaultStubLengthPixels`
- `PinParts.TargetHeadRadiusPx`
- `PinParts.TargetShaftHalfWidthPx`
- `ClusterDistanceThreshold`
- `LocationMarkerSize`
- `ClusterMarkerSize`

Research note: all listed fields exist in `VisualConfig`, `DebugConfig`, or `PinPartConfig`. `PinParts.Enabled` is included because `CanUseCompositePins()` gates composite rendering on both `PinParts.Enabled` and `PinParts.UseCompositeRendering`; leaving it out would make `UseCompositeRendering` appear ineffective when `Enabled` is false. `PinParts.HeadAssetVariant` exists in the model and render builder tests but is currently omitted from `visual-config.json`, so the panel should load the model default empty string unless the user sets a variant.

Defer broader drawn-pin styling controls (`PinMarkers.BallSize`, `ShaftWidth`, colors, and outline thicknesses) unless a second pass is explicitly requested. They are useful but widen the recreation and validation surface.

## Modularity & File-Size Impact
- Create `Views/DeveloperTuningPanel.xaml` and `.cs` for UI-only input collection and validation.
- Create `MainWindow.DeveloperTuning.partial.cs` for apply/reload/save orchestration. Target under 250 lines.
- Keep `MainWindow.xaml.cs` under 800 lines. If the hotkey or field wiring pushes it over the limit, move key handling into a new focused partial rather than adding more logic to the primary file.
- Keep `Views/DeveloperTuningPanel.xaml.cs` independent of `InteractiveWorldMap.Services` and `InteractiveWorldMap.Utilities`; the existing architecture test will catch this.

## Runtime Constraints & Reference Stability
1. **Reference stability:** Do not reassign `_visualConfig` during reload. Copy supported tuning values onto the existing instance.
2. **Full recreation values:** Changes to `ClusterDistanceThreshold`, `LocationMarkerSize`, or `ClusterMarkerSize` require marker recreation because clustering and marker UserControl sizing are baked during creation.
3. **Composite plan values:** Changes to `PinParts.Enabled`, asset variants, lit shafts, target head radius, target shaft width, default stub length, prerasterization, or composite/debug toggles require `UpdateMarkerPositions()` at minimum. Asset/path changes also require cache clearing.
4. **Composite off fallback:** Turning composite rendering off should call `RestoreBaseMarkerVisuals()`. If `_baseMarkerVisuals` is empty or a full recreation was needed, use the recreation path instead.
5. **Busy and edit-state guard:** Apply, reload, and save should no-op with a visible status message while tuning is busy, the app is animating, or edit mode is active.

## Preparation Before Implementation
- [x] Run `py -3 scripts/doc_gardening.py` after plan edits; this plan must have active front matter and an active README row.
- [x] Confirm `CompositePinLayoutContentHasher.ComputeConfigHash` behavior: it includes `ShaftAssetVariant` but not `HeadAssetVariant`. Implementation should add `HeadAssetVariant` to the hash and test it, while still clearing runtime caches when asset variants change.
- [x] Confirm the first tuning surface against `Models/VisualConfig.cs`, `Models/DebugConfig.cs`, and `Models/PinPartConfig.cs`. Add `PinParts.Enabled` to avoid an invisible gate around `UseCompositeRendering`.
- [x] Capture line counts before implementation: `MainWindow.xaml.cs` 776, `MainWindow.DeveloperTuning.partial.cs` absent, `Views/DeveloperTuningPanel.xaml.cs` absent. Keep new tuning code out of existing near-limit files.
- [x] Decide whether `visual-config.json` should enable the panel by default for local development. Decision: add `"EnableTuningPanel": true` under `Debug` in the repo's local `visual-config.json` when implementing this feature, while keeping `DebugConfig.EnableTuningPanel` default `false` so missing/new configs remain production-safe.

## Proposed Implementation

### 1. Configuration & Models
Update `Models/DebugConfig.cs`:

```csharp
/// <summary>
/// Shows the developer-only runtime tuning panel and F12 toggle.
/// </summary>
public bool EnableTuningPanel { get; set; } = false;
```

Create `Models/TuningPanelEventArgs.cs`:

```csharp
namespace InteractiveWorldMap.Models
{
    public class TuningPanelEventArgs : System.EventArgs
    {
        public bool PinPartsEnabled { get; set; }
        public bool UseComposite { get; set; }
        public bool UsePrerasterize { get; set; }
        public bool ShowDebugOverlay { get; set; }
        public bool UseLitShafts { get; set; }
        public string ShaftVariant { get; set; } = string.Empty;
        public string HeadVariant { get; set; } = string.Empty;
        public double ClusterThreshold { get; set; }
        public double StubLength { get; set; }
        public double TargetHeadRadiusPx { get; set; }
        public double TargetShaftHalfWidthPx { get; set; }
        public double LocationMarkerSize { get; set; }
        public double ClusterMarkerSize { get; set; }
    }
}
```

Do not include `NeedsFullRecreation` in the event args. `MainWindow` has the previous config values and must compute recreation/caching decisions itself.

### 2. Composite Plan Hashing
Update `Services/CompositePinLayoutContentHasher.cs` so `ComputeConfigHash(PinPartConfig config)` includes `config.HeadAssetVariant` alongside `config.ShaftAssetVariant`. Add a regression test in `Tests/CompositePinLayoutContentHasherTests.cs` proving the hash changes when only `HeadAssetVariant` changes.

This is required even though the runtime tuning apply path also clears `_compositePinPlanCache`: the cache key should be correct for normal startup and non-tuning code paths too.

### 3. UserControl (`Views/DeveloperTuningPanel.xaml`)
Create a developer panel with checkboxes, text boxes, and `Apply`, `Save`, and `Reload` buttons. It communicates only through:

```csharp
public event EventHandler<TuningPanelEventArgs>? ApplyRequested;
public event EventHandler? SaveRequested;
public event EventHandler? ReloadRequested;
```

Validation requirements:
- Parse and format numeric values with `CultureInfo.InvariantCulture`.
- Reject invalid numeric input; do not silently clamp user input in the UI.
- Require positive values for `ClusterThreshold`, `LocationMarkerSize`, and `ClusterMarkerSize`.
- Allow zero or positive values for `StubLength`, `TargetHeadRadiusPx`, and `TargetShaftHalfWidthPx`; zero preserves existing fallback behavior for target head/shaft scaling.
- Show a compact inline error and keep `Apply` disabled while invalid.

Load current values from `VisualConfig`:

```csharp
public void LoadValues(VisualConfig config)
{
    ChkPinPartsEnabled.IsChecked = config.PinParts.Enabled;
    ChkComposite.IsChecked = config.PinParts.UseCompositeRendering;
    ChkPrerasterize.IsChecked = config.PinParts.UsePrerasterizedRendering;
    ChkDebugOverlay.IsChecked = config.Debug.ShowCompositePinDebugOverlay;
    ChkUseLitShafts.IsChecked = config.PinParts.UseLitShafts;
    TxtShaftVariant.Text = config.PinParts.ShaftAssetVariant;
    TxtHeadVariant.Text = config.PinParts.HeadAssetVariant;
    TxtClusterThreshold.Text = config.ClusterDistanceThreshold.ToString(CultureInfo.InvariantCulture);
    TxtStubLength.Text = config.PinParts.DefaultStubLengthPixels.ToString(CultureInfo.InvariantCulture);
    TxtTargetHeadRadius.Text = config.PinParts.TargetHeadRadiusPx.ToString(CultureInfo.InvariantCulture);
    TxtTargetShaftHalfWidth.Text = config.PinParts.TargetShaftHalfWidthPx.ToString(CultureInfo.InvariantCulture);
    TxtLocationMarkerSize.Text = config.LocationMarkerSize.ToString(CultureInfo.InvariantCulture);
    TxtClusterMarkerSize.Text = config.ClusterMarkerSize.ToString(CultureInfo.InvariantCulture);
}
```

### 4. MainWindow XAML
Add the namespace:

```xml
xmlns:views="clr-namespace:InteractiveWorldMap.Views"
```

Add the panel and toggle button to the existing overlay/root grid:

```xml
<views:DeveloperTuningPanel x:Name="DeveloperTuningPanel"
        Visibility="Collapsed"
        HorizontalAlignment="Right"
        VerticalAlignment="Bottom"
        Margin="20,20,20,60"
        ApplyRequested="OnApplyTuning"
        SaveRequested="OnSaveTuningToDisk"
        ReloadRequested="OnReloadTuningFromDisk" />

<Button x:Name="TuningPanelToggleBtn"
        Content="Tuning"
        Visibility="Collapsed"
        HorizontalAlignment="Right"
        VerticalAlignment="Bottom"
        Margin="20"
        Padding="15,8"
        Background="#CC555555"
        Foreground="White"
        Click="OnTuningPanelToggleClick" />
```

### 5. `MainWindow.xaml.cs` Wiring
Promote the config service/path so save and reload reuse the same path:

```csharp
private readonly VisualConfigService _configService = new VisualConfigService();
private readonly string _configPath;
```

In the constructor:

```csharp
_configPath = IOPath.Combine(AppDomain.CurrentDomain.BaseDirectory, "visual-config.json");
_visualConfig = _configService.Load(_configPath);
```

Remove the existing local constructor variables:

```csharp
var configPath = IOPath.Combine(AppDomain.CurrentDomain.BaseDirectory, "visual-config.json");
var visualConfigService = new VisualConfigService();
```

Then update existing log statements and config-path consumers to use `_configPath`. This avoids shadowing the promoted fields and ensures save/reload use the same loaded file path.

After event wiring and config-dependent UI setup:

```csharp
SetupTuningPanel();
```

Add F12 to `OnKeyDown` without changing existing Escape/Ctrl+S behavior:

```csharp
else if (e.Key == Key.F12 && _visualConfig.Debug.EnableTuningPanel)
{
    OnTuningPanelToggleClick(this, new RoutedEventArgs());
    e.Handled = true;
}
```

### 6. `MainWindow.DeveloperTuning.partial.cs`
Create a focused partial for tuning behavior. The apply path should follow this order:

1. Return with status if `_isTuningBusy`, `IsAnimating`, or `_layoutEditor.IsEditMode`.
2. Compare old `_visualConfig` values to incoming event args.
3. Compute:
   - `needsRecreate`: cluster threshold or marker sizes changed, or composite-off fallback has no captured base visuals.
   - `assetVariantChanged`: shaft/head variant or lit-shaft path behavior changed.
   - `compositePlanChanged`: `PinParts.Enabled`, asset variants, target head radius, target shaft half width, stub length, prerasterize, composite toggle, or debug overlay changed.
4. Mutate the existing `_visualConfig` instance with an explicit mapping:
   - `e.PinPartsEnabled` -> `_visualConfig.PinParts.Enabled`
   - `e.UseComposite` -> `_visualConfig.PinParts.UseCompositeRendering`
   - `e.UsePrerasterize` -> `_visualConfig.PinParts.UsePrerasterizedRendering`
   - `e.ShowDebugOverlay` -> `_visualConfig.Debug.ShowCompositePinDebugOverlay`
   - `e.UseLitShafts` -> `_visualConfig.PinParts.UseLitShafts`
   - `e.ShaftVariant.Trim()` -> `_visualConfig.PinParts.ShaftAssetVariant`
   - `e.HeadVariant.Trim()` -> `_visualConfig.PinParts.HeadAssetVariant`
   - `e.ClusterThreshold` -> `_visualConfig.ClusterDistanceThreshold`
   - `e.StubLength` -> `_visualConfig.PinParts.DefaultStubLengthPixels`
   - `e.TargetHeadRadiusPx` -> `_visualConfig.PinParts.TargetHeadRadiusPx`
   - `e.TargetShaftHalfWidthPx` -> `_visualConfig.PinParts.TargetShaftHalfWidthPx`
   - `e.LocationMarkerSize` -> `_visualConfig.LocationMarkerSize`
   - `e.ClusterMarkerSize` -> `_visualConfig.ClusterMarkerSize`
5. If `assetVariantChanged`, clear `_pinPartBitmapCache`.
6. If `compositePlanChanged`, call `_compositePinPlanCache.ClearAll()`.
7. Do not clear `_pinPartGeometryHash` for shaft/head asset variant changes alone. Variants currently change resolved image paths under the same `GeometryMetadataPath`; rereading the same metadata file does not provide variant-specific anchors. Only clear `_pinPartGeometryHash` if this plan later adds `PinParts.GeometryMetadataPath` to the tuning surface or changes the metadata file path at runtime.
8. If `needsRecreate`, call `RecreateAllMarkersAsync()`. Otherwise restore base visuals when turning composite off, then call `UpdateMarkerPositions()`.
9. Reload the panel values from `_visualConfig`.

Define `SetupTuningPanel()` in this partial:

```csharp
private void SetupTuningPanel()
{
    TuningPanelToggleBtn.Visibility = _visualConfig.Debug.EnableTuningPanel
        ? Visibility.Visible
        : Visibility.Collapsed;

    DeveloperTuningPanel.Visibility = Visibility.Collapsed;
    DeveloperTuningPanel.LoadValues(_visualConfig);
}
```

Full recreation must update the content loader before reclustering:

```csharp
private async Task RecreateAllMarkersAsync()
{
    _extensionLineRenderer.Clear();
    _overrideStore.ClearAll();
    ClearAllMarkers();

    _contentLoader.ClusterDistanceThreshold = _visualConfig.ClusterDistanceThreshold;
    _clusters = await _contentLoader.LoadClustersAsync();
    AddClustersToMap(_clusters);
}
```

Save should call `_configService.Save(_visualConfig, _configPath)`. Reload should load a fresh config, copy the supported tuning values onto `_visualConfig`, and then use the same apply/recreate decision path rather than duplicating logic.

## Failure Modes & Error Handling
1. **Invalid input:** Keep `Apply` disabled and show inline errors until all fields parse and pass range validation.
2. **Edit mode or animation active:** Do not apply or reload. Show a status message explaining that tuning is blocked until edit/animation completes.
3. **Configuration I/O exceptions:** Log the error through `_logger` and show panel status without crashing.
4. **Composite-off fallback limits:** If base visuals are unavailable, use full recreation instead of leaving blank markers.
5. **Missing composite assets:** Existing `ApplyCompositePinTargetToMarker` fallback should keep drawn markers visible. The tuning path should not suppress those warnings.

## Tests
- Add JSON roundtrip/default tests in `Tests/VisualConfigServiceTests.cs` for `Debug.EnableTuningPanel`.
- Add a focused cluster-threshold test in `Tests/ContentLoaderTests.cs` or a new `Tests/ClusterCacheTests.cs` proving different thresholds produce independent cache keys or different cluster results.
- Add a source-level architecture test, if needed, that `Views/DeveloperTuningPanel.xaml.cs` does not reference `InteractiveWorldMap.Services`.
- Add a source-level MainWindow wiring test that the tuning apply path updates `_contentLoader.ClusterDistanceThreshold` before `LoadClustersAsync()`.
- Add a config-hash test for `CompositePinLayoutContentHasher.ComputeConfigHash` proving `HeadAssetVariant` changes the hash.

## Documentation Updates
- Ensure this plan is listed in `docs/exec-plans/active/README.md`.
- Keep the existing `docs/TO_DO.md` developer-tooling bullet short and linked to this plan.
- Add a `CHANGELOG.md` entry under `[Unreleased]` when implementation lands, not for this planning-only review unless the registry/docs change is the deliverable.

## Completion Gate
- `py -3 scripts/doc_gardening.py`
- `dotnet test Tests/InteractiveWorldMap.Tests.csproj`
- `.\scripts\verify.ps1` before claiming implementation complete
