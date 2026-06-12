---
status: active
owner: agent
started: 2026-06-07
requirements_ref: composite-pins-unzoomed
parent_program: composite-pins-program.md
parent_plan: pin-parts-composite-placement-plan.md
---

# Composite Pins — Unzoomed and All-Marker Rollout Plan

Extend `CompositePinMarker` to every individual location marker (zoomed and unzoomed), replacing legacy `ImagePinMarker` everywhere except cluster-aggregate markers.

Parent plan: [pin-parts-composite-placement-plan.md](../completed/pin-parts-composite-placement-plan.md)

TO_DO items: [Decide non-extended pin rendering policy](../../TO_DO.md), [Extend composite to all individual markers](../../TO_DO.md), [Run Phase 6 verification](../../TO_DO.md), [Manual edit mode for composite](../../TO_DO.md)

## Prerequisites (must complete first)

| Prerequisite | Plan reference |
|--------------|----------------|
| Extended-marker composite path stable | pin-parts Phase 5 ✅ |
| Phase 6 verification (debug overlay, angle spot-checks) | pin-parts Phase 6 ✅ |
| Non-extended rendering policy decided | This plan Phase 0 ✅ |

Do **not** start unzoomed rollout until Phase 6 acceptance criteria pass on extended markers in zoomed cluster view.

## Problem

Composite pins render only for **extended** image markers in the radial-extension pipeline (`TryApplyCompositePinMarker` gated by `PinParts.Enabled` + `PinParts.UseCompositeRendering`). Individual markers at full-map zoom and non-extended markers in cluster view still use legacy `ImagePinMarker`.

This split causes:

- Visual inconsistency between zoom levels
- Duplicate hit-testing and hover code paths
- Edit mode forced onto legacy markers even when composite is enabled globally

## Goal

When `PinParts.UseCompositeRendering` is true, every individual location marker uses `CompositePinMarker` at all zoom levels. Cluster aggregate markers (`ClusterMarker`) remain unchanged.

## Current status

- Phase 0 complete: Option A screen-up stub policy recorded.
- Phases 1–3 complete for non-edit rendering: visible individual image markers now use composite targets for extended and non-extended placements; cluster aggregate markers remain unchanged.
- Phase 4 complete (2026-06-11): edit mode now works on composite pins — removed `IsEditMode` gate, added extension lines as drag guides, rebuilt composite pins during drag, added composite-pin endpoint fallback, skipped `RestoreBaseMarkerVisuals` in edit mode.
- Phase 5 remains open: manual visual capture/tuning is still pending.
- Phase 6 in progress: **fully zoomed-out** manual layout edit — implementation and automated coverage landed 2026-06-12; manual smoke remains pending.

## Phase 0 — Policy decision ✅ (2026-06-09)

**Deliverable:** documented decision recorded in pin-parts plan Open Decisions §3.

### Decision: Option A — Stub segment

| Field | Choice |
|-------|--------|
| **Policy** | Option A — default short upward stub shaft when no radial extension exists |
| **Default length** | `DefaultStubLengthPixels = 24` (screen px); config in `PinPartConfig` / `visual-config.json` |
| **Stub direction** | Fixed **screen-up** (negative Y in WPF screen coordinates) |
| **Extension lines** | Do not draw radial extension lines for stub-only markers |

### Scope

| Marker context | Composite stub? |
|----------------|-----------------|
| Unzoomed **individual** location markers (visible as their own marker, not aggregated) | **Yes** — stub composite |
| Unzoomed **cluster aggregate** markers (`ClusterMarker` — dense groups collapsed at low zoom) | **No** — unchanged |
| Zoomed cluster — extended members (radial extension active) | **Yes** — existing composite path |
| Zoomed cluster — non-extended members in dense group | **Yes** — stub composite (Phase 3) |

**Rationale:** Unzoomed individual markers get a consistent composite look without forcing composite rendering onto cluster blobs. Dense groups at low zoom remain aggregate cluster markers until the user zooms in.

### Options considered

| Option | Description | Trade-off |
|--------|-------------|-----------|
| **A — Stub segment** ✅ | Non-extended pins use a default short upward shaft from original position | Consistent composite look; arbitrary stub direction |
| **B — Tip-only composite** | Render head + minimal shaft cap with zero-length logical segment | Less visual noise; may look like floating head |
| **C — Keep legacy at unzoomed only** | Extended = composite; unzoomed individual = legacy | Minimal work; inconsistent cross-zoom |

**Acceptance:** ✅ Decision recorded; `DefaultStubLengthPixels` added to config (unused by runtime until Phase 2).

## Phase 1 — Unified target segment builder ✅ (2026-06-10)

**Deliverables:** single helper that produces composite render targets for any marker.

### Files

| Action | Path |
|--------|------|
| Create | `Services/CompositePinTargetBuilder.cs` |
| Create | `Tests/CompositePinTargetBuilderTests.cs` |
| Modify | `MainWindow.xaml.cs` — use builder from extended and non-extended paths |

### Tasks

1. [x] Create `CompositePinTargetBuilder.Build(...)` returning `PinPlacementTarget`.
2. [x] When `RadialExtension` present: start = original screen, end = extended screen (current behavior).
3. [x] When no extension: start = location screen position, end = start + stub vector per Phase 0 policy.
4. [x] Unit tests: extended case matches current pipeline; stub case produces expected length and direction.
5. [x] Refactor composite apply path to accept built targets regardless of extension state.

**Acceptance:**

- Builder tests pass
- No behavior change for existing extended composite pins

## Phase 2 — Unzoomed individual marker integration ✅ (2026-06-10)

**Scope:** Only markers rendered as **individual** location pins at unzoomed zoom (`ZoomLevel` below cluster threshold). **Exclude** `ClusterMarker` aggregates — dense groups collapsed at low zoom stay unchanged.

**Deliverables:** full-map view uses composite pins for all individual markers.

### Files

| Action | Path |
|--------|------|
| Modify | `MainWindow.xaml.cs` — `CreateImagePinMarker`, `PositionMarkerAtNormalLocation`, marker rebuild paths |
| Modify | `Views/CompositePinMarker.xaml.cs` — verify scale at `ZoomLevel ≈ 1.0` |

### Tasks

1. [x] Post-create/update hook applies `CompositePinMarker` when composite rendering is enabled and a visible individual marker is not extended.
2. [x] Position unzoomed markers via `CompositePinTargetBuilder` stub targets, not centered bitmap placement.
3. [x] Ensure marker wrapper bounds follow composite bounds after render-plan application.
4. [ ] Verify hover feedback and click → subwindow behavior unchanged in manual smoke.
5. [ ] Verify locations visible as individual markers at both zoom levels do not jump visually on zoom in/out.

**Acceptance:**

- Unzoomed map shows composite pins for all individual image locations
- No regression in subwindow open on click
- Manual smoke at 1920×1080 and one high-DPI display if available

## Phase 3 — Non-extended markers in **zoomed** cluster view ✅ (2026-06-10)

**Scope:** Applies only after zooming into a dense group (above `RadialExtension.ZoomThresholdForExtensions`). Not unzoomed cluster aggregates.

**Deliverables:** dense-group members without radial extension also use composite rendering.

### Tasks

1. [x] After placement, markers skipped by extension use stub composite targets.
2. [x] Extension line renderer does not draw lines for stub-only markers.
3. [x] Existing extended-marker `ExtensionLineRenderer.Apply` call stays focused on true extension groups; normal placement hook handles non-extended image markers.

**Acceptance:**

- Zoomed cluster with mix of extended and non-extended image pins — all composite when flag enabled

## Phase 4 — Edit mode on composite markers

**Deliverables:** edit mode drags composite pins (both extended and stub) at all zoom levels, not legacy `ImagePinMarker` fallback.

Related TO_DO: [Make manual edit mode available for composite layouts](../../TO_DO.md)

### Files

| Action | Path |
|--------|------|
| Modify | `MainWindow.xaml.cs` — `CanUseCompositePins`, `OnEditLayoutButtonClick`, `ExitEditMode` |
| Modify | `MainWindow.LayoutEditor.partial.cs` — `OnMarkerDragMove`, `CollectCurrentExtensions`, `ApplyManualLayout` |
| Modify | `MainWindow.CompositePins.partial.cs` — `ApplyCompositePinsToNormalPlacements` (if needed) |
| Create | `Tests/CompositePinEditModeTests.cs` |
| Modify | `docs/TO_DO.md` |

### Technical constraints discovered during code review

1. **Positioning mismatch** — Legacy markers are **center-anchored** (`Canvas.Left = extendedPos.X - markerSize/2`). Composite pins are **tip-anchored** (`Canvas.Left = originalPos.X - plan.TipAnchorLocal.X`). Dragging the existing `LocationMarker` wrapper moves the tip, not the head. We must rebuild the composite pin each drag frame so the head follows the mouse.
2. **Extension lines are absent for composite pins** — `ExtensionLineRenderer.Apply` skips drawing lines when `tryCompositePinApplier` succeeds. This breaks `CollectCurrentExtensions` (uses `TryGetLineEndpoint` as primary source) and leaves no visual drag target. We must add lines for composite pins in edit mode.
3. **`RestoreBaseMarkerVisuals()` destroys composite pins** — `OnEditLayoutButtonClick` calls it before `ApplyManualLayout`, reverting to `ImagePinMarker`. We must skip this when composite rendering is active.
4. **Drag events live on `LocationMarker`, not `CompositePinMarker`** — `CompositePinMarker` is just `Content`. The plan's "drag hit targets" note is a red herring.

### Tasks

1. **Remove the edit-mode gate from `CanUseCompositePins()`**
   - Delete `&& !_layoutEditor.IsEditMode` from the `CanUseCompositePins()` return expression.
   - This lets composite pins render everywhere, including edit mode.

2. **Keep extension lines as visual guides in edit mode**
   - In `ApplyManualLayout`, when `instruction.CachedPlan != null` (composite pin applied) and `_layoutEditor.IsEditMode`, still add an extension line via `_extensionLineRenderer.AddLine(marker, instruction.OriginalScreen, instruction.ExtendedScreen)` so `CollectCurrentExtensions` has a reliable endpoint and the user sees a drag target.
   - Same for `TryApplyCompositePinMarker` in the normal build path — if `IsEditMode`, add a line after applying the composite pin.

3. **Fix `OnMarkerDragMove` for composite pins**
   - Detect composite marker: `marker.Content is CompositePinMarker`.
   - For composite markers:
     - `originalPos` = fixed from `viewport.SourceToScreen(location.PixelX, location.PixelY)`.
     - `mousePos` = current cursor position.
     - Rebuild `PinPlacementTarget` with `StartScreen = originalPos`, `EndScreen = mousePos`.
     - Call `ApplyCompositePinToMarker(marker, originalPos, mousePos)` to re-render the pin at the new angle/length.
     - Move the line endpoint via `_extensionLineRenderer.MoveLineEndpoint(marker, mousePos)`.
   - For legacy markers, keep existing drag behavior.
   - Ensure `newX/newY` bounds logic still works (composite pin `Canvas.Left` is tip-based, not center-based).

4. **Fix `CollectCurrentExtensions` for composite pins**
   - `TryGetLineEndpoint` will work once Task 2 is done (lines always present in edit mode).
   - Add a composite fallback: if no line and `marker.Content is CompositePinMarker cmp`, use `cmp.RenderPlan.EndScreen` (or compute from `Canvas.Left/Top + plan.TipAnchorLocal + (JoinAnchorLocal - TipAnchorLocal)`).

5. **Skip `RestoreBaseMarkerVisuals()` in edit mode when composite is active**
   - In `OnEditLayoutButtonClick`, wrap `RestoreBaseMarkerVisuals()` in a guard: `if (!CanUseCompositePins()) RestoreBaseMarkerVisuals();`.
   - When composite is active, `ApplyManualLayout` (which rebuilds composite pins) is the only visual path needed.

6. **Save/load round-trip verification**
   - `CollectCurrentExtensions` must produce correct `RadialExtension` with `ExtendedPosition` = endpoint.
   - `LayoutEditorController.BuildExtensions` must produce correct angles/lengths.
   - `ApplyManualLayout` replay must restore composite pins with the same endpoint.
   - Add unit test: `LayoutEditorController.BuildExtensions` with composite-pin endpoint.

7. **Exit edit mode without legacy flash**
   - `ExitEditMode` already replays `ApplyManualLayout` when `IsManualLayoutActive` is true — this is correct.
   - Ensure the `UpdateMarkerPositions()` fallback path also applies composite pins correctly (it already does via `ApplyCompositePinsToNormalPlacements`).

8. **Unzoomed edit mode**
   - Verify stub composite pins are draggable in full-map view.
   - `CollectCurrentExtensions` must capture the stub endpoint.
   - Save/load must round-trip stub positions.

9. **Tests**
   - `Tests/CompositePinEditModeTests.cs`:
     - `CanUseCompositePins_ReturnsTrue_InEditMode`
     - `ApplyManualLayout_AddsExtensionLines_WhenEditModeAndComposite`
     - `CollectCurrentExtensions_FallsBackToCompositePlan_WhenNoLine`
     - `BuildExtensions_CorrectAngleForCompositePin`

10. **Manual smoke checklist**
    - Zoom into cluster → enter edit mode → drag composite pin → save → exit edit mode → confirm composite pin at saved position.
    - Zoom out → zoom back in → confirm layout still loads.
    - Full-map view → edit mode → drag stub composite pin → save → exit → confirm.

**Acceptance:**

- Edit mode works on composite pins in zoomed cluster view and unzoomed full-map view
- Saved layouts replay correctly with composite rendering enabled
- `scripts\verify.ps1` passes
- No regression in legacy marker edit mode when `UseCompositeRendering = false`

## Phase 5 — Verification and tuning (extends pin-parts Phase 6)

**Deliverables:** visual and automated verification at both zoom levels.

### Tasks

1. Run debug overlay (`Debug.ShowCompositePinDebugOverlay: true`) at:
   - Unzoomed full map (stub targets)
   - Zoomed cluster — extended angles 0°, 45°, 90°, 135°, 180°
2. Capture before/after screenshots; store in `docs/screenshots/composite-pins-unzoomed/` (gitignored or committed per repo convention).
3. Run `.\scripts\verify.ps1` on .NET 6 SDK machine.
4. Update pin-parts plan Phase 6 checklist items as complete.
5. Update [VISUAL_CONFIG.md](../../guides/VISUAL_CONFIG.md) — document stub length and unzoomed behavior.

**Acceptance:**

- No visible shaft/head gap at representative angles
- Hit targets feel correct at unzoomed and zoomed scales
- Full verify green

## Phase 6 — Manual layout edit on fully zoomed-out map

**Scope:** User is at **full-map zoom** (fully zoomed out, `ZoomLevel ≈ 1.0`, `_currentZoomedCluster == null`). Editable targets are **only visible single-location individual markers** (stub composite pins). Not multi-location `ClusterMarker` blobs; not markers hidden because they belong to a dense cluster.

**Problem:** Phase 4 enabled composite pin dragging when edit mode is active, but **Edit Layout** is only offered after zooming into a cluster (`ShowZoomedView`). Save/delete require `_currentZoomedCluster != null`. Zoom-out exits edit mode and hides the button.

**Deliverables:** Edit, save, load, and variant-manage stub pin placement on the full map — same variant UX as zoomed-cluster edit, keyed by window size.

### Design decisions (2026-06-11)

| # | Decision |
|---|----------|
| 1 | **Edit scope:** Only markers visible as **single-location individuals** at full map (`ShowOnlyClusterMarkers` visibility rules). Do not surface hidden cluster members. |
| 2 | **Layout group key:** `fullmap_s{W}x{H}` — **canvas/window size only** (rounded `MapDisplay.ActualWidth` × `ActualHeight`). See [Layout key](#layout-key-recommendation) below. |
| 3 | **Save/delete:** Remove `_currentZoomedCluster == null` as a hard blocker when in full-map edit; use explicit full-map session flag + group key above. |
| 4 | **Load/replay:** After auto-placement on full map, overlay saved variant if one exists — see [Load/replay](#loadreplay-plain-language) below. |
| 5 | **Head/shaft on save:** Reuse existing `_assignmentEnricher` path (same as zoomed save); head-picker UI remains separate TO_DO. |
| 6 | **Variants:** Reuse `manual-layouts.json` variant model — **multiple Manual variants per** `fullmap_sWxH` group (picker, Save As, delete variant). No AutoSeed for full-map MVP. |
| 7 | **No zoom while editing:** Block zoom-in (cluster click, single-pin click zoom, zoom gestures), zoom-out (Back), and viewport navigation for **any** edit mode (full-map or cluster) until user exits edit mode. Show brief status if blocked. |
| 8 | **Full-map session state:** Track an explicit full-map edit/replay session state; do not infer full-map editing solely from `_currentZoomedCluster == null`. |

#### Layout key recommendation

**Group key (storage + variants):**

```text
fullmap_s1920x1080
```

- Format: `fullmap_s{W:F0}x{H:F0}` from `MapDisplay.ActualWidth` / `ActualHeight` at enter-edit and at load.
- `LayoutEditorController.CurrentLayoutKey` = this group key for full-map sessions (same as zoomed-cluster pattern).
- Full-map key compatibility is exact for MVP. Update `LayoutKeyGenerator.AreKeysCompatible` / `ManualLayoutManager` compatible-layout lookup so `fullmap_s1920x1080` never falls back to `fullmap_s1440x900`.
- **Not** in the key: location hash, viewport center, radial-extension params, stub length. Rationale: one layout family per window size; A/B layouts are variants; clustering changes which singles are visible without invalidating the group.

**Per-marker save set:** On Save, persist one `ManualLayoutMarker` per **currently visible** single-location individual (location name + stub endpoint screen position + angle/length + assignment fields). Markers in dense clusters are omitted (not editable at this zoom).

**Load matching:** On replay, apply saved entries **by location name** to visible singles only. Extra saved rows for locations not currently visible → ignore. Visible singles missing from variant → keep auto stub placement for those.

**Window resize:** New `fullmap_sWxH` → different group (expected). Optional future: `AreKeysCompatible` tolerance for ±N px; out of scope for MVP. Until then, full-map key compatibility must be exact.

**Implementation:** Add `LayoutKeyGenerator.GenerateFullMapGroupKey(double canvasWidth, double canvasHeight)`; do not overload cluster `GenerateKey` with sentinel hacks.

#### Load/replay (plain language)

Today (zoomed cluster only):

1. User zooms into cluster → app builds a layout key → looks for saved JSON → if found, applies saved pin positions **instead of** auto radial math.

Full map needs the same idea in one place:

```text
Show full map → place pins automatically (stubs) → IF saved layout exists for fullmap_sWxH → apply saved positions on top
```

**When replay runs (MVP):**

| Trigger | Action |
|---------|--------|
| App startup | After `AddClustersToMap` + `UpdateMarkerPositions`, call `TryApplyFullMapManualLayout()` |
| Return from cluster zoom (Back / zoom-out) | End of `ShowClusterView()` after `UpdateMarkerPositions` |
| Exit edit mode (saved layout active) | Existing `ExitEditMode` → `ApplyManualLayout` path (already works once key is set) |

**Not replay:** While user is actively in edit mode before Save (they are dragging live positions).

**Helper:** `TryApplyFullMapManualLayout()` — compute `fullmap_sWxH`, `SetLayoutKey`, `TryLoad` selected variant, `ApplyManualLayout` if Manual layout exists; else no-op.

**Full-map session lifecycle:** Set full-map edit session state when entering edit mode from full-map zoom; keep the captured `fullmap_sWxH` key through save, Save As, delete, and exit; clear the state when returning to normal full-map display or entering a zoomed-cluster layout session. Startup/replay may compute the same full-map key without entering edit mode.

**Delete/recalculate:** Full-map delete must not call `ShowZoomedView(_currentZoomedCluster)`. After deleting the active full-map variant/layout, exit edit mode, rebuild auto stub placements with `UpdateMarkerPositions()` / `ShowClusterView()`, and let `TryApplyFullMapManualLayout()` no-op if no Manual variant remains.

### Files (expected)

| Action | Path |
|--------|------|
| Modify | `MainWindow.Navigation.partial.cs` — show Edit Layout at full map; `TryApplyFullMapManualLayout` from `ShowClusterView`; block zoom/navigation when `IsEditMode` |
| Modify | `MainWindow.LayoutEditor.partial.cs` — enter/exit/save/delete full-map session; remove cluster-only null guards when full-map |
| Modify | `MainWindow.xaml.cs` — block single-pin click zoom + cluster click when `IsEditMode`; startup replay hook after initial placement |
| Add / modify | `Services/LayoutKeyGenerator.cs` — `GenerateFullMapGroupKey` |
| Modify | `Services/ManualLayoutManager.cs` — prevent compatible-layout fallback across different `fullmap_sWxH` keys |
| Add / modify | `Tests/LayoutKeyGeneratorTests.cs` — full-map key format, stability, and exact compatibility |
| Add / modify | `Tests/ManualLayoutManagerTests.cs` — no compatible fallback across different full-map sizes |
| Modify | `docs/guides/MANUAL_LAYOUT_EDITOR.md` — full-map edit flow, key rules, no-zoom-while-editing |

### Tasks

1. [x] **UI:** Show **Edit Layout** when `ManualLayoutEditor.Enabled`, `_currentZoomedCluster == null`, and viewport at full-map zoom (~1.0).
2. [x] **Session:** On enter full-map edit — set explicit full-map session state, capture `GenerateFullMapGroupKey(...)`, call `SetLayoutKey(...)`, populate variant picker; composite stub pins draggable (Phase 4 path + extension lines as guides).
3. [x] **Save/delete:** `OnSaveLayoutButtonClick`, `OnSaveAsVariantButtonClick`, `OnDeleteVariantButtonClick`, and `OnDeleteLayoutButtonClick` work when `_currentZoomedCluster == null` and full-map edit session is active; save only visible single-location markers.
4. [x] **Collect:** Update `CollectCurrentExtensions()` and any shared save helpers so full-map edit is allowed by explicit full-map session state; keep cluster-only guards for zoomed-cluster sessions only.
5. [x] **Delete/rebuild:** Full-map delete exits edit mode and rebuilds auto full-map stub placements without calling `ShowZoomedView(_currentZoomedCluster)`.
6. [x] **Replay:** Implement `TryApplyFullMapManualLayout()`; call from startup (post–`UpdateMarkerPositions`) and `ShowClusterView` completion.
7. [x] **Key compatibility:** Add `GenerateFullMapGroupKey` and make full-map compatibility exact; `fullmap_s1920x1080` must not load or list compatible variants from `fullmap_s1440x900`.
8. [x] **Assignments:** Include shaft/head fields on full-map save via `_assignmentEnricher` (no new picker UI in this phase).
9. [x] **No zoom while editing:** Guard `AnimateZoomToCluster`, zoom-out/Back, single-marker click-zoom, and any map zoom gestures when `_layoutEditor.IsEditMode`; optional status text (“Exit edit mode to zoom”).
10. [x] **Tests:** `GenerateFullMapGroupKey` unit tests; `AreKeysCompatible_FullMapDifferentSizes_ReturnsFalse`; manual-layout load/list tests that prove different full-map sizes do not fall back to each other; extend layout editor tests if save path is testable without UI.
11. [ ] **Manual smoke:** Fully zoomed out → Edit Layout → drag stubs → Save As variant → exit → Back from cluster if needed → confirm positions; resize window → confirm different group key; confirm zoom blocked while editing.

**Acceptance:**

- Edit Layout available at fully zoomed-out map for visible single-location stub pins
- Save/load/delete and **multiple variants** per `fullmap_sWxH` group
- `fullmap_sWxH` groups are exact-match only; no accidental compatible fallback between different canvas sizes
- Saved layouts restore on startup and when returning to full map without entering a cluster zoom
- Zoom/navigation blocked during edit mode (full-map and cluster)
- Zoomed-cluster manual layout behavior unchanged
- `scripts/verify.ps1` passes

### Risks

| Risk | Mitigation |
|------|------------|
| Clustering config change shifts which singles are visible | Load matches by name; missing names keep auto stubs; document in MANUAL_LAYOUT_EDITOR |
| Window resize mid-edit | Use key captured at enter-edit; warn on resize or exit edit if canvas size changes |
| Performance — many singles at full map | Reuse render-plan cache; same as current composite path |
| User expects to edit cluster members at full map | Out of scope — must zoom into cluster; UI copy / docs |

## Definition of Done

- `PinParts.UseCompositeRendering = true` → all individual image markers use `CompositePinMarker` at every zoom level
- Cluster markers unchanged
- Edit mode save/load verified on composite pins
- Phase 6 / Phase 5 verification complete
- `scripts/verify.ps1` passes
