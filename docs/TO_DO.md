# Interactive World Map — Backlog

Human steering list. Implementation detail lives in [exec-plans/active/](exec-plans/active/). Composite-pin work is coordinated in [composite-pins-program.md](exec-plans/active/composite-pins-program.md).

**Last updated:** June 24, 2026

---

## URGENT bugs (June 24, 2026)

- [ ] **[URGENT] Pin tilt after manual layout edit** — after manually repositioning any pin in the unzoomed/full-map layout editor, all other single-location drawn pins acquire a slight counter-clockwise lean from vertical. Green+blue close-together pins (manually moved) look correct; every other pin is tilted. Screenshot: `temp/Screenshot 2026-06-24 001758`. The existing tilt investigation entry (below in Composite pins) predates this confirmed reproduction — this is now a confirmed regression with a known trigger, not just a mystery. Fix priority: must resolve before layout-editor feature is considered stable.
- [ ] **[URGENT] Drawn-pin shaft length scales with zoom** — when zooming into a single-location pin the shaft grows much taller on screen with zoom level instead of remaining a fixed screen-space size. Screenshot: `temp/Screenshot 2026-06-24 001932` shows shaft spanning most of the window after zooming in on Michigan. Pin shaft height must be constant in screen pixels regardless of zoom.
- [ ] **[URGENT] No enable/disable or size/curvature tuning for tip caps in Tuning panel** — the `PinMarkers.DrawnPinTipCap` feature landed (config `Style: None/Horizontal/Concave`) but the Tuning panel has no toggle to enable or disable tip caps at runtime, and no sliders/fields to tune cap size or curvature. Add: on/off toggle, cap width (px), cap height/curvature (px or ratio), synced to visual-config live-reload.
- [ ] **[URGENT] No way to unload a saved manual layout without deleting it from disk** — once a manual layout is loaded it can only be cleared by deleting the saved file. Need an "Unload / Reset to default" action in the UI that: (a) removes the active layout from the runtime view so pins revert to their auto-placed positions, and (b) leaves the saved JSON file on disk untouched so the layout can be reloaded later.

---

## Zoom & animation

- [ ] make all pins track map during zoom, instead of only the clicked one tracking and others not appearing during zoom-out and only appearing when zoom-out is complete
- [ ] Smooth/fast zoom performance + appearance — remove per-frame overhead (sync logging on UI thread, verbose debug flags, `DateTime.Now` clock, per-frame line rebuilds, shadow/effect cost, blocking keyframe I/O) — [zoom-performance-appearance-plan.md](exec-plans/active/zoom-performance-appearance-plan.md) (findings: [performance-appearance-review.md](performance-appearance-review.md))
- [x] Close any open content popup when backing out of a zoomed view (incl. the single-click auto-popup) — `AnimateZoomOut` now calls `CloseActiveSubwindow` (DONE 2026-06-23)

---

## Composite pins & manual layouts

Dashboard: [composite-pins-program.md](exec-plans/active/composite-pins-program.md)

- [ ] Fix or remove composite pins that overstretch shadow
- [ ] Shared runtime/seed placement path — [manual-layout-seed-alignment-plan.md](exec-plans/active/manual-layout-seed-alignment-plan.md)
- [ ] Reliable seed loading in app — same plan, Phase 3
- [ ] Layout persistence robustness — same plan, Phase 5. **5a per-user storage (`%AppData%`) + 5b crash-proof load DONE 2026-06-23. 5c brittle keys: code DONE 2026-06-23** — full-map key is now size-independent (`"fullmap"`, collapses legacy `fullmap_s{W}x{H}` via compatible-key match) and user saves persist source-space coords (`SourceExtendedX/Y`) so layouts re-project at any window size; cluster keys already tolerated drift. **Pending: GUI verification** that a full-map layout survives a resize and lands on the right positions (can't run remotely)
- [ ] Investigate drawn-pin tilt — **reproduced 2026-06-24** (see URGENT section above): trigger is moving any pin in the unzoomed layout editor — all other drawn pins then lean CCW. Pin shaft is a plain vertical `Rectangle` with no explicit transform; cause still unknown — suspect a Canvas left/top rounding artifact or transform applied during drag propagating to non-dragged pins. Check `MainWindow.LayoutEditorDrag.partial.cs` drag-commit path and `MainWindow.MarkerPlacement.partial.cs` re-render for unintended transform side-effects.
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
- [ ] Tuning panel: variant search/filter — type-to-filter or grouping for 60+ shaft variant folders in combo pickers (deferred from [tuning-panel-dropdowns-plan.md](exec-plans/completed/tuning-panel-dropdowns-plan.md) v1).
- [ ] make sure that dev tools like Edit Layout and Tuning panel can be disabled during production

## User ideas (product)

- [ ] Subwindow opens near pin, not screen center
- [ ] Home / welcome screen before map
- [ ] Larger popup windows; general UI polish
- [ ] Wire in actual locations and content; accession-number folder structure
- [ ] Excel/table for addresses and coordinates
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
