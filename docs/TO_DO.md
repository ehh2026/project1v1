# Interactive World Map — Backlog

Human steering list. Implementation detail lives in [exec-plans/active/](exec-plans/active/). Composite-pin work is coordinated in [composite-pins-program.md](exec-plans/active/composite-pins-program.md).

**Last updated:** June 9, 2026

---

## Composite pins & manual layouts

Dashboard: [composite-pins-program.md](exec-plans/active/composite-pins-program.md)

- [ ] Fix or remove composite pins that overstretch shadow
- [ ] Shared runtime/seed placement path — [manual-layout-seed-alignment-plan.md](exec-plans/active/manual-layout-seed-alignment-plan.md)
- [ ] Reliable seed loading in app — same plan, Phase 3
- [ ] Remove `pins.jpg` / `ImagePinMarker` — [remove-pins-jpg-legacy-path-plan.md](exec-plans/active/remove-pins-jpg-legacy-path-plan.md)
- [ ] Composite pins on all individual markers — [composite-pins-unzoomed-plan.md](exec-plans/active/composite-pins-unzoomed-plan.md) Phases 1–3 (Phase 0 ✅ Option A stub; unzoomed individuals yes, unzoomed cluster aggregates no)
- [ ] Composite edit mode verification — [composite-pins-unzoomed-plan.md](exec-plans/active/composite-pins-unzoomed-plan.md) Phase 4
- [ ] Pin rendering polish (anti-aliasing, depth sort) — [pin-rendering-improvements-plan.md](exec-plans/active/pin-rendering-improvements-plan.md)

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

- Composite pin unzoomed Phase 0 — Option A stub segment (`DefaultStubLengthPixels = 24`, screen-up); unzoomed individual markers in scope; unzoomed cluster aggregates excluded — [composite-pins-unzoomed-plan.md](exec-plans/active/composite-pins-unzoomed-plan.md) (2026-06-09)
- Composite pin head placement fix — Phases 1–3 (`local_center` anchor, pin_07 geometry); tests pass, visual OK; plan parked 2026-06-09 — [composite-pin-head-placement-fix-plan.md](exec-plans/inactive/composite-pin-head-placement-fix-plan.md)
- Composite pin core placement — Phases 1–6, saved-layout cache, common-angle verification, preview grids, and `verify.ps1` passed 2026-06-09 — [pin-parts-composite-placement-plan.md](exec-plans/completed/pin-parts-composite-placement-plan.md)
- Large-file refactoring Phases 1–4 — PinDebugger split, `MarkerPlacementOrchestrator`, `BuildApplyInstructions`, MainWindow partials (2026-06-08); see [CHANGELOG.md](../CHANGELOG.md)
- Manual layout variants — [manual-layout-variants-plan.md](exec-plans/completed/manual-layout-variants-plan.md) (2026-06-08)
- Unzoomed marker offset — fixed 2026-06-06 ([UNZOOMED_MARKER_OFFSET_ASSESSMENT.md](assessments/UNZOOMED_MARKER_OFFSET_ASSESSMENT.md))
- Security CI — [security-ci-plan.md](exec-plans/completed/security-ci-plan.md)
- Composite pin Phases 1–5 partial — see [CHANGELOG.md](../CHANGELOG.md)
