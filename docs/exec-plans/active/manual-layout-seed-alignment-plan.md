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

## Phase 5 — Persistence robustness (storage location + brittle keys)

**Why here:** seed/runtime key parity (Phases 1–3) only helps if saved layouts actually survive and resolve. Two persistence weaknesses surfaced 2026-06-23 and are folded in here.

### 5a. Stable per-user storage — DONE (2026-06-23)
- Relative `ManualLayoutEditor.LayoutStoragePath` previously resolved to `BaseDirectory` (`bin/…`), so a `dotnet clean`/rebuild or redeploy discarded user-saved layouts, and runtime edits never reached the source seed file.
- `MainWindow.ResolveLayoutStoragePath` now treats a relative path as the bundled (app-folder) seed and points the writable store at `%AppData%/InteractiveWorldMap/manual-layouts.json`, seeded once from the bundled file. Rooted paths are honored as-is; falls back to the bundled path if AppData is unavailable.

### 5b. Don't crash on broken/old/invalidated layouts — DONE (2026-06-23)
- `ManualLayoutManager.LoadLayoutCollection` now wraps deserialize/normalize in try/catch: a corrupt or schema-incompatible file is backed up to `*.corrupt`, logged, and treated as an empty set instead of throwing. Test: `ManualLayoutManagerTests.LoadLayout_WhenFileIsCorrupt_DoesNotThrow_AndBacksUpBadFile`.

### 5c. Brittle keys — IN PROGRESS (normalized-coordinate design, 2026-06-23)

Layout keys embed canvas size, zoom, viewport center/size, and radial config (`Services/LayoutKeyGenerator.cs`), so a window-size or config change orphans saved layouts (they exist but never resolve via exact-match `TryLoad`).

**Findings (what's already built):**
- **Cluster keys already tolerate size/center/config drift.** `LoadLayout` already falls back to `FindCompatibleGroup` → `AreKeysCompatible`, which (for cluster keys) only compares the location hash + zoom (within 0.1). So a resize/config change on a *cluster* layout already resolves. No new code needed beyond a regression test.
- **The re-projection engine already exists.** `CompositePinApplicationService.BuildApplyInstructions` always re-projects the pin base from the location's source coords (`viewport.SourceToScreen`) and re-projects the extended position from `ManualLayoutMarker.SourceExtendedX/Y` when present. The apply path is already size-independent — **seeds populate `SourceExtendedX/Y`; user saves do not.**

**Two remaining gaps → chosen approach: normalized (source-space) coordinates.**

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
2. [ ] Size-independent full-map key + compatibility rule (`LayoutKeyGenerator`).
3. [ ] Populate `SourceExtendedX/Y` on user save (`RadialExtension`, `CollectCurrentExtensions`, `FromRadialExtension`).
4. [ ] Tests: full-map key is constant; two different-size full-map keys are compatible; a full-map layout saved at one size loads at another and re-projects; genuinely incompatible (cluster vs full-map, different hash) keys do not collide.

**Acceptance:** a saved full-map layout reloads after a window-resize and lands on the correct map positions; cluster layouts already survived; corrupt/stale files never crash; user layouts survive a rebuild.

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
