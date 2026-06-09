# Interactive World Map — Backlog

Human steering list. Implementation detail lives in [exec-plans/active/](exec-plans/active/). Composite-pin work is coordinated in [composite-pins-program.md](exec-plans/active/composite-pins-program.md).

**Last updated:** June 8, 2026

---

## Composite pins & manual layouts

Dashboard: [composite-pins-program.md](exec-plans/active/composite-pins-program.md)

- [ ] Fix or remove composite pins that overstretch shadow
- [ ] Shared runtime/seed placement path — [manual-layout-seed-alignment-plan.md](exec-plans/active/manual-layout-seed-alignment-plan.md)
- [ ] Reliable seed loading in app — same plan, Phase 3
- [ ] Remove `pins.jpg` / `ImagePinMarker` — [remove-pins-jpg-legacy-path-plan.md](exec-plans/active/remove-pins-jpg-legacy-path-plan.md)
- [ ] Composite placement caching for saved layouts — [pin-parts-composite-placement-plan.md](exec-plans/active/pin-parts-composite-placement-plan.md)
- [ ] Non-extended pin rendering policy — [composite-pins-unzoomed-plan.md](exec-plans/active/composite-pins-unzoomed-plan.md) Phase 0
- [ ] Composite pins on all individual markers — same plan, Phases 2–3
- [ ] Phase 6 verification — [pin-parts-composite-placement-plan.md](exec-plans/active/pin-parts-composite-placement-plan.md)
- [ ] Composite edit mode verification — [composite-pins-unzoomed-plan.md](exec-plans/active/composite-pins-unzoomed-plan.md) Phase 4
- [ ] Multiple layout variants per cluster — [manual-layout-variants-plan.md](exec-plans/active/manual-layout-variants-plan.md) (Phases 1–4 done; remaining polish/verification)
- [ ] Head placement fix (pin_07 recalibration) — [composite-pin-head-placement-fix-plan.md](exec-plans/active/composite-pin-head-placement-fix-plan.md)
- [ ] Pin rendering polish (anti-aliasing, depth sort) — [pin-rendering-improvements-plan.md](exec-plans/active/pin-rendering-improvements-plan.md)

## Refactoring & quality

- [ ] Refactoring assessment follow-through — [refactoring-assessment-followthrough-plan.md](exec-plans/active/refactoring-assessment-followthrough-plan.md)
- [ ] Resolve nullable reference warnings (CS8602/CS8604) — Phase 13 in same plan
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

- Unzoomed marker offset — fixed 2026-06-06 ([UNZOOMED_MARKER_OFFSET_ASSESSMENT.md](assessments/UNZOOMED_MARKER_OFFSET_ASSESSMENT.md))
- Security CI — [security-ci-plan.md](exec-plans/completed/security-ci-plan.md)
- Composite pin Phases 1–5 partial; manual-layout variants Phases 1–4 — see [CHANGELOG.md](../CHANGELOG.md)
