---
status: active
owner: agent
started: 2026-06-21
requirements_ref: code-review-2026-06-21T19-48-42
parent_program: composite-pins-program.md
---

# Runtime Tuning & Pin-Render Bug-Fix Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Fix the bugs and robustness gaps found in the 2026-06-21 code review of the runtime tuning panel and pin rendering (drawn + composite). Source of findings: [`../../../code-review-2026-06-21T19-48-42.md`](../../../code-review-2026-06-21T19-48-42.md).

**Architecture:** All fixes are localized to existing `MainWindow.*.partial.cs` orchestration, the `ExtensionLineRenderer`, and the `CompositePinPlacementPolicy`/`PinPartPlacementCalculator` services. No new view controls and no manual-layout JSON schema changes. The drawn-pin drag fix (H14) deliberately stops short of the larger control split tracked by [`drawn-pin-model-separation-plan.md`](drawn-pin-model-separation-plan.md) — it makes the *current* drawn drag behave correctly without introducing new controls, and notes the overlap so the two plans don't collide.

**Tech Stack:** WPF / .NET 6 / C#, existing `VisualConfig`/`VisualConfigService`, `MarkerPlacementOrchestrator`, `ExtensionLineRenderer`, `CompositePinPlacementPolicy`, `PinPartPlacementCalculator`, xUnit source + behavior tests.

---

## Findings index (from the review file)

| ID | Severity | Summary | Fixed in task |
|----|----------|---------|---------------|
| H1  | HIGH | Tuning recreate while zoomed corrupts view state | Task 1 |
| H12 | HIGH | Tuning changes don't replay the active manual layout (composite toggle "desync") | Task 1 |
| H14 | HIGH | Drawn-pin drag slides the whole glyph (head + built-in stub) with no connecting shaft | Task 2 |
| H13 | HIGH | Composite drag "stub": rendered head clamped to `MaxStretchFactor` diverges from drag-guide endpoint | Task 3 |
| M2  | MED | Reload-from-disk bypasses input validation | Task 4 |
| M3  | MED | `ShouldRepositionOnly` reuses a pixel tolerance as a degree tolerance | Task 5 |
| M4  | MED | `UpdateMarkerPositions` can orphan composite guide lines in edit mode | Task 6 |
| L6–L11 | LOW | Dead `CreateLine`, unused `previousState` (L6 already done), redundant checks, broad cache clears, `async void`, unused usings | Task 7 |

**Ordering:** Tasks 1–7 are independent and can land in any order (or as separate commits/PRs); Tasks 8 (docs) and 9 (final verify + move plan) come last. Recommended sequence by value: 1 → 2 → 3 → 4 → 5 → 6 → 7.

---

## Design rules

1. The draggable canvas element stays `LocationMarker`; only its `Content` and `Canvas` position change.
2. No behavior change when neither a manual layout nor composite mode is active — auto-placement must stay byte-for-byte equivalent.
3. Composite pin behavior and the manual-layout JSON schema are unchanged.
4. Tip (origin) positions remain derived from `Location.PixelX/PixelY` (Excel), never editable. Only head/endpoint is editable. (Already true; do not regress.)
5. Each task ends green: `dotnet test` for the touched area, then `.\scripts\verify.ps1` at the end.

---

## Task 1: Replay the active manual layout after tuning changes; unwind/guard zoom state (H12 + H1)

**Files:**
- Modify: `MainWindow.DeveloperTuning.partial.cs`
- Test: `Tests/TuningPanelWiringTests.cs`, new `Tests/TuningReapplyTests.cs`

**Root cause:** `ApplyTuningAsync`'s non-recreate branch ends with a bare `UpdateMarkerPositions()`, which recomputes auto-placement only and never replays an active manual layout. The recreate branch (`RecreateAllMarkersAsync → AddClustersToMap`) replays only the *full-map* layout and ignores `_currentZoomedCluster`, so a recreate while zoomed rebuilds the full-map cluster view against a zoomed viewport.

- [ ] **Step 1: Add a single re-apply helper that both branches use**

> **Important (review correction):** a manual layout is an *overlay*, not a full placement. `LayoutEditorController.CreateLayoutApplications` iterates **only `layout.Markers`**, so `ApplyManualLayout` only touches pins saved in the layout; every other visible pin relies on `UpdateMarkerPositions()` for its position/composite stub. The helper must therefore **always run base auto-placement first, then overlay the layout** — do **not** early-return into `ApplyManualLayout`, or non-layout pins go stale on a tuning toggle. This mirrors the existing idiom (`AddClustersToMap`, `OnSizeChanged`: `UpdateMarkerPositions(); TryApplyFullMapManualLayout();`).

In `MainWindow.DeveloperTuning.partial.cs`:

```csharp
/// <summary>
/// Restores the view to its correct state after a tuning change. Recomputes base
/// auto-placement for ALL pins, then overlays the saved manual layout (which only
/// covers the edited subset). Covers full-map root view AND a currently zoomed cluster.
/// </summary>
private void ReapplyViewAfterTuningChange()
{
    if (_currentZoomedCluster != null)
    {
        // ShowZoomedView already does UpdateMarkerPositions() + ApplyManualLayout()
        // internally, so the cluster layout overlay is preserved. (precedent: OnDeleteLayoutButtonClick)
        ShowZoomedView(_currentZoomedCluster);
        return;
    }

    UpdateMarkerPositions();          // base auto-placement for every visible pin
    TryApplyFullMapManualLayout();    // overlay saved full-map layout (no-op if none / not full-map root)
}
```

- [ ] **Step 2: Use the helper in the non-recreate branch**

In `ApplyTuningAsync`, replace the trailing bare `UpdateMarkerPositions();` (in the `else` of `needsRecreate`) with `ReapplyViewAfterTuningChange();`. Keep the `RestoreBaseMarkerVisuals()` call that precedes it.

- [ ] **Step 3: Guard recreate-class tuning changes while zoomed (H1)**

`RecreateAllMarkersAsync` rebuilds the **full-map cluster view**. Doing that while zoomed would also need to reset the display image (currently the high-res zoomed region), the zoomed-region cache, the viewport, the nav stack, and `BackButton` — duplicating most of `AnimateZoomOut` and easy to get subtly wrong. Recreate-class changes are cluster threshold and marker sizes; **cluster threshold is a full-map concept** and recomputing it while zoomed is meaningless. So the robust fix is to **reject** these changes while zoomed rather than unwind.

`needsRecreate` is computed *before* `_visualConfig` is mutated, so guard there and return cleanly with no partial apply:

```csharp
// after needsRecreate is computed, before mutating _visualConfig
if (needsRecreate && _currentZoomedCluster != null)
{
    DeveloperTuningPanel.SetStatus("Zoom out to apply cluster/marker-size changes.");
    return;
}
```

This composes with Step 2: while zoomed, **non-recreate** changes (composite toggle, variants, stub length, target sizes) still apply live via `ReapplyViewAfterTuningChange → ShowZoomedView`; only the cluster/size recreate is deferred until full map.

> **Alternative (only if live size changes while zoomed are a hard requirement):** unwind to full map inside `RecreateAllMarkersAsync` — reset `_currentZoomedCluster`, clear the nav stack (add `MapNavigationService.ClearAll()`), hide `BackButton`, `ClearFullMapLayoutSession()`, reset the viewport to full map **and** restore the full-map display image, then `UpdateMarkerPositions(); TryApplyFullMapManualLayout()`. Higher risk; mirror `AnimateZoomOut`'s completion exactly and add a manual smoke for the display-image swap. Not recommended for this pass.

- [ ] **Step 4: Tests**

Create `Tests/TuningReapplyTests.cs` with source-guard assertions (no WPF instantiation needed):
- `ApplyTuningAsync` non-recreate branch calls `ReapplyViewAfterTuningChange`, not a bare `UpdateMarkerPositions`.
- `ReapplyViewAfterTuningChange` references `UpdateMarkerPositions`, `TryApplyFullMapManualLayout`, and `ShowZoomedView` (and does **not** early-return into `ApplyManualLayout` at the full map — base auto-placement must run first).
- `ApplyTuningAsync` rejects a recreate-class change while `_currentZoomedCluster != null` (guard present, returns before mutating `_visualConfig`).

```powershell
dotnet test Tests\InteractiveWorldMap.Tests.csproj --filter "FullyQualifiedName~TuningReapplyTests|FullyQualifiedName~TuningPanelWiringTests" --no-restore
```

- [ ] **Step 5: Manual smoke (composite toggle no longer desyncs)**
  1. `Debug.EnableTuningPanel = true`, `PinParts.UseCompositeRendering = true`. Launch.
  2. At full map with a saved full-map layout, F12 → toggle composite **off** → Apply. Confirm pins switch to drawn **immediately at the saved positions** (no Edit Layout needed).
  3. Toggle composite **on** → Apply. Confirm composites reappear at saved positions immediately.
  4. Zoom into a cluster with a saved cluster layout; repeat the toggle. Confirm the cluster layout is preserved.
  5. While zoomed, change `LocationMarkerSize` → Apply (recreate-class). Confirm it is rejected with the "Zoom out to apply…" status and the view is unchanged (no half-zoomed state). Zoom out, Apply again, confirm it now takes effect.

---

## Task 2: Drawn-pin drag must keep the tip fixed and show the connecting shaft (H14)

**Files:**
- Modify: `MainWindow.LayoutEditor.partial.cs` (`OnMarkerDragMove` legacy/drawn branch, `OnMarkerDragStart`)
- Modify: `Views/ExtensionLineRenderer.cs` if a "create line if missing" entry point is needed
- Test: new `Tests/DrawnPinDragTests.cs`

**Root cause:** In the legacy drag branch, a drawn pin with no existing extension line gets no line created during drag and its built-in shaft is never hidden, so the whole glyph (head + stub shaft) slides under the cursor untethered from the fixed map tip. Centering also uses `LocationMarkerSize` (16) instead of the pin's connection point.

- [ ] **Step 1: Compute the fixed tip and the pin connection point up front**

In the drawn branch of `OnMarkerDragMove`, before positioning:

```csharp
var viewport = MapDisplay.CurrentViewport;
if (viewport == null) return;
var cw = MapDisplay.ActualWidth;
var ch = MapDisplay.ActualHeight;

// Fixed tip from Excel data — never moves during drag.
var tipScreen = viewport.SourceToScreen(
    _draggedMarker.Location.PixelX, _draggedMarker.Location.PixelY, cw, ch);

// Clamp the head (cursor) to canvas bounds.
var headScreen = new Point(
    Math.Max(0, Math.Min(currentPosition.X, MapDisplay.Markers.ActualWidth)),
    Math.Max(0, Math.Min(currentPosition.Y, MapDisplay.Markers.ActualHeight)));
```

- [ ] **Step 2: Ensure the extension-line shaft exists from the fixed tip to the cursor**

```csharp
if (!_extensionLineRenderer.HasLine(_draggedMarker))
    _extensionLineRenderer.AddLine(_draggedMarker, tipScreen, headScreen);   // start == Excel tip
else
    _extensionLineRenderer.MoveLineEndpoint(_draggedMarker, headScreen);     // keeps X1,Y1 (tip) fixed
```

> The line must be *created* with `tipScreen` as its start so the shaft anchors to the Excel tip, not the drag-start point. `MoveLineEndpoint` then only moves the endpoint.

- [ ] **Step 3: Anchor the head on the endpoint by its connection point**

Replace the `markerSize/2` centering with `ExtensionLineRenderer.AnchorExtendedMarker` — for a `PinMarker` it already (a) hides the built-in shaft, (b) sets z-index 2000, and (c) anchors by `GetConnectionPoint()`. So no separate `SetShaftVisible(false)` is needed:

```csharp
_extensionLineRenderer.AnchorExtendedMarker(_draggedMarker, headScreen); // hides built-in shaft + anchors head
_overrideStore.RecordEndpoints(_draggedMarker.Location.Name, tipScreen, headScreen); // parity with composite drag
```

Remove the now-dead `newX/newY`/`markerSize`/bounds block and its `LogDragDebug` lines (or keep one concise debug line using `tipScreen`/`headScreen`).

> **Edge:** `OnMarkerDragStart` calls `SetLineZIndex(marker, 1999)`, which no-ops for a non-extended pin that has no line yet. When Step 2 creates the line on first move it gets the default z-index (core 1000 / outline 999), which still sits under the marker (2000) — acceptable. If the guide must read as "lifted" during drag, call `SetLineZIndex(_draggedMarker, 1999)` right after `AddLine`.

- [ ] **Step 4: Tests**

`Tests/DrawnPinDragTests.cs` (source guards + any pure-geometry helper):
- The drawn branch of `OnMarkerDragMove` calls `AddLine`/`AnchorExtendedMarker` and no longer positions via `LocationMarkerSize / 2`.
- It records endpoints with the tip from `SourceToScreen` (string-guard `SourceToScreen` present in the branch).

```powershell
dotnet test Tests\InteractiveWorldMap.Tests.csproj --filter "FullyQualifiedName~DrawnPinDragTests" --no-restore
```

- [ ] **Step 5: Manual smoke (drawn mode)**
  1. `PinParts.UseCompositeRendering = false`. Launch, enter full-map Edit Layout.
  2. Drag a standalone (non-extended) pin. **During** the drag the head should follow the cursor while a single shaft connects back to the fixed map tip — no free-floating stub.
  3. Save + exit; confirm the result matches what was shown during drag.

> **Coordination:** this overlaps [`drawn-pin-model-separation-plan.md`](drawn-pin-model-separation-plan.md). This task is the minimal in-place fix; if the model-separation plan lands first, port these semantics onto `ManualLayoutPinMarker` instead of `SetShaftVisible(false)`.

---

## Task 3: Reconcile the composite drag "stub" with the guide endpoint (H13)

**Files:**
- Modify: `MainWindow.LayoutEditor.partial.cs` (`OnMarkerDragMove` composite branch)
- Reference: `Services/PinPartPlacementCalculator.cs` (`MinStretchFactor`/`MaxStretchFactor` clamp), `Models/PinPartConfig.cs`
- Test: new `Tests/CompositeDragStretchTests.cs`

**Root cause:** In `NearestFit` mode the shaft stretch is clamped to `[MinStretchFactor, MaxStretchFactor]` (0.75–1.35). When the head is dragged farther than `1.35 × nativeLength` of the best shaft, the rendered composite head stops at the clamp while the drag-guide line continues to the cursor → visible "stub + line" divergence.

- [ ] **Step 1: Decide the reconciliation policy** (pick one; default = A)
  - **A (recommended, low-risk):** Treat the *rendered composite head* as the source of truth during drag. Drive the guide line endpoint to the **clamped** head position (read it back from the rebuilt `RenderPlan.HeadCenterLocal` / tip + applied length) instead of the raw cursor, so the line and head never diverge. The saved endpoint then matches what is shown.
  - **B:** Allow temporary over-stretch during drag (bypass the clamp while `_draggedMarker != null`) so the head tracks the cursor; re-clamp on drop. Higher fidelity but changes rendered proportions mid-drag.

- [ ] **Step 2 (Policy A): After rebuilding, move the guide line to the rendered head**

In the composite branch, after `ApplyCompositePinToMarker(_draggedMarker, originalPos, mousePos)`:

```csharp
if (_draggedMarker.Content is CompositePinMarker cpm && cpm.RenderPlan != null)
{
    var renderedHead = new Point(
        Canvas.GetLeft(_draggedMarker) + cpm.RenderPlan.HeadCenterLocal.X,
        Canvas.GetTop(_draggedMarker)  + cpm.RenderPlan.HeadCenterLocal.Y);

    if (_extensionLineRenderer.HasLine(_draggedMarker))
        _extensionLineRenderer.MoveLineEndpoint(_draggedMarker, renderedHead);

    _overrideStore.RecordEndpoints(_draggedMarker.Location.Name, originalPos, renderedHead);
}
```

Remove the existing `MoveLineEndpoint(_draggedMarker, mousePos)` / `RecordEndpoints(..., mousePos)` calls that used the raw cursor.

- [ ] **Step 3: Surface the clamp (optional, recommended)**

Expose `MaxStretchFactor` (and/or a read-only "clamped" indicator from `IsStretchClamped`) so long pins aren't silently truncated. Minimal version: log once per drag when `result.IsStretchClamped` so QA can correlate. (Full tuning-panel field is optional follow-up — note in TO_DO if deferred.)

- [ ] **Step 4: Tests**

`Tests/CompositeDragStretchTests.cs` (pure):
- For a target longer than `MaxStretchFactor × nativeLength`, `PinPartPlacementCalculator.CalculatePlacement` returns `IsStretchClamped == true` and `AppliedStretchFactor == MaxStretchFactor`.
- A small helper that computes "rendered head from plan + tip" equals the clamped distance along the target angle (guards Policy A's reconciliation math).

```powershell
dotnet test Tests\InteractiveWorldMap.Tests.csproj --filter "FullyQualifiedName~CompositeDragStretchTests" --no-restore
```

- [ ] **Step 5: Manual smoke (composite mode)** — drag a head far; confirm the guide line stops with the rendered head (Policy A) rather than running past it to the cursor; on drop the pin is unchanged.

---

## Task 4: Validate config values on reload-from-disk (M2)

**Files:**
- Modify: `MainWindow.DeveloperTuning.partial.cs` (`OnReloadTuningFromDisk` / `CreateTuningArgs`)
- Modify: `Views/DeveloperTuningPanel.xaml.cs` — expose a static validator, **or** add a `MainWindow`-side guard
- Test: `Tests/VisualConfigServiceTests.cs` (extend), new `Tests/TuningReloadValidationTests.cs`

**Root cause:** The reload path builds `TuningPanelEventArgs` directly from the loaded file and applies it without the positivity/finite checks the UI Apply path enforces.

- [ ] **Step 1: Add a numeric validator** `TryValidate(TuningPanelEventArgs, out string error)`. Note the existing `DeveloperTuningPanel.TryReadPositive/TryReadNonNegative` operate on **text fields**; the reload path already has a built `TuningPanelEventArgs` (doubles), so this validator re-applies the same *rules* to the numeric values: `ClusterThreshold > 0`, `LocationMarkerSize > 0`, `ClusterMarkerSize > 0`, `StubLength/TargetHeadRadiusPx/TargetShaftHalfWidthPx >= 0`, and all finite (reject `NaN`/`Infinity`). Reuse it from the UI path too if convenient, but the UI path's string parsing stays as-is.

> **Layer constraint (review correction):** `Tests/Architecture/LayerDependencyTests.cs` forbids `Views → Services` and `Views → Utilities`. The validator is consumed by **both** `DeveloperTuningPanel` (a View) and `MainWindow`, so it must **not** live in `Services/` or `Utilities/`. Compliant placements:
> 1. **(Recommended)** Keep the validation as a `public static` method on the View itself (`DeveloperTuningPanel.TryValidate(...)`); `MainWindow` calls it (`MainWindow → Views` is permitted). The panel keeps owning its UI rules; zero duplication.
> 2. Put `TuningValueValidator` in `Models/` (both Views and MainWindow may depend on Models).
> 3. Duplicate the guard on the `MainWindow` side and leave the View's validation isolated.
>
> Do not place it in `Services/`/`Utilities/`. After this change, run `dotnet test --filter FullyQualifiedName~LayerDependencyTests` to confirm no layer violation.

- [ ] **Step 2: Guard the reload path**

(Snippet shows the call site; the validator name depends on the Step 1 placement — `DeveloperTuningPanel.TryValidate` for Option 1, `TuningValueValidator.TryValidate` for Option 2.)

```csharp
var fresh = _configService.Load(_configPath);
var args = CreateTuningArgs(fresh);
if (!DeveloperTuningPanel.TryValidate(args, out var error))   // Option 1 (recommended)
{
    _logger.LogWarning($"[Tuning] Reloaded config rejected: {error}");
    DeveloperTuningPanel.SetStatus($"Reload rejected: {error}");
    return;
}
await ApplyTuningAsync(args);
```

- [ ] **Step 3: Tests** — a config with `LocationMarkerSize = 0` / negative `ClusterMarkerSize` / `NaN` is rejected by `TryValidate`; a valid one passes.

```powershell
dotnet test Tests\InteractiveWorldMap.Tests.csproj --filter "FullyQualifiedName~TuningReloadValidationTests|FullyQualifiedName~VisualConfigServiceTests" --no-restore
```

---

## Task 5: Split angle vs length tolerance in `ShouldRepositionOnly` (M3)

**Files:**
- Modify: `Services/CompositePinPlacementPolicy.cs`
- Test: `Tests/CompositePinPlacementPolicyTests.cs`

- [ ] **Step 1: Separate parameters**

```csharp
public static bool ShouldRepositionOnly(
    CompositePinRenderPlan? existingPlan,
    PinPlacementTarget newTarget,
    string? preferredPairId = null,
    string? preferredHeadSourcePath = null,
    double toleranceDeg = 0.5,
    double tolerancePx = 0.5)
{
    ...
    if (AngleDifferenceDeg(angleDeg, existingPlan.TargetAngleDeg) > toleranceDeg) return false;
    if (Math.Abs(lengthPx - existingPlan.TargetLengthPx) > tolerancePx) return false;
    ...
}
```

- [ ] **Step 2: Update callers** (none pass the tolerance today, so defaults preserve behavior — verify with grep).

```powershell
rg -n "ShouldRepositionOnly" --type cs
```

- [ ] **Step 3: Tests** — add cases proving an angle change within `tolerancePx` but beyond `toleranceDeg` now forces a rebuild (and vice-versa).

```powershell
dotnet test Tests\InteractiveWorldMap.Tests.csproj --filter "FullyQualifiedName~CompositePinPlacementPolicyTests" --no-restore
```

---

## Task 6: Fix edit-mode composite guide-line lifecycle (M4)

**Files:**
- Modify: `Views/ExtensionLineRenderer.cs` (`Apply`) and/or `MainWindow.CompositePins.partial.cs` (`TryApplyCompositePinMarker`)
- Test: `Tests/CompositePinEditModeTests.cs`

**Root cause (precise):** In `ExtensionLineRenderer.Apply`, when the composite applier returns true the code runs:

```csharp
_markerToLine.Remove(marker);
_markerToPinLines.Remove(marker);   // untracks, but does NOT remove the Line from _lines/_canvas
```

In **edit mode** the applier (`TryApplyCompositePinMarker`) has *just added* a guide line for that marker (`if (ok && _layoutEditor.IsEditMode) AddLine(...)`). So these two lines untrack the freshly-added guide line, leaving the `Line` visible on the canvas but unreachable via `_markerToLine`/`_markerToPinLines` — it can no longer be moved (`MoveLineEndpoint` misses it) or cleaned until the next full `Clear()`.

> **Key tension:** a naive "also remove the visual" fix is wrong — in edit mode that line is the **intended drag guide** and must stay. The `Remove` calls are leftover defensive cleanup that is *redundant* in non-edit mode (UpdateMarkerPositions already called `Clear()` before the `WithExtensions` loop, so no stale mapping exists) and *actively harmful* in edit mode (it untracks the wanted guide).

- [ ] **Step 1: Delete the untracking in `Apply`'s composite-success branch.** Remove the `_markerToLine.Remove(marker); _markerToPinLines.Remove(marker);` pair. Rationale:
  - Non-edit mode: `UpdateMarkerPositions` calls `_extensionLineRenderer.Clear()` before the `WithExtensions` loop (when not animating), so the dictionaries are already empty — the removes are no-ops.
  - Edit mode: the applier deliberately added a tracked guide line; leaving it tracked is correct (it stays movable and gets cleaned by the next `Clear()`).

- [ ] **Step 2: Confirm no stale-mapping path depends on the removed lines.** Grep for other callers and the animating path:

```powershell
rg -n "tryCompositePinApplier|_markerToLine.Remove|_markerToPinLines.Remove" --type cs
```

If a stale mapping *can* survive into `Apply` without a preceding `Clear()` (e.g. a future animating + WithExtensions combination), prefer a single `RemoveLineFor(marker)` helper (removes visual **and** tracking) **guarded to run only when the applier did not just add a guide line** — i.e. only in non-edit mode. Do not unconditionally remove the visual.

- [ ] **Step 3: Tests** (`Tests/CompositePinEditModeTests.cs`) — after an edit-mode `Apply` over a composite + `WithExtensions` group: every `Line` in `_canvas.Children` is still tracked (`MarkerMappingCount` accounts for it; `TryGetLineEndpoint` succeeds for each guide-line marker); no `Line` remains in `_canvas` without a `_markerToLine` entry.

```powershell
dotnet test Tests\InteractiveWorldMap.Tests.csproj --filter "FullyQualifiedName~CompositePinEditModeTests" --no-restore
```

---

## Task 7: Low-severity cleanup (L6–L11)

**Files:** `MainWindow.Navigation.partial.cs`, `Views/ExtensionLineRenderer.cs`, `MainWindow.DeveloperTuning.partial.cs`, all `MainWindow.*.partial.cs`

- [ ] **L7 — Delete dead `CreateLine`** in `ExtensionLineRenderer.cs` (unused; `AddLine` uses `CreatePinLinePair`/`CreateDebugLine` directly). Confirm with `rg -n "CreateLine\b" --type cs`.
- [x] **L6 — `previousState` in `AnimateZoomOut`:** Decision = **Option A** (single-level zoom is the intended design). Added a one-line comment documenting that the navigation stack is a depth gate (`CanGoBack`), not a viewport history, so the `ZoomState` payload is intentionally unused; zoom-out always returns to full map. `ZoomState`-payload removal deferred until/unless the "intermediate zoom levels" idea (TO_DO "User ideas") is committed to. No behavior change.
- [ ] **L8 — Redundant coupled checks** in `compositePlanChanged`: drop one of `oldPinPartsEnabled != e.UseComposite` / `oldUseComposite != e.UseComposite` (both driven by `e.UseComposite`).
- [ ] **L9 — Narrow cache invalidation:** don't `_compositePinPlanCache.ClearAll()` for `ShowDebugOverlay`/`UsePrerasterize` toggles (render-time only). Only clear for asset-variant/stub/target/geometry changes. Re-render via the existing replay path instead.
- [ ] **L10 — `async void` handlers:** ensure all awaited work in `OnApplyTuning`/`OnReloadTuningFromDisk` stays inside `try`; no early `await` before the guard.
- [ ] **L11 — Unused usings:** run IDE "remove unused usings" / `dotnet format` over the `MainWindow.*.partial.cs` set.

```powershell
dotnet build InteractiveWorldMap.sln
dotnet test Tests\InteractiveWorldMap.Tests.csproj --no-restore
```

---

## Task 8: Documentation & plan state

**Files:** `docs/TO_DO.md`, `CHANGELOG.md`, `docs/exec-plans/active/README.md`, `docs/exec-plans/active/composite-pins-program.md`, `code-review-2026-06-21T19-48-42.md`

- [ ] **Step 1:** Tick resolved findings in `code-review-2026-06-21T19-48-42.md` (or add a "Resolved" note per ID with the commit/task).
- [ ] **Step 2:** Update the `docs/TO_DO.md` "Developer tooling" bullet to point at this plan and mark sub-items as they land.
- [ ] **Step 3:** Add a `CHANGELOG.md` `[Unreleased]` entry:

```markdown
- **Tuning & pin-render fixes:** Runtime tuning changes now replay the active manual layout immediately (composite on/off no longer requires Edit Layout); recreate while zoomed unwinds cleanly; drawn-pin drag keeps the tip fixed with a single connecting shaft; composite drag head no longer diverges from its guide line; reload-from-disk validates values.
```

- [ ] **Step 4:** Register this plan in `active/README.md` (Active plans table) and in `composite-pins-program.md`.
- [ ] **Step 5:** Doc checks:

```powershell
py -3 scripts\verify_doc_links.py
py -3 scripts\doc_gardening.py
```

---

## Task 9: Final verification

- [ ] **Step 1:** Full repo verification:

```powershell
.\scripts\verify.ps1
```

- [ ] **Step 2:** Combined manual smoke (one pass through all four HIGH scenarios): composite toggle at full map + zoomed; recreate while zoomed; drawn-pin drag; composite far-drag.
- [ ] **Step 3:** Move this plan to `../completed/` and update `active/README.md` + `TO_DO.md` per the maintenance rules.

---

## Acceptance criteria

- [ ] Toggling composite on/off in the tuning panel updates the view immediately at the saved manual-layout positions — full-map **and** zoomed-cluster — with no need to enter Edit Layout (H12).
- [ ] A recreate-class tuning change (size/threshold) while zoomed is rejected with a clear status and leaves the view unchanged (no half-zoomed/contradictory state); it applies after zooming out (H1).
- [ ] Dragging a drawn pin keeps the tip fixed at its Excel location and shows a single connecting shaft during the drag; the during-drag view matches the saved result (H14).
- [ ] Dragging a composite pin head keeps the guide line and rendered head coincident; no stub/line divergence (H13).
- [ ] Reload-from-disk rejects invalid values with a status message instead of applying them (M2).
- [ ] `ShouldRepositionOnly` uses independent angle/length tolerances; existing reposition behavior preserved at defaults (M3).
- [ ] No orphaned guide `Line`s remain after edit-mode composite placement (M4).
- [ ] Dead/redundant code removed; `.\scripts\verify.ps1` passes (L6–L11).

## Risks

| Risk | Mitigation |
|------|------------|
| Re-apply helper double-applies layout and flickers | Single entry point (`ReapplyViewAfterTuningChange`); base auto-placement then overlay — the same `UpdateMarkerPositions(); TryApplyFullMapManualLayout()` idiom used by `AddClustersToMap`/`OnSizeChanged` (note: do NOT copy `ExitEditMode`'s early-return-into-`ApplyManualLayout`, which is only safe there because edit-mode placement doesn't change pin content type) |
| Recreate while zoomed leaves a contradictory view (H1) | Guard rejects recreate-class changes while zoomed (Step 3); composes with the live non-recreate path so zoomed tuning still works |
| Drawn-drag fix collides with `drawn-pin-model-separation-plan.md` | Keep it a minimal in-place change; cross-link both plans; port to `ManualLayoutPinMarker` if separation lands first |
| Composite Policy A changes the "felt" drag (head lags cursor at extreme lengths) | Document the clamp; optionally expose `MaxStretchFactor`/clamp indicator (Task 3 Step 3) so the limit is visible, not surprising |
| Narrowed cache invalidation (L9) misses a real plan-affecting flag | Keep clear for asset-variant/stub/target/geometry; add a test that overlay/prerasterize toggles do NOT clear the plan cache while variant changes DO |
