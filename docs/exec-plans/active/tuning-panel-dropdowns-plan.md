---
status: active
owner: agent
started: 2026-06-22
parent_plan: runtime-tuning-panel-plan.md
review: tuning-panel-dropdowns-plan-review-2026-06-22.md
---

# Developer Tuning Panel Dropdowns Plan

Follow-up to [runtime-tuning-panel-plan.md](../completed/runtime-tuning-panel-plan.md). Backlog: [TO_DO.md](../../TO_DO.md) (Developer tooling).

## Objective

Replace the free-text `TxtShaftVariant` and `TxtHeadVariant` text boxes in the developer tuning panel with non-editable `ComboBox` dropdowns populated from on-disk variant folders. When composite pins are disabled (`ChkComposite` unchecked), both pickers are greyed out but retain their selected values in config.

## Context

The runtime tuning panel already reads/writes `PinParts.ShaftAssetVariant` and `PinParts.HeadAssetVariant` as strings. Free-text entry is error-prone (60+ shaft variant folders today). Dropdowns eliminate typos while preserving reload/save/apply behavior.

## Design decisions (resolved)

| Topic | Decision |
|-------|----------|
| Base / empty option | First list item is display label `"(base)"`; maps to `string.Empty` in `TuningPanelEventArgs` and config |
| Editable ComboBox | `IsEditable="False"` — strict pick-list only |
| Config value missing on disk | Include the configured variant name in the list when loading values if it is not found by the catalog scan (preserves reload/display for stale or custom folders) |
| Enumeration location | New `Services/PinPartVariantCatalog.cs` — no `Directory.*` or `Images&Content` literals in `Views/` |
| Catalog API shape | Single `ListVariants(contentFolderPath, partsFolderPath, subfolderName, ensureIncluded)` — pass `"shaft_variants"` or `"head_variants"` from MainWindow |
| Catalog logging | Inject `ILogger`; log a warning when the variants directory is missing or inaccessible (return empty list, do not throw) |
| Directory scan | `Directory.GetDirectories` only — immediate subdirectory names; never include stray files at the variants root |
| When to refresh lists | At `SetupTuningPanel()` and again before `LoadValues` on reload-from-disk (folders may change between sessions) |
| Sort order | Alphabetical, case-insensitive, with `"(base)"` always first (prepended in the View, not by the catalog) |
| Large shaft list UX | Plain `ComboBox` for v1; search/grouping deferred unless manual review says otherwise |
| ComboBox dark theme | Add inline XAML styles so selected item and dropdown list are readable on the `#EE111111` panel (dark background, light foreground) |
| ItemsSource reset guard | Wrap `SetVariantOptions` body in `_isLoading = true` / `finally false` so `SelectionChanged` does not run validation mid-rebind |
| Checkbox validation | Wire `ChkComposite` `Click` (and other checkboxes if convenient) to the shared input-changed handler so toggling composite re-validates Apply |

## Architecture constraints

Must respect existing harness rules:

1. **Golden principle #2** — resolve paths via `ContentLoader` + `PinPartConfig.PartsFolderPath` (default `Pins_v2/parts`), not hardcoded `Images&Content/Pins_v2/parts/...`.
2. **`GoldenPrincipleTests.Views_DoNotReferenceContentFolderPaths`** — `Views/DeveloperTuningPanel.xaml.cs` must not contain `Images&Content` or perform filesystem enumeration.
3. **`TuningPanelWiringTests.DeveloperTuningPanel_CodeBehind_DoesNotReferenceServicesOrUtilities`** — the View receives variant names from `MainWindow`; it does not call Services directly.
4. **Thin View** — `PinPartVariantCatalog` lives in `Services/`; `MainWindow.DeveloperTuning.partial.cs` orchestrates enumeration and calls `DeveloperTuningPanel.SetVariantOptions(...)`.

```text
ContentLoader.ContentFolderPath + PinPartConfig.PartsFolderPath
        → PinPartVariantCatalog.ListVariants(..., "shaft_variants" | "head_variants", ...)
        → MainWindow.SetupTuningPanel / OnReloadTuningFromDisk
        → DeveloperTuningPanel.SetVariantOptions(shaft, head)   [_isLoading guard inside]
        → ComboBox ItemsSource (display strings)
```

## Modularity & file-size impact

| File | Action | Target |
|------|--------|--------|
| `Services/PinPartVariantCatalog.cs` | Create | ~70–90 lines (single `ListVariants`, `ILogger`) |
| `Tests/PinPartVariantCatalogTests.cs` | Create | temp-dir unit tests |
| `Views/DeveloperTuningPanel.xaml` | Modify | ComboBox + dark styles + checkbox `Click` wiring |
| `Views/DeveloperTuningPanel.xaml.cs` | Modify | `SetVariantOptions`, selection mapping, unified input handler |
| `MainWindow.DeveloperTuning.partial.cs` | Modify | catalog field, `using Services`, refresh on setup/reload |
| `Tests/TuningPanelWiringTests.cs` | Modify | control names, tooltip loop, `IsEnabled` binding |

No changes to `MainWindow.xaml.cs` beyond what is already wired for the tuning panel.

## Phase 1: Variant catalog service

### `Services/PinPartVariantCatalog.cs`

- [ ] Add constructor taking `ILogger` (match existing service patterns).
- [ ] Add one public method:

```csharp
public IReadOnlyList<string> ListVariants(
    string contentFolderPath,
    string partsFolderPath,
    string subfolderName,
    string? ensureIncluded = null)
```

- [ ] Resolve `Path.Combine(contentFolderPath, partsFolderPath, subfolderName)`.
- [ ] If the directory does not exist or is inaccessible: log a warning via `ILogger`, return an empty list (no throw).
- [ ] Enumerate with `Directory.GetDirectories` only; take `Path.GetFileName` of each result (immediate child folder names, not files).
- [ ] Sort ordinal ignore-case.
- [ ] If `ensureIncluded` is non-empty and not already in the result, add it and re-sort (stale config values remain selectable).

`MainWindow` calls twice: `ListVariants(..., "shaft_variants", shaftToInclude)` and `ListVariants(..., "head_variants", headToInclude)`.

### `Tests/PinPartVariantCatalogTests.cs`

- [ ] Create temp content tree with `Pins_v2/parts/shaft_variants/foo` and `head_variants/bar`.
- [ ] Assert sorted folder names returned for each `subfolderName`.
- [ ] Assert missing directory returns empty list (no throw); optional mock-logger assertion that a warning was logged.
- [ ] Assert `ensureIncluded` adds a name not present on disk.
- [ ] Place a stray file (e.g. `README.md`) in the variants root — assert it is **not** listed.

```bash
dotnet test Tests\InteractiveWorldMap.Tests.csproj --filter "FullyQualifiedName~PinPartVariantCatalogTests" --no-restore
```

## Phase 2: XAML updates

### `Views/DeveloperTuningPanel.xaml`

- [ ] Replace `TxtShaftVariant` / `TxtHeadVariant` `TextBox` controls with `CmbShaftVariant` / `CmbHeadVariant` `ComboBox` controls.
- [ ] Set `IsEditable="False"` on both.
- [ ] Bind `IsEnabled="{Binding IsChecked, ElementName=ChkComposite}"` on both (grey out when composite pins off; values are **not** cleared on toggle).
- [ ] Wire `SelectionChanged="OnVariantSelectionChanged"` on both ComboBoxes.
- [ ] Add dark-theme inline styles (or local `UserControl.Resources`) so ComboBox background, foreground, and dropdown items match the panel (`#EE111111` / white or `#DDDDDD` text). Verify disabled state is still readable when `ChkComposite` is unchecked.
- [ ] Wire `Click="OnPanelInputChanged"` on `ChkComposite` (and the other tuning checkboxes if not already wired) so toggling composite re-runs validation and updates Apply button state.
- [ ] Update tooltips to mention picking from folder names (still under `Pins_v2/parts/*_variants/`).

## Phase 3: View code-behind

### `Views/DeveloperTuningPanel.xaml.cs`

- [ ] Add `public const string BaseVariantLabel = "(base)";` (or `internal` if tests read via source guard only).
- [ ] Rename/refactor `OnInputChanged(object, TextChangedEventArgs)` → `OnPanelInputChanged(object, RoutedEventArgs)` for text fields **and** checkbox `Click` handlers (handler body unchanged: `if (!_isLoading) ValidateInputs();`).
- [ ] Add `public void SetVariantOptions(IReadOnlyList<string> shaftVariants, IReadOnlyList<string> headVariants)`:
  - Set `_isLoading = true` before reassigning `ItemsSource`; clear in `finally` (prevents `SelectionChanged` from validating transient empty selection during reload/setup).
  - Build display lists: `"(base)"` first, then catalog folder names (already sorted).
  - Assign `ItemsSource` on both ComboBoxes; do **not** set `SelectedItem` here (caller follows with `LoadValues`).
- [ ] Add `OnVariantSelectionChanged` — call `ValidateInputs()` when `!_isLoading`.
- [ ] Add helper `TryGetVariantFromCombo(ComboBox cmb, out string variant)` — `"(base)"` or null selection → `string.Empty`; otherwise folder name.
- [ ] Update `LoadValues`:
  - After setting checkbox/text fields, select `"(base)"` when config variant is empty/whitespace.
  - Otherwise select matching folder name; caller must have passed `ensureIncluded` via `RefreshTuningPanelVariantOptions` before `LoadValues` if the folder is missing on disk.
- [ ] Update `TryBuildEventArgs` to use `TryGetVariantFromCombo` instead of `TxtShaftVariant.Text` / `TxtHeadVariant.Text`.
- [ ] Do **not** add `using System.IO`, `Directory.*`, or `Images&Content` strings.

## Phase 4: MainWindow wiring

### `MainWindow.DeveloperTuning.partial.cs`

- [ ] Add `using InteractiveWorldMap.Services;` (file does not import Services today).
- [ ] Add `PinPartVariantCatalog` field, constructed with `_logger` (same pattern as other services in `MainWindow.xaml.cs`).
- [ ] Add `RefreshTuningPanelVariantOptions(string? shaftToInclude = null, string? headToInclude = null)`:
  - Read `_contentLoader.ContentFolderPath` and `_visualConfig.PinParts.PartsFolderPath`.
  - Call `_variantCatalog.ListVariants(..., "shaft_variants", shaftToInclude)` and `ListVariants(..., "head_variants", headToInclude)`.
  - Call `DeveloperTuningPanel.SetVariantOptions(...)`.
- [ ] In `SetupTuningPanel()`: `RefreshTuningPanelVariantOptions()` then `LoadValues(_visualConfig)` (populate before select).
- [ ] In `OnReloadTuningFromDisk` success path: `RefreshTuningPanelVariantOptions` with reloaded shaft/head variants, then `ApplyTuningAsync` / `LoadValues` as today (catalog refresh happens before selection is restored).

## Phase 5: Test & harness updates

### `Tests/TuningPanelWiringTests.cs`

- [ ] Update `DeveloperTuningPanel_ProvidesTooltipsForTuningOptions` control-name loop: replace `"TxtShaftVariant"` / `"TxtHeadVariant"` with `"CmbShaftVariant"` / `"CmbHeadVariant"` (test breaks if only other assertions are updated).
- [ ] Rename other control references: `CmbShaftVariant`, `CmbHeadVariant`.
- [ ] Assert XAML contains `IsEnabled="{Binding IsChecked, ElementName=ChkComposite}"` for both ComboBoxes.
- [ ] Assert `IsEditable="False"` on both ComboBoxes.
- [ ] Assert code-behind does not reference `TxtShaftVariant` / `TxtHeadVariant`.
- [ ] Assert `SetVariantOptions` sets `_isLoading` during ItemsSource rebuild (source guard).
- [ ] Assert `ChkComposite` wires `Click="OnPanelInputChanged"` (or equivalent shared handler).
- [ ] Assert `SetVariantOptions` exists and `TryBuildEventArgs` uses variant combo helpers.

### Other

- [ ] `CHANGELOG.md` — `[Unreleased]` entry: tuning panel shaft/head variant pickers are dropdowns.
- [ ] No change to `docs/TO_DO.md` until complete (bullet already exists).

```bash
.\scripts\verify.ps1
```

## Acceptance criteria

- [ ] Shaft and head variant pickers are `ComboBox` controls populated from on-disk `shaft_variants` / `head_variants` under `PartsFolderPath`.
- [ ] `"(base)"` selects empty `ShaftAssetVariant` / `HeadAssetVariant` (base/lit shaft behavior unchanged).
- [ ] Pickers are disabled when `ChkComposite` is unchecked; re-enabling restores prior selection.
- [ ] Toggling `ChkComposite` updates Apply button enabled state without editing a text field.
- [ ] Reload-from-disk does not flash a transient `"(base)"` validation error while variant lists are rebuilt.
- [ ] Apply, Save, and Reload-from-disk still read/write the same config fields as before.
- [ ] A config variant not present on disk still appears in the list and can be selected after reload.
- [ ] ComboBoxes are readable on the dark tuning panel background.
- [ ] Missing variants directories log a warning and yield an empty list (plus `"(base)"` in the UI).
- [ ] `Views/` contains no `Images&Content` path literals and no filesystem enumeration.
- [ ] `.\scripts\verify.ps1` passes.

## Manual QA (optional, Windows)

1. `Debug.EnableTuningPanel = true`, `PinParts.UseCompositeRendering = true`. Launch app.
2. Open tuning panel — shaft dropdown lists `"(base)"` plus sorted shaft folders; head dropdown lists head outline variants. ComboBox text/background readable on dark panel.
3. Pick a shaft variant, Apply — pins update. Pick `"(base)"`, Apply — reverts to base/lit shaft assets.
4. Uncheck Composite pins — both dropdowns grey out. Re-check — prior selections restored; Apply state updates immediately.
5. Reload from disk with a valid variant selected — no momentary error text while lists refresh.
6. Set a variant in `visual-config.json` that does not exist on disk; Reload — value still shown and selectable.

## Risks

| Risk | Mitigation |
|------|------------|
| `SelectionChanged` during `LoadValues` triggers validation flicker | `_isLoading` guard in `OnVariantSelectionChanged` and inside `SetVariantOptions` |
| `ItemsSource` reset clears selection during reload | `_isLoading` in `SetVariantOptions`; `LoadValues` runs immediately after refresh |
| Toggling composite does not re-validate | `Click="OnPanelInputChanged"` on `ChkComposite` |
| Default WPF ComboBox unreadable on dark panel | Explicit dark-theme styles in Phase 2 |
| 60+ shaft names hard to scan | Alphabetical sort; search UI deferred |
| Custom `PartsFolderPath` in config | Catalog uses config path, not a hardcoded default |

## Completion

When done: move this file to `docs/exec-plans/completed/`, add row to completed section in [README.md](README.md), check off [TO_DO.md](../../TO_DO.md) bullet, ensure `CHANGELOG.md` entry is accurate.
