# Exec Plan: Complexity Refactoring + Hooks/CI

> **Branch:** `complexity-and-ci-hooks`
> **Status:** active
> **Created:** 2026-07-29
> **Reviewed:** 2026-07-29

## Goal

Add Roslyn analyzers, pre-commit formatting enforcement, coverage gating, Lizard complexity CI, and refactor the 10 worst cyclomatic-complexity offenders.

## Current State

- .NET 6 SDK pinned (`global.json` 6.0.400, `latestPatch` roll-forward)
- No `Directory.Build.props`, no Roslyn analyzers, no `.runsettings`
- Pre-push hook only (opt-in). No pre-commit hook.
- `dotnet format` never enforced anywhere
- Test coverage: 41.5% line / 36.4% branch (677 tests pass)
- Lizard results: 10 methods with CCN > 15 (worst: `TryValidate` CCN 58)
- CI: 4 GitHub Actions workflows (build/test, secrets, advisory health, doc gardening)
- Language version: C# 10.0 (no C# 11+ features)

---

## Phase 1: CI/Hooks Infrastructure

### 1a. `Directory.Build.props` (new file, repo root)

Use SDK-native analyzers (built into .NET 5+ SDK). No NuGet package needed — avoids version mismatch with pinned .NET 6 SDK.

```xml
<Project>
  <PropertyGroup>
    <EnableNETAnalyzers>true</EnableNETAnalyzers>
    <AnalysisLevel>latest</AnalysisLevel>
    <EnforceCodeStyleInBuild>true</EnforceCodeStyleInBuild>
  </PropertyGroup>
</Project>
```

### 1b. `.runsettings` (new file, repo root)

Use Coverlet's standard threshold configuration (`Threshold`, `ThresholdType`, `ThresholdStat`). The `<ThresholdLine>` / `<ThresholdBranch>` tags are not recognized by Coverlet's VSTest integration and would be silently ignored.

```xml
<RunSettings>
  <DataCollectionRunSettings>
    <DataCollectors>
      <DataCollector friendlyName="XPlat Code Coverage">
        <Configuration>
          <Exclude>[Tests]*,[ManualLayoutSeedGenerator]*,[ThumbnailTouchSmoke]*</Exclude>
          <Include>[InteractiveWorldMap]*</Include>
          <Threshold>42</Threshold>
          <ThresholdType>line</ThresholdType>
          <ThresholdStat>total</ThresholdStat>
        </Configuration>
      </DataCollector>
    </DataCollectors>
  </DataCollectionRunSettings>
</RunSettings>
```

### 1c. Pre-format codebase (Step 0 — before hooks are wired)

Run `dotnet format` on the entire solution to fix existing formatting violations. This must happen **before** the pre-commit hook is installed, otherwise every commit would immediately fail.

```powershell
dotnet format InteractiveWorldMap.sln
git add -A
git commit -m "style: format entire codebase for dotnet format enforcement"
```

### 1d. `.githooks/pre-commit` (new file)

Shell script: run `dotnet format --verify-no-changes`, exit 1 on violations.

Update `scripts/install_git_hooks.ps1` to confirm pre-commit is wired (already handles `core.hooksPath`).

### 1e. CI workflow updates

**`ci.yml`:**
- Add `dotnet format --verify-no-changes` step (after build, before test)
- Add `--settings .runsettings` to `dotnet test` step
- Add Lizard step with `-C 20` (fail on CCN > 20)

**`advisory-code-health.yml`:**
- Remove `continue-on-error: true` (promote to blocking)
- Add Lizard warning step with `-C 15` and `continue-on-error: true` (warn on CCN > 15)
- Add Lizard fail step with `-C 20` (fail on CCN > 20)
- Add `--settings .runsettings` to coverage collection

Lizard exclusions (prevent false positives on generated/test/tool code):

```bash
lizard -C 20 -x "**/Tests/*" -x "**/Tools/*" -x "**/obj/*" -x "**/bin/*" -x "**/scripts/venv/*" .
```

### 1f. Verify scripts

- `scripts/verify.ps1`: add `dotnet format --verify-no-changes` step
- `scripts/verify.sh`: add `dotnet format --verify-no-changes` step

---

## Phase 2: Complexity Refactoring

Each refactoring = 1 commit. Run `dotnet test` after each.

### Architectural constraints

1. **Views layer independence**: `DeveloperTuningPanel.xaml.cs` is in the Views layer. Extracted helpers must stay in `Views/` namespace — cannot reference `Services` or `Utilities` (enforced by `LayerDependencyTests.cs`).
2. **Property contracts**: `TuningPanelWiringTests.cs` validates property mappings. Cannot rename properties on `TuningPanelEventArgs`.
3. **Language version**: C# 10.0 only — no list patterns, required properties, raw string literals.

### Target methods

| # | Method | File | CCN | Approach |
|---|--------|------|-----|----------|
| 1 | `TryValidate` | `Views/DeveloperTuningPanel.xaml.cs` | 58 | Table-driven validation: extract a `ValidateField` helper and a list of static validation rules/delegates. ~30 identical guard clauses collapse to a loop. |
| 2 | `TryBuildEventArgs` | `Views/DeveloperTuningPanel.xaml.cs` | 35 | Extract category blocks into sub-methods (e.g. `TryReadMapTuning`, `TryReadPopupStyle`). Already uses `TryReadPositive`/etc. |
| 3 | `AdjustAnglesWithinGroups` | `Services/RadialExtensionAdjuster.cs` | 24 | Extract angle comparison/nudging into `AdjustAnglePair` helper. |
| 4 | `FixLineIntersections` | `Services/RadialExtensionAdjuster.cs` | 20 | Separate into `HasIntersectionOrClosePass` + `TryApplyRotationStrategy`. |
| 5 | `Compute` | `Services/MarkerPlacementOrchestrator.cs` | 18 | Break into `ClearState`, `ComputeIndividualPlacements`, `ComputeClusterPlacements`, `ComputeExtensionPlacements`, `MergeResults`. |
| 6 | `BuildApplyInstructions` | `Services/CompositePinApplicationService.cs` | 17 | Extract loop body into `BuildInstruction` helper. |
| 7 | `ValidateLocationsJson` | `Services/StartupValidator.cs` | 17 | Table-driven required field checks + helper for boundary validation. |
| 8 | `ReadLocationsFromExcel` | `Utilities/ExcelCoordinateReader.cs` | 16 | Extract sheet mapping helpers (e.g. `BuildBioDictionary`); separate header detection from data rows. |
| 9 | `AdjustExtensions` | `Services/RadialExtensionAdjuster.cs` | 16 | Extract `ResolveOscillatingPairs` and `AnalyzeAndLogFinalSeparation`. |
| 10 | `AdjustForMarkerOverlaps` | `Services/RadialExtensionAdjuster.cs` | 16 | Relocate start/end diagnostic logging to sub-helpers. |

---

## Phase 3: Documentation

- `CHANGELOG.md` — `[Unreleased]` entry for format enforcement, analyzers, coverage gate, Lizard CI, and complexity refactoring
- `scripts/README.md` — update if new scripts/hooks added
- `AGENTS.md` — update quick commands if verify.ps1/verify.sh gain format step

---

## Acceptance Criteria

- [ ] `dotnet format` run on entire codebase (committed before hook wiring)
- [ ] `.\scripts\verify.ps1` passes (includes new format check)
- [ ] `dotnet format --verify-no-changes` passes
- [ ] `dotnet test` passes with 42%+ line coverage (enforced by `.runsettings`)
- [ ] Lizard: no methods with CCN > 20
- [ ] Pre-commit hook blocks commits with formatting violations
- [ ] CI enforces: format, coverage, analyzers, Lizard (warn >15, fail >20)
- [ ] All 10 target methods reduced below CCN 15 (or as close as feasible)
- [ ] CHANGELOG updated
