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

TO_DO items: [Make seed generator use runtime algorithm](../../TO_DO.md), [Make generated seeds reliably load](../../TO_DO.md)

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

Replace the duplicated PowerShell math with a small **headless .NET console project** (`Tools/ManualLayoutSeedGenerator/`) that references the main app's Models, Utilities, and Services layers. The console reads the same inputs as the script (`visual-config.json`, Excel coordinates, map image dimensions) and writes `manual-layouts.json` using the real `RadialExtensionCalculator`, `LayoutKeyGenerator`, and `ViewportState`.

Keep `scripts/generate_manual_layout_seeds.ps1` as a thin wrapper that invokes `dotnet run --project Tools/ManualLayoutSeedGenerator` so existing docs and habits still work.

## Phase 1 — Parity baseline and drift detection

**Deliverables:** failing-then-passing parity tests that lock current behavior before refactor.

### Tasks

1. Add `Tests/ManualLayoutSeedParityTests.cs` with fixed fixture inputs:
   - 3–5 synthetic location clusters (3, 5, and 8 markers) at known pixel coordinates
   - Fixed viewport (`ZoomLevel = 55.0`, known `ViewportX/Y/Width/Height`, container 1920×1080)
   - Radial extension config from a checked-in test `visual-config` fragment
2. For each fixture, assert:
   - `LayoutKeyGenerator.GenerateKey(...)` matches golden key strings checked into `Tests/Fixtures/manual-layout-keys/`
   - `RadialExtensionCalculator` output (angles, line lengths, screen endpoints) matches golden JSON in `Tests/Fixtures/manual-layout-extensions/`
3. Add a harness test that loads a golden `manual-layouts.json` snippet and verifies `ManualLayoutManager.ApplyLayout` round-trips marker names and source-space endpoints.
4. Document current known mismatches (if any) in this plan's **Known Drift Log** section before changing the generator.

**Acceptance:**

- Parity tests exist and pass against current C# implementation
- Any script-vs-C# mismatch is recorded with repro fixture

## Phase 2 — Headless seed generator console

**Deliverables:** `Tools/ManualLayoutSeedGenerator` replaces inline PowerShell math.

### Files

| Action | Path |
|--------|------|
| Create | `Tools/ManualLayoutSeedGenerator/ManualLayoutSeedGenerator.csproj` |
| Create | `Tools/ManualLayoutSeedGenerator/Program.cs` |
| Modify | `scripts/generate_manual_layout_seeds.ps1` — delegate to dotnet console |
| Modify | `InteractiveWorldMap.sln` — add tool project |

### Tasks

1. Create console project targeting `net6.0-windows` (or `net6.0` if no WPF deps needed — prefer referencing existing class libraries only).
2. Implement CLI args mirroring the script:
   - `--config`, `--excel`, `--map-image`, `--output`
3. Reuse existing types:
   - Load locations via `Utilities/ExcelCoordinateReader.cs`
   - Build `ViewportState` for each cluster zoom view (mirror `MainWindow` cluster zoom viewport construction)
   - Call `RadialExtensionCalculator` for extensions
   - Call `LayoutKeyGenerator.GenerateKey` for keys
   - Serialize with `ManualLayoutCollection` / `ManualLayoutOrigin.AutoSeed` variant schema already in `Models/ManualLayout.cs`
4. Set `GeneratorVersion` on each seed variant (e.g. `"ManualLayoutSeedGenerator/1.0"`) for future invalidation.
5. Update `scripts/generate_manual_layout_seeds.ps1` to call the console; delete duplicated `SeedLayoutMath` C# embedded in PowerShell once parity tests pass.

**Acceptance:**

- `dotnet run --project Tools/ManualLayoutSeedGenerator` produces `manual-layouts.json`
- Parity tests from Phase 1 still pass (golden files updated only if intentional algorithm fixes)

## Phase 3 — End-to-end load verification

**Deliverables:** seeds auto-apply in the running app.

### Tasks

1. Add `Tests/ManualLayoutSeedLoadTests.cs`:
   - Generate seeds to a temp file using the console tool (or in-process generator entry point)
   - Feed file to `ManualLayoutManager` with the same key `LayoutEditorController` would compute for that cluster/viewport
   - Assert `LoadLayout(key)` returns non-null `AutoSeed` variant with expected marker count
2. Add `scripts/verify_manual_layout_seeds.ps1` (or extend `verify.ps1`) to:
   - Run seed generator against repo `visual-config.json` and Excel file
   - Assert output JSON deserializes to `ManualLayoutCollection`
   - Assert every group has at least one `AutoSeed` default variant
3. Manual smoke checklist (document in plan, run on Windows):
   - Launch app, zoom to a cluster covered by seeds
   - Confirm layout applies without entering edit mode
   - Confirm status/log indicates loaded variant origin `AutoSeed`

**Acceptance:**

- `.\scripts\verify.ps1` includes or calls seed verification
- Manual smoke checklist passes for at least two seeded clusters

## Phase 4 — Documentation and cleanup

### Tasks

1. Update [MANUAL_LAYOUT_EDITOR.md](../../guides/MANUAL_LAYOUT_EDITOR.md) — seed generation section references console tool, not PowerShell math port.
2. Update [AGENTS.md](../../../AGENTS.md) quick commands if a new script entry is added.
3. Add CHANGELOG entry under `[Unreleased]`.
4. Move this plan to `exec-plans/completed/` when all phases pass.

## Known Drift Log

| Area | Script behavior | App behavior | Status |
|------|-----------------|--------------|--------|
| Layout key hash | Embedded `SeedLayoutMath.GenerateLayoutKey` | `LayoutKeyGenerator.GenerateKey` | To verify in Phase 1 |
| Viewport projection | Embedded `SeedViewport.GetSourceRect` | `ViewportState.GetSourceRect` | To verify in Phase 1 |
| Radial extensions | Embedded `CalculateRadialExtensions` | `RadialExtensionCalculator` | To verify in Phase 1 |

## Risks

| Risk | Mitigation |
|------|------------|
| Console project pulls WPF dependencies | Reference only Models/Utilities/Services; extract shared viewport builder if needed |
| Golden files churn on every config change | Scope goldens to fixed test fixtures, not live `visual-config.json` |
| Cluster viewport construction differs from MainWindow | Extract `ClusterViewportBuilder` helper used by both MainWindow and seed tool |

## Definition of Done

- Seed generator calls the same C# placement code as runtime
- Parity and load tests pass in CI
- Generated `manual-layouts.json` auto-loads for representative clusters without manual key editing
- `scripts/verify.ps1` passes on .NET 6 SDK
