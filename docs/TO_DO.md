# Interactive World Map — Backlog

Human steering list. Implementation detail lives in [exec-plans/active/](exec-plans/active/). Composite-pin work is coordinated in [composite-pins-program.md](exec-plans/active/composite-pins-program.md).

**Last updated:** June 22, 2026

---

## Zoom & animation

- [ ] (none currently open — section kept as placeholder for future zoom/animation work)

---

## Composite pins & manual layouts

Dashboard: [composite-pins-program.md](exec-plans/active/composite-pins-program.md)

- [ ] Fix or remove composite pins that overstretch shadow
- [ ] Shared runtime/seed placement path — [manual-layout-seed-alignment-plan.md](exec-plans/active/manual-layout-seed-alignment-plan.md)
- [ ] Reliable seed loading in app — same plan, Phase 3
- [ ] Explore post-render smooth black outline on composite pins (runtime, after shaft+head compose) — assess vs baked `outline_dark_*` asset variants; see feasibility notes in [composite-pins-program.md](exec-plans/active/composite-pins-program.md) or new exec plan if pursued
- [ ] Composite mode: user UI to reassign pin head asset (`HeadSourcePath` / `pin_XX_head.png` — effectively head color) — [manual-layout-pin-appearance-plan.md](exec-plans/active/manual-layout-pin-appearance-plan.md) (today heads are auto-picked by location hash; only **shaft** has right-click override; verify reassigned head persists on manual layout save/reload; infrastructure exists: `ManualLayoutMarker.HeadSourcePath`, enricher on save, replay via `preferredHeadSourcePath`; missing: head picker UI like shaft menu)
- [ ] Drawn mode: user UI to pick pin head color from a fixed palette and persist per location in manual layout save — [manual-layout-pin-appearance-plan.md](exec-plans/active/manual-layout-pin-appearance-plan.md) (today: random color at create; `SetPinColor` exists but no picker or layout field)
- [x] Add pinhead variants with black outlines — generated `outline_black_2px`, `outline_black_4px`, `outline_black_6px`, `outline_black_8px`, `outline_black_10px`, `outline_black_12px`, and `outline_black_14px` under `Images&Content/Pins_v2/parts/head_variants/` — [pinhead-black-outline-variants-plan.md](exec-plans/completed/pinhead-black-outline-variants-plan.md)
- [ ] Do not use bright yellow pin heads unless manually assigned

## Inactive (optional polish)

- [ ] Composite head visual polish — shaft collar clip (§8.4 step 4), pin_09/10 shading (step 5), `TargetHeadRadiusPx` tuning (step 6) — [composite-pin-head-placement-fix-plan.md](exec-plans/inactive/composite-pin-head-placement-fix-plan.md)

## Refactoring & quality

Assessment: [LARGE_FILE_REFACTORING_ASSESSMENT.md](assessments/LARGE_FILE_REFACTORING_ASSESSMENT.md) — Phases 1–4 complete (2026-06-08)

- [ ] Refactoring assessment follow-through — [refactoring-assessment-followthrough-plan.md](exec-plans/active/refactoring-assessment-followthrough-plan.md)
- [ ] Resolve nullable reference warnings (CS8602/CS8604) — Phase 13 in follow-through plan
- [ ] Zoom-level doc cleanup — [ZOOM_LEVELS_AUDIT_ASSESSMENT.md](assessments/ZOOM_LEVELS_AUDIT_ASSESSMENT.md)

## Developer tooling

- [ ] Tuning panel: shaft/head variant pickers — replace `TxtShaftVariant` / `TxtHeadVariant` free-text boxes with drop-downs populated from `Images&Content/Pins_v2/parts/shaft_variants/` and `head_variants/` (include blank/base option); grey out both pickers when composite pins are off (`ChkComposite` unchecked). Follow-up to [runtime-tuning-panel-plan.md](exec-plans/completed/runtime-tuning-panel-plan.md). Plan: [tuning-panel-dropdowns-plan.md](exec-plans/active/tuning-panel-dropdowns-plan.md).

## User ideas (product)

- [ ] Single-location marker click while unzoomed: zoom in and auto-open content window
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
- [ ] Consider .NET 8 LTS upgrade (from .NET 6)
- [ ] Marker distortion at 50x+ zoom

## Medium priority

- [ ] App.xaml styling and resource cleanup
- [ ] Sample content expansion; README screenshots
- [ ] Error handling infrastructure (startup dialog, runtime notifications)

## Low priority

- [ ] Cross-platform / resolution manual testing
- [ ] Search, pan, categories, export — future enhancements
