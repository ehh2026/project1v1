# Changelog

All notable changes to this project are documented here. Format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/). Versioning follows [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

- **Security CI (Phases 1–3):** Dependabot (`.github/dependabot.yml`), Gitleaks workflow (`.github/workflows/gitleaks.yml`), NuGet vulnerability gate (`scripts/verify_nuget_vulnerabilities.py` in CI and `verify.ps1` / `verify.sh`)
- Pinned test transitive packages `System.Net.Http` 4.3.4 and `System.Text.RegularExpressions` 4.3.1 to clear High-severity advisories under the new audit gate

### Changed

- `docs/SECURITY.md` and `AGENTS.md` — document Gitleaks, Dependabot, and NuGet audit merge gate
- **Refactoring Phase 10** - Quality cleanup:
  - Replaced edit-mode status `Task.Delay(...).ContinueWith(...)` callbacks with awaited async status reset logic.
  - Removed app/UI console diagnostics outside `FileLogger`, added structural coverage to keep console output out of production code, and kept drag diagnostics behind `ILogger`.
  - Extracted `ZoomedRegionCache.ScaleBitmap`, merged `ContentSubwindow` sizing into `CalculateContentSize`, and replaced the animation boolean with `InteractionMode`.
  - Added ZoomedRegionCache fallback/cache-hit tests and architecture tests for the Phase 10 cleanup rules.
- **Refactoring Phase 9** - Clean up architecture boundaries:
  - Added `Models/IMarkerConfiguration.cs` and constructor-injected marker sizing into `LocationMarker`, `PinMarker`, and `ClusterMarker` instead of reading `Application.Current.MainWindow`.
  - Added `Services/VisualConfigService.cs` and moved visual-config load/save/ensure file I/O out of `Models/VisualConfig.cs`.
  - Added `IContentLoader` and `IManualLayoutManager`; `MainWindow` and `LayoutEditorController` now depend on service interfaces.
  - Added architecture/golden-principle tests for View/MainWindow coupling, model file I/O, service interface conformance, and visual-config service behavior.
- **Refactoring Phase 8** - Extract layout editor state/data operations from `MainWindow.xaml.cs`:
  - Added `Services/LayoutEditorController.cs` for edit-mode state, layout key/activity state, validation, save/delete/load delegation, saved-layout application mapping, and controller events for UI state changes.
  - Added `Tests/LayoutEditorControllerTests.cs` covering constructor guards, edit-mode events, layout activity events, extension building, validation, save/delete/load, and saved-layout application decisions.
  - Reduced `MainWindow.xaml.cs` manual-layout responsibilities to WPF-specific marker dragging, Canvas placement, status text, and extension-line rendering.

- **Refactoring Phase 7** — Fix `RadialExtensionCalculator` duplication in `Utilities/RadialExtensionCalculator.cs`:
  - Introduced private nested `LocationAngleInfo` record replacing the verbose `(Location, Point, double)` tuple used throughout the angle-adjustment pipeline.
  - Extracted `FindAngularPairsWithinThreshold(items, maxAngleDeg)` — enumerates forward and wrap-around angular pairs within a threshold, eliminating two near-identical double-loop structures shared by `NudgeAnglesApart` and `PreventConvergingLines`.
  - Extracted `SafeNudgeApart(items, i, j, diff, nudgeAmount)` — applies a circular-order–safe nudge, eliminating four copies of the nudge-with-crossover-prevention pattern.
  - Extracted `AngularDiff(from, to)` — canonical `(to - from + 360) % 360` one-liner.
  - Extracted `ExtendedPoint(item)` — projects a marker along its angle, replacing duplicate inline calculations in `PreventConvergingLines` and `PreventLineIntersections`.
  - Removed all `Console.WriteLine` from `PreventLineIntersections` and all `System.Diagnostics.Debug.WriteLine` from `PreventConvergingLines`.
  - Removed the large commented-out body of `ValidateNoCrossings`; method is now a documented one-liner.
  - Fixed latent bug: final extension loop iterates `items.Count` (not `group.Count`) preventing potential `IndexOutOfRangeException` when screen positions are missing.
  - Net change: ~698 → ~270 lines (~430 lines removed), 5 helpers added, 0 behaviour changes.
  - Added `Tests/RadialExtensionCalculatorTests.cs` — 13 new unit tests covering constructor guard, dense-group detection, radial extension output, and `ValidateNoCrossings`.
- **Refactoring Phase 1** — Extract geometry utilities from `MainWindow.xaml.cs`:
  - Added `Utilities/GeometryMath.cs` with 6 static marker-layout geometry helpers (`DoLineSegmentsIntersect`, `DoesLinePassTooCloseToMarker`, `CalculateMinimumDistanceBetweenLines`, `PointToLineSegmentDistance`, `CalculateAngularSpace`, `FindSafeAngleRotation`) and two named constants (`GeometryEpsilon`, `IntersectionEndpointMargin`). Behavior is identical to the original private methods.
  - Removed the 6 private geometry methods from `MainWindow.xaml.cs` (~150 lines); updated all call sites to use `GeometryMath.*`. `FindSafeAngleRotation` now takes an explicit `markerRadius` parameter instead of reading `_visualConfig`.
  - Added `Tests/GeometryMathTests.cs` — 22 new unit tests covering all six methods including wrap-around, endpoint-margin, and the 2 px buffer edge cases.
  - Added `docs/exec-plans/active/refactoring-plan.md` to track the full refactoring plan execution.
- **Refactoring Phase 2** — Consolidate duplicate animation loop in `MainWindow.xaml.cs`:
  - Extracted shared `AnimateViewportTransition(startViewport, targetViewport, animationLabel, onAnimationComplete)` (~75 lines) containing the pre-rendered keyframe loop that was duplicated between `AnimateZoomToCluster` and `AnimateZoomOut`.
  - `AnimateZoomToCluster` reduced from ~120 lines to ~45 lines; `AnimateZoomOut` reduced from ~142 lines to ~65 lines. Combined saving ~130 lines with zero duplication.
- **Refactoring Phase 6** — Deduplicate bitmap loading in `Services/ContentLoader.cs`:
  - Added private static helper `LoadFrozenBitmap(absolutePath)` — single canonical `BitmapImage { BeginInit → UriSource → CacheOption → EndInit → Freeze }` sequence, replacing 5 scattered copies.
  - Added private static helper `FindImageFiles(folder)` — single `.jpg`/`.png`/`.jpeg` glob, replacing 3 scattered copies.
  - Simplified `TryLoadContentBitmap`, `LoadMapImageAsync`, `LoadAllLocationImagesWithTranslationsAsync`, and `LoadLocationContentAsync` to delegate to the helpers.
  - Reduced `LoadAllLocationImagesAsync` from a 45-line near-duplicate of `LoadAllLocationImagesWithTranslationsAsync` to a 3-line thin delegate via `results.Select(r => r.Image).ToArray()`.
  - Added 7 new tests to `Tests/ContentLoaderTests.cs` covering null guards and empty/missing folder paths for both `LoadAllLocation*` methods and `LoadLocationContentAsync_NoImages`.
  - Net change: ~70 lines removed, 2 helpers added, 0 behaviour changes.
- **Refactoring Phase 5** — Extract Extension Line Rendering from `MainWindow.xaml.cs`:
  - Added `Views/IExtensionLineRenderer.cs` interface and `Views/ExtensionLineRenderer.cs` implementation.
  - `ExtensionLineRenderer` owns `_extensionLines` and `_markerToLineMap` state (removed from MainWindow), merges `CreateExtensionLine`/`CreatePinExtensionLine` into a private `CreateLine`, internalises `AnimateExtensionLines`, and moves `OnMarkerMouseEnter`/`OnMarkerMouseLeave` hover handlers.
  - Added `Apply(group, viewport, w, h, markers, tryCompositePinApplier)` (migrated from `ApplyRadialExtensions`), `AddLine`, `MoveLineEndpoint`, `SetLineZIndex`, `Clear`.
  - Logging injected as `Action<string>` delegates instead of `ILogger` to respect the Views→Services architecture boundary.
  - MainWindow reduced by ~280 lines; 6 call sites updated to use the renderer.
- **Refactoring Phase 4** — Decompose `CompositePinRenderPlanBuilder.BuildPlan`:
  - Introduced 4 private sealed pipeline-context records (`ValidatedInputs`, `PreparedGeometry`, `ComputedTransforms`, `ShiftedGeometry`) as intermediate data carriers between stages.
  - Reduced `BuildPlan` from a 167-line monolith to a 5-line orchestrator; logic distributed across 5 private pipeline methods (`ValidateInputs`, `PrepareGeometry`, `CalculateTransforms`, `CalculateBoundsAndShift`, `AssembleResult`).
  - Deduplicated the three shaft-layer transform calls via a local function `ShaftLayerTransform` inside `CalculateTransforms`, eliminating repeated axis-argument passing.
  - Added explicit guard clauses in `ValidateInputs` and `PrepareGeometry` (null args, zero-length axis, target too short for caps) that previously threw implicitly from NullReferenceException or division by zero.
  - Added 6 new tests to `Tests/CompositePinRenderPlanBuilderTests.cs` covering all 4 null-argument guards, too-short target, and positive canvas dimensions invariant.
  - All 16 low-level geometry helper methods preserved unchanged.
- **Refactoring Phase 3** — Extract Radial Extension Adjustment Engine from `MainWindow.xaml.cs`:
  - Added `Services/RadialExtensionAdjuster.cs` — pure data-manipulation service (`AdjustExtensions`, `AdjustForMarkerOverlaps`, `AdjustAnglesWithinGroups`, `AdjustPositionsAcrossExtensions`, `FixLineIntersections`, `CalculateCurrentLength`). No UI dependencies; injectable via constructor.
  - Removed `AdjustForMarkerOverlaps`, `CalculateCurrentLength`, `IterativelyAdjustExtensions`, and `FixLineIntersections` from `MainWindow.xaml.cs` (~600 lines removed).
  - `MainWindow.xaml.cs` now holds a `RadialExtensionAdjuster` field instantiated in the constructor; the single call site uses `_adjuster.AdjustExtensions(...)`.
  - Added `Tests/RadialExtensionAdjusterTests.cs` — 13 new unit tests covering constructor guards, trivial no-ops, angle nudging (2 and 3 extensions), position overlap separation, crossing-line angle adjustment, non-crossing lines unchanged, and idempotency.

### Fixed

- Dense-region zoom completion now uses `Images&Content/World Map 1976.jpg` as the high-quality source again, scales crop rectangles from actual image dimensions, and invalidates stale zoom-region cache entries.
- `visual-config.json` keeps the composite pin debug overlay disabled by default so the startup harness passes without developer-only UI enabled.
- Added zoom-cache regression coverage and documented the March 18 comparison in `docs/ZOOMED_REGION_CACHE_REGRESSION_ASSESSMENT.md`.

### Added

- `global.json` — pins .NET 6 SDK for local/CI alignment; documents side-by-side install with newer SDKs
- `Models/ContentFileNames.cs` — canonical content filenames (resolves TD-002)
- `Tests/Architecture/GoldenPrincipleTests.cs` — Views must not use `Images&Content` paths or `JObject`
- `scripts/doc_gardening.py` and `.github/workflows/doc-gardening.yml` — weekly doc drift checks
- `docs/agent-failures.md` — harness feedback log for repeatable agent mistakes
- `scripts/compute_pin_part_geometry.ps1` and `Images&Content/Pins_v2/parts/pin_part_geometry.json` — derive shaft endpoints and head centers for cropped pin part PNGs in both local and original pin coordinates
- `docs/exec-plans/active/pin-parts-composite-placement-plan.md` — plan the shift from single-bitmap pins to composite shaft/head placement using `Pins_v2/parts`

 - `Models/PinPartConfig.cs`, `Models/PinPartGeometry.cs`, `Models/PinPlacementTarget.cs`, and `Services/PinPartPlacementCalculator.cs` â€” add typed pin-part metadata/config and deterministic pair-selection logic
 - `Models/CompositePinRenderPlan.cs`, `Services/CompositePinRenderPlanBuilder.cs`, and `Views/CompositePinMarker.xaml*` â€” add the first isolated composite-pin render path with segmented shaft layers and rotated head placement
 - `Tests/PinPartPlacementCalculatorTests.cs`, `Tests/CompositePinRenderPlanBuilderTests.cs`, and updated `Tests/ContentLoaderTests.cs` â€” cover selection, metadata loading, and exact render-plan anchoring

### Changed

- `ManualLayout` storage and loading - support grouped layout variants with explicit origins (`AutoSeed`, `Manual`, `Imported`) so generated seeds and user-adjusted layouts stay distinct

- `docs/PIN_IMAGE_PLACEMENT_ASSESSMENT.md` and `docs/exec-plans/active/pin-parts-composite-placement-plan.md` - clarify that the automatic radial endpoint distributor and manual layout edit/save/load workflow are still present and should remain the upstream source of endpoint placement
- `MainWindow` and `ManualLayoutManager` â€” respect `ManualLayoutEditor.LayoutStoragePath` and fall back to compatible layout keys so generated manual-layout seeds can be found and applied more reliably
- `scripts/generate_manual_layout_seeds.ps1` and `Images&Content/manual-layouts.json` â€” add a rough manual-layout seed generator and save an initial seed set for multi-location clusters

- `LayerDependencyTests` — scans type references, not only `using` directives
- `ClusterMarker` — stamp image supplied by MainWindow via `ContentLoader` (no path construction in Views)
- `ContentLoader` — `GetWorldMapPath`, `ResolveContentFilePath`, `TryLoadContentBitmap`
- `StartupValidator` and `MainWindow` — use `ContentFileNames` for map and content paths
- `scripts/verify_taste.py` — Views `Images&Content` and `JObject` checks; removed `ClusterMarker` console grandfather
- `.github/workflows/ci.yml` — headless startup validation step (parity with `verify.ps1`)
- `AGENTS.md` — merge gate note and link to agent failure log
- `InteractiveWorldMap.csproj` — exclude `backups/` from compilation
- `Tests/InteractiveWorldMap.Tests.csproj` — `LangVersion` 11 for test project build
- `docs/exec-plans/active/README.md` — placeholder so doc links to active plans resolve

### Changed

- `.cursor/hooks.json` — removed per-turn `stop` verify reminder (too noisy); verification stays at task completion in `AGENTS.md`
- `scripts/verify.ps1` — fall back to `py -3` when `python` is missing or unavailable (e.g. unconfigured pyenv)
- `Tests` — C# 10-compatible JSON fixtures (no `LangVersion` 11 required on .NET 6 SDK)
- `ContentLoader` — optional `ExcelCoordinateFilePath` for isolated location-loading tests
- `ContentLoaderTests` — skip repo Excel via `ExcelCoordinateFilePath` override
- `README.md`, `docs/SETUP_GUIDE.md`, `AGENTS.md` — .NET 6 SDK install and `global.json` guidance
- `docs/TO_DO.md` — item to consider .NET 8 LTS upgrade

- `scripts/compute_pin_part_geometry.ps1` and `pin_part_geometry.json` Ã¢â‚¬â€ now emit head attach points, stub directions, shaft image sizes, and segmented-shaft heuristics for composite rendering
- `visual-config.json` and `Models/VisualConfig.cs` Ã¢â‚¬â€ add `PinParts` configuration for geometry loading and staged composite rendering
- `docs/exec-plans/active/pin-parts-composite-placement-plan.md` Ã¢â‚¬â€ now tracks the completed isolated render slice and the remaining exact-fit vs clamp integration gap
- `MainWindow.xaml.cs` Ã¢â‚¬â€ now swaps extended image pins onto the composite shaft/head renderer behind the `PinParts.UseCompositeRendering` gate and restores legacy marker visuals automatically when extensions are not active
- `MainWindow.xaml.cs` Ã¢â‚¬â€ entering edit mode now immediately rebuilds extended markers onto the legacy draggable path, and exiting edit mode refreshes back to the active non-edit rendering path
- `Views/CompositePinMarker.xaml*`, `Models/CompositePinRenderPlan.cs`, `Models/DebugConfig.cs`, `visual-config.json`, and `MainWindow.xaml.cs` Ã¢â‚¬â€ add an optional composite-pin debug overlay for validating tip, join, stretch-band, and head-center placement live in the app
- `docs/exec-plans/active/pin-parts-composite-placement-plan.md` Ã¢â‚¬â€ now clarifies manual-layout canonical endpoint data vs optional persisted composite-placement results, including recommended cache invalidation inputs and replay policy

### Impact

- Minor harness release: stricter CI and mechanical golden-principle enforcement; no user-visible UI behavior change intended.

## [0.2.0] - 2026-06-04

### Verified

- Harness checks on macOS (2026-06-04): `verify_doc_links.py` passed (33 files), `verify_taste.py` passed
- `dotnet build`/`test` require Windows (WPF `net6.0-windows`); full verification via `scripts/verify.ps1` or GitHub Actions `windows-latest` CI

### Added

- Agent harness per OpenAI harness engineering principles
- `AGENTS.md` — agent entry map with progressive disclosure pointers
- `ARCHITECTURE.md` — layer model, invariants, domain map
- `docs/index.md`, `docs/QUALITY_SCORE.md`, `docs/RELIABILITY.md`, `docs/SECURITY.md`
- `docs/exec-plans/` — active, completed, and tech-debt tracker
- `docs/design-docs/` — golden principles and design index
- `docs/agent-workflows.md` — Ralph Wiggum agent loop
- `InteractiveWorldMap.sln` and GitHub Actions CI (Windows)
- `scripts/verify.ps1` and `scripts/verify.sh` — unified verification
- `scripts/verify_taste.py` — taste invariant checks
- `scripts/query_logs.ps1` — agent-queryable log tail/filter
- `scripts/validate_startup.ps1` — headless startup validation
- `Tests/Architecture/LayerDependencyTests.cs` — structural layer enforcement
- `Tests/StartupValidatorTests.cs` and `Tests/ContentLoaderTests.cs`
- `.cursor/rules/project-harness.mdc` and `wpf-architecture.mdc`
- `.editorconfig` for deterministic formatting
- `.cursor/hooks.json` — format reminder on agent stop

### Changed

- `README.md` — updated development status to reflect MVP and harness docs
- `scripts/verify.sh` — discovers Homebrew `dotnet@6`; falls back to harness-only mode on macOS when WPF build is unavailable

### Impact

- **Minor version bump (0.2.0):** New developer/agent tooling and CI; no breaking API changes to the desktop app.

## [0.1.0] - 2025-12-01

### Added

- Initial MVP: WPF interactive world map with markers, clustering, zoom, content popups
- Excel coordinate loading, visual config, manual layout editor
- xUnit tests for coordinate utilities and clustering
- Kiro spec workflow in `.kiro/specs/interactive-world-map/`
