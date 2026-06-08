---
status: active
owner: agent
started: 2026-06-07
requirements_ref: composite-pins-manual-layout
---

# Composite Pins — Manual Layout Phases Plan

Extends the composite-pin rendering pipeline to fully support manually-edited layouts:
edit mode drag → save → reload preserves composite rendering; per-pin shaft overrides;
cached assignments; multi-variant layout management.

Parent plan: [pin-parts-composite-placement-plan.md](pin-parts-composite-placement-plan.md)
Fix context:  [composite-pin-head-placement-fix-plan.md](composite-pin-head-placement-fix-plan.md)

---

## Architecture Context

### How the current pipeline works (key facts from code inspection)

| Area | Key classes / files |
|------|--------------------|
| Edit entry/exit | `MainWindow.OnEditLayoutButtonClick`, `LayoutEditorController.EnterEditMode/ExitEditMode` |
| Drag | `OnMarkerDragStart/Move/End` in `MainWindow.xaml.cs`; `ExtensionLineRenderer.MoveLineEndpoint` |
| Save | `LayoutEditorController.BuildExtensions` → `ManualLayoutManager.SaveLayout` → `manual-layouts.json` |
| Load / replay | `ApplyManualLayout` in `MainWindow.xaml.cs`; calls `TryApplyCompositePinMarker` per extension |
| Composite render | `CompositePinPlanningService.BuildPlan` → `CompositePinRenderPlanBuilder` → `CompositePinMarker` |
| Shaft selection | `PinPartPlacementCalculator.CalculatePlacement` (scores by angle + length error) |
| Head selection | `CompositePinPlanningService.SelectHeadForLocation` (hash of `locationId` mod candidate count) |
| Layout keying | `LayoutKeyGenerator.GenerateKey` — encodes sorted-location SHA256 + zoom + canvas size + cluster params |
| Disk caches | `ZoomedRegionCache`, `ClusterCache`, `AnimationFrameCache` — all use version-prefixed SHA256 keys under `%AppData%/InteractiveWorldMap/` |

### Critical gaps this plan closes

1. **Edit mode downgrades composites to legacy.** `EnterEditMode` calls `UpdateMarkerPositions`, which
   rebuilds every marker as a plain `ImagePinMarker`. During drag the user sees legacy pins, not composites.
   On exit, composites are rebuilt from updated endpoints — this part already works. Gap: no explicit
   handshake that communicates "these are the new endpoints; rebuild assignments from them."

2. **No "Reassign Pins" action.** After dragging endpoints, there is no way to trigger the shaft/head
   selector without leaving and re-entering the cluster view.

3. **Composite assignment not persisted in layout.** `ManualLayoutMarker` stores only name, original
   position, extended position, angle, and line length. Which shaft pair and head were chosen is
   recomputed on every replay via hash-based determinism. If the geometry file is edited or candidates
   are reordered, replays produce different visuals.

4. **No render-plan cache.** `CompositePinRenderPlanBuilder` is called synchronously in
   `TryApplyCompositePinMarker` for every marker on every render. There is no cache keyed to a
   specific layout+geometry+config state.

5. **Right-click shaft override not modelled.** No data model or UI path for a per-pin manual
   shaft/head choice that survives a save/reload cycle.

6. **Multiple layout variants exist in the storage model** (`ManualLayoutCollection.LayoutGroups`) but
   there is no UI for browsing, creating, renaming, or selecting between variants. `LoadLayout` always
   returns the single manual-default variant. "Last used" variant preference is not persisted.

---

## Phase 1 — Edit Mode / Composite Roundtrip + Reassign Pins Button

### Goal
After dragging endpoints in edit mode and saving, the reloaded view renders composite pins
(not legacy pins) with freshly computed shaft/head assignments. A "Reassign Pins" button
re-runs the shaft/head selector on the current set of extensions without requiring a
drag or save first.

### Behaviour spec

| Trigger | What happens |
|---------|-------------|
| Enter edit mode | Composite pins are replaced by legacy draggable `ImagePinMarker` per current behaviour. No change needed here. |
| Drag endpoint | Extension line and draggable marker reposition per current behaviour. |
| Click "Reassign Pins" | For each current extension (manual or auto), re-runs `PinPartPlacementCalculator.CalculatePlacement` + `SelectHeadForLocation`; rebuilds composite markers in place; does not modify saved layout. |
| Click "Save Layout" | Saves endpoint positions as now. In Phase 2 this will also persist the current shaft assignment. |
| Exit edit mode | `ApplyManualLayout` / `TryApplyCompositePinMarker` already runs; no structural change needed provided gap below is closed. |

### Gap to close: stale assignment after drag-save-reload

When `ApplyManualLayout` is called after a manual save, it calls `TryApplyCompositePinMarker` which
calls `BuildPlan` which calls `CalculatePlacement`. So the composite is rebuilt from scratch on
load — this is already correct. The only real gap is:

- After the user saves and exits edit mode **in the same session**, `ExitEditMode` currently calls
  `UpdateMarkerPositions` which re-applies the active display state. If the layout was just saved
  and `_activeLayout != null`, it should call `ApplyManualLayout` rather than the auto-layout path.
  Verify this is what the current `ExitEditMode` flow does; patch if not.

### New UI element: "Reassign Pins" button

- Add to the edit-mode panel in `Views/EditModePanel.xaml` (or wherever the Save/Cancel buttons live).
- Wire to `MainWindow.OnReassignPinsButtonClick`:
  1. Collect current extensions (from `_denseGroups[activeGroup].Extensions` or from the active
     manual layout state).
  2. For each extension, call `_compositePinPlanningService.BuildPlan(target, config)`.
  3. Call `marker.SetCompositeImages(plan)` in place (marker is already on canvas).
  4. Do **not** auto-save; user explicitly saves when satisfied.
- The button should be enabled only when composite rendering is active
  (`CanUseCompositePins() == true`).

### Files affected

| File | Change |
|------|--------|
| `Views/EditModePanel.xaml` (or equivalent) | Add "Reassign Pins" button |
| `MainWindow.xaml.cs` | Add `OnReassignPinsButtonClick`; verify `ExitEditMode` calls `ApplyManualLayout` when a manual layout is active |
| `Services/CompositePinPlanningService.cs` | Expose a `RebuildAssignments(IEnumerable<PinPlacementTarget>)` helper if needed |

### Acceptance

- User drags endpoints → saves → exits edit mode → sees composite pins at new positions.
- "Reassign Pins" button visually updates composite pins without saving.
- All 211 existing tests pass (no logic changes to the render pipeline).

---

## Phase 2 — Store Shaft/Head Assignments in Saved Layouts

### Goal
The saved layout carries the shaft pair and head geometry key for each pin so that replay
produces exactly the same composite visual regardless of geometry-file reordering or candidate
count changes.

### Data model changes

**`ManualLayoutMarker`** (in `Models/ManualLayout.cs`) — add optional fields:

```csharp
// Nullable — absent for legacy entries that predate Phase 2.
public string? PairId            { get; init; }   // e.g. "pin_07"
public string? HeadGeometryKey   { get; init; }   // e.g. "pin_07_head.png"
```

These are written on save (Phase 2+) and read on load. If absent, the existing
hash-based fallback is used unchanged (backward-compatible).

### Save path

In `LayoutEditorController.BuildExtensions` (or `TrySave`), after computing `ManualLayoutMarker`
list, look up the composite plan that was active at save time:

```csharp
// _compositePinPlanningService.LastBuiltPlans is a Dict<locationId, CompositePinRenderPlan>
// populated by TryApplyCompositePinMarker in the current session.
if (_compositePlans.TryGetValue(marker.LocationName, out var plan))
{
    marker = marker with { PairId = plan.PairId, HeadGeometryKey = plan.HeadSourcePath };
}
```

`CompositePinPlanningService` should expose a `GetLastPlan(locationId)` accessor or maintain an
internal `Dictionary<string, CompositePinRenderPlan>` populated each time `BuildPlan` is called.

### Load path

In `TryApplyCompositePinMarker`, after locating the relevant `ManualLayoutMarker`:

```csharp
PinPartGeometryEntry? headOverride = null;
if (marker.PairId != null)
    preferredPairId = marker.PairId;    // passed to CalculatePlacement as preferred pair
if (marker.HeadGeometryKey != null)
    headOverride = LookupHeadByKey(marker.HeadGeometryKey);   // already possible via geometry dict
```

`PinPartPlacementCalculator.CalculatePlacement` receives an optional `preferredPairId`; if that
pair is in the candidate list it is returned directly (no scoring), otherwise the normal selection
runs as fallback.

### Keying concern

The `PairId` is the geometry key (e.g. `"pin_07"`), not a positional index. It is stable as long
as the entry exists in `pin_part_geometry.json`. If the entry is removed, the fallback selection
runs. No hash fragility.

### Files affected

| File | Change |
|------|--------|
| `Models/ManualLayout.cs` | Add `PairId`, `HeadGeometryKey` to `ManualLayoutMarker` |
| `Services/LayoutEditorController.cs` | Populate new fields on save from last-built plan cache |
| `Services/CompositePinPlanningService.cs` | Maintain `_lastPlans` dict; add `GetLastPlan` accessor |
| `Services/PinPartPlacementCalculator.cs` | Add optional `preferredPairId` parameter to `CalculatePlacement` |
| `MainWindow.xaml.cs` | Pass `preferredPairId` and `headOverride` through to `BuildPlan` on manual-layout replay |

### Acceptance

- Save layout, reorder entries in `pin_part_geometry.json`, reload → same shaft/head as saved.
- Missing or unrecognised `PairId` falls back gracefully; no exception.
- Legacy layouts without `PairId` behave as before.

---

## Phase 3 — Right-Click Shaft/Head Override

### Goal
In normal (non-edit) mode, right-clicking a composite pin shows a context menu listing
available shafts for the current angle. Selecting one re-renders that pin immediately and
marks the choice as a per-pin override in the active layout state (saved on the next explicit
save action).

### UX spec

1. User right-clicks the shaft area of a composite `CompositePinMarker`.
2. Context menu appears: "Change shaft" → submenu listing available shafts by pair ID and a
   short descriptor (e.g. angle class, length class).
3. User clicks a shaft option:
   a. `BuildPlan` is called with `preferredPairId = selected` and the existing `PinPlacementTarget`.
   b. The composite marker is updated in-place.
   c. The `PairId` for that location is updated in an in-memory overlay
      (`_pendingPairOverrides: Dictionary<string, string>`).
4. A subtle "unsaved changes" indicator appears near the edit button (or an auto-save fires,
   depending on UX preference — leave as a decision for implementation).
5. Next "Save Layout" writes `_pendingPairOverrides` into `ManualLayoutMarker.PairId` fields
   (per Phase 2 model).

### Context-menu trigger

`CompositePinMarker` already supports hover/click. Add a `MouseRightButtonUp` handler.
It needs to communicate back to `MainWindow` the `LocationId` and canvas position so the
menu can be constructed and wired there (keeping `CompositePinMarker` view-only).

Pattern: raise a routed event `ShaftOverrideRequested` carrying `LocationId`.

### Shaft list construction

In `MainWindow.OnShaftOverrideRequested(locationId)`:

```csharp
var target = _activePinTargets[locationId];
var candidates = _compositePinPlanningService.GetCandidates();
// Score all candidates against target; sort by score.
var scored = _placementCalculator.ScoreAll(target, candidates, config);
// Build menu items from scored list; mark current selection.
```

`PinPartPlacementCalculator` should expose a `ScoreAll` method (currently it only returns the
winner).

### Head override (later refinement, same phase)

Optionally add a "Change head" sub-option that lists heads in the same pair family. Defer this
to a second pass within Phase 3 if shaft-only is shipped first.

### Files affected

| File | Change |
|------|--------|
| `Views/CompositePinMarker.xaml.cs` | Add `ShaftOverrideRequested` routed event; `MouseRightButtonUp` handler |
| `MainWindow.xaml.cs` | Handle event; build context menu; call `BuildPlan` with override; update `_pendingPairOverrides` |
| `Services/PinPartPlacementCalculator.cs` | Add `ScoreAll` method returning ranked list |
| `Services/CompositePinPlanningService.cs` | Add `GetCandidates()` accessor |
| `Models/ManualLayout.cs` | Already extended in Phase 2 |

### Acceptance

- Right-click on any composite shaft shows the context menu.
- Selecting a shaft re-renders the pin immediately.
- Save Layout persists the override.
- Reload shows the overridden shaft, not the hash-selected default.

---

## Phase 4 — Cache Composite Render Plans

### Goal
Avoid recomputing `CompositePinRenderPlan` on every render by caching per
(layout variant, geometry version, relevant config hash). Follows the
`ClusterCache` / `ZoomedRegionCache` pattern already in the codebase.

### Cache key design

Modelled on `ClusterCache` (SHA256 of inputs, stored as JSON under `%AppData%`):

```
CompositePinPlanCache key =
  SHA256(
    "v{CacheVersion}",
    groupKey,              // LayoutKeyGenerator output — encodes locations + zoom + canvas size
    variantId,             // from ManualLayout.VariantId (or "auto" for auto-generated layouts)
    geometryHash,          // SHA256 of pin_part_geometry.json content (computed once on load)
    configHash             // SHA256 of relevant PinPartConfig fields:
                           //   TargetHeadRadiusPx, TargetShaftHalfWidthPx,
                           //   SelectionMode, MaxResidualRotationDeg, Min/MaxStretchFactor
  )
```

Storage file: `%AppData%/InteractiveWorldMap/composite_pin_plan_cache/{key}.json`

`CacheVersion` starts at 1. Bumping it invalidates all cached plans globally.

### Cached data

One JSON file per key containing a list of `CachedCompositePlanEntry`:

```csharp
public sealed record CachedCompositePlanEntry(
    string LocationId,
    CompositePinRenderPlan Plan);
```

`CompositePinRenderPlan` already contains all scalar fields and `Point` values needed for
deterministic replay; it is JSON-serialisable with a minimal converter for `Matrix` and `Point`.

### Cache service: `CompositePinPlanCache`

Place in `Services/CompositePinPlanCache.cs`, following `ClusterCache.cs` as a template:

```csharp
public class CompositePinPlanCache
{
    TryLoad(string key, out IReadOnlyList<CachedCompositePlanEntry> plans) → bool
    Save(string key, IReadOnlyList<CachedCompositePlanEntry> plans) → void
    Invalidate(string key) → void   // called by ManualLayoutManager.SaveLayout
    ClearAll() → void               // on CacheVersion bump
}
```

### Integration points

- `CompositePinPlanningService.BuildPlan` checks cache before calling `CompositePinRenderPlanBuilder`.
- On cache miss, builds normally and writes result to cache.
- `ManualLayoutManager.SaveLayout` calls `_planCache.Invalidate(key)` after write so next
  replay rebuilds from the new endpoints/assignments.
- Auto-layout path also benefits: for the same cluster at the same zoom, the second render is
  a cache hit.

### Auto-layout caching caveat

Auto-layout plans are cached with `variantId = "auto"`. If the automatic extension calculator
produces different endpoint positions (e.g. after an `AdjustExtensions` pass), the cache key
will differ from a prior run. The geometry hash and config hash will match, so only the
endpoint-encoded part of the group key guards this. Since the group key encodes zoom + canvas
size + location set (not individual positions), this could cause stale cache hits if
`AdjustExtensions` is non-deterministic for the same input. Mitigation: ensure
`RadialExtensionCalculator` is deterministic for fixed inputs (it currently is, per inspection).

### Files affected

| File | Change |
|------|--------|
| `Services/CompositePinPlanCache.cs` | New file |
| `Models/CachedCompositePlanEntry.cs` | New file (or nested record) |
| `Services/CompositePinPlanningService.cs` | Check/write cache in `BuildPlan` |
| `Services/ManualLayoutManager.cs` | Call `_planCache.Invalidate(key)` on save |
| `AppBootstrapper` (or wherever services are wired) | Register `CompositePinPlanCache` |

### Acceptance

- Second render of same cluster at same zoom is a cache hit (log a counter or debug flag).
- Modifying `visual-config.json` (any relevant field) invalidates cache.
- Saving a manual layout invalidates the cache for that key.
- No stale composite shown after a layout save and reload.

---

## Phase 5 — Multiple Saved Layouts + Last-Used Memory

### Goal
Surface the existing multi-variant storage model (`ManualLayoutCollection.LayoutGroups`) to
the user: list named variants, create a new variant from the current state, rename/delete, and
remember which variant was last selected per region.

### Current state

`ManualLayoutCollection` already stores `LayoutGroups` → `ManualLayoutGroup` → `List<ManualLayout>`
(variants). `LoadLayout` already has priority logic (Manual+Default > AutoSeed+Default > …).
`SaveLayout` already creates/updates the single manual-default variant.

What is missing:

1. **UI to list and select variants** — no panel, no picker.
2. **"Save as new variant"** — currently saving always overwrites the single manual-default.
3. **Last-used preference** — no file records which variant was last viewed per group.

### Last-used preference store

Create `Services/LayoutPreferenceStore.cs` (new file) following the `ClusterCache` JSON pattern:

```
Storage: %AppData%/InteractiveWorldMap/layout_preferences.json
Structure:
{
  "lastUsedVariants": {
    "{groupKey}": "{variantId}",
    ...
  }
}
```

`groupKey` = the same key produced by `LayoutKeyGenerator.GenerateKey()` — already encodes
location set + zoom + canvas size + cluster params, which is exactly the right granularity.

`ManualLayoutManager.LoadLayout` checks `LayoutPreferenceStore.GetLastUsed(groupKey)` and
promotes that variant to highest priority before the current scoring logic.

`ManualLayoutManager.SaveLayout` (and any future "select variant" action) updates the preference.

### Layout variant panel (UI)

Add a collapsible "Layouts" section inside the edit-mode panel (or a small dropdown next to
the edit button in normal mode):

- Lists variants for the current group key (name + origin badge: Auto / Manual / Imported).
- "Active" check-mark on the current variant.
- Actions: **Select**, **Duplicate** (saves current state as a new named variant), **Rename**,
  **Delete** (disabled on the last remaining variant).

A "Save as new layout…" dialog replaces the current silent overwrite when the user clicks Save
while a named variant other than the default manual one is active.

### Variant selection without entering edit mode

"Select" in the panel immediately replays the chosen variant's composite layout onto the
current canvas. This is a read-only action — the user does not need to be in edit mode to
switch between variants.

### Files affected

| File | Change |
|------|--------|
| `Services/LayoutPreferenceStore.cs` | New file |
| `Services/ManualLayoutManager.cs` | Inject `LayoutPreferenceStore`; honour last-used in `LoadLayout`; update preference in `SaveLayout` |
| `Views/EditModePanel.xaml` | Add variant list, select/duplicate/rename/delete actions |
| `MainWindow.xaml.cs` | Handle variant select (calls `ApplyManualLayout`); handle duplicate/rename/delete |
| `Models/ManualLayout.cs` | No structural change needed; `VariantId`, `DisplayName`, `Origin`, `IsDefault` already present |

### Acceptance

- Selecting a variant in the panel replays it immediately (composite pins update).
- "Save as new" creates a second variant under the same group; both are listed.
- After selecting variant B, closing and reopening the same cluster auto-loads variant B.
- Deleting the active variant reverts to the auto-seed variant.

---

## Cross-Cutting Concerns

### Layout key stability

The cache key and preference key both use `LayoutKeyGenerator.GenerateKey()`. That key encodes:
- SHA256 of sorted location names
- Zoom level (truncated to 1 decimal)
- Canvas centre (rounded to 10 px)
- Canvas size
- Cluster parameters

This is the same grain as `ClusterCache` and `ZoomedRegionCache`. If the user resizes the
window by a small amount the group key changes and cache/preference lookup falls back gracefully.
No action needed; this is existing behaviour.

### Geometry file versioning

Phase 4 uses SHA256 of `pin_part_geometry.json` as part of the cache key. This means any
geometry recalibration (e.g. after Phase 3 of the head-placement fix plan) automatically
invalidates the render-plan cache. The per-pin `PairId` stored in the layout (Phase 2) is
unaffected unless the entry is removed.

### Test strategy

- All existing 211 tests must pass after each phase.
- Each new service (`CompositePinPlanCache`, `LayoutPreferenceStore`) should have unit tests
  covering: miss→build→hit, invalidate→miss, version bump→miss.
- `PinPartPlacementCalculator.ScoreAll` (Phase 3) should have unit tests matching the existing
  `CalculatePlacement` coverage.
- Edit-mode roundtrip (Phase 1) can be covered by an integration test that simulates drag →
  save → reload → assert composite plan matches.

---

## Phase Summary

| Phase | Scope | Key deliverable | Files added |
|-------|-------|-----------------|-------------|
| 1 | Edit mode roundtrip + Reassign Pins | Button; verified composite-on-reload flow | `Views/EditModePanel.xaml` edit |
| 2 | Store assignments in layout | `ManualLayoutMarker.PairId/HeadGeometryKey`; stable replay | Model + save/load path edits |
| 3 | Right-click shaft override | Context menu; per-pin override in active state | `CompositePinMarker` event; `PinPartPlacementCalculator.ScoreAll` |
| 4 | Cache render plans | `CompositePinPlanCache`; cache miss/hit/invalidate | `Services/CompositePinPlanCache.cs` |
| 5 | Multi-variant + last-used | Variant panel; `LayoutPreferenceStore` | `Services/LayoutPreferenceStore.cs`; `Views/EditModePanel.xaml` additions |
