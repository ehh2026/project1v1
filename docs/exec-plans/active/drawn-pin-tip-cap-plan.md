---
status: active
owner: agent
started: 2026-06-23
needs_review: true
requirements_ref: drawn-stub-tip-cap
reviewed_by: temp/review-drawn-pin-tip-cap-plan-2026-06-23.md
parent_program: docs/exec-plans/active/composite-pins-program.md
depends_on: docs/exec-plans/active/drawn-pin-model-separation-plan.md
---

# ⚠️ NEEDS REVIEW — Drawn stub pin tip cap

**This plan still needs human review.** It was authored from a TO_DO bullet and revised in response to [temp/review-drawn-pin-tip-cap-plan-2026-06-23.md](../../../temp/review-drawn-pin-tip-cap-plan-2026-06-23.md). **Do not start implementation** until a human confirms:

| # | Decision | Options / default proposal |
|---|----------|---------------------------|
| 1 | **v1 shape** | Ship **both** `Horizontal` and `Concave` behind one enum, or pick one style for v1 only. |
| 2 | **v1 scope** | **Auto-stub drawn pins only** (proposal below). Extension-line tips deferred to v2. |
| 3 | **Outline pairing default** | Proposal: `UseOutlineRing = true` by default so the cap matches the shaft's outline + core look. |
| 4 | **Edit-mode behavior** | Proposal: cap hidden whenever the built-in shaft is hidden (drag **and** any pin with an extension line). |
| 5 | **Prerequisite** | [drawn-pin-model-separation-plan.md](drawn-pin-model-separation-plan.md) has landed (cap on `AutoStubPinMarker`) **or** is explicitly deferred (cap on monolithic `PinMarker` with a named migration hook). |

---

# Drawn stub pin tip cap

> TO_DO source: [../../TO_DO.md](../../TO_DO.md) — *Composite pins & manual layouts* → "Add a horizontal or concave line at the drawn pin tips"

## Goal

Add an opt-in cap (horizontal bar **or** shallow screen-space concave arc) at the visible tip of the **auto-stub drawn** pin shaft, so the terminus reads as deliberate rather than a flat `Rectangle` end. The cap is **always horizontal in screen space** — independent of any future pin rotation or local transform.

The concave variant's curve opens **toward the shaft**: for a vertical auto-stub shaft (the only v1 case) the arc is concave-up; the visual reads as the curve bowing away from the pin body. The cap's outline + core mirrors the shaft's existing two-layer styling when `UseOutlineRing` is enabled.

## Scope (v1)

| Pin role | Cap in v1? | Notes |
|----------|-----------|-------|
| **Auto-stub drawn** (built-in `PinShaft` visible, no extension line) | ✅ Yes | Primary target. Tip = map-projected screen position of `GetShaftTipPoint()` (`Views/PinMarker.xaml.cs:181-184`). |
| **Radial-extension / manual-layout drawn** (`ExtensionLineRenderer.HasLine(marker)` → built-in shaft hidden) | ❌ No (v2) | Visible shaft is `CreatePinLinePair` (`Views/ExtensionLineRenderer.cs:369`). Map anchor is `OriginalPosition`; a horizontal cap there meets a tilted line as a **T junction** — needs its own v2 plan and human sign-off. |
| **Composite pin** (`ShaftTipCapLayer` in `CompositePinRenderPlanBuilder.cs:307`) | ❌ No | Asset-based tip cap already exists. |
| **Edit-mode drag** (built-in shaft hidden, temporary extension line) | ❌ Hidden | Same rule as manual-layout: no cap when the built-in shaft is not the visible shaft. |

### Auto-stub eligibility (implementation filter)

A marker gets a cap when **all** of:

1. `marker.Content is PinMarker` (or post-split `AutoStubPinMarker`),
2. built-in shaft is visible (`ShaftHost.Visibility == Visible` — expose as `IsBuiltInShaftVisible` if needed),
3. `!_extensionLineRenderer.HasLine(marker)` (extended / manual-layout pins use the external line),
4. `DrawnStubTipCap.Style != None` in config,
5. marker is visible on canvas.

`MainWindow` evaluates this filter and passes screen-space tip points to the renderer; the renderer does **not** call `ExtensionLineRenderer` directly.

## Background

Current shaft from `Views/PinMarker.xaml:13-22`:

```xaml
<Grid x:Name="ShaftHost" VerticalAlignment="Top" HorizontalAlignment="Center">
    <Rectangle x:Name="PinShaftOutline" .../>
    <Rectangle x:Name="PinShaft" .../>
</Grid>
```

The shaft is a flat `Rectangle` with a wider, darker `PinShaftOutline` behind it. The bottom edge is flush with the shaft body — the motivation behind the TO_DO bullet.

Related code (verified):

| Area | Location |
|------|----------|
| Shaft dimensions / colors | `Views/PinMarker.xaml.cs:78` — `ApplyPinDimensions(PinMarkerConfig)` |
| Hide built-in shaft | `Views/PinMarker.xaml.cs:124` — `SetShaftVisible(bool)` |
| Canonical tip (marker-local) | `Views/PinMarker.xaml.cs:181` — `GetShaftTipPoint()`; used by `MainWindow.CompositePins.partial.cs:92`, `MainWindow.Navigation.partial.cs:85` |
| Placement apply path | `MainWindow.xaml.cs:449` — `UpdateMarkerPositions()` → `ApplyIndividualPlacements` (`:503`) |
| Config load | `Services/VisualConfigService.cs:10` — `Load(filePath)` deserializes root `visual-config.json` → `VisualConfig.PinMarkers` (`Models/VisualConfig.cs`) |
| Config docs | `docs/guides/VISUAL_CONFIG.md` |
| Composite naming collision | `Models/PinPartGeometry.cs:44` (`TipCapLength`), `CompositePinRenderPlan.cs:34` (`ShaftTipCapLayer`) — **do not reuse** |

## Architecture constraints

1. **Layering** — `Tests/Architecture/LayerDependencyTests.cs` forbids `Services` → `Views`. Cap **renderer** lives in `Views/` (like `ExtensionLineRenderer.cs`); pure math in `Utilities/`; placement DTO in `Models/`. `MainWindow` orchestrates both.
2. **No overlay inside `PinMarker`** — `PinMarker` cannot add siblings to the map canvas. Overlay is a `Canvas` in `MainWindow.xaml`, sibling to existing map content.
3. **Sequence** — Prefer implementing on `AutoStubPinMarker` after [drawn-pin-model-separation-plan.md](drawn-pin-model-separation-plan.md). If separation is deferred, attach to monolithic `PinMarker` and document the one-line migration (move cap hook to `AutoStubPinMarker`).
4. **No visual regression harness** — only `Tests/StartupValidationHarnessTests.cs` + `scripts/validate_startup.ps1`. v1 visual check is **manual smoke** (checklist below).
5. **Naming** — config/type prefix `DrawnStubTipCap` / `PinStubTipCap*`; never `TipCapLength` / `ShaftTipCapLayer` (composite).

## Modularity / file-size impact

| File | Expected growth |
|------|-------------------|
| `MainWindow.xaml` | +1 overlay `Canvas` element |
| `MainWindow.TipCap.partial.cs` (new, ~80–120 lines) | Filter eligible markers; compute screen tip; call renderer after `ApplyIndividualPlacements` |
| `Views/DrawnStubTipCapRenderer.cs` (new, ~150–250 lines) | Owns overlay children; pools `Path`/`Rectangle` visuals |
| `Utilities/PinStubTipCapGeometry.cs` (new, ~80–120 lines) | Pure geometry builders |
| `Models/DrawnStubTipCapConfig.cs` + `StubTipCapPlacement.cs` (new, small) | Config + placement DTO |
| `Models/PinMarkerConfig.cs` | +1 property (~5 lines) |
| `MainWindow.xaml.cs` | No growth if logic stays in partial |

Keep each new `.cs` file under 500 lines. No `ManualLayoutMarker` schema change in v1 (global config only).

## Cap geometry

### Screen tip position

For each eligible marker:

```
tipScreen = markerTopLeft + pin.GetShaftTipPoint()   // marker-local → canvas
```

Also apply `PinMarker`'s `RenderTransform` (hover `ScaleTransform` on `PinTransform`) when computing `tipScreen`, or refresh caps when hover animation runs (`IsHovered` / `LayoutUpdated`). **v1 pins are not rotated**, but hover scale (1.0 → 1.15) moves the tip ~1–2 px; caps must track it or they will drift on hover.

### Anchor rule (both variants)

- **Baseline** at `tipY` = bottom edge of the built-in shaft outline (same Y as `GetShaftTipPoint().Y` before screen transform).
- **Horizontal**: filled rect from `(tipX - halfWidth, tipY)` with `width = shaftOutlineWidth + 2 * ExtendPx`, `height = HeightPx`, extending **downward** (+Y). Drawn **on top** of the shaft bottom pixels (no geometric clip in v1).
- **Concave**: closed `PathFigure` (`IsClosed = true`, `FillRule = Nonzero`):
  1. `Move` to `(tipX - halfWidth, tipY)` (left baseline),
  2. `Line` to `(tipX + halfWidth, tipY)` (right baseline),
  3. `QuadraticBezier` to start, **control** `(tipX, tipY - ArcDepthPx)` (above baseline → arc bows toward shaft).

On-curve midpoint at `t = 0.5`:

```
midY = tipY - ArcDepthPx / 2
```

### Concave direction test (unit test)

Vector `v = midPoint - baselineCenter` has negative Y (midpoint above baseline in WPF coords). Shaft direction from tip toward head is `shaftUp = (0, -1)`. Assert `dot(v, shaftUp) > 0` (both point upward). **Not** `< 0`.

### Outline ring

When `UseOutlineRing` is true, stroke the same path/rect with `ShaftOutlineColor`, `StrokeThickness = ShaftOutlineThickness`, drawn **behind** the core fill (two elements or `Pen` + `Fill` on one `Path`).

### Z-order and hit-testing

- `Panel.SetZIndex(capVisual, 1500)` — above extension lines (999/1000), below extended marker heads (2000 per `AnchorExtendedMarker`).
- `IsHitTestVisible = false` on all cap visuals.

### Size units and refresh cadence

- Cap dimensions are **screen pixels** (match `ApplyPinDimensions` shaft sizes, not map zoom scale).
- Rebuild cap geometry when: placement apply runs (`UpdateMarkerPositions`), config/tuning changes shaft size, hover transform changes tip position.
- Pool and reuse `Path` / `Rectangle` instances; update `Data` / `Width` / `Canvas.SetLeft` in place. No new overlay children per frame when the eligible set is unchanged.
- `FileLogger` at `Debug` only in the refresh path.

## Config schema

Nested under `PinMarkers` in `visual-config.json` (PascalCase, matching existing keys like `ShaftWidth`):

```json
"PinMarkers": {
  "ShaftWidth": 3.0,
  "DrawnStubTipCap": {
    "Style": "None",
    "HeightPx": 6.0,
    "ArcDepthPx": 3.0,
    "ExtendPx": 0.0,
    "Color": null,
    "UseOutlineRing": true
  }
}
```

| Field | Type | Default | Notes |
|-------|------|---------|-------|
| `Style` | enum string | `"None"` | `"None"` \| `"Horizontal"` \| `"Concave"` (PascalCase, same pattern as `PinParts.SelectionMode: "NearestFit"`) |
| `HeightPx` | double | `6` | Horizontal bar height |
| `ArcDepthPx` | double | `3` | Concave control-point offset (concave only) |
| `ExtendPx` | double | `0` | Extra half-width beyond `PinShaftOutline.Width` |
| `Color` | string? | `null` | ARGB hex; `null` → `ShaftColor` core fill |
| `UseOutlineRing` | bool | `true` | Outline stroke matching shaft halo |

C# model: `PinMarkerConfig.DrawnStubTipCap` → `DrawnStubTipCapConfig` in `Models/`.

## Files (planned)

| Path | Change |
|------|--------|
| `Models/DrawnStubTipCapConfig.cs` | New enum `DrawnStubTipCapStyle` + config class |
| `Models/StubTipCapPlacement.cs` | New DTO: `LocationName`, `TipScreen`, `ShaftOutlineWidthPx`, style snapshot |
| `Models/PinMarkerConfig.cs` | `DrawnStubTipCap` property |
| `visual-config.json` | `PinMarkers.DrawnStubTipCap` block, default `Style: "None"` |
| `docs/guides/VISUAL_CONFIG.md` | Document fields |
| `Utilities/PinStubTipCapGeometry.cs` | Pure `Geometry` builders from tip + dimensions + config |
| `Views/DrawnStubTipCapRenderer.cs` | Overlay child management; `Sync(IReadOnlyList<StubTipCapPlacement>, DrawnStubTipCapConfig, PinMarkerConfig)` |
| `MainWindow.xaml` | Add `Canvas x:Name="StubTipCapOverlay"` above map markers, below popup layer (confirm z-order in smoke) |
| `MainWindow.TipCap.partial.cs` | Filter + screen-tip math + call renderer after placements |
| `MainWindow.xaml.cs` | Construct renderer; call `RefreshStubTipCaps()` at end of `UpdateMarkerPositions()` |
| `Views/PinMarker.xaml.cs` | Optional `IsBuiltInShaftVisible` property |
| `Tests/PinStubTipCapGeometryTests.cs` | Pure geometry + config defaults |
| `Tests/VisualConfigServiceTests.cs` | +2 tests for `DrawnStubTipCap` load / default |
| `CHANGELOG.md` | `[Unreleased]` entry |

## Phased tasks

### Phase 0 — preflight

- [ ] Confirm model-separation status; pick `PinMarker` vs `AutoStubPinMarker` attachment point.
- [ ] Human signs off design table at top of this plan.

### Phase 1 — config + geometry

- [ ] Add `DrawnStubTipCapConfig` + `PinMarkerConfig.DrawnStubTipCap`.
- [ ] Update `visual-config.json` + `VISUAL_CONFIG.md`.
- [ ] Implement `Utilities/PinStubTipCapGeometry.cs`.
- [ ] Tests: `PinStubTipCapGeometryTests`, `VisualConfigServiceTests` round-trip.

```powershell
dotnet test Tests\InteractiveWorldMap.Tests.csproj --filter "FullyQualifiedName~PinStubTipCap|FullyQualifiedName~VisualConfigService"
```

### Phase 2 — renderer + overlay

- [ ] Add `StubTipCapOverlay` to `MainWindow.xaml`.
- [ ] Implement `DrawnStubTipCapRenderer`.
- [ ] Wire `MainWindow.TipCap.partial.cs`: after `ApplyIndividualPlacements`, compute placements and call `Sync`.
- [ ] Include hover-transform in screen-tip math (or refresh on `IsHovered` change).

### Phase 3 — outline pairing + polish

- [ ] `UseOutlineRing` core + outline draw order.
- [ ] Minimum shaft length guard: skip cap when `ShaftLength < 8` px (configurable constant).
- [ ] Clear overlay when `Style == None` or `UsePinMarkers == false`.

### Phase 4 — verification

```powershell
.\scripts\verify.ps1
```

Manual smoke checklist:

- [ ] `Style: "None"` — no caps; pixels match pre-change baseline (eyeball).
- [ ] `Style: "Horizontal"` — caps on standalone auto-stub pins at full map and zoomed cluster.
- [ ] `Style: "Concave"` — same; arc bows toward shaft.
- [ ] Manual-layout pin (extension line visible) — **no** cap at map anchor.
- [ ] Composite mode — **no** cap on composite markers.
- [ ] Edit-mode drag — **no** cap on dragged pin.
- [ ] Hover pin — cap stays aligned with shaft tip during scale animation.

## Acceptance criteria

- [ ] `visual-config.json` accepts `PinMarkers.DrawnStubTipCap.Style` = `"Horizontal"` or `"Concave"`; default `"None"`.
- [ ] **Auto-stub only** — cap on eligible pins per filter above; not on extension-line, composite, or drag-hidden shafts.
- [ ] **Screen-space invariant** — cap is a horizontal screen-space shape on `StubTipCapOverlay`; `RenderTransform` is `Identity`; not a child of any pin visual tree.
- [ ] **Concave-side invariant** — on-curve midpoint at `t = 0.5` is on the shaft side of the baseline (`dot(mid - baselineCenter, shaftUp) > 0`).
- [ ] **Outline pairing** — when `UseOutlineRing` is true, outline width/color match `ShaftOutlineThickness` / `ShaftOutlineColor`.
- [ ] **Default regression** — `Style: "None"` → `StubTipCapOverlay.Children.Count == 0` after placement refresh.
- [ ] **No per-frame allocation** when eligible set and tip positions are unchanged between placement ticks.
- [ ] `CHANGELOG.md` updated; `scripts/verify.ps1` green.

## Tests

| Test | What it proves |
|------|----------------|
| `VisualConfigService.Load` with `DrawnStubTipCap` JSON | Config round-trip (use `VisualConfigService`, not a nonexistent `LoadFromFile`) |
| `PinStubTipCapGeometry` width table | `ExtendPx` math vs `shaftOutlineWidth` |
| Concave `midY == tipY - ArcDepthPx / 2` | Correct Bezier evaluation |
| Concave dot product `> 0` | Arc faces shaft |
| `Style == None` → empty overlay | Zero-regression guard (renderer unit test or source inspection) |
| `LayerDependencyTests` | Renderer in `Views/`, geometry in `Utilities/` |

**Out of scope for automated tests:** rendered pixel appearance, hover alignment, zoom animation smoothness — manual smoke only.

## Open risks

| Risk | Mitigation |
|------|------------|
| Model separation lands mid-implementation | Phase 0 gate; migrate hook to `AutoStubPinMarker` |
| Hover scale drift | Include `RenderTransform` in tip math or refresh on hover |
| Concave on very short shafts | `ShaftLength < 8` guard |
| No screenshot CI | Manual smoke checklist; optional PinDebugger preview later |
| T-junction on v2 extension-line caps | Separate v2 plan; human sign-off required |
| Per-frame work during zoom animation | Acceptable if tied to placement ticks only; pool geometries |

## v2 (deferred, not in this plan)

- Cap at extension-line map anchor for manual-layout / radial-extension pins.
- Optional per-location cap override in `ManualLayoutMarker` (only if product requests).
- Tuning-panel controls for cap style.
- Automated screenshot harness.

## Naming cheatsheet

| Layer | Name |
|-------|------|
| Config | `DrawnStubTipCap`, `DrawnStubTipCapStyle` |
| DTO | `StubTipCapPlacement` |
| Geometry | `Utilities/PinStubTipCapGeometry` |
| Renderer | `Views/DrawnStubTipCapRenderer` |
| Overlay | `StubTipCapOverlay` (`MainWindow.xaml`) |
| Composite (do not reuse) | `TipCapLength`, `ShaftTipCapLayer` |

## Status

Drafted 2026-06-23; revised 2026-06-23 after agent review. **Not started — awaiting human sign-off on design table above.**
