---
status: completed
owner: agent
started: 2026-06-22
completed_at: 2026-06-22
parent_plan: runtime-tuning-panel-plan.md
review:
  - tuning-panel-dropdowns-plan-review-2026-06-22.md
  - assessment-2026-06-22-14-59-04.md
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
| Folder named `(base)` on disk | **Do not create** — reserved UI label; a real folder with that name would map to empty config (document in catalog/plan comments only) |
| Editable ComboBox | `IsEditable="False"` — strict pick-list only |
| Config value missing on disk | Include via `ensureIncluded` even when the variants directory is missing or unreadable |
| Case-insensitive matching | Catalog dedupes `ensureIncluded` with `StringComparer.OrdinalIgnoreCase`; `LoadValues` selects via case-insensitive `SelectVariant` helper (WPF `SelectedItem` match is case-sensitive) |
| Enumeration location | New `Services/PinPartVariantCatalog.cs` — no `Directory.*` or `Images&Content` literals in `Views/` |
| Catalog API shape | Single `ListVariants(contentFolderPath, partsFolderPath, subfolderName, ensureIncluded)` — pass `"shaft_variants"` or `"head_variants"` from MainWindow |
| Catalog logging | Inject `ILogger`; log a warning when the variants directory is missing or `GetDirectories` fails; never throw |
| Directory scan | `Directory.GetDirectories` only — immediate subdirectory names; never include stray files at the variants root |
| Catalog init | `PinPartVariantCatalog` constructed inside `SetupTuningPanel()` (after `_logger` exists); field `private PinPartVariantCatalog _variantCatalog = null!;` — no `MainWindow.xaml.cs` constructor edits |
| When to refresh lists | At `SetupTuningPanel()` and again before `LoadValues` on reload-from-disk |
| Sort order | Alphabetical, case-insensitive, with `"(base)"` always first (prepended in the View, not by the catalog) |
| Large shaft list UX | Plain `ComboBox` for v1; search/filter deferred — [TO_DO.md](../../TO_DO.md) follow-up bullet |
| ComboBox dark theme | Style **both** the closed control and `ComboBox.ItemContainerStyle` (dropdown popup inherits parent `Foreground` but system white background → invisible text if only the parent is styled) |
| ItemsSource reset guard | Wrap `SetVariantOptions` body in `_isLoading = true` / `finally false` |
| Checkbox validation | Wire `ChkComposite` `Click` (and other checkboxes) to shared `OnPanelInputChanged` |
| Variant read helper | `GetVariantFromCombo(ComboBox)` returns `string` (no `Try*` / `out` — selection cannot fail) |

## Architecture constraints

Must respect existing harness rules:

1. **Golden principle #2** — resolve paths via `ContentLoader` + `PinPartConfig.PartsFolderPath` (default `Pins_v2/parts`), not hardcoded `Images&Content/Pins_v2/parts/...`.
2. **`GoldenPrincipleTests.Views_DoNotReferenceContentFolderPaths`** — `Views/DeveloperTuningPanel.xaml.cs` must not contain `Images&Content` or perform filesystem enumeration.
3. **`TuningPanelWiringTests.DeveloperTuningPanel_CodeBehind_DoesNotReferenceServicesOrUtilities`** — the View receives variant names from `MainWindow`; it does not call Services directly.
4. **Thin View** — `PinPartVariantCatalog` lives in `Services/`; `MainWindow.DeveloperTuning.partial.cs` orchestrates enumeration and calls `DeveloperTuningPanel.SetVariantOptions(...)`.

```text
ContentLoader.ContentFolderPath + PinPartConfig.PartsFolderPath
        → PinPartVariantCatalog.ListVariants(..., "shaft_variants" | "head_variants", ensureIncluded)
        → MainWindow.SetupTuningPanel (construct catalog here) / OnReloadTuningFromDisk
        → DeveloperTuningPanel.SetVariantOptions(shaft, head)   [_isLoading guard inside]
        → LoadValues → SelectVariant (case-insensitive)
```

## Modularity & file-size impact

| File | Action | Target |
|------|--------|--------|
| `Services/PinPartVariantCatalog.cs` | Create | ~80–100 lines |
| `Tests/PinPartVariantCatalogTests.cs` | Create | temp-dir unit tests |
| `Views/DeveloperTuningPanel.xaml` | Modify | ComboBox + `ItemContainerStyle` + checkbox `Click` |
| `Views/DeveloperTuningPanel.xaml.cs` | Modify | `SetVariantOptions`, `SelectVariant`, `GetVariantFromCombo` |
| `MainWindow.DeveloperTuning.partial.cs` | Modify | catalog lazy init in `SetupTuningPanel`, refresh on reload |
| `Tests/TuningPanelWiringTests.cs` | Modify | control names, tooltip loop, bindings |

No changes to `MainWindow.xaml.cs` beyond what is already wired for the tuning panel.

## Phase 1: Variant catalog service

### `Services/PinPartVariantCatalog.cs`

- [x] Add constructor taking `ILogger`.
- [x] Implement `ListVariants` with this behavior (directory failure must not discard `ensureIncluded`):

```csharp
public IReadOnlyList<string> ListVariants(
    string contentFolderPath,
    string partsFolderPath,
    string subfolderName,
    string? ensureIncluded = null)
{
    var list = new List<string>();
    var path = Path.Combine(contentFolderPath, partsFolderPath, subfolderName);

    if (Directory.Exists(path))
    {
        try
        {
            list.AddRange(
                Directory.GetDirectories(path)
                    .Select(Path.GetFileName)
                    .Where(name => !string.IsNullOrEmpty(name))!);
        }
        catch (Exception ex)
        {
            _logger.LogWarning($"Failed to read variants from {path}: {ex.Message}");
        }
    }
    else
    {
        _logger.LogWarning($"Variants directory does not exist: {path}");
    }

    if (!string.IsNullOrWhiteSpace(ensureIncluded) &&
        !list.Contains(ensureIncluded, StringComparer.OrdinalIgnoreCase))
    {
        list.Add(ensureIncluded);
    }

    list.Sort(StringComparer.OrdinalIgnoreCase);
    return list;
}
```

`MainWindow` calls twice: `ListVariants(..., "shaft_variants", shaftToInclude)` and `ListVariants(..., "head_variants", headToInclude)`.

### `Tests/PinPartVariantCatalogTests.cs`

- [x] Temp tree with `Pins_v2/parts/shaft_variants/foo` and `head_variants/bar` — assert sorted names.
- [x] Missing variants directory + non-empty `ensureIncluded` — assert list is `[ensureIncluded]` only (not empty).
- [x] `ensureIncluded` with different casing than on-disk folder — assert no duplicate entries.
- [x] Stray file in variants root — assert not listed.
- [x] `GetDirectories` failure path — source guard asserts `ensureIncluded` still appended after catch.

```bash
dotnet test Tests\InteractiveWorldMap.Tests.csproj --filter "FullyQualifiedName~PinPartVariantCatalogTests" --no-restore
```

## Phase 2: XAML updates

### `Views/DeveloperTuningPanel.xaml`

- [x] Replace `TxtShaftVariant` / `TxtHeadVariant` with `CmbShaftVariant` / `CmbHeadVariant` (`IsEditable="False"`).
- [x] Bind `IsEnabled="{Binding IsChecked, ElementName=ChkComposite}"` on both.
- [x] Wire `SelectionChanged="OnVariantSelectionChanged"` on both ComboBoxes.
- [x] Dark-theme styling — **required pattern** (shared via `UserControl.Resources` or duplicated on both combos):
  - Closed control: dark `Background` / light `Foreground`.
  - **`ComboBox.ItemContainerStyle`** on each combo — dark item background, light text, hover highlight (e.g. `#222222` / `White` / `#444444` on `IsMouseOver`). Without this, dropdown items show white-on-white.
- [x] Wire `Click="OnPanelInputChanged"` on `ChkComposite` and other tuning checkboxes.
- [x] Update tooltips for folder-name picking.

Example `ItemContainerStyle` (apply to both combos):

```xml
<ComboBox.ItemContainerStyle>
    <Style TargetType="ComboBoxItem">
        <Setter Property="Background" Value="#222222"/>
        <Setter Property="Foreground" Value="White"/>
        <Style.Triggers>
            <Trigger Property="IsMouseOver" Value="True">
                <Setter Property="Background" Value="#444444"/>
            </Trigger>
        </Style.Triggers>
    </Style>
</ComboBox.ItemContainerStyle>
```

## Phase 3: View code-behind

### `Views/DeveloperTuningPanel.xaml.cs`

- [x] Add `public const string BaseVariantLabel = "(base)";`.
- [x] Rename `OnInputChanged` → `OnPanelInputChanged(object, RoutedEventArgs)` for text + checkbox handlers.
- [x] `SetVariantOptions`: `_isLoading` guard; prepend `"(base)"`; assign `ItemsSource`; do not set selection.
- [x] `SelectVariant(ComboBox combo, string value)` — case-insensitive match against `combo.Items`; empty/whitespace → `BaseVariantLabel`; no match → `BaseVariantLabel` (catalog should have included via `ensureIncluded`).
- [x] `GetVariantFromCombo(ComboBox cmb)` — `SelectedItem` is `BaseVariantLabel` or null → `string.Empty`; else return string as-is.
- [x] `LoadValues`: call `SelectVariant(CmbShaftVariant, config.PinParts.ShaftAssetVariant)` and same for head (not direct `SelectedItem = value`).
- [x] `TryBuildEventArgs`: `ShaftVariant = GetVariantFromCombo(CmbShaftVariant).Trim()` (head likewise).
- [x] No `System.IO`, `Directory.*`, or `Images&Content` in this file.

## Phase 4: MainWindow wiring

### `MainWindow.DeveloperTuning.partial.cs`

- [x] Add `using InteractiveWorldMap.Services;`.
- [x] Declare `private PinPartVariantCatalog _variantCatalog = null!;` (do **not** construct at field initializer — `_logger` is not ready yet).
- [x] In `SetupTuningPanel()`: `_variantCatalog = new PinPartVariantCatalog(_logger);` then `RefreshTuningPanelVariantOptions()` then `LoadValues(_visualConfig)`.
- [x] `RefreshTuningPanelVariantOptions(string? shaftToInclude = null, string? headToInclude = null)`:
  - Use `_contentLoader.ContentFolderPath` and `_visualConfig.PinParts.PartsFolderPath`.
  - Call `ListVariants` for shaft and head; `SetVariantOptions` on the panel.
- [x] `OnReloadTuningFromDisk`: validate reloaded args before refreshing variant lists; refresh before apply/`LoadValues`.

## Phase 5: Test & harness updates

### `Tests/TuningPanelWiringTests.cs`

- [x] Update `DeveloperTuningPanel_ProvidesTooltipsForTuningOptions` loop: `CmbShaftVariant`, `CmbHeadVariant`.
- [x] Assert `IsEnabled` binding and `IsEditable="False"` on both combos.
- [x] Assert XAML includes `ItemContainerStyle` (or shared style key) for dark dropdown items.
- [x] Assert code-behind has `SelectVariant`, `GetVariantFromCombo`; no `TxtShaftVariant` / `TxtHeadVariant`.
- [x] Assert `SetVariantOptions` uses `_isLoading` guard; `ChkComposite` has `Click="OnPanelInputChanged"`.
- [x] Assert `SetupTuningPanel` constructs `PinPartVariantCatalog` (source guard on partial).
- [x] Assert reload validates before refreshing variant options.

### Other

- [x] `CHANGELOG.md` — `[Unreleased]` entry for dropdown pickers.
- [x] Add TO_DO follow-up for variant search/filter (see Deferred below).

```bash
.\scripts\verify.ps1
```

## Acceptance criteria

- [x] Shaft and head pickers are non-editable `ComboBox` controls from on-disk variant folders.
- [x] `"(base)"` → empty config; config with case mismatch still selects correctly (`outline_dark` config vs `Outline_Dark` folder).
- [x] `ensureIncluded` appears in the list even when the variants directory is missing.
- [x] No case-only duplicate entries when config casing differs from folder name on disk.
- [x] Dropdown **popup** items readable (not white-on-white).
- [x] Pickers disabled when `ChkComposite` unchecked; toggle re-validates Apply.
- [x] Reload does not flash transient validation errors during list rebuild.
- [x] Reload rejection leaves panel selection aligned with active in-memory config.
- [x] `ensureIncluded` for missing on-disk folder still selectable after reload.
- [x] `Views/` has no `Images&Content` literals or filesystem enumeration.
- [x] `.\scripts\verify.ps1` passes.

## Manual QA (optional, Windows)

1. Launch with tuning panel enabled and composite on — combos readable closed **and** open (scroll shaft list).
2. Set config variant with different casing than folder — Reload shows correct selection.
3. Rename/remove variants directory but keep value in `visual-config.json` — Reload still lists and selects config value.
4. Toggle composite, reload, apply — behaviors from prior QA checklist still hold.

## Deferred / follow-up

- [ ] **Variant search/filter** — 60+ shaft folders make a plain dropdown tedious; add type-to-filter or grouped list in a follow-up ([TO_DO.md](../../TO_DO.md) Developer tooling).

## Risks

| Risk | Mitigation |
|------|------------|
| WPF case-sensitive `SelectedItem` | `SelectVariant` ordinal-ignore-case |
| `ensureIncluded` lost when dir missing | Catalog adds `ensureIncluded` after failed/missing scan |
| Case-only duplicates in list | `Contains(..., OrdinalIgnoreCase)` before add |
| White dropdown text on white popup | `ItemContainerStyle` on ComboBox items |
| Catalog init before `_logger` | Construct in `SetupTuningPanel()` only |
| `SelectionChanged` during rebind | `_isLoading` in `SetVariantOptions` and `OnVariantSelectionChanged` |
| Real folder named `(base)` | Policy: do not create; document only |

## Completion

When done: move to `docs/exec-plans/completed/`, update [README.md](../active/README.md), check off main [TO_DO.md](../../TO_DO.md) picker bullet, ensure `CHANGELOG.md` is accurate. Leave the search/filter follow-up bullet open until that work ships.
