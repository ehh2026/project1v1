---
status: active
owner: agent
started: 2026-06-07
revised: 2026-06-08
requirements_ref: composite-pins-manual-layout
---

# Composite Pins — Manual Layout Phases Plan

Extends the composite-pin rendering pipeline to fully support manually-edited layouts:
edit mode drag → save → exit/reload preserves composite rendering; per-pin shaft/head overrides;
cached render plans; coordination with multi-variant layout management.

**Parent plan:** [pin-parts-composite-placement-plan.md](pin-parts-composite-placement-plan.md)  
**Fix context:** [composite-pin-head-placement-fix-plan.md](composite-pin-head-placement-fix-plan.md)  
**Variant UI/API (Phase 5 scope):** [manual-layout-variants-plan.md](manual-layout-variants-plan.md) — canonical plan for multi-variant storage and picker UI  
**Seed alignment:** [manual-layout-seed-alignment-plan.md](manual-layout-seed-alignment-plan.md) — `SourceExtendedX/Y` on seed markers; user saves still use angle/length replay

---

## Revision History — Corrections from 2026-06-07 Review

The first draft of this plan had several factual errors. They are fixed below; this section records what was wrong so reviewers can spot drift.

| # | Was incorrect | Corrected to |
|---|---------------|--------------|
| 1 | “On exit edit mode, composites are rebuilt from updated endpoints — this part already works.” | **False today.** `ExitEditMode` always calls `UpdateMarkerPositions()`, which recalculates **auto** radial extensions and never calls `ApplyManualLayout`. Manual replay only happens on zoom-in via `_savedLayoutToApply`. |
| 2 | Referenced `Views/EditModePanel.xaml`. | Edit-mode buttons live in **`MainWindow.xaml`** (Save, Delete, Exit Edit Mode stack). |
| 3 | `ManualLayoutMarker` defined in `Models/ManualLayout.cs`. | Marker type is in **`Models/ManualLayoutMarker.cs`**. |
| 4 | `marker with { PairId = ... }` on save. | `ManualLayoutMarker` is a **class**, not a record — mutate properties normally. |
| 5 | `BuildPlan(target, config)` and `marker.SetCompositeImages(plan)`. | Real APIs: `BuildPlan(target, candidates, config)` and `CompositePinMarker.SetCompositeImages(shaftBitmap, headBitmap, plan, debug)`. |
| 6 | Assignment capture in `LayoutEditorController.BuildExtensions`. | `BuildExtensions` is static geometry only. Capture belongs in **`OnSaveLayoutButtonClick` / `TrySave`** after session plan cache is populated. |
| 7 | “Reassign Pins” enabled when `CanUseCompositePins() == true`. | `CanUseCompositePins()` returns **false in edit mode** by design. Reassign needs a dedicated path that bypasses the edit-mode gate while still using current canvas endpoints. |
| 8 | `_activePinTargets` dictionary and `GetCandidates()` on `CompositePinPlanningService`. | Neither exists. Targets are built from marker/line state in `MainWindow`; candidates are **`_pinPartGeometry`** from `ContentLoader`. |
| 9 | `LoadLayout` “always returns the single manual-default variant.” | `SelectPreferredVariant` ranks Manual+Default > AutoSeed+Default > Manual > AutoSeed; compatible-key fallback also exists. |
| 10 | `HeadGeometryKey` example `"pin_07_head.png"`. | Persist **`HeadSourcePath`** format used by render plans (e.g. `Pins_v2/parts/pin_07_head.png`). |
| 11 | Phase 5 proposed separate `LayoutPreferenceStore` in `%AppData%`. | **De-duplicated:** variant selection persistence belongs in **`manual-layout-variants-plan.md`** (`SelectedVariants` in `manual-layouts.json`). |
| 12 | `AppBootstrapper` for service wiring. | Services are constructed in **`MainWindow.xaml.cs`** (e.g. `CompositePinPlanningService` field initializer). |
| 13 | “All 211 existing tests.” | Test project currently lists **213** tests; gate is **`.\scripts\verify.ps1`** passing. |
| 14 | Head selection described as independent head pool. | `SelectHeadForLocation` hashes `locationId` against **ordered pair keys** in the geometry dict; shaft (`PairId`) and head (`HeadSourcePath`) are **decoupled** — both must be persisted. |
| 15 | Phase 4 cache key hashed `(locationId, orig, ext, …)` using screen-space positions. | Use `(locationName, angle, lineLength, pairId, headSourcePath)` — matches `ApplyManualLayout` replay inputs and is viewport-independent. Screen positions change on resize. |
| 16 | Reassign Pins: "set `marker.Content = new CompositePinMarker`" would detach drag handlers. | Keep the `ImagePinMarker` canvas child as the drag-handle wrapper; update its visual content only. Exact approach (content slot vs sibling overlay) to be decided before implementing Phase 1. |
| 17 | Phase 2 session cache lookup assumed `LocationId == LocationName` without stating it. | Added explicit verification requirement: confirm `PinPlacementTarget.LocationId` equals `location.Name` (= `ManualLayoutMarker.LocationName`) before relying on the identity. |
| 18 | Phase 1 `ExitEditMode` branch assumed `IsManualLayoutActive` is already true after save. | Added prerequisite check: verify `OnSaveLayoutButtonClick`/`TrySave` calls `SetManualLayoutActive(true)`. If not, add it. |

---

## Execution Checklist

### Phase status

- [x] Phase 1: Edit-mode roundtrip fix + Reassign Pins
  - [x] ExitEditMode replays manual layout (composite pins at saved positions)
  - [x] Auto Assign Pins button (renamed from Reassign Pins)
  - [x] OnEditLayoutButtonClick restores saved positions when re-entering edit mode
  - [x] BuildExtensions angle convention fix (atan2(dx,-dy) north-up, matching ApplyManualLayout)
  - [x] OnSaveLayoutButtonClick uses extension line endpoint as MarkerCenter (correct after Auto Assign Pins)
- [x] Phase 2: Persist shaft/head assignments in saved layouts
- [ ] Phase 3: Right-click shaft override (head override optional second pass)
- [ ] Phase 4: Composite render-plan disk cache
- [ ] Phase 5: **Delegated** — implement via [manual-layout-variants-plan.md](manual-layout-variants-plan.md)

### Recommended order

1. Phase 1 (unblocks drag → save → exit composite replay)
2. Phase 2 (stable assignment replay; prerequisite for Phase 3 persistence)
3. Phase 3 (per-pin overrides; depends on Phase 2 fields)
4. Phase 4 (performance; after assignment model is stable — cache key must include endpoint fingerprint)
5. Phase 5 — follow **manual-layout-variants-plan** (service API before UI; composite replay via `ApplyManualLayout` on variant switch)

### Harness gate (each phase)

- [ ] `.\scripts\verify.ps1` passes
- [ ] [CHANGELOG.md](../../../CHANGELOG.md) updated for user-visible behaviour
- [ ] No architecture-layer violations (`Tests/Architecture/LayerDependencyTests.cs`)
- [ ] **File-size check:** net growth in `MainWindow.xaml.cs` ≤ ~50 lines for the phase; new logic lives in Services (see modularity section below)

---

## Implementation Modularity (File-Size Guardrails)

> **Builder instruction:** This work must not bloat already-oversized files. Treat extraction as part of each phase, not a follow-up refactor.

**Current state:** `MainWindow.xaml.cs` is ~**2000 lines** (repo rule: keep `.cs` files under **800** — see [ARCHITECTURE.md](../../../ARCHITECTURE.md), [docs/REFACTORING_ASSESSMENT.md](../../REFACTORING_ASSESSMENT.md)). The naive reading of this plan adds Reassign, exit-replay, overrides, cache wiring, and menu construction to MainWindow — that would make the god-object problem worse.

**Default rule for all phases:**

1. **MainWindow stays a thin orchestrator** — event handlers delegate in ≤15 lines to a Service; no new private helper regions >30 lines.
2. **New behaviour → new Service types** under `Services/` (Models for DTOs only). Prefer one focused class per concern over expanding existing large files.
3. **Do not** add substantial logic to `LayoutEditorController` unless it is pure layout state/data (no WPF, no bitmap loading). Target stays ~250 lines.
4. **Partial classes are a last resort** — `MainWindow.CompositePins.cs` is acceptable only if it *moves* code out of the main file (net line count across partials must not grow). Prefer Services over partials.
5. **Unit-test Services directly** — reduces need for MainWindow integration tests and keeps code-behind thin.

### Preferred extraction map (implementers: create these rather than inline in MainWindow)

| Concern | Preferred home | MainWindow keeps |
|---------|----------------|------------------|
| Apply composite to one/batch markers, edit-mode bypass, bitmap cache | **`Services/CompositePinApplicationService.cs`** (new) | Inject + call `ApplyToMarker(...)`, `ReassignAll(...)` |
| Build `PinPlacementTarget` from marker + extension line | **`Services/CompositePinTargetFactory.cs`** or static helper on ApplicationService | Pass marker reference into service |
| Exit edit → load + replay manual layout | **`LayoutEditorController.TryReplayManualLayout(key)`** returns `ManualLayout?`; replay rendering stays in ApplicationService + existing `ApplyManualLayout` body moved incrementally | `ExitEditMode` branch: load + delegate |
| Phase 2: enrich markers with `PairId` / `HeadSourcePath` on save | **`Services/ManualLayoutAssignmentEnricher.cs`** (new) | `OnSaveLayoutButtonClick`: call enricher before `TrySave` |
| Phase 2: last-built plan cache | **`CompositePinPlanningService`** (extend existing ~50-line file) | No cache logic in MainWindow |
| Phase 3: rank shafts for menu | **`PinPartPlacementCalculator.ScoreAll`** + **`Services/CompositePinShaftMenuModel.cs`** (DTO list) | Build WPF `ContextMenu` from DTOs only |
| Phase 3: pending overrides state | **`LayoutEditorController`** or small **`Services/ManualLayoutOverrideStore.cs`** | Read/write via controller; no dict in MainWindow |
| Phase 4: disk cache | **`Services/CompositePinPlanCache.cs`** + cache lookups inside **`CompositePinPlanningService`** or **`CompositePinApplicationService`** | No cache key hashing in MainWindow |
| Edit-mode toolbar button | **`MainWindow.xaml`** only | One-line `Click` → service call |

**Anti-patterns to avoid:** new `_pending*` dictionaries in MainWindow; copy-pasted `BuildPlan` + `LoadPinPartBitmap` blocks; context-menu scoring logic in code-behind; cache invalidation scattered across three MainWindow methods.

---

## Architecture Context

### How the current pipeline works (verified against code)

| Area | Key classes / files |
|------|---------------------|
| Edit entry | `MainWindow.OnEditLayoutButtonClick` → `LayoutEditorController.EnterEditMode` → `UpdateMarkerPositions` (legacy pins) |
| Edit exit | `MainWindow.ExitEditMode` → `UpdateMarkerPositions` (**bug:** does not replay manual layout) |
| Drag | `OnMarkerDragStart/Move/End` in `MainWindow.xaml.cs`; `ExtensionLineRenderer.MoveLineEndpoint` |
| Save | `OnSaveLayoutButtonClick` → `LayoutEditorController.BuildExtensions` → `TrySave` → `ManualLayoutManager.SaveLayout` → `manual-layouts.json` |
| Load on zoom | `ShowZoomedView` → `_savedLayoutToApply` → `ApplyManualLayout` |
| Composite render | `TryApplyCompositePinMarker` → `CompositePinPlanningService.BuildPlan` → `CompositePinRenderPlanBuilder` → `CompositePinMarker` |
| Edit-mode composite gate | `CanUseCompositePins()` requires `!_layoutEditor.IsEditMode` |
| Shaft selection | `PinPartPlacementCalculator.CalculatePlacement` (angle + length scoring) |
| Head selection | `CompositePinPlanningService` private `SelectHeadForLocation` — hash of `locationId` over **all pair keys**; independent of shaft pick |
| Layout keying | `LayoutKeyGenerator.GenerateKey` — sorted-location hash + zoom + canvas size + cluster params (**not** per-marker endpoints) |
| Disk caches | `ZoomedRegionCache`, `ClusterCache`, `AnimationFrameCache` under `%AppData%/InteractiveWorldMap/` |
| Service wiring | `MainWindow.xaml.cs` field initializers (no `AppBootstrapper`) |

### Critical gaps this plan closes

1. **Exit edit mode does not replay manual layouts.** After save, user exits edit mode but `UpdateMarkerPositions` recomputes auto extensions, discarding saved endpoint positions until re-zoom.
2. **No “Reassign Pins” action.** No way to re-run shaft/head selection on current endpoints without re-entering the cluster view.
3. **Assignments not persisted.** `ManualLayoutMarker` stores endpoints/angle/length only; shaft/head recomputed via scoring + hash on every replay.
4. **No render-plan cache.** Every `TryApplyCompositePinMarker` call rebuilds plans synchronously.
5. **No per-pin override UI.** No right-click path for manual shaft choice surviving save/reload.
6. **Multi-variant UI/API.** Storage model supports variants; picker and `SelectedVariants` persistence are scoped to [manual-layout-variants-plan.md](manual-layout-variants-plan.md).

---

## Phase 1 — Edit Mode Roundtrip Fix + Reassign Pins

### Goal

After drag → save → **exit edit mode in the same session**, the user sees **composite pins at saved endpoint positions**. A **Reassign Pins** button re-runs shaft/head selection on the **current canvas endpoints** without saving.

### Behaviour spec

| Trigger | What happens |
|---------|--------------|
| Enter edit mode | Legacy draggable `ImagePinMarker` per current behaviour (`CanUseCompositePins()` false). **No change.** |
| Drag endpoint | Extension line + marker move per current behaviour. **No change.** |
| Save layout | Persists endpoints via `TrySave`; user **stays in edit mode** on legacy pins. **No change** until Phase 2 adds assignment fields. |
| Exit edit mode | **Fix:** if `_layoutEditor.IsManualLayoutActive` and `CurrentLayoutKey` set, load layout and `ApplyManualLayout`; else if manual layout was just saved, `TryLoad(CurrentLayoutKey)` then `ApplyManualLayout`; else `UpdateMarkerPositions()` (auto path). |
| Reassign Pins | Rebuild composite visuals for visible extensions using **current** marker centers + line start points; **does not** save; **does not** exit edit mode. |

### Fix: `ExitEditMode` manual-layout replay

`ApplyManualLayout` already rebuilds composites correctly when invoked — it calls `TryApplyCompositePinMarker` per marker. The missing branch:

```csharp
private void ExitEditMode()
{
    _layoutEditor.ExitEditMode();
    // ... detach drag handlers ...

    if (_layoutEditor.IsManualLayoutActive && _layoutEditor.CurrentLayoutKey != null)
    {
        var layout = _layoutEditor.TryLoad(_layoutEditor.CurrentLayoutKey);
        if (layout != null)
        {
            ApplyManualLayout(layout);
            return;
        }
    }

    UpdateMarkerPositions();
}
```

Also consider: `UpdateMarkerPositions` long-term should honour `IsManualLayoutActive` anywhere it is called after edit mode (not only exit).

**Prerequisite check — `IsManualLayoutActive` must be true after save:** The new `ExitEditMode` branch only fires if `_layoutEditor.IsManualLayoutActive` is already true. Verify that `OnSaveLayoutButtonClick` (or `TrySave`) calls `_layoutEditor.SetManualLayoutActive(true)` after a successful save. If it does not, add that call. Without it the new branch is dead code for a first-time save in the current session.

### Reassign Pins — design decision (resolves edit-mode gate)

**Problem:** `CanUseCompositePins()` blocks composites during edit mode; markers are `ImagePinMarker`, not `CompositePinMarker`.

**MVP approach:** shared helper `ApplyCompositePinToMarker(marker, originalScreen, extendedScreen, bypassEditModeCheck: true)` extracted from `TryApplyCompositePinMarker`. Reassign uses current canvas geometry:

1. For each visible marker with an extension line, read `originalScreen` (map point) and `extendedScreen` (marker center from `Canvas.GetLeft/Top`).
2. Build `PinPlacementTarget` and call `BuildPlan(target, _pinPartGeometry, _visualConfig.PinParts)`.
3. Load shaft/head bitmaps; update the marker's visual **without replacing the canvas child**:
   - Keep the `ImagePinMarker` wrapper on the canvas (drag handlers remain attached to it).
   - Update its content/visual to show the composite render (e.g. set an inner `Image` source or overlay a `CompositePinMarker` as its content if `ImagePinMarker` supports a content slot).
   - If `ImagePinMarker` has no content slot, wrap the existing approach: add a `CompositePinMarker` as a sibling canvas child at the same Z-position and hide the `ImagePinMarker` visual while keeping the drag-handle element. Document the chosen approach explicitly before implementing.
4. After Reassign, drag handlers remain on the original wrapper element — dragging still works. Re-clicking Reassign rebuilds the composite at the new position.

**Button placement:** `MainWindow.xaml` edit-mode toolbar (alongside Save / Delete / Exit).

**Enable when:** `_visualConfig.PinParts.UseCompositeRendering` and geometry loaded — **not** `CanUseCompositePins()`.

### Files affected

| File | Change |
|------|--------|
| `MainWindow.xaml` | Add “Reassign Pins” button to edit-mode `StackPanel` |
| `MainWindow.xaml.cs` | Thin handlers only: `ExitEditMode` branch, `OnReassignPinsButtonClick` → delegate (**≤~50 lines net new**) |
| **`Services/CompositePinApplicationService.cs`** *(new, preferred)* | `ApplyToMarker`, `ReassignAll`, edit-mode bypass; absorb logic from `TryApplyCompositePinMarker` over time |
| `Services/LayoutEditorController.cs` | Optional: `TryReplayManualLayout(key)` data helper |
| `Services/CompositePinPlanningService.cs` | Optional: `BuildPlan` overload stubs for Phase 2 |

> **Builder note:** Do not grow `TryApplyCompositePinMarker` in MainWindow — move its body to `CompositePinApplicationService` as part of this phase (MainWindow calls service; existing tests may target service).

### Acceptance

- Drag endpoints → Save → Exit edit mode → composite pins at **saved** positions (same session, no re-zoom).
- Reassign Pins updates composite appearance at current endpoints without writing `manual-layouts.json`.
- After Reassign, user can still drag legacy markers in edit mode.
- All existing tests pass; add integration test: simulate save + exit → assert `ApplyManualLayout` path taken when `IsManualLayoutActive`.

---

## Phase 2 — Store Shaft/Head Assignments in Saved Layouts

### Goal

Saved layouts carry shaft pair and head source path per pin so replay matches the saved visual even if geometry JSON key order changes or candidate count changes.

### Head/shaft decoupling (required context)

`BuildPlan` selects shaft via `CalculatePlacement` but head via separate `SelectHeadForLocation`. Persist **both**:

- `PairId` — shaft geometry key (e.g. `"pin_07"`)
- `HeadSourcePath` — full content path from `CompositePinRenderPlan.HeadSourcePath` (e.g. `Pins_v2/parts/pin_03_head.png`)

### Data model changes

**`Models/ManualLayoutMarker.cs`** — add optional fields:

```csharp
/// <summary>Shaft pair id from CompositePinRenderPlan.PairId. Null = use scorer/hash fallback.</summary>
public string? PairId { get; set; }

/// <summary>Head asset path from CompositePinRenderPlan.HeadSourcePath. Null = use hash fallback.</summary>
public string? HeadSourcePath { get; set; }
```

Backward-compatible: absent fields → current behaviour unchanged.

**Note:** User-initiated saves still omit `SourceExtendedX/Y` (only seeds populate those). Angle/length replay remains the viewport-independent path per existing `ApplyManualLayout` logic.

### Session plan cache

`CompositePinPlanningService` maintains `Dictionary<string, CompositePinPlanningResult> _lastResultsByLocation` updated on every `BuildPlan` call. Expose `TryGetLastResult(string locationId, out CompositePinPlanningResult? result)`.

### Save path

In `OnSaveLayoutButtonClick`, after `BuildExtensions` and before/after `TrySave`:

1. Build `ManualLayoutMarker` list (today `SaveLayout` maps from extensions — extend `ManualLayoutManager.SaveLayout` or enrich markers in controller).
2. For each marker, look up by **`LocationName`** (the field on `ManualLayoutMarker`). The session cache key must use the same string. `PinPlacementTarget.LocationId` is set from the `Location` object passed to it — confirm this equals `location.Name` (the same string stored as `ManualLayoutMarker.LocationName`). Add an explicit assertion or normalisation step at the lookup site if any doubt; do not assume identity without confirming.
3. If `TryGetLastResult(locationName, out var result)`, set `PairId` and `HeadSourcePath` from `result.RenderPlan`.
4. If Reassign or override ran but composite was not applied to every pin, fall back to scorer for missing entries (log warning).

Prefer extending `TrySave` / `SaveLayout` to accept optional assignment enrichment rather than patching in `BuildExtensions`.

### Load path

Extend `TryApplyCompositePinMarker` (or `BuildPlan`) to accept optional `preferredPairId` and `headGeometryEntry` / `headSourcePath`:

- `preferredPairId` → `CalculatePlacement` uses that pair if present in candidates, else scores normally.
- `headSourcePath` → resolve geometry entry by matching `HeadFile` or path in `_pinPartGeometry`; else `SelectHeadForLocation` fallback.

Pass saved fields from `ManualLayoutMarker` when `ApplyManualLayout` runs.

### Files affected

| File | Change |
|------|--------|
| `Models/ManualLayoutMarker.cs` | Add `PairId`, `HeadSourcePath` |
| `Services/CompositePinPlanningService.cs` | Last-result cache; `BuildPlan` override parameters |
| `Services/PinPartPlacementCalculator.cs` | Optional `preferredPairId` on `CalculatePlacement` |
| **`Services/ManualLayoutAssignmentEnricher.cs`** *(new, preferred)* | Map extensions + last-built plans → markers with assignment fields |
| `Services/ManualLayoutManager.cs` | Persist new marker fields in JSON |
| `Services/LayoutEditorController.cs` | Accept enriched marker list in `TrySave` (or enrich inside controller via enricher) |
| `Services/CompositePinApplicationService.cs` | Pass overrides from layout markers into `BuildPlan` on replay |
| `MainWindow.xaml.cs` | Wire enricher on save only if not handled in controller (**minimal**) |
| `Tests/ManualLayoutManagerTests.cs` or new | Round-trip `PairId` / `HeadSourcePath` |

> **Builder note:** Save-path assignment logic belongs in `ManualLayoutAssignmentEnricher` + `LayoutEditorController.TrySave`, not in `OnSaveLayoutButtonClick` beyond a single delegate call.

### Acceptance

- Save layout, reorder keys in `pin_part_geometry.json`, reload → same shaft/head as saved.
- Missing/unknown `PairId` or `HeadSourcePath` → graceful fallback, no throw.
- Legacy layouts without new fields → unchanged behaviour.

---

## Phase 3 — Right-Click Shaft Override

### Goal

In **non-edit** mode, right-click a composite pin to pick an alternate shaft; choice lives in memory until Save, then persists via Phase 2 fields.

### UX spec

1. Right-click composite pin → context menu “Change shaft” with ranked candidates.
2. On select: rebuild that pin with `preferredPairId`; update pending overrides via **`ManualLayoutOverrideStore`** or `LayoutEditorController` (not a new dict in MainWindow).
3. Unsaved indicator near edit toolbar (or status text) when overrides pending.
4. Save layout (edit mode) writes overrides into `ManualLayoutMarker` fields.

**Deferred within phase:** “Change head” submenu (lists entries by pair key / head file).

### Context-menu trigger

`CompositePinMarker` raises routed event `ShaftOverrideRequested` with `LocationName`. `MainWindow` builds menu (Views stay free of Services).

### Shaft list construction

```csharp
// MainWindow: build target via CompositePinApplicationService / target factory, then:
var items = _shaftMenuModelBuilder.BuildMenuItems(target, _pinPartGeometry, config, currentPairId);
// Map items → WPF ContextMenu; on select call _compositePinApplication.ApplyToMarker(..., preferredPairId)
```

Add `PinPartPlacementCalculator.ScoreAll` — same ranking as `CalculatePlacement` but returns full ordered list.

### Files affected

| File | Change |
|------|--------|
| `Views/CompositePinMarker.xaml.cs` | `ShaftOverrideRequested` event; `MouseRightButtonUp` only |
| `MainWindow.xaml.cs` | Show `ContextMenu` from service-built menu model (**thin**) |
| `Services/PinPartPlacementCalculator.cs` | `ScoreAll` |
| **`Models/CompositePinShaftMenuItem.cs`** or record in `Models/` | DTO: pair id, label, score, isSelected |
| **`Services/CompositePinShaftMenuModelBuilder.cs`** *(new, preferred)* | `BuildMenuItems(target, candidates, config, currentPairId)` |
| **`Services/ManualLayoutOverrideStore.cs`** or `LayoutEditorController` | Pending `PairId` overrides until save |
| `Services/CompositePinApplicationService.cs` | Single-pin re-apply with override |
| `Models/ManualLayoutMarker.cs` | Phase 2 fields used on save |

> **Builder note:** Views must not reference Services — marker raises event; MainWindow asks `CompositePinShaftMenuModelBuilder` for items and applies selection via `CompositePinApplicationService`. No scoring loops in code-behind.

### Acceptance

- Right-click composite pin (not in edit mode) shows menu.
- Selection re-renders immediately.
- Save + reload preserves override.

---

## Phase 4 — Cache Composite Render Plans

### Goal

Cache built `CompositePinRenderPlan` lists on disk to avoid repeated `CompositePinRenderPlanBuilder` work. Follow `ClusterCache` patterns.

### Cache key design (corrected for endpoint sensitivity)

`LayoutKeyGenerator` output does **not** include per-marker endpoints. Include a **layout content fingerprint** in the cache key:

```
CompositePinPlanCache key = SHA256(
  "v{CacheVersion}",
  groupKey,                    // LayoutKeyGenerator output
  variantId,                   // ManualLayout.VariantId or "auto"
  layoutContentHash,           // SHA256 of sorted (locationName, angle, lineLength, pairId, headSourcePath)
                               // angle + lineLength are what ApplyManualLayout replays from, making this
                               // viewport-independent. Do NOT use screen-space orig/ext positions —
                               // those change with window resize and would give spurious cache misses.
                               // pairId / headSourcePath are empty string when assignments absent (Phase 2+).
  geometryHash,                // SHA256 of pin_part_geometry.json
  configHash                   // relevant PinPartConfig fields
)
```

Storage: `%AppData%/InteractiveWorldMap/composite_pin_plan_cache/{key}.json`

`CacheVersion` starts at `1`; bump to invalidate all entries.

### Cached payload

```csharp
public sealed record CachedCompositePlanEntry(
    string LocationId,
    CompositePinRenderPlan Plan);
```

**Serialization task:** add JSON converters for `System.Windows.Point` and `System.Windows.Media.Matrix` in layer plans (`CompositePinLayerPlan`).

### Cache service

`Services/CompositePinPlanCache.cs` — `TryLoad`, `Save`, `Invalidate`, `ClearAll` (mirror `ClusterCache`).

Wire instance in **`MainWindow.xaml.cs`** (or inject via constructor if refactored).

### Integration and invalidation

| Event | Action |
|-------|--------|
| Cache miss | Build all plans for visible cluster; save |
| `ManualLayoutManager.SaveLayout` | `Invalidate` keys for that `groupKey` + variant |
| Reassign Pins (Phase 1) | Invalidate or skip cache for session (MVP: bypass cache when `bypassEditModeCheck`) |
| Pending overrides (Phase 3) | Bypass cache until save |
| `visual-config.json` PinParts change | New `configHash` → miss |
| Geometry file change | New `geometryHash` → miss |

### Auto-layout caveat

Auto variant uses `variantId = "auto"` and `layoutContentHash` from computed extensions. `AdjustExtensions` is deterministic for fixed inputs today; if that changes, bump `CacheVersion`.

### Files affected

| File | Change |
|------|--------|
| `Services/CompositePinPlanCache.cs` | New |
| `Models/CachedCompositePlanEntry.cs` | New (or nested in cache service file) |
| **`Services/CompositePinLayoutContentHasher.cs`** *(new, optional)* | Stable `layoutContentHash` from marker fields (keep out of MainWindow) |
| `Services/CompositePinPlanningService.cs` and/or `CompositePinApplicationService.cs` | Cache check/write — **not** MainWindow |
| `Services/ManualLayoutManager.cs` | Invalidate on save |
| `Tests/CompositePinPlanCacheTests.cs` | miss→hit, invalidate, version bump |

> **Builder note:** If cache integration would add >20 lines to MainWindow, put orchestration in `CompositePinApplicationService.BuildOrLoadPlansForCluster(...)` instead.

### Acceptance

- Second render of identical cluster+layout+config logs cache hit (debug flag).
- Save layout → next render rebuilds (invalidate).
- Reassign / override without save → no stale cached visuals.

---

## Phase 5 — Multi-Variant Layouts (Delegated)

**This plan no longer defines Phase 5 implementation.** Use [manual-layout-variants-plan.md](manual-layout-variants-plan.md).

### What belongs there (not here)

- `IManualLayoutManager` variant CRUD (`ListVariants`, `SaveVariant`, `LoadVariant`, …)
- `SelectedVariants` dictionary in `manual-layouts.json` (not a separate `%AppData%` preference file)
- Edit-mode variant picker UI in `MainWindow.xaml`
- Seed generator integration (AutoSeed vs Manual coexistence)

### Composite-specific hook when variants land

When user selects a variant (edit mode per variants plan MVP):

1. `LoadVariant(groupKey, variantId)` → `ManualLayout`
2. `ApplyManualLayout(layout)` → composites via `TryApplyCompositePinMarker` with Phase 2 assignment fields
3. `SetSelectedVariantId` persists choice

**Coordination:** Complete Phase 1–2 before variant UI so switching variants shows correct composite assignments.

> **Builder note (Phase 5 / variants plan):** Variant picker UI in `MainWindow.xaml` is fine, but list/load/save-as logic must stay in `ManualLayoutManager` + `LayoutEditorController` per [manual-layout-variants-plan.md](manual-layout-variants-plan.md). Composite replay on variant switch = one call to `CompositePinApplicationService` / `ApplyManualLayout`, not duplicated in picker handlers.

---

## Cross-Cutting Concerns

### Layout key stability

Cache and variant selection both key off `LayoutKeyGenerator.GenerateKey()`. Small window resize changes key → cache miss and compatible-layout fallback (existing behaviour).

### Geometry recalibration

[composite-pin-head-placement-fix-plan.md](composite-pin-head-placement-fix-plan.md) may change visual appearance even when `PairId` is stable. `geometryHash` invalidates render-plan cache; stored assignments remain valid if geometry entries still exist.

### Test strategy

- **Gate:** `.\scripts\verify.ps1` after each phase.
- New unit tests: `CompositePinPlanCache`, `ScoreAll`, assignment round-trip on `ManualLayoutMarker`, `ExitEditMode` manual replay (mock or controller-level).
- Do not change existing tests to force green without reporting rationale.

---

## Phase Summary

| Phase | Scope | Key deliverable | Primary files (prefer **new Services**, thin MainWindow) |
|-------|-------|-----------------|---------------|
| 1 | Roundtrip fix + Reassign | Reassign button; replay on exit | **`CompositePinApplicationService.cs`**, minimal `MainWindow.xaml.cs` |
| 2 | Persist assignments | `PairId`, `HeadSourcePath` | **`ManualLayoutAssignmentEnricher.cs`**, `ManualLayoutMarker.cs` |
| 3 | Right-click override | Context menu + pending overrides | **`CompositePinShaftMenuModelBuilder.cs`**, `PinPartPlacementCalculator.cs` |
| 4 | Plan cache | `CompositePinPlanCache` + layout content hash | **`CompositePinPlanCache.cs`**, planning/application services |
| 5 | Variants | **See manual-layout-variants-plan.md** | (delegated; no composite logic in variant UI handlers) |
