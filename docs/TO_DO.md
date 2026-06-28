# Interactive World Map — Backlog

Human steering list. Implementation detail lives in [exec-plans/active/](exec-plans/active/). Composite-pin work is coordinated in [composite-pins-program.md](exec-plans/active/composite-pins-program.md).

**Last updated:** June 28, 2026

---

## URGENT bugs (June 24, 2026)

- [x] **[URGENT] Pin tilt after manual layout edit** — code fix DONE 2026-06-24; **manual GUI confirmation only.** `GetMarkerEndpoint` now uses the drawn pin's `GetConnectionPoint()` instead of `LocationMarkerSize/2`, so newly saved/replayed pins stay vertical. Caveat: layouts saved before the fix may still have the lean baked into JSON; re-save or migrate them to straighten.
- [x] **[URGENT] Drawn-pin shaft length scales with zoom** — code fix DONE 2026-06-24; **manual GUI confirmation only.** `CompositePinApplicationService.BuildApplyInstructions` now measures source-space saved head offsets against a full-map reference viewport, keeping shaft length zoom-invariant while still resize-aware.
- [x] **[URGENT] Unload saved manual layout without deleting it** — code fix DONE 2026-06-24; **manual GUI confirmation only.** Edit mode now has an "Unload Layout" action that suppresses the saved layout for the session, reverts to auto-placement, and leaves the JSON untouched.

---

## Zoom & animation

- [ ] Finish smooth/fast zoom performance + appearance — [zoom-performance-appearance-plan.md](exec-plans/active/zoom-performance-appearance-plan.md). Phase 1 hot-path logging/clock work and Phase 2a allocation/lookup reductions are code complete; remaining work is Phase 2b/2c (shadow/effect cost, keyframe bitmap I/O decision, render-options cleanup, anti-pop/crispness polish, shadow-opacity consistency, noisy warning downgrade). Findings: [performance-appearance-review.md](performance-appearance-review.md).
- [ ] Review zoom-out implementation against zoom-in: compare rendering path, animation timing, smoothness, and image quality; optimize or share behavior where appropriate.
- [x] Close any open content popup when backing out of a zoomed view (incl. the single-click auto-popup) — `AnimateZoomOut` now calls `CloseActiveSubwindow` (DONE 2026-06-23)

---

## Composite pins & manual layouts

Dashboard: [composite-pins-program.md](exec-plans/active/composite-pins-program.md)

- [ ] Fix or remove composite pins that overstretch shadow
- [ ] Manual GUI smoke only: generated AutoSeed loading — [manual-layout-seed-alignment-plan.md](exec-plans/active/manual-layout-seed-alignment-plan.md), Phase 3. Code and automated shared-path/load-key coverage are done; confirm in the running app for at least two seeded clusters.
- [ ] Manual GUI smoke only: layout persistence robustness — [manual-layout-seed-alignment-plan.md](exec-plans/active/manual-layout-seed-alignment-plan.md), Phase 5. Per-user storage, crash-proof load, size-independent full-map keys, and source-space saved positions are code complete; confirm a full-map layout survives resize and lands correctly.
- [x] Investigate drawn-pin tilt — **RESOLVED 2026-06-24** (see URGENT section above). Not a transform artifact: root cause was `MainWindow.GetMarkerEndpoint` saving the head with `LocationMarkerSize/2` instead of the pin's `GetConnectionPoint()`, offsetting the head ~2px left of the tip → ~5° CCW lean on replay. Fixed.
- [ ] Explore post-render smooth black outline on composite pins (runtime, after shaft+head compose) — assess vs baked `outline_dark_*` asset variants; see feasibility notes in [composite-pins-program.md](exec-plans/active/composite-pins-program.md) or new exec plan if pursued
- [ ] Composite mode: user UI to reassign pin head asset (`HeadSourcePath` / `pin_XX_head.png` — effectively head color) — [manual-layout-pin-appearance-plan.md](exec-plans/active/manual-layout-pin-appearance-plan.md) (today heads are auto-picked by location hash; only **shaft** has right-click override; verify reassigned head persists on manual layout save/reload; infrastructure exists: `ManualLayoutMarker.HeadSourcePath`, enricher on save, replay via `preferredHeadSourcePath`; missing: head picker UI like shaft menu)
- [ ] Drawn mode: user UI to pick pin head color from a fixed palette and persist per location in manual layout save — [manual-layout-pin-appearance-plan.md](exec-plans/active/manual-layout-pin-appearance-plan.md) (today: random color at create; `SetPinColor` exists but no picker or layout field)
- [x] Add pinhead variants with black outlines — generated `outline_black_2px`, `outline_black_4px`, `outline_black_6px`, `outline_black_8px`, `outline_black_10px`, `outline_black_12px`, and `outline_black_14px` under `Images&Content/Pins_v2/parts/head_variants/` — [pinhead-black-outline-variants-plan.md](exec-plans/completed/pinhead-black-outline-variants-plan.md)
- [ ] Do not use bright yellow pin heads unless manually assigned
- [ ] Manual visual acceptance: drawn-pin divot caps. Code and automated coverage are complete; compare `ScreenHorizontal` and `ShaftAligned` on normal/inverted/angled pins, then smoke drag/hover/zoom behavior.
- [ ] Revisit pin shadows — allow tuning shadow strength via config + Tuning panel (today: drawn head shadow hardcoded `PinMarker.xaml`, composite head shadow hardcoded `CompositePinMarker.xaml`, drawn extended-shaft shadow floored in `ExtensionLineRenderer.cs`; `ShadowOpacity` only partly honored). Follow-up to shadow perf work in [zoom-performance-appearance-plan.md](exec-plans/active/zoom-performance-appearance-plan.md) (2.4/2.9)

## Inactive (optional polish)

- [ ] Composite head visual polish — shaft collar clip (§8.4 step 4), pin_09/10 shading (step 5), `TargetHeadRadiusPx` tuning (step 6) — [composite-pin-head-placement-fix-plan.md](exec-plans/inactive/composite-pin-head-placement-fix-plan.md)

## Refactoring & quality

Assessment: [LARGE_FILE_REFACTORING_ASSESSMENT.md](assessments/LARGE_FILE_REFACTORING_ASSESSMENT.md) — Phases 1–4 complete (2026-06-08)

- [ ] Refactoring assessment follow-through — [refactoring-assessment-followthrough-plan.md](exec-plans/active/refactoring-assessment-followthrough-plan.md)
- [ ] Resolve nullable reference warnings (CS8602/CS8604) — Phase 13 in follow-through plan
- [ ] Zoom-level doc cleanup — [ZOOM_LEVELS_AUDIT_ASSESSMENT.md](assessments/ZOOM_LEVELS_AUDIT_ASSESSMENT.md)

## Developer tooling

- [x] Tuning panel: shaft/head variant pickers — replace `TxtShaftVariant` / `TxtHeadVariant` free-text boxes with drop-downs populated from `Images&Content/Pins_v2/parts/shaft_variants/` and `head_variants/` (include blank/base option); grey out both pickers when composite pins are off (`ChkComposite` unchecked). Follow-up to [runtime-tuning-panel-plan.md](exec-plans/completed/runtime-tuning-panel-plan.md). Plan: [tuning-panel-dropdowns-plan.md](exec-plans/completed/tuning-panel-dropdowns-plan.md).

## User ideas (product)

- [ ] Thumbnail side panel: support touchscreen vertical scrolling so users can swipe through all images.
- [ ] Content view: prevent the Information panel from covering `Back to Map`; prefer moving `Back to Map` to the lower-left corner.
- [ ] Main content: optionally show information about the selected image/content in a bottom pane or a separate left-side window.
- [ ] Main content display: consider sizing the display window to the selected content's aspect ratio.
- [ ] Main content images: support zooming and/or maximizing the selected image for closer inspection.
- [ ] Subwindow opens near pin, not screen center
- [ ] Home / welcome screen before map
- [ ] Larger popup windows; general UI polish
- [ ] Wire in actual locations and content; accession-number folder structure
- [ ] Welcome / instructions screen
- [ ] Content ordering; bio popup per marker
- [ ] Don't animate extension lines until fully zoomed in
- [ ] Consider intermediate zoom levels and free panning — explore discrete zoom steps between full map and cluster zoom, plus drag-to-pan on the map canvas (today: cluster click zoom + back only)
- [ ] Better logging filters; smoother zoom

## High priority

- [ ] Separate drawn pin model into head-only, auto-stub, and manual-layout pin components so edited pins do not rely on hiding a built-in vertical shaft — [drawn-pin-model-separation-plan.md](exec-plans/active/drawn-pin-model-separation-plan.md)
- [x] Refactor `MainWindow.xaml.cs` (872 lines) back under the 800-line taste limit — split into focused partials or extract services. Fails `scripts/verify_taste.py`; **pre-existing debt that predates the tip-cap feature** (already red at HEAD) and currently keeps `scripts/verify.ps1` from going green. **DONE 2026-06-24** — marker-placement engine extracted to `MainWindow.MarkerPlacement.partial.cs`; core file now 625 taste-lines.
- [x] Refactor `MainWindow.LayoutEditor.partial.cs` (801 lines) back under the 800-line taste limit. Same pre-existing taste failure as above. **DONE 2026-06-24** — drag handlers extracted to `MainWindow.LayoutEditorDrag.partial.cs`; file now 661 taste-lines.
- [ ] Consider .NET 8 LTS upgrade (from .NET 6)
- [ ] Marker distortion at 50x+ zoom

## Medium priority

- [ ] App.xaml styling and resource cleanup
- [ ] Sample content expansion; README screenshots
- [ ] Error handling infrastructure (startup dialog, runtime notifications)

## Low priority

- [ ] Cross-platform / resolution manual testing
- [ ] Search, pan, categories, export — future enhancements
- [ ] Look into adapting the app to be able to run in a browser over the internet

## Deferred

- [ ] Tuning panel: variant search/filter — type-to-filter or grouping for 60+ shaft variant folders in combo pickers. Deferred from [tuning-panel-dropdowns-plan.md](exec-plans/completed/tuning-panel-dropdowns-plan.md) v1; dropdown picker basics are complete, and this is parked until the list size becomes a real workflow drag.
- [ ] Intermittent divot cap inside a stub-looking pin head near Japan/China — not currently reproducible after stale-cap refresh and head-layer safeguards; revisit if observed again.
