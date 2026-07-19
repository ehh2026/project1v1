# Drawn Pin Divot Cap Design

**Date:** 2026-06-28
**Status:** Approved direction; awaiting written-spec review

## Goal

Make drawn-pin tip caps read as dark divots where the pin shaft enters the map.
Both cap styles must be thin, near-black strokes rather than filled platforms.
The concave style must bow away from the pin head, including when a manual
layout or dense cluster places the head below the map tip.

## Visual Behavior

### Shared treatment

- Render `Horizontal` and `Concave` as open, unfilled, near-black stroked paths.
- Center the cap horizontally on the visible shaft tip.
- Keep the cap horizontal in screen space, including on angled extension lines.
- Use rounded stroke ends and joins so the cap meets the shaft cleanly.
- Draw exactly one cap at the terminus of the visible shaft, preserving the
  existing built-in-stub versus extension-line eligibility rules.

### Horizontal

Render a straight line through the visible shaft tip. Width and line weight use
the same settings as the concave style.

### Concave

The curve center is the visible shaft tip and is the point farthest away from
the pin head. The endpoints rise toward the head side:

- Head above tip (`shaftDir.Y < 0`): endpoints are above the tip and the curve
  bows downward.
- Head below tip (`shaftDir.Y > 0`): endpoints are below the tip and the curve
  bows upward.
- Diagonal shafts use the sign of `shaftDir.Y`; the cap remains horizontal.
- A nearly horizontal or degenerate shaft uses the normal pin orientation:
  endpoints above the tip and the curve bows downward.

This rule makes the center of the curve coincide with the shaft bottom in the
normal orientation while allowing the outer ends to wrap slightly up the shaft
sides. Curvature changes endpoint depth, not the center anchor.

## Configuration

`PinMarkers.DrawnPinTipCap` remains opt-in through `Style`, whose default stays
`None`.

The active controls become:

| Property | Meaning |
|---|---|
| `Style` | `None`, `Horizontal`, or `Concave` |
| `WidthPx` | Total screen-space cap width |
| `LineWeightPx` | Stroke thickness; defaults near the drawn shaft outline weight |
| `ArcDepthPx` | Vertical distance from the concave center to its endpoints |
| `Color` | Stroke color; default is near-black |

`HeightPx`, `ExtendPx`, and `UseOutlineRing` belong to the old filled-shape
model. Loading must remain tolerant of configs that contain them, but newly
saved config and the Tuning panel use the active controls above. The
implementation plan must choose and document a deterministic compatibility
mapping for old `ExtendPx` values so existing tuned widths do not unexpectedly
shrink.

## Tuning Panel

The Tip cap section provides:

- Style picker
- Width
- Line weight
- Curvature, enabled for `Concave`

Changes continue to use the existing live reapply flow. Labels and tooltips
describe the visual result without referring to the obsolete filled cap,
height, or outline ring.

## Architecture

- `Utilities/PinTipCapGeometry` owns open line and quadratic-curve geometry.
- `Views/DrawnPinTipCapRenderer` owns stroke presentation only.
- `MainWindow.TipCap.partial.cs` continues to resolve the live visible tip and
  tip-to-head direction for built-in and extension-line shafts.
- Models carry configuration and placement data; no layer dependency changes
  are required.

The change stays within the existing cap feature boundary and should not grow
the main composition files materially.

## Testing

Automated tests cover:

- Horizontal geometry is an open line centered on the tip.
- Concave geometry is an open curve with its midpoint at the tip.
- Head above the tip produces endpoints above the midpoint.
- Head below the tip flips the endpoints below the midpoint.
- Diagonal above/below directions use the correct vertical side.
- Degenerate and nearly horizontal directions use the normal downward bow.
- Width and line-weight config values load, save, validate, and reach the
  renderer through the Tuning event.
- Legacy cap fields do not prevent config loading.
- Existing one-visible-cap placement rules remain green.

Manual visual acceptance covers normal stubs and extension lines with heads
above and below their tips, at full-map and zoomed scales. The accepted
appearance is the 2026-06-28 mockup: near-black, approximately shaft-outline
weight, wider than the shaft, and with the curve center meeting the shaft tip.

## Documentation And Completion

Implementation updates the active drawn-pin tip-cap exec plan, removes or
narrows the corresponding `docs/TO_DO.md` item after visual confirmation,
updates `[Unreleased]` in `CHANGELOG.md`, and archives the exec plan only after
the remaining visual/manual acceptance gates pass.
