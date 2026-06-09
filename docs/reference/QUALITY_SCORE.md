# Quality Score

Grades track harness maturity and code health. Update after each harness phase or major feature.

**Last updated:** 2026-06-04 (harness enforcement hardening)

| Domain | Grade | Gaps | Next action |
|--------|-------|------|-------------|
| Models | A | Some WPF types in Models (Point, etc.) | Acceptable for WPF; monitor |
| Services | A- | ContentFileNames + ContentLoader path helpers | Add integration tests |
| Views | B- | MainWindow god object; no FlaUI yet | Extract services from MainWindow |
| Tests | B+ | Golden principle + strengthened layer tests | Add FsCheck property tests |
| Docs | A | agent-failures log, weekly doc-gardening CI | Archive stale planning docs |
| Harness | B+ | CI includes startup validation; golden rules mechanical | Add FlaUI smoke tests (Windows) |

## Grading Scale

- **A** — Enforced mechanically, well-tested, agent-legible
- **B** — Documented and mostly consistent; gaps are known
- **C** — Functional but drift risk; needs investment
- **D** — Missing or broken; blocks agent autonomy
- **F** — Not present

## Update Cadence

1. After completing an exec plan milestone
2. After a harness phase ships
3. Monthly doc-gardening (see [agent-workflows.md](../agent-workflows.md))

## History

| Date | Harness | Tests | Docs | Notes |
|------|---------|-------|------|-------|
| 2026-06-04 | B→B+ | B-→B+ | A-→A | Golden principle tests, CI startup validation, ContentFileNames |
| 2026-06-04 | D→B | C→B- | B→A- | Initial harness engineering rollout |
