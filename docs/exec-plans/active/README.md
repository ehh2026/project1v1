# Active Execution Plans

Multi-step agent work lives here with front-matter:

```yaml
---
status: active
owner: agent
started: YYYY-MM-DD
---
```

## Program dashboards

| Plan | Scope |
|------|--------|
| [composite-pins-program.md](composite-pins-program.md) | Umbrella for all composite-pin and manual-layout tracks |

## Active plans

| Plan | Scope |
|------|--------|
| [refactoring-assessment-followthrough-plan.md](refactoring-assessment-followthrough-plan.md) | Remaining [REFACTORING_ASSESSMENT.md](../../assessments/REFACTORING_ASSESSMENT.md) items (Phases 11+) |
| [composite-pins-unzoomed-plan.md](composite-pins-unzoomed-plan.md) | Roll composite pins to all individual markers + edit mode |
| [manual-layout-seed-alignment-plan.md](manual-layout-seed-alignment-plan.md) | Shared runtime/seed placement path + reliable seed loading |
| [pinhead-black-outline-variants-plan.md](pinhead-black-outline-variants-plan.md) | Generate black-outline head variants + config-gated head asset selection |
| [drawn-pin-model-separation-plan.md](drawn-pin-model-separation-plan.md) | Split drawn pins into head-only, auto-stub, and manual-layout roles |
| [manual-layout-pin-appearance-plan.md](manual-layout-pin-appearance-plan.md) | Add manual-layout pin head and drawn color override UI |
| [tuning-and-pin-render-bugfixes-plan.md](tuning-and-pin-render-bugfixes-plan.md) | Fix tuning-panel & pin-render bugs from the 2026-06-21 code review (H1/H12/H13/H14, M2–M4, cleanup) |

## Recently completed (moved to ../completed/)

- `refactoring-plan.md` — MainWindow decomposition tracker (Phases 1–10) — moved 2026-06-08
- `composite-pins-manual-layout-phases-plan.md` — Composite-pin manual-layout integration (Phases 1–4 done; Phase 5 delegated to `manual-layout-variants-plan.md`) — moved 2026-06-08
- `manual-layout-variants-plan.md` — Multiple saved layout variants per cluster + edit-mode UI — moved 2026-06-08
- `pin-parts-composite-placement-plan.md` — Extended-marker composite placement Phases 1–6 — moved 2026-06-09
- `pin-rendering-improvements-plan.md` — Anti-aliasing, gated pre-rasterization, and depth sorting — moved 2026-06-10
- `composite-pin-shaft-visibility-plan.md` — Baked shaft contrast variants; default `outline_dark_7px` — moved 2026-06-11
- `remove-pins-jpg-legacy-path-plan.md` — Removed `pins.jpg` / `ImagePinMarker`; drawn vs composite only — moved 2026-06-21
- `runtime-tuning-panel-plan.md` — Developer-only runtime panel for visual-config tuning without restart — moved 2026-06-21

Completed plans move to [../completed/](../completed/). Parked plans (core done, optional follow-ups) move to [../inactive/](../inactive/).

## Maintenance rules

1. **New multi-step work** — create a plan here with front-matter, add a row to the tables above, add one bullet to [TO_DO.md](../../TO_DO.md).
2. **Composite-pin work** — also register in [composite-pins-program.md](composite-pins-program.md); keep phase detail in child plans, not `TO_DO.md`.
3. **Plan finishes** — move file to `../completed/`, update [CHANGELOG.md](../../../CHANGELOG.md), remove or shorten the `TO_DO.md` bullet.
4. **Investigations** — live in [../../assessments/](../../assessments/); feature how-to in [../../guides/](../../guides/).
5. **Historical only** — move to [../../archive/planning/](../../archive/planning/), not `docs/` root.

Full doc model: [agent-workflows.md](../../agent-workflows.md#documentation-maintenance). Harness: `scripts/doc_gardening.py`.
