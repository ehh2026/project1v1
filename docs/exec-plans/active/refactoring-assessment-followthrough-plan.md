---
status: active
owner: agent
started: 2026-06-07
updated: 2026-07-30
requirements_ref: refactoring-assessment-followthrough
assessment: assessments/REFACTORING_ASSESSMENT.md
tracker: ../completed/refactoring-plan.md
---

# Refactoring Assessment Follow-Through Plan

Close remaining items from [REFACTORING_ASSESSMENT.md](../../assessments/REFACTORING_ASSESSMENT.md) that were not finished in [refactoring-plan.md](../completed/refactoring-plan.md) (Phases 1–10).

> **2026-07-30 refresh:** Rebased against current code. Phase 12 (MarkerLayerControl) is obsolete; Phase 13 (nullable) and the Phase 18 large-file slice are done. Remaining work is reordered for implementation below. Historical assessments/completed plans are left unchanged.

TO_DO: [Refactoring assessment follow-through](../../TO_DO.md)

## Current Status (2026-07-30)

### Done (do not re-implement)

| Item | Evidence |
|------|----------|
| Phases 1–10 (original refactoring plan) | [refactoring-plan.md](../completed/refactoring-plan.md) |
| Phase 12 — `MarkerLayerControl` positioning extraction | **Obsolete** — control removed; placement via `MarkerPlacementOrchestrator`, `ViewportState`, `MapDisplayControl` |
| Phase 13 — nullable CS8602/CS8604 cleanup | Release build clean; Excel/cluster guards landed |
| Phase 18 large-file slice | `MarkerPlacementOrchestrator`, composite apply service, MainWindow partials; primary `MainWindow.xaml.cs` ~554 lines; TD-001 resolved |

### Remaining — implement next

| Priority | Phase | Item | Notes |
|----------|-------|------|-------|
| 1 | 11 | Map dimensions single source of truth | Runtime still uses `MainWindow` constants `8198×5542`; validator ceilings still `16397×11085` |
| 2 | 14 | `LocationClusterer` spatial indexing | Still nested O(n²) neighbor scan; no `SpatialGrid` |
| 3 | 15 | `ExcelCoordinateReader` streaming parse | Still `XmlDocument`; prefer `XmlReader` (no new NuGet) |
| 4 | 17 | `ApplicationState` decision | Class exists; **zero** other `.cs` references — delete or wire |
| 5 | 16 | `ContentLoader` cache bounds | Unbounded `Dictionary` keyed by location name |
| 6 | 18b | Optional MainWindow extractions | Only if files approach 800 lines again |

### Explicitly deferred (tech debt / separate plans)

| Item | Tracker |
|------|---------|
| Full MVVM (`ViewModels/`) | TD-005 |
| DI container | TD-005 |
| Coordinate value types (`PixelCoordinate`, etc.) | High churn — new plan if pursued |
| `ContentSubwindow` DPI sizing | Low / cosmetic |
| Full UI automation suite | TD-004 |

## Goal

Ship remaining assessment debt in priority order without breaking demo-ready behavior. Each phase is independently shippable with tests and `.\scripts\verify.ps1` green.

## Modularity / File Size Impact

| Area | Constraint |
|------|------------|
| `MainWindow*.cs` | Stay under 800 taste-lines; prefer Services/Utilities extractions over growing partials |
| `Utilities/ExcelCoordinateReader.cs` | Streaming refactor must not re-inflate CCN — keep helpers extracted (complexity gates) |
| `Utilities/LocationClusterer.cs` | New spatial index in its own type (`SpatialGrid`); clusterer stays thin |
| Layer rules | Models ← Utilities/Services ← Views; no Services in Views |

---

## Phase 11 — Map dimensions single source of truth

**Assessment:** §3  
**Status:** ready to implement

### Reality check

- Display / placement space: `MainWindow` `ImageWidth = 8198`, `ImageHeight = 5542` (half of full-res map).
- Full-res map (`16397×11085`) is still relevant for zoomed-region / high-quality crop paths and `StartupValidator` coordinate ceilings.
- There is no `MapMetadata` / `IMapMetadata` type yet.

### Files

| Action | Path |
|--------|------|
| Create | `Models/MapMetadata.cs` (display size + optional full-res ceilings) |
| Modify | `MainWindow.xaml.cs` (remove local constants; inject/load once) |
| Modify | Call sites that pass `ImageWidth`/`ImageHeight` (Navigation, Content, LayoutEditor partials) |
| Modify | `Services/StartupValidator.cs` (use metadata ceilings, not literals) |
| Create | `Tests/MapMetadataTests.cs` |

### Tasks

1. Introduce `MapMetadata` with display width/height (required) and optional full-res max X/Y for validation.
2. Load once at startup from image metadata and/or `visual-config.json` — single owner (`ContentLoader` or app init).
3. Replace `MainWindow` constants and validator literals with that metadata.
4. Regression: known fixture coordinate round-trip unchanged; seed generator / orchestrator tests still pass.

**Acceptance:**
- No hard-coded `8198`/`5542`/`16397`/`11085` in production `.cs` outside tests/fixtures and documented config defaults.
- `MapMetadataTests` cover construction and ceiling behavior.
- `.\scripts\verify.ps1` green.

---

## Phase 14 — LocationClusterer spatial indexing

**Assessment:** §7  
**Status:** ready after Phase 11 (independent; can run in parallel if preferred)

### Files

| Action | Path |
|--------|------|
| Create | `Utilities/SpatialGrid.cs` |
| Modify | `Utilities/LocationClusterer.cs` |
| Create | `Tests/SpatialGridTests.cs`; extend clusterer tests |

### Tasks

1. Grid-based neighbor query for `FindNearbyLocations` (cell size = cluster threshold).
2. Preserve identical cluster output for existing fixtures (behavior lock).
3. Add a synthetic n≥200 timing comparison documenting improvement (not a flaky CI gate).

**Acceptance:** Fixture clusters unchanged; spatial tests pass; verify green.

---

## Phase 15 — ExcelCoordinateReader streaming parse

**Assessment:** §4  
**Status:** ready (independent)

### Decision gate

- **Option A (default):** `XmlReader` forward-only parse — no new dependency.
- **Option B:** ClosedXML/EPPlus — only with security review per AGENTS.md.

### Tasks

1. Replace `XmlDocument` loads with streaming parse; keep public API stable.
2. Preserve row output vs existing `ExcelCoordinateReaderTests`.
3. Keep CCN under Lizard gate (reuse existing helper splits from complexity work).

**Acceptance:** Existing Excel tests pass; no new NuGet unless Option B approved.

---

## Phase 17 — ApplicationState decision

**Assessment:** §10  
**Status:** ready (small; good cleanup PR)

### Tasks

1. Confirm zero production usages of `Models/ApplicationState.cs`.
2. **Preferred:** delete the orphan and any stale doc references in living docs (`AGENTS`/`ARCHITECTURE` only if they mention it).
3. Alternative: wire into layout/navigation mode state with tests — only if a concrete consumer exists in the same PR.

**Acceptance:** No unused public application-state type; architecture docs (living) match reality.

---

## Phase 16 — ContentLoader cache bounds

**Assessment:** §5  
**Status:** ready (independent; lower priority)

### Tasks

1. Optional `MaxCachedLocations` in config (default `0` = unlimited, current behavior).
2. LRU eviction by cache key when limit > 0; Info-level eviction logs.
3. Tests: eviction order; small sets unchanged when unlimited.

**Acceptance:** Default unlimited path unchanged; bounded mode tested.

---

## Phase 18b — Optional MainWindow extractions

**Assessment:** §1 remaining  
**Status:** optional — only when a touched partial approaches the 800-line taste limit

| Extract | New home | When |
|---------|----------|------|
| Subwindow open/position lifecycle | `Services/SubwindowManager.cs` | Content partial growth |
| Zoom animation orchestration | extend animation helper / `Services/ZoomAnimationController.cs` | Navigation growth |
| Marker creation factory | `Services/MarkerFactory.cs` (drawn path already has `Views/DrawnPinMarkerFactory`) | Marker-placement growth |

**Acceptance:** Each extraction reduces a MainWindow partial by ≥100 lines with tests; no feature regression.

---

## Execution Order

1. **Phase 11** — map metadata (unblocks cleaner placement/validation)
2. **Phase 17** — ApplicationState delete-or-wire (quick win; can ship anytime)
3. **Phase 14** — spatial clusterer
4. **Phase 15** — Excel streaming
5. **Phase 16** — ContentLoader LRU
6. **Phase 18b** — only as file-size pressure appears

Ship one phase per PR when practical.

## Documentation (living only)

After each phase:

- Update this plan’s status tables
- Update [tech-debt-tracker.md](../tech-debt-tracker.md) if a TD closes or is added
- Update [TO_DO.md](../../TO_DO.md) remaining scope
- `[Unreleased]` in [CHANGELOG.md](../../../CHANGELOG.md)

Do **not** rewrite completed/archived plans or historical assessment bodies; link forward from this plan instead.

## Definition of Done (remaining scope)

- [ ] Phase 11 complete
- [ ] Phase 14 complete
- [ ] Phase 15 complete **or** deferred with TD entry
- [ ] Phase 17 complete (delete or wire)
- [ ] Phase 16 complete **or** deferred with TD entry
- [ ] Phase 18b triaged (done as needed, or explicitly deferred)
- [ ] `.\scripts\verify.ps1` green
- [ ] This plan moved to `../completed/` (or `../inactive/` if parked mid-flight)
