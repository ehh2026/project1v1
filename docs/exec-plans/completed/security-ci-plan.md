---
status: completed
owner: agent
started: 2026-06-06
completed: 2026-06-06
---

# Security CI Plan — Dependabot, Secrets, NuGet Audit

Closes the gap between [docs/SECURITY.md](../../SECURITY.md) guidance (“evaluate advisories”, “never hardcode credentials”) and automated enforcement. Defers container scanners (Grype/Trivy) until the repo ships Docker or grows a large dependency graph.

**Related:** [TO_DO.md](../../TO_DO.md) → High Priority → Security & CI (complete)

---

## Goals

| Priority | Capability | Tool | Status |
|----------|------------|------|--------|
| P0 | Automated dependency update PRs | Dependabot (NuGet + GitHub Actions) | Done |
| P0 | Block committed secrets | Gitleaks in CI | Done |
| P0 | Fail CI on known-vulnerable packages | `scripts/verify_nuget_vulnerabilities.py` | Done |
| P1 | Static analysis for C# | CodeQL | Deferred |
| Defer | Container/OS CVE scan | Grype/Trivy | Deferred |
| Defer | Commercial overlap | Snyk | Deferred |

---

## Phase 0 — Baseline & false-positive triage ✅

- [x] Gitleaks: not run locally (CLI not installed); CI workflow added; no `.gitleaks.toml` required at baseline
- [x] `dotnet list InteractiveWorldMap.sln package --vulnerable --include-transitive` baseline recorded:
  - **InteractiveWorldMap:** no vulnerable packages
  - **InteractiveWorldMap.Tests (before fix):** transitive `System.Net.Http` 4.3.0 (High), `System.Text.RegularExpressions` 4.3.0 (High)
- [x] **Policy:** fail on **High** and **Critical** only (direct + transitive); Moderate/Low informational via `dotnet list` only
- [x] Remediation: pinned `System.Net.Http` 4.3.4 and `System.Text.RegularExpressions` 4.3.1 in `Tests/InteractiveWorldMap.Tests.csproj`

**Note:** `dotnet list package --vulnerable` always exits 0; harness uses `scripts/verify_nuget_vulnerabilities.py` to parse output and fail correctly.

---

## Phase 1 — Dependabot ✅

- [x] Add [`.github/dependabot.yml`](../../../.github/dependabot.yml) (NuGet `/`, NuGet `/Tests`, github-actions)
- [ ] Confirm Dependabot enabled in GitHub repo settings (Settings → Code security → Dependabot) — **human step after push**
- [ ] After first PRs: verify `.\scripts\verify.ps1` on a sample Dependabot branch — **when PRs appear**

---

## Phase 2 — Gitleaks in CI ✅

- [x] Add [`.github/workflows/gitleaks.yml`](../../../.github/workflows/gitleaks.yml) — `ubuntu-latest`, `fetch-depth: 0`, `gitleaks/gitleaks-action@v2`
- [x] No `.gitleaks.toml` at baseline
- [x] Document remediation in [docs/SECURITY.md](../../SECURITY.md)

**Org repos:** add `GITLEAKS_LICENSE` secret from https://gitleaks.io if the workflow requires it.

---

## Phase 3 — NuGet vulnerability gate ✅

- [x] CI step after restore in [`.github/workflows/ci.yml`](../../../.github/workflows/ci.yml)
- [x] Same check in [`scripts/verify.ps1`](../../../scripts/verify.ps1) and [`scripts/verify.sh`](../../../scripts/verify.sh)
- [x] [AGENTS.md](../../../AGENTS.md) merge-gate bullet updated

---

## Phase 4 — CodeQL (deferred)

Deferred per plan — Phases 1–3 deliver sufficient value for this repo size.

---

## Phase 5 — Harness alignment (partial)

- [ ] Consider delegating CI steps to single `.\scripts\verify.ps1` — not done (explicit steps kept for clarity)
- [ ] NuGet cache in Windows CI — optional performance follow-up
- [x] [CHANGELOG.md](../../../CHANGELOG.md) updated
- [x] Plan moved to [docs/exec-plans/completed/](../completed/)
- [x] TO_DO item marked complete

---

## Verification checklist

- [x] `scripts/verify_nuget_vulnerabilities.py` passes locally after transitive pin
- [x] `.\scripts\verify.ps1` passes on Windows with .NET 6 SDK
- [x] [docs/SECURITY.md](../../SECURITY.md) and [AGENTS.md](../../../AGENTS.md) updated
- [ ] Dependabot PR merges cleanly — after first Dependabot PR
- [ ] Gitleaks workflow green on `main` — after push to GitHub

---

## Rollback

- Remove or disable individual workflows / Dependabot config
- Remove NuGet step from CI and verify scripts
- Remove test project package pins if audit gate is removed
- No app runtime behavior changes — harness-only
