---
status: active
owner: agent
started: 2026-06-23
---

# Oversize-File Refactor Plan (>800-line taste limit)

**Generated:** 2026-06-23 23:24 · **Corrected:** 2026-06-23 23:30

## Finding

Two active source files exceed the 800-line taste limit and are **currently failing
`scripts/verify_taste.py`, which keeps `scripts/verify.ps1` from going green**. Both are
already tracked in [docs/TO_DO.md](../../TO_DO.md) under "High priority" (lines 68–69):

| File | Taste lines | Over by |
|------|-------------|---------|
| `MainWindow.xaml.cs` | 872 | 72 |
| `MainWindow.LayoutEditor.partial.cs` | 801 | 1 |

**Counting note:** the taste check uses `text.count("\n") + 1` and fails on `> 800`
(strict). Count with that method — `[System.IO.File]::ReadAllLines(path).Count` or
`git grep -c ''` — **not** PowerShell `Measure-Object -Line`, which drops blank lines and
under-reports (it reported 790/692 for these files; the real figures are 872/801).

`backups/` (gitignored) also contains two >800 files; those are out of scope for the rule
and excluded here.

## Goal

Get both files comfortably under 800 (target ≤ ~750 for headroom) using the partial-class
pattern already established for `MainWindow` (`Navigation`, `CompositePins`, `Content`,
`TipCap`, `DeveloperTuning`, `LayoutEditor`). Pure mechanical moves — no behavior, signature,
or public-API changes. Partials share class scope, so field *declarations* stay put and moved
methods reference them directly.

## Phase 1 — `MainWindow.xaml.cs` (872 → ~620)

Create `MainWindow.MarkerPlacement.partial.cs` (`public partial class MainWindow`) and move
the marker-placement engine out of the core file:

- [ ] `UpdateMarkerPositions()`
- [ ] `BuildIndividualMarkerIndex()`
- [ ] `ApplyIndividualPlacements(...)`
- [ ] `ApplyClusterPlacements(...)`
- [ ] `ClearAllMarkers()`
- [ ] `ShowOnlyClusterMarkers()`
- [ ] `ShowOnlyIndividualMarkers(LocationCluster)`

Leave in core: constructor, `InitializeAsync`, `AddClustersToMap`, `AddIndividualMarker`,
`AddClusterMarker`, `ResolveLayoutStoragePath`, `HandleOutsideClick`, all `On*` handlers.

Checklist:
- [ ] Create partial; cut (don't copy) the 7 methods above
- [ ] Keep all field declarations in `MainWindow.xaml.cs`
- [ ] `dotnet build InteractiveWorldMap.sln` succeeds
- [ ] `MainWindow.xaml.cs` ≤ ~750 taste-lines; new partial ≤ 800
- [ ] `dotnet test Tests/InteractiveWorldMap.Tests.csproj` green
- [ ] CHANGELOG.md updated; commit

## Phase 2 — `MainWindow.LayoutEditor.partial.cs` (801 → ~660)

Only 1 line over, but extract a cohesive cluster for real headroom. Preferred seam: the
marker-drag handlers (lines ~660–800, ~140 lines) → `MainWindow.LayoutEditorDrag.partial.cs`:

- [ ] `OnMarkerDragStart(...)`
- [ ] `OnMarkerDragMove(...)`
- [ ] `LogDragDebug(...)`
- [ ] `OnMarkerDragEnd(...)`

(Alternative seam if preferred: the variant-management cluster, `PopulateVariantPicker` …
`OnDeleteVariantButtonClick`, lines ~142–220, → `MainWindow.LayoutVariants.partial.cs`.)

Checklist:
- [ ] Create partial; cut the chosen cluster
- [ ] `dotnet build` succeeds; both files ≤ ~750 taste-lines
- [ ] `dotnet test` green
- [ ] CHANGELOG.md updated; commit

## Phase 3 — Close out

- [ ] `.\scripts\verify.ps1` passes end-to-end (taste now green)
- [ ] Tick TO_DO.md lines 68–69 as done (or remove)
- [ ] Confirm no other in-scope `.cs` is within ~50 lines of the limit
      (`ManualLayoutManager.cs` is next at 688 — fine for now)

## Verification gate

Merge only after `.\scripts\verify.ps1` is green and no in-scope `.cs` exceeds 800 taste-lines.

## Out of scope

- `backups/` snapshots (gitignored, rule-exempt).
- The 550–688-line files — compliant; leave unless a feature forces growth.
- Any logic, signature, or public-API change.
