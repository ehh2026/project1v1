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

Parent plan: [pin-parts-composite-placement-plan.md](pin-parts-composite-placement-plan.md)

TO_DO items: [Decide non-extended pin rendering policy](../../TO_DO.md), [Extend composite to all individual markers](../../TO_DO.md), [Run Phase 6 verification](../../TO_DO.md), [Manual edit mode for composite](../../TO_DO.md)

## Prerequisites (must complete first)

| Prerequisite | Plan reference |
|--------------|----------------|
| Extended-marker composite path stable | pin-parts Phase 5 ✅ |
| Phase 6 verification (debug overlay, angle spot-checks) | pin-parts Phase 6 |
| Non-extended rendering policy decided | This plan Phase 0 |

Do **not** start unzoomed rollout until Phase 6 acceptance criteria pass on extended markers in zoomed cluster view.

## Problem

Composite pins render only for **extended** image markers in the radial-extension pipeline (`TryApplyCompositePinMarker` gated by `PinParts.Enabled` + `PinParts.UseCompositeRendering`). Individual markers at full-map zoom and non-extended markers in cluster view still use legacy `ImagePinMarker`.

This split causes:

- Visual inconsistency between zoom levels
- Duplicate hit-testing and hover code paths
- Edit mode forced onto legacy markers even when composite is enabled globally

## Goal

When `PinParts.UseCompositeRendering` is true, every individual location marker uses `CompositePinMarker` at all zoom levels. Cluster aggregate markers (`ClusterMarker`) remain unchanged.

## Phase 0 — Policy decision (required gate)

**Deliverable:** documented decision recorded in pin-parts plan Open Decisions §3.

### Options

| Option | Description | Trade-off |
|--------|-------------|-----------|
| **A — Stub segment** | Non-extended pins use a default short upward shaft (configurable length, e.g. 24 px at current scale) from original position | Consistent composite look; arbitrary stub direction |
| **B — Tip-only composite** | Render head + minimal shaft cap anchored at location with zero-length logical segment | Less visual noise; may look like floating head |
| **C — Keep legacy at unzoomed only** | Extended = composite; unzoomed individual = legacy | Minimal work; inconsistent cross-zoom |

### Recommended MVP: Option A

- Add `PinParts.DefaultStubLengthPixels` to `visual-config.json` / `Models/PinPartConfig.cs`
- Stub direction: toward nearest map edge center or fixed "up" in screen space (document choice)
- Update pin-parts plan Open Decisions with chosen option

**Acceptance:** Decision recorded; config property added (can be unused until Phase 2)

## Phase 1 — Unified target segment builder

**Deliverables:** single helper that produces composite render targets for any marker.

### Files

| Action | Path |
|--------|------|
| Create | `Services/CompositePinTargetBuilder.cs` |
| Create | `Tests/CompositePinTargetBuilderTests.cs` |
| Modify | `MainWindow.xaml.cs` — use builder from extended and non-extended paths |

### Tasks

1. Create `CompositePinTargetBuilder.Build(Location, ViewportState, containerSize, RadialExtension?, PinPartsConfig)` returning `(Point start, Point end, double angleDeg)`.
2. When `RadialExtension` present: start = original screen, end = extended screen (current behavior).
3. When no extension: start = location screen position, end = start + stub vector per Phase 0 policy.
4. Unit tests: extended case matches current pipeline; stub case produces expected length and direction.
5. Refactor `TryApplyCompositePinMarker` to accept targets from builder regardless of extension state.

**Acceptance:**

- Builder tests pass
- No behavior change for existing extended composite pins

## Phase 2 — Unzoomed individual marker integration

**Deliverables:** full-map view uses composite pins for all individual markers.

### Files

| Action | Path |
|--------|------|
| Modify | `MainWindow.xaml.cs` — `CreateImagePinMarker`, `PositionMarkerAtNormalLocation`, marker rebuild paths |
| Modify | `Views/CompositePinMarker.xaml.cs` — verify scale at `ZoomLevel ≈ 1.0` |

### Tasks

1. In `CreateImagePinMarker` (or post-create hook), when composite enabled, create `CompositePinMarker` instead of `ImagePinMarker`.
2. Position unzoomed markers via `CompositePinTargetBuilder` stub targets, not centered bitmap placement.
3. Ensure marker wrapper bounds and hit-testing match composite bounds at unzoomed scale.
4. Verify hover feedback and click → subwindow behavior unchanged.
5. Verify locations visible as individual markers at both zoom levels do not jump visually on zoom in/out (same pin part pair where possible — store pair id on marker state if needed).

**Acceptance:**

- Unzoomed map shows composite pins for all individual image locations
- No regression in subwindow open on click
- Manual smoke at 1920×1080 and one high-DPI display if available

## Phase 3 — Non-extended markers in zoomed cluster view

**Deliverables:** dense-group members without radial extension also use composite rendering.

### Tasks

1. After `ApplyRadialExtensions`, for markers skipped by extension (below `MinLocationsForExtension` or non-dense), apply stub composite targets.
2. Confirm extension line renderer does not draw lines for stub-only markers (or draw optional faint stub — decide in Phase 0).
3. Update `ExtensionLineRenderer.Apply` call sites to pass composite applier for all image markers in group.

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
