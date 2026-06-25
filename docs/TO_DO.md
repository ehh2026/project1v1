# Interactive World Map — Backlog

Human steering list. Implementation detail lives in [exec-plans/active/](exec-plans/active/). Composite-pin work is coordinated in [composite-pins-program.md](exec-plans/active/composite-pins-program.md).

**Last updated:** June 25, 2026

---

## URGENT bugs (June 24, 2026)

- [x] **[URGENT] Pin tilt after manual layout edit** — **code fix DONE 2026-06-24.** Root cause: `MainWindow.GetMarkerEndpoint` (the save/collect path) located a drawn pin's head with `LocationMarkerSize/2` (12px) instead of the pin's own `GetConnectionPoint()` — a `PinMarker` is wider (~16px), so the saved head landed ~2px left of the tip. Over a ~24px shaft that is a ~5° counter-clockwise lean, baked into every non-moved pin on save and replayed as a slanted extension line. Manually-moved pins read correct because they carry a real extension endpoint (`TryGetLineEndpoint`) and never hit the fallback. Verified against screenshot `temp/Screenshot 2026-06-24 001758` (cropped + pixel-traced: shaft center-x grows top→bottom = CCW, matching the report). Fix: `GetMarkerEndpoint` now returns `Canvas.Left/Top + pin.GetConnectionPoint()` for `PinMarker` content, so head sits directly above tip (vertical). Build clean, 412/412 tests pass. **Caveat:** layouts saved before the fix have the lean baked into their JSON — re-save (or a future one-time migration) straightens them. **Pending: GUI confirmation.**
- [x] **[URGENT] Drawn-pin shaft length scales with zoom** — **regression introduced 2026-06-23 by commit `dd9e6d7` (persistence 5c), code fix DONE 2026-06-24.** Root cause: that commit made user saves persist the pin head in source-image space (`SourceExtendedX/Y`); `CompositePinApplicationService.BuildApplyInstructions` then re-projected the head through the *current* (zoomed) viewport, so the tip→head shaft grew by the zoom factor. Fix: `BuildApplyInstructions` now takes an optional full-map reference viewport and measures the head offset at that fixed fit scale — the shaft keeps a constant screen length at any zoom yet stays resize-aware (the fit scale tracks the window). Regression test `BuildApplyInstructions_SourceExtendedHead_ShaftLengthIsZoomInvariant` locks it in (411→412 tests pass). Screenshot of the bug: `temp/Screenshot 2026-06-24 001932`. **Pending: GUI confirmation** that a saved single-location layout zooms in with a fixed-length shaft (can't verify headless).
- [x] **[URGENT] No enable/disable or size/curvature tuning for tip caps in Tuning panel** — **code fix DONE 2026-06-24.** Added a "Tip cap (drawn pins)" section to the Developer Tuning panel: a Style combo (`None`/`Horizontal`/`Concave` — None = disabled, doubles as the on/off toggle) plus Cap width (`ExtendPx`), Cap height (`HeightPx`), and Curvature (`ArcDepthPx`) fields. Wired through `TuningPanelEventArgs` → `ApplyTuningAsync` (mutates `_visualConfig.PinMarkers.DrawnPinTipCap`, refresh via `ReapplyViewAfterTuningChange` → `UpdatePinTipCaps`), with `LoadValues`/`CreateTuningArgs` round-trip and `TryValidate` rejecting negative/non-finite values. Honors Save (writes to `visual-config.json`) and Reload (validates then applies). 6 tests added (validation + wiring); 422 tests pass, build clean. **Pending: GUI confirmation** that toggling Style and editing the px fields updates caps live.
- [x] **[URGENT] No way to unload a saved manual layout without deleting it from disk** — **code fix DONE 2026-06-24.** Added an "Unload Layout" button to the edit-mode panel (amber, non-destructive sibling of "Delete and Recalculate"). Implementation: session-scoped `LayoutEditorController.IsManualLayoutSuppressed` flag set by `UnloadManualLayout()`; the full-map auto-apply entry points (`TryApplyFullMapManualLayout`, `TryLoadFullMapManualLayoutForAnimation`, `TryApplyFullMapLayoutForZoomedSingle`) skip applying while suppressed, so pins revert to auto-placement across zoom/back/resize/tuning-reapply. The saved JSON is untouched. Suppression is cleared when a layout next becomes active (re-entering the editor reloads it from disk via `OnEditLayoutButtonClick`) and is not persisted, so an app restart restores normal auto-apply. Coverage extended 2026-06-24 to multi-location cluster-key layouts: `ShowZoomedView` no longer stages or applies `_savedLayoutToApply` while suppressed, so unloading also reverts a zoomed cluster to auto-placement. Unit + source-guard tests added (423 tests pass), build clean. **Pending: GUI confirmation.**

---

## Zoom & animation

- [ ] Finish smooth/fast zoom performance + appearance — [zoom-performance-appearance-plan.md](exec-plans/active/zoom-performance-appearance-plan.md). Phase 1 hot-path logging/clock work and Phase 2a allocation/lookup reductions are code complete; remaining work is Phase 2b/2c (shadow/effect cost, keyframe bitmap I/O decision, render-options cleanup, anti-pop/crispness polish, shadow-opacity consistency, noisy warning downgrade). Findings: [performance-appearance-review.md](performance-appearance-review.md).
- [x] Close any open content popup when backing out of a zoomed view (incl. the single-click auto-popup) — `AnimateZoomOut` now calls `CloseActiveSubwindow` (DONE 2026-06-23)

---

## Composite pins & manual layouts

Dashboard: [composite-pins-program.md](exec-plans/active/composite-pins-program.md)

- [ ] Fix or remove composite pins that overstretch shadow
- [ ] Manual GUI smoke for generated AutoSeed loading — [manual-layout-seed-alignment-plan.md](exec-plans/active/manual-layout-seed-alignment-plan.md), Phase 3 manual checklist. Automated shared-path generation and load-key coverage are done; confirm in the running app for at least two seeded clusters.
- [ ] Layout persistence robustness — same plan, Phase 5. **5a per-user storage (`%AppData%`) + 5b crash-proof load DONE 2026-06-23. 5c brittle keys: code DONE 2026-06-23** — full-map key is now size-independent (`"fullmap"`, collapses legacy `fullmap_s{W}x{H}` via compatible-key match) and user saves persist source-space coords (`SourceExtendedX/Y`) so layouts re-project at any window size; cluster keys already tolerated drift. **Pending: GUI verification** that a full-map layout survives a resize and lands on the right positions (can't run remotely)
- [x] Investigate drawn-pin tilt — **RESOLVED 2026-06-24** (see URGENT section above). Not a transform artifact: root cause was `MainWindow.GetMarkerEndpoint` saving the head with `LocationMarkerSize/2` instead of the pin's `GetConnectionPoint()`, offsetting the head ~2px left of the tip → ~5° CCW lean on replay. Fixed.
- [ ] Explore post-render smooth black outline on composite pins (runtime, after shaft+head compose) — assess vs baked `outline_dark_*` asset variants; see feasibility notes in [composite-pins-program.md](exec-plans/active/composite-pins-program.md) or new exec plan if pursued
- [ ] Composite mode: user UI to reassign pin head asset (`HeadSourcePath` / `pin_XX_head.png` — effectively head color) — [manual-layout-pin-appearance-plan.md](exec-plans/active/manual-layout-pin-appearance-plan.md) (today heads are auto-picked by location hash; only **shaft** has right-click override; verify reassigned head persists on manual layout save/reload; infrastructure exists: `ManualLayoutMarker.HeadSourcePath`, enricher on save, replay via `preferredHeadSourcePath`; missing: head picker UI like shaft menu)
- [ ] Drawn mode: user UI to pick pin head color from a fixed palette and persist per location in manual layout save — [manual-layout-pin-appearance-plan.md](exec-plans/active/manual-layout-pin-appearance-plan.md) (today: random color at create; `SetPinColor` exists but no picker or layout field)
- [x] Add pinhead variants with black outlines — generated `outline_black_2px`, `outline_black_4px`, `outline_black_6px`, `outline_black_8px`, `outline_black_10px`, `outline_black_12px`, and `outline_black_14px` under `Images&Content/Pins_v2/parts/head_variants/` — [pinhead-black-outline-variants-plan.md](exec-plans/completed/pinhead-black-outline-variants-plan.md)
- [ ] Do not use bright yellow pin heads unless manually assigned
- [ ] Add a horizontal or concave line at the drawn pin tips — [drawn-pin-tip-cap-plan.md](exec-plans/active/drawn-pin-tip-cap-plan.md) (**code landed 2026-06-23**: opt-in `PinMarkers.DrawnPinTipCap` in `visual-config.json` — `Style` `None`/`Horizontal`/`Concave`, both shapes implemented, default `None`. **Pending human visual gate**: set `Style:"Concave"` and confirm it reads as the pin stuck into the map (Phase 4b), plus manual smoke on stub/extension/drag/hover/zoom (Phase 5) — needs the GUI)
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
