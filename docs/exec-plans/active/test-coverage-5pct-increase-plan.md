---
status: active
owner: agent
started: 2026-08-10
---

# Test Coverage Improvement Plan - Next Coverage Ratchet

> **For agentic workers:** REQUIRED SUB-SKILL: Use `superpowers:subagent-driven-development` for independent service slices, or `superpowers:executing-plans` for inline execution. Use `superpowers:test-driven-development` for every production-code seam and new behavior test. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Raise the verified non-performance coverage baseline from **46.2% line / 40.7% branch** to at least **50% line / 45% branch**, while preserving a practical path toward the backlog target of **60% line / 50% branch**.

**Architecture:** Expand coverage through small service-level tests and narrow testability seams. Prefer constructor-injected paths or interfaces over reflection, static-state manipulation, or tests that write broad data into `%AppData%`. Keep MainWindow partials out of this unit-test push except for source-guard tests or later GUI smoke plans.

**Tech Stack:** .NET 6, WPF, xUnit, Coverlet/Cobertura via `.runsettings`, repo-local `.\scripts\verify.ps1`.

---

## Current State

**Revised:** 2026-08-11 after the service-testability slice.

**Verified baseline:** `.\scripts\verify.ps1` passed with 771 non-performance tests and coverage at **46.2% line / 40.7% branch**.

**Implementation pause point (2026-08-11):** Phase 1 cache/logging seams, Phase 2 composite application-service expansion, and selected Phase 3/4 service expansions are implemented. The focused written-test set passed with **204 tests**. Full `.\scripts\verify.ps1` passed with **842 non-performance tests** and **48.9% line / 44.0% branch coverage**. The 50% / 45% next-ratchet target remains unfinished.

**Already completed in this coverage track:**

| Area | Status | Tests |
|------|--------|-------|
| `MapNavigationService` | Complete | `Tests/MapNavigationServiceTests.cs` - 9 tests |
| `ManualLayoutOverrideStore` | Complete | `Tests/ManualLayoutOverrideStoreTests.cs` - 12 tests |
| `ManualLayoutAssignmentEnricher` | Complete after `ICompositePinPlanningResultProvider` seam | `Tests/ManualLayoutAssignmentEnricherTests.cs` - 6 tests |
| `AnimationFrameCache` | Complete after optional cache-directory seam | `Tests/AnimationFrameCacheTests.cs` - 9 tests |
| `CompositePinApplicationService` | Expanded after temp `CompositePinPlanCache` seam | `Tests/CompositePinApplicationServiceTests.cs` - 14 tests |
| `ClusterCache` | Complete after optional cache-root constructor seam | `Tests/ClusterCacheTests.cs` - 10 tests |
| `FileLogger` | Complete after `ILogPathProvider` seam | `Tests/FileLoggerTests.cs` - 6 tests |
| `ManualLayoutManager` | Expanded with public persistence/variant/application workflows | `Tests/ManualLayoutManagerTests.cs`, `Tests/ManualLayoutVariantTests.cs` |
| `LayoutEditorController` | Expanded with variant workflow coverage | `Tests/LayoutEditorControllerTests.cs` - 50 focused tests |
| `StartupValidator` | Expanded with content-set and location-data validation coverage | `Tests/StartupValidatorTests.cs` - 16 tests |

**Remaining high-value gaps:**

| Area | Why It Matters | Current Constraint |
|------|----------------|--------------------|
| `ContentLoader` and `ContentLoader.Images` | User-visible content loading, fallback, captions, and decode error paths. | Partially expanded; full focused suite is passing, but full coverage target still needs verification. |
| `ManualLayoutManager` | Persistence, migration, and variant constraints are high-risk behavior. | Expanded; remaining branches can be selected from the next coverage report. |
| `LayoutEditorController` | Edit-mode variant flow and validation branch coverage. | Expanded; remaining validation branches can be selected from the next coverage report. |
| `RadialExtensionAdjuster` / `RadialExtensionCalculator` | Complex geometry is where subtle overlap bugs hide. | Prefer public behavior tests; extract internal helpers only when a cohesive helper has a stable contract. |

---

## Plan Rules

- [ ] Run a focused baseline for the slice before editing code. Use `dotnet test Tests/InteractiveWorldMap.Tests.csproj --filter "FullyQualifiedName~<RelevantTests>"` when a related test class exists.
- [ ] For every production seam, write or update the failing test first and run it to see the expected compile/runtime failure.
- [ ] Add only narrow production seams needed for deterministic tests, such as optional cache directories or small interfaces.
- [ ] Do not introduce a mocking framework just for this plan. Use hand-written fakes, temp directories, and existing `Tests/TestHelpers/MockLogger.cs`.
- [ ] Any test that writes to AppData must either move to a temp-directory seam first or clean a unique key in `finally`.
- [ ] If a test exposes a real behavior bug, add a skipped characterization test with a clear reason and create or update an exec-plan/backlog item before fixing it.
- [ ] After each slice, run its focused tests. Before marking the plan complete, run `.\scripts\verify.ps1`.

---

## Files And Ownership

**Likely production seams:**

| File | Planned Change |
|------|----------------|
| `Services/ClusterCache.cs` | Add optional `string? cacheRootDirectory = null` constructor parameter; default remains `%AppData%\InteractiveWorldMap\clusters`. Preserve legacy demo migration behavior for default AppData path and allow temp-root migration tests. |
| `Services/CompositePinPlanCache.cs` | Add optional `string? cacheDirectory = null` constructor parameter; default remains `%AppData%\InteractiveWorldMap\composite_pin_plan_cache`. |
| `Services/FileLogger.cs` | Add `ILogPathProvider` or optional log directory/file-name constructor seam; preserve the public no-arg constructor and default AppData behavior. |
| `Services/ILogPathProvider.cs` | Create only if the FileLogger seam uses a provider interface instead of constructor strings. |
| `Utilities/RadialExtensionGeometry.cs` | Create only if calculator tests require extracting stable geometry helpers; do not expose private implementation details solely for coverage. |

**Likely test files:**

| File | Planned Change |
|------|----------------|
| `Tests/ClusterCacheTests.cs` | New focused cache tests. |
| `Tests/CompositePinApplicationServiceTests.cs` | Expand beyond `SaveIfMissed`. |
| `Tests/FileLoggerTests.cs` | New sequential async logging tests. |
| `Tests/ContentLoaderTests.cs` | Expand error paths and image/caption fallback tests. |
| `Tests/ManualLayoutManagerTests.cs` | Expand malformed JSON, migration, variant cap, and persistence tests. |
| `Tests/LayoutEditorControllerTests.cs` | Expand variant switching, validation, and event tests. |
| `Tests/StartupValidatorTests.cs` | Expand missing content and malformed location data tests. |
| `Tests/RadialExtensionAdjusterTests.cs` | Add branch-heavy overlap and oscillation scenarios. |
| `Tests/RadialExtensionCalculatorTests.cs` | Add public outcome tests, or tests for an extracted internal geometry helper if created. |
| `Tests/TestHelpers/TestFixtures.cs` | Add shared temp-directory and bitmap helpers only when duplication appears in a second test file. |
| `Tests/TestHelpers/AsyncTestHelpers.cs` | Add polling helpers only when needed for FileLogger or file-lock tests. |

---

## Phase 0: Re-Baseline And Guardrails

**Purpose:** Start each implementation run from confirmed numbers, because coverage and test counts drift quickly in this repo.

- [ ] **Step 0.1: Capture current full verification.**

Run:

```powershell
.\scripts\verify.ps1
```

Expected: build, non-performance tests, coverage gate, doc checks, taste checks, Lizard, and startup validation pass.

- [ ] **Step 0.2: Record latest coverage attachment and summary.**

Run:

```powershell
py -3 scripts\summarize_coverage.py --results-directory TestResults\verify-coverage --min-line-coverage 42 --min-branch-coverage 37
```

Expected: line coverage is at least 46.2% and branch coverage is at least 40.7%. Update this section if the numbers changed.

- [ ] **Step 0.3: Snapshot existing focused tests.**

Run:

```powershell
dotnet test Tests/InteractiveWorldMap.Tests.csproj --filter "FullyQualifiedName~ClusterCacheTests|FullyQualifiedName~CompositePinApplicationServiceTests|FullyQualifiedName~CompositePinPlanCacheTests|FullyQualifiedName~ContentLoaderTests|FullyQualifiedName~ManualLayoutManagerTests"
```

Expected: all existing matching tests pass. If a class does not exist yet, `dotnet test` still runs the matching discovered tests.

---

## Phase 1: Cache And Logging Testability Seams

### Task 1.1: Make `ClusterCache` Temp-Directory Testable

**Files:**
- Modify: `Services/ClusterCache.cs`
- Create: `Tests/ClusterCacheTests.cs`

- [x] **Step 1: Write failing constructor-seam tests.**

Add tests named:

```csharp
[Fact] public void Save_ThenTryLoad_WithTempCacheRoot_ReturnsClusters()
[Fact] public void TryLoad_WithMissingCacheFile_ReturnsNull()
[Fact] public void Constructor_WithDemoSuffix_MigratesLegacyCacheFromTempRoot()
```

Use a temp root such as `Path.Combine(Path.GetTempPath(), "ClusterCache_" + Guid.NewGuid().ToString("N"))`.

- [ ] **Step 2: Run the red test.**

Run:

```powershell
dotnet test Tests/InteractiveWorldMap.Tests.csproj --filter "FullyQualifiedName~ClusterCacheTests"
```

Expected: fail to compile because `ClusterCache` does not yet accept a temp cache root.

- [x] **Step 3: Add the minimal seam.**

Change the constructor shape to:

```csharp
public ClusterCache(ILogger logger, string contentSetSuffix, string? cacheRootDirectory = null)
```

Use `cacheRootDirectory ?? Path.Combine(appData, "InteractiveWorldMap", "clusters")` for the new cache root. Keep the default runtime path unchanged. For legacy migration, compute the old path from the parent `InteractiveWorldMap` directory so tests can create a temp equivalent.

- [x] **Step 4: Add cache behavior tests.**

Add tests named:

```csharp
[Fact] public void TryLoad_AfterSave_PreservesClusterIdsCentersAndLocations()
[Fact] public void TryLoad_WithChangedThreshold_ReturnsNull()
[Fact] public void TryLoad_WithChangedLocationCoordinate_ReturnsNull()
[Fact] public void TryLoad_WithLocationOrderChanged_ReturnsClusters()
[Fact] public void TryLoad_WithMissingLocationReferencedByCache_ReturnsNull()
[Fact] public void TryLoad_WithMalformedJson_ReturnsNull()
[Fact] public void Save_WhenDirectoryCannotBeCreated_DoesNotThrowAndLogsWarning()
```

- [ ] **Step 5: Run focused verification.**

Run:

```powershell
dotnet test Tests/InteractiveWorldMap.Tests.csproj --filter "FullyQualifiedName~ClusterCacheTests"
```

Expected: all `ClusterCacheTests` pass.

- [x] **Step 5: Run focused verification.** — 10 `ClusterCacheTests` pass (Phase 1 Task 1.1 complete).

### Task 1.2: Make `CompositePinPlanCache` Temp-Directory Testable

**Files:**
- Modify: `Services/CompositePinPlanCache.cs`
- Modify: `Tests/CompositePinApplicationServiceTests.cs`
- Modify: `Tests/CompositePinPlanCacheTests.cs`

- [ ] **Step 1: Write failing constructor-seam tests.**

Add or update tests so `CompositePinPlanCache` is created with a temp cache directory:

```csharp
var cache = new CompositePinPlanCache(new MockLogger(), tempCacheDirectory);
```

Expected red failure: constructor overload does not exist.

- [ ] **Step 2: Add the minimal seam.**

Change constructor shape to:

```csharp
public CompositePinPlanCache(ILogger logger, string? cacheDirectory = null)
```

Default path remains `%AppData%\InteractiveWorldMap\composite_pin_plan_cache`.

- [ ] **Step 3: Migrate existing tests off AppData.**

Update `CompositePinPlanCacheTests` and `CompositePinApplicationServiceTests` to use temp directories instead of unique AppData keys where practical.

- [ ] **Step 4: Run focused verification.**

Run:

```powershell
dotnet test Tests/InteractiveWorldMap.Tests.csproj --filter "FullyQualifiedName~CompositePinPlanCacheTests|FullyQualifiedName~CompositePinApplicationServiceTests"
```

Expected: all matching tests pass and no AppData cleanup is needed for these classes.

### Task 1.3: Make `FileLogger` Testable Without Global AppData Writes

**Files:**
- Modify: `Services/FileLogger.cs`
- Create if using provider seam: `Services/ILogPathProvider.cs`
- Create: `Tests/FileLoggerTests.cs`
- Create if needed: `Tests/TestHelpers/AsyncTestHelpers.cs`

- [ ] **Step 1: Write failing path-seam test.**

Add:

```csharp
[Collection("FileLogger")]
public class FileLoggerTests
{
    [Fact]
    public async Task LogInfo_WritesMessageToInjectedLogFile()
    {
        // create temp log directory, create FileLogger with injected path, write, dispose,
        // wait until the file contains one line, assert "[INFO]" and message are present.
    }
}
```

Expected red failure: `FileLogger` does not accept an injected path provider or directory.

- [ ] **Step 2: Add the minimal path seam.**

Preferred constructor:

```csharp
public FileLogger(ILogPathProvider? pathProvider = null)
```

`DefaultLogPathProvider` must preserve the existing `%AppData%\InteractiveWorldMap\logs\app.log` behavior.

- [ ] **Step 3: Add async-safe tests.**

Add tests named:

```csharp
[Fact] public async Task LogWarning_WritesWarningLevel()
[Fact] public async Task LogError_WithException_WritesExceptionMessage()
[Fact] public async Task Dispose_WhenLastInstance_CompletesWriter()
[Fact] public void Constructor_CreatesLogDirectory()
[Fact] public void Constructor_WhenDirectoryCreationFails_DoesNotThrow()
```

Use `[Collection("FileLogger")]` to keep static queue/thread tests sequential. Avoid asserting exact timestamps.

- [ ] **Step 4: Run focused verification.**

Run:

```powershell
dotnet test Tests/InteractiveWorldMap.Tests.csproj --filter "FullyQualifiedName~FileLoggerTests"
```

Expected: all `FileLoggerTests` pass reliably.

---

## Phase 2: Composite Application Service Expansion

### Task 2.1: Test Cache Load And Cache Key Behavior

**Files:**
- Modify: `Tests/CompositePinApplicationServiceTests.cs`
- Modify only if Task 1.2 was not completed: `Services/CompositePinPlanCache.cs`

- [ ] **Step 1: Add cache load tests.**

Add tests named:

```csharp
[Fact] public void TryCacheLoad_WithCacheHit_ReturnsPlansAndComputedKey()
[Fact] public void TryCacheLoad_WithCacheMiss_ReturnsNullAndComputedKey()
[Fact] public void TryCacheLoad_WhenGeometryFileChanges_ComputesDifferentCacheKey()
[Fact] public void InvalidateGroup_RemovesMatchingCachedPlan()
```

Use a temp `CompositePinPlanCache`, a real temporary geometry file, and simple `ManualLayout` / `PinPartConfig` objects.

- [ ] **Step 2: Run focused verification.**

Run:

```powershell
dotnet test Tests/InteractiveWorldMap.Tests.csproj --filter "FullyQualifiedName~CompositePinApplicationServiceTests"
```

Expected: all application-service tests pass.

### Task 2.2: Test Manual Layout Apply Instruction Projection

**Files:**
- Modify: `Tests/CompositePinApplicationServiceTests.cs`

- [ ] **Step 1: Add projection tests.**

Add tests named:

```csharp
[Fact] public void BuildApplyInstructions_WithViewportSourceCoords_ProjectsOriginalPosition()
[Fact] public void BuildApplyInstructions_WithSourceExtendedCoords_PreservesFullMapOffset()
[Fact] public void BuildApplyInstructions_WithoutSourceExtendedCoords_UsesAngleAndLength()
[Fact] public void BuildApplyInstructions_WithCachedPlan_AttachesPlanByLocationName()
[Fact] public void BuildApplyInstructions_WithCacheMiss_SetsShouldSaveToCache()
[Fact] public void BuildApplyInstructions_WhenCompositePinsDisabled_DoesNotAttemptCache()
[Fact] public void BuildApplyInstructions_WhenGroupKeyBlank_DoesNotAttemptCache()
```

Assert observable `ManualLayoutApplyResult` values, not private helper methods.

- [ ] **Step 2: Run focused verification.**

Run:

```powershell
dotnet test Tests/InteractiveWorldMap.Tests.csproj --filter "FullyQualifiedName~CompositePinApplicationServiceTests"
```

Expected: all application-service tests pass.

---

## Phase 3: Persistence And Content Error Paths

### Task 3.1: Expand `ManualLayoutManager` Persistence Coverage

**Files:**
- Modify: `Tests/ManualLayoutManagerTests.cs`
- Modify if a seam is missing: `Services/ManualLayoutManager.cs`

- [ ] **Step 1: Add malformed and missing file tests.**

Add tests named:

```csharp
[Fact] public void LoadLayoutCollection_WhenFileMissing_ReturnsEmptyCollection()
[Fact] public void LoadLayoutCollection_WhenJsonMalformed_ReturnsEmptyCollectionAndBacksUpFile()
[Fact] public void LoadLayoutCollection_WhenLegacyFlatLayoutExists_MigratesToVariantCollection()
```

- [ ] **Step 2: Add variant boundary tests.**

Add tests named:

```csharp
[Fact] public void SaveVariant_WhenManualVariantCapReached_ReturnsFalse()
[Fact] public void SaveVariant_WhenImportedVariantCapReached_ReturnsFalse()
[Fact] public void DeleteVariant_WhenAutoSeed_ReturnsFalse()
[Fact] public void DeleteVariant_WhenOnlyVariant_ReturnsFalse()
[Fact] public void SelectPreferredVariant_WithManualDefault_SelectsManualDefault()
[Fact] public void SelectPreferredVariant_WithOnlyAutoSeed_SelectsAutoSeed()
```

- [ ] **Step 3: Run focused verification.**

Run:

```powershell
dotnet test Tests/InteractiveWorldMap.Tests.csproj --filter "FullyQualifiedName~ManualLayoutManagerTests|FullyQualifiedName~ManualLayoutVariantTests"
```

Expected: all matching tests pass.

### Task 3.2: Expand `ContentLoader` And Image Loading Coverage

**Files:**
- Modify: `Tests/ContentLoaderTests.cs`
- Modify only if necessary for testability: `Services/ContentLoader.cs`, `Services/ContentLoader.Images.cs`

- [ ] **Step 1: Add content fallback tests.**

Add tests named:

```csharp
[Fact] public async Task LoadLocationContentAsync_WhenLocationFolderMissing_ReturnsEmptyContent()
[Fact] public async Task LoadDidacticTextAsync_WhenFileMissing_ReturnsNull()
[Fact] public async Task LoadDidacticTextAsync_WhenExcelBioExists_PrefersWorkbookText()
[Fact] public async Task LoadCaptionsAsync_WhenSidecarExists_ReturnsCaption()
[Fact] public async Task LoadCaptionsAsync_WhenCaptionMissing_ReturnsEmptyCaption()
```

- [ ] **Step 2: Add image decode and path tests.**

Add tests named:

```csharp
[Fact] public void TryLoadContentBitmap_WhenImageInvalid_ReturnsNullAndLogsWarning()
[Fact] public void LoadFrozenBitmap_WithValidPng_ReturnsFrozenBitmap()
[Fact] public void TryLoadContentBitmap_WithInvalidImage_ReturnsNull()
[Fact] public void WarnIfHeavyImageFile_WithLargeFile_LogsWarning()
[Fact] public void ComputeDecodeWidth_WithBothBounds_UsesSmallerScale()
```

Use public `ContentLoader` methods for invalid-image behavior; do not call private bitmap helpers via reflection.

- [ ] **Step 3: Run focused verification.**

Run:

```powershell
dotnet test Tests/InteractiveWorldMap.Tests.csproj --filter "FullyQualifiedName~ContentLoaderTests"
```

Expected: all `ContentLoaderTests` pass.

---

## Phase 4: Controller, Startup, And Geometry Branch Coverage

### Task 4.1: Expand `LayoutEditorController` Coverage

**Files:**
- Modify: `Tests/LayoutEditorControllerTests.cs`

- [ ] **Step 1: Add variant workflow tests.**

Add tests named:

```csharp
[Fact] public void SwitchToVariant_WithValidVariant_UpdatesActiveVariant()
[Fact] public void TrySaveAsVariant_WithBlankName_ReturnsFalse()
[Fact] public void TryDeleteActiveVariant_WithSavedManualVariant_DeletesAndRaisesEvent()
[Fact] public void VariantsChanged_WhenVariantChanges_FiresOnce()
```

- [ ] **Step 2: Add validation tests.**

Add tests named:

```csharp
[Fact] public void ValidateLayout_WithLineIntersection_ReturnsWarning()
[Fact] public void ValidateLayout_WithLineNearMarker_ReturnsWarning()
[Fact] public void CreateLayoutApplications_WithSourceCoords_ReprojectsPositions()
```

- [ ] **Step 3: Run focused verification.**

Run:

```powershell
dotnet test Tests/InteractiveWorldMap.Tests.csproj --filter "FullyQualifiedName~LayoutEditorControllerTests"
```

Expected: all controller tests pass.

### Task 4.2: Expand `StartupValidator` Coverage

**Files:**
- Modify: `Tests/StartupValidatorTests.cs`

- [ ] **Step 1: Add missing content tests.**

Add tests named:

```csharp
[Fact] public void ValidateEnvironment_WhenWorldMapMissing_AddsError()
[Fact] public void ValidateEnvironment_WhenCoordinatesMissing_AddsWarning()
[Fact] public void ValidateEnvironment_WithProductionContentSet_ValidatesProductionPaths()
```

- [ ] **Step 2: Add malformed location-data tests.**

Add tests named:

```csharp
[Fact] public void ValidateLocationsJson_WithMissingName_AddsWarning()
[Fact] public void ValidateLocationsJson_WithMissingPixelX_AddsWarning()
[Fact] public void ValidateLocationsJson_WithOutOfRangeCoordinate_AddsWarning()
[Fact] public void ValidateLocationsJson_WithValidCoordinate_DoesNotAddWarning()
```

- [ ] **Step 3: Run focused verification.**

Run:

```powershell
dotnet test Tests/InteractiveWorldMap.Tests.csproj --filter "FullyQualifiedName~StartupValidatorTests"
```

Expected: all startup validator tests pass.

### Task 4.3: Expand Radial Extension Geometry Coverage

**Files:**
- Modify: `Tests/RadialExtensionAdjusterTests.cs`
- Modify: `Tests/RadialExtensionCalculatorTests.cs`
- Create only if justified: `Utilities/RadialExtensionGeometry.cs`

- [ ] **Step 1: Add public behavior tests for adjuster branches.**

Add tests named:

```csharp
[Fact] public void AdjustExtensions_WithLineNearMarker_MovesLineAway()
[Fact] public void AdjustExtensions_WithIntersectingLines_ReducesIntersections()
[Fact] public void AdjustExtensions_WithOscillatingPair_AppliesLengthSeparation()
[Fact] public void AdjustExtensions_WithAngleNearZero_WrapsWithoutCrossing()
[Fact] public void AdjustExtensions_WithProtectedLocation_DoesNotMoveProtectedExtension()
```

- [ ] **Step 2: Add calculator outcome tests.**

Add tests named:

```csharp
[Fact] public void CalculateExtensions_WithDenseCluster_ReturnsDistinctAngles()
[Fact] public void CalculateExtensions_WithCanvasBounds_KeepsHeadsInsideBounds()
[Fact] public void CalculateExtensions_WithWrapAroundAngles_PreservesMinimumSpacing()
```

- [ ] **Step 3: Extract helper only if public tests cannot isolate a stable rule.**

If extraction is justified, create `Utilities/RadialExtensionGeometry.cs` with internal pure methods and add:

```csharp
[Fact] public void RadialExtensionGeometry_AngularDistanceAcrossZero_ReturnsSmallDistance()
[Fact] public void RadialExtensionGeometry_NudgeApart_PreservesCircularOrder()
```

Do not use reflection against private methods.

- [ ] **Step 4: Run focused verification.**

Run:

```powershell
dotnet test Tests/InteractiveWorldMap.Tests.csproj --filter "FullyQualifiedName~RadialExtensionAdjusterTests|FullyQualifiedName~RadialExtensionCalculatorTests"
```

Expected: all matching geometry tests pass.

---

## Phase 5: MainWindow-Adjacent Guards And Stretch Coverage

### Task 5.1: Add Source-Guard Tests For Critical MainWindow Wiring

**Files:**
- Modify or create: `Tests/MainWindowCompositePinWiringTests.cs`
- Modify or create: `Tests/MainWindowContentWiringTests.cs`

- [ ] **Step 1: Add composite manual-layout wiring guards.**

Add tests that read the relevant partial source files and assert these calls remain wired:

```csharp
[Fact] public void ApplyManualLayout_UsesCompositePinApplicationServiceBuildApplyInstructions()
[Fact] public void SaveManualLayout_InvalidatesCompositePinApplicationCache()
[Fact] public void ManualLayoutSave_UsesManualLayoutAssignmentEnricher()
```

- [ ] **Step 2: Add content-loading wiring guards.**

Add tests that assert content image loading stays on the existing async/downscaled path:

```csharp
[Fact] public void ContentLoad_UsesBoundedDecodePath()
[Fact] public void ContentLoad_LogsLargeImageWarningsThroughContentLoader()
```

- [ ] **Step 3: Run focused verification.**

Run:

```powershell
dotnet test Tests/InteractiveWorldMap.Tests.csproj --filter "FullyQualifiedName~MainWindowCompositePinWiringTests|FullyQualifiedName~MainWindowContentWiringTests"
```

Expected: source-guard tests pass. If these become brittle, prefer extracting tested orchestration services over adding more string checks.

### Task 5.2: Identify Next 60% Coverage Candidates

**Files:**
- Modify: `docs/exec-plans/active/test-coverage-5pct-increase-plan.md`
- Modify: `docs/TO_DO.md`

- [ ] **Step 1: After reaching 50% / 45%, run a fresh coverage report.**

Run:

```powershell
.\scripts\verify.ps1
py -3 scripts\summarize_coverage.py --results-directory TestResults\verify-coverage --min-line-coverage 50 --min-branch-coverage 45
```

- [ ] **Step 2: Update this plan or create a successor plan for the 60% / 50% target.**

Candidate areas for the successor plan:

```text
MainWindow.Navigation.partial.cs
MainWindow.MarkerPlacement.partial.cs
MainWindow.Content.partial.cs
ContentLoader gallery/content matrix
ManualLayoutManager migration and backup matrix
RadialExtensionCalculator dense-cluster matrix
FileLogger lifecycle stress tests
```

Keep GUI smoke/manual acceptance work separate from unit coverage.

---

## Estimated Impact

| Phase | Focus | Estimated Line Gain | Estimated Branch Gain | Effort |
|-------|-------|---------------------|-----------------------|--------|
| Phase 1 | Cache/logging seams plus `ClusterCache`, `CompositePinPlanCache`, `FileLogger` tests | 0.8-1.2% | 0.8-1.4% | 8-12 hours |
| Phase 2 | `CompositePinApplicationService` cache load and projection expansion | 0.5-0.9% | 0.7-1.2% | 5-8 hours |
| Phase 3 | `ManualLayoutManager`, `ContentLoader`, image/caption error paths | 1.2-1.8% | 1.4-2.1% | 12-18 hours |
| Phase 4 | Controller, startup, and geometry branch coverage | 1.0-1.6% | 1.6-2.4% | 12-20 hours |
| Phase 5 | MainWindow-adjacent source guards and next-candidate audit | 0.2-0.5% | 0.2-0.5% | 3-5 hours |

**Expected result:** 50-52% line coverage and 45-47% branch coverage if Phases 1-4 land cleanly.

**Stretch path:** After the next ratchet stabilizes, create a successor plan for the remaining climb to 60% / 50%. Do not raise the hard gate straight to the observed coverage; leave a small buffer for line-count drift.

---

## Verification Matrix

Run focused tests after each phase:

```powershell
dotnet test Tests/InteractiveWorldMap.Tests.csproj --filter "FullyQualifiedName~ClusterCacheTests|FullyQualifiedName~CompositePinPlanCacheTests|FullyQualifiedName~CompositePinApplicationServiceTests|FullyQualifiedName~FileLoggerTests"
dotnet test Tests/InteractiveWorldMap.Tests.csproj --filter "FullyQualifiedName~ContentLoaderTests|FullyQualifiedName~ManualLayoutManagerTests|FullyQualifiedName~ManualLayoutVariantTests"
dotnet test Tests/InteractiveWorldMap.Tests.csproj --filter "FullyQualifiedName~LayoutEditorControllerTests|FullyQualifiedName~StartupValidatorTests|FullyQualifiedName~RadialExtensionAdjusterTests|FullyQualifiedName~RadialExtensionCalculatorTests"
```

Run full verification before closing the plan:

```powershell
.\scripts\verify.ps1
py -3 scripts\summarize_coverage.py --results-directory TestResults\verify-coverage --min-line-coverage 50 --min-branch-coverage 45
py -3 scripts\verify_doc_links.py
py -3 scripts\doc_gardening.py
```

Expected final state:

```text
Build: 0 errors
Tests: all non-performance tests pass
Coverage: >=50% line, >=45% branch
Doc links: pass
Doc gardening: pass
Taste/Lizard/startup: pass through verify.ps1
```

---

## Risks And Mitigations

| Risk | Impact | Mitigation |
|------|--------|------------|
| Cache tests write into AppData | Local state pollution and flaky failures | Add optional temp-directory seams before broad cache tests. |
| FileLogger static queue/thread leaks across tests | Intermittent failures | Use `[Collection("FileLogger")]`, unique temp paths, deterministic disposal, and polling helpers. |
| Coverage-only work encourages brittle private tests | High maintenance cost | Test public outcomes; extract pure internal helpers only when the helper has a stable domain contract. |
| Source-guard tests become string-fragile | False failures during refactors | Use them sparingly for MainWindow wiring only; prefer service extraction if behavior needs deeper tests. |
| Raising coverage gates too aggressively blocks unrelated work | CI friction | Keep existing gates until a new baseline passes repeatedly, then ratchet below the observed value. |
| WPF bitmap tests require thread affinity or file-lock cleanup | CI-specific failures | Follow existing `AnimationFrameCacheTests` and `ZoomedRegionCacheTests` patterns; freeze bitmaps and release URI-backed locks. |

---

## Completion Criteria

- [ ] `ClusterCacheTests`, expanded `CompositePinApplicationServiceTests`, and `FileLoggerTests` exist and pass.
- [ ] High-value expansions land for `ContentLoader`, `ManualLayoutManager`, `LayoutEditorController`, `StartupValidator`, and radial-extension geometry.
- [ ] New tests use temp directories or hand-written fakes; no broad AppData writes remain in new tests.
- [ ] `.\scripts\verify.ps1` passes.
- [ ] Coverage summary is at least 50% line / 45% branch.
- [ ] `docs/TO_DO.md` is updated with either the completed next-ratchet result or narrowed remaining scope.
- [ ] `CHANGELOG.md` has an `[Unreleased]` entry summarizing the coverage ratchet.
- [ ] If all phases are complete, move this plan to `docs/exec-plans/completed/` and update `docs/exec-plans/active/README.md`.

---

## Post-Completion Gate Ratchet

After the full suite passes repeatedly at the new baseline, update `.runsettings` conservatively. Example if final coverage is around 51% / 46%:

```xml
<MinimumLineCoverage>49</MinimumLineCoverage>
<MinimumBranchCoverage>43</MinimumBranchCoverage>
```

Do not promote advisory 60% / 50% aspirations into blocking gates until the repo is already stable above them.

---

## Appendix: Current Quick Reference

**Baseline coverage:** 46.2% line / 40.7% branch

**Next target:** 50% line / 45% branch

**Backlog target:** 60% line / 50% branch

**Current non-performance tests:** 771 in latest verified run

**Completed test files in this track:** `MapNavigationServiceTests`, `ManualLayoutOverrideStoreTests`, `ManualLayoutAssignmentEnricherTests`, `AnimationFrameCacheTests`, initial `CompositePinApplicationServiceTests`

**Primary remaining test files to create:** `ClusterCacheTests`, `FileLoggerTests`

**Primary remaining test files to expand:** `CompositePinApplicationServiceTests`, `ContentLoaderTests`, `ManualLayoutManagerTests`, `LayoutEditorControllerTests`, `StartupValidatorTests`, `RadialExtensionAdjusterTests`, `RadialExtensionCalculatorTests`
