# Continuous Pin Tracking During Zoom Animation

## Problem Statement

During zoom-in animations (`AnimateZoomToCluster` → `AnimateViewportTransition`), markers that have radial extension lines drawn beneath them freeze in their pre-animation screen positions while the map image and other markers move. This breaks the visual illusion of pins being attached to their map coordinates.

The original plan framed this as a general "zoom in and out" problem and proposed a single offset cache populated inside `AnimateViewportTransition`. Code inspection (see review at `temp/review-continuous-pin-tracking-2026-06-21.md`) revealed the problem is narrower and the proposed mechanism has several silent conflicts. This revision narrows scope and moves offset capture to the right place.

---

## OPEN INVESTIGATION — zoom-out tracking (NOT covered by this plan)

**Status: RESOLVED — 2026-06-21 — human verification confirms zoom-out tracks correctly.**

The reported visual issue (drawn pins not tracking during zoom-out) could not be reproduced. Manual verification confirmed that pins move with the map at the correct speed during zoom-out animation. The orchestrator's positions are being correctly applied to the canvas each frame. No follow-up fix is needed for zoom-out.

---

## Scope of the Fix

**In scope (this plan):**

- Zoom-in path only (`AnimateZoomToCluster`). Markers with radial extension lines freeze during the animation while the map moves behind them.
- Markers whose `Canvas.Left`/`Top` is suppressed by the `IsAnimating && HasLine` guard in `ApplyIndividualPlacements` (`MainWindow.xaml.cs:506`).
- Extension line detachment from markers during the same animation.

**Out of scope (this plan does not address):**

- **The specific `continue`-guard freeze on zoom-out**: `AnimateZoomOut` calls `_extensionLineRenderer.Clear()` (`MainWindow.Navigation.partial.cs:272`) before the animation loop starts and the per-frame guard at `MainWindow.xaml.cs:449` skips further clears while animating. So `_markerToLine` stays empty throughout the zoom-out animation, `HasLine(marker)` returns false for every marker, and the freeze guard at `MainWindow.xaml.cs:506` never fires. The freeze mechanism that breaks zoom-in does not occur on zoom-out. (Caveat: this only means the same *mechanism* is not active. Whether zoom-out's normal per-frame placement produces visually correct tracking — especially for manual-layout pins whose `OriginalScreen` is captured at full-map coordinates — is a separate question and is *not* validated by this plan. If a bug report or visual test shows zoom-out tracking is broken for other reasons, that is a follow-up plan.)
- **Manual-layout replay during zoom-out** (`ApplyManualLayoutDuringAnimation`, `MainWindow.Navigation.partial.cs:347`). The per-frame callback `onFrameUpdated` already owns marker placement on this path. The offset system must not interfere.
- **Composite pin "tracking" during animation.** Already works via `TryGetCompositeAnchoredPlacement` (`MainWindow.CompositePins.partial.cs:307`), which repositions existing composite pins to their map coordinate using the current viewport each frame.

## Root Cause Analysis (revised)

1. **`ApplyIndividualPlacements` early return** (`MainWindow.xaml.cs:506`): The `if (IsAnimating && _extensionLineRenderer.HasLine(marker)) continue;` guard freezes the Canvas position of any marker that has an extension line. This is the primary cause of the visible "pin left behind" effect during zoom-in.

2. **Extension lines do not move** (`Views/ExtensionLineRenderer.cs`): `_extensionLineRenderer.Apply()` is gated on `plan.Mode == MarkerPlacementMode.WithExtensions` (`MainWindow.xaml.cs:474`). During animation the orchestrator returns `AnimatingFallback` (`Services/MarkerPlacementOrchestrator.cs:61`), so lines are never re-applied. Lines have no in-place translate method (only `AddLine` adds new shapes and `MoveLineEndpoint` recreates the line). So even if the marker is moved by an offset system, its line stays at the pre-animation geometry and visibly disconnects from the marker.

3. **Capture ordering would be wrong**: `AnimateZoomToCluster` sets `_mode = InteractionMode.Animating` (`MainWindow.Navigation.partial.cs:52`) *before* calling `AnimateViewportTransition`. The first `UpdateMarkerPositions()` call inside the transition runs with `IsAnimating == true`, so the orchestrator returns AnimatingFallback and the freeze guard fires — meaning any offset captured *after* the first `UpdateMarkerPositions()` would be captured from a frame where the marker is already frozen. Captures must happen *before* the mode flips, while the settled state is still authoritative.

4. **`MarkerPlacementOrchestrator` skipping extensions** is intentional and correct (it preserves the fast path). It is not the cause of marker freeze — it is what allows the freeze guard at `ApplyIndividualPlacements:506` to fire without recomputing extensions.

5. **`ApplyCompositePinsToNormalPlacements` early return** (`MainWindow.CompositePins.partial.cs:374`) is also intentional and unrelated to marker tracking. Composite pins are repositioned each frame by `TryGetCompositeAnchoredPlacement`; the early return only blocks *rebuilding* composite markers from scratch during animation.

## Proposed Solution

Cache the per-marker visual delta from the marker's map-point projection to its actual canvas position, captured in the settled state before the animation starts. During animation, project the marker's map point using the current interpolated viewport and re-apply the cached delta. In parallel, suppress extension lines during animation and rebuild them at settle.

### Phase 1: Pre-Animation Offset Capture (in `AnimateZoomToCluster`)

1. Introduce a transient field in `MainWindow.xaml.cs`:
   ```csharp
   private readonly Dictionary<LocationMarker, Vector> _animationOffsets = new();
   ```
   (A `Vector` is a screen-space delta. Use `System.Windows.Vector` — same struct used elsewhere for canvas deltas.)

2. **Before** setting `_mode = InteractionMode.Animating` in `AnimateZoomToCluster`, run a full settled `UpdateMarkerPositions()` so all markers are at their authoritative pre-animation positions.

3. While `_mode == Normal`, populate `_animationOffsets`:
   - For each visible individual marker:
     - Project its `Location.PixelX`/`PixelY` to the *start* viewport screen coordinate `P_map = viewport.SourceToScreen(...)`.
     - Read the marker's current `Canvas.GetLeft`/`Top` as `P_canvas`.
     - Compute the **anchor** offset: pick the correct anchor for the marker's content type:
       - For `PinMarker` (drawn pin) content: anchor = `drawnPin.GetShaftTipPoint()` (in marker-local coords). The offset is `(P_canvas + shaftTip) - P_map`.
       - For `CompositePinMarker` content with a `RenderPlan`: anchor = `plan.TipAnchorLocal`. The offset is `(P_canvas + tipAnchor) - P_map`.
       - For plain (non-pin) marker: offset is `P_canvas - P_map`.
     - **Skip** markers that are flagged as belonging to a manual-layout replay path on this transition (zoom-out uses that path; zoom-in does not, so for the zoom-in scope we capture all visible individual markers).
     - **Skip** markers that are not visible (`Visibility != Visible`).

4. The capture invariant: `Canvas.TopLeft = P_map_screen - anchor + offset_screen` for the entire animation. As long as the anchor (shaft tip / tip anchor) is invariant for the marker, the offset is constant in screen pixels. Note: when the viewport scales, `P_map_screen` already moves with the map, so the offset's screen-space vector is correctly re-applied each frame to keep the pin attached.

5. Clear `_animationOffsets` after the animation completes (in the completion callback or a `finally` block).

### Phase 2: Frame-by-Frame Interpolation (in `UpdateMarkerPositions` and `ApplyIndividualPlacements`)

1. Keep the existing `IsAnimating` skip in `MarkerPlacementOrchestrator.Compute` — it returns `AnimatingFallback` and saves CPU. The plan does not change the orchestrator.

2. In `ApplyIndividualPlacements` (`MainWindow.xaml.cs:494`), **replace** the current guard:
   ```csharp
   if (IsAnimating && _extensionLineRenderer.HasLine(marker))
       continue;
   ```
   with offset-aware placement:
   - If `IsAnimating` and the marker is in `_animationOffsets`:
     - Compute `P_map_screen` from the current viewport for the marker's map coordinate.
     - Look up the anchor for the marker's current content (shaft tip for drawn pin, `TipAnchorLocal` for composite, origin for plain).
     - Apply: `Canvas.SetLeft(marker, P_map_screen.X - anchor.X + offset.X)`, `Canvas.SetTop(marker, P_map_screen.Y - anchor.Y + offset.Y)`.
     - `continue` to next marker.
   - If `IsAnimating` and the marker is **not** in `_animationOffsets` (e.g., it appeared after capture): fall through to the existing placement paths.
   - If not animating: keep the existing `TryPlaceDrawnPinAtMapPoint` / `TryGetCompositeAnchoredPlacement` / plain `Canvas.SetLeft/Top` branches unchanged.

3. In `UpdateMarkerPositions`, suppress extension lines during animation:
   - Before any line/marker work, if `IsAnimating` and lines currently exist, call `_extensionLineRenderer.Hide()` (or just `_extensionLineRenderer.Clear()` and remember the prior state). At settle, the post-animation `UpdateMarkerPositions()` will rebuild them via `_extensionLineRenderer.Apply` because the orchestrator returns `WithExtensions` again.
   - Note: this is a deliberate visual choice. During zoom the user sees pins attached to the map; the radial extensions reform at the end. This avoids the "line stays put while marker moves" problem entirely.

4. **`ApplyCompositePinsToNormalPlacements` and `PrepareMarkerVisualsForPlacementUpdate`**: no change. The offset path uses the marker's existing content (so the captured anchor is still valid for the duration of the animation) and `CanUseCompositePins()` is unchanged. Depth sort still runs each frame on the offset-positioned pins — that is correct because the depth item reads `Canvas.GetLeft/Top` (now reflecting the live map position) and the marker's `RenderPlan.TipAnchorLocal` (invariant).

### Phase 3: Post-Animation Settlement

1. The completion path is unchanged: `_mode = Normal`, full settled `UpdateMarkerPositions()` runs.
2. `_extensionLineRenderer.Apply` is called for each dense group (line 474) and lines reappear at the new viewport's geometry.
3. `ApplyCompositePinsToNormalPlacements` runs and rebuilds composite pins if needed.
4. `ApplyCompositePinDepthSort` runs on the settled positions.
5. Clear `_animationOffsets` (in the completion callback in `AnimateZoomToCluster`).

Because offsets were captured in the settled state and `P_map_screen` was tracked perfectly each frame, the first settled frame's positions should match the last animated frame's positions within sub-pixel tolerance (the only delta is rounding in viewport interpolation). There should be no visible snap for markers that don't change their dense-cluster membership across the transition.

## File Modifications

1. **`MainWindow.xaml.cs`**:
   - Add `_animationOffsets` field (private dictionary).
   - In `UpdateMarkerPositions`: when `IsAnimating`, hide/clear extension lines before any per-marker work. (Optional: re-add them at settle via the existing post-animation path.)
   - In `ApplyIndividualPlacements`: replace the freeze guard with offset-aware placement as described in Phase 2.2.

2. **`MainWindow.Navigation.partial.cs`**:
   - In `AnimateZoomToCluster`:
     - Run a settled `UpdateMarkerPositions()` *before* setting `_mode = Animating` (to guarantee a clean baseline).
     - After computing `startViewport` and before `_mode = Animating`, populate `_animationOffsets` as described in Phase 1.3.
     - In the completion callback, clear `_animationOffsets`.
   - **Do not** add offset logic to `AnimateZoomOut` (out of scope) or to `AnimateViewportTransition` (the offset population is animation-specific and should live with its caller).
   - `AnimateViewportTransition` stays a pure viewport interpolation method with no awareness of offsets.

## Decisions Deferred (call out, not block)

- **Manual-layout pins during zoom-in**: if the zoom-in source state has a manual layout, `ApplyManualLayoutDuringAnimation` is not called for zoom-in (it is only wired into zoom-out, `MainWindow.Navigation.partial.cs:324`). So this conflict is theoretical for the current scope. If a future change reuses the offset system for zoom-out, this needs revisiting.
- **New markers appearing mid-animation**: for zoom-in, marker visibility changes happen in the post-animation `ShowZoomedView` callback, not during the animation. So no marker lifecycle change happens mid-loop. The current scope does not need to handle this.
- **Edge case: a marker's content swaps mid-animation via `PrepareMarkerVisualsForPlacementUpdate`**: for `CanUseCompositePins() == true` (the configs that use composite rendering), `PrepareMarkerVisualsForPlacementUpdate` returns immediately (line 62) and does not mutate content. For non-composite configs, the offset system skips this concern because `TryGetCompositeAnchoredPlacement` returns false and the drawn-pin anchor is used. Either way, the anchor used during capture is the same anchor used during interpolation, so the offset remains valid. Validate this with a test on a non-composite config.

## Testing & Verification

1. **Zoom-in baseline**:
   - Launch with a config that has composite pins enabled. Enter a state with a dense cluster that has radial extension lines.
   - Trigger zoom-in (click cluster).
   - **Assert**: every marker — drawn, composite, and extended — tracks with the map throughout the animation. No marker visibly "left behind" relative to the moving image.
   - **Assert**: extension lines do not appear during the animation (they reform at settle).
   - **Assert**: at settle, extension lines appear at the new (zoomed) viewport's geometry, attached to their markers.

2. **Zoom-out regression — drawn pins on single-location markers specifically**:
   - This is a **required regression test** because the user has reported that drawn pins on single-location markers may not track the map during zoom-out. See the "OPEN INVESTIGATION" section above.
   - With composite rendering enabled, zoom in to a single-location cluster, then zoom out (Back button).
   - **Assert**: the single-location drawn pin's screen position matches the source-point projection of `viewport.SourceToScreen(PixelX, PixelY, ...)` at every interpolated frame.
   - **Assert**: this matches the pre-change behavior exactly. The offset system must not engage on zoom-out. No `_animationOffsets` entries should be populated.
   - If the pin *was* failing to track before this plan, this test will fail and must be flagged back to the open investigation rather than silently passed.
   - Repeat with a manual layout present for the full map (so `ApplyManualLayoutDuringAnimation` runs each frame). Both the manual-layout-replay path and the no-manual-layout path must track.
   - Repeat with `UseCompositeRendering = false` (drawn-pin-only config).

3. **Non-composite config**:
   - Launch with `UseCompositeRendering = false`. Repeat test 1.
   - **Assert**: drawn pins track with the map; no composite-related visual artifacts.

4. **Hidden markers**:
   - With zoom-in starting from a full map (where individual markers are hidden), `_animationOffsets` should be empty (no visible individual markers at capture time). The first frame's per-marker work is then a no-op for offsets. Validate no exceptions are thrown.

5. **Performance**:
   - Confirm frame rate during zoom-in remains smooth (no visible stutter compared to baseline). The offset path is O(N) per frame with a dictionary lookup plus basic addition.

6. **Depth sort integrity**:
   - During a zoom-in, observe whether z-order remains sensible (no marker that should be behind a line suddenly pops in front). This validates the G7 concern from the review.

7. **First-frame snap check**:
    - Compare the last animated frame's marker positions to the first settled frame's marker positions. Difference should be sub-pixel (just rounding from the viewport interpolation).

---

## Implementation Status

**Date:** 2026-06-21

### Completed
- [x] `_animationOffsets` field added to `MainWindow.xaml.cs:46`
- [x] Offset capture block added in `AnimateZoomToCluster` (`MainWindow.Navigation.partial.cs:52-89`): settled `UpdateMarkerPositions()` → capture per-marker offset → clear extension lines → flip to `Animating`
- [x] Freeze guard (`IsAnimating && HasLine`) replaced with offset-aware placement in `ApplyIndividualPlacements` (`MainWindow.xaml.cs:504-518`)
- [x] `_animationOffsets.Clear()` added in completion callback (`MainWindow.Navigation.partial.cs:107`)
- [x] Build: 0 warnings, 0 errors; 334 tests pass

### Pending
- [x] Visual/manual verification of zoom-in animation (pins track map, no lines during animation, lines reform at settle) — verified 2026-06-21
- [x] Zoom-out regression test per plan section "Testing & Verification" item 2 — verified 2026-06-21: zoom-out tracks correctly, no regression
- [x] OPEN INVESTIGATION: zoom-out tracking — RESOLVED 2026-06-21: manual verification confirms zoom-out tracks correctly; no fix needed
