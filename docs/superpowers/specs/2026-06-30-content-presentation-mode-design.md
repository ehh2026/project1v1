# Content Presentation Mode Design

**Date:** June 30, 2026

## Goal

Allow a visitor to tap or click the loaded content in the center content window to inspect it at the maximum size available inside the main map window, then tap or click it again to return to the original popup layout.

## Interaction

- A completed primary click or touchscreen tap on the content area toggles presentation mode.
- Entering presentation mode saves the content window's current bounds and presentation styling.
- A second completed primary click or tap on the content area restores the saved bounds and styling.
- Clicking the Translate button does not toggle presentation mode.
- The location title is hidden in presentation mode.
- The Translate button remains visible when translation text is available.
- The thumbnail browser and didactic-text windows are hidden while presentation mode is active and shown again after restoration.
- Closing the content or navigating back while presentation mode is active closes the hidden companion windows through the existing content-window cleanup path; they must not reappear independently.

## Presentation Appearance

- The content window fills the main map window's bounds in device-independent pixels.
- The maximized surface has no visible border, rounded corners, drop shadow, or outer padding.
- Images keep proportional `Uniform` scaling and are centered.
- Space not occupied by the image is black.
- `visual-config.json` exposes `MaximizedContentBackgroundOpacity`, defaulting to `1.0`. Values are clamped to the inclusive range `0.0` through `1.0` before use.
- The configured opacity applies only to the black presentation background. Normal popup background styling remains unchanged.
- The Translate button occupies only its required bottom area; the image receives all other available presentation space.

## Architecture

`ContentSubwindow` owns presentation-specific visual state and the saved normal bounds. It exposes a toggle request/state-change contract without learning about the thumbnail or didactic windows.

`MainWindow.Content.partial.cs` remains the content-window coordinator. It responds to the content window's presentation state changes by hiding or restoring the companion windows. It supplies the current main-window bounds and configured background opacity to the content window.

`VisualConfig` owns the new opacity setting so gallery operators can change it in `visual-config.json` without recompiling. No runtime Tuning panel control is added in this slice.

The design does not introduce a second viewer window or move content into a new main-window overlay. Reusing the existing `ContentSubwindow` preserves the current image, translation state, selected thumbnail, and close/navigation lifecycle.

## State and Lifecycle

1. `MainWindow` creates the content window and supplies the configured presentation opacity.
2. The content window displays content normally and records no presentation bounds.
3. A content-area click/tap requests presentation mode.
4. The content window records its normal `Left`, `Top`, `Width`, and `Height`, applies the owner's current bounds, and switches to borderless black presentation styling.
5. `MainWindow` hides currently visible companion windows and remembers which ones were visible.
6. A second content-area click/tap restores the normal bounds and popup styling.
7. `MainWindow` shows only the companion windows that it hid for this presentation session.
8. Loading another thumbnail while presentation mode is active updates the image without leaving presentation mode.
9. Closing or replacing the active content window clears presentation state as part of normal window disposal.

## Error and Edge Handling

- If no owner window is available, the toggle request is ignored and normal popup mode is preserved.
- Invalid opacity values are clamped instead of causing startup or rendering failure.
- Repeated toggle requests are idempotent at each transition boundary: normal state saves bounds once, and restoration clears the saved bounds once.
- Companion windows that were already hidden or absent before presentation mode are not shown during restoration.
- Owner bounds are read when entering presentation mode so the feature uses the main window's current size and position.

## Testing

- Configuration tests verify the default opacity and JSON deserialization.
- Focused presentation-state tests verify owner-relative maximum bounds, opaque-black default styling, removal and restoration of popup chrome, title visibility, Translate-button visibility, and exact normal-bound restoration.
- Input-wiring tests verify that the content surface toggles presentation mode while the Translate button remains independent.
- Main-window coordination tests verify that visible companion windows hide on entry, restore on exit, and remain closed when the content lifecycle ends.
- Full repository verification uses `.\scripts\verify.ps1`.

## Documentation and Completion

- Replace the broad `docs/TO_DO.md` maximize bullet with a precise linked implementation item while work is active.
- Remove that item when implementation and verification are complete.
- Register the implementation plan under `docs/exec-plans/active/`, then archive it under `docs/exec-plans/completed/` at completion and update the active registry.
- Add the user-visible interaction and configurable background opacity under `[Unreleased]` in `CHANGELOG.md`.
