---
status: active
owner: agent
started: 2026-06-07
last_updated: 2026-06-20
requirements_ref: composite-pins-unzoomed
parent_program: composite-pins-program.md
parent_plan: pin-parts-composite-placement-plan.md
related: remove-pins-jpg-legacy-path-plan.md
---

# Composite Pins — Unzoomed and All-Marker Rollout Plan

Extend `CompositePinMarker` to every individual location marker (zoomed and unzoomed). Legacy `ImagePinMarker` / `pins.jpg` was removed by [remove-pins-jpg-legacy-path-plan.md](remove-pins-jpg-legacy-path-plan.md); cluster-aggregate markers remain `ClusterMarker` blobs.

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

Historical problem: composite pins originally rendered only for **extended** image markers in the radial-extension pipeline. `PinParts.Enabled` + `PinParts.UseCompositeRendering` now select the composite path, while drawn `PinMarker` remains the fallback.

This split causes:

- Visual inconsistency between zoom levels
- Duplicate hit-testing and hover code paths
- Edit mode forced onto legacy markers even when composite is enabled globally

## Goal

When `PinParts.UseCompositeRendering` is true, every individual location marker uses `CompositePinMarker` at all zoom levels. Cluster aggregate markers (`ClusterMarker`) remain unchanged.

## Current status

- Phase 0 complete: Option A screen-up stub policy recorded.
- Phases 1–3 complete for non-edit rendering: visible individual markers now use composite targets for extended and non-extended placements; cluster aggregate markers remain unchanged.
- Phase 4 complete (2026-06-11): edit mode now works on composite pins — removed `IsEditMode` gate, added extension lines as drag guides, rebuilt composite pins during drag, added composite-pin endpoint fallback, skipped `RestoreBaseMarkerVisuals` in edit mode.
- Phase 5 complete: user confirmed debug-overlay geometry and shaft/head gap checks on 2026-06-12; screenshot capture skipped by request because no screenshot artifact path was available in this session.
- Phase 6 complete for core/manual smoke: implementation, automated coverage, and basic full-map edit smoke accepted 2026-06-12.
- Phase 7 **in progress** (2026-06-20): reposition-only + single-location full-map layout precedence done; automated guards green (315 tests). Manual smoke **#1–7 passed**; remaining: **#8** (composite-off regression), move plan to `completed/`.

### Phase 7 policy: zoom persistence and visual invariance (decisions 2026-06-19)

**Who this applies to**

| Marker context | Visual invariance across zoom? | Editable? |
|----------------|-------------------------------|-----------|
| Visible **single-location individual** (not collapsed into a multi-location `ClusterMarker` blob) | **Yes** — same composite stub at full map and when zoomed to that location | **Yes** — full-map edit (Phase 6) when unzoomed; cluster/single edit (Phase 4) when zoomed |
| Multi-location **dense cluster** members after radial extension layout | **No** — extended members use real extension segments; non-extended members use stub composite inside the cluster | Cluster edit only (must zoom into cluster) |
| Multi-location **`ClusterMarker` aggregate blobs** at low zoom | N/A — unchanged blobs, not composite individuals | No |

**Invariance means (non-dense-cluster singles only)**

- Same renderer: `CompositePinMarker` whenever `UsePinMarkers=true`, `PinParts.Enabled=true`, and `PinParts.UseCompositeRendering=true`.
- Same stub geometry in screen space: `DefaultStubLengthPixels` and screen-up direction do not scale with zoom.
- Same assignment: shaft/head pair stable for the location (no visible re-roll while panning/zooming).
- Same anchor: tip on the location's viewport-projected screen coordinate; only `Canvas.Left/Top` changes with viewport.
- No drawn-pin flash in composite mode: never restore to captured `PinMarker` fallback during normal updates.

**Animation bar (for now)**

- **Settled state:** `_mode != InteractionMode.Animating` (`IsAnimating == false`) and the most recent `UpdateMarkerPositions()` call has returned.
- After zoom/pan/resize reaches settled state, markers must show composite (or intentional drawn fallback on asset failure only).
- **During** viewport animation: `ApplyCompositePinsToNormalPlacements` currently **early-returns** when `IsAnimating`; composites are repositioned via `ApplyIndividualPlacements` + `TryGetCompositeAnchoredPlacement` only. Full `BuildPlan` rebuild is deferred until settled state. Frame-perfect per-frame composite rebuild during animation is **out of scope** for this phase.

**Smoothness requirement (settled + non-animation updates)**

- When the logical target segment is **visually unchanged**, non-animation updates must **reposition only** — update `Canvas.Left/Top` from `CompositePinPlacementPolicy` without recreating `CompositePinMarker` or re-running `BuildPlan`. Required for pan, resize, and consecutive `UpdateMarkerPositions()` calls when the viewport moves the tip but the stub/extension vector, length, assignment, and rendered appearance do not change.

## Phase 0 — Policy decision ✅ (2026-06-09)

**Deliverable:** documented decision recorded in pin-parts plan Open Decisions §3.

### Decision: Option A — Stub segment

| Field | Choice |
|-------|--------|
| **Policy** | Option A — default short upward stub shaft when no radial extension exists |
| **Default length** | `DefaultStubLengthPixels = 24` (screen px); config in `PinPartConfig` / `visual-config.json` |
| **Stub direction** | Fixed **screen-up** (negative Y in WPF screen coordinates) |
| **Extension lines** | Do not draw radial extension lines for stub-only markers in normal render (**edit mode exception:** Phase 4 adds invisible drag-guide lines only — see Phase 4 task 2) |

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
| Modify | `MainWindow.xaml.cs` — `CreatePinMarker`, `PositionMarkerAtNormalLocation`, marker rebuild paths |
| Modify | `Views/CompositePinMarker.xaml.cs` — verify scale at `ZoomLevel ≈ 1.0` |

### Tasks

1. [x] Post-create/update hook applies `CompositePinMarker` when composite rendering is enabled and a visible individual marker is not extended.
2. [x] Position unzoomed markers via `CompositePinTargetBuilder` stub targets, not centered bitmap placement.
3. [x] Ensure marker wrapper bounds follow composite bounds after render-plan application.
4. [x] Verify hover feedback and click → subwindow behavior unchanged in manual smoke.
5. [x] Verify locations visible as individual markers at both zoom levels do not jump visually on zoom in/out.

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
3. [x] Existing extended-marker `ExtensionLineRenderer.Apply` call stays focused on true extension groups; normal placement hook handles non-extended markers.

**Acceptance:**

- Zoomed cluster with mix of extended and non-extended pins — all composite when flag enabled

## Phase 4 — Edit mode on composite markers

**Deliverables:** edit mode drags composite pins (both extended and stub) at all zoom levels, with drawn `PinMarker` fallback only when composite rendering fails or is disabled.

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
2. **Extension lines are absent for composite pins** — `ExtensionLineRenderer.Apply` skips drawing its own lines when the composite-pin callback (`TryApplyCompositePinMarker` at `MainWindow.xaml.cs`) succeeds. Edit mode adds lines manually in `ApplyCompositePinsToNormalPlacements` / `TryApplyCompositePinMarker` when `_layoutEditor.IsEditMode` (see `MainWindow.CompositePins.partial.cs`). Without those edit-mode lines, `CollectCurrentExtensions` (which uses `TryGetLineEndpoint` as primary source) has no endpoint and the user has no drag target.
3. **`RestoreBaseMarkerVisuals()` can destroy composite pins** — `OnEditLayoutButtonClick` calls it before `ApplyManualLayout`, reverting to the captured drawn fallback. We must skip this when composite rendering is active.
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
     - `mousePos` = current cursor position, **clamped to** `MapDisplay.Markers` inner canvas (`0` … `ActualWidth` × `ActualHeight`) — same for composite and legacy markers.
     - Rebuild `PinPlacementTarget` with `StartScreen = originalPos`, `EndScreen = mousePos`.
     - Call `ApplyCompositePinToMarker(marker, originalPos, mousePos)` to re-render the pin at the new angle/length.
     - Move the line endpoint via `_extensionLineRenderer.MoveLineEndpoint(marker, mousePos)`.
   - For legacy markers, keep existing drag behavior.
   - Composite pin `Canvas.Left` is tip-based, not center-based; wrapper drag moves the tip anchor.
   - **Fragility (Phase 7):** drag currently routes through `ApplyCompositePinToMarker`, which builds a fake empty `ViewportState` with zero container size. Safe today only because explicit start/end screen points bypass viewport math. Prefer refactoring drag to call `ApplyCompositePinTargetToMarker` with a hand-built `PinPlacementTarget` (Phase 7 task 11).

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
- Drag opacity: `OnMarkerDragStart` → `0.7`; `OnMarkerDragEnd` → `1.0` (regression — Phase 7 task 16)
- Saved layouts replay correctly with composite rendering enabled
- `scripts\verify.ps1` passes
- No regression in legacy marker edit mode when `UseCompositeRendering = false`

## Phase 5 — Verification and tuning (extends pin-parts Phase 6)

**Deliverables:** visual and automated verification at both zoom levels.

### Tasks

1. [x] Run debug overlay (`Debug.ShowCompositePinDebugOverlay: true`) at:
   - Unzoomed full map (stub targets)
   - Zoomed cluster — extended angles 0°, 45°, 90°, 135°, 180°
   - User confirmed tip/start, shaft direction, head center, endpoint behavior, and shaft/head gaps match intended placement on 2026-06-12.
2. [x] Capture before/after screenshots; store in `docs/screenshots/composite-pins-unzoomed/` (gitignored or committed per repo convention).
   - Skipped 2026-06-12 by request; no screenshot artifact path was available in this session, and screenshots are not required for functional acceptance.
3. [x] Run `.\scripts\verify.ps1` on .NET 6 SDK machine.
4. [x] Update pin-parts plan Phase 6 checklist items as complete.
   - Parent plan is already completed with Phase 6 checked; no further parent-plan edit required.
5. [x] Update [VISUAL_CONFIG.md](../../guides/VISUAL_CONFIG.md) — document stub length and unzoomed behavior.

**Acceptance:**

- No visible shaft/head gap at representative angles — accepted by user on 2026-06-12.
- Hit targets feel correct at unzoomed and zoomed scales — accepted by user on 2026-06-12.
- Full verify green.

## Phase 6 — Manual layout edit on fully zoomed-out map

**Scope:** User is at **full-map zoom** (fully zoomed out, `ZoomLevel ≈ 1.0`, `_currentZoomedCluster == null`). Editable targets are **only visible single-location individual markers** (stub composite pins). Not multi-location `ClusterMarker` blobs; not markers hidden because they belong to a dense cluster.

**Problem:** Phase 4 enabled composite pin dragging when edit mode is active, but **Edit Layout** is only offered after zooming into a cluster (`ShowZoomedView`). Save/delete require `_currentZoomedCluster != null`. Zoom-out exits edit mode and hides the button.

**Deliverables:** Edit, save, load, and variant-manage stub pin placement on the full map — same variant UX as zoomed-cluster edit, keyed by window size.

### Design decisions (2026-06-11)

| # | Decision |
|---|----------|
| 1 | **Edit scope:** Only markers visible as **single-location individuals** at full map (`ShowOnlyClusterMarkers` visibility rules). Do not surface hidden cluster members. |
| 2 | **Layout group key:** `fullmap_s{W}x{H}` — **canvas/window size only** (rounded `MapDisplay.ActualWidth` × `ActualHeight`). See [Layout key](#layout-key-recommendation) below. |
| 3 | **Save/delete/load:** These flows must use `IsFullMapLayoutSessionActive()` (not `_currentZoomedCluster != null`) to decide whether a full-map edit session is active. The **enter-edit** branch on `_currentZoomedCluster == null` (to choose full-map vs cluster session) stays — remove the cluster-null requirement only from save/delete/load paths, not from enter-edit. |
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
11. [x] **Manual smoke:** Fully zoomed out → Edit Layout → drag stubs → Save As variant → exit → Back from cluster if needed → confirm positions; resize window → confirm different group key; confirm zoom blocked while editing.

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

## Phase 7 — Persist composite pins through zoom/pan/update cycles

_Phase 7 expanded 2026-06-19 (policy + checklist); refined 2026-06-20 per `temp/review-composite-pins-unzoomed-plan-2026-06-20.md`._

**Scope:** Non-dense-cluster visible individuals (single-location markers) must stay visually composite and editable at all zoom levels. Dense-cluster members use extension or stub composite per radial layout. `ClusterMarker` blobs unchanged.

**Related plans:** Overlapping acceptance in [remove-pins-jpg-legacy-path-plan.md](remove-pins-jpg-legacy-path-plan.md) Phase 3/6 is **partially delegated** — see [Cross-plan closure](#cross-plan-closure) below. **Source of truth for pan/zoom flicker:** this plan's Phase 7 acceptance; flip legacy checkboxes only when Phase 7 manual smoke passes.

### Problem (original)

`UpdateMarkerPositions()` called `RestoreBaseMarkerVisuals()` on every update, restoring drawn `PinMarker` fallback before composite reapply — causing drawn-pin flash even when the final frame was composite.

### Problem (remaining)

Even after the no-restore fix, `ApplyCompositePinsToNormalPlacements` still **rebuilds** composite content on every update (`new CompositePinMarker` + `BuildPlan`). That wastes work and can flicker during pan/zoom/animation. We need **reposition-only** when the target segment is visually unchanged.

### Target behavior

| Context | Expected |
|---------|----------|
| Full-map visible single (non-dense) | Composite stub; same appearance when zoomed to that single location |
| Zoom into **single-location** cluster | Settled state: identical stub composite to full-map view |
| Zoom into **multi-location** dense cluster | Extended members: composite on real extension; non-extended: stub composite |
| Zoom out / Back | Non-dense singles return to same stub composite appearance |
| Pan / resize / `UpdateMarkerPositions` while segment unchanged | Reposition only (`Canvas.Left/Top`); keep existing `CompositePinMarker` + render plan |
| Segment changed (manual layout, drag, extension apply) | Full reapply allowed |
| Viewport animation (`InteractionMode.Animating`) | `ApplyCompositePinsToNormalPlacements` skips full apply; `ApplyIndividualPlacements` repositions existing composites via tip anchor until settled |
| Composite asset/planning failure | Per-marker drawn fallback only; log warning |
| Edit mode | Non-dense singles editable at full map (Phase 6) and when zoomed (Phase 4); no restore-to-drawn on enter/switch when composite active |

### Implementation approach

**A. Fallback cache, not reset (done)**

1. Treat `_baseMarkerVisuals` as fallback cache only.
2. `PrepareMarkerVisualsForPlacementUpdate()` skips restore when `CanUseCompositePins()`.
3. Per-marker `RestoreDrawnFallbackForCompositeFailure` on composite failure only.
4. Non-composite modes unchanged.

**B. Reposition-only optimization (required — not done)**

Extend **`Services/CompositePinPlacementPolicy.cs`** (do **not** create a separate `CompositePinSegmentPolicy.cs` — segment comparison belongs with placement policy).

1. Add behavior-tested helper, e.g.:
   ```csharp
   bool ShouldRepositionOnly(
       CompositePinRenderPlan? existingPlan,
       PinPlacementTarget newTarget,
       string? preferredPairId = null,
       string? preferredHeadSourcePath = null,
       double tolerancePx = 0.5)
   ```
   - Keep this helper pure and architecture-safe: `Services/` must not reference `Views/CompositePinMarker`; WPF callers extract `((CompositePinMarker)marker.Content).RenderPlan` before calling the policy.
   - Do **not** require `CompositePinRenderPlan` to store screen endpoints unless implementation proves it is necessary. Prefer comparing `existingPlan.TargetAngleDeg`, `existingPlan.TargetLengthPx`, `existingPlan.PairId`, `existingPlan.HeadSourcePath`, and the new target vector.
   - Stub: unchanged when the new target vector remains screen-up and length matches the existing plan / `DefaultStubLengthPixels` within `tolerancePx`; absolute `StartScreen` / `EndScreen` may move during pan/resize.
   - Extension: unchanged when angle and length match within tolerance, or when explicitly recorded extension endpoints match within tolerance for saved/manual layouts.
   - Never reposition-only when `preferredPairId` or `preferredHeadSourcePath` requests an assignment that differs from the existing render plan.
2. Add `GetCompositeTopLeft(Point tipScreen, CompositePinRenderPlan plan)` as the **primary** tip-anchor helper:
   ```csharp
   Point GetCompositeTopLeft(Point tipScreen, CompositePinRenderPlan plan)
   ```
   - Refactor the existing `GetCompositeTopLeft(MarkerScreenPlacement, double locationMarkerSize, CompositePinRenderPlan)` to **delegate** to it after computing `tipScreen = placement.Left + locationMarkerSize/2, placement.Top + locationMarkerSize/2`.
   - Normal, extension, and drag paths should use the same tip-anchor math.
3. In `ApplyCompositePinsToNormalPlacements` (when **not** `IsAnimating`):
   - Build target via `CompositePinTargetBuilder`.
   - Extract `existingPlan` from `marker.Content as CompositePinMarker`.
   - If `ShouldRepositionOnly` → set top-left via `GetCompositeTopLeft(target.StartScreen, existingPlan)` only; skip `ApplyCompositePinTargetToMarker`.
   - Else → full reapply.
4. In extension apply callback (`ExtensionLineRenderer.Apply` → `TryApplyCompositePinMarker`, **cluster extension layout — not drag**): same reposition-only branch when the extension vector/assignment is unchanged (not "always full-reapply").
5. **During `InteractionMode.Animating`:** keep current early return in `ApplyCompositePinsToNormalPlacements`; rely on `ApplyIndividualPlacements` + `TryGetCompositeAnchoredPlacement` for tip reposition. After animation settles, one full pass may rebuild markers whose segment changed (e.g. entering dense cluster with extensions).
6. **Drag path refactor (task 11):** `OnMarkerDragMove` should call `ApplyCompositePinTargetToMarker` with an explicit `PinPlacementTarget` instead of `ApplyCompositePinToMarker` (avoids fake empty `ViewportState` / zero container size). **Structural only** — drag ticks change angle/length every frame, so `ShouldRepositionOnly` will always return false during drag; this refactor does not enable reposition-only in drag.

**C. Call-site audit**

| Location | Expected guard |
|----------|----------------|
| `UpdateMarkerPositions` | `PrepareMarkerVisualsForPlacementUpdate()` only — no unconditional restore |
| `OnEditLayoutButtonClick` / saved layout load | Skip `RestoreBaseMarkerVisuals()` when `CanUseCompositePins()` — already guarded |
| `SwitchToVariantInEditor` | Skip restore when `CanUseCompositePins()` — already guarded |
| `ExitEditMode` | Replay via `ApplyManualLayout`; no restore flash |

### Files (expected)

| Action | Path |
|--------|------|
| Modify | `MainWindow.CompositePins.partial.cs` — reposition-only branch in normal + extension apply paths |
| Modify | `Services/CompositePinPlacementPolicy.cs` — add `ShouldRepositionOnly` (vector/assignment equality) |
| Modify | `MainWindow.LayoutEditor.partial.cs` — drag path calls `ApplyCompositePinTargetToMarker` directly (task 11) |
| Modify | `Tests/CompositePinPlacementPolicyTests.cs` — vector/assignment equality + reposition decision |
| Modify | `Tests/CompositePinZoomPersistenceTests.cs` — normal-path reposition-only contract + `TryApplyCompositePinMarker` extension-callback contract |
| Modify | [remove-pins-jpg-legacy-path-plan.md](remove-pins-jpg-legacy-path-plan.md) — [cross-plan closure](#cross-plan-closure) |

### Tasks

**Core persistence (done 2026-06-12)**

1. [x] Replace unconditional `RestoreBaseMarkerVisuals()` in `UpdateMarkerPositions()` with `PrepareMarkerVisualsForPlacementUpdate()`.
2. [x] Per-marker drawn fallback on composite failure only (`RestoreDrawnFallbackForCompositeFailure`).
3. [x] Tip-anchor reposition in `CompositePinPlacementPolicy.GetCompositeTopLeft` + behavior tests.
4. [x] Source-contract tests in `CompositePinZoomPersistenceTests`.
5. [x] Stub length/direction invariant across viewports (`CompositePinTargetBuilderTests`).

**Reposition-only optimization (required — implement in this order)**

6. [x] Add `CompositePinPlacementPolicy.GetCompositeTopLeft(Point tipScreen, CompositePinRenderPlan plan)`; refactor the `MarkerScreenPlacement` overload to delegate to it.
7. [x] Add `ShouldRepositionOnly(CompositePinRenderPlan? existingPlan, PinPlacementTarget newTarget, ...)` skeleton.
8. [x] Add `ShouldRepositionOnly` behavior tests (**before** wiring WPF paths).
9. [x] Wire reposition-only in `ApplyCompositePinsToNormalPlacements` when `!IsAnimating` via `TryApplyCompositePinAtTarget`.
10. [x] Wire reposition-only in extension apply callback (`TryApplyCompositePinMarker`) via `TryApplyCompositePinAtTarget`.
11. [x] Refactor `ApplyCompositePinToMarker` to build explicit `PinPlacementTarget` (removes fake `ViewportState`); drag path unchanged structurally.
12. [x] Document animation behavior: early return during `InteractionMode.Animating` is intentional; settled-state full pass handles segment changes.
13. [x] Source-contract tests: normal + extension paths reference `ShouldRepositionOnly` with render plan.

**Verification and closure**

14. [x] Manual smoke — #1–7 passed 2026-06-20; **#8** (composite-off regression) still open.
15. [x] Audit `RestoreBaseMarkerVisuals()` call sites — guarded at `SwitchToVariantInEditor` and `OnEditLayoutButtonClick` when `!CanUseCompositePins()`; `UpdateMarkerPositions` uses `PrepareMarkerVisualsForPlacementUpdate` only.
16. [x] Verify drag opacity: `OnMarkerDragStart` → `0.7`; `OnMarkerDragEnd` → `1.0` (`MainWindow.LayoutEditor.partial.cs`).
17. [x] Confirm `DefaultStubLengthPixels` consistency: `visual-config.json` has `24.0`; `VisualConfigServiceTests.Load_PinPartsDefaultStubLengthPixels_UsesDefaultWhenOmitted` asserts model default `24.0`.
18. [x] [Cross-plan closure](#cross-plan-closure) — pan/zoom flicker items flipped in legacy plan (smoke #2, #6, 2026-06-20).
19. [ ] Update [composite-pins-program.md](composite-pins-program.md) dashboard; move this plan to `docs/exec-plans/completed/` per program rules.
20. [x] `.\scripts\verify.ps1` green after reposition-only lands (314 tests, 2026-06-20).

### Cross-plan closure

| Legacy plan item ([remove-pins-jpg Phase 3](remove-pins-jpg-legacy-path-plan.md)) | Status | Closed by |
|-----------------------------------------------------------------------------------|--------|-----------|
| Full-map composite stub pins | [x] | Unzoomed Phases 2–5 + placement policy |
| Zoomed cluster extension/stub mix | [x] | Unzoomed Phases 3–4 |
| Non-extended tips anchored on map coordinate | [x] | `CompositePinPlacementPolicyTests` |
| `CompositePinTargetBuilderTests` green | [x] | Harness |
| Pan/zoom no drawn-pin flicker | [x] | Manual smoke **#2**, **#6** (2026-06-20) |
| Manual smoke matrix pan/zoom row | [x] | Same — [remove-pins-jpg Phase 6](remove-pins-jpg-legacy-path-plan.md) matrix row closed |

### Phase 7 risks

| Risk | Mitigation |
|------|------------|
| Clustering config change shifts which singles are visible | Load matches by name (Phase 6); missing names keep auto stubs |
| Clustering config change between save and replay | Reposition-only can make stub-vs-saved desync **visually silent** — manual smoke #7 must confirm saved layouts still replay correctly |
| `ApplyCompositePinToMarker` fake viewport in drag path | Task 11 removed fake `ViewportState`; drag builds explicit `PinPlacementTarget` |
| Single-location zoom replaces full-map stub with cluster/auto layout | `TryApplyFullMapLayoutForZoomedSingle` replays `fullmap_sWxH` when entry exists (2026-06-20) |
| Animation early-return hides full reposition-only path | Documented in Animation bar; settled-state pass is the verification point |

### Phase 7 manual smoke checklist

Run with `PinParts.UseCompositeRendering=true`. `DefaultStubLengthPixels` should be `24.0` in `visual-config.json` (see verification task 17; covered by `VisualConfigServiceTests`).

**Settled state:** see [Animation bar](#phase-7-policy-zoom-persistence-and-visual-invariance-decisions-2026-06-19) above (`_mode != InteractionMode.Animating` and last `UpdateMarkerPositions()` returned). Start pass/fail checks only after zoom/pan/resize animations finish.

| # | Step | Pass criteria |
|---|------|---------------|
| 1 | Start at full map | Visible single-location markers are composite stubs (not drawn pins) |
| 2 | Pan map / resize window (settled) | No flash to drawn pin; stubs stay composite; no visible head/shaft swap |
| 3 | Pick a **single-location** marker; zoom in (settled) | Same stub appearance as full map (screen-up, same length) |
| 4 | Back to full map (settled) | Same stub appearance as step 1 |
| 5 | Zoom into **multi-location** cluster (settled) | All members composite (extended + stub as layout dictates) |
| 6 | Rapid zoom in/out on a single-location marker | No drawn-pin flash at any **settled** frame |
| 7 | Full map → Edit Layout → drag stub → Save → exit → zoom in/out | Saved stub persists; editable at both zoom levels |
| 8 | Toggle `UseCompositeRendering=false` | Drawn fallback still works (regression) |

Optional log check during steps 2–6 (settled only):

```powershell
Select-String -Path "$env:APPDATA\InteractiveWorldMap\logs\app.log" -Pattern "leaving drawn pin fallback"
```

Healthy markers should not spam this warning. Exact source: `MainWindow.CompositePins.partial.cs` (~line 200) — `"Composite pin assets missing for '{name}', leaving drawn pin fallback."`

**Manual smoke result:** **#1–7 passed** 2026-06-20 (user confirmed). Remaining: **#8** (toggle `UseCompositeRendering=false` regression).

### Modularity and line-count guardrails

- Keep `MainWindow.xaml.cs` orchestration-only; composite persistence logic in `MainWindow.CompositePins.partial.cs` or `MainWindow.CompositePinPersistence.partial.cs` if it grows.
- Segment comparison stays in `Services/` (pure policy); WPF partials call it.
- Keep methods under ~40 lines; split reposition vs reapply vs animation deferral.
- No `Views/*` types in `Services/*`.
- Keep all touched `.cs` files under 800 lines.

### Tests

| Test | Status | Purpose |
|------|--------|---------|
| `CompositePinZoomPersistenceTests.UpdateMarkerPositions_DoesNotUnconditionallyRestoreBaseVisuals` | [x] | No unconditional restore in update path |
| `CompositePinZoomPersistenceTests.CompositeApply_HasExplicitDrawnFallbackRestorePath` | [x] | Fallback only on failure |
| `CompositePinTargetBuilderTests.Build_StubTarget_IsViewportProjectedButScreenLengthInvariant` | [x] | Stub segment invariant across viewports |
| `CompositePinPlacementPolicyTests` | [x] | Tip-anchor reposition math |
| `CompositePinPlacementPolicyTests.GetCompositeTopLeft_FromTipScreen_*` | [x] | Explicit tip point uses same anchor math as marker placement |
| `CompositePinPlacementPolicyTests.ShouldRepositionOnly_*` | [x] | Unchanged vector/assignment → reposition; angle/length/assignment change → full reapply |
| `CompositePinZoomPersistenceTests` normal-path reposition-only contract | [x] | `ApplyCompositePinsToNormalPlacements` calls `TryApplyCompositePinAtTarget` |
| `CompositePinZoomPersistenceTests.TryApplyCompositePinMarker_UsesRepositionOnlyPath` | [x] | Extension callback uses reposition path |

Source-contract tests remain for private `MainWindow` seams only; prefer behavior tests in `Services/`.

### Acceptance

- [x] No unconditional `RestoreBaseMarkerVisuals()` in normal composite placement update path.
- [x] Full-map stub pins retain `CompositePinMarker` across consecutive updates when composite enabled.
- [x] Stub length and screen-up direction invariant across viewports (non-dense singles).
- [x] Non-dense-cluster singles look the same composite stub at full map and when zoomed to that location (settled state) — manual smoke #3–4 passed 2026-06-20.
- [x] Reposition-only when target segment is visually unchanged (no `BuildPlan` / new `CompositePinMarker` on pan/resize/unchanged stub).
- [x] Zoom-in/zoom-out **settled** states: composite everywhere composite mode applies; no drawn-pin flash — manual smoke #1–2, #5–6 passed 2026-06-20.
- [x] Multi-location dense clusters show composite pins (extended + stub) when zoomed — manual smoke #5 passed 2026-06-20.
- [x] Non-dense singles editable at full map and when zoomed; saved stub persists across zoom — manual smoke #7 passed 2026-06-20.
- [x] `MainWindow.xaml.cs` remains orchestration-only for persistence work done so far.
- [x] No touched `.cs` file exceeds 800 lines (re-check after reposition-only).
- [x] `.\scripts\verify.ps1` passes after reposition-only lands (315 tests, 2026-06-20).

## Definition of Done

- `PinParts.UseCompositeRendering = true` → all individual markers use `CompositePinMarker` at every zoom level (dense cluster: extension or stub as layout dictates)
- Non-dense-cluster visible singles: same stub composite appearance at all zoom levels (**settled state**)
- Reposition-only optimization active when target segment is visually unchanged (non-animation updates)
- No drawn-pin flash during normal pan/zoom/update in composite mode (**settled state**)
- Cluster aggregate markers unchanged
- Edit mode save/load verified on composite pins (full map + cluster)
- Phases 5–6 verification complete; Phase 7 manual smoke recorded
- [Cross-plan closure](#cross-plan-closure) complete (legacy pan/zoom items flipped after manual smoke #2 and #6)
- Plan moved to `docs/exec-plans/completed/`; [composite-pins-program.md](composite-pins-program.md) updated with one-line stub per program rules
- `docs/guides/MANUAL_LAYOUT_EDITOR.md` reflects full-map edit flow, `fullmap_sWxH` keys, and no-zoom-while-editing
- `scripts/verify.ps1` passes
