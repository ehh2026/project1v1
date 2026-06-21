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
| Unzoomed rollout | [composite-pins-unzoomed-plan.md](composite-pins-unzoomed-plan.md) | Phase 7 smoke #1–7 passed; cross-plan pan/zoom closed | #8 regression + move to `completed/` |
| Legacy removal | [remove-pins-jpg-legacy-path-plan.md](remove-pins-jpg-legacy-path-plan.md) | Not started | After unzoomed path stable |
| Rendering polish | [pin-rendering-improvements-plan.md](../completed/pin-rendering-improvements-plan.md) | Complete | Shaft visibility follow-up assessment |
| Shaft visibility | [composite-pin-shaft-visibility-plan.md](../completed/composite-pin-shaft-visibility-plan.md) | Complete | Default `outline_dark_7px` in `visual-config.json` |
| Head visibility | [pinhead-black-outline-variants-plan.md](pinhead-black-outline-variants-plan.md) | Planned | Generate 2-14px black-outline head variants and config selector |
| Seed alignment | [manual-layout-seed-alignment-plan.md](manual-layout-seed-alignment-plan.md) | In progress | Phases 1–2 shared path; Phase 3 load verify |

## Dependency order

```text
pin-parts completed baseline
  → composite-pins-unzoomed Phase 0 policy ✅
  → composite-pins-unzoomed Phases 1–4
  → remove-pins-jpg-legacy-path

manual-layout-seed-alignment Phases 1–3
  → reliable auto-seed loading at runtime

pin-rendering-improvements ✅
  → composite-pin-shaft-visibility asset-variant plan
```

## Inactive (../inactive/)

- [composite-pin-head-placement-fix-plan.md](../inactive/composite-pin-head-placement-fix-plan.md) — Phases 1–3 done; optional collar/shading/radius polish in TO_DO

## Completed (moved to ../completed/)

- [composite-pins-manual-layout-phases-plan.md](../completed/composite-pins-manual-layout-phases-plan.md) — Phases 1–4; Phase 5 delegated to manual-layout-variants
- [manual-layout-variants-plan.md](../completed/manual-layout-variants-plan.md) — variant CRUD, edit-mode UI, seed merge; manual smoke passed 2026-06-08
- [pin-parts-composite-placement-plan.md](../completed/pin-parts-composite-placement-plan.md) — extended-marker composite placement Phases 1–6; `verify.ps1` passed 2026-06-09
- [pin-rendering-improvements-plan.md](../completed/pin-rendering-improvements-plan.md) — anti-aliasing, gated pre-rasterization, and depth sorting; `verify.ps1` passed 2026-06-10
- [composite-pin-shaft-visibility-plan.md](../completed/composite-pin-shaft-visibility-plan.md) — baked shaft variants; default `outline_dark_7px`; `verify.ps1` passed 2026-06-11

## Rules

1. Update this dashboard when a child plan phase completes — do not duplicate status in `TO_DO.md`.
2. New composite-pin work gets a child plan (or extends an existing one) and a row here.
3. When a child plan finishes, move it to `../completed/` and leave a one-line stub in this file.
