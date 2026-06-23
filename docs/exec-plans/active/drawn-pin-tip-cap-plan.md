---
status: active
owner: agent
started: 2026-06-23
---

# ⚠️ NEEDS REVIEW — Drawn pin tip cap

This plan has not been reviewed or approved yet. The owner/author wrote it from the TO_DO.md bullet alone without confirming the design direction with a human. Do not start implementation until a human confirms: shape (horizontal vs concave), scope (drawn-only vs also composite), and whether the cap is decorative or functional (e.g., to mask the shaft where it meets a tilted or extended manual layout).

# Drawn pin tip cap

> TO_DO source: [../../TO_DO.md](../../TO_DO.md) — *Composite pins & manual layouts* → "Add a horizontal or concave line at the drawn pin tips"

## Goal

Add a visible cap (horizontal bar, or shallow concave arc) at the bottom tip of the **drawn** pin shaft in `PinMarker.xaml` so the tip reads as a deliberate terminus rather than a flat rectangle end. The cap should be optional, style-driven via `visual-config.json`, and not regress performance during zoom.

**Cap orientation is fixed in screen space**, not in the pin's local coordinate system:

- The horizontal cap is a true horizontal line in screen coordinates (along screen +x), regardless of the shaft's on-screen rotation. Even if the pin is tilted, drawn at an angle, or rendered as a manual-layout pin with a non-vertical shaft, the cap stays flat to the screen.
- The concave variant is a shallow screen-space arc, with the **concave side facing the shaft** (i.e., the curve bows away from the shaft, opening toward the shaft). For the common case of a vertical shaft that means the arc curves downward at the ends and peaks in the middle — concave-up.
- The shaft still attaches to the cap at whatever point on the cap the geometry intersects, and any shaft pixels that would extend below the cap (after the intersection) are clipped, so the cap reads as the actual tip.

Out of scope unless expanded later: composite pins, manual-layout pins with extended/saved geometry (their shafts end at the layout endpoint already and are handled by `ExtensionLineRenderer` / `CompositePinMarker`). Confirm in review.

## Background

Current state from `Views/PinMarker.xaml:13-22`:

```xaml
<Grid x:Name="ShaftHost" VerticalAlignment="Top" HorizontalAlignment="Center">
    <Rectangle x:Name="PinShaftOutline" .../>
    <Rectangle x:Name="PinShaft" .../>
</Grid>
```

The shaft is a plain filled `Rectangle` with a darker `PinShaftOutline` behind it. There is no tip decoration; at low zoom the bottom edge is a flat horizontal line flush with the shaft body, which can read as truncated or unfinished — the motivation behind the TO_DO bullet.

Related code:
- `Models/PinMarkerConfig.cs` — runtime config for shaft thickness, length, colors.
- `Models/visual-config.json` (`pinMarker.*`) — user-editable tuning values.
- `Services/.../PinPlacementService` / `Views/PinMarker.xaml.cs` — places the cap relative to shaft bottom and exposes the cap to layout/save if needed.

## Design questions for review

1. **Shape** — horizontal bar (extends shaft width × ~6 px, same fill as shaft) vs concave arc (shallow screen-space `Path` with concave side facing the shaft, ~4–8 px tall). Human picks one or ships both. Both should be implementable behind the same `TipCapStyle` enum.
2. **Color** — same as shaft fill, or contrasting (outline color)?
3. **Width relative to shaft** — flush with shaft, or slightly wider (cap-of-cap look)?
4. **Scope** — drawn only, or also composite / manual-layout? TO_DO bullet says "drawn pin tips" but clarify. Note: when scope is extended, the screen-space-cap invariant still holds — even a tilted manual-layout shaft gets a horizontal/horizontal-concave cap.
5. **Disabled / legacy layout** — if a pin is on an old `ManualLayoutMarker` that pre-dates this feature, cap should either render with defaults or be off. Pick a default.

## Proposed approach

1. **Config**
   - Add `PinMarkerConfig.TipCap` with `Style` (`None` | `Horizontal` | `Concave`, default `None` for backwards compatibility; opt-in via `visual-config.json`).
   - Add `TipCapHeightPx` (default 6) and `TipCapColor` (default `null` → use shaft fill) and `TipCapExtendPx` (default 0, how much wider than shaft).
   - Concave-only: `TipCapArcDepthPx` (default 3) — how far the arc sags below the horizontal baseline (i.e., toward the screen-bottom) so the concave side faces the shaft.
2. **XAML / render model**
   - The cap is **not** drawn inside the `PinMarker` UserControl's local coordinate system — local transforms on the pin (e.g., manual-layout rotation) would tilt the cap. Instead, the cap is drawn by a sibling/overlay element positioned in **map canvas (screen) space** at the tip's current screen position.
   - Concretely: introduce a `TipCapOverlay` `Canvas` (or a list of cap visuals) on the map host that draws a small `Path` per drawn pin whose tip is currently visible. Each `Path` uses screen-space coordinates directly; its `Width`/`Height`/`Data` are recomputed on every pin position / zoom / orientation change but no rotation is applied to the visual.
   - For `Horizontal`: a flat `Rectangle` (or `Path` with a single horizontal segment) of width = shaft-width-on-screen + `TipCapExtendPx` and height = `TipCapHeightPx`, anchored to the shaft tip.
   - For `Concave`: a `Path` with three points (left baseline, mid-arc, right baseline). The mid-arc point sits `TipCapArcDepthPx` **above** the baseline (toward the shaft) so the curve opens downward and the concave side faces the shaft. Use a quadratic Bezier through the three points; the closed region between the baseline and the arc forms the cap shape.
   - Shaft clipping: clip the shaft's lower pixels against the cap region so the visible shaft ends exactly at the cap (no protrusion below). The clip is in screen space, so it composes correctly with any pin rotation.
3. **Code-behind / placement**
   - `PinMarker.xaml.cs` (or a dedicated `TipCapService` in `Services/`) is responsible for:
     - Tracking each drawn pin's current screen-space tip point (the bottom-center of the shaft in screen coords, after all pin transforms).
     - Re-positioning / re-shaping the matching `TipCap` visual in the overlay canvas whenever:
       - the map zooms or pans,
       - the pin's manual-layout orientation changes,
       - the pin's shaft length / thickness changes (head variant, stub length),
       - the pin enters / leaves the visible region.
   - The cap visual carries **no rotation transform** — orientation comes entirely from the cap's geometry being rebuilt in screen space. This is what enforces the "horizontal in screen space" invariant.
4. **Persistence** — if the cap should ride along with manual-layout saves, add a `TipCap` field to `ManualLayoutMarker`. If purely cosmetic and deterministic from config, skip persistence in v1. (Cap orientation is not stored — it's always screen-space.)
5. **Performance** — cap is one extra `Path` per visible drawn pin, rebuilt only on geometry-change ticks (not every frame) and reused between frames when the tip hasn't moved. Verify on the zoom-perf track's harness in [zoom-performance-appearance-plan.md](zoom-performance-appearance-plan.md) that adding it does not regress the per-frame budget. The cap path itself is a 2- or 3-segment `Path` — trivially cheap to draw.

## Acceptance criteria

- [ ] `visual-config.json` can set `tipCap.style = "horizontal"` and `tipCap.style = "concave"` and the cap is visible at the bottom of every drawn pin shaft.
- [ ] **Screen-space orientation invariant**: the cap is horizontal in screen space (along screen +x) regardless of the pin's on-screen rotation. A pin whose shaft is tilted 30° still has a flat horizontal cap; a manual-layout pin with a non-vertical saved shaft still has a flat horizontal cap.
- [ ] **Concave-side invariant**: the concave variant's arc opens toward the shaft — the curve bows away from the shaft. For a vertical shaft the arc is concave-up; for an inverted (upside-down) pin the arc is concave-down. The cap never looks like a hat sitting on top of the shaft.
- [ ] **Shaft terminates at the cap**: no shaft pixels are visible below the cap line, even when the pin is tilted (clipping happens in screen space).
- [ ] Default config (`"none"`) leaves the current visual unchanged — zero regression for users who don't opt in.
- [ ] Cap scales with shaft thickness and follows the pin during zoom (covered by the continuous-tracking work in [zoom-performance-appearance-plan.md](zoom-performance-appearance-plan.md)).
- [ ] No new per-frame allocations in the cap rendering path; logged via `FileLogger` at `Debug` only.
- [ ] `scripts/verify.ps1` green; visual harness screenshot includes one drawn pin with each cap style, including a tilted/manual-layout pin to confirm the screen-space invariant.

## Tests

- **Unit** — `Models/PinMarkerConfig` deserialization for new `TipCap` fields and default fallback when absent.
- **Unit** — geometry helper that computes cap width/height from shaft size; small table-driven test.
- **Unit** — cap orientation: given a pin rotated by an arbitrary angle around its head, the cap's `RenderTransform` is `Identity` and its `Data` / `Width` are aligned to screen +x. Asserts the screen-space invariant without rendering.
- **Unit** — concave direction: given the shaft direction vector, the arc mid-point lies on the shaft side of the cap baseline (dot product of (midPoint − baselineCenter) with shaftDirection > 0). Asserts the concave-side invariant.
- **Structural** — confirm Views still only depend on Models (`Tests/Architecture/LayerDependencyTests.cs`). The new `TipCapService` lives in `Services/`, not Views.
- **Visual / harness** — pin screenshot at full map and at 4× zoom with each cap style, plus a screenshot of a tilted pin to manually verify the cap stays horizontal in screen space.

## Open risks

- Concave arc on very short shafts (< 8 px) can look like a glitch — clamp minimum shaft length for cap visibility.
- Composite pin heads are bigger and the cap is not designed for them; review must decide scope before any composite work.
- Manual-layout pins already have a saved endpoint — adding a cap there means a new field in `ManualLayoutMarker` and a migration for existing saves.

## Status

Drafted 2026-06-23. **Not yet started — needs human review on the five design questions above before implementation.**
