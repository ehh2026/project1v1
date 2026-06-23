---
status: active
owner: agent
started: 2026-06-23
needs_review: false
requirements_ref: drawn-pin-tip-cap
reviewed_by: temp/review-drawn-pin-tip-cap-plan-2026-06-23.md
parent_program: docs/exec-plans/active/composite-pins-program.md
depends_on: docs/exec-plans/active/drawn-pin-model-separation-plan.md
---

# Drawn pin tip cap — design confirmed

**Human-confirmed 2026-06-23.** Authored from a TO_DO bullet, revised in response to [temp/review-drawn-pin-tip-cap-plan-2026-06-23.md](../../../temp/review-drawn-pin-tip-cap-plan-2026-06-23.md), and the intent ("the cap makes the pin look stuck into the map") confirmed. Implementation may begin per the phasing below; **concave is still allowed to iterate** (Phase 4b) and horizontal may ship before concave is finalized.

| # | Decision | Confirmed |
|---|----------|-----------|
| 1 | **Shape phasing** | Ship **`Horizontal` first** (Phases 2–3). Add **`Concave` behind the same enum** once horizontal is verified on stub + extension-line tips. Expect **visual iteration** on concavity before calling concave done (see [Concave iteration](#concave-iteration)). |
| 2 | **T-junction look** | Extension-line caps are horizontal in screen space at the map anchor while the shaft is angled — the resulting **T junction** is the intended screen-space invariant. |
| 3 | **Outline pairing default** | `UseOutlineRing = true` so the cap matches the shaft outline + core. |
| 4 | **Phasing** | Phase 2 = stub tips (`Horizontal`); Phase 3 = extension-line tips (`Horizontal`); Phase 4 = outline polish; **Phase 4b** = concave geometry + human visual pass (may loop). |
| 5 | **Prerequisite** | [drawn-pin-model-separation-plan.md](drawn-pin-model-separation-plan.md) has landed **or** is explicitly deferred (cap hooks on monolithic `PinMarker` with a named migration path). |

---

# Drawn pin tip cap

> TO_DO source: [../../TO_DO.md](../../TO_DO.md) — *Composite pins & manual layouts* → "Add a horizontal or concave line at the drawn pin tips"

## Goal

Add an opt-in cap (horizontal bar **or** shallow screen-space concave arc) at the **visible terminus of every drawn pin shaft** — built-in auto-stub **or** extension line — so the tip reads as the pin being **stuck into the map surface** rather than a flat cut end resting on top of it.

**Intent (drives every visual decision below):** the cap depicts the point where the pin *enters the map*. The shape should suggest penetration into the surface — the map material parting/puckering around the shaft at the entry — not a pin floating above a flat line. This is why the **concave** variant bows **toward the shaft/head** (the surface lifts around the entry), and why the horizontal variant sits **at** the tip rather than below it. Phase 4b's human review judges concave against this "stuck-in" read, not generic prettiness.

**Core rule — cap follows the visible shaft, not the hidden one:**

| Visible shaft | Cap location | Built-in shaft hidden? |
|---------------|--------------|------------------------|
| Built-in `PinShaft` (auto-stub) | `GetShaftTipPoint()` projected to screen | No |
| Extension line (`ExtensionLineRenderer` line pair) | Line **start** = map anchor (`X1`, `Y1`) | Yes — **no** cap on the hidden built-in; cap **on** the extension line tip |
| Edit-mode drag (temporary extension line) | Same as extension line — map anchor | Yes — cap stays on the **visible** drag line tip |
| Neither shaft visible | No cap | — |

The cap is **always horizontal in screen space** (along screen +x). The **horizontal** variant is the baseline shape and should be stable after Phases 2–4. The **concave** variant is a first-pass quadratic Bezier that bows **toward the shaft/head** so the map surface appears to pucker up around the pin's entry point — the "stuck-in" read from the Goal. Its direction is specified and unit-tested, but **the exact curve is expected to need visual iteration** (depth, width, outline pairing) before we treat concave as shippable. Outline + core pairing mirrors `PinShaftOutline` / `PinShaft` and extension-line outline/core when `UseOutlineRing` is enabled.

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

Apply `PinTransform` (hover scale). Width from `PinShaftOutline.Width + 2 * ExtendPx`.

### Extension-line tip (Phase 3)

```
tipScreen = lineStart   // (X1, Y1) — map anchor
shaftDir  = normalize(lineEnd - lineStart)   // toward head
```

Width from extension line outline thickness: `coreWidth + 2 * outlineExtra` (same math as `CreatePinLinePair`). During zoom animation without a line visual, `tipScreen` = projected map point from placement / offset cache.

### Anchor rule (both shaft kinds, both cap styles)

- **Baseline** — horizontal through `tipScreen`, along screen +x.
- **Horizontal** — rect from `(tipX - halfWidth, tipY)`, width = outline width + `2 * ExtendPx`, height = `HeightPx`, extending **downward** (+Y). Drawn on top of shaft terminus (no geometric clip).
- **Concave** — closed `PathFigure` (`IsClosed = true`, `FillRule = Nonzero`):
  1. `Move` to `(tipX - halfWidth, tipY)`,
  2. `Line` to `(tipX + halfWidth, tipY)`,
  3. `QuadraticBezier` to start; **control** = `tipScreen + shaftDir * ArcDepthPx` (i.e. `ArcDepthPx` **toward** the shaft / head along `shaftDir`, so the arc bows up around the entry point — the "stuck-in" read).

For a vertical stub, `shaftDir = (0, -1)` → control at `(tipX, tipY - ArcDepthPx)`, lifting the curve toward the head.

> **Sign note:** `shaftDir` points *toward the head*, so the multiplier is **positive** `+ArcDepthPx`. A negative sign would bow the arc *away* from the shaft (a crater below the tip), which reads as the pin sitting on a dished surface rather than stuck into it — and would also fail the direction test below.

On-curve midpoint at `t = 0.5` for symmetric horizontal baseline:

```
midPoint = tipScreen + shaftDir * (ArcDepthPx / 2)   // component along shaft only when baseline is horizontal and symmetric
```

### Concave direction test

Assert `dot(midPoint - baselineCenter, shaftDir) > 0` (midpoint lies toward the shaft from the baseline). Stub tests use `shaftDir = (0, -1)`; extension-line tests use a tilted `shaftDir`.

**Unit tests prove direction only, not aesthetics.** Whether the arc looks good on stub vs extension-line vs shallow angle is a **human visual gate** in Phase 4b.

### Concave iteration

The concave cap is the highest-risk visual piece. Plan for **one or more tune-and-review loops** after horizontal is working:

| Principle | Implementation |
|-----------|----------------|
| **Isolate curve math** | All concave path construction lives in `Utilities/PinTipCapGeometry` behind a single entry point (e.g. `BuildConcaveGeometry(...)`). Swapping quadratic → cubic Bezier, circular arc, or adjusted control-point formula must not require renderer changes. |
| **Tune via config first** | Primary knob: `ArcDepthPx`. Secondary: `ExtendPx`, `HeightPx`, `UseOutlineRing`. Iterate in `visual-config.json` (and Tuning panel later if useful) before changing code. |
| **Horizontal before concave** | Phases 2–3 ship and smoke-test `Style: "Horizontal"` only. Concave is Phase 4b — do not block stub/extension plumbing on concave look. |
| **Review matrix** | Human eyeball on concave at: vertical stub (full map + zoomed), shallow-angle extension line, steep-angle extension line, with and without outline ring. Capture notes or screenshots in a scratch file under `temp/` (not committed unless human asks). |
| **Acceptable code churn** | If the first Bezier formula reads as too flat, too deep, or wrong on T-junctions, revise `PinTipCapGeometry` and re-run smoke — no need to reopen overlay/placement architecture. |
| **Optional follow-up knobs** | If `ArcDepthPx` alone is insufficient after iteration, add config fields (e.g. `ArcControlBias`, separate stub vs extension depth) in a small follow-up — avoid preemptive over-configuration. |

**Phase 4b exit:** human confirms concave reads as **the pin stuck into the map** (surface puckering around the entry, not a glitch or a dished crater) on the review matrix above. If concave cannot be made to read that way, ship **horizontal only** and leave `Concave` in the enum as experimental or remove it — human decides at review time.

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
    "HeightPx": 6.0,
    "ArcDepthPx": 3.0,
    "ExtendPx": 0.0,
    "Color": null,
    "UseOutlineRing": true
  }
}
```

| Field | Default | Notes |
|-------|---------|-------|
| `Style` | `"None"` | `"None"` \| `"Horizontal"` \| `"Concave"` (PascalCase) |
| `HeightPx` | `6` | Horizontal bar height |
| `ArcDepthPx` | `3` | Concave offset along `shaftDir`; primary visual tuning knob — expect iteration |
| `ExtendPx` | `0` | Extra half-width beyond shaft outline |
| `Color` | `null` | `null` → `ShaftColor` |
| `UseOutlineRing` | `true` | Matches shaft / line outline |

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

- [ ] `DrawnPinTipCapConfig`, `PinTipCapPlacement`, `PinTipCapGeometry` (parameterized by `shaftDir`).
- [ ] `visual-config.json` + `VISUAL_CONFIG.md`.
- [ ] Unit tests: config load, width math (concave direction tests deferred to Phase 4b).

```powershell
dotnet test Tests\InteractiveWorldMap.Tests.csproj --filter "FullyQualifiedName~PinTipCap|FullyQualifiedName~VisualConfigService"
```

### Phase 2 — built-in stub tips (`Horizontal` only)

- [ ] `DrawnPinTipCapRenderer` + `PinTipCapOverlay`.
- [ ] `MainWindow.TipCap.partial.cs`: stub branch — `!HasLine(marker)` && built-in shaft visible.
- [ ] Hover `RenderTransform` in tip math.
- [ ] Smoke with `Style: "Horizontal"` on auto-stub pins before Phase 3.

### Phase 3 — extension-line tips (`Horizontal` only)

- [ ] `TryGetLineStart` on extension line renderer.
- [ ] Extension branch — `HasLine(marker)` → cap at line start; **no** cap on hidden built-in.
- [ ] Edit-mode drag: cap on temporary extension line at map anchor.
- [ ] Zoom-animation fallback: projected map anchor when line visuals suppressed.
- [ ] Minimum length guard: skip when line length or `ShaftLength` &lt; 8 px.
- [ ] Smoke with `Style: "Horizontal"` on manual-layout + drag before Phase 4.

### Phase 4 — outline pairing + polish (`Horizontal`)

- [ ] `UseOutlineRing` draw order (outline behind core).
- [ ] Clear overlay when `Style == None` or `UsePinMarkers == false`.

### Phase 4b — concave geometry + visual iteration

- [ ] Implement `Style: "Concave"` in `PinTipCapGeometry` (quadratic Bezier v1).
- [ ] Unit tests: direction invariant only (see [Concave direction test](#concave-direction-test)).
- [ ] Tune `ArcDepthPx` / outline on the [review matrix](#concave-iteration); revise geometry helper if needed.
- [ ] Human visual pass on review matrix — loop until acceptable or horizontal-only fallback decided.
- [ ] Extend manual smoke to concave on stub + extension-line cases.

### Phase 5 — verification

```powershell
.\scripts\verify.ps1
```

Manual smoke:

- [ ] `Style: "None"` — no overlay children.
- [ ] **Stub** — horizontal on standalone auto-stub pins (full map + zoomed); concave after Phase 4b passes human review.
- [ ] **Extension line** — horizontal at map anchor on manual-layout pin; concave bows toward angled shaft after Phase 4b; **no** duplicate cap on hidden built-in.
- [ ] **Edit-mode drag** — cap on visible drag line tip at fixed map location.
- [ ] **Composite** — no drawn-pin caps on composite markers.
- [ ] **Hover** (stub) — cap tracks scaled tip.
- [ ] **Zoom in** — extension-line cap tracks map anchor during animation.

## Acceptance criteria

- [ ] `PinMarkers.DrawnPinTipCap.Style` = `"Horizontal"` (required); `"Concave"` (after Phase 4b human sign-off); default `"None"`.
- [ ] **Visible-shaft rule** — exactly one cap per drawn pin at the terminus of the **visible** shaft: built-in stub **or** extension line, never both, never neither (when style ≠ None).
- [ ] **Hidden built-in does not suppress cap** — when the built-in shaft is hidden and an extension line is visible, the cap appears on the extension-line tip (including edit-mode drag).
- [ ] **Screen-space invariant** — cap is horizontal in screen space on `PinTipCapOverlay`; `RenderTransform` is `Identity`; not a child of pin visual tree.
- [ ] **Horizontal** — firm bar at tip on stub and extension-line cases (Phases 2–4).
- [ ] **Concave-side invariant** (automated) — `dot(midPoint - baselineCenter, shaftDir) > 0` for stub and extension-line cases.
- [ ] **Concave appearance** (human) — reads as the pin **stuck into the map** (arc bows toward shaft) on the Phase 4b review matrix, or horizontal-only fallback documented in CHANGELOG.
- [ ] **Outline pairing** — `UseOutlineRing` matches shaft / line outline thickness and color.
- [ ] **Default regression** — `Style: "None"` → empty overlay.
- [ ] `CHANGELOG.md` updated; `scripts/verify.ps1` green.

## Tests

| Test | Phase | Proves |
|------|-------|--------|
| `VisualConfigService.Load` + `DrawnPinTipCap` | 1 | Config round-trip |
| Width / `ExtendPx` table | 1 | Outline width math |
| Concave midpoint, `shaftDir = (0,-1)` | 4b | Vertical stub — direction only |
| Concave midpoint, tilted `shaftDir` | 4b | Extension-line — direction only |
| `dot(mid - baseline, shaftDir) > 0` | 4b | Concave faces shaft (not “looks good”) |
| Stub eligibility source guard | 2 | Cap when `!HasLine` + shaft visible |
| Extension eligibility + `TryGetLineStart` | 3 | Cap when `HasLine`; not on hidden built-in |
| `LayerDependencyTests` | 2 | Renderer in `Views/` |

## Open risks

| Risk | Mitigation |
|------|------------|
| T-junction on angled extension lines | Accepted design per human sign-off; screen-space horizontal cap is intentional |
| Model separation mid-work | Phase 0 gate; named migration hooks |
| Zoom animation without line visual | Projected map anchor in Phase 3 |
| Hover scale (stub only) | Include `RenderTransform` |
| Very short shafts / lines | Length &lt; 8 px guard |
| Concave reads wrong on angled / T-junction tips | Phase 4b iteration; geometry isolated in `PinTipCapGeometry`; horizontal-only fallback |
| No screenshot CI | Manual smoke checklist; concave notes in `temp/` during iteration |

## Deferred (not in this plan)

- Per-location cap override in `ManualLayoutMarker`.
- Tuning-panel controls for cap style.
- Automated screenshot harness.
- Composite pin caps (already exist).

## Naming cheatsheet

| Layer | Name |
|-------|------|
| Config | `DrawnPinTipCap`, `DrawnPinTipCapStyle` |
| DTO | `PinTipCapPlacement`, `PinTipCapShaftKind` |
| Geometry | `Utilities/PinTipCapGeometry` |
| Renderer | `Views/DrawnPinTipCapRenderer` |
| Overlay | `PinTipCapOverlay` |
| Composite (avoid) | `TipCapLength`, `ShaftTipCapLayer` |

## Status

Drafted 2026-06-23; revised after agent review; updated for extension-line tips, visible-shaft rule, and concave iteration phasing. **2026-06-23: anchored the design to the stated intent — the cap depicts the pin stuck into the map surface — and corrected the concave control-point sign (`+ArcDepthPx` toward the shaft) so the geometry, prose, and direction test agree. Design confirmed by human; `needs_review` cleared.** **Not started — ready to implement.**
