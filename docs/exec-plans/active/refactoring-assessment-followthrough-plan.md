---
status: active
owner: agent
started: 2026-06-07
requirements_ref: refactoring-assessment-followthrough
assessment: REFACTORING_ASSESSMENT.md
tracker: refactoring-plan.md
---

# Refactoring Assessment Follow-Through Plan

Close remaining items from [REFACTORING_ASSESSMENT.md](../../REFACTORING_ASSESSMENT.md) not already completed in [refactoring-plan.md](refactoring-plan.md) (Phases 1–10).

TO_DO item: [Consider addressing issues in REFACTOR ASSESSMENT](../../TO_DO.md)

## Status Summary

### Already addressed (refactoring-plan Phases 1–10)

| Assessment item | Resolution |
|-----------------|------------|
| §1 MainWindow god object — animation duplication | Phase 2 `AnimateViewportTransition` |
| §1 Radial extension adjustment engine | Phase 3 `RadialExtensionAdjuster` |
| §1 Extension rendering | Phase 5 `ExtensionLineRenderer` |
| §1 Layout editor extraction | Phase 8 `LayoutEditorController` |
| §1 CompositePinRenderPlanBuilder decomposition | Phase 4 |
| §2 RadialExtensionCalculator duplication | Phase 7 |
| §5 ContentLoader duplication | Phase 6 |
| §6 Console.WriteLine / logging | Phase 7, 10 |
| §15 Magic strings (map filename) | `Models/ContentFileNames.cs` (TD-002) |
| §11–§12 Partial architecture cleanup | Phase 9 interfaces, `VisualConfigService` |
| §16 Async inconsistencies | Phase 10 |

### Remaining (this plan)

| Assessment § | Item | Priority |
|--------------|------|----------|
| §1 | MainWindow still large — further decomposition | High |
| §2 | `MarkerLayerControl` positioning duplication | High |
| §3 | Hard-coded map dimensions (16397×11085) | Medium |
| §4 | `ExcelCoordinateReader` DOM parsing | Medium |
| §5 | ContentLoader LRU / cache limits | Low |
| §7 | `LocationClusterer` O(n²) | Medium |
| §8 | `ContentSubwindow` size calculation | Low |
| §9 | Coordinate system value types | Medium |
| §10 | `ApplicationState` unused | Low |
| §11 | MVVM incomplete (`ViewModels/` empty) | Low (defer) |
| §12 | No DI container | Low (defer) |
| §13 | UI / integration test gaps | Medium |
| §14 | Nullable reference warnings (CS8602/CS8604) | Medium |

## Goal

Reduce remaining assessment debt in priority order without breaking demo-ready behavior. Each phase is independently shippable with tests and `verify.ps1` green.

## Phase 11 — Map dimensions single source of truth

**Assessment:** §3

### Files

| Action | Path |
|--------|------|
| Create or extend | `Models/MapMetadata.cs` or extend `VisualConfig` map section |
| Modify | `MainWindow.xaml.cs`, `Views/MarkerLayerControl.xaml.cs`, `Views/MapDisplayControl.xaml.cs` |
| Create | `Tests/MapMetadataTests.cs` |

### Tasks

1. Load map width/height from image metadata at startup (or from `visual-config.json` `Map.SourceWidth/Height`) — single load in `ContentLoader` or `ApplicationInitializer`.
2. Replace literal `16397` / `11085` with injected `MapMetadata` or `IMapMetadata`.
3. Pass dimensions to marker positioning code via constructor/config — not static constants.
4. Regression test: coordinate round-trip unchanged for known fixture point.

**Acceptance:** grep for `16397` and `11085` in `.cs` files returns zero (except tests/fixtures)

## Phase 12 — MarkerLayerControl positioning extraction

**Assessment:** §2

### Files

| Action | Path |
|--------|------|
| Create | `Utilities/MarkerPositionCalculator.cs` |
| Modify | `Views/MarkerLayerControl.xaml.cs` |
| Create | `Tests/MarkerPositionCalculatorTests.cs` |

### Tasks

1. Extract shared screen-position math from `AddMarker`, `AddClusterMarker`, `UpdateMarkerPositions`.
2. Remove duplicate `UpdateMarkerPositionsWithoutTransform` if still redundant after ViewportState fix.
3. Inject map dimensions from Phase 11 — no hard-coded sizes in View.
4. Reduce debug logging verbosity or gate behind `Debug.VerboseMarkerLogging` config flag.
5. Unit tests for calculator; existing UI tests unchanged.

**Acceptance:** `MarkerLayerControl.xaml.cs` under 400 lines; calculator tested

## Phase 13 — Nullable reference warning cleanup

**Assessment:** §14; TO_DO High Priority testing section

### Files

| Action | Path |
|--------|------|
| Modify | `MainWindow.xaml.cs` (~737, ~854, ~891, ~769, ~1183) |
| Modify | `Utilities/ExcelCoordinateReader.cs` (~126, ~216) |

### Tasks

1. Guard `ClusterMarker.Cluster` dereferences — use null-forgiving only where invariant documented (marker always created with cluster).
2. Narrow `_extensionCalculator` and `viewport` flow analysis with early returns or local non-null variables.
3. Fix Excel reader indexing after count checks.
4. Acceptance: `dotnet build -c Release` reports zero CS8602/CS8604 in these files.

**Acceptance:** Release build clean for listed warnings; verify.ps1 passes

## Phase 14 — LocationClusterer spatial indexing

**Assessment:** §7

### Files

| Action | Path |
|--------|------|
| Create | `Utilities/SpatialGrid.cs` |
| Modify | `Utilities/LocationClusterer.cs` |
| Create | `Tests/SpatialGridTests.cs`, extend clusterer tests |

### Tasks

1. Implement grid-based neighbor query for `FindNearbyLocations` (cell size = cluster threshold).
2. Preserve identical cluster output for existing test fixtures (behavior must not change).
3. Add benchmark-style test asserting O(n²) baseline vs grid path faster at n ≥ 200 (synthetic data).

**Acceptance:** Cluster output identical on real `locations.json` sample; perf test documents improvement

## Phase 15 — ExcelCoordinateReader streaming parse (optional EPPlus)

**Assessment:** §4

### Decision gate

- **Option A:** Refactor to `XmlReader` streaming (no new dependency)
- **Option B:** Add `ClosedXML` or `EPPlus` (requires security review per AGENTS.md)

### Recommended: Option A first

1. Replace `XmlDocument` load with `XmlReader` forward-only parse in `ExcelCoordinateReader`.
2. Preserve public API and row output identical to current tests.
3. If parse time still poor on full workbook, evaluate Option B in separate security-reviewed PR.

**Acceptance:** Existing Excel reader tests pass; no new NuGet deps unless approved

## Phase 16 — ContentLoader cache bounds

**Assessment:** §5

### Tasks

1. Add optional `MaxCachedLocations` to config (default: 0 = unlimited for MVP backward compat).
2. Implement simple LRU keyed by `location.Id` when limit > 0.
3. Log cache evictions at Info level.
4. Tests: eviction order, hit rate unchanged for small sets.

**Acceptance:** Default behavior unchanged; bounded mode tested

## Phase 17 — ApplicationState decision

**Assessment:** §10

### Tasks

1. Audit usages of `Models/ApplicationState.cs`.
2. Either wire into `LayoutEditorController` / navigation for centralized mode state **or** delete class and update architecture docs.
3. No dead public API left untested or undocumented.

**Acceptance:** Single documented state strategy; no orphan class

## Phase 18 — MainWindow further decomposition (ongoing)

**Assessment:** §1 remaining

### Targets (incremental — one PR each)

| Extract | New home |
|---------|----------|
| Subwindow open/position lifecycle | `Services/SubwindowManager.cs` |
| Zoom animation orchestration callbacks | extend existing animation helper or `Services/ZoomAnimationController.cs` |
| Marker factory (`CreateImagePinMarker`, cluster markers) | `Services/MarkerFactory.cs` |

Update [refactoring-plan.md](refactoring-plan.md) with Phases 11+ checkboxes as each lands.

**Acceptance:** Each extraction reduces `MainWindow.xaml.cs` by ≥100 lines with tests; no feature regression

## Deferred (document only — not in scope)

| Item | Reason |
|------|--------|
| Full MVVM (`ViewModels/`) | Large architectural shift; track as TD-005 |
| Microsoft.Extensions.DependencyInjection | TD-005; Phase 9 interfaces sufficient for now |
| Coordinate value types (`PixelCoordinate`, etc.) | Nice-to-have; high churn — separate plan if pursued |
| ContentSubwindow DPI sizing | Low severity; cosmetic |
| Full integration / UI automation suite | Track under TD-004; add incrementally |

## Execution Order

1. Phase 13 (nullable) — low risk, clears CI noise
2. Phase 11 (map dimensions) — unblocks Phase 12
3. Phase 12 (MarkerLayerControl)
4. Phase 14 (clusterer perf)
5. Phases 15–18 as capacity allows

## Documentation

After each phase:

- Update [refactoring-plan.md](refactoring-plan.md) checkbox
- Update [tech-debt-tracker.md](../tech-debt-tracker.md) if TD item closed
- CHANGELOG entry under `[Unreleased]`

## Definition of Done (full plan)

- All Phase 11–14 complete
- At least one Phase 18 extraction complete
- Phases 15–17 triaged (done or explicitly deferred with issue link in tech-debt-tracker)
- `REFACTORING_ASSESSMENT.md` header note pointing to this plan and completion status
- `scripts/verify.ps1` passes
