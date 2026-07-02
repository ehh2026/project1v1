---
status: active
owner: agent
started: 2026-06-23
requirements_ref: zoom-performance-appearance
---

# Zoom Performance & Appearance Fixes

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make zoom in/out smooth and fast, preserve fine map-image detail at rest and in motion, and tighten marker appearance by removing per-frame overhead and correcting rendering policy in the animation and display hot paths.

**Source review:** [performance-appearance-review.md](../../performance-appearance-review.md) — findings, file:line references, and reasoning behind each item.

**Sequencing:** Phase 0 (baseline) → Phase 1 (ship & merge independently; low risk) → resolve [Open decisions](#open-decisions) → Phase 2. Phase 1 is independently valuable; do not gate it on Phase 2.

**Verification caveat:** Most items touch the animation loop and marker placement, which are not unit-testable here and need on-device visual verification. Change one item at a time and eyeball a full zoom in/out cycle after each. Run `scripts/verify.ps1` before merging each phase.

---

## Phase 0: Baseline measurement (prep — do before any change)

Establish objective before/after numbers so we can prove the changes helped rather than eyeballing. Uses existing instrumentation; no code change.

- [ ] **Define a fixed, repeatable scenario.** Pick one specific dense cluster (record its name/center) and a script: launch → zoom into it → zoom out → repeat 3×. Use the same window size each run (full-screen maximized, as shipped).
- [ ] **Capture the metric.** From `%AppData%/InteractiveWorldMap/logs/app.log`, read the `[FRAMES TOTAL] N frames in Xms` line (`MainWindow.Navigation.partial.cs:487`) and the per-frame `delta=` values (`:476-480`) for each zoom. Record, per run: **frames rendered** (higher = smoother, since total time is pinned to `AnimationDurationMs`) and **max frame delta** (the worst hitch). Also note the pre-zoom stall (gap between the click log and the first `[FRAME 1]`).
- [ ] **Record the numbers** in this plan or a scratch note so Phase 1/2 exits can compare against them.
- [ ] *Observer-effect note:* the `[FRAME]` logs themselves go through the synchronous logger today, so baseline includes that cost. After Phase 1 the same log lines route through the async path; prefer comparing **frame count** and **max delta**, which stay meaningful across the logger change.

**Phase 0 exit:** Baseline frames/run, max delta, and pre-zoom stall recorded for the fixed scenario.

---

## Phase 1: Logging & animation-clock hot-path (the cheap, high-impact wins)

The three changes most likely to fix "not smooth" on their own. Low risk, no intended visual change. Ship and merge this phase on its own.

> **Status: code complete (committed `4c30d8d`), build clean, 391→395 tests pass. On-device smoothness/baseline verification still pending (needs GUI).**

- [x] **1.1 Move synchronous console/debug writes off the hot path** (`Services/FileLogger.cs`) — `WriterLoop` now does the `Console`/`Debug` writes on the background thread; `WriteLog` is enqueue-only.
  - `WriteLog` calls `Console.WriteLine` and `Debug.WriteLine` inline on the calling (UI) thread before enqueuing — only the file write is async.
  - Move both writes into the background `WriterLoop` consumer (`:48-66`), or compile them out in Release. Hot path becomes enqueue-only.
  - Keep the bounded-queue drop behavior; confirm nothing relies on synchronous console ordering.
  - **Verify:** app still writes app.log; a forced error still appears in console/log; no deadlock on exit (`Dispose` still drains). Re-run Phase 0 scenario; expect frame count up / max delta down.

- [x] **1.2 Turn off verbose debug logging in shipped config** (`visual-config.json`) — all four flags set `false`; the per-marker line-factory log (`Views/ExtensionLineRenderer.cs:419`) now gated behind `LogRadialExtensionCalculation`.

- [x] **1.3 Replace `DateTime.Now` with `Stopwatch` for the animation clock** (`MainWindow.Navigation.partial.cs`) — single `Stopwatch` drives `elapsed`/`progress`/delta; per-frame keyframe search replaced with direct `Math.Round` index. Regression test: `AnimateViewportTransition_UsesStopwatchNotDateTimeNow`.

**Phase 1 exit:** Build clean; tests pass (395); on-device smoothness + Phase 0 baseline still to be confirmed in the GUI. **Merge gated on that visual check.**

---

## Phase 2: Per-frame allocation, effects, I/O, and appearance polish

Structural changes to the animation loop and rendering. Higher effort / more visual risk than Phase 1. **Resolve [Open decisions](#open-decisions) first** — items D-tagged below are blocked on a decision. Do each one at a time with a Phase 0 re-measure and a visual check.

### 2a. Per-frame allocation in the animation loop (decided — ready)

> **Status: code complete, build clean, 395 tests pass (4 new regression tests). On-device visual verification of 2.1/2.2 still pending (needs GUI).**

- [x] **2.1 Stop rebuilding extension lines every frame in drawn mode** (`MainWindow.LayoutEditor.partial.cs` `ApplyManualLayout`)
  - `Clear()` now guarded behind `!IsAnimating`; new `IExtensionLineRenderer.TryRepositionPinLine` updates the existing pair's endpoints in place (reusing Line/Brush/Effect), and the `RequiresExtensionLine` branch repositions during animation, falling back to `AddLine` on the first frame.
  - Safety: the settle frame runs with `IsAnimating == false` (mode flips to `Normal` before the final `UpdateMarkerPositions`/`onFrameUpdated`), so it always does a clean rebuild — in-flight reuse can't leave a stale final state.
  - Regression test: `ApplyManualLayout_RepositionsLinesInPlaceDuringAnimation_NotFullRebuild`.
  - **Pending visual check:** drawn-mode manual-layout zoom — shaft tracks the map every frame, no flicker/disappear, lands correctly at settle.

- [x] **2.2 Reduce per-frame LINQ/dictionary allocation** (`MainWindow.xaml.cs` `UpdateMarkerPositions`)
  - Visible-individual and visible-cluster-center projections cached in `_animVisibleIndividuals`/`_animVisibleClusterCenters` for the animation's duration; the non-animating branch always rebuilds and clears the cache, so stale data can't leak into normal placement.
  - Regression test: `UpdateMarkerPositions_CachesVisibleProjectionsDuringAnimation`.
  - **Pending visual check:** no placement drift vs. before.

- [x] **2.3 Replace O(n) marker lookups in per-frame loops** — `BuildIndividualMarkerIndex()` builds a name→marker dictionary once per pass; `ApplyIndividualPlacements` and `ApplyCompositePinsToNormalPlacements` (`MainWindow.CompositePins.partial.cs`) take it; `ExtensionLineRenderer.Apply` builds a local `Location`→marker index instead of per-extension `FirstOrDefault`. Output-identical (first-per-name wins). Regression test: `UpdateMarkerPositions_UsesNameIndexNotPerPlacementScan`.

### 2b. Effects & bitmap I/O (blocked on decisions)

- [ ] **2.4 Trim `DropShadowEffect` cost** — **decided (D1):** drop shadows during animation, restore on settle; halve the drawn extended shaft to one shadow; do **not** add per-marker `BitmapCache`.
  - Shadow inventory (every `DropShadowEffect`): drawn head/ball `PinMarker.xaml:28-34` (op 0.55); drawn extended shaft = extension line, **both** outline+core shadowed `ExtensionLineRenderer.cs:401-413` (op ≥0.45, gated on `ShowShadow`); composite head `CompositePinMarker.xaml:28-34` (op 0.35). Non-extended drawn shaft (Rectangle) and composite shafts (PNG, baked depth) have **no** runtime shadow.
  - Sub-step a: drawn extended shaft → one shadow (on the core or outline only), not two.
  - Sub-step b: while `IsAnimating`, suppress the head shadows (drawn `PinBall`, composite `HeadImage`) and the shaft-line shadow; restore on settle. Shadows are faint (op 0.35–0.55), so the visual cost of hiding them mid-motion is minimal while removing the per-frame GPU pass on moving elements.
  - **Verify:** at rest, pins look unchanged; during zoom no shadow flicker; re-measure during a multi-marker zoom.
- [ ] **2.5 [D2] Keyframe bitmap I/O off the UI thread** (`MainWindow.Navigation.partial.cs:504-563`, `Services/AnimationFrameCache.cs:122-173`) — scope pending **Decision D2**.
  - `PreRenderKeyframes` does synchronous disk I/O (30 PNG encode on miss / decode on hit) before the animation — the pre-zoom stall.
  - Unconditional sub-step: avoid the `new WriteableBitmap(...)` copy (`:539,550`) where a frozen `TransformedBitmap` displays identically.
  - **Verify:** pre-zoom stall (Phase 0 metric) shrinks; frames still correct; no first-frame flash.
- [ ] **2.6 High-quality settled full-map rendering + render-options cleanup** (`Views/MapDisplayControl.xaml(.cs)`) — follow [the approved design](../../superpowers/specs/2026-07-01-map-render-quality-design.md) and [implementation plan](../../superpowers/plans/2026-07-01-map-render-quality.md): keep `Stretch="Fill"` and pixel snapping, replace local `NearestNeighbor` with settled `Fant`, remove `EdgeMode="Aliased"`, and set the unchanged cache hint once rather than on every viewport update. **Verify:** the `U` in `SOUTH AMERICA` and representative thin grid/coastline strokes retain coverage; marker/input alignment and full-screen Fill geometry remain unchanged.

### 2c. Appearance polish

- [ ] **2.7 Explicit animation and settled-zoom scaling policy** — use `Linear` before keyframe bitmap materialization, increment `AnimationFrameCache.CacheVersion` so old pixels cannot bypass the change, and preserve the existing full-resolution `Fant` path in `ZoomedRegionCache`. Detailed steps and the 1080p/1440p/4K visual matrix are in [2026-07-01-map-render-quality.md](../../superpowers/plans/2026-07-01-map-render-quality.md). This resolves **Decision D3** without adding the separately deferred settled full-map render cache.
- [ ] **2.8 Sub-pixel marker crispness** (`Views/PinMarker.xaml:4-5`) — spike `UseLayoutRounding="True"` at rest; **revert if it shimmers during animation**. Low stakes.
- [ ] **2.9 Fix shadow-opacity inconsistency** — `PinMarker.xaml:33` hard-codes `Opacity="0.55"` (not bound) and `ExtensionLineRenderer.cs:409` floors at `0.45`, so `visual-config.json:40` `ShadowOpacity` is only partly honored. Bind both to config. **Verify:** changing config value moves both shadows.
- [ ] **2.10 Gate/downgrade the expected "Marker not found for location" warning** (`Views/ExtensionLineRenderer.cs:143`) — fires for markers outside the current view during zoom; put behind the debug flag or downgrade to info.

**Phase 2 exit:** Build clean; `verify.ps1` passes; per-change visual verification confirms no marker-placement / shaft / crispness regressions; Phase 0 metrics improved further (lower pre-zoom stall, fewer dropped frames).

---

## Open decisions

These change *what work happens* in Phase 2 and need a call (mine, with a recommendation, or the user's) before the tagged items start. Resolve, record the choice here, then unblock.

- **D1 — Shadow strategy (blocks 2.4). RESOLVED 2026-06-23:** drop shadows during animation + restore on settle, and halve the drawn extended shaft to one shadow; skip the `BitmapCache` option. Rationale: shadows are faint (op 0.35–0.55) and only the head is shadowed in both modes (drawn shaft shadow exists only when extended; composite shafts bake depth into the PNG), so hiding them during motion costs almost nothing visually while shedding the per-frame GPU pass. Future: make shadow strength config/Tuning-panel-driven (see TO_DO "Composite pins & manual layouts").
- **D2 — Disk frame cache: keep or drop (blocks 2.5 scope).** `AnimationFrameCache` persists 30 PNGs/zoom to disk. The in-memory `CroppedBitmap`→`TransformedBitmap` build is cheap; the disk round-trip may cost more than it saves. **Spike to decide:** measure a "no disk cache, build in memory each zoom" variant against the Phase 0 baseline. If in-memory is within noise, delete the disk cache (simpler, no stale-cache bugs); otherwise keep it but move encode to a background thread. Recommendation: spike, lean toward delete.
- **D3 — Anti-"pop" approach (blocks 2.7). RESOLVED 2026-07-01:** use `Linear` for materialized animation keyframes, `Fant` for settled full-map and settled high-resolution zoom rendering, and invalidate existing frame-cache pixels. Do not add a cross-fade unless the verified result still shows a settle pop.
- **D4 — Appearance scope. RESOLVED 2026-07-01 for map rendering:** items 2.6/2.7 stay in this plan because the settled full-map dropout has been reproduced and shares the same scaling-policy boundary. Item 2.8 remains independent low-stakes marker polish.

---

## Out of scope / related
- Continuous pin tracking during zoom is tracked separately (TO_DO "Zoom & animation"; completed `continuous-pin-tracking-during-zoom-plan.md`). This plan is about throughput/smoothness, not which pins track.
