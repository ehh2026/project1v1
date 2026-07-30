# Exec Plan: Complexity Refactoring + Hooks/CI

> **Branch:** `complexity-and-ci-hooks`
> **Status:** active
> **Created:** 2026-07-29
> **Last reviewed:** 2026-07-30

## Goal

Add Roslyn analyzers, pre-commit formatting enforcement, coverage gating, Lizard complexity CI, and refactor the 10 worst cyclomatic-complexity offenders.

## Current State

- .NET 6 SDK pinned (`global.json` 6.0.400, `latestPatch` roll-forward)
- No `Directory.Build.props`, no Roslyn analyzers, no `.runsettings`
- Pre-push hook only (opt-in). No pre-commit hook.
- `dotnet format` never enforced anywhere
- Test coverage: 41.5% line / 36.4% branch (677 tests pass) — gates at 42%/37%
- Lizard results: 10 methods with CCN > 15 (worst: `TryValidate` CCN 58)
- CI: 4 GitHub Actions workflows (build/test, secrets, advisory health, doc gardening)
- Language version: C# 10.0 (no C# 11+ features)

---

## Phase 0: Formatting Baseline & Pre-Verification

Establish a clean formatting baseline and perform sanity audits *before* any refactoring commits.

### 0a. Codebase Formatting Baseline

Run `dotnet restore` followed by `dotnet format` to resolve existing formatting violations across the entire solution.

*Important: Commit and merge this baseline pass immediately as a standalone PR to minimize merge conflicts with other feature branches. Formatting test files and `Tools/` files is safe — these are text-only changes that don't affect XAML compilation, test discovery, or tool behavior.*

```powershell
dotnet restore InteractiveWorldMap.sln
dotnet format InteractiveWorldMap.sln
git add -A
git commit -m "style: format entire codebase for dotnet format enforcement"
```

### 0b. Pre-Promotion Advisory Audit

Run the current `advisory-code-health.yml` workflow on the main branch prior to making changes to identify any pre-existing failures before promoting it to blocking.

---

### Phase 0 Review (subagent)

After Phase 0 commits land, launch a subagent to verify:

1. `dotnet format --verify-no-changes` passes on the entire solution
2. `dotnet build` succeeds with no new warnings
3. `dotnet test` passes (no regressions from formatting)
4. Git diff shows only whitespace/formatting changes (no logic changes)

---

## Phase 1: Complexity Refactoring

The 10 target methods must be refactored to reduce CCN below 15 **before** blocking hooks and CI gates are installed. Each refactoring = 1 commit. Run `dotnet test` after each commit.

### Coverage Comparison Protocol

Before starting refactoring, capture a coverage baseline:

```powershell
dotnet test Tests/InteractiveWorldMap.Tests.csproj --settings .runsettings --collect:"XPlat Code Coverage"
# Record the line/branch rates from TestResults/ as the pre-refactoring baseline
```

After each refactoring commit, run coverage again and compare. If line or branch coverage drops by more than 0.5%, investigate whether the refactoring shifted branch attribution. If degradation is real, adjust the refactoring to preserve coverage.

### Coverage Bridging

If after completing all 10 refactors the combined coverage is still below 42% line / 37% branch, add targeted tests to close the gap before Phase 2. Focus on:

1. **Uncovered branches in refactored helpers** — extraction often reveals conditional paths that existing tests never exercised
2. **Newly extracted methods** — each helper (e.g., `AdjustAnglePair`, `HasIntersectionOrClosePass`, `BuildBioDictionary`) is a testable unit
3. **Boundary/edge cases in validation logic** — Target 7 (`ValidateLocationsJson`) and Target 1 (`TryValidate`) have coordinate boundary checks and field-presence guards with likely zero-coverage edge cases

Estimated gap: ~0.5% line = roughly 3-5 new test cases. Do not write broad integration tests to pad coverage.

### Architectural Constraints & Safeguards

1. **Views Layer Independence (Critical)**:
   `DeveloperTuningPanel.xaml.cs` is in the `Views` layer. `Tests/Architecture/LayerDependencyTests.cs` forbids `Views` from referencing `Services` or `Utilities` namespaces.
   * **Rule**: Extracted helpers, validation descriptors, or delegates for Targets 1 & 2 must remain in `Views`. No exports to shared services/utilities.

2. **Property Binding Contracts**:
   `Tests/TuningPanelWiringTests.cs` verifies XAML element existence and source-text string presence (static text-scan tests, not runtime binding tests). There is **no automated test** for the property-to-XAML binding contract. When refactoring Targets 1 & 2, manually verify that `TuningPanelEventArgs` property names are not renamed and XAML bindings remain intact.

3. **XAML Binding Verification**:
   After refactoring Targets 1 & 2, run `TuningReloadValidationTests.cs` and `validate_startup.ps1`. Check logs for `System.Windows.Data Warning` (binding errors).

4. **Git Rollback & Rebase Strategy**:
   Work on an isolated branch and rebase regularly onto `main`. If a refactoring introduces a regression, `git revert` the commit.
   * **Cascading revert warning**: Targets 3, 4, 9, 10 all modify `RadialExtensionAdjuster.cs`. Each extraction must be **independent** (no cross-calling between targets) so reverting any single commit doesn't break others. Do not call helpers from Target 3 inside Target 4, etc.

5. **C# 10 Language Level**:
   Do not use C# 11+ features. `<LangVersion>10.0</LangVersion>` is enforced in `Directory.Build.props` (Phase 2b).

### Target 1 Spike

`TryValidate` has CCN 58 — the table-driven approach may not get it below 15 if validation rules have complex interdependencies. **Prototype Target 1 as a spike commit first.** If CCN stays above 15 after extraction, document the limit and adjust the acceptance criteria ("as close as feasible").

### Target Methods & Refactoring Approaches

| # | Method | File | Current CCN | Refactoring Approach |
|---|---|---|---|---|
| 1 | `TryValidate` | `Views/DeveloperTuningPanel.xaml.cs` | 58 | Table-driven validation: extract `ValidateField` helper + static validation rules list. **Spike first.** All extracted code stays in `Views`. |
| 2 | `TryBuildEventArgs` | `Views/DeveloperTuningPanel.xaml.cs` | 35 | Extract category blocks into helpers (`TryReadMapTuning`, `TryReadPopupStyle`) within code-behind. |
| 3 | `AdjustAnglesWithinGroups` | `Services/RadialExtensionAdjuster.cs` | 24 | Extract `AdjustAnglePair` helper. **Independent — no cross-target calls.** |
| 4 | `FixLineIntersections` | `Services/RadialExtensionAdjuster.cs` | 20 | Extract `HasIntersectionOrClosePass` and `TryApplyRotationStrategy`. **Independent — own helpers only.** |
| 5 | `Compute` | `Services/MarkerPlacementOrchestrator.cs` | 18 | Break into sub-methods per placement mode (e.g., `ComputeExtensionPlacements`). |
| 6 | `BuildApplyInstructions` | `Services/CompositePinApplicationService.cs` | 17 | Extract inner loop body into `BuildInstruction` helper. No implicit side effects on loop-scoped variables. |
| 7 | `ValidateLocationsJson` | `Services/StartupValidator.cs` | 17 | Table-driven required field checks + boundary validation helper. |
| 8 | `ReadLocationsFromExcel` | `Utilities/ExcelCoordinateReader.cs` | 16 | Extract `BuildBioDictionary` and header detection. ILogger: keep as standard DI (not a smell), only extract if it simplifies the method. |
| 9 | `AdjustExtensions` | `Services/RadialExtensionAdjuster.cs` | 16 | Extract `ResolveOscillatingPairs` and `AnalyzeAndLogFinalSeparation`. **Independent — own helpers only.** |
| 10 | `AdjustForMarkerOverlaps` | `Services/RadialExtensionAdjuster.cs` | 16 | Relocate verbose logging into sub-helpers. **Independent — own helpers only.** |

*Note on RadialExtensionAdjuster targets (3, 4, 9, 10): Refactor sequentially (3→4→9→10) to avoid merge conflicts. Each extraction must be independent — no target calls another target's helpers.*

---

### Phase 1 Review (subagent)

After Phase 1 commits land, launch a subagent to verify:

1. Lizard reports no methods with CCN > 20: `lizard -C 20 -x "./Tests/*" -x "./Tools/*" -x "./bin/*" -x "./obj/*" -x "./scripts/venv/*" -x "./TestResults/*" .`
2. `dotnet test` passes with no regressions
3. Coverage at or above 42% line / 37% branch (run with `--settings .runsettings --collect:"XPlat Code Coverage"`)
4. Each RadialExtensionAdjuster target's helpers are independent (no cross-calls between targets 3, 4, 9, 10)
5. No C# 11+ features in any refactored code

---

## Phase 2: CI/Hooks Infrastructure

After complexity refactoring is merged (all methods below CCN 15, coverage at 42%+), introduce blocking infrastructure.

### 2a. Line Ending Normalization (`.gitattributes`)

Force LF line endings on hook scripts and tooling to prevent CRLF shebang breaks on Windows:

```gitattributes
*.sh text eol=lf
*.ps1 text eol=lf
scripts/*.py text eol=lf
.githooks/* text eol=lf
```

### 2b. `Directory.Build.props` (new file, repo root)

```xml
<Project>
  <PropertyGroup>
    <EnableNETAnalyzers>true</EnableNETAnalyzers>
    <AnalysisLevel>latestRecommended</AnalysisLevel>
    <EnforceCodeStyleInBuild>true</EnforceCodeStyleInBuild>
    <LangVersion>10.0</LangVersion>
  </PropertyGroup>
</Project>
```

**Rollback plan:** If `latestRecommended` causes build failures that can't be quickly resolved, temporarily set `<AnalysisLevel>5.0</AnalysisLevel>` (the exact version for .NET 6 SDK) and address warnings incrementally.

**Verification:** Run `dotnet build` immediately after creating this file. If `EnforceCodeStyleInBuild` causes failures from pre-existing violations, run `dotnet format` again and commit the fixes before proceeding.

### 2c. `.runsettings` (new file, repo root)

```xml
<RunSettings>
  <DataCollectionRunSettings>
    <DataCollectors>
      <DataCollector friendlyName="XPlat Code Coverage">
        <Configuration>
          <Exclude>[Tests]*,[ManualLayoutSeedGenerator]*,[ThumbnailTouchSmoke]*</Exclude>
          <Include>[InteractiveWorldMap]*</Include>
        </Configuration>
      </DataCollector>
    </DataCollectors>
  </DataCollectionRunSettings>
</RunSettings>
```

*Note: Coverlet's VSTest integration ignores threshold config in `.runsettings`. Threshold gating is handled by `summarize_coverage.py`.*

### 2d. Coverage Threshold Gating

Extend `scripts/summarize_coverage.py` to support `--min-line-coverage` and `--min-branch-coverage` arguments. The existing `parse_args()` has a positional `path` argument — extend it with optional threshold arguments while keeping backward compatibility.

```python
# Integration with existing parse_args():
def parse_args(argv: list[str]) -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Summarize Cobertura coverage output.")
    parser.add_argument("path", nargs="?", type=Path, default=REPO_ROOT / "TestResults")
    parser.add_argument("--min-line-coverage", type=float, default=None)
    parser.add_argument("--min-branch-coverage", type=float, default=None)
    return parser.parse_args(argv)

# In main(), after building summary:
def check_thresholds(args, summary: str) -> int:
    """Parse line/branch rates from summary text and compare against thresholds."""
    # Extract rates from the markdown summary or re-parse Cobertura XML
    # Exit 1 if any threshold is breached
    ...
```

Gate: exit code `1` if line coverage < 42% or branch coverage < 37%.

### 2e. `.githooks/pre-commit` (new file)

```sh
#!/bin/sh
set -e
dotnet format InteractiveWorldMap.sln --verify-no-changes
```

This enforces formatting only. Roslyn analyzer violations are caught by CI and by the pre-push build check (2f).

### 2f. Pre-push Hook Update

Add a build check to the existing `.githooks/pre-push` hook (alongside the existing doc-link, taste, and advisory-code-health steps). This catches analyzer warnings locally without slowing down every commit:

```sh
echo "Running build check..."
dotnet build InteractiveWorldMap.sln --configuration Release --no-restore --no-incremental 2>&1
if [ $? -ne 0 ]; then
  echo "Build failed. Fix compilation errors before pushing."
  exit 1
fi
```

### 2g. `install_git_hooks.ps1` Update

Keep the script focused. Add only: verify `.githooks/pre-commit` exists, verify `core.hooksPath` is set, print status. Do **not** add Git Bash `/usr/bin/sh` accessibility checks — that's a system-level concern outside this script's scope.

```powershell
# Add after the existing core.hooksPath setup:
$hooks = @("pre-push", "pre-commit")
foreach ($hook in $hooks) {
    $path = Join-Path $repoRoot ".githooks/$hook"
    if (Test-Path $path) {
        Write-Host "  [OK] .githooks/$hook exists"
    } else {
        Write-Warning "  [MISSING] .githooks/$hook — hooks may not run"
    }
}
$currentHooksPath = git config core.hooksPath
if ($currentHooksPath -eq ".githooks") {
    Write-Host "  [OK] core.hooksPath = .githooks"
} else {
    Write-Warning "  [WARN] core.hooksPath = '$currentHooksPath' (expected '.githooks')"
}
```

### 2h. CI Workflow Updates

**`ci.yml`:**
- Add `dotnet format InteractiveWorldMap.sln --verify-no-changes` step after build.
- Add `--settings .runsettings` to `dotnet test` step.
- Add Lizard warning + fail steps:
  ```yaml
  - name: Install Lizard
    run: pip3 install lizard
  - name: Lizard complexity warning
    run: lizard -C 15 -x "./Tests/*" -x "./Tools/*" -x "./bin/*" -x "./obj/*" -x "./scripts/venv/*" -x "./TestResults/*" .
    continue-on-error: true
  - name: Lizard complexity fail
    run: lizard -C 20 -x "./Tests/*" -x "./Tools/*" -x "./bin/*" -x "./obj/*" -x "./scripts/venv/*" -x "./TestResults/*" .
  ```
  *Note: Lizard's `-x` uses Python `fnmatch`. `*` does **not** match `/`. `./Tests/*` only excludes files directly in `./Tests/`, not subdirectories. Use multiple `-x` entries or verify with `lizard --help` on the CI runner. If subdirectory exclusion is needed, add explicit patterns like `-x "./Tests/Architecture/*" -x "./Tests/Services/*"`.*

**`advisory-code-health.yml`:**
- Keep job-level `continue-on-error: true` (advisory report + summary remain non-blocking).
- Add Lizard warning step with `continue-on-error: true`.
- Add Lizard fail step with `continue-on-error: false` (blocking).
- Add coverage threshold enforcement with `continue-on-error: false` (blocking):
  ```yaml
  - name: Enforce coverage threshold
    run: python3 scripts/summarize_coverage.py --min-line-coverage 42 --min-branch-coverage 37
  ```

### 2i. Verify Scripts Updates

Add new steps to the existing verify scripts. The scripts are restructured to accommodate the new steps.

**PowerShell (`verify.ps1`)** — add after step [4/8]:

```powershell
Write-Host "[4b/10] code formatting check"
dotnet format InteractiveWorldMap.sln --verify-no-changes
if ($LASTEXITCODE -ne 0) { Write-Error "Formatting verification failed."; exit 1 }

Write-Host "[4c/10] coverage threshold gate"
Invoke-HarnessPython "scripts/summarize_coverage.py --min-line-coverage 42 --min-branch-coverage 37"
if ($LASTEXITCODE -ne 0) { Write-Error "Coverage gates failed."; exit 1 }
```

*Note: Steps renumber from [5/8] to [5/10] through [10/10] for the remaining steps.*

**Bash (`verify.sh`)** — add conditional coverage gate (harness-only mode skips if no coverage file):

```bash
if [[ "$RUN_DOTNET" == true ]]; then
  echo "[4b/8] code formatting check"
  dotnet format InteractiveWorldMap.sln --verify-no-changes
  [ $? -eq 0 ] || { echo "Formatting verification failed."; exit 1; }

  echo "[4c/8] coverage threshold gate"
  if [ -f "TestResults/coverage.cobertura.xml" ]; then
    python3 scripts/summarize_coverage.py --min-line-coverage 42 --min-branch-coverage 37
    [ $? -eq 0 ] || { echo "Coverage gates failed."; exit 1; }
  else
    echo "SKIP: No coverage file found (harness-only mode)."
  fi
fi
```

---

### Phase 2 Review (subagent)

After Phase 2 commits land, launch a subagent to verify:

1. `.\scripts\verify.ps1` passes on Windows (full mode with coverage)
2. `./scripts/verify.sh` passes in harness-only mode (no coverage gate failure)
3. `dotnet format --verify-no-changes` passes
4. `.githooks/pre-commit` and `.githooks/pre-push` exist and are executable
5. `git config core.hooksPath` returns `.githooks`
6. `Directory.Build.props` exists with correct properties
7. `.runsettings` exists with correct exclusion patterns
8. CI workflows are syntactically valid YAML

---

## Phase 3: Documentation

- `CHANGELOG.md` — `[Unreleased]` entry for format enforcement, analyzers, coverage gate, Lizard CI, and complexity refactoring.
- `scripts/README.md` — document new `summarize_coverage.py` arguments (`--min-line-coverage`, `--min-branch-coverage`, `--coverage-file`) and the updated `install_git_hooks.ps1` behavior.
- `AGENTS.md` — update quick commands:
  ```markdown
  # Build and test (run before claiming work is done)
  ./scripts/verify.sh          # macOS / Linux (build + test + harness checks)
  .\scripts\verify.ps1         # Windows (full verification)
  .\scripts\verify_manual_layout_seeds.ps1 # Windows: seed generator/load sanity check

  # Manual steps
  dotnet build InteractiveWorldMap.sln
  dotnet test Tests/InteractiveWorldMap.Tests.csproj --settings .runsettings
  dotnet run --project InteractiveWorldMap.csproj   # Windows UI only
  .\run-demo.bat                                    # Windows: build + launch
  .\scripts\validate_startup.ps1                  # Headless startup check (Windows)

  # Formatting verification
  dotnet format InteractiveWorldMap.sln --verify-no-changes

  # Coverage threshold gate
  python3 scripts/summarize_coverage.py --min-line-coverage 42 --min-branch-coverage 37
  ```

---

### Phase 3 Review (subagent)

After Phase 3 commits land, launch a subagent to verify:

1. `CHANGELOG.md` has an `[Unreleased]` entry covering all changes
2. `scripts/README.md` documents new `summarize_coverage.py` arguments
3. `AGENTS.md` quick commands include `--settings .runsettings` and coverage gate commands
4. No stale or contradictory documentation remains

---

## Acceptance Criteria

- [ ] `dotnet format` run on entire codebase (committed before hook wiring)
- [ ] `.\scripts\verify.ps1` passes (includes format verification and coverage thresholds)
- [ ] `./scripts/verify.sh` passes in harness-only mode (no coverage gate failure)
- [ ] `dotnet format --verify-no-changes` passes
- [ ] `dotnet test` passes with 42%+ line / 37%+ branch coverage
- [ ] Lizard: no methods with CCN > 20 (gated in CI/verify scripts)
- [ ] Pre-commit hook blocks commits with formatting violations
- [ ] Pre-push hook includes build check (catches analyzer warnings locally)
- [ ] CI enforces: format, coverage threshold, analyzers, Lizard (warn >15, fail >20)
- [ ] All 10 target methods reduced below CCN 15 (or as close as feasible — Target 1 spike may set limit)
- [ ] RadialExtensionAdjuster extractions are independent (no cross-target helper calls)
- [ ] `Directory.Build.props` with `LangVersion>10.0` and `AnalysisLevel>latestRecommended`
- [ ] `.gitattributes` forces LF endings on hook scripts and tooling
- [ ] CHANGELOG updated
- [ ] `scripts/README.md` documents new arguments
- [ ] `AGENTS.md` quick commands updated
