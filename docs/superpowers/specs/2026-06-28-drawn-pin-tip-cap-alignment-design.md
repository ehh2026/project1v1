# Drawn Pin Tip Cap Alignment Design

**Date:** 2026-06-28
**Status:** Approved direction; awaiting written-spec review

## Goal

Allow drawn-pin divot caps to align with the visible pin shaft instead of always
remaining horizontal in screen space. The shaft-aligned curve must keep its
center at the map tip and bow away from the pin head at every shaft angle.

## Configuration

Add a string-enum `Alignment` property under
`PinMarkers.DrawnPinTipCap`:

| Value | Behavior |
|---|---|
| `ScreenHorizontal` | Preserve the existing horizontal screen-space cap. |
| `ShaftAligned` | Rotate the cap so its width axis is perpendicular to the shaft and its concavity follows the shaft direction. |

`ScreenHorizontal` is the model default so configs that omit `Alignment`
retain current behavior. The checked-in `visual-config.json` selects
`ShaftAligned` so the new appearance is available for immediate evaluation.
`Style` remains independently selectable as `None`, `Horizontal`, or
`Concave`, and remains default-off.

The alignment setting applies to both visible cap styles:

- `Horizontal` becomes a straight line perpendicular to the shaft in
  `ShaftAligned` mode.
- `Concave` uses the same perpendicular width axis while curving along the
  shaft axis.

## Geometry

`PinTipCapPlacement.ShaftDir` is the screen-space unit vector from the map tip
toward the pin head. For shaft-aligned geometry:

1. Normalize `ShaftDir` as the head direction `h`.
2. Derive the perpendicular width direction `w = (-h.Y, h.X)`.
3. Place concave endpoints at
   `tip + h * ArcDepthPx +/- w * (WidthPx / 2)`.
4. Place the quadratic control point at `tip - h * ArcDepthPx`.

This construction makes the quadratic point at `t = 0.5` exactly equal to the
map tip. The endpoints sit toward the head and the center bows away from it,
including diagonal, horizontal, vertical, and inverted manual-layout shafts.

For `Horizontal` style in shaft-aligned mode, endpoints are
`tip +/- w * (WidthPx / 2)`.

If `ShaftDir` is non-finite or too short to normalize, geometry falls back to
the existing normal orientation, with the head direction pointing upward.
Dimensions continue to use the current non-negative clamping behavior.

`ScreenHorizontal` retains the current geometry and vertical-sign fallback
unchanged.

## Tuning Panel

Add an `Alignment` combo box to the existing drawn-pin Tip cap section with:

- `ScreenHorizontal`
- `ShaftAligned`

Load, Apply, Reload, and Save use the existing Tuning event flow. Changing
alignment refreshes visible caps immediately. The control remains available
when the cap style is `None`, matching the existing width, line-weight, and
curvature controls, so a complete setup can be prepared before enabling caps.

## Architecture

- `Models/DrawnPinTipCapConfig.cs` owns the alignment enum and compatibility
  default.
- `Utilities/PinTipCapGeometry.cs` owns normalized shaft-relative basis math.
- `MainWindow.TipCap.partial.cs` selects screen-horizontal or shaft-aligned
  geometry from config.
- `Models/TuningPanelEventArgs.cs` and `Views/DeveloperTuningPanel.*` carry and
  edit the alignment value.

No renderer, placement DTO, extension-line, or manual-layout ownership changes
are required. The implementation should reuse the existing `ShaftDir` supplied
for built-in and extension-line caps.

## Testing

Automated coverage must verify:

- Omitted alignment deserializes as `ScreenHorizontal`.
- Both alignment values round-trip as strings.
- A diagonal shaft-aligned straight cap is perpendicular to the shaft.
- Shaft-aligned concave endpoints lie toward the head and its midpoint equals
  the map tip for diagonal, horizontal, vertical, and inverted directions.
- Degenerate and non-finite vectors use the normal-orientation fallback.
- Screen-horizontal geometry remains unchanged.
- Tuning loads, emits, validates, applies, reloads, and saves alignment.
- Existing cap placement, lifecycle, and layering tests remain green.

Manual visual acceptance compares `ScreenHorizontal` and `ShaftAligned` on
normal stubs plus angled extension/manual/dense-layout pins at full-map and
zoomed scales.

## Backlog And Completion

Move the intermittent cap-inside-head item to Deferred with a note that it is
not currently reproducible and that stale-cap refresh and head-layer safeguards
are implemented. Keep the broader visual acceptance item active until
shaft-aligned caps have been visually checked.

Update the active drawn-pin tip-cap exec plan and `[Unreleased]` changelog.
Archive the plan only when its remaining manual visual and interaction gates
are complete.
