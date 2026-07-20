---
status: active
owner: agent
started: 2026-06-23
needs_review: false
requirements_ref: drawn-pin-tip-cap
parent_program: docs/exec-plans/active/composite-pins-program.md
depends_on: docs/exec-plans/completed/drawn-pin-model-separation-plan.md
---

# Drawn pin tip cap — design confirmed

**Human-confirmed 2026-06-23.** Authored from a TO_DO bullet, revised after human review, and the intent ("the cap makes the pin look stuck into the map") confirmed. Implementation may begin per the phasing below; **concave is still allowed to iterate** (Phase 4b) and horizontal may ship before concave is finalized.

| # | Decision | Confirmed |
|---|----------|-----------|
| 1 | **Shape phasing** | Ship **`Horizontal` first** (Phases 2–3). Add **`Concave` behind the same enum** once horizontal is verified on stub + extension-line tips. Expect **visual iteration** on concavity before calling concave done (see [Concave iteration](#concave-iteration)). |
| 2 | **T-junction look** | Extension-line caps are horizontal in screen space at the map anchor while the shaft is angled — the resulting **T junction** is the intended screen-space invariant. |
| 3 | **Stroke treatment** | Both styles are open, unfilled, near-black strokes. `LineWeightPx` defaults near the shaft outline thickness. |
| 4 | **Phasing** | Phase 2 = stub tips (`Horizontal`); Phase 3 = extension-line tips (`Horizontal`); Phase 4 = outline polish; **Phase 4b** = concave geometry + human visual pass (may loop). |
| 5 | **Prerequisite** | [drawn-pin-model-separation-plan.md](../completed/drawn-pin-model-separation-plan.md) has landed **or** is explicitly deferred (cap hooks on monolithic `PinMarker` with a named migration path). |

---

# Drawn pin tip cap

> TO_DO source: [../../TO_DO.md](../../TO_DO.md) — *Composite pins & manual layouts* → "Add a horizontal or concave line at the drawn pin tips"

## Goal

Add an opt-in cap (horizontal bar **or** shallow screen-space concave arc) at the **visible terminus of every drawn pin shaft** — built-in auto-stub **or** extension line — so the tip reads as the pin being **stuck into the map surface** rather than a flat cut end resting on top of it.

**Intent (drives every visual decision below):** the cap depicts the point where the pin *enters the map*. Both styles are thin, near-black strokes rather than filled platforms. The **concave** variant bows **away from the pin head**; its true midpoint coincides with the shaft tip. When a manual layout places the head below the tip, the curve flips upward to preserve the perspective.

**Core rule — cap follows the visible shaft, not the hidden one:**

| Visible shaft | Cap location | Built-in shaft hidden? |
|---------------|--------------|------------------------|
| Built-in `PinShaft` (auto-stub) | `GetShaftTipPoint()` projected to screen | No |
| Extension line (`ExtensionLineRenderer` line pair) | Line **start** = map anchor (`X1`, `Y1`) | Yes — **no** cap on the hidden built-in; cap **on** the extension line tip |
| Edit-mode drag (temporary extension line) | Same as extension line — map anchor | Yes — cap stays on the **visible** drag line tip |
| Neither shaft visible | No cap | — |

The cap alignment is configurable. `ScreenHorizontal` preserves the original screen-space +x width axis. `ShaftAligned` rotates the width axis perpendicular to the visible shaft and curves along the full tip-to-head direction. `Horizontal` is an open straight stroke through the tip. `Concave` is an open quadratic Bezier whose endpoints sit on the head side and whose true midpoint is the tip, so it bows away from the head. Width, line weight, curvature, and alignment are independently tunable.

## Scope

| Pin role | In plan? | Phase | Notes |
|----------|----------|-------|-------|
| **Auto-stub** (built-in shaft visible, no extension line) | ✅ | 2 | Tip = screen projection of `GetShaftTipPoint()` (`Views/PinMarker.xaml.cs:181-184`). |
| **Extension-line drawn** (manual-layout, radial-extension, edit-mode drag) | ✅ | 3 | Visible shaft = `CreatePinLinePair` (`Views/ExtensionLineRenderer.cs:369`). Tip = line start / map anchor. Built-in hidden — cap moves to extension tip. |
| **Composite pin** (`ShaftTipCapLayer`) | ❌ | — | Asset-based tip cap already exists. |
| **Non–drawn-pin mode** (`UsePinMarkers = false`) | ❌ | — | Circular `LocationMarker` only. |

### Eligibility (one cap per marker)

For each visible `LocationMarker` where `UsePinMarkers` is true and content is a drawn pin (`PinMarker` or post-split `AutoStubPinMarker` / `ManualLayoutPinMarker` head):

1. `DrawnPinTipCap.Style != None`,
2. Determine **which shaft is visible** (mutually exclusive in normal operation):
   - **Extension line** — `_extensionLineRenderer.HasLine(marker)` → cap at line start (Phase 3). Built-in shaft is hidden; do **not** cap the built-in.
   - **Built-in stub** — no extension line and built-in shaft visible → cap at `GetShaftTipPoint()` (Phase 2).
3. Skip when neither shaft is visible (should not occur for drawn pins in normal operation).

`MainWindow` evaluates this filter, resolves tip screen position + shaft direction + outline width, and passes `PinTipCapPlacement` records to the renderer. The renderer does not call `ExtensionLineRenderer` directly; add `TryGetLineStart(marker, out Point)` on `IExtensionLineRenderer` / `ExtensionLineRenderer` in Phase 3 if not already available (`TryGetLineEndpoint` exists at `:296` for the head end only).

### Zoom animation note

During zoom-in, extension lines may be suppressed while markers still track via the offset cache ([continuous-pin-tracking-during-zoom-plan.md](../completed/continuous-pin-tracking-during-zoom-plan.md)). Extension-line caps must use the **projected map anchor** each placement tick when the line visual is absent, so the cap stays on the map tip even when `HasLine` is temporarily false mid-animation. Phase 3 must cover both settled (line present) and animating (projected anchor) paths.

## Background

Built-in shaft (`Views/PinMarker.xaml:13-22`):

```xaml
<Grid x:Name="ShaftHost" VerticalAlignment="Top" HorizontalAlignment="Center">
    <Rectangle x:Name="PinShaftOutline" .../>
    <Rectangle x:Name="PinShaft" .../>
</Grid>
```

Extension-line shaft (`ExtensionLineRenderer.CreatePinLinePair`): outline `Line` (z 999) + core `Line` (z 1000); `start` = map anchor, `end` = head connection.

| Area | Location |
|------|----------|
| Built-in dimensions | `Views/PinMarker.xaml.cs:78` — `ApplyPinDimensions` |
| Hide built-in shaft | `Views/PinMarker.xaml.cs:124` — `SetShaftVisible(false)` when extension line is active |
| Built-in tip (local) | `Views/PinMarker.xaml.cs:181` — `GetShaftTipPoint()` |
| Line factory | `Views/ExtensionLineRenderer.cs:369` — `CreatePinLinePair(start, end)` |
| Placement apply | `MainWindow.xaml.cs:449` — `UpdateMarkerPositions()` → `ApplyIndividualPlacements` (`:503`) |
| Config | `Services/VisualConfigService.cs:10` — `Load`; section `PinMarkers` in root `visual-config.json` |
| Composite naming | `TipCapLength` / `ShaftTipCapLayer` — **do not reuse** for drawn-pin caps |

## Architecture constraints

1. **Layering** — renderer in `Views/`; geometry in `Utilities/`; DTO in `Models/`; `MainWindow` orchestrates (`LayerDependencyTests`).
2. **Overlay canvas** — `Canvas` sibling in `MainWindow.xaml`, not inside `PinMarker`.
3. **Model separation** — prefer hooks on `AutoStubPinMarker` (stub) and extension-line path (manual-layout); migrate if separation lands mid-work.
4. **No screenshot CI** — manual smoke only.
5. **Naming** — `DrawnPinTipCap*` / `PinTipCap*`; not composite `TipCapLength`.

## Modularity / file-size impact

| File | Expected growth |
|------|-------------------|
| `MainWindow.xaml` | +1 `PinTipCapOverlay` canvas |
| `MainWindow.TipCap.partial.cs` (~120–180 lines) | Eligibility, stub + extension tip resolution, animation fallback |
| `Views/DrawnPinTipCapRenderer.cs` (~200–280 lines) | Overlay pool; `Sync(placements, config)` |
| `Utilities/PinTipCapGeometry.cs` (~100–140 lines) | Geometry from tip, shaft direction, outline width |
| `Models/DrawnPinTipCapConfig.cs`, `PinTipCapPlacement.cs` | Config + DTO (`ShaftKind`: `BuiltIn` \| `ExtensionLine`) |
| `Views/ExtensionLineRenderer.cs` | +`TryGetLineStart` (~10 lines) in Phase 3 |
| `Models/PinMarkerConfig.cs` | +1 property |

No `ManualLayoutMarker` schema change (global config only).

## Cap geometry

### Stub tip (Phase 2)

```
tipScreen = markerTopLeft + Transform(GetShaftTipPoint())
shaftDir  = normalize(headConnectionScreen - tipScreen)   // ≈ (0, -1) for vertical stub
```

Apply `PinTransform` (hover scale). Width comes from `WidthPx`; legacy configs without it resolve to `PinShaftOutline.Width + 2 * ExtendPx`.

### Extension-line tip (Phase 3)

```
tipScreen = lineStart   // (X1, Y1) — map anchor
shaftDir  = normalize(lineEnd - lineStart)   // toward head
```

Width from extension line outline thickness: `coreWidth + 2 * outlineExtra` (same math as `CreatePinLinePair`). During zoom animation without a line visual, `tipScreen` = projected map point from placement / offset cache.

### Anchor rule (both shaft kinds, both cap styles)

- **Horizontal** — open `LineGeometry` from `tipX - WidthPx/2` to `tipX + WidthPx/2` through `tipY`.
- **Concave** — open quadratic Bezier. Both endpoints are `ArcDepthPx` toward the head's vertical side. The control point is mirrored `ArcDepthPx` away from that side, making the evaluated point at `t = 0.5` equal `tipScreen`.
- If `shaftDir.Y < 0`, endpoints are above the tip and the curve bows down.
- If `shaftDir.Y > 0`, endpoints are below the tip and the curve bows up.
- Near-horizontal or degenerate directions use the normal downward bow.

### Concave direction test

Assert the evaluated Bezier midpoint equals `tipScreen`, and endpoint Y is on the head side for heads above, below, and diagonally offset from the tip.

**Unit tests prove direction only, not aesthetics.** Whether the arc looks good on stub vs extension-line vs shallow angle is a **human visual gate** in Phase 4b.

### Concave iteration

The concave cap is the highest-risk visual piece. Plan for **one or more tune-and-review loops** after horizontal is working:

| Principle | Implementation |
|-----------|----------------|
| **Isolate curve math** | All concave path construction lives in `Utilities/PinTipCapGeometry` behind a single entry point (e.g. `BuildConcaveGeometry(...)`). Swapping quadratic → cubic Bezier, circular arc, or adjusted control-point formula must not require renderer changes. |
| **Tune via config first** | Use `WidthPx`, `LineWeightPx`, and `ArcDepthPx` in `visual-config.json` or the Tuning panel. |
| **Horizontal before concave** | Phases 2–3 ship and smoke-test `Style: "Horizontal"` only. Concave is Phase 4b — do not block stub/extension plumbing on concave look. |
| **Review matrix** | Human eyeball on concave at: vertical stub (full map + zoomed), extension line with head above tip, and inverted manual-layout/dense-cluster case with head below tip. |
| **Acceptable code churn** | If the first Bezier formula reads as too flat, too deep, or wrong on T-junctions, revise `PinTipCapGeometry` and re-run smoke — no need to reopen overlay/placement architecture. |
| **Optional follow-up knobs** | If `ArcDepthPx` alone is insufficient after iteration, add config fields (e.g. `ArcControlBias`, separate stub vs extension depth) in a small follow-up — avoid preemptive over-configuration. |

**Phase 4b exit:** human confirms the near-black stroke reads as a divot where the pin enters the map and flips correctly when the head is below the tip.

### Z-order and hit-testing

- `Panel.SetZIndex(cap, 1500)` — above extension lines (999/1000), below marker heads (2000).
- `IsHitTestVisible = false`.

### Refresh cadence

Rebuild on placement-apply ticks, config changes, hover transform (stub), line endpoint moves (extension). Pool visuals; no per-tick allocation when placements unchanged.

## Config schema

```json
"PinMarkers": {
  "DrawnPinTipCap": {
    "Style": "None",
    "Alignment": "ShaftAligned",
    "WidthPx": 12.0,
    "LineWeightPx": 3.0,
    "ArcDepthPx": 3.0,
    "Color": "#FF111111"
  }
}
```

| Field | Default | Notes |
|-------|---------|-------|
| `Style` | `"None"` | `"None"` \| `"Horizontal"` \| `"Concave"` (PascalCase) |
| `Alignment` | `"ScreenHorizontal"` model default; checked-in config uses `"ShaftAligned"` | `"ScreenHorizontal"` \| `"ShaftAligned"` |
| `WidthPx` | `12` | Total cap width |
| `LineWeightPx` | `3` | Stroke thickness |
| `ArcDepthPx` | `3` | Concave endpoint depth |
| `Color` | `"#FF111111"` | Near-black stroke |

Single config drives caps on **both** stub and extension-line tips.

## Files (planned)

| Path | Phase | Change |
|------|-------|--------|
| `Models/DrawnPinTipCapConfig.cs` | 1 | Enum + config |
| `Models/PinTipCapPlacement.cs` | 1 | DTO: `LocationName`, `TipScreen`, `ShaftDir`, `OutlineWidthPx`, `ShaftKind` |
| `Models/PinMarkerConfig.cs` | 1 | `DrawnPinTipCap` property |
| `Utilities/PinTipCapGeometry.cs` | 1 | Pure geometry builders |
| `Views/DrawnPinTipCapRenderer.cs` | 2 | Overlay renderer |
| `MainWindow.xaml` | 2 | `PinTipCapOverlay` canvas |
| `MainWindow.TipCap.partial.cs` | 2–3 | Stub tips (2); extension tips + animation fallback (3) |
| `Views/ExtensionLineRenderer.cs` | 3 | `TryGetLineStart` |
| `Views/IExtensionLineRenderer.cs` | 3 | Interface for `TryGetLineStart` |
| `visual-config.json`, `VISUAL_CONFIG.md` | 1 | Schema + docs |
| `Tests/PinTipCapGeometryTests.cs` | 1–3 | Geometry + direction tests |
| `Tests/VisualConfigServiceTests.cs` | 1 | Config round-trip |
| `CHANGELOG.md` | 5 | `[Unreleased]` |

## Phased tasks

### Phase 0 — preflight

- [ ] Human signs off design table above (especially T-junction on angled extension lines).
- [ ] Confirm model-separation attachment points for stub vs head-only pins.

### Phase 1 — config + shared geometry

- [x] `DrawnPinTipCapConfig`, `PinTipCapPlacement`, `PinTipCapGeometry` (parameterized by `shaftDir`).
- [x] `visual-config.json` (`PinMarkers.DrawnPinTipCap` block added).
- [x] Unit tests: config load + string-enum round-trip, width math. (Concave direction tests landed early — see Phase 4b.)

```powershell
dotnet test Tests\InteractiveWorldMap.Tests.csproj --filter "FullyQualifiedName~PinTipCap|FullyQualifiedName~VisualConfigService"
```

### Phase 2 — built-in stub tips (`Horizontal` only)

- [x] `DrawnPinTipCapRenderer` (renders into `MapDisplay.Markers` at z 1500/1501 — see note below).
- [x] `MainWindow.TipCap.partial.cs`: stub branch — `!HasLine(marker)` && built-in shaft visible.
- [x] Hover `RenderTransform` in tip math (`PinMarker.GetScaledShaftTipPoint`/`GetScaledConnectionPoint`).
- [ ] Smoke with `Style: "Horizontal"` on auto-stub pins (needs GUI — Phase 5).

> **Overlay note:** the cap must interleave by z-index *between* lines (999/1000) and heads (2000), which a separate sibling canvas cannot do. So caps render directly into the marker canvas (`MapDisplay.Markers`) at z 1500/1501 as siblings of the markers — never children of a `PinMarker` — satisfying the real constraint (horizontal in screen space, ignores hover/rotation) without a dedicated `PinTipCapOverlay` canvas.

### Phase 3 — extension-line tips (`Horizontal` only)

- [x] `TryGetLineStart` on extension line renderer (+ interface).
- [x] Extension branch — `HasLine(marker)` → cap at line start; **no** cap on hidden built-in.
- [x] Manual-layout replay, edit-mode drag, and zoom rebuild caps from live line endpoints. `ApplyManualLayout` refreshes after final placement; both drag branches refresh after line/marker mutation; drag-end refresh removes stale paths and restores head layering. (Lines are repositioned, not cleared, while animating — `HasLine` stays true — so the projected-anchor fallback was unnecessary in practice.)
- [x] Minimum length guard: skip when shaft/line length &lt; 8 px (`MinShaftLengthForCapPx`).
- [ ] Smoke with `Style: "Horizontal"` on manual-layout + drag (needs GUI — Phase 5).

### Phase 4 — stroked rendering + polish

- [x] One open, unfilled, near-black path per cap with rounded ends.
- [x] Tunable `WidthPx` and `LineWeightPx` in config and Tuning panel.
- [x] Clear overlay when `Style == None` or `UsePinMarkers == false`.

### Phase 4b — concave geometry + visual iteration

- [x] Implement `Style: "Concave"` as an open quadratic whose midpoint is the shaft tip.
- [x] Unit tests: normal, inverted, diagonal, near-horizontal, and degenerate direction cases.
- [x] Flip the curve when the pin head is below the tip.
- [ ] Tune `WidthPx`, `LineWeightPx`, and `ArcDepthPx` on the [review matrix](#concave-iteration) if needed.
- [ ] Human visual pass on review matrix — loop until acceptable or horizontal-only fallback decided.
- [ ] Extend manual smoke to concave on stub + extension-line cases.

### Phase 5 — verification

```powershell
.\scripts\verify.ps1
```

Manual smoke:

- [ ] `Style: "None"` — no overlay children.
- [ ] **Stub** — horizontal on standalone auto-stub pins (full map + zoomed); concave after Phase 4b passes human review.
- [ ] **Extension line** — cap at the map anchor; concave bows away from the head and flips when the head is below the tip; **no** duplicate cap on hidden built-in.
- [ ] **Edit-mode drag** — cap on visible drag line tip at fixed map location.
- [ ] **Composite** — no drawn-pin caps on composite markers.
- [ ] **Hover** (stub) — cap tracks scaled tip.
- [ ] **Zoom in** — extension-line cap tracks map anchor during animation.

## Acceptance criteria

- [ ] `PinMarkers.DrawnPinTipCap.Style` = `"Horizontal"` (required); `"Concave"` (after Phase 4b human sign-off); default `"None"`.
- [ ] **Visible-shaft rule** — exactly one cap per drawn pin at the terminus of the **visible** shaft: built-in stub **or** extension line, never both, never neither (when style ≠ None).
- [ ] **Hidden built-in does not suppress cap** — when the built-in shaft is hidden and an extension line is visible, the cap appears on the extension-line tip (including edit-mode drag).
- [x] **Alignment invariant** — `ScreenHorizontal` preserves the horizontal cap; `ShaftAligned` uses a shaft-perpendicular width axis and full-vector concavity without relying on a visual transform.
- [x] **Horizontal** — open, unfilled line centered at the tip.
- [x] **Concave-side invariant** (automated) — true midpoint equals the tip; endpoints stay on the head side for normal and inverted placements.
- [ ] **Concave appearance** (human) — near-black stroke reads as a divot where the pin enters the map on the Phase 4b review matrix.
- [x] **Stroke controls** — width and line weight are independently tunable through config and the Tuning panel.
- [x] **Default regression** — `Style: "None"` → empty overlay.
- [x] `CHANGELOG.md` updated; `scripts/verify.ps1` green.

## Tests

| Test | Phase | Proves |
|------|-------|--------|
| `VisualConfigService.Load` + `DrawnPinTipCap` | 1 | Config round-trip |
| Explicit width/weight + legacy fallback | 1 | Config compatibility |
| Concave midpoint, `shaftDir = (0,-1)` | 4b | Normal orientation |
| Concave midpoint, `shaftDir = (0,1)` | 4b | Inverted orientation |
| Diagonal / near-horizontal direction table | 4b | Stable perspective flip |
| Shaft-aligned line/curve direction table | 4b | Perpendicular width axis, full-vector concavity, and invalid-vector fallback |
| Renderer path test | 4 | One unfilled rounded stroke |
| Stub eligibility source guard | 2 | Cap when `!HasLine` + shaft visible |
| Extension eligibility + `TryGetLineStart` | 3 | Cap when `HasLine`; not on hidden built-in |
| `DrawnPinTipCapLifecycleTests` | 3 | Manual replay and drag refresh caps; every eligible drawn head remains above the cap layer |
| `LayerDependencyTests` | 2 | Renderer in `Views/` |

## Open risks

| Risk | Mitigation |
|------|------------|
| T-junction on angled extension lines | Compare `ScreenHorizontal` and `ShaftAligned` during the remaining visual review |
| Model separation mid-work | Phase 0 gate; named migration hooks |
| Zoom animation without line visual | Projected map anchor in Phase 3 |
| Hover scale (stub only) | Include `RenderTransform` |
| Very short shafts / lines | Length &lt; 8 px guard |
| Concave reads wrong on angled / T-junction tips | Phase 4b iteration; geometry isolated in `PinTipCapGeometry`; horizontal-only fallback |
| A cap intersects a neighboring head | Every eligible drawn-pin head is explicitly raised above the cap layer on each cap sync |
| No screenshot CI | Manual smoke checklist; concave notes in `temp/` during iteration |

## Deferred (not in this plan)

- Per-location cap override in `ManualLayoutMarker`.
- Automated screenshot harness.
- Composite pin caps (already exist).

## Naming cheatsheet

| Layer | Name |
|-------|------|
| Config | `DrawnPinTipCap`, `DrawnPinTipCapStyle`, `DrawnPinTipCapAlignment` |
| DTO | `PinTipCapPlacement`, `PinTipCapShaftKind` |
| Geometry | `Utilities/PinTipCapGeometry` |
| Renderer | `Views/DrawnPinTipCapRenderer` |
| Overlay | `PinTipCapOverlay` |
| Composite (avoid) | `TipCapLength`, `ShaftTipCapLayer` |

## Status

Drafted 2026-06-23; revised after agent review; updated for extension-line tips, visible-shaft rule, and concave iteration phasing.

**2026-06-28 — Divot-cap correction implemented.** Human review rejected the filled cap and toward-head curve. Both styles now render as one open, unfilled, near-black stroke. Concave endpoints sit on the head side while the evaluated midpoint stays at the shaft tip, so the curve bows away from the head and flips for inverted manual-layout/dense-cluster placements. `WidthPx`, `LineWeightPx`, and `ArcDepthPx` are available in config and the Tuning panel; legacy fields still load through deterministic fallbacks. Automated config, geometry, renderer, Tuning, and architecture coverage is complete. **Remaining: GUI-only human gates** — normal/inverted concave visual acceptance and the Phase 5 interaction matrix. Default `Style: "None"` remains inert.

**2026-06-28 verification:** `.\scripts\verify.ps1` passes with 449 tests, zero build warnings/errors, seed verification, doc links, taste checks, and headless startup. Computer Use connected, but launching the WPF executable required an app approval that timed out twice; no live screenshot was captured. The config was restored to `Style: "None"` with no residual diff.

**2026-06-28 — stale-cap and layering safeguards implemented.** Investigation found that pooled cap paths refreshed only during automatic marker placement, not after manual-layout replay or drag mutations, and that ordinary/post-drag drawn heads could sit below the cap layer. `ApplyManualLayout`, both drag branches, and drag-end now refresh caps after their final mutations. Every eligible drawn-pin head is explicitly placed above the cap layer during sync. `DrawnPinTipCapLifecycleTests` guards both invariants. The reported Japan/China occurrence is no longer reproducible and is deferred unless observed again. `.\scripts\verify.ps1` passes with 453 tests, zero build warnings/errors, seed verification, doc links, taste checks, and headless startup.

**2026-06-28 — selectable cap alignment implemented.** `DrawnPinTipCap.Alignment` now supports compatibility-default `ScreenHorizontal` and full-vector `ShaftAligned` geometry. Shaft-aligned straight caps run perpendicular to the visible shaft; concave caps place their endpoints toward the head while keeping the quadratic midpoint at the map tip, including diagonal, horizontal, inverted, and invalid-vector fallback cases. Config and Runtime Tuning expose both modes, while checked-in `visual-config.json` selects `ShaftAligned` for evaluation and leaves `Style: "None"`. Automated config, geometry, lifecycle, and Tuning coverage is complete. Remaining: manual visual comparison and interaction smoke.

**2026-06-28 alignment verification:** `.\scripts\verify.ps1` passes with 466 tests, zero build warnings/errors, seed verification, doc links, taste checks, and headless startup.
