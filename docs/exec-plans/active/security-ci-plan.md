---
status: active
owner: agent
started: 2026-06-06
---

# Security CI Plan — Dependabot, Secrets, NuGet Audit

Closes the gap between [docs/SECURITY.md](../../SECURITY.md) guidance (“evaluate advisories”, “never hardcode credentials”) and automated enforcement. Defers container scanners (Grype/Trivy) until the repo ships Docker or grows a large dependency graph.

**Related:** [TO_DO.md](../../TO_DO.md) → High Priority → Security & CI

---

## Goals

| Priority | Capability | Tool |
|----------|------------|------|
| P0 | Automated dependency update PRs | Dependabot (NuGet + GitHub Actions) |
| P0 | Block committed secrets | Gitleaks in CI |
| P0 | Fail CI on known-vulnerable packages | `dotnet list package --vulnerable` |
| P1 | Static analysis for C# | CodeQL (optional fourth phase) |
| Defer | Container/OS CVE scan | Grype/Trivy — no containers today |
| Defer | Commercial overlap | Snyk — Dependabot + dotnet audit sufficient for now |

---

## Current state

- [`.github/workflows/ci.yml`](../../../.github/workflows/ci.yml): build, test, doc links, taste, headless startup on `windows-latest`
- [`.github/workflows/doc-gardening.yml`](../../../.github/workflows/doc-gardening.yml): weekly doc drift
- **No** `.github/dependabot.yml`
- **No** secret scanning in CI (Cursor/CodeGuard rules only)
- **No** NuGet vulnerability gate
- Runtime deps: `Newtonsoft.Json` 13.0.3; test deps: xUnit, Microsoft.NET.Test.Sdk, coverlet

---

## Phase 0 — Baseline & false-positive triage

- [ ] Run Gitleaks locally against full repo history (or `gitleaks detect --source . --verbose`) and record any findings
- [ ] Run `dotnet list InteractiveWorldMap.sln package --vulnerable --include-transitive` locally; record baseline
- [ ] Decide policy: fail on **High/Critical** only vs any reported vulnerability
- [ ] If baseline secrets exist: rotate/remediate or add narrow `.gitleaks.toml` allowlist with documented justification (prefer fixing over allowlisting)

**Acceptance:** Written baseline in this plan (findings count + policy); no surprise CI red on first merge.

---

## Phase 1 — Dependabot

- [ ] Add `.github/dependabot.yml`:

```yaml
version: 2
updates:
  - package-ecosystem: nuget
    directory: /
    schedule:
      interval: weekly
    open-pull-requests-limit: 5

  - package-ecosystem: nuget
    directory: /Tests
    schedule:
      interval: weekly
    open-pull-requests-limit: 5

  - package-ecosystem: github-actions
    directory: /
    schedule:
      interval: weekly
    open-pull-requests-limit: 3
```

- [ ] Confirm Dependabot is enabled in GitHub repo settings (Settings → Code security → Dependabot)
- [ ] After first PRs: verify `.\scripts\verify.ps1` still passes on a sample Dependabot branch

**Acceptance:** Dependabot opens PRs for NuGet and `actions/*`; merge gate unchanged.

**Notes:** No pip ecosystem — Python harness scripts use stdlib only.

---

## Phase 2 — Gitleaks in CI

- [ ] Add `.github/workflows/gitleaks.yml` (or a `security` job in `ci.yml` on `ubuntu-latest` for speed):

  - Trigger: `push` + `pull_request` to `main` / `master`
  - Use official `gitleaks/gitleaks-action` (pin major version) **or** install pinned Gitleaks binary
  - `fetch-depth: 0` if scanning full history on default branch; `fetch-depth: 2` sufficient for PR diff-only mode
  - Fail workflow on leak detection (`continue-on-error: false`)

- [ ] Optional: add `.gitleaks.toml` only if Phase 0 requires targeted allowlists (e.g. known test fixtures)

- [ ] Document remediation in [docs/SECURITY.md](../../SECURITY.md): “CI runs Gitleaks; never commit secrets; use env/external config”

**Acceptance:** CI fails when a test secret is committed on a branch; clean main stays green.

---

## Phase 3 — NuGet vulnerability gate

- [ ] Add CI step after `dotnet restore` in [`.github/workflows/ci.yml`](../../../.github/workflows/ci.yml):

```powershell
dotnet list InteractiveWorldMap.sln package --vulnerable --include-transitive
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
```

- [ ] Mirror the same check in [`scripts/verify.ps1`](../../../scripts/verify.ps1) (new step `[n/7]` or fold into restore/build section) so local and CI stay aligned
- [ ] Optionally add `dotnet list InteractiveWorldMap.sln package --outdated` as **non-blocking** informational step (log only) or weekly Dependabot substitute check

- [ ] Update [AGENTS.md](../../../AGENTS.md) merge-gate bullet to mention vulnerability scan

**Acceptance:** Introducing a package with a known high-severity advisory fails `verify.ps1` and CI; current graph passes.

**Notes:** Prefer native `dotnet list` over Grype for this repo size. Revisit Grype if adding container images or installer bundling.

---

## Phase 4 — CodeQL (optional, P1)

- [ ] Add `.github/workflows/codeql.yml` using GitHub’s `github/codeql-action` init/analyze for `csharp`
- [ ] Run on `ubuntu-latest` (CodeQL C# does not require WPF build for analysis) **or** extend `windows-latest` job if analysis quality requires it
- [ ] Triage initial findings; fix or dismiss with documented reason
- [ ] Enable default setup in repo Security tab if using GitHub’s guided CodeQL onboarding

**Acceptance:** CodeQL runs on PRs; no unreviewed Critical findings on main.

**Defer if:** Private repo without CodeQL minutes / team prefers minimal CI surface — Phases 1–3 still deliver most value.

---

## Phase 5 — Harness alignment & close-out

- [ ] Consider delegating CI build/test/harness steps to a single `.\scripts\verify.ps1` invocation to prevent drift with [ci.yml](../../../.github/workflows/ci.yml)
- [ ] Add NuGet cache to Windows CI (`actions/cache` on `~/.nuget/packages`) — performance only
- [ ] Update [CHANGELOG.md](../../../CHANGELOG.md) under `[Unreleased]` when Phases 1–3 land (harness/minor semver note)
- [ ] Move this plan to [docs/exec-plans/completed/](../completed/) when all P0 phases done
- [ ] Mark TO_DO item complete

**Acceptance:** `.\scripts\verify.ps1` and CI cover the same security checks; docs point to the new workflows.

---

## Explicitly out of scope (for now)

| Item | Reason |
|------|--------|
| Grype / Trivy | No Dockerfiles or release images |
| Snyk | Overlaps Dependabot + dotnet audit |
| SBOM (Syft/CycloneDX) | Single runtime NuGet; add when compliance requires |
| Pre-commit hooks | CI + Dependabot sufficient unless human commits bypass CI |
| GitHub Secret Scanning push protection | Enable in repo settings if available; complements Gitleaks, not a substitute for Phase 2 in private repos |

---

## Verification checklist (final)

- [ ] Dependabot PR merges cleanly through existing CI
- [ ] Gitleaks workflow green on `main`
- [ ] `dotnet list … --vulnerable` green on `main`
- [ ] `.\scripts\verify.ps1` passes on Windows with .NET 6 SDK
- [ ] [docs/SECURITY.md](../../SECURITY.md) and [AGENTS.md](../../../AGENTS.md) updated

---

## Rollback

- Remove or disable individual workflows / Dependabot config
- Vulnerability gate: remove CI step and verify.ps1 step
- No app runtime behavior changes — harness-only
