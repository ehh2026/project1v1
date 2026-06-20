# Review — composite-pins-unzoomed-plan.md

**Reviewer:** opencode (model: minimax-m3)
**Reviewed:** 2026-06-19
**Plan file:** `docs/exec-plans/active/composite-pins-unzoomed-plan.md`
**Plan status claimed:** Phases 0–6 complete; Phase 7 in progress

---

## Summary

The plan is structurally sound, mostly accurate against the current code, and well-aligned with the parent program dashboard. The core Phase 4–6 work appears to have been implemented as described. The remaining Phase 7 work is the actual focus of this review.

The biggest issues are:

1. **Phase 7 Section B (reposition-only) is the real open work**, but several supporting claims in the plan's "done" sections either no longer match the code or are stated ambiguously — a future agent will trip over them.
2. **A claimed helper file does not exist** (`Services/CompositePinSegmentPolicy.cs`).
3. **The `IsAnimating` semantics changed** (`_mode` is now an `InteractionMode` enum) but the plan still phrases the policy around the boolean form in places. Verify this hasn't drifted the plan's intended behavior.
4. **The legacy plan (`remove-pins-jpg-legacy-path-plan.md`) cross-references this plan and claims the work is "delegated" — those delegated checkboxes are stale relative to current Phase 7 status.**
5. **A few smaller correctness/consistency nits** in tasks, file paths, and a Phase 4 task description that no longer matches the implemented code.

Findings below are grouped: critical → important → minor.

---

## 1. Critical

### 1.1 `CompositePinSegmentPolicy.cs` does not exist on disk

The plan repeatedly says "Create or modify `Services/CompositePinSegmentPolicy.cs`" (Phase 7 files table) and "Add pure `Services/` helper to detect unchanged target segment" (Phase 7 task 6). Verified:

```
Test-Path Services/CompositePinSegmentPolicy.cs  →  False
```

`Test-Path Services/CompositePinPlacementPolicy.cs` is `True`, so the recommended extension target already exists. The plan should:

- Either (preferred) explicitly say **"extend `Services/CompositePinPlacementPolicy.cs`"** and drop the `Create or modify` ambiguity, or
- Explicitly state "create a new `Services/CompositePinSegmentPolicy.cs` for this responsibility".

Right now a future agent will not know which path to take. Given the plan's own modularity guardrail (segment comparison stays in `Services/`, pure policy) the natural fit is to extend the existing `CompositePinPlacementPolicy` — recommend that.

### 1.2 Phase 7 "Core persistence (done)" claims do not fully match the code

The plan claims these tasks are `[x]` complete:

- Task 1: Replace unconditional `RestoreBaseMarkerVisuals()` with `PrepareMarkerVisualsForPlacementUpdate()`.
- Task 2: Per-marker drawn fallback on composite failure.
- Task 3: Tip-anchor reposition in `CompositePinPlacementPolicy` + tests.
- Task 4: Source-contract tests in `CompositePinZoomPersistenceTests`.
- Task 5: Stub length/direction invariant across viewports.

These are all present in the code. **However**, the description in task 3 ("Extract tip-anchor reposition to `CompositePinPlacementPolicy` + behavior tests") is accurate but a follow-up contract is missing: nothing in the plan currently prevents a future refactor from rebuilding `CompositePinMarker` on every placement. Phase 7 is the right place to encode that as a behavior test (Phase 7 task 10 is the right slot, but it's still `[ ]`). Consider moving the behavior test earlier so a "core persistence done" reviewer can actually verify non-rebuild on pan/resize.

### 1.3 Plan does not account for `_mode` being an enum, not a bool

`IsAnimating => _mode == InteractionMode.Animating` exists (MainWindow.xaml.cs:113), and `_mode` is an `InteractionMode` enum (Normal / Animating / Editing). The plan writes:

> "During `IsAnimating`: prefer reposition-only for normal placements; skip full normal-path rebuild loop (current early return) but ensure `ApplyIndividualPlacements` + reposition-only covers visible composites."

That's still correct in intent. But Phase 7 task 9 says "**During** `IsAnimating`: reposition-only for unchanged segments" — and `ApplyCompositePinsToNormalPlacements` (MainWindow.CompositePins.partial.cs:313) already returns early when `IsAnimating` is true:

```csharp
if (!CanUseCompositePins() || IsAnimating)
    return;
```

So during animation, **no** composite pin placement runs at all (not even reposition-only). The smoothness requirement in the plan ("when the logical target segment is unchanged, updates must reposition only — … required for pan, resize, **and animation frames**") therefore **cannot be met** by the current code path. If the requirement is for animation to reposition-only as well, Phase 7 task 9 needs a code change to `ApplyCompositePinsToNormalPlacements` so it does **not** early-return on `IsAnimating`. If the plan really means "settled state after animation must show composite" (which is what section "Animation bar" says), then task 9 is fine — but the smoothness bullet's "and animation frames" wording is then misleading. **Pick one and make it explicit.** Recommend:

- Delete the "and animation frames" tail of the smoothness bullet, **or**
- Convert task 9 to require removing the `IsAnimating` early-return from `ApplyCompositePinsToNormalPlacements` so it reposition-only-during-animation.

### 1.4 The `remove-pins-jpg-legacy-path-plan.md` cross-delegation is stale

That plan's Phase 3 acceptance includes:

```
- [ ] Full-map view: all individual locations show composite pins (stub shaft). → Delegated to unzoomed Phase 7 manual smoke #1
- [ ] Panning/zooming does not cause composite markers to flicker back to drawn pins. → Owned by unzoomed Phase 7
```

Those checkboxes are still `[ ]` in the legacy plan, but the unzoomed plan claims "Phase 5 complete: user confirmed debug-overlay geometry … on 2026-06-12" and "Phase 6 complete for core/manual smoke … accepted 2026-06-12." The unzoomed plan's own "Definition of Done" still lists "Delegated items in remove-pins-jpg-legacy-path-plan.md marked done" as a DoD item (line 562). So the cross-link is consistent in intent but the legacy plan's checkboxes have not been updated to reflect Phase 5/6 acceptance. The DoD check at line 562 should either be ticked, or the legacy plan's `[ ]` items should be flipped — pick one source of truth.

---

## 2. Important

### 2.1 Phase 4 task 3 no longer matches the implementation

The plan (Phase 4 task 3) says:

> `originalPos` = fixed from `viewport.SourceToScreen(location.PixelX, location.PixelY)`.
> `mousePos` = current cursor position.
> Rebuild `PinPlacementTarget` with `StartScreen = originalPos`, `EndScreen = mousePos`.
> Call `ApplyCompositePinToMarker(marker, originalPos, mousePos)` to re-render the pin at the new angle/length.

The actual code (MainWindow.LayoutEditor.partial.cs:658–686) uses bounds-clamped `mousePos` (via `Math.Max(0, Math.Min(...))`) and only constrains it to the **inner canvas** — not the wrapper. That is fine but the task description says "Ensure `newX/newY` bounds logic still works (composite pin `Canvas.Left` is tip-based, not center-based)" without specifying the bounds contract. For a future agent diffing this, the divergence is benign but worth a one-line note: **bounds are clamped to `MapDisplay.Markers.ActualWidth` × `ActualHeight` regardless of whether the marker is composite or legacy**.

### 2.2 `ApplyCompositePinToMarker` builds a fake `ViewportState` during drag

In `OnMarkerDragMove` (line 674) the call goes to `ApplyCompositePinToMarker(marker, originalPos, mousePos)`, which constructs a `ViewportState()` empty and passes `containerWidth: 0`, `containerHeight: 0` to `_compositePinTargetBuilder.Build` (MainWindow.CompositePins.partial.cs:165–179). That works today only because the drag path uses the **extended** branch of the target builder (it supplies a `RadialExtension` with explicit screen points) — the empty viewport and zero container size are not used. This is a **fragile invariant** that Phase 7 will step on. Recommend one of:

- Refactor `ApplyCompositePinToMarker` to skip the target builder and call `ApplyCompositePinTargetToMarker` directly with a hand-built `PinPlacementTarget` (preferred — drag already knows both screen points).
- Or document in Phase 7 that "drag path uses the extension target branch; do not introduce a viewport-dependent code path here."

### 2.3 "Stub only — extension line is not drawn" vs. "Add line as drag guide in edit mode"

The Phase 0 table says: "Extension lines — Do not draw radial extension lines for stub-only markers." Phase 4 says: "In `ApplyManualLayout`, when … composite pin applied and `_layoutEditor.IsEditMode`, still add an extension line via `_extensionLineRenderer.AddLine(marker, ...)` so `CollectCurrentExtensions` has a reliable endpoint and the user sees a drag target."

These are not actually contradictory (drag-only line, never a normal render line), but a reader skimming Phase 0 → Phase 4 will see a conflict. Recommend adding a one-line note at the Phase 0 table:

> "Edit mode is the only exception that adds an extension line for stub-only markers; see Phase 4 task 2."

### 2.4 `OnMarkerDragStart` highlights at 0.7 opacity and never restores

MainWindow.LayoutEditor.partial.cs:622–639 sets `marker.Opacity = 0.7` on drag start. The plan does not mention opacity restore on drag end. Grepping for the corresponding restore is worth checking during Phase 7 closure (the `OnMarkerDragEnd` snippet at line 748+ wasn't fully read here, so this may already be handled). If the plan is going to be authoritative on drag behavior, add a task: "verify `marker.Opacity` is restored on `OnMarkerDragEnd` and on edit mode exit." This is a behavior gap the plan should own.

### 2.5 Phase 6 decision table — "Remove `_currentZoomedCluster == null` as a hard blocker"

The plan's Phase 6 design table (line 303) says "Remove `_currentZoomedCluster == null` as a hard blocker when in full-map edit." Yet `OnEditLayoutButtonClick` (line 264) still has the structure:

```csharp
if (_currentZoomedCluster == null) { ... TrySetFullMapLayoutKey ... }
```

That is the *correct* gate, not a removal. The decision should be re-read as "do not make the **save** path require `_currentZoomedCluster != null`." Wording in the plan is technically right if "hard blocker" is scoped to save/delete/load paths, but a literal reading suggests deleting the null check entirely. Recommend rewording to "Save/delete/load must not require `_currentZoomedCluster != null` when full-map edit session is active."

### 2.6 Phase 7 manual smoke checklist #2 vs. plan's own debug log signal

Step 2 of the manual smoke says "Pan map / resize window — no flash to drawn pin; stubs stay composite; no visible head/shaft swap." The optional log check immediately after says:

> `app.log` should not spam `"leaving drawn pin fallback"` for healthy markers.

A grep for that exact phrase in the source would be useful. The plan should include that grep as a verification step, or drop the log check if the string is fictitious. (Worth confirming in the repo before finalizing the plan — I did not search.)

### 2.7 Phase 7 acceptance "settled state" is ambiguous

The plan's Definition of Done and acceptance criteria use "settled state" repeatedly without defining it. The "Animation bar" section implicitly defines it as "`IsAnimating` is false" but that is not used in the acceptance checklist. A future agent running the manual smoke will not know when to start the clock. Recommend adding to the Phase 7 manual smoke section a single line:

> **Settled state:** `IsAnimating == false` AND the most recent `UpdateMarkerPositions` call has returned.

---

## 3. Minor

### 3.1 `ToString` ambiguity in Phase 7 task 6

Phase 7 task 6: "Add pure `Services/` helper to detect unchanged target segment vs existing composite plan/endpoints." Inputs include "built `PinPlacementTarget` (or start/end screen) + existing `CompositePinRenderPlan` / recorded endpoints." The "or" is a choice the implementer must make — recommend specifying which one. Given that `PinPlacementTarget` is the natural output of `CompositePinTargetBuilder` and `CompositePinRenderPlan` is the natural cache key, the helper signature is most useful as:

```
bool IsUnchangedSegment(LocationMarker marker, PinPlacementTarget newTarget, double tolerancePx = 0.5)
```

…and it should consult `marker.Content as CompositePinMarker` → `RenderPlan` internally.

### 3.2 `DefaultStubLengthPixels = 24` is hard-coded in two places

The plan says it's in `PinPartConfig` / `visual-config.json`. Verified in `Tests/CompositePinEditModeTests.cs:97` that the test sets it to 24 inline. If a real `visual-config.json` default is supposed to be 24, Phase 7 should call that out as a check (`grep -n DefaultStubLengthPixels visual-config.json`). If the JSON default is different, the tests will drift. (Worth a one-line task in the closure section.)

### 3.3 Phase 7 risk: "Clustering config change shifts which singles are visible"

This risk is in Phase 6 and not restated in Phase 7. Phase 7's invariant ("same appearance at full map and when zoomed to that single location") is **stronger** than Phase 6's variant replay tolerance ("missing names keep auto stubs"). If a clustering config change shifts visibility while a user is in edit mode, the user's saved manual layout will not match the live stub placements. Add a Phase 7 risk row: "Clustering config change mid-edit (or between save and replay) desyncs stub-vs-saved." Mitigation: same as Phase 6 (load matches by name), but Phase 7's reposition-only path makes the desync visually silent and harder to detect — flag it.

### 3.4 Phase 6 link to `MANUAL_LAYOUT_EDITOR.md`

Phase 6 tasks (line 370) say "Modify `docs/guides/MANUAL_LAYOUT_EDITOR.md` — full-map edit flow, key rules, no-zoom-while-editing." That guide update is not on the DoD list (line 553+). The DoD is otherwise tight; the doc update should be on it.

### 3.5 Header date `started: 2026-06-07` vs first phase date `2026-06-09`

The front-matter `started:` is 2026-06-07; the first dated event in the plan body is 2026-06-09 (Phase 0). The two-day gap is benign but, given this is a long-running plan with a fresh in-progress phase (7) starting 2026-06-19, consider adding a `last_updated:` field in the front matter to make search-by-date in exec plans directory listings easier. (Cosmetic.)

### 3.6 Phase 7 task 4 says "or document why extension path always full-reapplies"

The plan does not yet state why. The current code (`MainWindow.xaml.cs:471–483` extension path) calls a callback that always builds a new composite marker. If that is the right answer, document it in Phase 7. If the answer is "we should also reposition-only on the extension path when start/end are unchanged," say so and add a test. Without the resolution, a future implementer cannot tell whether the unchecked box represents "done" or "deferred with reason."

### 3.7 Cross-plan pointer: `supersedes_partial` in legacy plan

The legacy plan's front matter says `supersedes_partial: composite-pins-unzoomed-plan.md`. With Phase 7 nearly closed, that field's meaning is fine, but the unzoomed plan does not reciprocate — and once Phase 7 closes, the unzoomed plan should move to `completed/` per the program dashboard rules (line 56 of `composite-pins-program.md`). Add a note to the unzoomed plan's Definition of Done: "Move to `docs/exec-plans/completed/` and leave a one-line stub in the program dashboard." That step is not currently in the DoD.

---

## 4. What I would change first

If I were the next agent on this plan, the priority order to address findings is:

1. **1.1** — Pin down whether `CompositePinSegmentPolicy` is a new file or an extension. (Decide once; this unblocks the whole Section B implementation.)
2. **1.3** — Resolve the `IsAnimating` semantics mismatch (drop "and animation frames" **or** remove the early return).
3. **1.4** — Decide on the cross-plan source of truth (update the legacy plan's `[ ]` boxes to `[x]`, or formally mark the unzoomed DoD as deferred until those are flipped).
4. **2.2** — Refactor `ApplyCompositePinToMarker` to skip the fake `ViewportState`/zero container size in the drag path.
5. **2.5** — Reword the Phase 6 design table so a future agent does not literally delete the `_currentZoomedCluster == null` null check.
6. **2.4, 2.6, 2.7** — Small additions: opacity restore verification, real log-grep string, settled-state definition.

The rest (minor) can ride along with Phase 7 closure.

---

## 5. Files consulted

- `docs/exec-plans/active/composite-pins-unzoomed-plan.md` (subject)
- `docs/exec-plans/active/composite-pins-program.md` (dashboard)
- `docs/exec-plans/active/remove-pins-jpg-legacy-path-plan.md` (cross-delegation source)
- `docs/TO_DO.md` (status reconciliation)
- `MainWindow.xaml.cs`, `MainWindow.CompositePins.partial.cs`, `MainWindow.LayoutEditor.partial.cs`, `MainWindow.Navigation.partial.cs` (code)
- `Services/*.cs` directory listing (segment policy check)
- `Tests/CompositePinEditModeTests.cs`, `Tests/CompositePinZoomPersistenceTests.cs`, `Tests/CompositePinTargetBuilderTests.cs`, `Tests/LayoutKeyGeneratorTests.cs`, `Tests/ManualLayoutManagerTests.cs` (verification)
