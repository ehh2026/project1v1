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

## Phase 5 — Extract Extension Rendering from MainWindow ✅

- [x] Create `Views/IExtensionLineRenderer.cs`
- [x] Create `Views/ExtensionLineRenderer.cs`
- [x] Move and merge `CreateExtensionLine` / `CreatePinExtensionLine`
- [x] Move `ClearExtensionLines`
- [x] Move `AnimateExtensionLines`
- [x] Move `ApplyRadialExtensions`
- [x] Inject renderer into MainWindow
- [x] Build succeeds
- [ ] Manual smoke: lines render, animate, highlight on hover
- [x] `.\scripts\verify.ps1` passed
- [x] CHANGELOG.md updated
- [ ] Committed

## Phase 6 — Deduplicate ContentLoader ✅

- [x] Extract `LoadFrozenBitmap` helper
- [x] Extract `FindImageFiles` helper
- [x] Merge image-loading methods
- [x] Build succeeds
- [x] `ContentLoaderTests` green + new tests added (7 new, 126 total)
- [x] `.\scripts\verify.ps1` passed
- [x] CHANGELOG.md updated
- [x] Committed (a7785ff)

## Phase 7 — Fix RadialExtensionCalculator Duplication ✅

- [x] Create `LocationAngleInfo` record (private nested record in RadialExtensionCalculator)
- [x] Extract `FindAngularPairsWithinThreshold`
- [x] Extract `SafeNudgeApart`, `AngularDiff`, `ExtendedPoint` helpers
- [x] Refactor `NudgeAnglesApart` and `PreventConvergingLines`
- [x] Fix inconsistent logging (remove Console.WriteLine, Debug.WriteLine)
- [x] Remove dead code (ValidateNoCrossings commented block)
- [x] Fix latent IndexOutOfRange bug (markerCount → items.Count)
- [x] Build succeeds
- [x] Write `Tests/RadialExtensionCalculatorTests.cs` (13 tests)
- [x] Full test suite green (139 total)
- [x] `.\scripts\verify.ps1` passed
- [x] CHANGELOG.md updated
- [x] Committed (fe7f503)

## Phase 8 — Extract Layout Editor from MainWindow

- [x] Create `Services/LayoutEditorController.cs`
- [x] Move `ValidateLayout`
- [x] Move save/delete logic
- [x] Move `ApplyManualLayout` data mapping
- [x] Move edit mode state management
- [x] Wire events to MainWindow UI
- [x] Build succeeds
- [x] Write `Tests/LayoutEditorControllerTests.cs`
- [x] Full test suite green (171 total)
- [ ] Manual smoke: full edit mode flow
- [x] `.\scripts\verify.ps1` passed
- [x] CHANGELOG.md updated
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
