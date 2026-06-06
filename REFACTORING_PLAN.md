# Refactoring Plan

**Date:** 2026-06-05
**Goal:** Reduce `MainWindow.xaml.cs` from 3,458 lines to ~1,200–1,400 lines; improve testability and maintainability across the codebase.

**Approach:** Incremental phases, each ending with a green test suite. No behavior changes — pure structural refactoring.

**Testing conventions:** XUnit 2.4.1, `Method_Condition_Expected` naming, `MockLogger` for ILogger, direct service instantiation, AAA pattern, temp directories for I/O tests.

**Plan tracking:** Before implementation starts, either move this file under `docs/exec-plans/active/` or add a link from that folder. Keep the active plan checklist and `CHANGELOG.md` synchronized as each phase lands.

**Verification gate:** Each phase may use focused build/test runs while iterating, but a phase is not complete until the repo verification script passes (`.\scripts\verify.ps1` on Windows; `./scripts/verify.sh` on macOS/Linux where WPF limitations apply). Windows `verify.ps1` remains the merge-quality gate because it includes build, tests, doc links, taste checks, and headless startup validation.

---

## Phase 1 — Extract Geometry Utilities

**Risk:** Low
**Lines moved out of MainWindow:** ~150
**New files:** `Utilities/GeometryMath.cs`

Extract 6 pure geometry functions from `MainWindow.xaml.cs` into a static utility class. These have zero UI dependencies — they operate on `Point`, `double`, and `RadialExtension` values only.

These are marker-layout geometry helpers, not generic computational geometry primitives. Preserve the current behavior intentionally:

- line intersections ignore endpoint touches via `IntersectionEndpointMargin`
- marker proximity uses the current `markerRadius + 2.0` buffer
- threshold equality behavior stays unchanged

### Methods to extract

| Method | Current Location | Signature |
|---|---|---|
| `DoLineSegmentsIntersect` | MainWindow ~L2790 | `(Point, Point, Point, Point) → bool` |
| `DoesLinePassTooCloseToMarker` | MainWindow ~L2811 | `(Point, Point, Point, double) → bool` |
| `CalculateMinimumDistanceBetweenLines` | MainWindow ~L2840 | `(Point, Point, Point, Point) → double` |
| `PointToLineSegmentDistance` | MainWindow ~L2855 | `(Point, Point, Point) → double` |
| `CalculateAngularSpace` | MainWindow ~L2690 | `(RadialExtension, List<RadialExtension>) → double` |
| `FindSafeAngleRotation` | MainWindow ~L2715 | `(RadialExtension, List<RadialExtension>, ...) → double` |

Also extract hardcoded constants:

```csharp
public const double GeometryEpsilon = 0.0001;
public const double IntersectionEndpointMargin = 0.01;
```

### Checklist

- [ ] Create `Utilities/GeometryMath.cs` with the 6 static methods and constants
- [ ] Update all call sites in `MainWindow.xaml.cs` to use `GeometryMath.*`
- [ ] Update all call sites in any other files that use these methods
- [ ] Remove the methods from `MainWindow.xaml.cs`
- [ ] Verify build succeeds
- [ ] Write tests (see below)
- [ ] Run full test suite — all green
- [ ] Run `.\scripts\verify.ps1` before marking the phase complete
- [ ] Update active exec-plan checklist and `CHANGELOG.md`
- [ ] Commit

### Tests — `Tests/GeometryMathTests.cs`

```
Namespace: InteractiveWorldMap.Tests

DoLineSegmentsIntersect_CrossingLines_ReturnsTrue
DoLineSegmentsIntersect_ParallelLines_ReturnsFalse
DoLineSegmentsIntersect_CollinearOverlapping_ReturnsFalse
DoLineSegmentsIntersect_SharedEndpoint_ReturnsFalse
DoLineSegmentsIntersect_TMeeting_ReturnsTrue
DoLineSegmentsIntersect_PerpendicularNonTouching_ReturnsFalse
DoLineSegmentsIntersect_EndpointWithinMargin_ReturnsFalse

PointToLineSegmentDistance_PointOnSegment_ReturnsZero
PointToLineSegmentDistance_PointProjectsOntoSegment_ReturnsPerpendicularDistance
PointToLineSegmentDistance_PointBeyondEndA_ReturnsDistanceToA
PointToLineSegmentDistance_PointBeyondEndB_ReturnsDistanceToB

DoesLinePassTooCloseToMarker_FarAway_ReturnsFalse
DoesLinePassTooCloseToMarker_WithinThreshold_ReturnsTrue
DoesLinePassTooCloseToMarker_ExactlyAtThreshold_ReturnsFalse
DoesLinePassTooCloseToMarker_InsideTwoPixelBuffer_ReturnsTrue

CalculateAngularSpace_SingleExtension_Returns360
CalculateAngularSpace_TwoOpposite_Returns180
CalculateAngularSpace_ThreeEvenly_Returns120
CalculateAngularSpace_WrapAroundCase_ReturnsCorrectGap

FindSafeAngleRotation_NoConflicts_ReturnsZero
FindSafeAngleRotation_NearbyConflict_ReturnsSmallRotation
FindSafeAngleRotation_Blocked_ReturnsMaxRotation
```

### Architecture test update

- [ ] Add `GeometryMath` to the Utilities layer in `LayerDependencyTests.cs` if needed (should already be covered by namespace convention)

---

## Phase 2 — Consolidate Duplicate Animation Code

**Risk:** Low–Medium (UI-coupled but structurally simple)
**Lines saved in MainWindow:** ~130 (duplication eliminated)
**New files:** None (internal refactor of MainWindow)

### What to do

Merge `AnimateZoomToCluster()` (~120 lines) and `AnimateZoomOut()` (~142 lines) into a shared private method:

```csharp
private async Task AnimateViewportTransition(
    ViewportState start,
    ViewportState target,
    Action? onBeforeAnimation,
    Action onAnimationComplete)
```

The two callers become thin wrappers that supply the before/after callbacks:
- `AnimateZoomToCluster`: `onAnimationComplete` → `ShowZoomedView(cluster)`
- `AnimateZoomOut`: `onBeforeAnimation` → clear extension lines, hide edit UI; `onAnimationComplete` → `ShowClusterView()`

### Checklist

- [ ] Create `AnimateViewportTransition()` method with the shared animation loop
- [ ] Refactor `AnimateZoomToCluster()` to delegate to the shared method
- [ ] Refactor `AnimateZoomOut()` to delegate to the shared method
- [ ] Remove duplicated animation loop code from both methods
- [ ] Verify build succeeds
- [ ] Manual smoke test: zoom in/out behavior unchanged
- [ ] Run full test suite — all green
- [ ] Commit

### Tests

Animation is deeply tied to WPF's `CompositionTarget.Rendering` and `Dispatcher`, so unit testing the animation loop directly is not practical. Instead:

```
Regression tests (manual or automated UI test):
- [ ] Zoom into a cluster: animation plays smoothly, markers reposition after
- [ ] Zoom out: animation plays smoothly, extension lines cleared, markers return
- [ ] Rapid zoom in/out: no crash, _isAnimating flag prevents overlap
- [ ] Resize during animation: no crash
```

---

## Phase 3 — Extract Radial Extension Adjustment Engine

**Risk:** Medium
**Lines moved out of MainWindow:** ~600
**New files:** `Services/RadialExtensionAdjuster.cs`

This is the largest single extraction. Move the iterative overlap/intersection adjustment logic out of MainWindow into a testable service. These methods operate on `RadialExtension` data objects and config values — they read/write `Angle`, `ExtendedPosition`, and line lengths but do NOT touch the Canvas or UI.

### Methods to extract

| Method | Lines | UI Dependency |
|---|---|---|
| `AdjustForMarkerOverlaps` | ~270 | None (reads/writes RadialExtension properties) |
| `IterativelyAdjustExtensions` | ~150 | None |
| `FixLineIntersections` | ~147 | None |
| `CalculateCurrentLength` | ~6 | None |

These methods will call `GeometryMath.*` (extracted in Phase 1) for intersection/distance calculations.

### Class design

```csharp
namespace InteractiveWorldMap.Services;

public class RadialExtensionAdjuster
{
    private readonly ILogger _logger;

    public RadialExtensionAdjuster(ILogger logger) { ... }

    public void AdjustExtensions(
        List<RadialExtension> allExtensions,
        double markerSize,
        RadialExtensionConfig config)
    {
        // Calls IterativelyAdjust → AdjustForMarkerOverlaps + FixLineIntersections
    }

    // Internal methods (private or internal for testing):
    // - AdjustForMarkerOverlaps(...)
    // - FixLineIntersections(...)
    // - IterativelyAdjustExtensions(...)
    // - CalculateCurrentLength(...)
}
```

### Decompose AdjustForMarkerOverlaps

While extracting, also break the 270-line method into smaller pieces:

```csharp
private bool AdjustForMarkerOverlaps(...)
{
    // Multi-pass loop stays here as orchestrator (~30 lines)
    do {
        hadAdjustments = AdjustAnglesWithinGroups(allExtensions, config, protectedLocations);
        hadAdjustments |= AdjustPositionsAcrossExtensions(allExtensions, markerSize, config);
        pass++;
    } while (hadAdjustments && pass < maxPasses);
}

private bool AdjustAnglesWithinGroups(...) { /* ~60 lines */ }
private bool AdjustPositionsAcrossExtensions(...) { /* ~80 lines */ }
```

### Checklist

- [ ] Create `Services/RadialExtensionAdjuster.cs` with the class skeleton
- [ ] Move `CalculateCurrentLength` first (simplest, no dependencies)
- [ ] Move `AdjustForMarkerOverlaps` — decompose into sub-methods during the move
- [ ] Move `FixLineIntersections`
- [ ] Move `IterativelyAdjustExtensions` as the public entry point `AdjustExtensions`
- [ ] Update calls in `MainWindow.xaml.cs` to use `_adjuster.AdjustExtensions(...)`
- [ ] Add `_adjuster` field and constructor instantiation in MainWindow
- [ ] Verify build succeeds
- [ ] Write tests (see below)
- [ ] Run full test suite — all green
- [ ] Manual smoke test: extension lines still render correctly, overlaps resolved
- [ ] Commit

### Tests — `Tests/RadialExtensionAdjusterTests.cs`

```
Namespace: InteractiveWorldMap.Tests

// Angle adjustment
AdjustExtensions_TwoExtensionsAtSameAngle_NudgesApart
AdjustExtensions_TwoExtensionsWellSeparated_NoChange
AdjustExtensions_WrapAroundAngles_HandledCorrectly (e.g., 355° and 5°)
AdjustExtensions_ProtectedLocations_NotAdjusted

// Position overlap
AdjustExtensions_OverlappingPositions_AdjustsLengths
AdjustExtensions_NonOverlapping_NoLengthChange
AdjustExtensions_MinimumLineLength_Respected

// Line intersections
AdjustExtensions_CrossingLines_RotatesOneToResolve
AdjustExtensions_NoCrossings_NoChange
AdjustExtensions_LineTooCloseToMarker_Adjusts

// Convergence
AdjustExtensions_OscillatingPair_ReducesNudge
AdjustExtensions_MaxPassesReached_Stops

// Integration
AdjustExtensions_ComplexCluster_ProducesNonOverlappingResult
AdjustExtensions_EmptyList_NoError
AdjustExtensions_SingleExtension_NoChange
```

---

## Phase 4 — Decompose CompositePinRenderPlanBuilder.BuildPlan

**Risk:** Low
**Lines affected:** ~167 (internal restructure of `CompositePinRenderPlanBuilder.cs`)
**New files:** None

### What to do

Break the 167-line `BuildPlan` method into 5 focused private methods:

```csharp
public CompositePinRenderPlan BuildPlan(...)
{
    ValidateInputs(pinPartGeometry, markerSize, extensionAngle);
    var geometry = PrepareGeometry(pinPartGeometry, markerSize, extensionAngle);
    var transforms = CalculateTransforms(geometry);
    var (bounds, shiftedTransforms) = CalculateBoundsAndShift(transforms, geometry);
    return AssembleResult(shiftedTransforms, geometry, bounds);
}
```

Also extract the repeated matrix create-then-translate pattern:

```csharp
private static Matrix CreateTransform(double rotation, double scaleX, double scaleY, Point origin, Vector translation)
```

### Checklist

- [ ] Extract `ValidateInputs` (~18 lines)
- [ ] Extract `PrepareGeometry` into a private method returning an intermediate record
- [ ] Extract `CalculateTransforms` — deduplicate the 4x matrix pattern
- [ ] Extract `CalculateBoundsAndShift`
- [ ] Extract `AssembleResult`
- [ ] Verify build succeeds
- [ ] Run existing `CompositePinRenderPlanBuilderTests` — all green
- [ ] Add edge case tests (see below)
- [ ] Commit

### Tests — additions to `Tests/CompositePinRenderPlanBuilderTests.cs`

```
BuildPlan_ZeroMarkerSize_ThrowsArgumentException
BuildPlan_NegativeAngle_HandledCorrectly
BuildPlan_360DegreeAngle_SameAs0Degree
BuildPlan_VerySmallMarkerSize_ProducesValidPlan
BuildPlan_VeryLargeMarkerSize_ProducesValidPlan
BuildPlan_AllTransformsProduceNonNegativeBounds
```

---

## Phase 5 — Extract Extension Rendering from MainWindow

**Risk:** Medium (UI-coupled — requires interface abstraction)
**Lines moved out of MainWindow:** ~200
**New files:** `Views/IExtensionLineRenderer.cs`, `Views/ExtensionLineRenderer.cs`

### What to do

Extract the UI rendering portion of the extension system. These methods touch `Canvas`, `Line`, animation types, and marker views, so the implementation belongs in `Views/` (or remains in `MainWindow`) rather than `Services/`. If additional pure planning data is needed, create DTOs in `Models/` and keep WPF element creation in `Views/`.

### Methods to extract

| Method | Lines | Notes |
|---|---|---|
| `ApplyRadialExtensions` | ~104 | Renders extensions, creates lines, wires events |
| `CreateExtensionLine` | ~34 | Creates a `Line` visual |
| `CreatePinExtensionLine` | ~40 | Creates a pin-styled `Line` visual |
| `AnimateExtensionLines` | ~40 | Animates line growth |
| `ClearExtensionLines` | ~8 | Removes lines from canvas |

### Interface design

```csharp
namespace InteractiveWorldMap.Views;

public interface IExtensionLineRenderer
{
    void ClearLines();
    void RenderExtensions(
        List<RadialExtension> extensions,
        ViewportState viewport,
        double containerWidth,
        double containerHeight,
        Dictionary<Location, LocationMarker> markerLookup);
    void AnimateLines();
}
```

### Checklist

- [ ] Create `Views/IExtensionLineRenderer.cs` interface
- [ ] Create `Views/ExtensionLineRenderer.cs` implementing the interface
- [ ] Move `CreateExtensionLine` and `CreatePinExtensionLine` — merge shared logic (~80% identical)
- [ ] Move `ClearExtensionLines`
- [ ] Move `AnimateExtensionLines`
- [ ] Move `ApplyRadialExtensions` (main orchestrator)
- [ ] Inject `IExtensionLineRenderer` into MainWindow
- [ ] Update `UpdateMarkerPositions()` to delegate to the renderer
- [ ] Keep Services free of `Canvas`, `Line`, `Storyboard`, and `InteractiveWorldMap.Views` dependencies
- [ ] Preserve composite-pin fallback behavior when `PinParts.Enabled` / `PinParts.UseCompositeRendering` is off or assets are missing
- [ ] Preserve edit-mode legacy draggable rendering: entering edit mode rebuilds composite pins onto the legacy path, exiting edit mode refreshes the active non-edit rendering path
- [ ] Verify build succeeds
- [ ] Run full test suite — all green
- [ ] Run `.\scripts\verify.ps1` before marking the phase complete
- [ ] Update active exec-plan checklist and `CHANGELOG.md`
- [ ] Manual smoke test: extension lines render, animate, highlight on hover
- [ ] Commit

### Tests

The renderer is UI-coupled, so direct unit testing is limited. Focus on:

```
Tests/Architecture/LayerDependencyTests.cs:
- [ ] Verify no Services file references `InteractiveWorldMap.Views` or creates WPF rendering elements
- [ ] Verify Views still do not construct content paths or parse dynamic config

Manual regression:
- [ ] Extension lines appear when zoomed into dense cluster
- [ ] Lines animate from center outward
- [ ] Hovering a marker highlights its extension line
- [ ] Lines clear on zoom out
- [ ] Composite pin markers render correctly where applicable
- [ ] Composite pin assets missing or disabled: legacy extension rendering still appears
- [ ] Edit mode: markers remain draggable and save/delete layout still works
```

---

## Phase 6 — Deduplicate ContentLoader

**Risk:** Low
**Lines saved:** ~60
**New files:** None

### What to do

1. **Merge duplicate image loading methods:** Combine `LoadAllLocationImagesAsync` and `LoadAllLocationImagesWithTranslationsAsync` into one method with an `includeTranslations` parameter.

2. **Extract bitmap init pattern:** The `BeginInit/UriSource/CacheOption/EndInit/Freeze` sequence appears 4+ times. Extract to:

```csharp
private static BitmapImage LoadFrozenBitmap(string path)
```

3. **Extract image file discovery:** The file enumeration pattern (`Directory.GetFiles` with image extensions) appears 3 times. Extract to:

```csharp
private static string[] FindImageFiles(string folderPath)
```

### Checklist

- [ ] Extract `LoadFrozenBitmap` helper method
- [ ] Extract `FindImageFiles` helper method
- [ ] Merge `LoadAllLocationImagesAsync` and `LoadAllLocationImagesWithTranslationsAsync`
- [ ] Update all call sites to use the merged method
- [ ] Replace all inline bitmap init code with `LoadFrozenBitmap`
- [ ] Verify build succeeds
- [ ] Run existing `ContentLoaderTests` — all green
- [ ] Add tests (see below)
- [ ] Commit

### Tests — additions to `Tests/ContentLoaderTests.cs`

```
LoadAllLocationImagesAsync_WithTranslations_LoadsTranslationFiles
LoadAllLocationImagesAsync_WithoutTranslations_SkipsTranslationFiles
LoadAllLocationImagesAsync_EmptyFolder_ReturnsEmptyArray
LoadAllLocationImagesAsync_MixedFileTypes_LoadsOnlyImages
```

---

## Phase 7 — Fix RadialExtensionCalculator Duplication

**Risk:** Low
**Lines saved:** ~80
**New files:** None

### What to do

1. **Extract wrap-around logic:** Both `NudgeAnglesApart` (114 lines) and `PreventConvergingLines` (163 lines) implement normal-angle and wrap-around logic as separate near-identical blocks. Extract a shared method:

```csharp
private List<(int indexA, int indexB, double gap)> FindAngularPairsWithinThreshold(
    List<(Location loc, Point pos, double angle)> sorted,
    double threshold)
```

This method handles both the normal range and the wrap-around (last→first) case in one place.

2. **Replace unnamed tuples:** Replace `(Location, Point, double)` with a named record:

```csharp
public record LocationAngleInfo(Location Location, Point Position, double Angle);
```

3. **Fix inconsistent logging:** Replace `Console.WriteLine` in `PreventLineIntersections` with `ILogger`. Current `ILogger` has `LogInfo`, `LogWarning`, and `LogError` only; do not call `LogDebug` unless the interface, `FileLogger`, and `MockLogger` are extended first.

4. **Remove dead code:** Delete commented-out validation logic in `ValidateNoCrossings`.

5. **Remove duplicate line-intersection logic:** `RadialExtensionCalculator` has its own `DoLinesIntersect`. Replace it with `GeometryMath` only after preserving the calculator's current endpoint behavior in tests. If the Phase 1 `IntersectionEndpointMargin` semantics differ, add a separate `GeometryMath` helper or parameter rather than changing behavior silently.

### Checklist

- [ ] Create `LocationAngleInfo` record (in Models or inline in the calculator)
- [ ] Replace all `(Location, Point, double)` tuples with `LocationAngleInfo`
- [ ] Extract `FindAngularPairsWithinThreshold` shared method
- [ ] Refactor `NudgeAnglesApart` to use the shared method
- [ ] Refactor `PreventConvergingLines` to use the shared method
- [ ] Replace `Console.WriteLine` with `_logger.LogInfo` / `_logger.LogWarning`, or first add `LogDebug` to `ILogger`, `FileLogger`, and `MockLogger`
- [ ] Replace private line-intersection duplication with a tested `GeometryMath` helper while preserving current endpoint semantics
- [ ] Remove commented-out code in `ValidateNoCrossings`
- [ ] Verify build succeeds
- [ ] Run full test suite — all green
- [ ] Run `.\scripts\verify.ps1` before marking the phase complete
- [ ] Update active exec-plan checklist and `CHANGELOG.md`
- [ ] Add tests (see below)
- [ ] Commit

### Tests — `Tests/RadialExtensionCalculatorTests.cs` (new file)

```
Namespace: InteractiveWorldMap.Tests

DetectDenseGroups_NoLocations_ReturnsEmpty
DetectDenseGroups_SingleLocation_ReturnsEmpty
DetectDenseGroups_TwoCloseLocations_ReturnsSingleGroup
DetectDenseGroups_TwoFarLocations_ReturnsEmpty
DetectDenseGroups_TransitiveChain_GroupsAll

CalculateRadialExtensions_SingleLocation_ReturnsOneExtension
CalculateRadialExtensions_TwoLocations_AnglesAreSpread
CalculateRadialExtensions_WrapAroundAngles_Handled

FindAngularPairsWithinThreshold_NoPairsClose_ReturnsEmpty
FindAngularPairsWithinThreshold_OnePairClose_ReturnsPair
FindAngularPairsWithinThreshold_WrapAround_DetectsLastToFirst
DoLinesIntersect_CurrentEndpointSemantics_ArePreservedThroughGeometryMath
```

---

## Phase 8 — Extract Layout Editor from MainWindow

**Risk:** Medium
**Lines moved out of MainWindow:** ~250
**New files:** `Services/LayoutEditorController.cs`

### What to do

Extract the manual layout editing subsystem. This group of methods manages a distinct "edit mode" with its own state (`_isEditMode`, `_draggedMarker`, drag offsets) and UI interactions.

Preserve the current layout compatibility contract while extracting: manual variants, auto-seed variants, legacy flat layouts, compatible-key fallback, and the composite-pin-to-legacy edit-mode transition must keep working.

### Methods to extract

| Method | Lines | Notes |
|---|---|---|
| `OnEditLayoutButtonClick` | ~50 | Enter edit mode |
| `OnSaveLayoutButtonClick` | ~110 | Collect extensions, validate, save |
| `OnDeleteLayoutButtonClick` | ~34 | Delete with confirmation |
| `OnExitEditModeButtonClick` | ~9 | Exit wrapper |
| `ExitEditMode` | ~22 | Reset state |
| `ValidateLayout` | ~58 | Validate extension data |
| `ApplyManualLayout` | ~45 | Apply saved layout |
| `OnMarkerDragStart` | ~20 | Begin drag |
| `OnMarkerDragMove` | ~87 | Handle drag delta |
| `OnMarkerDragEnd` | ~18 | End drag, update extension |

### Class design

```csharp
public class LayoutEditorController
{
    private readonly ManualLayoutManager _layoutManager;
    private readonly ILogger _logger;

    public bool IsEditMode { get; private set; }

    // Events to notify MainWindow of state changes
    public event Action<string, bool>? StatusChanged;  // (message, isError)
    public event Action? EditModeEntered;
    public event Action? EditModeExited;
    public event Action? LayoutApplied;

    public void EnterEditMode(...) { }
    public void SaveLayout(...) { }
    public void DeleteLayout(...) { }
    public void ExitEditMode() { }
    public bool ValidateLayout(...) { }
    // Drag handled in MainWindow (deeply UI-coupled) but delegated where possible
}
```

### Checklist

- [ ] Create `Services/LayoutEditorController.cs`
- [ ] Move `ValidateLayout` (least coupled)
- [ ] Move save/delete logic (keeping UI feedback in MainWindow via events)
- [ ] Move `ApplyManualLayout`
- [ ] Move edit mode state management (`EnterEditMode`, `ExitEditMode`)
- [ ] Keep drag handlers in MainWindow but extract data logic where possible
- [ ] Wire up events from controller to MainWindow UI updates
- [ ] Preserve `ManualLayoutManager` variant priority and compatible-key fallback behavior
- [ ] Preserve layout keys generated from `LayoutKeyGenerator` and `RadialExtensionConfig`
- [ ] Preserve edit-mode composite-pin behavior: edit mode uses legacy draggable markers, non-edit mode can return to composite rendering
- [ ] Verify build succeeds
- [ ] Write tests (see below)
- [ ] Run full test suite — all green
- [ ] Run `.\scripts\verify.ps1` before marking the phase complete
- [ ] Update active exec-plan checklist and `CHANGELOG.md`
- [ ] Manual smoke test: full edit mode flow (enter, drag, save, delete, exit)
- [ ] Commit

### Tests — `Tests/LayoutEditorControllerTests.cs`

```
Namespace: InteractiveWorldMap.Tests

ValidateLayout_AllExtensionsValid_ReturnsTrue
ValidateLayout_ExtensionWithZeroLength_ReturnsFalse
ValidateLayout_EmptyList_ReturnsFalse
ValidateLayout_NullExtension_ReturnsFalse

EnterEditMode_SetsIsEditModeTrue
ExitEditMode_SetsIsEditModeFalse
ExitEditMode_WhenNotInEditMode_NoError

SaveLayout_ValidExtensions_CallsLayoutManager
SaveLayout_InvalidExtensions_ReturnsFalseAndDoesNotSave
DeleteLayout_ExistingLayout_CallsManagerDelete
ApplyManualLayout_LegacyFlatLayout_AppliesCompatibleMarkers
ApplyManualLayout_ManualVariantPreferredOverAutoSeed
EnterEditMode_WithCompositePins_RebuildsLegacyDraggableMarkers
```

---

## Phase 9 — Architectural Cleanup

**Risk:** Low
**New files:** `Services/IMarkerConfiguration.cs`, `Services/VisualConfigService.cs`

This phase addresses the architectural violations identified in the assessment.

### 9A — Decouple Views from MainWindow

Create an `IMarkerConfiguration` interface and inject it into Views instead of casting `Application.Current.MainWindow`.

```csharp
public interface IMarkerConfiguration
{
    double LocationMarkerSize { get; }
    double ClusterMarkerSize { get; }
}
```

Views receive this via constructor parameter or attached property.

### 9B — Move I/O out of VisualConfig model

Extract `VisualConfig.Load()`, `Save()`, `EnsureConfigExists()` into `Services/VisualConfigService.cs`. The model becomes data-only.

### 9C — Introduce service interfaces

Add interfaces for key services consumed by MainWindow:

- `IContentLoader`
- `IManualLayoutManager`

### Checklist

- [ ] Create `IMarkerConfiguration` interface
- [ ] Update `LocationMarker.xaml.cs` to accept config via constructor instead of casting MainWindow
- [ ] Update `PinMarker.xaml.cs` similarly
- [ ] Update `ClusterMarker.xaml.cs` similarly
- [ ] Update all marker creation sites in MainWindow to pass config
- [ ] Create `Services/VisualConfigService.cs` with `Load`/`Save`/`EnsureConfigExists`
- [ ] Remove I/O methods from `Models/VisualConfig.cs`
- [ ] Update callers to use `VisualConfigService`
- [ ] Create `IContentLoader` interface matching `ContentLoader` public API
- [ ] Create `IManualLayoutManager` interface
- [ ] Update MainWindow fields to use interfaces
- [ ] Verify build succeeds
- [ ] Run full test suite — all green (including `LayerDependencyTests`)
- [ ] Commit

### Tests

```
Tests/Architecture/LayerDependencyTests.cs:
- [ ] Existing tests should now pass more cleanly (Views no longer reference MainWindow for config)

Tests/Architecture/GoldenPrincipleTests.cs:
- [ ] Add: Views_DoNotCastApplicationCurrentMainWindow

Tests/VisualConfigServiceTests.cs (new):
Load_ValidJsonFile_ReturnsConfig
Load_MissingFile_CreatesDefault
Save_WritesJsonToFile
EnsureConfigExists_CreatesFileIfMissing
```

---

## Phase 10 — Quality Improvements

**Risk:** Low
**Applied across codebase**

### 10A — Fix async anti-patterns

Replace fire-and-forget `Task.Delay(...).ContinueWith(...)` in MainWindow with proper `async/await`:

```csharp
// Before (fire-and-forget, no error handling):
Task.Delay(3000).ContinueWith(_ => Dispatcher.Invoke(() => { ... }));

// After:
await UpdateStatusTemporarily("Saved!", Colors.Green, 3000);
```

### 10B — Standardize logging

- Remove `Console.WriteLine` calls from Services and Utilities, except `FileLogger.cs` console mirroring by design
- Remove `Console.WriteLine` / redundant `Debug.WriteLine` from Views and Models where the touched code already has `ILogger` or has been moved behind a service
- Remove `Debug.WriteLine` where redundant with `ILogger`
- Keep only `_logger.*` calls
- If a debug log level is desired, add `LogDebug` deliberately to `ILogger`, `FileLogger`, and `MockLogger`; do not call a method that does not exist

### 10C — Deduplicate ZoomedRegionCache scaling

Extract the bitmap scaling sequence (repeated 3x in `GenerateAndCacheRegion`) into `ScaleBitmap()`.

### 10D — Merge identical ContentSubwindow sizing methods

`CalculateSizeForImage` and `CalculateSizeForText` are identical — consolidate into `CalculateContentSize()`.

### 10E — Replace state flags with enum

```csharp
private enum InteractionMode { Normal, Animating, Editing }
private InteractionMode _mode = InteractionMode.Normal;
```

Replace checks like `if (_isAnimating)` and `if (_isEditMode)` with `if (_mode == InteractionMode.Animating)`.

### Checklist

- [ ] Replace `Task.Delay.ContinueWith` with async/await (MainWindow ~L3043, ~L3070)
- [ ] Remove `Console.WriteLine` from Services/ and Utilities/ except `FileLogger.cs`
- [ ] Remove touched-code `Console.WriteLine` / redundant `Debug.WriteLine` from Views/ and Models/
- [ ] If using `LogDebug`, add it to `ILogger`, `FileLogger`, and `MockLogger`
- [ ] Remove redundant `Debug.WriteLine` calls
- [ ] Extract `ScaleBitmap` in `ZoomedRegionCache.cs`
- [ ] Merge sizing methods in `ContentSubwindow.xaml.cs`
- [ ] Create `InteractionMode` enum, replace boolean flags
- [ ] Verify build succeeds
- [ ] Run full test suite — all green
- [ ] Run `.\scripts\verify.ps1` before marking the phase complete
- [ ] Update active exec-plan checklist and `CHANGELOG.md`
- [ ] Commit

### Tests

```
Tests/ZoomedRegionCacheTests.cs (additions):
GenerateAndCacheRegion_FullResAvailable_UsesFullRes
GenerateAndCacheRegion_FullResUnavailable_FallsToHalfRes
GenerateAndCacheRegion_CacheHit_ReturnsCachedBitmap
```

---

## Execution Order & Dependencies

```
Phase 1  ──→  Phase 3  ──→  Phase 5
(Geometry)    (Adjuster)    (Renderer)
                  ↑
Phase 2       uses Phase 1
(Animation)   geometry utils

Phase 4  (independent — can run in parallel with 1–3)
(BuildPlan)

Phase 6  (independent)
(ContentLoader)

Phase 7  (independent — can run in parallel with 6)
(RadialExtCalc)

Phase 8  (depends on Phase 3 — adjuster used by editor)
(Layout Editor)

Phase 9  (depends on Phases 1–8 complete)
(Architecture)

Phase 10 (depends on all above)
(Quality)
```

**Recommended groupings:**

| Sprint | Phases | Focus |
|---|---|---|
| Sprint 1 | 1, 2, 4 | Low-risk extractions, quick wins |
| Sprint 2 | 3, 5 | Core extension system extraction (biggest impact) |
| Sprint 3 | 6, 7 | Deduplication cleanup |
| Sprint 4 | 8 | Layout editor extraction |
| Sprint 5 | 9, 10 | Architecture and quality |

---

## Expected Results

| Metric | Before | After All Phases |
|---|---|---|
| MainWindow.xaml.cs lines | 3,458 | ~1,200–1,400 |
| Largest method (lines) | 270 | <80 |
| New test files | 0 | 5 |
| New test cases | 0 | ~55 |
| Service interfaces | 1 (ILogger) | 5+ |
| Pure-function test coverage | Low | High (geometry, adjustment logic) |

---

## Regression Test Checklist (After Each Phase)

Run after every phase to confirm no behavior changes:

- [ ] `dotnet build` — no errors or new warnings
- [ ] `dotnet test Tests/InteractiveWorldMap.Tests.csproj` — all green
- [ ] `.\scripts\verify.ps1` on Windows before completing any phase or merge-ready slice
- [ ] Active exec-plan checklist and `CHANGELOG.md` updated for the completed phase
- [ ] Launch app → map loads with markers
- [ ] Click a cluster → zoom animation plays, markers spread
- [ ] Extension lines appear for dense groups (zoom in)
- [ ] Hover marker → extension line highlights
- [ ] Click location → content subwindow opens
- [ ] Enter edit mode → drag markers → save layout
- [ ] Delete layout → markers return to defaults
- [ ] Zoom out → animation plays, markers reset
- [ ] Close and relaunch → layouts persist
