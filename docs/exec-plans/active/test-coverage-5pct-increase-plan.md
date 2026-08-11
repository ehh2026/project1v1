---
status: active
owner: agent
started: 2026-08-10
---

# Test Coverage Improvement Plan — 5% Increase

**Goal:** Increase coverage from current **44.7% line / 39.8% branch** to **~50% line / ~45% branch** (+5.3% line, +5.2% branch)

**Date:** 2026-08-10  
**Status:** ⚠️ DRAFT — Undergoing revision based on comprehensive coverage audit  
**Revised:** 2026-08-10 23:45 — Subagent analysis incorporated, critical gaps identified  
**Codebase size:** ~24,600 production lines  
**Current tests:** 732 tests, 69 test files

---

## Executive Summary

This plan targets **5 completely untested Services** (0% coverage), **2 critical Services with NO test files**, error paths in existing partial-coverage Services, plus **high-value coverage expansion** in existing test files. Focus is on **unit-testable business logic** rather than UI integration (MainWindow partials remain 0% but are appropriate for smoke/integration tests, not this unit-test push).

**Critical findings from comprehensive audit:**
- ❌ **CompositePinApplicationService.cs** (264 lines) — **NO TEST FILE EXISTS** (was incorrectly listed as partial coverage)
- ❌ **ClusterCache.cs** (171 lines) — **NO TEST FILE EXISTS** (was incorrectly listed as partial coverage)
- ✅ **ViewportCalculator.cs** has excellent coverage (93.8% / 100%) — no action needed
- ✅ **RadialExtensionCalculator** private methods need direct testing (complex geometry algorithms)

**Revised target breakdown:**
- **Phase 1 (Primary):** 5 zero-coverage Services → ~1.4% line coverage gain (conservative 70-80% coverage)
- **Phase 2 (Critical):** 2 Services with NO test files → ~1.5% line coverage gain (conservative 70-80% coverage)
- **Phase 3 (Expansion):** Error paths + high-value expansions → ~2.1% line coverage gain
- **Total estimated gain:** ~5% line coverage (realistic with all phases)

---

## Current Coverage Analysis

### Zero-Coverage Services (Immediate Opportunities)

|| Service | Lines | Status | Why Untested |
||---------|-------|--------|--------------|
|| `FileLogger.cs` | 98 | 0% / 0% | Background threading, file I/O — needs refactor for testability |
|| `MapNavigationService.cs` | 49 | 0% / 0% | Simple stack wrapper — low-hanging fruit |
|| `AnimationFrameCache.cs` | 194 | 0% / 0% | Disk cache, versioning — needs temp directory fixture |
|| `ManualLayoutAssignmentEnricher.cs` | 33 | 0% / 0% | Dictionary transform — straightforward |
|| `ManualLayoutOverrideStore.cs` | 75 | 0% / 0% | In-memory dictionary — easy to test |

**Total untested lines in these 5 files:** ~449 lines

### Critical Gap: Services with NO Test Files

|| Service | Lines | Status | Why Critical |
||---------|-------|--------|-------------|
|| `CompositePinApplicationService.cs` | 264 | **0% / 0%** | **NO TEST FILE** — cache orchestration, projection math, critical for composite pins |
|| `ClusterCache.cs` | 171 | **0% / 0%** | **NO TEST FILE** — cache hit/miss, hash validation, clustering performance |

**Total untested lines in these 2 files:** ~435 lines

### Partial-Coverage Services (Error Path + Expansion Opportunities)

|| Service | Current Coverage | Opportunity | Time Estimate |
||---------|------------------|-------------|----------------|
|| `ContentLoader.cs` (main) | 61.8% / 50% | File not found, malformed images, decode failures, Excel loading, didactic text | 4-6 hours |
|| `ManualLayoutManager.cs` | 69.3% / 64.1% | JSON parse errors, file permission failures, backup logic, variant constraints (20-25 missing tests) | 6-8 hours |
|| `LayoutEditorController.cs` | **73% / 58.7%** | Branch coverage expansion, variant switching, validation logic (12-15 missing tests) | 3-4 hours |
|| `StartupValidator.cs` | **77.9% / 77.3%** | Edge case expansion, validation scenarios, content set resolution (10-12 missing tests) | 2-3 hours |
|| `RadialExtensionAdjuster.cs` | **84.5% / 70.2%** | Branch coverage expansion, oscillation detection, complex multi-group (15-20 missing tests) | 5-7 hours |

### Utilities Coverage Opportunities

|| Utility | Lines | Current Status | Opportunity | Time Estimate |
||---------|-------|----------------|-------------|----------------|
|| `RadialExtensionCalculator.cs` | 385 | Has tests (223 lines) | **Private methods not tested** — complex geometry algorithms (6-8 hours) | 6-8 hours |
|| `GeometryMath.cs` | 210 | Has tests (329 lines) | Edge case expansion for minimum distance calculation | 30 min |
|| `ContentLoader.Images.cs` | 114 | Partial class | No dedicated tests — image loading/downscaling error paths | 2-3 hours |

**Coverage audit findings:**
- ✅ **ViewportCalculator.cs** — 93.8% / 100% coverage — already excellent, no action needed
- ✅ **RadialExtensionAdjuster**, **StartupValidator**, **LayoutEditorController** have existing tests but significant expansion opportunities
- ❌ **CompositePinApplicationService.cs** — **NO TEST FILE** — critical gap (264 lines)
- ❌ **ClusterCache.cs** — **NO TEST FILE** — critical gap (171 lines)
- ⚠️ **RadialExtensionCalculator** private methods contain complex untested geometry algorithms

**Estimated untested lines across all phases:** ~1,200-1,400 lines

### Excluded from This Plan

**MainWindow partials (0% coverage):** These require WPF UI integration and are better suited for manual smoke tests or GUI automation (e.g., FlaUI/Appium). Unit testing `MainWindow.Content.partial.cs` or `MainWindow.Navigation.partial.cs` would require extensive WPF mocking with low ROI.

**App.xaml.cs (0% coverage):** Application entry point — tested implicitly by startup validation.

**Interfaces:** IContentLoader.cs, ILogger.cs, IManualLayoutManager.cs, IZoomedMapResampler.cs — no implementation to test.

---

## Phase 1: Zero-Coverage Services (Primary Target)

### 1.1 MapNavigationService (Easiest — ~30 min) ✅ COMPLETED

**File:** `Services/MapNavigationService.cs` (49 lines)

**Why this first:**
- Zero dependencies (just `Stack<ZoomState>`)
- Pure logic, no I/O
- Straightforward test cases

**Test Class:** `Tests/MapNavigationServiceTests.cs` ✅ Created (9 tests passing)

**Test Coverage:**
```csharp
[Fact] public void InitialState_CanGoBackIsFalse()
[Fact] public void PushState_IncreasesStackDepth()
[Fact] public void PopState_ReturnsLastPushedState()
[Fact] public void PopState_DecreasesStackDepth()
[Fact] public void PopState_WhenEmpty_ReturnsNull()
[Fact] public void PushState_WithNull_DoesNothing()
[Fact] public void Clear_RemovesAllStates()
[Fact] public void CanGoBack_ReflectsStackState()
[Fact] public void MultiplePushPop_MaintainsLifoOrder()
```

**Estimated coverage gain:** 49 lines × 80% = 39 lines (~0.16% of total)

---

### 1.2 ManualLayoutOverrideStore (Easy — ~45 min)

**File:** `Services/ManualLayoutOverrideStore.cs` (75 lines)

**Why:**
- Pure in-memory dictionary operations
- No I/O, no dependencies
- Well-structured methods

**Test Class:** `Tests/ManualLayoutOverrideStoreTests.cs` ✅ Created (12 tests passing)

**Test Coverage:**
```csharp
[Fact] public void InitialState_HasNoPendingOverrides()
[Fact] public void SetOverride_StoresOverride()
[Fact] public void TryGetOverride_ReturnsStoredOverride()
[Fact] public void TryGetOverride_WhenNotSet_ReturnsFalse()
[Fact] public void GetAllOverrides_ReturnsAllStored()
[Fact] public void ClearOverrides_RemovesOverridesButNotEndpoints()
[Fact] public void RecordEndpoints_StoresEndpoints()
[Fact] public void TryGetEndpoints_ReturnsStoredEndpoints()
[Fact] public void TryGetEndpoints_WhenNotSet_ReturnsFalse()
[Fact] public void ClearAll_RemovesOverridesAndEndpoints()
[Fact] public void SetOverride_OverwritesPrevious()
[Fact] public void HasPendingOverrides_ReflectsState()
```

**Estimated coverage gain:** 75 lines × 85% = 64 lines (~0.26% of total)

---

### 1.3 ManualLayoutAssignmentEnricher (Easy — ~30 min) ⚠️ SKIPPED

**File:** `Services/ManualLayoutAssignmentEnricher.cs` (33 lines)

**Why:**
- Single method: `GetAssignments`
- Takes extensions + planning service, returns dictionary
- Needs mock `CompositePinPlanningService`

**Test Class:** `Tests/ManualLayoutAssignmentEnricherTests.cs`

**Status:** SKIPPED - Requires source code modification (interface extraction) to test. Per implementation guidelines, this requires approval and a separate refactoring plan.

**Test Coverage:**
```csharp
[Fact] public void GetAssignments_WithNoPlans_ReturnsEmpty()
[Fact] public void GetAssignments_WithCachedPlan_ReturnsAssignment()
[Fact] public void GetAssignments_WithMultiplePlans_ReturnsAll()
[Fact] public void GetAssignments_WithNoPlan_OmitsLocation()
[Fact] public void GetAssignments_WithNullPlan_OmitsLocation()
[Fact] public void GetAssignments_KeysByLocationName()
```

**Mock strategy:**
- Stub `CompositePinPlanningService.TryGetLastResult` to return test data

**Estimated coverage gain:** 33 lines × 80% = 26 lines (~0.11% of total)

---

### 1.4 AnimationFrameCache (Medium — ~90 min) ⚠️ SKIPPED

**File:** `Services/AnimationFrameCache.cs` (194 lines)

**Why:**
- Disk I/O testing with temp directories
- Cache versioning logic
- Bitmap encoding/decoding

**Test Class:** `Tests/AnimationFrameCacheTests.cs`

**Status:** SKIPPED - Requires source code modification (injectable cache directory path) to test. The constructor hardcodes AppData path. Per implementation guidelines, this requires approval and a separate refactoring plan.

**Test Coverage:**
```csharp
[Fact] public void Constructor_CreatesDirectory()
[Fact] public void Constructor_WritesVersionFile()
[Fact] public void TryLoadFrame_WhenNotCached_ReturnsNull()
[Fact] public void SaveFrame_ThenLoad_ReturnsFrame()
[Fact] public void ClearCache_RemovesDirectory()
[Fact] public void GetCacheKey_SameParams_ReturnsSameKey()
[Fact] public void GetCacheKey_DifferentParams_ReturnsDifferentKey()
[Fact] public void ValidateCacheVersion_WhenMismatch_ClearsCacheFiles()
[Fact] public void ValidateCacheVersion_WhenMissing_ClearsCacheFiles()
[Fact] public void ValidateCacheVersion_OnFirstRun_CreatesVersionFile()
[Fact] public void CleanupOldCacheFiles_RemovesPngFiles()
[Fact] public void TryLoadFrame_WithCorruptedFile_ReturnsNull()
[Fact] public void SaveFrame_WithInvalidPath_DoesNotThrow()
```

**Test fixture:**
```csharp
private static string CreateTempDir() => 
    Path.Combine(Path.GetTempPath(), "AnimationFrameCache_" + Guid.NewGuid().ToString("N"));

private static BitmapSource CreateTestBitmap(int width, int height) { /* ... */ }
```

**Estimated coverage gain:** 194 lines × 70% = ~136 lines (~0.55% of total)

---

### 1.5 FileLogger (Hard — ~3.5 hours including refactor)

**File:** `Services/FileLogger.cs` (98 lines)

**Why difficult:**
- Background thread with `BlockingCollection<string>`
- Static state (`_writerThread`, `_queue`)
- File I/O and console output interleaved
- **Current design is not testable without refactoring**

**Refactor Required (2 hours before testing):**

Extract log path resolution to interface for dependency injection:

```csharp
public interface ILogPathProvider
{
    string GetLogDirectory();
    string GetLogFileName();
}

public class DefaultLogPathProvider : ILogPathProvider
{
    public string GetLogDirectory()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return Path.Combine(appData, "InteractiveWorldMap", "logs");
    }
    
    public string GetLogFileName() => "app.log";
}
```

Update FileLogger constructor:
```csharp
public FileLogger(ILogPathProvider? pathProvider = null)
{
    _pathProvider = pathProvider ?? new DefaultLogPathProvider();
    lock (_initLock)
    {
        _instanceCount++;
        if (_writerThread == null)
            Initialize();
    }
}

private void Initialize()
{
    var logDir = _pathProvider.GetLogDirectory();
    try { Directory.CreateDirectory(logDir); } catch { }

    _logFilePath = Path.Combine(logDir, _pathProvider.GetLogFileName());
    // ... rest of initialization
}
```

**Test Class:** `Tests/FileLoggerTests.cs` (1.5 hours after refactor)

**Test Coverage:**
```csharp
[Fact] public async Task LogInfo_WritesToFile()
[Fact] public async Task LogWarning_WritesToFile()
[Fact] public async Task LogError_WritesToFile()
[Fact] public async Task LogError_WithException_IncludesMessage()
[Fact] public async Task MultipleInstances_ShareQueue()
[Fact] public async Task Dispose_CompletesAdding_WhenLastInstance()
[Fact] public async Task Initialize_CreatesLogDirectory()
[Fact] public async Task WriteLog_WhenQueueFull_DropsMessage()
[Fact] public void Constructor_Increments_InstanceCount()
[Fact] public void Dispose_Decrements_InstanceCount()
```

**Test implementation approach:**
```csharp
private class TestLogPathProvider : ILogPathProvider
{
    private readonly string _testDir;
    
    public TestLogPathProvider(string testDir) => _testDir = testDir;
    public string GetLogDirectory() => _testDir;
    public string GetLogFileName() => "test.log";
}

[Fact]
public async Task LogInfo_WritesToFile()
{
    var testDir = TestFixtures.CreateTempDirectory("FileLogger");
    var provider = new TestLogPathProvider(testDir);
    var logger = new FileLogger(provider);
    
    logger.LogInfo("Test message");
    logger.Dispose();
    
    await AsyncTestHelpers.WaitForFileContentAsync(
        Path.Combine(testDir, "test.log"), 
        expectedLineCount: 1, 
        timeout: TimeSpan.FromSeconds(2));
    
    var lines = File.ReadAllLines(Path.Combine(testDir, "test.log"));
    Assert.Contains("Test message", lines[0]);
    Assert.Contains("[INFO]", lines[0]);
}
```

**Test challenges:**
1. **Static state:** Use `[Collection("FileLogger")]` attribute to force sequential execution
2. **Background thread:** Add async helper to wait for flush (see Test Infrastructure section)
3. **File I/O:** Use temp directory per test with injected path provider

**Estimated coverage gain (after refactor):** 98 lines × 70% = ~69 lines (~0.28% of total)

**Why refactor approach:**
- ✅ No reflection hacks (maintainable)
- ✅ Better design (testable by default)
- ✅ More reliable tests
- ✅ Reusable for future file-based services
- ⚠️ Requires 2 hours upfront refactor time

**Phase 1 Total Estimated Gain:** 334 lines (~1.36% of 24,600 total) with conservative 70-85% coverage assumptions

---

## Phase 2: Critical Services with NO Test Files

### 2.1 CompositePinApplicationService (HIGH PRIORITY — ~5-7 hours)

**File:** `Services/CompositePinApplicationService.cs` (264 lines)

**Why critical:**
- **NO TEST FILE EXISTS** — completely untested
- Orchestrates composite pin cache orchestration
- Contains complex projection math
- Critical for composite pin functionality

**Test Class:** `Tests/CompositePinApplicationServiceTests.cs` (NEW FILE)

**Test Coverage:**
```csharp
// Cache orchestration
[Fact] public void TryCacheLoad_WithCacheHit_ReturnsPlansAndKey()
[Fact] public void TryCacheLoad_WithCacheMiss_ReturnsNull()
[Fact] public void TryCacheLoad_ComputesCorrectCacheKey()
[Fact] public void SaveIfMissed_WithValidResults_SavesToCache()
[Fact] public void SaveIfMissed_WithEmptyResults_DoesNotSave()
[Fact] public void InvalidateGroup_RemovesCacheFile()

// Projection math
[Fact] public void ResolveOriginalPosition_ScalesToViewport()
[Fact] public void ResolveExtendedPosition_WithAngledProjection_ComputesCorrectly()
[Fact] public void ResolveExtendedPosition_WithSourceProjection_ComputesCorrectly()
[Fact] public void ProjectSourceExtendedPosition_ScalesByViewportRatio()
[Fact] public void ProjectAngledExtendedPosition_UsesAngleMath()

// Instruction building
[Fact] public void BuildApplyInstructions_WithValidApplications_BuildsCorrectInstructions()
[Fact] public void BuildApplyInstructions_WithNullViewport_ThrowsArgumentNullException()
[Fact] public void BuildInstruction_ComputesCorrectTransform()

// Error handling
[Fact] public void TryCacheLoad_WithNullLayout_ThrowsArgumentNullException()
[Fact] public void TryCacheLoad_WithNullConfig_ThrowsArgumentNullException()
[Fact] public void ResolveCachedPlan_WithCacheMiss_ReturnsNull()
```

**Mock strategy:**
- Mock `CompositePinPlanCache` for cache operations
- Mock `CompositePinPlanningService` for plan retrieval
- Use test `ManualLayout` and `PinPartConfig` objects

**Estimated coverage gain:** 264 lines × 75% = ~198 lines (~0.8% of total)

---

### 2.2 ClusterCache (HIGH PRIORITY — ~3-4 hours)

**File:** `Services/ClusterCache.cs` (171 lines)

**Why critical:**
- **NO TEST FILE EXISTS** — completely untested
- Cache hit/miss logic for clustering performance
- Hash validation for cache invalidation
- Legacy migration logic

**Test Class:** `Tests/ClusterCacheTests.cs` (NEW FILE)

**Test Coverage:**
```csharp
// Cache hit/miss
[Fact] public void TryLoad_WithCacheHit_ReturnsClusters()
[Fact] public void TryLoad_WithCacheMiss_ReturnsNull()
[Fact] public void TryLoad_WithMissingCacheFile_ReturnsNull()
[Fact] public void TryLoad_WithStaleCache_HashMismatch_ReturnsNull()

// Hash validation
[Fact] public void TryLoad_WithMatchingHash_ValidatesCorrectly()
[Fact] public void ComputeHash_WithSameLocations_ReturnsSameHash()
[Fact] public void ComputeHash_WithDifferentOrder_ReturnsSameHash()
[Fact] public void ComputeHash_WithDifferentThreshold_ReturnsDifferentHash()
[Fact] public void ComputeHash_WithDifferentLocations_ReturnsDifferentHash()

// Cache operations
[Fact] public void Save_WithValidClusters_SavesToDisk()
[Fact] public void Save_WithFileLocked_DoesNotThrow()
[Fact] public void LocationKey_GeneratesConsistentKeys()

// Location resolution
[Fact] public void TryLoad_WithStaleLocationKey_ReturnsNull()
[Fact] public void TryLoad_WithValidLocationKey_ResolvesCorrectly()

// Legacy migration
[Fact] public void Constructor_MigratesLegacyCache_WhenExists()
[Fact] public void Constructor_DoesNotMigrate_WhenNewCacheExists()
```

**Test fixture:**
```csharp
private static string CreateTempCacheDir() => 
    Path.Combine(Path.GetTempPath(), "ClusterCache_" + Guid.NewGuid().ToString("N"));

private static List<Location> CreateTestLocations(int count) { /* ... */ }
```

**Estimated coverage gain:** 171 lines × 75% = ~128 lines (~0.52% of total)

---

**Phase 2 Total Estimated Gain:** 326 lines (~1.32% of total)

---

## Implementation Guidelines

### Source Code Modifications

**DO NOT modify source code without explicit approval.** This test coverage initiative is focused on adding tests only.

If you encounter a service that is not testable without source code changes:
1. Skip that service and move to the next one
2. Document the testability limitation in the plan
3. Create a separate exec plan for the required refactoring if needed
4. Continue with other testable services

**Exception:** The FileLogger refactor for testability was explicitly approved in the plan as part of Phase 1.5. No other source code modifications are authorized.

### Bug Discovery During Testing

If you discover bugs while implementing test coverage, follow this process:

1. **Do NOT fix the bug immediately** — stay focused on test coverage
2. **Document the bug** in the relevant exec plan or create a new bug-specific plan file in `docs/exec-plans/active/`
3. **Add a TO_DO.md entry** pointing to the bug plan for tracking
4. **Write an xfail test** that demonstrates the bug and will pass when fixed:
   ```csharp
   [Fact(Skip = "Bug: Describe the bug and reference plan file")]
   public void TestName_ThatDemonstratesBug()
   {
       // Test that currently fails due to bug
       // When bug is fixed, remove Skip attribute
   }
   ```
5. **Continue with test coverage** — the xfail test documents the issue without blocking progress

**Example TO_DO.md entry:**
```markdown
- [ ] Fix RadialExtensionCalculator angle wrap-around bug at 360° (see docs/exec-plans/active/radial-extension-bug-fix.md)
```

**Rationale:** This keeps the coverage initiative focused while ensuring bugs are properly documented and tracked for future fixes.

---

### Testability Limitations Discovered

During implementation, the following services were found to require source code modifications for testability:

#### ManualLayoutAssignmentEnricher
- **Issue:** Requires mocking `CompositePinPlanningService` which is a concrete class
- **Needed change:** Extract `ICompositePinPlanningService` interface
- **Impact:** Low risk, well-contained change
- **Refactoring effort:** ~30 minutes
- **Approval required:** ✅ APPROVED - see [service-testability-refactoring-plan.md](./service-testability-refactoring-plan.md)

#### AnimationFrameCache
- **Issue:** Constructor hardcodes `Environment.SpecialFolder.ApplicationData` path
- **Needed change:** Inject cache directory path via constructor
- **Impact:** Low risk, maintains backward compatibility with default path
- **Refactoring effort:** ~1 hour
- **Approval required:** ✅ APPROVED - see [service-testability-refactoring-plan.md](./service-testability-refactoring-plan.md)

#### FileLogger
- **Issue:** Already documented in plan as requiring refactor for testability
- **Needed change:** Extract `ILogPathProvider` interface (already detailed in Phase 1.5)
- **Approval required:** Yes - explicitly approved as part of Phase 1.5

**Refactoring Plan:** See [service-testability-refactoring-plan.md](./service-testability-refactoring-plan.md) for detailed implementation steps, impact analysis, and verification checklist.

**Recommendation:** Complete the refactoring plan (1.5-2 hours), then resume test coverage work for ManualLayoutAssignmentEnricher and AnimationFrameCache.

---

## Phase 3: Error Paths + High-Value Expansions

### 3.1 ContentLoader Error Handling (~4-6 hours)

**File:** `Services/ContentLoader.cs` (current: 61.8% / 50%)

**Target:** Error paths for file I/O failures, Excel loading, didactic text

**New Tests for `ContentLoaderTests.cs`:**
```csharp
// File not found errors
[Fact] public async Task LoadMapImageAsync_WhenFileNotFound_ThrowsFileNotFoundException()
[Fact] public async Task LoadLocationContentAsync_WhenFolderMissing_ReturnsNull()
[Fact] public async Task LoadPinPartGeometry_WhenFileNotFound_ReturnsNull()

// Image decode failures
[Fact] public async Task LoadAllLocationImagesAsync_WhenImageCorrupted_SkipsImage()
[Fact] public void TryLoadContentBitmap_WhenInvalidImageFormat_ReturnsNull()
[Fact] public async Task LoadImageDownscaled_WithInvalidPath_ReturnsNull()

// Excel loading
[Fact] public async Task LoadLocationsAsync_WhenExcelCorrupted_ThrowsInvalidDataException()
[Fact] public async Task LoadLocationsAsync_WhenExcelMissing_FallsBackToJson()
[Fact] public async Task LoadLocationsAsync_WhenBothMissing_ReturnsEmpty()

// Didactic text
[Fact] public async Task LoadDidacticTextAsync_WhenFileNotFound_ReturnsNull()
[Fact] public async Task LoadDidacticTextAsync_WhenEncodingInvalid_HandlesGracefully()

// Sidecar files
[Fact] public async Task TryReadSidecarTextAsync_WhenFileLockedByAnotherProcess_ReturnsNull()
[Fact] public async Task TryReadSidecarTextAsync_WhenFileNotFound_ReturnsNull()

// Content set resolution
[Fact] public void ResolveActiveContentSet_WithDemoSet_ReturnsDemo()
[Fact] public void ResolveActiveContentSet_WithProductionSet_ReturnsProduction()
[Fact] public void ResolveActiveContentSet_WithLegacySet_ReturnsLegacy()

// Cache key fallback
[Fact] public void ComputeLocationCacheKey_WithBlankId_UsesName()
[Fact] public void ComputeLocationCacheKey_WithId_UsesId()

// Asset path resolution
[Fact] public void ResolveAssetPath_WithAssetsFolder_ReturnsAssetsPath()
[Fact] public void ResolveAssetPath_WithoutAssetsFolder_ReturnsRootPath()
```

**Estimated coverage gain:** ~120 lines (~0.49% of total)

---

### 3.2 ManualLayoutManager Error Handling (~6-8 hours)

**File:** `Services/ManualLayoutManager.cs` (current: 69.3% / 64.1%)

**Target:** JSON parse errors, file permission failures, backup logic, variant constraints

**New Tests for `ManualLayoutManagerTests.cs`:**
```csharp
// Error handling
[Fact] public void LoadLayoutCollection_WhenJsonMalformed_ReturnsEmpty()
[Fact] public void LoadLayoutCollection_WhenFileNotFound_ReturnsEmpty()
[Fact] public void SaveLayoutCollection_WhenDirectoryReadOnly_LogsError()
[Fact] public void TryBackupUnreadableFile_CreatesBackupWithTimestamp()
[Fact] public void TryBackupUnreadableFile_WhenBackupFails_DoesNotThrow()
[Fact] public void SaveVariant_WhenDiskFull_ReturnsFalse()

// Variant constraints
[Fact] public void SaveVariant_WhenManualCapReached_ReturnsFalse()
[Fact] public void SaveVariant_WhenImportedCapReached_ReturnsFalse()
[Fact] public void SaveVariant_AutoSeedRefusesToOverwriteManual_ReturnsFalse()
[Fact] public void DeleteVariant_WhenAutoVariant_ReturnsFalse()
[Fact] public void DeleteVariant_WhenOnlyVariant_ReturnsFalse()

// Variant operations
[Fact] public void GetAllLayoutKeys_ReturnsAllKeys()
[Fact] public void SetDefaultVariant_UpdatesDefaultVariant()
[Fact] public void SetSelectedVariantId_UpdatesSelectedVariant()
[Fact] public void GetSelectedVariantId_ReturnsSelectedVariant()
[Fact] public void LoadVariant_WhenNotFound_ReturnsNull()
[Fact] public void ListVariants_WithInvalidGroupKey_ReturnsEmpty()

// Compatible key matching
[Fact] public void FindCompatibleGroup_WithSimilarZoom_ReturnsGroup()
[Fact] public void FindCompatibleLayout_WithCompatibleKey_ReturnsLayout()
[Fact] public void SelectPreferredVariant_WithManualVariant_SelectsManual()
[Fact] public void SelectPreferredVariant_WithAutoSeed_SelectsAutoSeed()

// Legacy migration
[Fact] public void LoadLayoutCollection_MigratesLegacyFlatLayout()
[Fact] public void NormalizeCollection_FixesLegacyIndex()
```

**Estimated coverage gain:** ~150 lines (~0.61% of total)

---

### 3.3 RadialExtensionAdjuster Expansion (~5-7 hours)

**File:** `Services/RadialExtensionAdjuster.cs` (current: 84.5% / 70.2%)

**Target:** Branch coverage expansion, oscillation detection, complex multi-group scenarios

**New Tests for `RadialExtensionAdjusterTests.cs`:**
```csharp
// Line-to-marker proximity
[Fact] public void DoesLinePassTooCloseToMarker_WithCloseLine_ReturnsTrue()
[Fact] public void DoesLinePassTooCloseToMarker_WithDistantLine_ReturnsFalse()

// Oscillation detection
[Fact] public void AdjustExtensions_DetectsOscillation_ReducesNudgeMultiplier()
[Fact] public void AdjustExtensions_WithOscillatingPair_AppliesLengthSeparation()
[Fact] public void AdjustExtensions_ReachesMaxIterations_LogsWarning()

// Complex multi-group scenarios
[Fact] public void AdjustExtensions_WithMultipleGroups_ResolvesAllOverlaps()
[Fact] public void AdjustExtensions_WithCircularDependencies_HandlesCorrectly()

// Edge cases
[Fact] public void AdjustExtensions_WithAngleAt360_WrapsCorrectly()
[Fact] public void AdjustExtensions_WithNearMissIntersection_DetectsCorrectly()
[Fact] public void AdjustExtensions_WithMinimumLineLengthConstraint_RespectsConstraint()

// Config variations
[Fact] public void AdjustExtensions_WithDifferentAngleNudgeThreshold_UsesThreshold()
[Fact] public void AdjustExtensions_WithDifferentProximityThreshold_UsesThreshold()

// Protected locations
[Fact] public void ProtectedFromOverlapAdjustment_WithProtectedLocation_SkipsAdjustment()
```

**Estimated coverage gain:** ~100 lines (~0.41% of total)

---

### 3.4 LayoutEditorController Expansion (~3-4 hours)

**File:** `Services/LayoutEditorController.cs` (current: 73% / 58.7%)

**Target:** Branch coverage expansion, variant switching, validation logic

**New Tests for `LayoutEditorControllerTests.cs`:**
```csharp
// Variant operations
[Fact] public void SwitchToVariant_WithValidVariant_SwitchesSuccessfully()
[Fact] public void GetVariants_ReturnsAllVariants()
[Fact] public void TrySaveAsVariant_WithValidName_CreatesVariant()
[Fact] public void TrySaveAsVariant_WithEmptyName_ReturnsFalse()
[Fact] public void TryDeleteActiveVariant_WithActiveVariant_DeletesSuccessfully()

// Validation expansion
[Fact] public void ValidateLayout_DetectsLineToMarkerProximity()
[Fact] public void ValidateLayout_DetectsLineIntersections()

// Source coordinate reprojection
[Fact] public void CreateLayoutApplications_WithSourceCoords_ReprojectsCorrectly()

// Variant ID generation
[Fact] public void MakeVariantId_WithValidName_GeneratesSlug()
[Fact] public void MakeVariantId_WithSpecialChars_SanitizesCorrectly()

// Edge cases
[Fact] public void TrySaveAsVariant_WithNoVariants_HandlesCorrectly()
[Fact] public void VariantsChanged_WhenVariantChanges_FiresEvent()
[Fact] public void TrySaveAsVariant_WithManualOrigin_HandlesCorrectly()
```

**Estimated coverage gain:** ~80 lines (~0.33% of total)

---

### 3.5 StartupValidator Expansion (~2-3 hours)

**File:** `Services/StartupValidator.cs` (current: 77.9% / 77.3%)

**Target:** Edge case expansion, validation scenarios, content set resolution

**New Tests for `StartupValidatorTests.cs`:**
```csharp
// Content validation
[Fact] public void ValidateEnvironment_WhenWorldMapMissing_AddsError()
[Fact] public void ValidateEnvironment_WhenExcelMissing_AddsWarning()
[Fact] public void ValidateEnvironment_WithProductionSet_ValidatesProduction()

// Location field validation
[Fact] public void ValidateLocationsJson_WithMissingIdField_AddsWarning()
[Fact] public void ValidateLocationsJson_WithMissingNameField_AddsWarning()
[Fact] public void ValidateLocationsJson_WithMissingPixelX_AddsWarning()
[Fact] public void ValidateLocationsJson_WithMissingPixelY_AddsWarning()

// Coordinate validation
[Fact] public void AddCoordinateWarning_WithOutOfRangeCoordinate_AddsWarning()
[Fact] public void AddCoordinateWarning_WithValidCoordinate_DoesNotAddWarning()

// Content set resolution
[Fact] public void ValidateEnvironment_WithDemoSet_ResolvesDemo()
[Fact] public void ValidateEnvironment_WithProductionSet_ResolvesProduction()
[Fact] public void ValidateEnvironment_WithLegacySet_LogsFallbackWarning()

// Error handling
[Fact] public void ValidateEnvironment_WithException_LogsError()
[Fact] public void ResolveAssetPath_WithAssetsFolder_ReturnsAssetsPath()
[Fact] public void ResolveAssetPath_WithoutAssetsFolder_ReturnsRootPath()
```

**Estimated coverage gain:** ~60 lines (~0.24% of total)

---

### 3.6 RadialExtensionCalculator Private Methods (~6-8 hours)

**File:** `Utilities/RadialExtensionCalculator.cs` (385 lines, has tests but private methods untested)

**Why critical:**
- Complex geometry algorithms in private methods
- Currently only tested indirectly through public API
- High risk of bugs in angle adjustment, convergence detection, intersection resolution

**Strategy:** Extract private methods to internal testable class or use InternalsVisibleTo

**New Tests for `RadialExtensionCalculatorTests.cs`:**
```csharp
// Angle adjustment (currently private)
[Fact] public void NudgeAnglesApart_WithConvergingAngles_SeparatesThem()
[Fact] public void NudgeAnglesApart_WithDivergentAngles_DoesNothing()
[Fact] public void PreventConvergingLines_WithConvergingLines_AdjustsAngles()
[Fact] public void PreventLineIntersections_WithIntersectingLines_ResolvesIntersection()

// Wrap-around logic
[Fact] public void FindAngularPairsWithinThreshold_WithWrapAround_DetectsPairs()
[Fact] public void FindAngularPairsWithinThreshold_AtThresholdBoundary_DetectsCorrectly()
[Fact] public void SafeNudgeApart_WithCircularOrdering_PreservesOrder()

// Boundary calculations
[Fact] public void CalculateMaxLength_WithCanvasBounds_RespectsBounds()
[Fact] public void CalculateMaxLength_WithNoBounds_ReturnsMax()
```

**Estimated coverage gain:** ~80 lines (~0.33% of total)

---

### 3.7 ContentLoader.Images Tests (~2-3 hours)

**File:** `Services/ContentLoader.Images.cs` (114 lines, partial class)

**Target:** Image loading/downscaling error paths

**New Tests for `ContentLoaderTests.cs`:**
```csharp
// File size checking
[Fact] public void WarnIfHeavyImageFile_WithLargeFile_LogsWarning()
[Fact] public void WarnIfHeavyImageFile_WithSmallFile_DoesNotLog()
[Fact] public void WarnIfHeavyImageFile_RaisesLargeImageDetectedEvent()

// Image loading
[Fact] public void LoadImageDownscaled_WithValidImage_ReturnsBitmap()
[Fact] public void LoadImageDownscaled_WithInvalidPath_ReturnsNull()
[Fact] public void LoadFrozenBitmap_WithValidImage_ReturnsFrozenBitmap()
[Fact] public void LoadFrozenBitmap_WithInvalidPath_Throws()

// Decode width calculation
[Fact] public void ComputeDecodeWidth_WithMaxWidth_ReturnsCalculatedWidth()
[Fact] public void ComputeDecodeWidth_WithMaxHeight_ReturnsCalculatedWidth()
[Fact] public void ComputeDecodeWidth_WithNoConstraints_ReturnsZero()
[Fact] public void ComputeDecodeWidth_WithBothConstraints_UsesSmaller()
```

**Estimated coverage gain:** ~70 lines (~0.28% of total)

---

**Phase 3 Total Estimated Gain:** ~660 lines (~2.68% of total)

---

## Summary

|| Phase | Focus | Estimated Lines | % Gain | Difficulty | Time Estimate |
||-------|-------|----------------|--------|------------|----------------|
|| **Phase 1** | Zero-coverage Services | 334 | 1.36% | Easy-Medium | 6-8 hours |
|| **Phase 2** | NO test file Services | 326 | 1.32% | Medium-High | 8-11 hours |
|| **Phase 3** | Error paths + expansions | 660 | 2.68% | Medium-Hard | 28-41 hours |
|| **Total** | | **1,320 lines** | **~5.36%** | | **42-60 hours** |

**Conservative line coverage gain:** 5.36% (44.7% → 50.06%)  
**Branch coverage gain (estimated):** 4-5% (39.8% → 44-44.8%)

**Why this achieves 5%:**
- ✅ Addresses critical gaps (CompositePinApplicationService, ClusterCache)
- ✅ Uses conservative 70-80% coverage estimates
- ✅ Includes high-value expansions in existing test files
- ✅ Covers complex geometry algorithms (RadialExtensionCalculator)
- ✅ Comprehensive error path coverage

---

## Implementation Order (Recommended)

### Week 1: Foundation + Quick Wins
1. **Day 1:** FileLogger refactor for testability (2 hours)
2. **Day 2:** MapNavigationServiceTests (30 min) + ManualLayoutAssignmentEnricherTests (30 min)
3. **Day 3:** ManualLayoutOverrideStoreTests (45 min)
4. **Day 4:** AnimationFrameCacheTests (90 min)
5. **Day 5:** FileLoggerTests (1.5 hours after refactor)

### Week 2: Critical Gap Closure
6. **Day 1-2:** CompositePinApplicationServiceTests (5-7 hours) — **CRITICAL**
7. **Day 3-4:** ClusterCacheTests (3-4 hours) — **CRITICAL**

### Week 3: Error Paths (ContentLoader + ManualLayoutManager)
8. **Day 1-2:** ContentLoader error paths (4-6 hours)
9. **Day 3-5:** ManualLayoutManager error paths (6-8 hours)

### Week 4: High-Value Expansions
10. **Day 1-2:** RadialExtensionAdjuster expansion (5-7 hours)
11. **Day 3:** LayoutEditorController expansion (3-4 hours)
12. **Day 4:** StartupValidator expansion (2-3 hours)
13. **Day 5:** Buffer/overflow

### Week 5: Advanced Coverage
14. **Day 1-2:** RadialExtensionCalculator private methods (6-8 hours)
15. **Day 3:** ContentLoader.Images tests (2-3 hours)
16. **Day 4-5:** Coverage verification, CI gate adjustments, documentation

**Total effort:** ~42-60 hours (5-7 weeks at 8-12 hours/week)

---

## Test Infrastructure Additions

### 1. Shared Test Fixtures (Create `Tests/TestHelpers/TestFixtures.cs`)

```csharp
public static class TestFixtures
{
    public static string CreateTempDirectory(string prefix = "test")
    {
        var path = Path.Combine(Path.GetTempPath(), $"{prefix}_{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }

    public static void CleanupTempDirectory(string path)
    {
        if (Directory.Exists(path))
            Directory.Delete(path, recursive: true);
    }

    public static BitmapSource CreateTestBitmap(int width, int height, Color color)
    {
        var bitmap = new WriteableBitmap(width, height, 96, 96, PixelFormats.Bgra32, null);
        var pixels = new byte[width * height * 4];
        for (int i = 0; i < pixels.Length; i += 4)
        {
            pixels[i] = color.B;
            pixels[i + 1] = color.G;
            pixels[i + 2] = color.R;
            pixels[i + 3] = color.A;
        }
        bitmap.WritePixels(new Int32Rect(0, 0, width, height), pixels, width * 4, 0);
        bitmap.Freeze();
        return bitmap;
    }

    public static string CreateMalformedJson() => "{ invalid json structure";

    public static string CreateLockedFile(string path)
    {
        File.WriteAllText(path, "locked content");
        var fs = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.None);
        // Caller must dispose fs to release lock
        return path;
    }
}
```

### 2. Async Test Helpers (Add to `Tests/TestHelpers/AsyncTestHelpers.cs`)

```csharp
public static class AsyncTestHelpers
{
    public static async Task<bool> WaitForConditionAsync(
        Func<bool> condition, 
        TimeSpan timeout, 
        TimeSpan pollInterval = default)
    {
        if (pollInterval == default)
            pollInterval = TimeSpan.FromMilliseconds(50);

        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (condition())
                return true;
            await Task.Delay(pollInterval);
        }
        return false;
    }

    public static async Task WaitForFileContentAsync(
        string path, 
        int expectedLineCount, 
        TimeSpan timeout)
    {
        var success = await WaitForConditionAsync(
            () => File.Exists(path) && File.ReadAllLines(path).Length >= expectedLineCount,
            timeout);
        
        if (!success)
            throw new TimeoutException($"File {path} did not reach {expectedLineCount} lines within {timeout}");
    }
}
```

---

## Coverage Verification

After implementation, run:
```bash
.\scripts\verify.ps1
py -3 scripts\summarize_coverage.py --results-directory TestResults\verify-coverage --min-line-coverage 49 --min-branch-coverage 44
```

**Expected output:**
```
Line coverage:    50.1% (threshold: 49%, PASS)
Branch coverage:  44.5% (threshold: 44%, PASS)
```

**If below target:**
- Run individual test coverage to identify gaps
- Add more edge cases to existing tests
- Focus on remaining error paths

---

## Risks & Mitigations

|| Risk | Impact | Mitigation |
||------|--------|------------|
|| WPF bitmap tests need STA thread | Tests fail on CI | Wrap in `[STAThread]` or use `Task.Factory.StartNew` with appropriate options |
|| FileLogger static state causes flaky tests | Tests interfere with each other | Use `[Collection("FileLogger")]` to force sequential execution |
|| CompositePinApplicationService complex mocking | Tests hard to set up | Create comprehensive test fixture with pre-built mock objects |
|| RadialExtensionCalculator private methods | Cannot test directly | Extract to internal class or use `InternalsVisibleTo` attribute |
|| Time estimate overrun | Plan takes longer than expected | Phase implementation, prioritize critical gaps first |
|| CI gate too aggressive | PRs blocked during implementation | Keep current gates (42% line, 37% branch) until new baseline stable |

---

## Success Criteria

✅ **Minimum:** 49% line coverage, 44% branch coverage (conservative target)  
✅ **Target:** 50% line coverage, 45% branch coverage (original goal)  
✅ **Tests pass:** All new tests green on first run  
✅ **No flaky tests:** New tests pass 10 consecutive runs  
✅ **Critical gaps closed:** CompositePinApplicationService and ClusterCache have test files  
✅ **CI gates stable:** Current gates (42% line, 37% branch) pass consistently  
✅ **Documentation:** Each test class has XML doc summary explaining purpose

---

## Post-Implementation

1. Update `docs/TO_DO.md` to reflect new baseline (50% coverage)
2. Update `.runsettings` minimum thresholds if new baseline is stable:
   ```xml
   <MinimumLineCoverage>47</MinimumLineCoverage>
   <MinimumBranchCoverage>42</MinimumBranchCoverage>
   ```
3. Add to `CHANGELOG.md`:
   ```markdown
   ### Changed
   - Increased test coverage from 44.7% to 45.0% line coverage by adding tests for 
     MapNavigationService and ManualLayoutOverrideStore (Phase 1 partial completion).
   - Skipped AnimationFrameCache and ManualLayoutAssignmentEnricher tests due to 
     testability limitations requiring source code refactoring (see Testability Limitations section).
   ```

---

## Appendix: Quick Reference

**Total production lines:** ~24,600
**Current coverage:** 44.7% line / 39.8% branch
**Target coverage:** 50% line / 45% branch
**Estimated effort:** 42-60 hours (5-7 weeks)
**Test files created:** 2 new files (MapNavigationServiceTests ✅, ManualLayoutOverrideStoreTests ✅)
**Test files skipped:** 2 due to testability (ManualLayoutAssignmentEnricher, AnimationFrameCache - see Testability Limitations section)
**Test files to create:** 5 remaining (FileLogger, CompositePinApplicationService, ClusterCache, plus 2 after refactoring)
**Test files to expand:** 7 existing files (ContentLoader, ManualLayoutManager, RadialExtensionAdjuster, LayoutEditorController, StartupValidator, RadialExtensionCalculator, ContentLoader.Images partial)
