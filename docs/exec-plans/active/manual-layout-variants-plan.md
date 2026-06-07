---
status: active
owner: agent
started: 2026-06-07
requirements_ref: manual-layout-variants
---

# Manual Layout Variants Plan

Support multiple saved layout alternatives per cluster/viewport with explicit user selection, while keeping auto-generated seeds distinct from user-authored layouts.

Related design doc: [MANUAL_LAYOUT_EDITOR.md](../../MANUAL_LAYOUT_EDITOR.md)

Prerequisite: [manual-layout-seed-alignment-plan.md](manual-layout-seed-alignment-plan.md) Phase 3 (reliable seed loading) should be complete or in progress.

TO_DO item: [Multiple saved layout variants per cluster/viewport](../../TO_DO.md)

## Problem

`ManualLayoutManager` already persists `ManualLayoutGroup` with multiple `Variants` and distinguishes `ManualLayoutOrigin.AutoSeed` vs `Manual`, but:

- Only one effective layout is chosen automatically via `SelectPreferredVariant` (Manual beats AutoSeed)
- There is no UI to list variants, save-as a new named alternative, switch active variant, or delete obsolete variants
- Users cannot intentionally pick among multiple arrangements for the same cluster key
- `IManualLayoutManager` exposes flat key APIs only — no variant CRUD or selection persistence

## Goal

Users can save, name, list, load, and delete layout variants per group key. AutoSeed layouts remain generated starting points; Manual layouts are never silently overwritten by seed regeneration.

## Current State (already implemented)

| Component | Status |
|-----------|--------|
| `ManualLayoutGroup` / `ManualLayout.VariantId` / `Origin` | ✅ in `Models/ManualLayout.cs` |
| AutoSeed + Manual coexistence on save | ✅ `ManualLayoutManager.SaveLayout` |
| Prefer Manual over AutoSeed on load | ✅ `SelectPreferredVariant` |
| Legacy flat `Layouts` dictionary | ✅ migrated via `UpdateLegacyLayoutIndex` |

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
bool SaveVariant(string groupKey, string variantId, string displayName, ManualLayoutOrigin origin, List<RadialExtension> extensions, bool setAsDefault);
bool DeleteVariant(string groupKey, string variantId);
bool SetDefaultVariant(string groupKey, string variantId);
string? GetSelectedVariantId(string groupKey);          // persisted user preference
bool SetSelectedVariantId(string groupKey, string variantId);
```

Add `ManualLayoutSummary` record in `Models/` (variant id, display name, origin, timestamp, is default, marker count).

### Tasks

1. Extend `ManualLayoutCollection` with optional `SelectedVariants` dictionary (`groupKey → variantId`) for per-group user choice.
2. Implement `ListVariants`, `LoadVariant`, `SaveVariant` (support save-as with new `variantId`), `DeleteVariant`, `SetDefaultVariant`.
3. Update `LoadLayout(string key)` to honor `SelectedVariants` when set; fall back to current prefer-Manual logic.
4. Guard: `DeleteVariant` must not remove the last `AutoSeed` default unless user confirms (UI phase); service returns false if it would leave group empty.
5. Guard: seed regeneration (`Origin = AutoSeed`) must never overwrite `Origin = Manual` variants — only update matching AutoSeed variant ids.
6. Wire `LayoutEditorController` to use new APIs for save/load/delete instead of assuming `"manual-default"`.
7. Unit tests for: list, save-as second manual variant, select variant B, delete variant, AutoSeed regen preserves manual variants.

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
4. **Save As:** dialog for display name → new `variantId` (slug from name + short guid suffix).
5. **Delete:** enabled only for Manual variants; confirm dialog.
6. **Set as default:** optional checkbox or command — marks variant `IsDefault` for its origin class.
7. Visual indicator when active layout is AutoSeed vs Manual vs imported.
8. Status text shows `Loaded: {DisplayName} ({Origin})`.

**Acceptance:**

- Manual smoke: create two manual variants for same cluster, switch between them, restart app, selection persists
- AutoSeed variant remains after saving a manual copy

## Phase 3 — Seed generator integration

**Deliverables:** regenerated seeds update AutoSeed variants only.

### Files

| Action | Path |
|--------|------|
| Modify | `Tools/ManualLayoutSeedGenerator/Program.cs` (from seed alignment plan) |
| Modify | `Services/ManualLayoutManager.cs` |

### Tasks

1. Seed generator writes/updates `variantId = "seed-default"`, `Origin = AutoSeed`, `IsDefault = true` per group.
2. If group already has Manual variants, merge — do not delete them.
3. Bump `GeneratorVersion`; if version changes, optionally flag AutoSeed variants for review (log warning only in MVP).
4. Add test: regen seeds on file with existing Manual variant → Manual preserved, AutoSeed updated.

**Acceptance:**

- Running seed generator twice updates AutoSeed endpoints without touching Manual variants

## Phase 4 — Documentation and verification

### Tasks

1. Update [MANUAL_LAYOUT_EDITOR.md](../../MANUAL_LAYOUT_EDITOR.md) — variant model, UI flows, JSON schema examples with `LayoutGroups` and multiple variants.
2. Update [VISUAL_CONFIG.md](../../VISUAL_CONFIG.md) if new config flags added (e.g. `AllowMultipleVariants`).
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

1. **Max variants per group:** cap at 10 for MVP to avoid unbounded JSON growth?
2. **Import/export:** defer single-variant export to a follow-up?
3. **Variant picker outside edit mode:** defer; MVP requires edit mode to switch.

## Recommended MVP Decisions

1. Cap at 10 variants per group; oldest Manual variant evicted with confirmation.
2. Defer import/export.
3. Variant switching only in edit mode for MVP.

## Definition of Done

- Users can save-as, list, load, and delete manual variants per cluster
- AutoSeed and Manual variants coexist; seeds never overwrite manual work
- Selected variant persists across sessions
- `scripts/verify.ps1` passes
