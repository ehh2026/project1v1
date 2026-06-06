---
status: active
owner: agent
started: 2026-06-05
---

# Refactoring Plan — Active Execution Tracker

Full plan: [REFACTORING_PLAN.md](../../../REFACTORING_PLAN.md)
Assessment: [REFACTORING_ASSESSMENT.md](../../../docs/REFACTORING_ASSESSMENT.md)

---

## Phase 1 — Extract Geometry Utilities ✅

- [x] Create `Utilities/GeometryMath.cs` with 6 static methods and constants
- [x] Update all call sites in `MainWindow.xaml.cs` to use `GeometryMath.*`
- [x] Update call sites in any other files
- [x] Remove the 6 methods from `MainWindow.xaml.cs`
- [x] Build succeeds
- [x] Write `Tests/GeometryMathTests.cs`
- [x] Full test suite green
- [x] `.\scripts\verify.ps1` passed
- [x] CHANGELOG.md updated
- [x] Committed

## Phase 2 — Consolidate Duplicate Animation Code ✅

- [x] Create `AnimateViewportTransition()` shared method
- [x] Refactor `AnimateZoomToCluster()` to delegate to it
- [x] Refactor `AnimateZoomOut()` to delegate to it
- [x] Remove duplicated animation loop code
- [x] Build succeeds
- [ ] Manual smoke test: zoom in/out unchanged (requires running app)
- [x] Full test suite green (100/100)
- [ ] `.\scripts\verify.ps1` passed
- [x] CHANGELOG.md updated
- [ ] Committed

## Phase 3 — Extract Radial Extension Adjustment Engine ✅

- [x] Create `Services/RadialExtensionAdjuster.cs`
- [x] Move `CalculateCurrentLength`
- [x] Move and decompose `AdjustForMarkerOverlaps`
- [x] Move `FixLineIntersections`
- [x] Move `IterativelyAdjustExtensions` as public `AdjustExtensions`
- [x] Wire adjuster into `MainWindow.xaml.cs`
- [x] Build succeeds
- [x] Write `Tests/RadialExtensionAdjusterTests.cs`
- [x] Full test suite green (113/113)
- [ ] Manual smoke test: extension lines resolve overlaps
- [x] `.\scripts\verify.ps1` passed
- [x] CHANGELOG.md updated
- [ ] Committed

## Phase 4 — Decompose CompositePinRenderPlanBuilder.BuildPlan ✅

- [x] Extract `ValidateInputs`
- [x] Extract `PrepareGeometry`
- [x] Extract `CalculateTransforms` (deduplicate matrix pattern)
- [x] Extract `CalculateBoundsAndShift`
- [x] Extract `AssembleResult`
- [x] Build succeeds
- [x] Existing `CompositePinRenderPlanBuilderTests` green
- [x] Add edge-case tests (6 new: 4 null guards, too-short target, canvas dims positive)
- [x] `.\scripts\verify.ps1` passed
- [x] CHANGELOG.md updated
- [ ] Committed

## Phase 5 — Extract Extension Rendering from MainWindow

- [ ] Create `Views/IExtensionLineRenderer.cs`
- [ ] Create `Views/ExtensionLineRenderer.cs`
- [ ] Move and merge `CreateExtensionLine` / `CreatePinExtensionLine`
- [ ] Move `ClearExtensionLines`
- [ ] Move `AnimateExtensionLines`
- [ ] Move `ApplyRadialExtensions`
- [ ] Inject renderer into MainWindow
- [ ] Build succeeds
- [ ] Manual smoke: lines render, animate, highlight on hover
- [ ] `.\scripts\verify.ps1` passed
- [ ] CHANGELOG.md updated
- [ ] Committed

## Phase 6 — Deduplicate ContentLoader

- [ ] Extract `LoadFrozenBitmap` helper
- [ ] Extract `FindImageFiles` helper
- [ ] Merge image-loading methods
- [ ] Build succeeds
- [ ] `ContentLoaderTests` green + new tests added
- [ ] `.\scripts\verify.ps1` passed
- [ ] CHANGELOG.md updated
- [ ] Committed

## Phase 7 — Fix RadialExtensionCalculator Duplication

- [ ] Create `LocationAngleInfo` record
- [ ] Extract `FindAngularPairsWithinThreshold`
- [ ] Refactor `NudgeAnglesApart` and `PreventConvergingLines`
- [ ] Fix inconsistent logging
- [ ] Remove dead code
- [ ] Build succeeds
- [ ] Write `Tests/RadialExtensionCalculatorTests.cs`
- [ ] Full test suite green
- [ ] `.\scripts\verify.ps1` passed
- [ ] CHANGELOG.md updated
- [ ] Committed

## Phase 8 — Extract Layout Editor from MainWindow

- [ ] Create `Services/LayoutEditorController.cs`
- [ ] Move `ValidateLayout`
- [ ] Move save/delete logic
- [ ] Move `ApplyManualLayout`
- [ ] Move edit mode state management
- [ ] Wire events to MainWindow UI
- [ ] Build succeeds
- [ ] Write `Tests/LayoutEditorControllerTests.cs`
- [ ] Full test suite green
- [ ] Manual smoke: full edit mode flow
- [ ] `.\scripts\verify.ps1` passed
- [ ] CHANGELOG.md updated
- [ ] Committed

## Phase 9 — Architectural Cleanup

- [ ] Create `IMarkerConfiguration` interface
- [ ] Decouple Views from MainWindow for marker sizing
- [ ] Create `VisualConfigService` (move I/O from model)
- [ ] Create `IContentLoader` and `IManualLayoutManager` interfaces
- [ ] Build succeeds
- [ ] Architecture tests green
- [ ] `.\scripts\verify.ps1` passed
- [ ] CHANGELOG.md updated
- [ ] Committed

## Phase 10 — Quality Improvements

- [ ] Fix async anti-patterns (fire-and-forget)
- [ ] Standardize logging (remove Console.WriteLine)
- [ ] Extract `ScaleBitmap` in ZoomedRegionCache
- [ ] Merge identical sizing methods in ContentSubwindow
- [ ] Create `InteractionMode` enum, replace boolean flags
- [ ] Build succeeds
- [ ] Full test suite green
- [ ] `.\scripts\verify.ps1` passed
- [ ] CHANGELOG.md updated
- [ ] Committed
