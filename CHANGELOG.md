# Changelog

All notable changes to this project are documented here. Format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/). Versioning follows [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Fixed

- **Composite pin anti-aliasing (Part 1A):** Composite pin image layers now use `Fant` bitmap scaling with anti-aliased edge mode, and the marker root no longer forces pixel snapping/layout rounding that fought rotated shaft transforms.
- **Composite pin pre-rasterization (Part 1B):** Added default-off `PinParts.UsePrerasterizedRendering` to flatten composite shaft/head layers inside `CompositePinMarker` while preserving debug overlays, hover/click behavior, shaft overrides, and depth sorting.
- **Composite pin depth sorting (Part 2):** Visible composite pins now sort by tip/head shaft direction so interior pins render above exterior pins during viewport updates, manual layout replay, pin reassignment, shaft overrides, and drag-end restore.
- **Gitleaks false positive:** `LayoutKeyGeneratorTests` no longer hardcodes 16-char hex layout-cache key fixtures that matched `generic-api-key`; `AreKeysCompatible` tests now build keys via `GenerateKey`.
- **Nullable warnings (CS8602):** Guard nullable XML nodes in `ExcelCoordinateReader` and nullable `Cluster` on visible cluster markers in `MainWindow.xaml.cs`.

### Changed

- **Composite shaft visibility planning:** Added [composite-pin-shaft-visibility-plan.md](docs/exec-plans/active/composite-pin-shaft-visibility-plan.md), an asset-variant-first plan for improving shaft/stub contrast without repeated runtime image processing.

- **Composite pin rendering plan completed:** Moved [pin-rendering-improvements-plan.md](docs/exec-plans/completed/pin-rendering-improvements-plan.md) to completed after anti-aliasing, gated pre-rasterization, and depth sorting; added [COMPOSITE_PIN_SHAFT_VISIBILITY_ASSESSMENT.md](docs/assessments/COMPOSITE_PIN_SHAFT_VISIBILITY_ASSESSMENT.md) for the next shaft/stub contrast pass.

- **Composite pins unzoomed rollout (Phases 1–3):** Added `CompositePinTargetBuilder` and applied screen-up stub composite pins to visible non-extended individual image markers; unzoomed cluster aggregate markers remain unchanged.

- **CI / Dependabot:** GitHub Actions bumped to `actions/checkout@v6`, `actions/setup-dotnet@v5`, and `gitleaks/gitleaks-action@v3` (Node 24–compatible); Newtonsoft.Json 13.0.3 → 13.0.4.

- **Composite pin unzoomed Phase 0:** Recorded Option A stub-segment policy — `PinPartConfig.DefaultStubLengthPixels` (default 24, screen-up) in `visual-config.json`; unzoomed individual markers in scope; unzoomed `ClusterMarker` aggregates excluded. Runtime stub rendering deferred to [composite-pins-unzoomed-plan.md](docs/exec-plans/active/composite-pins-unzoomed-plan.md) Phase 2.

- **Composite pin head placement fix (Phases 1–3):** Head-ball anchor uses `local_center`; pin_07 shaft geometry recalibrated after shadow removal. Tests pass; visual check OK. Plan parked in [docs/exec-plans/inactive/composite-pin-head-placement-fix-plan.md](docs/exec-plans/inactive/composite-pin-head-placement-fix-plan.md); optional collar/shading/`TargetHeadRadiusPx` polish tracked in [docs/TO_DO.md](docs/TO_DO.md) inactive section.

- **Composite pin core placement completed:** [pin-parts-composite-placement-plan.md](docs/exec-plans/completed/pin-parts-composite-placement-plan.md) moved to completed after Phase 6. Added common-angle endpoint drift coverage for 0, 45, 90, 135, 180, 225, 270, and 315 degrees; regenerated `Tools/PinDebugger/composites/` preview grids; `scripts/verify.ps1` passed on 2026-06-09.

- **Large-file refactoring (Phases 1–4):** Per [LARGE_FILE_REFACTORING_ASSESSMENT.md](docs/assessments/LARGE_FILE_REFACTORING_ASSESSMENT.md) — closed TD-013 and reduced TD-001; `verify.ps1` taste check green (MainWindow removed from `FILE_SIZE_GRANDFATHER`).
  - **Phase 1 — PinDebugger:** Split `Tools/PinDebugger/Program.cs` (1051 lines) into focused files (`PinDebuggerContext`, `ShaftPixelSampler`, `ShaftCleaner`, `JoinAnalysis`, `Annotator`, `CompositePreviewRenderer`, slim `Program.cs`); deduplicated LockBits pixel sampling.
  - **Phase 2 — Marker placement:** Added `Services/MarkerPlacementOrchestrator` and `Models/MarkerPlacementResult`; slimmed `UpdateMarkerPositions` in `MainWindow.xaml.cs`; gated verbose placement logging on `Debug.LogRadialExtensionCalculation`; added `Tests/MarkerPlacementOrchestratorTests.cs`.
  - **Phase 3 — Manual layout apply:** Added `Models/ManualLayoutApplyResult` / `ManualLayoutApplyInstruction`; extended `CompositePinApplicationService.BuildApplyInstructions`; `ApplyManualLayout` delegates cache/reprojection decisions to the service; extended `CompositePinPlanCacheTests`.
  - **Phase 4 — MainWindow partials:** Primary `MainWindow.xaml.cs` now ~732 lines; extracted `MainWindow.LayoutEditor.partial.cs`, `MainWindow.CompositePins.partial.cs`, `MainWindow.Navigation.partial.cs`, `MainWindow.Content.partial.cs`.

- **Large-file refactoring assessment:** [LARGE_FILE_REFACTORING_ASSESSMENT.md](docs/assessments/LARGE_FILE_REFACTORING_ASSESSMENT.md) — files ≥1000 lines (`MainWindow.xaml.cs`, `Tools/PinDebugger/Program.cs`); bloat hotspots and safe split order; TD-013 added to tech-debt tracker.

- **Manual layout variants plan completed** — moved [manual-layout-variants-plan.md](docs/exec-plans/completed/manual-layout-variants-plan.md) to completed; updated [composite-pins-program.md](docs/exec-plans/active/composite-pins-program.md) dashboard; manual smoke passed 2026-06-08.

- **Docs folder layout:** `docs/guides/` (feature how-to), `docs/assessments/` (investigations), `docs/reference/` (quality/reliability/security); `docs/` root keeps harness files only (`index.md`, `TO_DO.md`, `agent-workflows.md`, `agent-failures.md`). Removed empty stub files. Updated links across repo.
- **Documentation cleanup:** Slimmed [docs/TO_DO.md](docs/TO_DO.md) to a short human backlog; added [composite-pins-program.md](docs/exec-plans/active/composite-pins-program.md) as the composite-pin status dashboard; completed [exec-plans/active/README.md](docs/exec-plans/active/README.md) registry; archived historical plans to [docs/archive/planning/](docs/archive/planning/); refreshed [docs/index.md](docs/index.md) and [tech-debt-tracker.md](docs/exec-plans/tech-debt-tracker.md); extended `scripts/doc_gardening.py` with TO_DO size, active-plan registry, and front-matter checks; documented maintenance rules in [agent-workflows.md](docs/agent-workflows.md#documentation-maintenance) and linked from [AGENTS.md](AGENTS.md).

### Added

- **Manual Layout Variants (Phases 1–4):**
  - `Models/ManualLayoutSummary.cs` — lightweight `sealed record` for variant list display (GroupKey, VariantId, DisplayName, Origin, UpdatedUtc, IsDefault, IsSelected, MarkerCount).
  - `ManualLayoutCollection.SelectedVariants` — new per-group user selection dictionary (`groupKey → variantId`); persisted in `manual-layouts.json` and honoured by `LoadLayout`.
  - `IManualLayoutManager` — seven new variant CRUD methods: `ListVariants`, `LoadVariant`, `SaveVariant`, `DeleteVariant`, `SetDefaultVariant`, `GetSelectedVariantId`, `SetSelectedVariantId`. Flat key methods (`SaveLayout`, `LoadLayout`, `DeleteLayout`) kept as compatibility wrappers.
  - `ManualLayoutManager` — implemented all new APIs with guards: AutoSeed cannot overwrite Manual variants; last variant in a group cannot be deleted; AutoSeed deletion rejected by service; cap of 10 Manual/Imported variants per group; stale `SelectedVariants` entry is cleared on load and logged once; `SelectPreferredVariant` honours `SelectedVariants` before priority-order fallback.
  - `LayoutEditorController` — added `ActiveVariantId`, `ActiveVariantOrigin`, `ActiveVariantDisplayName` state; `VariantsChanged` event; `SwitchToVariant`, `TrySaveAsVariant`, `TryDeleteActiveVariant`, `GetVariants` methods; `TrySave` targets the active Manual variant or "manual-default"; `TryDelete` clears active-variant state.
  - `Tests/ManualLayoutVariantTests.cs` — 10 unit tests covering: list, save-as, select variant B, persist across restart, stale-id fallback, delete selected variant, reject last-variant delete, AutoSeed regen preserves Manual, assignment round-trip, cap enforcement.
  - Edit-mode UI — variant picker ComboBox (DisplayName [Origin]), `VariantStatusText`, inline Save As row (`SaveAsInputRow`), "Save As..." button, "Delete Variant" button added to `MainWindow.xaml` EditModePanel; thin handlers in `MainWindow.xaml.cs` (≤15 lines each); `CollectCurrentExtensions` helper extracts marker-position collection.
  - "Save Layout" button auto-redirects to Save As prompt when the active variant is AutoSeed.
  - `generate_manual_layout_seeds.ps1` now merges seeds into existing groups instead of overwriting: loads existing JSON at startup, preserves Manual/Imported variants and `SelectedVariants`, replaces only the `seed-default` AutoSeed variant per group.
  - `MANUAL_LAYOUT_EDITOR.md` updated with current JSON schema, variant model table, and edit-mode variant UI flows.

- **Composite Pins Phase 4 — Render-plan disk cache:**
  - `Services/CompositePinPlanCache.cs` — SHA-256–keyed disk cache under `%AppData%\InteractiveWorldMap\composite_pin_plan_cache\`. Mirrors `ClusterCache` pattern: `TryLoad`, `Save`, `Invalidate(groupKey)`, `ClearAll`. Includes custom `System.Text.Json` converters for `System.Windows.Point` and `System.Windows.Media.Matrix` (required by `CompositePinLayerPlan`).
  - `Services/CompositePinLayoutContentHasher.cs` — static helpers that produce viewport-independent hash inputs for the cache key: `ComputeLayoutContentHash` (sorted markers: name + angle + lineLength + pairId + headSourcePath), `ComputeGeometryHash` (SHA-256 of geometry JSON bytes), `ComputeConfigHash` (relevant `PinPartConfig` fields).
  - `Services/CompositePinApplicationService.cs` — orchestrates cache use for `ApplyManualLayout`: `TryCacheLoad` computes the full cache key and returns a locationName→RenderPlan dict on hit; `SaveIfMissed` collects plans from the planning service's session cache and persists them after a miss; `InvalidateGroup` clears stale entries on layout save. All cache key hashing and file I/O are delegated out of MainWindow.
  - `Models/CachedCompositePlanEntry.cs` — `sealed record(string LocationId, CompositePinRenderPlan Plan)` payload for serialisation.
  - `Tests/CompositePinPlanCacheTests.cs` — 16 tests covering miss→hit round-trip, Matrix/Point coefficient preservation, polygon preservation, invalidation, key uniqueness, and all three hasher methods (layout content, geometry file, config).
  - `ApplyManualLayout` in `MainWindow.xaml.cs` now checks the disk cache before the per-marker build loop and saves plans on miss; logs "Cache hit" at Info level.
  - `OnSaveLayoutButtonClick` calls `InvalidateGroup` so the next render after a save builds fresh plans.
  - Extracted `ApplyRenderPlanToMarker` helper shared by the normal build path and the cache-hit path.

- **Composite Pins Phase 1 — Edit-mode roundtrip fix + Reassign Pins:**
  - `ExitEditMode` now replays the saved manual layout (via `ApplyManualLayout`) instead of falling back to `UpdateMarkerPositions()` when `IsManualLayoutActive` is true, so composite pins appear at saved positions after save → exit in the same session.
  - Added **Reassign Pins** button to the edit-mode toolbar in `MainWindow.xaml`. Clicking it rebuilds composite shaft/head selection for all visible extension-line markers at their current canvas positions without saving or exiting edit mode; drag handlers remain intact.
  - Extracted `ApplyCompositePinToMarker` helper from `TryApplyCompositePinMarker` so composite rendering is reachable from Reassign (bypassing the `CanUseCompositePins()` edit-mode gate).
  - Added `IExtensionLineRenderer.TryGetLineEndpoint` (and implementation in `ExtensionLineRenderer`) to expose current line endpoints for the Reassign handler.
  - Added `LayoutEditorControllerTests`: `ExitEditMode_AfterTrySave_IsManualLayoutActiveRemainsTrue` and `TryLoad_AfterSaveAndExitEditMode_ReturnsLayout` — verify the controller invariants that the `ExitEditMode` replay branch depends on.
- **Docs:** Python venv and script catalog — [scripts/README.md](scripts/README.md); sections in [AGENTS.md](AGENTS.md), [docs/SETUP_GUIDE.md](docs/SETUP_GUIDE.md), [docs/index.md](docs/index.md)
- **Exec plans:** [manual-layout-seed-alignment-plan.md](docs/exec-plans/active/manual-layout-seed-alignment-plan.md), [manual-layout-variants-plan.md](docs/exec-plans/completed/manual-layout-variants-plan.md), [composite-pins-unzoomed-plan.md](docs/exec-plans/active/composite-pins-unzoomed-plan.md), [refactoring-assessment-followthrough-plan.md](docs/exec-plans/active/refactoring-assessment-followthrough-plan.md) — linked from [docs/TO_DO.md](docs/TO_DO.md)
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
- `docs/exec-plans/completed/pin-parts-composite-placement-plan.md` — plan the shift from single-bitmap pins to composite shaft/head placement using `Pins_v2/parts`

 - `Models/PinPartConfig.cs`, `Models/PinPartGeometry.cs`, `Models/PinPlacementTarget.cs`, and `Services/PinPartPlacementCalculator.cs` â€” add typed pin-part metadata/config and deterministic pair-selection logic
 - `Models/CompositePinRenderPlan.cs`, `Services/CompositePinRenderPlanBuilder.cs`, and `Views/CompositePinMarker.xaml*` â€” add the first isolated composite-pin render path with segmented shaft layers and rotated head placement
 - `Tests/PinPartPlacementCalculatorTests.cs`, `Tests/CompositePinRenderPlanBuilderTests.cs`, and updated `Tests/ContentLoaderTests.cs` â€” cover selection, metadata loading, and exact render-plan anchoring

### Changed

- `ManualLayout` storage and loading - support grouped layout variants with explicit origins (`AutoSeed`, `Manual`, `Imported`) so generated seeds and user-adjusted layouts stay distinct

- `docs/PIN_IMAGE_PLACEMENT_ASSESSMENT.md` and `docs/exec-plans/completed/pin-parts-composite-placement-plan.md` - clarify that the automatic radial endpoint distributor and manual layout edit/save/load workflow are still present and should remain the upstream source of endpoint placement
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
- `docs/exec-plans/completed/pin-parts-composite-placement-plan.md` Ã¢â‚¬â€ now tracks the completed isolated render slice and the remaining exact-fit vs clamp integration gap
- `MainWindow.xaml.cs` Ã¢â‚¬â€ now swaps extended image pins onto the composite shaft/head renderer behind the `PinParts.UseCompositeRendering` gate and restores legacy marker visuals automatically when extensions are not active
- `MainWindow.xaml.cs` Ã¢â‚¬â€ entering edit mode now immediately rebuilds extended markers onto the legacy draggable path, and exiting edit mode refreshes back to the active non-edit rendering path
- `Views/CompositePinMarker.xaml*`, `Models/CompositePinRenderPlan.cs`, `Models/DebugConfig.cs`, `visual-config.json`, and `MainWindow.xaml.cs` Ã¢â‚¬â€ add an optional composite-pin debug overlay for validating tip, join, stretch-band, and head-center placement live in the app
- `docs/exec-plans/completed/pin-parts-composite-placement-plan.md` Ã¢â‚¬â€ now clarifies manual-layout canonical endpoint data vs optional persisted composite-placement results, including recommended cache invalidation inputs and replay policy

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
