# Large-File Refactoring Assessment

**Date:** 2026-06-08  
**Scope:** Production source files ≥ 1000 lines (line count = `content.count('\n') + 1`, same as `scripts/verify_taste.py`)  
**Harness limit:** 800 lines for `.cs` files (`MAX_CS_LINES` in `verify_taste.py`)

## Conclusion

Two files exceed 1000 lines. Both are refactor candidates; only one currently blocks `verify.ps1`.

| File | Lines | Taste check | Priority |
|------|------:|-------------|----------|
| `MainWindow.xaml.cs` | 2505 | Grandfathered (`FILE_SIZE_GRANDFATHER`) | High — TD-001 |
| `Tools/PinDebugger/Program.cs` | 1051 | **Fails** | Medium — unblocks verify |

Near-threshold watch (not in scope but related):

| File | Lines | Note |
|------|------:|------|
| `scripts/generate_manual_layout_seeds.ps1` | 942 | Embedded C# duplicate of runtime math; replace via [manual-layout-seed-alignment-plan.md](../exec-plans/active/manual-layout-seed-alignment-plan.md) |

Follow-through plan: [refactoring-assessment-followthrough-plan.md](../exec-plans/active/refactoring-assessment-followthrough-plan.md) · Prior assessment: [REFACTORING_ASSESSMENT.md](REFACTORING_ASSESSMENT.md)

---

## 1. `MainWindow.xaml.cs` (2505 lines)

**Severity:** HIGH  
**Debt ID:** TD-001  
**Status:** Partially improved (Phases 1–10 of [refactoring-plan.md](../exec-plans/completed/refactoring-plan.md)); still 3× the 800-line guideline.

### Responsibilities (too many for one class)

`MainWindow` remains the application orchestrator for:

1. Startup (`InitializeAsync`)
2. Content subwindows and thumbnail/didactic windows
3. Marker and cluster lifecycle on the map canvas
4. Viewport zoom animations and navigation stack
5. Radial extension detection, adjustment, and rendering orchestration
6. Composite pin planning, caching, overrides, and reassignment
7. Manual layout edit mode (drag, save, variants UI, replay)
8. Visual config surface properties for marker sizing

Phases 2–10 extracted `AnimateViewportTransition`, `RadialExtensionAdjuster`, `ExtensionLineRenderer`, `LayoutEditorController`, `CompositePinApplicationService`, and service interfaces — but **orchestration and WPF wiring stayed in MainWindow**, so the file grew as composite pins and layout variants landed.

### Region map (current structure)

| Approx. lines | Region / topic | Methods (count) |
|---------------|----------------|-----------------|
| 1–225 | Fields, ctor, service wiring | 2 |
| 226–360 | Layout variant UI handlers | 12 |
| 361–624 | Init, content popups | 6 |
| 625–844 | Marker creation / add to map | 6 |
| 845–1040 | `UpdateMarkerPositions` | 1 (**196 lines**) |
| 1041–1644 | Visibility, zoom in/out, animation | 15 |
| 1645–2023 | Radial extension + composite pins | 18 |
| 2025–2466 | Manual layout editor + drag | 14 |
| 2468–2505 | Master pin image load | 1 |

### Bloated / inefficient hotspots

#### `UpdateMarkerPositions` (~196 lines, 845–1040)

- **God-method:** dense-group detection, extension calculation, adjuster pass, composite callback, normal fallback, and cluster positioning in one method.
- **Duplicated cluster positioning:** identical `SourceToScreen` + `Canvas.SetLeft/Top` block appears **four times** (animation branch, extension branch, normal branch, cluster loop).
- **Logging noise:** `_logger.LogInfo` on every reposition when radial extensions are enabled — hot path during resize and zoom-complete.
- **Mixed abstraction levels:** inline lambdas calling `TryApplyCompositePinMarker` inside renderer API.

**Remediation:** Extract `MarkerPlacementOrchestrator` (Service) with inputs `(viewport, markers, config, extensionRenderer, compositeApplier)`. MainWindow calls one method. Unit-test dense-group vs normal paths without WPF.

#### `ShowZoomedView` (~98 lines, 1249–1346)

- Layout key generation, cache load, zoomed-region cache, marker visibility, manual layout apply, and edit-button visibility in one try/catch.
- **Sequential I/O:** cache miss triggers synchronous region generation on UI thread.

**Remediation:** Split into `PrepareZoomedClusterView(cluster)` returning a small DTO; keep UI mutations in a thin MainWindow method.

#### `OnSaveLayoutButtonClick` (~86 lines, 2082–2168)

- Marker collection, validation UI, assignment enrichment, cache invalidation, override clearing, and status text — mostly delegatable.

**Remediation:** Move save pipeline to `LayoutEditorController.TrySaveCurrentView(...)` accepting a `LayoutSaveContext` (extensions source, enricher, plan cache). MainWindow updates status text only.

#### `ApplyManualLayout` (~99 lines, 2251–2349)

- Cache load, per-marker reprojection, composite vs legacy branch, cache save, override reapply — correct logic but belongs in `CompositePinApplicationService.ApplyLayoutToMarkers(...)`.

**Remediation:** Service method returns `LayoutApplyResult`; MainWindow iterates markers for canvas updates only.

#### `OnMarkerDragMove` (~53 lines, 2383–2436)

- Six `LogDragDebug` calls per mouse move when debug flag enabled — acceptable behind flag, but method could live in `LayoutEditorController` or a `MarkerDragHandler` helper.

#### Constructor field sprawl (lines 28–80)

- 20+ private fields spanning four feature areas. New features keep adding fields instead of facet objects.

**Remediation:** Introduce small state bags, e.g. `CompositePinSessionState`, `LayoutEditorUiState`, injected into handlers.

### Safe split strategy (recommended order)

Prefer **service extraction first** (testable, matches ARCHITECTURE.md), then **partial classes** only for irreducible WPF event wiring.

| Step | Extract to | Moves out of MainWindow | Risk |
|------|------------|-------------------------|------|
| 1 | `Services/MarkerPlacementOrchestrator.cs` | `UpdateMarkerPositions`, `CalculateMarkerSourcePositions`, `CalculateMarkerScreenPositions`, `PositionMarkerNormally`, `ApplyNormalPositioning` | Low — existing services cover calculator/adjuster |
| 2 | Extend `CompositePinApplicationService` | `ApplyManualLayout` loop, cache orchestration, `ApplyRenderPlanToMarker` | Low — already has cache API |
| 3 | `Services/ContentSubwindowCoordinator.cs` | `ShowContentForLocation`, `ShowImageAtIndexAsync`, `CloseActiveSubwindow*` | Medium — window ownership stays in MainWindow |
| 4 | `MainWindow.LayoutEditor.partial.cs` | Variant UI handlers, drag handlers, edit-mode button clicks | Low — partial class, same type |
| 5 | `MainWindow.CompositePins.partial.cs` | `TryApplyCompositePinMarker`, overrides, reassign | Low |
| 6 | `MainWindow.Navigation.partial.cs` | `AnimateZoomToCluster`, `AnimateZoomOut`, `AnimateViewportTransition`, `ShowZoomedView` | Medium — animation state |

**Do not split** until each extraction has a unit test or existing integration coverage. Run `.\scripts\verify.ps1` after each step.

**Partial class rules:**

- One partial per concern; each file ≤ 500 lines target.
- Shared fields stay in `MainWindow.xaml.cs` (or a `MainWindow.Fields.partial.cs`).
- No new business logic in partials — delegate to Services.

### Acceptance (MainWindow)

- `MainWindow.xaml.cs` + partials each ≤ 800 lines, OR MainWindow removed from `FILE_SIZE_GRANDFATHER` in `verify_taste.py` because total orchestration ≤ 800 lines in primary file.
- No regression in manual layout smoke, composite pin render, zoom animation.
- Architecture tests still pass (`LayerDependencyTests`).

---

## 2. `Tools/PinDebugger/Program.cs` (1051 lines)

**Severity:** MEDIUM (blocks `verify.ps1` today)  
**Debt ID:** TD-013 (proposed)  
**Type:** Top-level script (`Program.cs` with file-scoped functions, not a class)

### Responsibilities

Single-file CLI tool with **six modes** sharing bitmap/geometry utilities:

| Mode | Flag | Approx. lines | Purpose |
|------|------|---------------|---------|
| Annotate | (default) | ~120 | Draw calibration dots on pin parts |
| Clean | `--clean` | ~130 | Flood-fill shadow removal |
| Find-join | `--find-join` | ~90 | Suggest `local_join` from axis projection |
| Fit-axis | `--fit-axis` | ~120 | PCA axis + join estimate |
| Measure-shaft | `--measure-shaft` | ~70 | Shaft measurement output |
| Composites | `--composites` | ~380 | Grid preview of shaft+head composites |

Plus shared drawing helpers (`DrawDot`, `DrawCapLine`, `CompLayerTransform`, etc.).

### Bloated / inefficient patterns

#### Duplicated pixel scanning (~80 lines × 3)

`FindJoin`, `FitAxis`, and `MeasureShaft` each:

1. Resolve cleaned vs original PNG path
2. `LockBits` → copy pixel buffer
3. Loop all pixels for alpha threshold
4. Project or accumulate coordinates

**Remediation:** One `ShaftPixelSampler` static class:

```csharp
static ShaftPixelSample ReadOpaquePixels(string imagePath, byte alphaThreshold = 10);
```

#### `KeepConnectedComponent` (~95 lines, 148–243)

- Correct flood-fill; self-contained. Fine as `ShaftConnectedComponentFilter.cs`.

#### `RunComposites` + `RenderComposite` + `Comp*` helpers (~380 lines, 670–1050)

- Largest block; mirrors app composite math for offline preview.
- `CompClipHalf`, `CompClipBand`, matrix helpers are generic geometry — not pin-specific.

**Remediation:** `CompositePreviewRenderer.cs` with public `RenderComposite(...)` and internal clip/transform helpers.

#### Top-level mode dispatch (lines 42–120)

- Long `if/else` chain; acceptable short-term but grows with each new mode.

**Remediation:** `Dictionary<string, Action<PinDebuggerContext>>` mode table in slim `Program.cs`.

#### `Console.WriteLine` throughout

- Acceptable for a CLI tool (not subject to Services/Views console rule). No change required.

### Safe split strategy

Target layout:

```text
Tools/PinDebugger/
  Program.cs                 # ~60 lines — args, mode dispatch, JSON load
  PinDebuggerContext.cs      # paths, flags, shared options record
  ShaftPixelSampler.cs       # bitmap read, opaque pixel enumeration
  ShaftCleaner.cs            # CleanShaft, KeepConnectedComponent
  JoinAnalysis.cs            # FindJoin, FitAxis, MeasureShaft
  Annotator.cs               # AnnotateHead, AnnotateShaft, DrawDot, legend
  CompositePreviewRenderer.cs # RunComposites, RenderComposite, Comp* helpers
```

| Step | Action | Verification |
|------|--------|--------------|
| 1 | Extract `ShaftPixelSampler`; refactor three analysis modes to use it | `dotnet run --project Tools/PinDebugger -- --find-join` on pin_07 |
| 2 | Move composite preview block to `CompositePreviewRenderer.cs` | `--composites` output unchanged (byte compare one PNG) |
| 3 | Move annotate/clean to separate files | Default annotate + `--clean` smoke |
| 4 | Slim `Program.cs` to dispatch only | `.\scripts\verify.ps1` taste check green |

**Risk:** Low — tool is not referenced by app or Tests; no architecture layer rules. Use `partial class` only if converting to a class; prefer separate static classes in same project.

### Acceptance (PinDebugger)

- Every `Tools/PinDebugger/*.cs` file ≤ 800 lines.
- `verify_taste.py` passes without new grandfather entries.
- Existing PinDebugger modes produce identical output for a golden pin (manual spot-check documented in plan).

---

## 3. Cross-cutting recommendations

### Harness alignment

| Item | Action |
|------|--------|
| TD-001 grandfather | Remove `MainWindow.xaml.cs` from `FILE_SIZE_GRANDFATHER` only after Step 1–2 of MainWindow split reduces primary file below 800 lines |
| TD-013 PinDebugger | Add to [tech-debt-tracker.md](../exec-plans/tech-debt-tracker.md); fix before next merge if verify gate is strict |
| Taste check scope | Consider warning tier at 600 lines for Tools/ (optional) |

### What not to do

- **Do not** move PinDebugger logic into `Services/` — it is a dev tool using `System.Drawing`, not part of the WPF app stack.
- **Do not** split MainWindow by copying code without extracting Services first — partial classes alone recreate the god object across files.
- **Do not** delete `scripts/generate_manual_layout_seeds.ps1` until `ManualLayoutSeedGenerator` console exists (seed alignment plan).

### Suggested execution order

1. **PinDebugger split** — small, isolated, unblocks `verify.ps1` (~1 session).
2. **`MarkerPlacementOrchestrator`** — highest MainWindow line reduction (~1–2 sessions).
3. **`CompositePinApplicationService` layout apply** — completes manual-layout/composite orchestration move (~1 session).
4. **MainWindow partial classes** — only for remaining XAML event handlers (~1 session).

---

## Definition of done (this assessment)

- [x] PinDebugger ≤ 800 lines per file; verify taste check green
- [x] MainWindow primary file ≤ 800 lines OR documented phased plan with TD-001 grandfather removal date
- [x] Tech debt tracker lists TD-013 (resolved 2026-06-08)
- [ ] No new files over 1000 lines without an exec-plan row
