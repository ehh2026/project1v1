# Service Testability Refactoring Plan

**Status:** Draft  
**Created:** 2026-08-10  
**Related Plan:** [test-coverage-5pct-increase-plan.md](./test-coverage-5pct-increase-plan.md)  
**Estimated Effort:** 1.5-2 hours  

## Purpose

This plan addresses testability limitations discovered during test coverage implementation. Two Services require minor refactoring to enable unit testing without source code modifications in test files.

## Scope

### 1. ManualLayoutAssignmentEnricher Testability (~30 min)

**File:** `Services/ManualLayoutAssignmentEnricher.cs` (33 lines)

**Current Issue:**
- `GetAssignments` method takes concrete `CompositePinPlanningService` parameter
- Cannot mock the service in tests without interface
- Prevents testing assignment extraction logic

**Required Change:**
Extract `ICompositePinPlanningService` interface and update dependency injection.

**Implementation Steps:**

1. Create `Services/ICompositePinPlanningService.cs`:
   ```csharp
   using InteractiveWorldMap.Models;

   namespace InteractiveWorldMap.Services
   {
       /// <summary>
       /// Interface for CompositePinPlanningService to enable testing.
       /// </summary>
       public interface ICompositePinPlanningService
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
   - Add `: ICompositePinPlanningService` to class declaration
   - No other changes needed (already implements the method)

3. Update `Services/ManualLayoutAssignmentEnricher.cs`:
   - Change parameter type from `CompositePinPlanningService` to `ICompositePinPlanningService`
   - No other changes needed

4. Update all call sites:
   - `MainWindow.xaml.cs` line 83: Constructor still uses concrete class (no change needed)
   - Test files can now use `Mock<ICompositePinPlanningService>`

**Impact Analysis:**
- **Risk:** Very low - pure interface extraction, no behavior changes
- **Breaking changes:** None - concrete class still implements same interface
- **Backward compatibility:** Full - all existing code continues to work
- **Performance:** No impact

**Verification:**
- Build succeeds: `dotnet build InteractiveWorldMap.sln`
- Tests pass: `dotnet test Tests/InteractiveWorldMap.Tests.csproj`
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
   private static string CreateTempCacheDir() =>
       Path.Combine(Path.GetTempPath(), "AnimationFrameCache_" + Guid.NewGuid().ToString("N"));
   ```

**Impact Analysis:**
- **Risk:** Low - optional parameter maintains backward compatibility
- **Breaking changes:** None - default parameter preserves existing behavior
- **Backward compatibility:** Full - all existing code continues to work
- **Performance:** No impact

**Verification:**
- Build succeeds: `dotnet build InteractiveWorldMap.sln`
- Tests pass: `dotnet test Tests/InteractiveWorldMap.Tests.csproj`
- App behavior unchanged (still uses AppData by default)

---

## Execution Order

1. **ManualLayoutAssignmentEnricher refactor** (30 min)
   - Create interface
   - Update CompositePinPlanningService
   - Update ManualLayoutAssignmentEnricher
   - Verify build and tests

2. **AnimationFrameCache refactor** (1 hour)
   - Add optional constructor parameter
   - Verify no call site changes needed
   - Verify build and tests

3. **Resume test coverage implementation**
   - Return to [test-coverage-5pct-increase-plan.md](./test-coverage-5pct-increase-plan.md)
   - Complete ManualLayoutAssignmentEnricherTests
   - Complete AnimationFrameCacheTests

## Post-Refactoring Checklist

- [ ] Build succeeds: `dotnet build InteractiveWorldMap.sln`
- [ ] All existing tests pass: `dotnet test Tests/InteractiveWorldMap.Tests.csproj`
- [ ] App launches and runs correctly: `dotnet run --project InteractiveWorldMap.csproj`
- [ ] Full verification passes: `.\scripts\verify.ps1`
- [ ] Update test coverage plan to mark refactoring as complete
- [ ] Resume test coverage work

## Risks and Mitigations

| Risk | Likelihood | Impact | Mitigation |
|------|-----------|--------|------------|
| Breaking existing code | Very Low | Medium | Use optional parameters and interface inheritance to maintain backward compatibility |
| Performance regression | Very Low | Low | No algorithmic changes, only dependency injection |
| Cache directory conflicts | Low | Low | AnimationFrameCache uses GUID-based temp directories in tests |

## Notes

- Both refactors follow dependency injection patterns already used in the codebase
- Changes are minimal and focused on testability only
- No business logic changes
- All changes maintain existing behavior
- Refactoring enables the test coverage plan to proceed without further source code modifications

## Related Documentation

- [Test Coverage Plan](./test-coverage-5pct-increase-plan.md) - Phase 1 items 1.3 and 1.4
- [Architecture Rules](../../ARCHITECTURE.md) - Layer dependency guidelines
- [AGENTS.md](../../AGENTS.md) - Implementation workflow
