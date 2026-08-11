---
status: active
owner: agent
started: 2026-08-10
---

# Service Testability Refactoring Plan

**Status:** Draft  
**Created:** 2026-08-10  
**Related Plan:** [test-coverage-5pct-increase-plan.md](./test-coverage-5pct-increase-plan.md)  
**Estimated Effort:** 1.5-2 hours  

## Purpose

This plan addresses testability limitations discovered during test coverage implementation. Two Services require minor refactoring to enable unit testing without source code modifications in test files.

**Goal:** Enable focused unit coverage for `ManualLayoutAssignmentEnricher`, `CompositePinApplicationService.SaveIfMissed`, and `AnimationFrameCache` without changing runtime behavior.

**Architecture:** Keep runtime service construction unchanged in `MainWindow.xaml.cs`. Extract only the narrow "last planning result" dependency used by save/cache paths, and inject only the animation-frame cache directory needed for isolated disk tests.

**Tech Stack:** WPF / .NET 6 / C#, xUnit, hand-written test fakes, temp-directory fixtures.

## Scope

### 1. ManualLayoutAssignmentEnricher Testability (~30 min)

**File:** `Services/ManualLayoutAssignmentEnricher.cs` (33 lines)

**Current Issue:**
- `GetAssignments` method takes concrete `CompositePinPlanningService` parameter
- Cannot mock the service in tests without interface
- Prevents testing assignment extraction logic

**Required Change:**
Extract a role-based `ICompositePinPlanningResultProvider` interface and update only consumers that need the last-result lookup. Do not expose `BuildPlan` on this interface.

**Implementation Steps:**

1. Create `Services/ICompositePinPlanningResultProvider.cs`:
   ```csharp
   using InteractiveWorldMap.Models;

   namespace InteractiveWorldMap.Services
   {
       /// <summary>
       /// Supplies the last composite-pin planning result built for a location.
       /// </summary>
       public interface ICompositePinPlanningResultProvider
       {
           /// <summary>
           /// Returns the last plan built for locationId in this session.
           /// Returns false if no plan has been built yet for that location.
           /// </summary>
           bool TryGetLastResult(string locationId, out CompositePinPlanningResult? result);
       }
   }
   ```

2. Update `Services/CompositePinPlanningService.cs`:
   - Add `: ICompositePinPlanningResultProvider` to class declaration
   - No other changes needed (already implements the method)

3. Update `Services/ManualLayoutAssignmentEnricher.cs`:
   - Change parameter type from `CompositePinPlanningService` to `ICompositePinPlanningResultProvider`
   - No other changes needed

4. Update `Services/CompositePinApplicationService.cs`:
   - Change field and constructor parameter type from `CompositePinPlanningService` to `ICompositePinPlanningResultProvider`
   - `MainWindow.xaml.cs` can still pass the concrete `CompositePinPlanningService`

5. Update all call sites:
   - `MainWindow.xaml.cs`: service field initializer remains concrete; constructor call to `CompositePinApplicationService` should still compile because the concrete class implements the interface
   - Existing `CompositePinPlanningServiceTests` continue to construct the concrete service directly
   - New tests use a small hand-written fake instead of adding a mocking package

6. Add `Tests/ManualLayoutAssignmentEnricherTests.cs`:
   ```csharp
   private sealed class FakePlanningResultProvider : ICompositePinPlanningResultProvider
   {
       private readonly Dictionary<string, CompositePinPlanningResult?> _results =
           new Dictionary<string, CompositePinPlanningResult?>(StringComparer.Ordinal);

       public void Add(string locationId, CompositePinPlanningResult? result)
       {
           _results[locationId] = result;
       }

       public bool TryGetLastResult(string locationId, out CompositePinPlanningResult? result)
       {
           return _results.TryGetValue(locationId, out result);
       }
   }
   ```
   Cover at least:
   - `GetAssignments_WithNoPlans_ReturnsEmpty`
   - `GetAssignments_WithCachedPlan_ReturnsAssignment`
   - `GetAssignments_WithMultiplePlans_ReturnsAll`
   - `GetAssignments_WithNoPlan_OmitsLocation`
   - `GetAssignments_WithNullPlan_OmitsLocation`
   - `GetAssignments_KeysByLocationName`

7. Add or expand `Tests/CompositePinApplicationServiceTests.cs` for `SaveIfMissed`:
   - Use the same fake result provider type or a local equivalent
   - Assert no save occurs when no matching plans exist
   - Assert matching locations are saved as `CachedCompositePlanEntry`
   - Assert missing/null results are omitted

**Impact Analysis:**
- **Risk:** Very low - pure interface extraction, no behavior changes
- **Breaking changes:** None - concrete class still implements same interface
- **Backward compatibility:** Full - all existing code continues to work
- **Performance:** No impact
- **Package impact:** None - do not add Moq/NSubstitute unless a later plan adopts a repo-wide mocking convention

**Verification:**
- Build succeeds: `dotnet build InteractiveWorldMap.sln`
- Focused tests pass: `dotnet test Tests/InteractiveWorldMap.Tests.csproj --filter "FullyQualifiedName~ManualLayoutAssignmentEnricherTests|FullyQualifiedName~CompositePinApplicationServiceTests"`
- Tests pass: `dotnet test Tests/InteractiveWorldMap.Tests.csproj --settings .runsettings --filter "Category!=Performance"`
- Existing test files using concrete class still pass

---

### 2. AnimationFrameCache Testability (~1 hour)

**File:** `Services/AnimationFrameCache.cs` (194 lines)

**Current Issue:**
- Constructor hardcodes `Environment.SpecialFolder.ApplicationData` path
- Cannot inject temporary directory for tests
- Prevents testing cache versioning, file I/O, and bitmap operations

**Required Change:**
Inject cache directory path via constructor with default to AppData for backward compatibility.

**Implementation Steps:**

1. Add optional constructor parameter to `AnimationFrameCache`:
   ```csharp
   public AnimationFrameCache(ILogger logger, string? cacheDirectory = null)
   {
       _logger = logger ?? throw new ArgumentNullException(nameof(logger));

       // Use provided directory or default to AppData
       _cacheDirectory = cacheDirectory ?? Path.Combine(
           Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
           "InteractiveWorldMap",
           "frame_cache");

       Directory.CreateDirectory(_cacheDirectory);
       ValidateCacheVersion();
   }
   ```

2. Update all call sites:
   - Search for `new AnimationFrameCache(`
   - All existing calls use single-parameter constructor (no change needed)
   - Optional parameter means no breaking changes

3. Update test fixture (to be created after refactor):
   ```csharp
   private sealed class TempCacheDirectory : IDisposable
   {
       public string Path { get; } = System.IO.Path.Combine(
           System.IO.Path.GetTempPath(),
           "AnimationFrameCache_" + Guid.NewGuid().ToString("N"));

       public void Dispose()
       {
           if (Directory.Exists(Path))
               Directory.Delete(Path, recursive: true);
       }
   }
   ```

4. Add `Tests/AnimationFrameCacheTests.cs` with temp-directory cleanup:
   - `Constructor_CreatesDirectory`
   - `Constructor_WritesVersionFile`
   - `TryLoadFrame_WhenNotCached_ReturnsNull`
   - `SaveFrame_ThenLoad_ReturnsFrame`
   - `ClearCache_RemovesDirectoryContentsAndRecreatesDirectory`
   - `ValidateCacheVersion_WhenMismatch_ClearsPngFilesAndPreservesNonPngFiles`
   - `ValidateCacheVersion_WhenMissing_ClearsExistingPngFilesAndWritesVersion`
   - `TryLoadFrame_WithCorruptedFile_ReturnsNull`
   - `SaveFrame_WithInvalidPath_DoesNotThrow`

5. Test data details:
   - Create bitmaps with `BitmapSource.Create(...)` and freeze them before saving when needed
   - Seed version tests by writing `cache_version.txt` before constructing `AnimationFrameCache`
   - Seed old cache files with at least one `.png` file and one non-`.png` file so cleanup behavior is explicit
   - Avoid assertions against the private cache-key method; verify key behavior through save/load with same and different animation parameters

**Impact Analysis:**
- **Risk:** Low - optional parameter maintains backward compatibility
- **Breaking changes:** None - default parameter preserves existing behavior
- **Backward compatibility:** Full - all existing code continues to work
- **Performance:** No impact
- **Test isolation:** Required - every test-created temp directory must be deleted in `finally` or `IDisposable.Dispose`

**Verification:**
- Build succeeds: `dotnet build InteractiveWorldMap.sln`
- Focused tests pass: `dotnet test Tests/InteractiveWorldMap.Tests.csproj --filter "FullyQualifiedName~AnimationFrameCacheTests"`
- Tests pass: `dotnet test Tests/InteractiveWorldMap.Tests.csproj --settings .runsettings --filter "Category!=Performance"`
- App behavior unchanged (still uses AppData by default)

---

## Test Strategy

### Existing baseline tests to run before changes

Run these before touching production code so any pre-existing failures are known:

```powershell
dotnet test Tests/InteractiveWorldMap.Tests.csproj --filter "FullyQualifiedName~CompositePinPlanningServiceTests|FullyQualifiedName~CompositePinPlanCacheTests|FullyQualifiedName~ZoomedRegionCacheTests|FullyQualifiedName~MapImageRenderingPolicyTests"
```

Why these help:
- `CompositePinPlanningServiceTests` protects existing render-plan selection and the last-result cache producer.
- `CompositePinPlanCacheTests` already covers `CompositePinApplicationService.BuildApplyInstructions` call paths and disk cache behavior.
- `ZoomedRegionCacheTests` covers comparable bitmap cache file I/O helpers that the new `AnimationFrameCacheTests` can mirror.
- `MapImageRenderingPolicyTests` guards animation-frame cache version policy expectations.

### New tests to add first

Follow TDD for each production change:

1. Add `ManualLayoutAssignmentEnricherTests` using `ICompositePinPlanningResultProvider`. The initial run should fail to compile because the interface/signature does not exist yet.
2. Add `CompositePinApplicationServiceTests` for `SaveIfMissed` using a fake result provider. The initial run should fail to compile until `CompositePinApplicationService` accepts the narrow interface.
3. Add `AnimationFrameCacheTests` with the optional cache-directory constructor. The initial run should fail to compile until the constructor overload exists.

Focused red/green command:

```powershell
dotnet test Tests/InteractiveWorldMap.Tests.csproj --filter "FullyQualifiedName~ManualLayoutAssignmentEnricherTests|FullyQualifiedName~CompositePinApplicationServiceTests|FullyQualifiedName~AnimationFrameCacheTests"
```

### Final after-change verification

Run the focused baseline and new-test commands again, then run:

```powershell
dotnet test Tests/InteractiveWorldMap.Tests.csproj --settings .runsettings --filter "Category!=Performance"
.\scripts\verify.ps1
```

Only run the WPF app manually if the implementation changes runtime wiring beyond the planned constructor/interface type updates.

## Execution Order

1. **ManualLayoutAssignmentEnricher refactor** (30 min)
   - Create narrow result-provider interface
   - Update CompositePinPlanningService
   - Update ManualLayoutAssignmentEnricher
   - Update CompositePinApplicationService to depend on the same narrow interface
   - Add focused tests with hand-written fakes
   - Verify focused tests

2. **AnimationFrameCache refactor** (1 hour)
   - Add optional constructor parameter
   - Verify no call site changes needed
   - Add temp-directory and bitmap round-trip tests
   - Verify focused tests

3. **Resume test coverage implementation**
   - Return to [test-coverage-5pct-increase-plan.md](./test-coverage-5pct-increase-plan.md)
   - Mark ManualLayoutAssignmentEnricher and AnimationFrameCache skipped sections complete or narrowed to remaining coverage work
   - Update coverage numbers after `.\scripts\verify.ps1`

## Post-Refactoring Checklist

- [ ] Build succeeds: `dotnet build InteractiveWorldMap.sln`
- [ ] Focused service tests pass:
  `dotnet test Tests/InteractiveWorldMap.Tests.csproj --filter "FullyQualifiedName~ManualLayoutAssignmentEnricherTests|FullyQualifiedName~CompositePinApplicationServiceTests|FullyQualifiedName~AnimationFrameCacheTests"`
- [ ] All existing non-performance tests pass:
  `dotnet test Tests/InteractiveWorldMap.Tests.csproj --settings .runsettings --filter "Category!=Performance"`
- [ ] App launches and runs correctly if runtime behavior is touched: `dotnet run --project InteractiveWorldMap.csproj`
- [ ] Full verification passes: `.\scripts\verify.ps1`
- [ ] Update test coverage plan to mark refactoring as complete
- [ ] Update `docs/exec-plans/active/README.md` if this plan remains active; archive to `docs/exec-plans/completed/` when complete
- [ ] Update `docs/TO_DO.md`: remove or narrow the linked coverage/refactoring bullet
- [ ] Add or update the `[Unreleased]` `CHANGELOG.md` entry for testability/workflow-visible changes
- [ ] Resume test coverage work

## Risks and Mitigations

| Risk | Likelihood | Impact | Mitigation |
|------|-----------|--------|------------|
| Breaking existing code | Very Low | Medium | Use optional parameters and interface inheritance to maintain backward compatibility |
| Performance regression | Very Low | Low | No algorithmic changes, only dependency injection |
| Cache directory conflicts | Low | Low | AnimationFrameCache uses GUID-based temp directories in tests |
| Interface grows too broad | Low | Medium | Keep interface limited to `TryGetLastResult`; create a separate abstraction only if future tests need `BuildPlan` |
| Mocking dependency churn | Low | Low | Use hand-written fakes consistent with current test patterns instead of adding a mocking library |

## Notes

- Both refactors follow dependency injection patterns already used in the codebase
- Changes are minimal and focused on testability only
- No business logic changes
- All changes maintain existing behavior
- Refactoring enables the test coverage plan to proceed without further source code modifications
- `AnimationFrameCache` default construction must continue to use `%AppData%\InteractiveWorldMap\frame_cache`
- Do not assert exact cache-key strings in tests; assert observable save/load behavior

## Related Documentation

- [Test Coverage Plan](./test-coverage-5pct-increase-plan.md) - Phase 1 items 1.3 and 1.4
- [Architecture Rules](../../../ARCHITECTURE.md) - Layer dependency guidelines
- [AGENTS.md](../../../AGENTS.md) - Implementation workflow
