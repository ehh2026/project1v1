---
status: active
owner: agent
started: 2026-06-23
parent_program: none
---

# Single-location Zoom Click Plan

> **For agentic workers:** Use project workflow (`docs/agent-workflows.md`, `AGENTS.md`) to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Allow users to click on a standalone marker while the map is unzoomed. The app should zoom into the region encompassing the marker and immediately auto-open the marker's content window. The zoom behavior already exists; this plan adds the deferred auto-open state.

## Phase 1: Event Handling and State Tracking (`MainWindow.xaml.cs`)
- [ ] Inspect the existing `LocationMarker.MouseLeftButtonDown` event handler in `MainWindow.xaml.cs`. (Note: Zoom already works via `OnClusterClicked(singleCluster)`.)
- [ ] Introduce a `Location? _autoOpenLocation` class-level field in `MainWindow` to track the target location.
- [ ] In the unzoomed branch (`viewport.ZoomLevel <= 1.0`), set `_autoOpenLocation` to the clicked `Location` reference right before the existing `OnClusterClicked` zoom routing is invoked.
- [ ] Ensure this only happens in the non-edit mode path.
- [ ] To handle rapid double-clicks or clicks during an ongoing zoom, add a guard: return early from the handler if `_mode == InteractionMode.Animating` (or equivalent animation state flag). **Ensure this guard is placed *after* the edit-mode check and *before* any animation/zoom logic, so edit-drag remains unaffected.**

## Phase 2: Post-Zoom Auto-Open and State Cleanup (`MainWindow.Navigation.partial.cs`)
- [ ] Do **not** clear `_autoOpenLocation` at the start of `AnimateZoomToCluster` on the success path.
- [ ] Clear `_autoOpenLocation` only on **failure/abort** paths: early `return` (e.g. `startViewport == null`), inside `catch` blocks of `AnimateZoomToCluster`, and at the start of `AnimateZoomOut`.
- [ ] In the completion callback of `AnimateZoomToCluster`, check if `_autoOpenLocation` is set.
- [ ] If set, copy the reference to a local variable and immediately set `_autoOpenLocation = null` synchronously.
- [ ] *After* `ShowZoomedView(cluster)` is called in the callback, invoke `ShowContentForLocation(localRef)` to open the subwindow correctly positioned.

## Phase 3: Integration, Testing, and Documentation
- [ ] Write AST/text-based structural validation tests in a new file `Tests/SingleLocationZoomClickTests.cs`. Verify:
  - `MainWindow.xaml.cs` declares `_autoOpenLocation`.
  - The individual marker handler sets it before zoom.
  - The individual marker handler references `IsAnimating` or `InteractionMode.Animating` to guard against double-clicks.
  - The cluster marker handler does *not* set it.
  - `MainWindow.Navigation.partial.cs` clears it in `AnimateZoomToCluster` exits/catch and `AnimateZoomOut`.
  - The completion callback clears the field and calls `ShowContentForLocation` *after* `ShowZoomedView`.
- [ ] Manual smoke tests (see Acceptance Criteria):
  - Check subwindow position at zoomed scale (after `ShowZoomedView`).
  - Verify existing subwindow is closed when auto-open runs.
  - Check composite stub standalone pin at full map works the same way.
- [ ] Update `CHANGELOG.md` under `[Unreleased]`.
- [ ] Register this plan in `docs/exec-plans/active/README.md`.

## Modularity / File Size Impact
- `MainWindow.xaml.cs` (~724 lines) will grow by ~5-10 lines.
- `MainWindow.Navigation.partial.cs` (~494 lines) will grow by ~10 lines.
- Both remain well under the 800-line limit.

## Expected Files Table
| File | Action | Description |
|------|--------|-------------|
| `MainWindow.xaml.cs` | Modify | Add field, `IsAnimating` guard, and set state on unzoomed click |
| `MainWindow.Navigation.partial.cs` | Modify | Handle completion callback and cleanup paths |
| `Tests/SingleLocationZoomClickTests.cs` | Add | Structural AST tests for the new logic |
| `CHANGELOG.md` | Modify | Add release note |
| `docs/exec-plans/active/README.md` | Modify | Register plan |

## Acceptance Criteria
- [ ] Unzoomed click on a visible standalone individual marker zooms with standard `ZoomScale` animation.
- [ ] When zoom-in completes, content subwindow opens for that location without a second click.
- [ ] Cluster marker click zooms in and does **not** auto-open any subwindow.
- [ ] Already-zoomed individual marker click still opens content immediately (no regression).
- [ ] Edit mode: individual marker drag works; no zoom, no auto-open.
- [ ] Back from auto-opened zoom returns to full map; no stale auto-open on subsequent cluster zoom.
- [ ] `_autoOpenLocation` cleared on zoom-out, animation failure, rapid double-clicks (ignored via `IsAnimating`), and early abort paths.
- [ ] Structural tests pass; `scripts/verify.ps1` passes.
- [ ] `CHANGELOG.md` updated.
- [ ] Plan registered in `docs/exec-plans/active/README.md`.
