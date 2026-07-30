# Exec Plan: Complexity Refactoring + Hooks/CI

> **Branch:** `complexity-and-ci-hooks`
> **Status:** active
> **Created:** 2026-07-29

## Goal

Add Roslyn analyzers, pre-commit formatting enforcement, coverage gating, Lizard complexity CI, and refactor the 10 worst cyclomatic-complexity offenders.

## Current State

- .NET 6 SDK pinned (`global.json`)
- No `Directory.Build.props`, no Roslyn analyzers, no `.runsettings`
- Pre-push hook only (opt-in). No pre-commit hook.
- `dotnet format` never enforced anywhere
- Test coverage: 41.5% line / 36.4% branch (677 tests pass)
- Lizard results: 10 methods with CCN > 15 (worst: `TryValidate` CCN 58)
- CI: 4 GitHub Actions workflows (build/test, secrets, advisory health, doc gardening)

---

## Phase 1: CI/Hooks Infrastructure

### 1a. `Directory.Build.props` (new file, repo root)

```xml
<Project>
  <ItemGroup>
    <PackageReference Include="Microsoft.CodeAnalysis.NetAnalyzers" Version="8.0.0">
      <PrivateAssets>all</PrivateAssets>
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
    </PackageReference>
  </ItemGroup>
  <PropertyGroup>
    <EnforceCodeStyleInBuild>true</EnforceCodeStyleInBuild>
  </PropertyGroup>
</Project>
```

### 1b. `.githooks/pre-commit` (new file)

Shell script: run `dotnet format --verify-no-changes`, exit 1 on violations.

Update `scripts/install_git_hooks.ps1` to confirm pre-commit is wired (already handles `core.hooksPath`).

### 1c. `.runsettings` (new file, repo root)

```xml
<RunSettings>
  <DataCollectionRunSettings>
    <DataCollectors>
      <DataCollector friendlyName="XPlat Code Coverage">
        <Configuration>
          <Exclude>[Tests]*,[ManualLayoutSeedGenerator]*,[ThumbnailTouchSmoke]*</Exclude>
          <Include>[InteractiveWorldMap]*</Include>
          <ThresholdLine>42</ThresholdLine>
          <ThresholdBranch>0</ThresholdBranch>
        </Configuration>
      </DataCollector>
    </DataCollectors>
  </DataCollectionRunSettings>
</RunSettings>
```

### 1d. CI workflow updates

**`ci.yml`:**
- Add `dotnet format --verify-no-changes` step (after build, before test)
- Add `--settings .runsettings` to `dotnet test` step
- Add Lizard step (install via pip, run with `--fail-under-cyclomatic-complexity 20`)

**`advisory-code-health.yml`:**
- Remove `continue-on-error: true` (promote to blocking)
- Add Lizard step (warn CCN > 15, fail CCN > 20)
- Add `--settings .runsettings` to coverage collection

### 1e. Verify scripts

- `scripts/verify.ps1`: add `dotnet format --verify-no-changes` step
- `scripts/verify.sh`: add `dotnet format --verify-no-changes` step

---

## Phase 2: Complexity Refactoring

Each refactoring = 1 commit. Run `dotnet test` after each.

| # | Method | File | CCN | Approach |
|---|--------|------|-----|----------|
| 1 | `TryValidate` | `Views/DeveloperTuningPanel.xaml.cs` | 58 | Extract table-driven `ValidateField` helper. ~30 identical guard clauses collapse to a loop over validation descriptors. |
| 2 | `TryBuildEventArgs` | `Views/DeveloperTuningPanel.xaml.cs` | 35 | Already uses `TryReadPositive`/etc. — minor cleanup only. |
| 3 | `AdjustAnglesWithinGroups` | `Services/RadialExtensionAdjuster.cs` | 24 | Extract sort/clamp/distribute into named methods. |
| 4 | `FixLineIntersections` | `Services/RadialExtensionAdjuster.cs` | 20 | Separate intersection detection from resolution into `DetectIntersections` + `ResolveIntersections`. |
| 5 | `Compute` | `Services/MarkerPlacementOrchestrator.cs` | 18 | Break into `ClearState`, `ComputeIndividualPlacements`, `ComputeClusterPlacements`, `MergeResults`. |
| 6 | `BuildApplyInstructions` | `Services/CompositePinApplicationService.cs` | 17 | Group related instructions into helper methods. |
| 7 | `ValidateLocationsJson` | `Services/StartupValidator.cs` | 17 | Table-driven validation (like TryValidate). |
| 8 | `ReadLocationsFromExcel` | `Utilities/ExcelCoordinateReader.cs` | 16 | Extract row-parsing into helper; separate header detection from data rows. |
| 9 | `AdjustExtensions` | `Services/RadialExtensionAdjuster.cs` | 16 | Extract initial setup / per-marker adjustments / cleanup phases. |
| 10 | `AdjustForMarkerOverlaps` | `Services/RadialExtensionAdjuster.cs` | 16 | Separate overlap detection from resolution. |

---

## Phase 3: Documentation

- `CHANGELOG.md` — `[Unreleased]` entry for format enforcement, analyzers, coverage gate, Lizard CI, and complexity refactoring
- `scripts/README.md` — update if new scripts/hooks added
- `AGENTS.md` — update quick commands if verify.ps1/verify.sh gain format step

---

## Acceptance Criteria

- [ ] `.\scripts\verify.ps1` passes (includes new format check)
- [ ] `dotnet format --verify-no-changes` passes
- [ ] `dotnet test` passes with 42%+ line coverage (enforced by `.runsettings`)
- [ ] Lizard: no methods with CCN > 20
- [ ] Pre-commit hook blocks commits with formatting violations
- [ ] CI enforces: format, coverage, analyzers, Lizard (warn >15, fail >20)
- [ ] All 10 target methods reduced below CCN 15 (or as close as feasible)
- [ ] CHANGELOG updated
