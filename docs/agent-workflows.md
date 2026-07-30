# Agent Workflows

Standard procedures for agent-driven development (Ralph Wiggum Loop).

## Core Loop

When given a task, repeat until verification passes:

1. **Orient** — Read [AGENTS.md](../AGENTS.md) → relevant [exec-plans/active/](exec-plans/active/) plan → design doc
2. **Slice** — Implement the smallest vertical slice that moves acceptance criteria forward
3. **Verify** — Run `scripts/verify.sh` (macOS/Linux) or `scripts/verify.ps1` (Windows)
4. **Review** — Re-read your diff against acceptance criteria; fix gaps yourself
5. **Record** — Update exec plan checkboxes and [CHANGELOG.md](../CHANGELOG.md)
6. **Loop** — If verification fails, diagnose, fix, return to step 3. Do not ask humans to "try harder."

The Record step includes backlog hygiene: remove completed `TO_DO.md` bullets, narrow partially completed bullets to the remaining scope, move deferred bullets to Deferred/Inactive, archive finished exec plans, and update active registries.

## When Something Fails

Ask: **What capability is missing, and how do we make it legible and enforceable?**

| Failure type | Response |
|--------------|----------|
| Test failure | Fix code; do not weaken the test without human approval |
| Layer violation | Move logic per [ARCHITECTURE.md](../ARCHITECTURE.md) remediation message |
| Taste check | Follow REMEDIATION line in script output |
| Missing context | Add doc to `docs/` and link from `AGENTS.md` or `docs/index.md` |
| Repeated mistake | Log in [agent-failures.md](agent-failures.md); promote to [golden-principles.md](design-docs/golden-principles.md) or structural test |

## PR Completion Checklist

- [ ] `scripts/verify` passes locally
- [ ] No unrelated files changed
- [ ] Exec plan updated (if task came from a plan)
- [ ] Originating `TO_DO.md` bullet removed, shortened to the remaining scope, or moved to Deferred/Inactive
- [ ] `CHANGELOG.md` entry added under `[Unreleased]` or version section
- [ ] New behavior documented in appropriate `docs/` file

## Platform Notes

| Environment | Can do | Cannot do |
|-------------|--------|-----------|
| Windows | Full verify, UI run, log query, startup validation | — |
| macOS/Linux | `dotnet build`, `dotnet test`, Python harness scripts | WPF UI, `validate_startup.ps1` step 4 against built output |

## Documentation maintenance

### Doc roles (do not duplicate across files)

| Doc | Holds | Does not hold |
|-----|-------|---------------|
| [TO_DO.md](TO_DO.md) | Short human backlog bullets + plan links | Phase checklists, acceptance criteria, implementation detail |
| [exec-plans/active/](exec-plans/active/) | Multi-step checklists, phases, acceptance criteria | Long product wishlists |
| [exec-plans/completed/](exec-plans/completed/) | Finished plans (read-only archive) | Active checkboxes |
| [exec-plans/tech-debt-tracker.md](exec-plans/tech-debt-tracker.md) | Debt IDs linking to plans | Duplicated task lists |
| [guides/](guides/) (`MANUAL_LAYOUT_EDITOR.md`, etc.) | How the app works **now** | Future work |
| [assessments/](assessments/) | Investigations and audits | Active execution checklists |
| [reference/](reference/) | Quality, reliability, security | Feature how-to |
| [archive/planning/](archive/planning/) | Historical plans | Active execution |

### When starting or finishing work

| Event | Action |
|-------|--------|
| New multi-step task | New plan in `exec-plans/active/` with YAML front-matter + row in [active/README.md](exec-plans/active/README.md) + one line in `TO_DO.md` |
| Composite-pin / manual-layout task | Also update [composite-pins-program.md](exec-plans/active/composite-pins-program.md) dashboard |
| Phase or plan completes | Check off child plan; update program dashboard if applicable; move finished plan to `exec-plans/completed/` |
| TO_DO item completed | Remove the `[x]` line from `TO_DO.md` (don't leave checked-off items); record the work in `CHANGELOG.md` under `[Unreleased]` — CHANGELOG is the canonical record of done work |
| TO_DO item partially completed | Rewrite the bullet so it names only remaining scope and points at the active plan section that still owns it |
| TO_DO item deferred | Move it to a Deferred or Inactive section with one sentence explaining why it is parked and what would make it worth reviving |
| Investigation concludes | Put conclusion at top of file in `assessments/`; stop tracking in `TO_DO.md` |
| Behavior stabilizes | Merge durable knowledge into `guides/`; archive or complete the plan |

Enforced by `scripts/doc_gardening.py` (TO_DO size cap, active-plan registry, front-matter, no active/completed duplicates). Incomplete active plans older than 30 days are a **non-blocking warning** in `verify_taste.py` / `doc_gardening.py` — agents must report those warnings to the user and ask whether to continue, park under `inactive/`, or archive under `completed/`.

### Exec plan quality

Plans must preserve modularity. Any plan that touches large files, composition roots, or shared workflows must include a **Modularity / File Size Impact** section covering:

- expected file growth and whether touched files are near the 800-line limit
- ownership boundaries and architecture layers that must not be crossed
- extraction points for focused partials, helpers, services, or pure model logic
- tests that protect the new boundary or prevent future inline growth

Do not add substantial logic inline to large orchestration files when a focused partial, helper, or service would keep responsibilities clearer. Keep `.cs` files under 800 lines.

## Doc Gardening (monthly)

- [ ] `py -3 scripts/doc_gardening.py` on Windows or `python3 scripts/doc_gardening.py` on macOS/Linux passes (links, AGENTS/TO_DO size, active plan registry, front-matter)
- [ ] `AGENTS.md` under 150 lines
- [ ] Completed exec plans moved to `exec-plans/completed/`
- [ ] [QUALITY_SCORE.md](reference/QUALITY_SCORE.md) updated
- [ ] README matches current features

## Escalate to Human When

- Product judgment required (UX tradeoffs, feature priority)
- Test failure cause is ambiguous after 3 fix attempts
- Change requires secrets, credentials, or external service setup
- Architectural decision not covered by [golden-principles.md](design-docs/golden-principles.md)
