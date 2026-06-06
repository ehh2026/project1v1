# Refactoring Assessment

**Date:** 2026-06-05
**Scope:** All non-backup, non-imported source files
**Codebase:** InteractiveWorldMap (WPF/C#)

---

## Executive Summary

The codebase has a clear architectural vision (layered architecture, documented in ARCHITECTURE.md) but implementation has outpaced structure. The primary issue is a **3,458-line god class** in `MainWindow.xaml.cs` that handles 8+ distinct responsibilities. Secondary issues include duplicate code in several service files, missing abstractions, no dependency injection, and an empty ViewModels layer (MVVM planned but not implemented).

**Overall Health: 5/10** — Functional, well-documented intent, but structural debt is accumulating.

---

## Table of Contents

1. [Critical Files](#1-critical-files)
2. [Architecture-Level Concerns](#2-architecture-level-concerns)
3. [Prioritized Refactoring Recommendations](#3-prioritized-refactoring-recommendations)

---

## 1. Critical Files

### 1.1 MainWindow.xaml.cs — 3,458 lines (CRITICAL)

This is the single most impactful refactoring target. It is a **god class** with 52+ methods spanning 8 distinct responsibility groups:

| Responsibility Group | Approx. Lines | Largest Method (lines) |
|---|---|---|
| Radial extension system (geometry, overlap, intersection) | ~600 | `AdjustForMarkerOverlaps` (270) |
| Viewport/zoom animation | ~270 | `AnimateZoomOut` (142) |
| Marker management & positioning | ~400 | `UpdateMarkerPositions` (194) |
| Pin/marker visual creation | ~200 | `TryApplyCompositePinMarker` (54) |
| Manual layout editor (drag, save, validate) | ~250 | `OnSaveLayoutButtonClick` (110) |
| Content/subwindow management | ~180 | `ShowImageAtIndexAsync` (65) |
| Event handlers & input | ~80 | `OnKeyDown` (38) |
| Initialization & config | ~60 | `InitializeAsync` (57) |

**Key problems:**

- **`AdjustForMarkerOverlaps` (270 lines):** 6-level nesting with nested `while`, `foreach`, and `for` loops. Oscillation detection, adaptive nudging, and geometry all interleaved. Extremely difficult to reason about or test.
- **`UpdateMarkerPositions` (194 lines):** 5+ concerns in one method — viewport validation, animation state checking, extension detection, extension calculation, extension application, normal positioning fallback, and cluster positioning.
- **`AnimateZoomToCluster` (120 lines) and `AnimateZoomOut` (142 lines):** ~90% identical animation loop code duplicated between these two methods.
- **`FixLineIntersections` (147 lines) and `IterativelyAdjustExtensions` (150 lines):** Complex geometry with deep nesting.
- **10+ boolean/nullable state flags** (`_isAnimating`, `_isEditMode`, `_isManualLayoutActive`, `_draggedMarker`, `_currentZoomedCluster`, `_savedLayoutToApply`, etc.) managed as loose fields with no state machine.
- **Hardcoded magic numbers** throughout: keyframe counts (30), nudge multipliers (2.5, 3.0), max passes (5), angle thresholds (30.0°), epsilon values (0.0001), etc.
- **Async anti-patterns:** Fire-and-forget `Task.Delay(...).ContinueWith(...)` with no error handling or cancellation support.

### 1.2 RadialExtensionCalculator.cs — 697 lines (HIGH)

Calculates radial extension lines for densely packed markers.

| Method | Lines | Issue |
|---|---|---|
| `PreventConvergingLines` | 163 | Duplicate wrap-around logic mirrors normal-angle logic |
| `NudgeAnglesApart` | 114 | Same duplication pattern as above |
| `CalculateRadialExtensions` | 87 | Mixed concerns: angle math, screen-space conversion, boundary checking |
| `DetectDenseGroups` | 69 | O(n²) neighbor finding; could use spatial indexing |

- **Duplicate code:** Both `NudgeAnglesApart` and `PreventConvergingLines` implement normal-angle and wrap-around logic as separate near-identical blocks.
- **Unnamed tuples:** Uses `(Location, Point, double)` throughout instead of a named type.
- **Inconsistent logging:** `PreventLineIntersections` uses `Console.WriteLine` while the rest uses `ILogger`.
- **Dead code:** Commented-out validation logic in `ValidateNoCrossings`.

### 1.3 ContentLoader.cs — 562 lines (HIGH)

Loads map images, locations, and content with caching.

| Method | Lines | Issue |
|---|---|---|
| `LoadAllLocationImagesWithTranslationsAsync` | 78 | 90% identical to `LoadAllLocationImagesAsync` |
| `LoadLocationsAsync` | 66 | Mixed file-format handling (Excel/JSON) |
| `LoadLocationContentAsync` | 59 | Bitmap init pattern repeated 4+ times |
| `LoadAllLocationImagesAsync` | 55 | Duplicate of above method minus translations |

- **Code duplication:** `LoadAllLocationImagesAsync` and `LoadAllLocationImagesWithTranslationsAsync` share ~90% of their code. Image file discovery logic is duplicated in 3 places. The `BeginInit/UriSource/CacheOption/EndInit/Freeze` bitmap pattern appears 4+ times.
- **Too many responsibilities:** File I/O, bitmap loading, clustering, and caching all in one class.

### 1.4 ManualLayoutManager.cs — 462 lines (MEDIUM)

Manages saving, loading, and deleting manual layouts.

| Method | Lines | Issue |
|---|---|---|
| `SaveLayout` | 64 | Duplicate conditional blocks for variant handling |
| `LoadLayout` | 48 | 4-level fallback chain creates deep nesting |
| `DeleteLayout` | 42 | Handles both new and legacy deletion |
| `NormalizeCollection` | 34 | Migration side effects during load |

- **Fallback complexity:** `LoadLayout` tries 4 sequential strategies (exact key → legacy → compatible group → compatible legacy), making the flow hard to follow.
- **Magic strings:** `"manual-default"`, `"seed-default"` scattered throughout.

### 1.5 CompositePinRenderPlanBuilder.cs — 386 lines (HIGH)

Builds layered render plans for composite pins.

| Method | Lines | Issue |
|---|---|---|
| `BuildPlan` | 167 | Single monolithic method doing validation, geometry, matrices, bounds, and assembly |

- **Monolithic method:** `BuildPlan` at 167 lines handles 6 distinct phases that should be separate methods: input validation, geometry extraction, matrix transformations, bounds calculation, translation/shifting, and result construction.
- **Repeated matrix pattern:** The create-then-translate matrix sequence appears 4 times.

### 1.6 ZoomedRegionCache.cs — 248 lines (MEDIUM)

Caches high-quality zoomed region images.

| Method | Lines | Issue |
|---|---|---|
| `GenerateAndCacheRegion` | 99 | Duplicate scaling logic in full-res, half-res, and error-fallback branches |

- **Triple duplication:** The bitmap scaling/transform sequence appears in three places (full-res path, half-res fallback, error recovery).
- **No eviction:** Cache grows indefinitely except on version mismatch.

### 1.7 ContentSubwindow.xaml.cs — 258 lines (LOW)

- **Identical methods:** `CalculateSizeForImage` and `CalculateSizeForText` are completely identical — redundant code.
- **Magic numbers:** Window sizing percentages hardcoded.

---

## 2. Architecture-Level Concerns

### 2.1 No MVVM Implementation

The `ViewModels/` folder is empty. All business logic lives in code-behind (primarily `MainWindow.xaml.cs`). No `INotifyPropertyChanged`, no data binding for state, no command pattern.

### 2.2 No Dependency Injection

All services are manually instantiated in `MainWindow`'s constructor:
```
_logger = new FileLogger();
_contentLoader = new ContentLoader(_logger);
_layoutManager = new ManualLayoutManager(..., _logger);
// ... 6 more direct instantiations
```

Only one interface exists (`ILogger`). All other services are concrete classes with no abstraction, making them difficult to test or swap.

### 2.3 Views Coupled to MainWindow

Multiple Views reach up to `MainWindow` for configuration via `Application.Current.MainWindow`:
- `LocationMarker.xaml.cs` casts to `MainWindow` to read `LocationMarkerSize`
- `PinMarker.xaml.cs` does the same for pin sizing
- `ClusterMarker.xaml.cs` does the same for cluster sizing

This violates the layer separation that ARCHITECTURE.md prescribes and the architectural tests enforce.

### 2.4 Models Doing Too Much

- **`VisualConfig.cs`** (193 lines) contains `Load()`, `Save()`, and `EnsureConfigExists()` — file I/O logic that belongs in a service.
- **`ViewportState.cs`** (225 lines) contains coordinate math (`SourceToScreen()`, `ScreenToSource()`) that belongs in Utilities.

### 2.5 Inconsistent Error Handling

- Some methods log AND throw; others log and return null; others silently fail.
- Triple logging in places: `ILogger` + `Console.WriteLine` + `Debug.WriteLine`.
- No standard Result/Either pattern for error propagation.

---

## 3. Prioritized Refactoring Recommendations

### Priority 1 — High Impact, Reduce Risk (Do First)

#### R1: Extract geometry utilities from MainWindow

**What:** Move the 6 pure geometry methods out of `MainWindow.xaml.cs` into a `GeometryUtilities` static class (or extend the existing utilities layer):
- `DoLineSegmentsIntersect()`
- `DoesLinePassTooCloseToMarker()`
- `CalculateMinimumDistanceBetweenLines()`
- `PointToLineSegmentDistance()`
- `CalculateAngularSpace()`
- `FindSafeAngleRotation()`

Also extract hardcoded constants (epsilon values, angle thresholds) into a constants class.

**Why:** ~150 lines removed from MainWindow. These are pure functions with no UI dependency — immediately testable. Low risk since they have no side effects.

**Files affected:** `MainWindow.xaml.cs` → new `Utilities/GeometryUtilities.cs`

---

#### R2: Consolidate duplicate animation code

**What:** Extract the shared animation loop from `AnimateZoomToCluster()` and `AnimateZoomOut()` into a single `PerformViewportAnimation()` method, parameterized by start/target viewport and a completion callback.

**Why:** ~130 lines of duplicate code eliminated. The two methods are ~90% identical — a maintenance hazard where a fix in one must be mirrored in the other.

**Files affected:** `MainWindow.xaml.cs`

---

#### R3: Break apart `AdjustForMarkerOverlaps` (270 lines)

**What:** Decompose into focused methods:
- `AdjustAnglesWithinGroups()` — the inner angle-pair adjustment loop
- `AdjustPositionsAcrossGroups()` — the position overlap checking loop
- `DetectAndHandleOscillation()` — the oscillation tracking/dampening logic

**Why:** This is the most complex method in the codebase (6 levels of nesting, 270 lines). Breaking it apart makes each piece independently understandable and testable.

**Files affected:** `MainWindow.xaml.cs`

---

#### R4: Break apart `BuildPlan` in CompositePinRenderPlanBuilder (167 lines)

**What:** Split into phases:
1. `ValidateInputs()` — input validation
2. `PrepareGeometry()` — geometry extraction and stretch calculation
3. `CalculateTransforms()` — matrix transformations (deduplicate the 4x repeated pattern)
4. `CalculateBoundsAndShift()` — bounds union and translation
5. `AssembleResult()` — final result construction

**Why:** Single largest method outside MainWindow. Each phase is a distinct concern. The repeated matrix pattern is a clear DRY violation.

**Files affected:** `Services/CompositePinRenderPlanBuilder.cs`

---

### Priority 2 — Structural Improvements

#### R5: Extract radial extension system from MainWindow

**What:** Move the entire radial extension responsibility group (~600 lines) into a dedicated service class `RadialExtensionLayoutEngine`:
- `ApplyRadialExtensions()`
- `AdjustForMarkerOverlaps()` (already decomposed per R3)
- `IterativelyAdjustExtensions()`
- `FixLineIntersections()`
- Extension line creation and animation

**Why:** This is the single largest responsibility group in MainWindow. It's a self-contained subsystem with clear inputs (marker positions, config) and outputs (adjusted positions, extension lines). Extracting it removes ~600 lines and creates a testable unit.

**Files affected:** `MainWindow.xaml.cs` → new `Services/RadialExtensionLayoutEngine.cs`

---

#### R6: Extract animation controller from MainWindow

**What:** Create `ViewportAnimationController` containing:
- The consolidated animation method (from R2)
- `PreRenderKeyframes()`
- Frame cache interaction
- Animation state management (`_isAnimating`)

**Why:** ~270 lines removed from MainWindow. Animation is a self-contained concern with clear boundaries.

**Files affected:** `MainWindow.xaml.cs` → new `Services/ViewportAnimationController.cs`

---

#### R7: Deduplicate ContentLoader image loading

**What:** Merge `LoadAllLocationImagesAsync` and `LoadAllLocationImagesWithTranslationsAsync` into a single method with an optional `includeTranslations` parameter. Extract the repeated bitmap initialization pattern into a `LoadBitmapFromPath(string path)` helper. Extract image file discovery into a shared `FindImageFiles(string folderPath)` method.

**Why:** Eliminates ~90% code duplication between two methods and removes 4 copies of the bitmap init pattern.

**Files affected:** `Services/ContentLoader.cs`

---

#### R8: Fix wrap-around duplication in RadialExtensionCalculator

**What:** Extract the angle-wrapping logic that's duplicated in both `NudgeAnglesApart` and `PreventConvergingLines` into a shared method like `ProcessAngularPairsWithWrapAround()`. Replace the unnamed `(Location, Point, double)` tuple with a named record type.

**Why:** Two methods each contain near-identical normal-angle and wrap-around blocks. This is the largest duplication outside MainWindow.

**Files affected:** `Utilities/RadialExtensionCalculator.cs`

---

### Priority 3 — Architectural Alignment

#### R9: Decouple Views from MainWindow

**What:** Create an `IMarkerConfiguration` interface (or a simple config record) that provides marker sizes. Inject it into Views instead of having them cast `Application.Current.MainWindow`.

**Why:** This is an architectural layer violation — Views should not reference MainWindow. It also makes Views untestable in isolation.

**Files affected:** `Views/LocationMarker.xaml.cs`, `Views/PinMarker.xaml.cs`, `Views/ClusterMarker.xaml.cs`, `MainWindow.xaml.cs`

---

#### R10: Move I/O out of Models

**What:** Extract `VisualConfig.Load()`, `Save()`, and `EnsureConfigExists()` into a `VisualConfigService` in the Services layer. Move `ViewportState.SourceToScreen()` / `ScreenToSource()` to the Utilities layer.

**Why:** Models should be data-only per the project's own architecture rules. These methods introduce I/O and computation dependencies into the model layer.

**Files affected:** `Models/VisualConfig.cs` → new `Services/VisualConfigService.cs`, `Models/ViewportState.cs` → `Utilities/CoordinateMapper.cs` (or similar)

---

#### R11: Introduce service interfaces

**What:** Add interfaces for the key services: `IContentLoader`, `IManualLayoutManager`, `IZoomedRegionCache`, `IAnimationFrameCache`. Update MainWindow to depend on interfaces.

**Why:** Prerequisite for dependency injection and testability. Currently only `ILogger` exists as an abstraction.

**Files affected:** New interface files in `Services/`, `MainWindow.xaml.cs` field types

---

#### R12: Extract layout editor from MainWindow

**What:** Create `LayoutEditorController` containing:
- Edit mode state management
- Save/delete/validate operations
- Drag start/move/end handlers

**Why:** ~250 lines removed from MainWindow. The layout editor is a distinct mode with its own state and UI interactions.

**Files affected:** `MainWindow.xaml.cs` → new `Services/LayoutEditorController.cs`

---

### Priority 4 — Quality Improvements (Do Opportunistically)

#### R13: Replace state flags with a state machine

Replace the loose boolean flags (`_isAnimating`, `_isEditMode`, `_isManualLayoutActive`) with an `InteractionMode` enum or state machine that prevents invalid state combinations.

#### R14: Fix async anti-patterns

Replace `Task.Delay(...).ContinueWith(...)` fire-and-forget patterns with proper `async/await` with `CancellationToken` support.

#### R15: Standardize logging

Remove `Console.WriteLine` and `Debug.WriteLine` calls. Use `ILogger` consistently. Reduce verbose debug logging (some methods log every 5 lines).

#### R16: Deduplicate ZoomedRegionCache scaling

Extract the bitmap scaling sequence (repeated 3x in `GenerateAndCacheRegion`) into a shared `ScaleAndInterpolate()` method.

#### R17: Merge identical ContentSubwindow sizing methods

`CalculateSizeForImage` and `CalculateSizeForText` are identical — consolidate into one `CalculateContentSize()` method.

---

## Summary of Expected Impact

| Refactoring | Lines Reduced from MainWindow | Testability Gain | Risk |
|---|---|---|---|
| R1: Extract geometry utils | ~150 | High (pure functions) | Low |
| R2: Consolidate animation | ~130 | Medium | Low |
| R3: Decompose overlap method | 0 (restructure) | High | Medium |
| R4: Decompose BuildPlan | N/A (other file) | High | Low |
| R5: Extract extension system | ~600 | High | Medium |
| R6: Extract animation controller | ~270 | Medium | Medium |
| R7: Deduplicate ContentLoader | N/A (other file) | Low | Low |
| R8: Fix RadialExtCalc duplication | N/A (other file) | Medium | Low |
| R9: Decouple Views | ~20 | High (architecture) | Low |
| R10: Move I/O from Models | ~0 | Medium (architecture) | Low |
| R11: Add service interfaces | ~0 | High (testing) | Low |
| R12: Extract layout editor | ~250 | Medium | Medium |

**After R1–R6 and R12:** MainWindow.xaml.cs drops from ~3,458 lines to ~1,200–1,400 lines — a ~60% reduction.
