---
status: active
owner: agent
started: 2026-06-08
role: program-dashboard
---

# Composite Pins Program

Coordinates all composite-pin and manual-layout execution plans. Child plans hold phases and acceptance criteria; this file is the status dashboard only.

Backlog links: [TO_DO.md](../../TO_DO.md) · Feature doc: [MANUAL_LAYOUT_EDITOR.md](../../guides/MANUAL_LAYOUT_EDITOR.md)

## Status dashboard

| Track | Plan | Status | Next action |
|-------|------|--------|-------------|
| Core placement | [pin-parts-composite-placement-plan.md](../completed/pin-parts-composite-placement-plan.md) | Complete | Baseline for unzoomed rollout and legacy removal |
| Unzoomed rollout | [composite-pins-unzoomed-plan.md](composite-pins-unzoomed-plan.md) | Phases 1–3 done | Phase 4 — composite edit mode verification |
| Legacy removal | [remove-pins-jpg-legacy-path-plan.md](remove-pins-jpg-legacy-path-plan.md) | Not started | After unzoomed path stable |
| Rendering polish | [pin-rendering-improvements-plan.md](pin-rendering-improvements-plan.md) | Parts 1A–2 done | Visual review; optional pre-rasterization (Part 1B) only if needed |
| Seed alignment | [manual-layout-seed-alignment-plan.md](manual-layout-seed-alignment-plan.md) | In progress | Phases 1–2 shared path; Phase 3 load verify |

## Dependency order

```text
pin-parts completed baseline
  → composite-pins-unzoomed Phase 0 policy ✅
  → composite-pins-unzoomed Phases 1–4
  → remove-pins-jpg-legacy-path

manual-layout-seed-alignment Phases 1–3
  → reliable auto-seed loading at runtime

pin-rendering-improvements
  → independent, lower priority
```

## Inactive (../inactive/)

- [composite-pin-head-placement-fix-plan.md](../inactive/composite-pin-head-placement-fix-plan.md) — Phases 1–3 done; optional collar/shading/radius polish in TO_DO

## Completed (moved to ../completed/)

- [composite-pins-manual-layout-phases-plan.md](../completed/composite-pins-manual-layout-phases-plan.md) — Phases 1–4; Phase 5 delegated to manual-layout-variants
- [manual-layout-variants-plan.md](../completed/manual-layout-variants-plan.md) — variant CRUD, edit-mode UI, seed merge; manual smoke passed 2026-06-08
- [pin-parts-composite-placement-plan.md](../completed/pin-parts-composite-placement-plan.md) — extended-marker composite placement Phases 1–6; `verify.ps1` passed 2026-06-09

## Rules

1. Update this dashboard when a child plan phase completes — do not duplicate status in `TO_DO.md`.
2. New composite-pin work gets a child plan (or extends an existing one) and a row here.
3. When a child plan finishes, move it to `../completed/` and leave a one-line stub in this file.
