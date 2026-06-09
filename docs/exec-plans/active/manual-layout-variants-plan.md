---
status: active
owner: agent
started: 2026-06-07
revised: 2026-06-08
requirements_ref: manual-layout-variants
parent_program: composite-pins-program.md
---

# Manual Layout Variants Plan

Support multiple saved layout alternatives per cluster/viewport with explicit user selection, while keeping auto-generated seeds distinct from user-authored layouts.

Related design doc: [MANUAL_LAYOUT_EDITOR.md](../../guides/MANUAL_LAYOUT_EDITOR.md)

Prerequisite: [manual-layout-seed-alignment-plan.md](manual-layout-seed-alignment-plan.md) Phase 3 (reliable seed loading) should be complete or in progress.

TO_DO item: [Multiple saved layout variants per cluster/viewport](../../TO_DO.md)

## Problem

`ManualLayoutManager` already persists `ManualLayoutGroup` with multiple `Variants` and distinguishes `ManualLayoutOrigin.AutoSeed` vs `Manual`, but:

- Only one effective layout is chosen automatically via `SelectPreferredVariant` (Manual beats AutoSeed)
- There is no UI to list variants, save-as a new named alternative, switch active variant, or delete obsolete variants
- Users cannot intentionally pick among multiple arrangements for the same cluster key
- `IManualLayoutManager` exposes flat key APIs only -- no variant CRUD or selection persistence
- `SaveLayout` and `DeleteLayout` still hard-code the `"manual-default"` workflow: save updates one manual variant; delete removes all manual variants for the group

## Goal

Users can save, name, list, load, and delete layout variants per group key. AutoSeed layouts remain generated starting points; Manual layouts are never silently overwritten by seed regeneration.

## 2026-06-08 Review Notes

- Direction is solid and matches the current model shape, but the first draft was underspecified around selected-variant fallback, delete/default rules, and how `LayoutEditorController` tracks the active variant.
- The seed-generator file path was conditional, not current-state fact: `Tools/ManualLayoutSeedGenerator/Program.cs` only exists after [manual-layout-seed-alignment-plan.md](manual-layout-seed-alignment-plan.md) creates it. Today, `scripts/generate_manual_layout_seeds.ps1` is the active generator.
- Variant saves must preserve composite assignment fields (`PairId`, `HeadSourcePath`) added by the composite-pin manual-layout phases; otherwise switching variants can silently change shaft/head choices.

## Current State (already implemented)

| Component | Status |
|-----------|--------|
| `ManualLayoutGroup` / `ManualLayout.VariantId` / `Origin` | ✅ in `Models/ManualLayout.cs` |
| AutoSeed + Manual coexistence on save | ✅ `ManualLayoutManager.SaveLayout` |
| Prefer Manual over AutoSeed on load | ✅ `SelectPreferredVariant` |
| Legacy flat `Layouts` dictionary | ✅ migrated via `UpdateLegacyLayoutIndex` |
| Assignment fields for composite replay | ✅ `ManualLayoutMarker.PairId` / `HeadSourcePath` |
| Save activity flag | ✅ `LayoutEditorController.TrySave` sets manual layout active on success |

## Modularity / Bloat Guardrails

This plan touches an already-large WPF surface. Implementers must treat extraction as part of the work, not as a cleanup after the feature lands.

1. **Keep `MainWindow.xaml.cs` thin.** Variant UI event handlers should delegate in about 15 lines or less. Do not add new private helper regions over 30 lines.
2. **Put variant state and decisions in Services.** Selection, save-as, delete, default selection, stale-selection fallback, and variant summaries belong in `ManualLayoutManager` / `LayoutEditorController`, not in code-behind.
3. **No duplicate replay logic.** Variant switching calls `LoadVariant` / `SetSelectedVariantId`, then reuses the existing `ApplyManualLayout` path and composite application service hooks.
4. **Prefer focused DTOs and Services over expanding existing files.** Add `ManualLayoutSummary` in `Models/`; add small Service/controller methods rather than growing `MainWindow` or creating broad utility bags.
5. **Protect existing file-size rules.** No touched `.cs` file should move farther from the repo's 800-line guideline unless the change is explicitly extracting code out of a larger file.
6. **Unit-test service behavior directly.** Cover variant CRUD, selected-variant fallback, delete/default rules, and assignment-field round-trips without needing WPF integration tests for every branch.

## Phase 1 — Service API for variant management

**Deliverables:** explicit variant operations without UI.

### Files

| Action | Path |
|--------|------|
| Modify | `Services/IManualLayoutManager.cs` |
| Modify | `Services/ManualLayoutManager.cs` |
| Modify | `Services/LayoutEditorController.cs` |
| Create | `Tests/ManualLayoutVariantTests.cs` |

### New API (proposed)

```csharp
IReadOnlyList<ManualLayoutSummary> ListVariants(string groupKey);
ManualLayout? LoadVariant(string groupKey, string variantId);
bool SaveVariant(
    string groupKey,
    string variantId,
    string displayName,
    ManualLayoutOrigin origin,
    List<RadialExtension> extensions,
    IReadOnlyDictionary<string, (string PairId, string HeadSourcePath)>? assignments,
    bool setAsDefault,
    bool setAsSelected,
    string? basedOnVariantId = null);
bool DeleteVariant(string groupKey, string variantId);
bool SetDefaultVariant(string groupKey, string variantId);
string? GetSelectedVariantId(string groupKey);          // persisted user preference
bool SetSelectedVariantId(string groupKey, string variantId);
```

Add `ManualLayoutSummary` record in `Models/`:

```csharp
public sealed record ManualLayoutSummary(
    string GroupKey,
    string VariantId,
    string DisplayName,
    ManualLayoutOrigin Origin,
    DateTime UpdatedUtc,
    bool IsDefault,
    bool IsSelected,
    int MarkerCount);
```

### Tasks

1. Extend `ManualLayoutCollection` with optional `SelectedVariants` dictionary (`groupKey -> variantId`) for per-group user choice. Normalize null to an empty dictionary.
2. Implement `ListVariants`, `LoadVariant`, `SaveVariant` (support save-as with new `variantId`), `DeleteVariant`, `SetDefaultVariant`.
3. Update `LoadLayout(string key)` to honor `SelectedVariants` for the matched group. For compatible-key fallback, use the compatible group's stored selection, not the requested key.
4. If `SelectedVariants[groupKey]` points to a missing variant, ignore it, log once, remove or overwrite the stale selection on the next save, and fall back to current prefer-Manual logic.
5. `SetDefaultVariant` changes the default only within the variant's origin class. A group may have both one Manual default and one AutoSeed default; explicit `SelectedVariants` still wins.
6. Guard: `DeleteVariant` must not remove the last remaining variant in a group. MVP UI only enables delete for Manual variants; service should reject AutoSeed deletion unless an explicit future `allowGeneratedDelete` path is added.
7. After deleting the selected variant, clear `SelectedVariants[groupKey]` or move it to the next preferred variant before saving.
8. Guard: seed regeneration (`Origin = AutoSeed`) must never overwrite `Origin = Manual` variants -- only update matching AutoSeed variant ids.
9. Wire `LayoutEditorController` to use new APIs for save/load/delete instead of assuming `"manual-default"`; track `ActiveVariantId`, `ActiveVariantOrigin`, and dirty/save-as state.
10. Unit tests for: list, save-as second manual variant, select variant B, selected variant load after restart, stale selected id fallback, delete selected variant, reject last-variant delete, AutoSeed regen preserves manual variants.

### Compatibility requirements

- Keep `SaveLayout` / `LoadLayout` / `DeleteLayout` on `IManualLayoutManager` as compatibility wrappers until callers are migrated.
- Existing `SaveLayout(key, extensions, assignments)` should delegate to `SaveVariant(..., "manual-default", ..., setAsDefault: true, setAsSelected: true)` so current code paths keep working.
- `UpdateLegacyLayoutIndex` should continue to expose the selected/preferred variant in `Layouts` for older consumers.
- Variant save paths must pass through existing assignment enrichment so `PairId` and `HeadSourcePath` round-trip.

**Acceptance:**

- All new tests pass
- Existing `ManualLayoutManagerTests` still pass
- Architecture layer rules unchanged

## Phase 2 — Edit-mode UI for variant selection

**Deliverables:** user-visible variant picker in edit mode.

### Files

| Action | Path |
|--------|------|
| Modify | `MainWindow.xaml` — edit-mode toolbar |
| Modify | `MainWindow.xaml.cs` — bind variant list, handle selection/save-as/delete |
| Modify | `Services/LayoutEditorController.cs` — expose variant list events |

### Tasks

1. When edit mode opens, call `ListVariants(groupKey)` and populate a dropdown or list box (DisplayName + origin badge).
2. **Load selected variant:** switching selection re-applies marker positions and extension lines from that variant.
3. **Save:** updates current variant (if Manual) or prompts save-as if editing an AutoSeed layout.
4. **Save As:** dialog for display name -> new `variantId` (slug from name + short guid suffix); new variant becomes selected.
5. **Delete:** enabled only for Manual variants; confirm dialog.
6. **Set as default:** optional checkbox or command -- marks variant `IsDefault` for its origin class.
7. Visual indicator when active layout is AutoSeed vs Manual vs imported.
8. Status text shows `Loaded: {DisplayName} ({Origin})`.
9. Variant picker must not duplicate replay logic: it calls `LoadVariant` / `SetSelectedVariantId`, then reuses the existing `ApplyManualLayout` path (and composite application service where available).
10. Disabling delete/save for generated variants is a UI rule; service still enforces the deletion guard.

**Acceptance:**

- Manual smoke: create two manual variants for same cluster, switch between them, restart app, selection persists
- AutoSeed variant remains after saving a manual copy

## Phase 3 — Seed generator integration

**Deliverables:** regenerated seeds update AutoSeed variants only.

### Files

| Action | Path |
|--------|------|
| Modify | `scripts/generate_manual_layout_seeds.ps1` if seed alignment Phase 1 has not created the console tool yet |
| Modify | `Tools/ManualLayoutSeedGenerator/Program.cs` after [manual-layout-seed-alignment-plan.md](manual-layout-seed-alignment-plan.md) Phase 1 exists |
| Modify | `Services/ManualLayoutManager.cs` |

### Tasks

1. Seed generator writes/updates `variantId = "seed-default"`, `Origin = AutoSeed`, `IsDefault = true` per group.
2. If group already has Manual variants, merge -- do not delete them.
3. Bump `GeneratorVersion`; if version changes, optionally flag AutoSeed variants for review (log warning only in MVP).
4. Add test: regen seeds on file with existing Manual variant → Manual preserved, AutoSeed updated.
5. Generator must preserve `SelectedVariants` and any non-seed variants when rewriting `manual-layouts.json`.

**Acceptance:**

- Running seed generator twice updates AutoSeed endpoints without touching Manual variants

## Phase 4 — Documentation and verification

### Tasks

1. Update [MANUAL_LAYOUT_EDITOR.md](../../guides/MANUAL_LAYOUT_EDITOR.md) — variant model, UI flows, JSON schema examples with `LayoutGroups` and multiple variants.
2. Update [VISUAL_CONFIG.md](../../guides/VISUAL_CONFIG.md) if new config flags added (e.g. `AllowMultipleVariants`).
3. Add CHANGELOG entry.
4. Run `.\scripts\verify.ps1`.

## JSON Schema Example (target)

```json
{
  "LayoutGroups": {
    "abc123_z55.00_...": {
      "GroupKey": "abc123_z55.00_...",
      "Variants": [
        {
          "VariantId": "seed-default",
          "DisplayName": "Generated Seed",
          "Origin": "AutoSeed",
          "IsDefault": true,
          "GeneratorVersion": "ManualLayoutSeedGenerator/1.0",
          "Markers": [ "..."]
        },
        {
          "VariantId": "manual-spread-v2",
          "DisplayName": "Wider spread",
          "Origin": "Manual",
          "IsDefault": true,
          "BasedOnVariantId": "seed-default",
          "Markers": [ "..."]
        }
      ]
    }
  },
  "SelectedVariants": {
    "abc123_z55.00_...": "manual-spread-v2"
  }
}
```

## Open Decisions

1. **Max variants per group:** cap at 10 Manual/Imported variants per group for MVP to avoid unbounded JSON growth?
2. **Import/export:** defer single-variant export to a follow-up?
3. **Variant picker outside edit mode:** defer; MVP requires edit mode to switch.

## Recommended MVP Decisions

1. Cap at 10 Manual/Imported variants per group; when cap is reached, block Save As until the user deletes a variant.
2. Defer import/export.
3. Variant switching only in edit mode for MVP.

## Definition of Done

- Users can save-as, list, load, and delete manual variants per cluster
- AutoSeed and Manual variants coexist; seeds never overwrite manual work
- Selected variant persists across sessions
- Stale or deleted selected variants fall back predictably and do not corrupt `manual-layouts.json`
- Composite assignment fields persist across variant save/load/switch
- `scripts/verify.ps1` passes
