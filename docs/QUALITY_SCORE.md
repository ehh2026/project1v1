# Quality Score

Grades track harness maturity and code health. Update after each harness phase or major feature.

**Last updated:** 2026-06-04 (harness engineering implementation)

| Domain | Grade | Gaps | Next action |
|--------|-------|------|-------------|
| Models | A | Some WPF types in Models (Point, etc.) | Acceptable for WPF; monitor |
| Services | B+ | ContentLoader/StartupValidator tests added | Add integration tests |
| Views | C | Large code-behind, no UI automation | Extract services from MainWindow |
| Tests | B- | Architecture tests + P0 service tests | Add FsCheck property tests |
| Docs | A- | Index and harness docs added | Monthly doc-gardening |
| Harness | B | CI, verify scripts, cursor rules | Add FlaUI smoke tests (Windows) |

## Grading Scale

- **A** — Enforced mechanically, well-tested, agent-legible
- **B** — Documented and mostly consistent; gaps are known
- **C** — Functional but drift risk; needs investment
- **D** — Missing or broken; blocks agent autonomy
- **F** — Not present

## Update Cadence

1. After completing an exec plan milestone
2. After a harness phase ships
3. Monthly doc-gardening (see [agent-workflows.md](agent-workflows.md))

## History

| Date | Harness | Tests | Docs | Notes |
|------|---------|-------|------|-------|
| 2026-06-04 | D→B | C→B- | B→A- | Initial harness engineering rollout |
