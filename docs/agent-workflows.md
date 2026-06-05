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
- [ ] `CHANGELOG.md` entry added under `[Unreleased]` or version section
- [ ] New behavior documented in appropriate `docs/` file

## Platform Notes

| Environment | Can do | Cannot do |
|-------------|--------|-----------|
| Windows | Full verify, UI run, log query, startup validation | — |
| macOS/Linux | `dotnet build`, `dotnet test`, Python harness scripts | WPF UI, `validate_startup.ps1` step 4 against built output |

## Doc Gardening (monthly)

- [ ] `python scripts/doc_gardening.py` passes (links, AGENTS size, stale active plans)
- [ ] `AGENTS.md` under 150 lines
- [ ] Completed exec plans moved to `exec-plans/completed/`
- [ ] [QUALITY_SCORE.md](QUALITY_SCORE.md) updated
- [ ] README matches current features

## Escalate to Human When

- Product judgment required (UX tradeoffs, feature priority)
- Test failure cause is ambiguous after 3 fix attempts
- Change requires secrets, credentials, or external service setup
- Architectural decision not covered by [golden-principles.md](design-docs/golden-principles.md)
