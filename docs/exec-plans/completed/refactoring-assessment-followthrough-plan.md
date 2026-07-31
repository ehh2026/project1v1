---
status: completed
owner: agent
started: 2026-06-07
updated: 2026-07-30
completed: 2026-07-30
requirements_ref: refactoring-assessment-followthrough
assessment: assessments/REFACTORING_ASSESSMENT.md
tracker: ../completed/refactoring-plan.md
---

# Refactoring Assessment Follow-Through Plan

Close remaining items from [REFACTORING_ASSESSMENT.md](../../assessments/REFACTORING_ASSESSMENT.md) that were not finished in [refactoring-plan.md](../completed/refactoring-plan.md) (Phases 1–10).

> **2026-07-30 refresh:** Rebased against current code. Phase 12 (MarkerLayerControl) is obsolete; Phase 13 (nullable) and the Phase 18 large-file slice are done. Open questions resolved below; remaining work ordered for implementation. Historical assessments/completed plans are left unchanged.

TO_DO: [Refactoring assessment follow-through](../../TO_DO.md)

## Current Status (2026-07-30)

### Done (do not re-implement)

| Item | Evidence |
|------|----------|
| Phases 1–10 (original refactoring plan) | [refactoring-plan.md](../completed/refactoring-plan.md) |
| Phase 11 â€” Map dimensions single source of truth | `Models/MapMetadata`; MainWindow properties; display-space `StartupValidator` ceilings; TD-014 resolved |
| Phase 17 â€” ApplicationState decision | Deleted orphan `Models/ApplicationState.cs`; TD-017 resolved |
| Phase 14 â€” LocationClusterer spatial indexing | `SpatialGrid` + 3×3 neighbor query; TD-015 resolved |
| Phase 15 â€” ExcelCoordinateReader streaming parse | `XmlReader` for shared strings + rows; workbook/rels DOM; TD-016 resolved |
| Phase 16 â€” ContentLoader cache bounds | Id-keyed LRU + `MaxCachedLocations`; TD-018 resolved |
| Phase 12 â€” `MarkerLayerControl` positioning extraction | **Obsolete** â€” control removed; placement via `MarkerPlacementOrchestrator`, `ViewportState`, `MapDisplayControl` |
| Phase 13 â€” nullable CS8602/CS8604 cleanup | Release build clean; Excel/cluster guards landed |
| Phase 18 large-file slice | `MarkerPlacementOrchestrator`, composite apply service, MainWindow partials; primary `MainWindow.xaml.cs` ~554 lines; TD-001 resolved |

### Remaining â€” implement next

| Priority | Phase | Item | Notes |
|----------|-------|------|-------|
| 1 | 18b | Optional MainWindow extractions | Deferred â€” no partial near 800-line taste pressure |

### Explicitly deferred (tech debt / separate plans)

| Item | Tracker |
|------|---------|
| Full MVVM (`ViewModels/`) | TD-005 |
| DI container | TD-005 |
| Coordinate value types (`PixelCoordinate`, etc.) | High churn â€” new plan if pursued |
| `ContentSubwindow` DPI sizing | Low / cosmetic |
| Full UI automation suite | TD-004 |
| Assessment §6 excessive logging cleanup | Out of scope here â€” use existing `DebugConfig` / levels; no dedicated phase |
| Gallery multi-image cache (`LoadAllLocationImages*`) | Not Phase 16 â€” UI path bypasses `_contentCache`; new TD if memory becomes an issue |

## Decisions (resolved 2026-07-30)

These close the readiness gaps. Implementers should treat them as constraints, not reopen them without evidence.

### Phase 11 â€” map dimensions

| Question | Decision |
|----------|----------|
| Coordinate / placement space | **Display** space only. Excel columns E/F are half-size; docs and `MainWindow` constants (`8198×5542`) match `World Map Extra Large.jpg`. |
| Full-res (`16397×11085`) role | Crop / high-quality zoom source only (`ZoomedRegionCache` already scales from actual bitmap sizes). Not used for marker placement or Excel validation. |
| Validator ceilings | **Bug fix:** change `StartupValidator` from full-res ceilings to **display** max X/Y. Coords above display size are invalid for current content. |
| Type shape | `Models/MapMetadata` with required `DisplayWidth` / `DisplayHeight` and optional `FullResWidth` / `FullResHeight` (defaults / probes for docs and sanity checks â€” not placement). |
| Source of truth | `MapMetadata.CreateDefault()` documents asset defaults (`8198×5542`, `16397×11085`). After the display map loads, prefer `MapMetadata.FromDisplayBitmap(bitmap)` (PixelWidth/Height) with default fallback if load failed. **Do not** add map size fields to `visual-config.json` in this phase (avoids config/Excel/bitmap drift). |
| Owner | Construct once at app/MainWindow init after display map load; pass into navigation / placement / validator. Do not re-read literals in partials. |
| `ZoomedRegionCache` | Leave as-is (measures bitmaps). No requirement to inject `MapMetadata` into crop scale math. |
| Docs / tools | Living guide [UPDATING_COORDINATES.md](../../guides/UPDATING_COORDINATES.md) already matches display space â€” add a one-line pointer to `MapMetadata` if useful. `Tools/ManualLayoutSeedGenerator` CLI dims stay optional overrides; out of scope unless a tiny default-from-`CreateDefault()` is free. |

### Phase 14 â€” spatial grid

| Question | Decision |
|----------|----------|
| Dependency on Phase 11 | **None** â€” independent; may ship in parallel or after 17. |
| Correctness | Cell size = `DistanceThreshold`. Neighbor query checks the seed cell and its **3×3** Moore neighborhood so Euclidean radius â‰¤ threshold is preserved. |
| Behavior lock | Same clusters as today for existing fixtures (membership + centers within current float tolerance). Input list order still defines seed order. |
| Timing comparison | Add a test marked with `Trait("Performance")` **or** `Skip` documenting n≥200 improvement. **Not** included in default `dotnet test` / verify filter. |
| `ClusterCache` | Identical outputs ⇒ existing cache keys remain valid; no migration. |

### Phase 15 â€” Excel streaming

| Question | Decision |
|----------|----------|
| Library | **Option A:** `XmlReader` only. Option B (ClosedXML/EPPlus) needs security review â€” do not start without explicit approval. |
| Approach | (1) Stream `xl/sharedStrings.xml` into `List<string>`. (2) Workbook + rels are tiny â€” stream or keep a small DOM load for sheet path discovery only. (3) Stream each worksheet `sheetData` row-by-row into the existing `Dictionary<string,string>` row shape. (4) Keep `ParseLocationRow` / public `ReadLocationsFromExcel` API stable. |
| CCN | Reuse helper splits; stay under Lizard gate. Prefer extracting stream helpers over growing one method. |

### Phase 16 â€” ContentLoader cache

| Question | Decision |
|----------|----------|
| Hot path | UI opens content via `LoadAllLocationImagesWithTranslationsAsync`, which **does not** use `_contentCache`. Only `LoadLocationContentAsync` uses it (tests / legacy interface). |
| Phase scope | Harden that existing cache only: key by **`location.Id`**, optional `MaxCachedLocations` on `VisualConfig` (default `0` = unlimited), LRU eviction + Info log when limit > 0. |
| Out of scope | Caching / bounding for `LoadAllLocationImages*` â€” separate TD if needed. |
| Eviction | Remove dictionary entry on LRU drop; frozen `BitmapImage`s become unreachable for GC. No explicit `Freeze` undo required. |

### Phase 17 â€” ApplicationState

| Question | Decision |
|----------|----------|
| Choice | **Delete** `Models/ApplicationState.cs`. Confirmed zero `.cs` references outside its own file. |
| Docs | Update living AGENTS/ARCHITECTURE only if they mention it. Do **not** rewrite `.kiro` historical specs. |
| Wire alternative | Rejected unless a concrete consumer appears in the same PR (none today). |

---

## Goal

Ship remaining assessment debt in priority order without breaking demo-ready behavior. Each phase is independently shippable with tests and `.\scripts\verify.ps1` green.

## Modularity / File Size Impact

| Area | Constraint |
|------|------------|
| `MainWindow*.cs` | Stay under 800 taste-lines; prefer Services/Utilities extractions over growing partials |
| `Utilities/ExcelCoordinateReader.cs` | Streaming refactor must not re-inflate CCN â€” keep helpers extracted (complexity gates) |
| `Utilities/LocationClusterer.cs` | New spatial index in its own type (`SpatialGrid`); clusterer stays thin |
| Layer rules | Models ← Utilities/Services ← Views; no Services in Views |

---

## Phase 11 â€” Map dimensions single source of truth

**Assessment:** §3  
**Status:** complete (2026-07-30)  
**TD:** TD-014 (resolved)

### Reality check

- Display / placement space: `MainWindow` `ImageWidth = 8198`, `ImageHeight = 5542` (half of full-res map).
- Full-res map (`16397×11085`) is still relevant for zoomed-region / high-quality crop paths; **`ZoomedRegionCache` already uses bitmap pixel sizes**.
- `StartupValidator` currently warns against full-res ceilings â€” **incorrect for display-space coords**; fix as part of this phase.
- There is no `MapMetadata` / `IMapMetadata` type yet.

### Files

| Action | Path |
|--------|------|
| Create | `Models/MapMetadata.cs` (`CreateDefault`, `FromDisplayBitmap`, display + optional full-res) |
| Modify | `MainWindow.xaml.cs` (remove local constants; hold one `MapMetadata`) |
| Modify | Call sites that pass `ImageWidth`/`ImageHeight` (Navigation, Content, LayoutEditor partials) |
| Modify | `Services/StartupValidator.cs` (display ceilings from metadata, not `16397`/`11085` literals) |
| Create | `Tests/MapMetadataTests.cs` |
| Optional | Pointer in [UPDATING_COORDINATES.md](../../guides/UPDATING_COORDINATES.md) |

### Tasks

1. Introduce `MapMetadata` per Decisions.
2. Construct once after display map load; fall back to `CreateDefault()`.
3. Replace `MainWindow` constants and validator literals; validator max = display size.
4. Regression: known fixture coordinate round-trip unchanged; seed generator / orchestrator tests still pass.

**Acceptance:**
- No hard-coded `8198`/`5542`/`16397`/`11085` in production `.cs` outside tests/fixtures and `MapMetadata.CreateDefault()` (documented defaults).
- Coordinate validation uses **display** ceilings.
- `MapMetadataTests` cover construction, `FromDisplayBitmap`, and ceiling behavior.
- `.\scripts\verify.ps1` green.

---

## Phase 17 â€” ApplicationState decision

**Assessment:** §10  
**Status:** complete (2026-07-30) â€” deleted orphan type  
**TD:** TD-017 (resolved)

### Tasks

1. Reconfirm zero production usages of `Models/ApplicationState.cs`.
2. Delete the orphan; grep living docs (`AGENTS.md`, `ARCHITECTURE.md`) and fix only if mentioned.
3. Do not edit archived assessments or `.kiro` specs.

**Acceptance:** Type gone; living architecture docs match reality; verify green.

---

## Phase 14 â€” LocationClusterer spatial indexing

**Assessment:** §7  
**Status:** complete (2026-07-30)  
**TD:** TD-015 (resolved)

### Files

| Action | Path |
|--------|------|
| Create | `Utilities/SpatialGrid.cs` |
| Modify | `Utilities/LocationClusterer.cs` |
| Create | `Tests/SpatialGridTests.cs`; extend `Tests/LocationClustererTests.cs` |

### Tasks

1. Grid-based neighbor query for `FindNearbyLocations` (cell size = cluster threshold; **3×3** cell scan).
2. Preserve identical cluster output for existing fixtures (behavior lock).
3. Add Trait/Skip timing comparison for n≥200 (not a verify gate).

**Acceptance:** Fixture clusters unchanged; spatial tests pass; verify green; `ClusterCache` needs no migration.

---

## Phase 15 â€” ExcelCoordinateReader streaming parse

**Assessment:** §4  
**Status:** complete (2026-07-30)  
**TD:** TD-016 (resolved)

### Decision gate

- **Option A (default):** `XmlReader` forward-only parse â€” no new dependency.
- **Option B:** ClosedXML/EPPlus â€” only with security review per AGENTS.md.

### Tasks

1. Replace worksheet (and shared-strings) `XmlDocument` loads with streaming per Decisions sketch; keep public API stable.
2. Preserve row output vs existing `ExcelCoordinateReaderTests`.
3. Keep CCN under Lizard gate (reuse existing helper splits from complexity work).

**Acceptance:** Existing Excel tests pass; no new NuGet unless Option B approved.

---

## Phase 16 â€” ContentLoader cache bounds

**Assessment:** §5  
**Status:** complete (2026-07-30)  
**TD:** TD-018 (resolved)

### Tasks

1. Change `_contentCache` key from `location.Name` to `location.Id`.
2. Add optional `MaxCachedLocations` to `VisualConfig` / `visual-config.json` (default `0` = unlimited); document in [VISUAL_CONFIG.md](../../guides/VISUAL_CONFIG.md).
3. LRU eviction when limit > 0; Info-level eviction logs.
4. Tests: eviction order; Id key; unlimited path unchanged.
5. Do **not** add gallery `LoadAll*` caching in this phase.

**Acceptance:** Default unlimited path unchanged; bounded mode + Id key tested; verify green.

---

## Phase 18b â€” Optional MainWindow extractions

**Assessment:** §1 remaining  
**Status:** deferred (2026-07-30) â€” no MainWindow partial currently near the 800-line taste limit; extract when growth pressure returns

| Extract | New home | When |
|---------|----------|------|
| Subwindow open/position lifecycle | `Services/SubwindowManager.cs` | Content partial growth |
| Zoom animation orchestration | extend animation helper / `Services/ZoomAnimationController.cs` | Navigation growth |
| Marker creation factory | `Services/MarkerFactory.cs` (drawn path already has `Views/DrawnPinMarkerFactory`) | Marker-placement growth |

**Acceptance:** Each extraction reduces a MainWindow partial by ≥100 lines with tests; no feature regression.

---

## Execution Order

1. ~~**Phase 11** â€” map metadata + validator ceiling fix~~ **done**
2. ~~**Phase 17** â€” delete `ApplicationState`~~ **done**
3. ~~**Phase 14** â€” spatial clusterer~~ **done**
4. ~~**Phase 15** â€” Excel streaming~~ **done**
5. ~~**Phase 16** â€” ContentLoader LRU (narrow scope)~~ **done**
6. ~~**Phase 18b** â€” optional extractions~~ **deferred** (no file-size pressure)

Ship one phase per PR when practical.

## Documentation (living only)

After each phase:

- Update this planâ€™s status tables
- Update [tech-debt-tracker.md](../tech-debt-tracker.md) if a TD closes or is added
- Update [TO_DO.md](../../TO_DO.md) remaining scope
- `[Unreleased]` in [CHANGELOG.md](../../../CHANGELOG.md)

Do **not** rewrite completed/archived plans or historical assessment bodies; link forward from this plan instead.

## Definition of Done (remaining scope)

- [x] Phase 11 complete
- [x] Phase 17 complete (delete)
- [x] Phase 14 complete
- [x] Phase 15 complete **or** deferred with TD entry
- [x] Phase 16 complete **or** deferred with TD entry
- [x] Phase 18b triaged (done as needed, or explicitly deferred)
- [x] `.\scripts\verify.ps1` green
- [x] This plan moved to `../completed/` (or `../inactive/` if parked mid-flight)

