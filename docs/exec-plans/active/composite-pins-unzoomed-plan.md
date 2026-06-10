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
- Phase 4 remains open: edit mode still uses the existing legacy/edit-mode behavior.
- Phase 5 remains open: manual visual capture/tuning is still pending.

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

**Deliverables:** edit mode drags composite pins, not legacy fallback.

Related TO_DO: [Make manual edit mode available for composite layouts](../../TO_DO.md)

### Files

| Action | Path |
|--------|------|
| Modify | `MainWindow.xaml.cs` — edit mode enter/exit rebuild |
| Modify | `Services/LayoutEditorController.cs` |
| Modify | `Views/CompositePinMarker.xaml.cs` — drag hit targets |

### Tasks

1. Remove or narrow "force legacy on edit mode enter" gate — edit mode should drag composite marker anchor (extended endpoint / stub end).
2. Verify drag updates extension line endpoint and `ManualLayoutMarker.ExtendedPosition` in source or screen space consistently with save format.
3. Verify save/load round-trip through `LayoutEditorController` with composite markers active.
4. Exit edit mode refreshes composite rendering without legacy flash.
5. Manual smoke: full edit → save → reload → zoom out → zoom in cycle.

**Acceptance:**

- Edit mode works on composite pins in zoomed cluster view
- Saved layouts replay correctly with composite rendering enabled

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

## Risks

| Risk | Mitigation |
|------|------------|
| Performance — composite per marker at full map | Cache render plans; lazy-build on first show |
| Visual clutter from stubs at unzoomed scale | Tune `DefaultStubLengthPixels`; allow 0 = head-only mode |
| Pair selection changes on zoom transition | Persist `selected_pair_id` on marker state across rebuilds |
| Edit mode drag on rotated head | Drag handle on shaft endpoint only for MVP |

## Definition of Done

- `PinParts.UseCompositeRendering = true` → all individual image markers use `CompositePinMarker` at every zoom level
- Cluster markers unchanged
- Edit mode save/load verified on composite pins
- Phase 6 / Phase 5 verification complete
- `scripts/verify.ps1` passes
