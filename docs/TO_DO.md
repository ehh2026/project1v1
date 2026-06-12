# Interactive World Map — Backlog

Human steering list. Implementation detail lives in [exec-plans/active/](exec-plans/active/). Composite-pin work is coordinated in [composite-pins-program.md](exec-plans/active/composite-pins-program.md).

**Last updated:** June 12, 2026

---

## Composite pins & manual layouts

Dashboard: [composite-pins-program.md](exec-plans/active/composite-pins-program.md)

- [ ] Fix or remove composite pins that overstretch shadow
- [ ] Shared runtime/seed placement path — [manual-layout-seed-alignment-plan.md](exec-plans/active/manual-layout-seed-alignment-plan.md)
- [ ] Reliable seed loading in app — same plan, Phase 3
- [x] Remove `pins.jpg` / `ImagePinMarker` — [remove-pins-jpg-legacy-path-plan.md](exec-plans/active/remove-pins-jpg-legacy-path-plan.md)
- [x] Composite pins on all individual markers — [composite-pins-unzoomed-plan.md](exec-plans/active/composite-pins-unzoomed-plan.md) Phases 1–4 (non-edit and edit mode; unzoomed individuals yes, unzoomed cluster aggregates no)
- [x] Composite edit mode verification — [composite-pins-unzoomed-plan.md](exec-plans/active/composite-pins-unzoomed-plan.md) Phase 4 (zoomed cluster; composite drag path also wired for stubs)
- [x] Manual layout edit on fully zoomed-out map — visible single-location stub pins only; group key `fullmap_sWxH` + variants; no zoom while editing — [composite-pins-unzoomed-plan.md](exec-plans/active/composite-pins-unzoomed-plan.md) Phase 6 (core smoke accepted 2026-06-12)
- [ ] Composite pins persist during zoom — visible single-location stubs should look the same zoomed/unzoomed (same composite renderer, screen-space stub length/direction, and tip anchoring; only viewport-projected position changes), with no drawn-pin flash when composite is enabled — [composite-pins-unzoomed-plan.md](exec-plans/active/composite-pins-unzoomed-plan.md) Phase 7
- [x] Composite shaft visibility / contrast — [composite-pin-shaft-visibility-plan.md](exec-plans/completed/composite-pin-shaft-visibility-plan.md) (`outline_dark_7px` default)
- [ ] Generate 7px outline variants from lit pin shaft parts images (`Images&Content/Pins_v2/parts/shaft_variants/`)
- [ ] Allow setting/changing pin heads as well as shafts; persist head choice in manual layout save
- [ ] Do not use bright yellow pin heads unless manually assigned

## Inactive (optional polish)

- [ ] Composite head visual polish — shaft collar clip (§8.4 step 4), pin_09/10 shading (step 5), `TargetHeadRadiusPx` tuning (step 6) — [composite-pin-head-placement-fix-plan.md](exec-plans/inactive/composite-pin-head-placement-fix-plan.md)

## Refactoring & quality

Assessment: [LARGE_FILE_REFACTORING_ASSESSMENT.md](assessments/LARGE_FILE_REFACTORING_ASSESSMENT.md) — Phases 1–4 complete (2026-06-08)

- [ ] Refactoring assessment follow-through — [refactoring-assessment-followthrough-plan.md](exec-plans/active/refactoring-assessment-followthrough-plan.md)
- [x] Split `Tools/PinDebugger/Program.cs` — see assessment §2 (TD-013 resolved)
- [x] Decompose `MainWindow.xaml.cs` (TD-001) — orchestrators + partials; primary file ~732 lines
- [ ] Resolve nullable reference warnings (CS8602/CS8604) — Phase 13 in follow-through plan
- [ ] Zoom-level doc cleanup — [ZOOM_LEVELS_AUDIT_ASSESSMENT.md](assessments/ZOOM_LEVELS_AUDIT_ASSESSMENT.md)

## User ideas (product)

- [ ] Subwindow opens near pin, not screen center
- [ ] Home / welcome screen before map
- [ ] Larger popup windows; general UI polish
- [ ] Wire in actual locations and content; accession-number folder structure
- [ ] Excel/table for addresses and coordinates
- [ ] Welcome / instructions screen
- [ ] Content ordering; bio popup per marker
- [ ] Don't animate extension lines until fully zoomed in
- [ ] Better logging filters; smoother zoom

## High priority

- [ ] Consider .NET 8 LTS upgrade (from .NET 6)
- [ ] Marker distortion at 50x+ zoom

## Medium priority

- [ ] App.xaml styling and resource cleanup
- [ ] Sample content expansion; README screenshots
- [ ] Error handling infrastructure (startup dialog, runtime notifications)

## Low priority

- [ ] Cross-platform / resolution manual testing
- [ ] Search, pan, categories, export — future enhancements

## Recently done

- Composite shaft visibility — baked `outline_dark_7px` variant generated, enabled as default in `visual-config.json`; plan complete 2026-06-11 — [composite-pin-shaft-visibility-plan.md](exec-plans/completed/composite-pin-shaft-visibility-plan.md)
- Composite edit mode (Phase 4) — composite pins draggable in edit mode at all zoom levels; `CompositePinEditModeTests` added 2026-06-11 — [composite-pins-unzoomed-plan.md](exec-plans/active/composite-pins-unzoomed-plan.md)
- Pin rendering polish — anti-aliasing, gated pre-rasterization, and depth sorting; plan moved 2026-06-10 — [pin-rendering-improvements-plan.md](exec-plans/completed/pin-rendering-improvements-plan.md)
- Composite pin unzoomed Phase 0 — Option A stub segment (`DefaultStubLengthPixels = 24`, screen-up); unzoomed individual markers in scope; unzoomed cluster aggregates excluded — [composite-pins-unzoomed-plan.md](exec-plans/active/composite-pins-unzoomed-plan.md) (2026-06-09)
- Composite pin head placement fix — Phases 1–3 (`local_center` anchor, pin_07 geometry); tests pass, visual OK; plan parked 2026-06-09 — [composite-pin-head-placement-fix-plan.md](exec-plans/inactive/composite-pin-head-placement-fix-plan.md)
- Composite pin core placement — Phases 1–6, saved-layout cache, common-angle verification, preview grids, and `verify.ps1` passed 2026-06-09 — [pin-parts-composite-placement-plan.md](exec-plans/completed/pin-parts-composite-placement-plan.md)
- Large-file refactoring Phases 1–4 — PinDebugger split, `MarkerPlacementOrchestrator`, `BuildApplyInstructions`, MainWindow partials (2026-06-08); see [CHANGELOG.md](../CHANGELOG.md)
- Manual layout variants — [manual-layout-variants-plan.md](exec-plans/completed/manual-layout-variants-plan.md) (2026-06-08)
- Unzoomed marker offset — fixed 2026-06-06 ([UNZOOMED_MARKER_OFFSET_ASSESSMENT.md](assessments/UNZOOMED_MARKER_OFFSET_ASSESSMENT.md))
- Security CI — [security-ci-plan.md](exec-plans/completed/security-ci-plan.md)
- Composite pin Phases 1–5 partial — see [CHANGELOG.md](../CHANGELOG.md)
