# Agent Failure Log

Living harness feedback: when an agent makes a repeatable mistake, record it here and promote a mechanical fix (test, script, or doc).

| Date | Mistake | Harness change |
|------|---------|----------------|
| 2026-06-04 | Built `Images&Content` paths inside `Views/ClusterMarker` | `GoldenPrincipleTests`, `verify_taste.py` Views path check; stamp via `ContentLoader` |
| 2026-06-04 | Map filename mismatch (`1976` vs `Extra Large`) | `Models/ContentFileNames.cs` single source of truth |
| 2026-06-04 | CI green without startup validation | `validate_startup.ps1` added to `.github/workflows/ci.yml` |

## Promotion workflow

1. Add a row to this table.
2. If the same class of mistake happens twice, update [golden-principles.md](design-docs/golden-principles.md) and add enforcement in `Tests/Architecture/` or `scripts/verify_taste.py`.
3. Link new rules from [AGENTS.md](../AGENTS.md) if agents need to discover them quickly.
