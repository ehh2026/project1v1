---
status: active
owner: agent
started: 2026-06-07
requirements_ref: manual-layout-seed-alignment
parent_program: composite-pins-program.md
---

# Manual Layout Seed Alignment Plan

Align offline seed generation with runtime placement so `Images&Content/manual-layouts.json` loads without manual fixes.

Related design doc: [MANUAL_LAYOUT_EDITOR.md](../../guides/MANUAL_LAYOUT_EDITOR.md)

TO_DO items: [Shared runtime/seed placement path](../../TO_DO.md), [Reliable seed loading in app](../../TO_DO.md), [Layout persistence robustness](../../TO_DO.md)

## Problem

`scripts/generate_manual_layout_seeds.ps1` embeds a PowerShell/C# hybrid reimplementation of:

- `Utilities/RadialExtensionCalculator.cs` — dense-group detection and radial extension math
- `Services/LayoutKeyGenerator.cs` — layout key hashing and formatting
- `Models/ViewportState.cs` — `GetSourceRect()` / `SourceToScreen()` projection

Any drift between the script and the app causes:

- Generated layout keys that do not match what `LayoutEditorController` computes at runtime
- Marker endpoints that differ from live `ApplyRadialExtensions` output
- Seeds that exist in JSON but are never auto-applied

## Goal

One authoritative placement path for seed generation and runtime rendering. Generated seeds must load via `ManualLayoutManager.LoadLayout()` without key-mismatch workarounds.

## Architecture

Replace the duplicated PowerShell math with a small **headless .NET console project** (`Tools/ManualLayoutSeedGenerator/`) that references the main app project and reuses the app's Models, Utilities, and Services layers. The console reads the same inputs as the script (`visual-config.json`, Excel coordinates, map image dimensions) and writes `manual-layouts.json` using the real `LocationClusterer`, `RadialExtensionCalculator`, `LayoutKeyGenerator`, `ViewportState`, and `ManualLayoutCollection` schema.

Because the main app targets `net6.0-windows` and uses WPF/`System.Windows.Point`, the tool should also target `net6.0-windows` and run on the Windows verification path. Do not create a `net6.0` tool unless the shared placement code has first been extracted into a non-WPF class library.

Keep `scripts/generate_manual_layout_seeds.ps1` as a thin wrapper that invokes `dotnet run --project Tools/ManualLayoutSeedGenerator` so existing docs and habits still work.

## Phase 1 — Parity baseline and drift detection — CODE DONE (2026-06-25)

**Deliverables:** failing-then-passing parity tests that lock current behavior before refactor.

### Tasks

1. [x] Add fixed fixture inputs in `Tests/ManualLayoutSeedGeneratorTests.cs`:
   - 3–5 synthetic location clusters (3, 5, and 8 markers) at known pixel coordinates
   - Fixed viewport (`ZoomLevel = 55.0`, known `ViewportX/Y/Width/Height`, container 1920×1080)
   - Radial extension config from a checked-in test `visual-config` fragment
2. [x] Assert runtime-key and generated-extension behavior:
   - `LayoutKeyGenerator.GenerateKey(...)` matches golden key strings checked into `Tests/Fixtures/manual-layout-keys/`
   - `RadialExtensionCalculator` output (angles, line lengths, screen endpoints) matches golden JSON in `Tests/Fixtures/manual-layout-extensions/`
3. [x] Add harness coverage that generated seeds use the same key shape the runtime computes for the cluster viewport.
4. [x] Document current known mismatches (if any) in this plan's **Known Drift Log** section before changing the generator.

Implementation note: the landed tests use in-process behavior assertions instead of separate checked-in golden JSON files. That keeps the guard tied to the runtime API seam now used by the generator and avoids brittle fixture churn.

**Acceptance:**

- Parity/load-key tests exist and pass against current C# implementation
- PowerShell script-vs-C# drift is removed by deleting the embedded script math in Phase 2

## Phase 2 — Headless seed generator console — CODE DONE (2026-06-25)

**Deliverables:** `Tools/ManualLayoutSeedGenerator` replaces inline PowerShell math.

### Files

| Action | Path |
|--------|------|
| Create | `Tools/ManualLayoutSeedGenerator/ManualLayoutSeedGenerator.csproj` |
| Create | `Tools/ManualLayoutSeedGenerator/Program.cs` |
| Create | `Tools/ManualLayoutSeedGenerator/ManualLayoutSeedGenerator.cs` — reusable generator service called by `Program` and tests |
| Modify | `scripts/generate_manual_layout_seeds.ps1` — delegate to dotnet console |
| Modify | `InteractiveWorldMap.sln` — add tool project |

### Tasks

1. [x] Create console project targeting `net6.0-windows`; add a `ProjectReference` to `InteractiveWorldMap.csproj`.
2. [x] Implement CLI args mirroring the script:
   - `--config`, `--excel`, `--map-image`, `--output`
3. [x] Reuse existing types:
   - Load locations via `Utilities/ExcelCoordinateReader.cs`
   - Cluster locations via `Utilities/LocationClusterer.cs` with `VisualConfig.ClusterDistanceThreshold`
   - Use the same default viewport sizes as the current script unless intentionally changed: 1920×1080, 1440×900, 1600×1200, 3440×1440
   - Build each cluster viewport with the runtime shape:
     `ViewportState.CreateZoomedView(cluster.CenterPoint.X, cluster.CenterPoint.Y, visualConfig.ZoomScale, mapWidth, mapHeight, viewportWidth, viewportHeight)`
   - Call `RadialExtensionCalculator` for extensions
   - Call `LayoutKeyGenerator.GenerateKey` for keys
   - Serialize with `ManualLayoutCollection` / `ManualLayoutOrigin.AutoSeed` variant schema already in `Models/ManualLayout.cs`
4. [x] Preserve existing `Manual`/`Imported` variants and `SelectedVariants` when writing the output file; replace only the `AutoSeed` variant with `VariantId = "seed-default"` for each generated group.
5. [x] Set `GeneratorVersion` on each seed variant (`"ManualLayoutSeedGenerator/1.0"`) for future invalidation.
6. [x] Update `scripts/generate_manual_layout_seeds.ps1` to call the console; delete duplicated `SeedLayoutMath` C# embedded in PowerShell once parity tests pass.

**Acceptance:**

- `dotnet run --project Tools/ManualLayoutSeedGenerator/ManualLayoutSeedGenerator.csproj` produces `manual-layouts.json`
- Parity/load-key tests from Phase 1 pass

## Phase 3 — End-to-end load verification — AUTOMATED DONE; GUI SMOKE PENDING (2026-06-25)

**Deliverables:** seeds auto-apply in the running app.

### Tasks

1. [x] Add generator/load-key tests:
   - Generate seeds to a temp file using the console tool (or in-process generator entry point)
   - Feed file to `ManualLayoutManager` with the same key `LayoutEditorController` would compute for that cluster/viewport
   - Assert `LoadLayout(key)` returns non-null `AutoSeed` variant with expected marker count
2. [x] Add `scripts/verify_manual_layout_seeds.ps1` and call it from `verify.ps1` to:
   - Run seed generator against repo `visual-config.json` and Excel file
   - Assert output JSON deserializes to `ManualLayoutCollection`
   - Assert every group has at least one `AutoSeed` default variant
3. [ ] Manual smoke checklist (run on Windows):
   - Launch app, zoom to a cluster covered by seeds
   - Confirm layout applies without entering edit mode
   - Confirm status/log indicates loaded variant origin `AutoSeed`

**Acceptance:**

- `.\scripts\verify.ps1` includes seed verification
- Pending: manual smoke checklist passes for at least two seeded clusters

## Phase 4 — Documentation and cleanup — PARTIAL (2026-06-25)

### Tasks

1. [x] Update [MANUAL_LAYOUT_EDITOR.md](../../guides/MANUAL_LAYOUT_EDITOR.md) — seed generation section references console tool, not PowerShell math port.
2. [x] Update [AGENTS.md](../../../AGENTS.md) quick commands if a new script entry is added.
3. [x] Add CHANGELOG entry under `[Unreleased]`.
4. [ ] Move this plan to `exec-plans/completed/` when all phases pass, including GUI smoke.

## Phase 5 — Persistence robustness (storage location + brittle keys)

**Why here:** seed/runtime key parity (Phases 1–3) only helps if saved layouts actually survive and resolve. Two persistence weaknesses surfaced 2026-06-23 and are folded in here.

### 5a. Stable per-user storage — DONE (2026-06-23)
- Relative `ManualLayoutEditor.LayoutStoragePath` previously resolved to `BaseDirectory` (`bin/…`), so a `dotnet clean`/rebuild or redeploy discarded user-saved layouts, and runtime edits never reached the source seed file.
- `MainWindow.ResolveLayoutStoragePath` now treats a relative path as the bundled (app-folder) seed and points the writable store at `%AppData%/InteractiveWorldMap/manual-layouts.json`, seeded once from the bundled file. Rooted paths are honored as-is; falls back to the bundled path if AppData is unavailable.

### 5b. Don't crash on broken/old/invalidated layouts — DONE (2026-06-23)
- `ManualLayoutManager.LoadLayoutCollection` now wraps deserialize/normalize in try/catch: a corrupt or schema-incompatible file is backed up to `*.corrupt`, logged, and treated as an empty set instead of throwing. Test: `ManualLayoutManagerTests.LoadLayout_WhenFileIsCorrupt_DoesNotThrow_AndBacksUpBadFile`.

### 5c. Brittle keys — CODE DONE (2026-06-23; GUI confirmation pending)

Layout keys embed canvas size, zoom, viewport center/size, and radial config (`Services/LayoutKeyGenerator.cs`), so a window-size or config change orphans saved layouts (they exist but never resolve via exact-match `TryLoad`).

**Findings (what's built):**
- **Cluster keys already tolerate size/center/config drift.** `LoadLayout` already falls back to `FindCompatibleGroup` → `AreKeysCompatible`, which (for cluster keys) only compares the location hash + zoom (within 0.1). So a resize/config change on a *cluster* layout already resolves. No new code needed beyond a regression test.
- **The re-projection engine exists.** `CompositePinApplicationService.BuildApplyInstructions` re-projects the pin base from the location's source coords (`viewport.SourceToScreen`) and re-projects the extended position from `ManualLayoutMarker.SourceExtendedX/Y` when present. Seeds populate `SourceExtendedX/Y`; user saves now do too via `MainWindow.CollectCurrentExtensions`.

**Resolved gaps → chosen approach: normalized (source-space) coordinates.**

1. **Full-map key is the real blocker.** `fullmap_s{W}x{H}` embeds canvas size and `AreKeysCompatible` forces an *exact* match for it, so after a resize the layout exists but is never looked up. Because positions re-project from source space, **size does not belong in the key** — there is only ever one "whole map" layout.
   - `LayoutKeyGenerator.GenerateFullMapGroupKey()` → parameterless, returns the constant `"fullmap"`.
   - `IsFullMapKey` matches the `"fullmap"` prefix (covers new `"fullmap"` and legacy `"fullmap_s…"`).
   - `AreKeysCompatible`: if **both** keys are full-map → compatible (`true`); if exactly one is → `false`.
2. **User saves must store source-space extended coords** so the re-projection is exact (not the angle+pixel-length fallback).
   - Add nullable `SourceExtendedX/Y` to `Models/RadialExtension.cs` (mirrors `ManualLayoutMarker`).
   - `MainWindow.CollectCurrentExtensions` sets them via `viewport.ScreenToSource(extendedScreen…)` (viewport + canvas are in scope there).
   - `ManualLayoutMarker.FromRadialExtension` copies them through. No `IManualLayoutManager`/`SaveVariant` signature changes.

**Migration (decided): leave orphaned, resolve via compatibility.** Old `fullmap_s{W}x{H}` groups on disk are *not* re-keyed. On load, the new `"fullmap"` key resolves them through `FindCompatibleGroup`/`AreKeysCompatible` (both full-map ⇒ compatible). The next user save writes the canonical `"fullmap"` group; the stale sized group becomes harmless dead data. Re-keying on load was rejected as unnecessary churn.

**Out of scope here:** the offline seed generator (`scripts/generate_manual_layout_seeds.ps1`) still emits `fullmap_s{W}x{H}`; those keys keep resolving via compatibility. Aligning the generator to emit `"fullmap"` is folded into the Phase 2 generator rewrite.

Tasks:
1. [x] Cluster compatible-key fallback — already implemented in `LoadLayout`; lock with a regression test.
2. [x] Size-independent full-map key + compatibility rule (`LayoutKeyGenerator`).
3. [x] Populate `SourceExtendedX/Y` on user save (`RadialExtension`, `CollectCurrentExtensions`, `FromRadialExtension`).
4. [x] Tests: full-map key is constant; two different-size full-map keys are compatible; a full-map layout saved at one size loads at another and re-projects; genuinely incompatible (cluster vs full-map, different hash) keys do not collide.

**Acceptance:** code coverage exists for full-map key stability, legacy full-map key compatibility, source-space coordinate persistence, and incompatible full-map-vs-cluster keys. Remaining human GUI confirmation: a saved full-map layout reloads after a window resize and lands on the correct map positions. Cluster layouts already survived; corrupt/stale files never crash; user layouts survive a rebuild.

## Known Drift Log

| Area | Script behavior | App behavior | Status |
|------|-----------------|--------------|--------|
| Layout key hash | Embedded `SeedLayoutMath.GenerateLayoutKey` | `LayoutKeyGenerator.GenerateKey` | Resolved: script math removed; generator calls runtime service |
| Viewport projection | Embedded `SeedViewport.GetSourceRect` | `ViewportState.GetSourceRect` | Resolved: script math removed; generator calls runtime `ViewportState` |
| Radial extensions | Embedded `CalculateRadialExtensions` | `RadialExtensionCalculator` | Resolved: script math removed; generator calls runtime calculator |

## Risks

| Risk | Mitigation |
|------|------------|
| Console project pulls WPF dependencies | Reference only Models/Utilities/Services; extract shared viewport builder if needed |
| Golden files churn on every config change | Scope goldens to fixed test fixtures, not live `visual-config.json` |
| Cluster viewport construction differs from MainWindow | Use `ViewportState.CreateZoomedView(cluster.CenterPoint.X, cluster.CenterPoint.Y, visualConfig.ZoomScale, mapWidth, mapHeight, viewportWidth, viewportHeight)` exactly; extract a `ClusterViewportBuilder` helper only if another call site would use it |

## Definition of Done

- [x] Seed generator calls the same C# placement code as runtime
- [x] Parity and load tests pass locally
- [ ] Generated `manual-layouts.json` auto-loads for representative clusters without manual key editing (manual GUI smoke pending)
- [x] `scripts/verify.ps1` passes on .NET 6 SDK (2026-06-25)
