---
status: active
owner: agent
started: 2026-08-18
---

# Layout Editor Safety & Developer Tooling Plan

**Goal:** Stop the manual layout editor from destroying user work — via silent bulk delete
(P1) and via saves that zero out pin geometry (P0) — then fix the developer-tools toggle
that never runs, add a root-level config guide, and close out the one known skipped-test bug.

**Architecture:** All fixes stay inside existing layers. Geometry/persistence changes land in
`Services/` and `Views/`; button wiring stays in the `MainWindow` partials; the two scripts are
repo-root/`scripts/` only. No new layer edges — `LayerDependencyTests.cs` must stay green.

**Tech Stack:** .NET 6, WPF, xUnit, PowerShell 5.1, `.\scripts\verify.ps1`.

---

**User-facing companion:**
[../../reference/layout-editor-known-issues.md](../../reference/layout-editor-known-issues.md) —
the same issues in plain language, for users rather than agents. **Keep it updated as each phase
lands**; it carries a "Last updated" date that must move with it.

---

## Progress log

| Date | Change | Verification |
|------|--------|--------------|
| 2026-08-18 | Plan created (`c950876`). Issues 1–6 diagnosed against merged `main` @ `9487fc0`. | Doc link check |
| 2026-08-18 | Read-only audit of every `CurrentLayoutKey` reader/writer. Found two defects not in the original plan (0.9, 0.10) and **corrected** the Phase 1.6 approach — a blanket `IsEditMode` guard on `UpdateMarkerPositions` would break two legitimate mid-edit callers. | n/a |
| 2026-08-19 | **Phase 0 core landed** (`ae527b1`): 0.3, 0.4, 0.5, 0.9. Layout-key derivation extracted to `MainWindow.LayoutEditorKeys.partial.cs` (the editor partial had exceeded the 800-line limit). | `.\scripts\verify.ps1` **PASSED**, all 11 steps. 880 passed / 2 known skips. New tests confirmed failing before the fix by reverting `LayoutEditorController.cs`. |
| 2026-08-19 | **Phase 1 landed**: 1.1–1.8. Endpoint resolution made explicit, saves refuse unusable geometry, `OnSizeChanged` guarded, rolling `.bak` before every write. Marker geometry extracted to `MainWindow.LayoutEditorGeometry.partial.cs`. | `.\scripts\verify.ps1` **PASSED**, all 11 steps. 888 passed / 2 known skips. |
| 2026-08-19 | **Manual smoke S2 FAILED against `b1fa379`** — user saved variant `taipei1` in the Hong Kong / Taipei cluster and all pins collapsed to stubs. **Root cause: the Phase 1 guards were added to `OnSaveLayoutButtonClick`, but Save As used `CollectCurrentExtensions`, a second unguarded copy of the same collection logic.** Fixed in Phase 1b below. | Reproduced by user against a build containing Phase 0+1 |
| 2026-08-19 | **Phase 1b landed** (`81e6ced`): collapsed both save routes into one guarded `TryCollectCurrentExtensions`; removed the duplicate inline collection and the now-redundant second scope check. Added `IsCollapsedLayout` backstop. | `.\scripts\verify.ps1` **PASSED**, all 11 steps. 893 passed / 2 known skips. |
| 2026-08-19 | **Phase 1c landed** (`83f716d`): cluster layouts no longer replay as stubs. Root cause was the replay path, not saving. | `.\scripts\verify.ps1` **PASSED**. 894 passed / 2 known skips. Regression test measures the 0.46px collapse without the fix. |
| 2026-08-19 | **Manual smoke S5 PASSED** against `83f716d` — the originally reported failure no longer reproduces. | User confirmation in the running app |
| 2026-08-19 | **Phase 0.6 landed** (`58abd6f`): layout precedence by origin rather than scope, so a hand-made zoomed layout is displayed instead of being saved and ignored. | `.\scripts\verify.ps1` **PASSED**. 899 passed / 2 known skips. |
| 2026-08-19 | **Qodo review addressed** (`3db2514`): three real defects — `HasManualLayout` masked by a selected seed; the `OnSizeChanged` guard leaving stale endpoints that a later save would mis-project; the collapse backstop refusing a deliberate all-anchor drag. Plus layering/size cleanups and the `LayoutEditorControllerTests` split. | `.\scripts\verify.ps1` **PASSED**, all 11 steps. 901 passed / 2 known skips. Masking fix verified failing against the old implementation. |

**Durable fix for the whole 0.x class — tracked in [docs/TO_DO.md](../../TO_DO.md) under High
priority:** replace ambient `CurrentLayoutKey` state with an **immutable edit-session context**
captured once on editor entry (scope, viewport, expected key), which every save and delete reads
from. Every defect in Phase 0, 1b and the two Qodo rounds traces back to that ambient state, and the
guards now holding them closed are defence in depth rather than a fix at source — each round of
guards has produced its own edge case. Subsumes 0.7 and 0.10. Recommended independently by the Qodo
review of PR #13. Too large for this PR; do it before adding further behaviour to the editor.

**Carried forward:** 0.7 and 0.10 remain open — the remaining `CurrentLayoutKey` writers should
funnel through one guarded method, and `TryLoad` still mutates active-variant state from any key it
is handed (0.10 is partly mitigated: `HasManualLayout` is side-effect free). Phase 1c leaves an open
question about what a saved head offset means — map-anchored position versus constant screen
length; the fallback reconciles the conflicting tests but the semantics deserve a deliberate
decision, adjacent to 6.9. **0.6 is done** (`58abd6f`) and no longer carried.

**Smoke coverage:** S5 passed. S1, S3, S4, S6, S7, S8, S9, S10 not yet run. **S8 and S10 are the
priority** — both assert that a *valid* save is not wrongly refused, which is the failure mode the
1b.4 collapse backstop can introduce and the one automated tests approximate least well.

---

## Current State

Branched from `main` @ `9487fc0` (PR #7 + dependabot bumps merged). Issues were reproduced or traced
against merged `main`; the status column is kept current as phases land.

| # | Issue | Severity | Status |
|---|-------|----------|--------|
| 0 | Stale `CurrentLayoutKey` — editor edits/saves the wrong scope's layout | **P0 — data loss** | **Fixed** (0.3–0.6, 0.9); 0.7 and 0.10 open as hardening |
| 1 | Saving a layout sometimes rewrites every pin as a short vertical stub | **P0 — data loss** | **Fixed** (1.1–1.8, 1b, 1c); confirmed in the app by smoke S5 |
| 2 | "Delete and Recalculate" deletes *all* saved variants for the key | **P1 — data loss** | **Done** (2.1–2.5) |
| 3 | `toggle-dev-tools.ps1` cannot be run from cmd.exe / Explorer | P2 | **Done** (3.1–3.4) |
| 4 | No root-level guide to which config files to edit | P3 | **Done** (4.1–4.4) |
| 5 | `CalculateMaxLength` 20px floor lets heads leave the canvas | P2 | **Done** (5.1-5.2) |
| 6 | Layout scoping is undocumented and invisible in the edit panel | P2 | Partly — scoping documented; editor panel still does not show it (6.1) |

---

## Phase 0 — P0: Stale `CurrentLayoutKey` — the editor edits the wrong layout

**Added 2026-08-18** after a user reported seeing the same variant (`layout1`) in the dropdown from
every view. Reproduced by inspection; this is now the **primary** suspect for the stub bug and
supersedes the render-race theory as the leading cause.

### Diagnosis

`OnEditLayoutButtonClick` (`MainWindow.LayoutEditor.partial.cs:313-324`) only derives a key for the
full-map case:

```csharp
if (_currentZoomedCluster == null)
{
    if (!TrySetFullMapLayoutKey(editSession: true)) { … return; }
}
else
{
    ClearFullMapLayoutSession();   // ← never sets a cluster key
}
```

Entering the editor while zoomed reuses whatever `CurrentLayoutKey` happens to hold. Two paths keep
that stale value pointing at the full-map layout:

- `TryLoadFullMapManualLayoutForAnimation` (`MainWindow.Navigation.partial.cs:445`) sets
  `SetLayoutKey(GenerateCurrentFullMapGroupKey())` on **every** zoom animation, before knowing
  whether a full-map layout loads.
- `MainWindow.Navigation.partial.cs:233-245` — for a single-location cluster whose location appears
  in a saved full-map layout, `preferFullMapLayout = true` and the cluster-key branch is skipped.

**Symptom 1 (reported):** after saving `layout1` on the full map, the variant dropdown lists it in
every cluster, because the dropdown is faithfully listing `fullmap`'s variants.

**Symptom 2 (very likely the stub bug):** saving while zoomed with a stale `fullmap` key writes the
cluster's handful of markers into the **full-map** layout. `ApplyLayout`
(`Services/ManualLayoutManager.cs:184`) matches by `LocationName`, so every location not in that
cluster loses its manual data and falls back to default placement — short vertical stubs. Requires
no window resize, is intermittent, and presents as global damage. Matches the report better than
the `Clear()` race.

Root cause: `CurrentLayoutKey` is ambient mutable state written from six call sites, and the editor
never re-derives it on entry.

### Steps

- [x] 0.1 Behavior tests for key derivation — `LayoutKeyGeneratorTests`:
      `DeriveEditSessionKey_WithZoomedCluster_ReturnsClusterKeyNotFullMap`,
      `DeriveEditSessionKey_DifferentClusters_ProduceDifferentKeys`, plus the two full-map cases.
      Source guard for the MainWindow wiring in `Tests/LayoutEditorKeyDerivationTests.cs`.
- [x] 0.2 `TrySave_AfterKeyChange_DoesNotWriteIntoPreviousKeysVariant` — verified failing before the
      fix (saved into variant `layout-one-…` under the *new* key), passing after.
- [x] 0.3 Added `LayoutKeyGenerator.DeriveEditSessionKey(...)` — a pure, unit-testable derivation —
      and `OnEditLayoutButtonClick` now calls it in the zoomed branch instead of inheriting the key.
- [x] 0.4 `CurrentLayoutKeyMatchesView()` guards `OnSaveLayoutButtonClick`; a mismatch aborts with
      `✗ SAVE ABORTED — WRONG LAYOUT` and an error log rather than writing.
- [x] 0.5 `TryLoadFullMapManualLayoutForAnimation` no longer calls `SetLayoutKey`.
      `ApplyManualLayoutDuringAnimation` still sets it when a layout is actually applied.
- [x] 0.9 **(new, found during the audit)** `SetLayoutKey` cleared the active-variant identity only
      when the key was null. A change to a *different* non-null key left the previous scope's
      `ActiveVariantId`/`Origin`/`DisplayName` in place, and `TrySave` targets its variant from
      those fields — so a save wrote into a variant belonging to another layout. Now cleared on
      every key change; same key still preserves. Covered by
      `SetLayoutKey_ChangingKey_ClearsActiveVariantIdentity` and
      `SetLayoutKey_SameKey_PreservesActiveVariantIdentity`.
- [x] 0.6 **Done 2026-08-19 — layout precedence is now by origin, not scope.**
      Reported by the user (Ohio pin): with 0.3 in place the editor always derived the *cluster*
      key while the display always preferred the *full-map* layout for a single-location zoom. So a
      zoomed layout could be saved and then never shown — zooming in kept the unzoomed appearance
      until Edit Layout was clicked, which loaded the cluster key directly. The two paths disagreed
      about which layout was in effect, and 0.3 is what exposed it.

      **Rule, decided with the user:** a layout the user deliberately made wins, most specific
      first — Manual zoomed layout → Manual full-map layout → auto seeds. Scope alone was the wrong
      test: every cluster has a seed, and the original branch existed precisely so a seed could not
      override hand-made full-map work. Origin preserves that protection while honoring intent.

      **"Full-map layout containing the location" means it holds a saved marker record for that
      location — not that the location falls inside the map area.** `FullMapLayoutContainsLocation`
      is `layout.Markers.Any(m => string.Equals(m.LocationName, name, StringComparison.Ordinal))`.
      The distinction matters because a full-map layout covers only a *subset* of locations: saving
      captures visible markers only, and most individual pins are hidden inside cluster markers when
      zoomed out. Observed in live data — the `unzoomed1` full-map variant holds 6 markers against
      38 locations in the demo content. If no record exists for the pin, precedence falls through to
      seeds / auto-placement, which is the intended outcome.

      **Sharp edge:** the match is ordinal and exact, so renaming a location (or a stray space or
      case change) silently orphans its saved entry. It would present to a user as "my arrangement
      vanished". Worth a guard or a diagnostic if location renaming ever becomes a supported flow.

      Implemented as `LayoutEditorController.HasManualLayout` (deliberately side-effect free, unlike
      `TryLoad` — see 0.10) plus `HasManualLayoutForZoomedView`, gating **both**
      `TryApplyFullMapLayoutForZoomedSingle` and `ShowZoomedView`'s `preferFullMapLayout` so the two
      paths cannot disagree again.

      **Consequence to surface in the UI (feeds 6.1):** a pin can now legitimately have two saved
      layouts — one unzoomed, one zoomed. The editor panel must say which scope is in effect, or
      "why did my change not show up" becomes ambiguous in a new way.
- [ ] 0.7 Narrow the writers. Six call sites mutate `CurrentLayoutKey`. Partially addressed (0.5
      removed one, 0.3/0.4 make the editor independent of it); the remaining writers should funnel
      through one guarded method.
- [ ] 0.10 **(new, found during the audit)** `TryLoad(key)` mutates `ActiveVariantId`/`Origin`/
      `DisplayName` without checking `key == CurrentLayoutKey`. Three probe-loads call it with
      locally computed keys (`Navigation:169`, `Navigation:237`,
      `TryLoadFullMapManualLayoutForAnimation`), desyncing variant identity from the current key.
      Either make probe loads side-effect-free or require the key to match.
- [x] 0.8 **Manual smoke S5 passed 2026-08-19 against `83f716d`** — save, load another variant,
      reload: the arrangement redraws correctly, no stubs. This is the first confirmation against the
      originally reported failure. Note the true cause turned out to be Phase 1c (replay), not the
      render race; the Phase 0 and 1 fixes remain correct and independently tested.

**Exit:** the editor always edits the layout for the view actually on screen, and a save can never
write into a different scope's group.

---

## Phase 1 — P0: Saves that flatten every pin to a stub

> **Reprioritized 2026-08-18:** Phase 0's stale key is the more likely cause of the reported stubs
> — the user confirms they never resize the window while editing, which was this theory's trigger.
> The hole below is still real and worth closing, but treat it as the secondary cause.

### Diagnosis

`LayoutEditorController.BuildExtensions` (`Services/LayoutEditorController.cs:226`) derives angle
and length solely from the delta between the two points it is handed:

```csharp
double dx = markerCenter.X - originalScreen.X;
double dy = markerCenter.Y - originalScreen.Y;
double angle = Math.Atan2(dx, -dy) * (180.0 / Math.PI);
```

When `markerCenter == originalScreen` the result is length 0 / angle 0 — a short vertical stub.
There is no guard for the degenerate case and no guard for `NaN`.

`markerCenter` comes from `GetMarkerEndpoint` (`MainWindow.LayoutEditor.partial.cs:265`), which
prefers `_extensionLineRenderer.TryGetLineEndpoint(marker, out _)` and otherwise walks a fallback
chain ending at `Canvas.GetLeft(marker) + markerSize/2` — approximately the marker's own anchor,
i.e. approximately `originalScreen`.

`TryGetLineEndpoint` (`Views/ExtensionLineRenderer.cs:323`) reads only `_markerToLine`, and
`ExtensionLineRenderer.Clear()` (line 66) empties that dictionary for all markers at once. So any
re-render that lands between the clear and the save makes the lookup miss for **every** marker
simultaneously — matching the observed "all pins at once, sometimes" behavior. `TrySave` then
persists the zeroed geometry and `_planApplicationService.InvalidateGroup` drops the cached plans,
so the next render rebuilds from the ruined file. The damage is written to disk, not just displayed.

Ruled out: pin line *pairs* do register in `_markerToLine` (`Views/ExtensionLineRenderer.cs:225`),
so an endpoint/start asymmetry is **not** the cause.

### Steps

- [x] 1.1 `BuildExtensions_ZeroLengthDelta_ProducesVerticalStub` documents the degenerate output.
      **Finding:** the angle is **180°, not 0°** — `Math.Atan2(0, -0.0)` is π because negating zero
      gives negative zero. That is precisely the reported symptom: every pin a short stub pointing
      the same direction. **Design correction:** a zero-length extension is *not* itself an error —
      a pin with no radial extension legitimately sits on its anchor. Rejecting on zero length
      would block valid saves. The real signal is whether the endpoint could be *resolved*, which
      is what 1.5 now tracks.
- [x] 1.2 `FindNonFiniteMarkers` rejects `NaN`/infinity coordinates (`Canvas.GetLeft` returns `NaN`
      when a position was never set). Covered by three tests including the legitimate zero-length
      case, which must *not* be reported.
- [x] 1.3 Superseded by 1.5 — resolution is tracked where the endpoint is read, not inferred from
      geometry afterwards. This is the more direct signal and avoids the false positives in 1.1.
- [x] 1.4 The save path refuses when any marker is unresolved or non-finite, showing
      `✗ SAVE ABORTED — GEOMETRY UNAVAILABLE, RETRY` and logging the affected names.
- [x] 1.5 `GetMarkerEndpoint` split into `TryGetMarkerEndpoint(marker, out Point)`, returning false
      only for the last-resort marker-anchor guess. The four authoritative sources return true.
      Extracted to `MainWindow.LayoutEditorGeometry.partial.cs` (800-line limit).
- [ ] 1.6 **Close the race at its source — but narrowly.** `UpdateMarkerPositions()`
      (`MainWindow.MarkerPlacement.partial.cs:38`) calls `_extensionLineRenderer.Clear()` guarded
      only by `if (!IsAnimating)`.
      **Correction (2026-08-18 audit):** do *not* blanket-guard `UpdateMarkerPositions` with
      `IsEditMode`. Of its 13 call sites, three are reachable mid-edit and two of those are
      legitimate — `OnDeleteVariantButtonClick` (`MainWindow.LayoutEditor.partial.cs:225`) and
      `OnEditLayoutButtonClick` (`:351`, called right after `EnterEditMode()`). A blanket guard
      would break both. The only unguarded illegitimate caller is `OnSizeChanged`
      (`MainWindow.xaml.cs:713`), which has no `IsEditMode` check at all — that is where the guard
      belongs. Every other caller is already blocked during edit by the navigation guards.
      **Done 2026-08-19:** `OnSizeChanged` returns early while `IsEditMode`, refreshing only the
      Edit Layout button visibility.
- [x] 1.7 Pre-save backup added in `ManualLayoutManager.SaveLayoutCollection`.
      **Deviation from plan:** a single rolling `<layoutfile>.bak` beside the layout file, rather
      than timestamped copies in `temp/`. Reasons: `ManualLayoutManager` only knows its own file
      path, so reaching into a repo-relative `temp/` would bake a layer assumption into a service
      that tests point at arbitrary directories; and a rolling copy is bounded, needs no cleanup
      policy, and covers the case that matters — the save that just ran. A failed backup never
      blocks the save.
- [x] 1.8 `SaveLayout_KeepsBackupOfPreviousFile`; existing round-trip coverage
      (`BuildExtensions_AngleNorthUp_RoundTrips`) still passes unchanged.

**Exit:** cannot produce an all-stub layout file through the save path; a save that would do so is
refused with a visible message.

### Phase 1b — the guard was on only one of two save routes

**Added 2026-08-19 after manual smoke S2 failed.** The Phase 1 checks went into
`OnSaveLayoutButtonClick`. "Save As" used `CollectCurrentExtensions`, a **second copy** of the same
marker-collection logic, and was never protected. Saving a named variant still collapsed the layout.

The lesson is about shape, not about a missed line: two copies of a capture path meant guarding one
provably did not guard the other, and nothing in the tests noticed the asymmetry.

- [x] 1b.1 Collapse both routes into a single `TryCollectCurrentExtensions` in
      `MainWindow.LayoutEditorGeometry.partial.cs` that checks scope **and** geometry before
      building anything. Delete `CollectCurrentExtensions`.
- [x] 1b.2 Remove the now-duplicated scope check from `OnSaveLayoutButtonClick`; one guard, one
      place. Comments at both sites warn against re-inlining.
- [x] 1b.3 `EverySavePath_CapturesMarkersThroughTheGuardedRoute` asserts *both* handlers call the
      shared route and that `MainWindow.LayoutEditor.partial.cs` no longer calls
      `BuildExtensions` directly — so a third unguarded save path fails the build.
- [x] 1b.4 **`IsCollapsedLayout` backstop** (answers "does allowing zero-length extensions open the
      door to bugs?"): yes, partly. An endpoint that *resolves* but equals the anchor still yields a
      180°/zero-length stub, which the resolution guard cannot see.
      **Corrected after user review — first attempt was wrong.** It refused when *every* marker sat
      on its anchor, which false-positives on a real case: a zoomed view whose pins are too far
      apart to form a dense group is **legitimately all default stubs**, and that save would have
      been blocked. The guard now judges only markers the renderer would actually extend, obtained
      from `RadialExtensionCalculator.DetectDenseGroups` — the same rule placement uses, not a
      second approximation of it. Sparse views produce an empty expected set and can never trip it.

**Terminology worth keeping straight** (it caused confusion while implementing): a *zero-length
extension* does not render as a head sitting on the pin tip with no shaft. The pin graphic has its
own shaft — `Math.Max(pinConfig.ShaftLength, 12.0)` in `Views/AutoStubPinMarker.xaml.cs:37`, 24px by
default — so it draws as a short vertical stub with the head above the dot. The stub appearance *is*
the zero-length case; there is no separate "collapsed to nothing" visual. Consequently one or two
stubs among angled pins is normal, and only the all-dense-members-at-once case is a defect.

---

## Phase 1c — P0: cluster layouts replay as stubs (the actual reported bug)

**Added 2026-08-19 after manual smoke.** User saved `taipei2`, which looked correct, then loaded
another variant and returned to `taipei2` — all pins were stubs. **This was never a save bug.**

### Evidence

The saved file (`%AppData%\InteractiveWorldMap\manual-layouts.demo.json`) was intact: `taipei2`
carried real angles (75.7, 69.5, -61.3, 35.0) and valid `SourceExtendedX/Y`. The app log showed the
layout resolving and applying correctly, then:

```
[ApplyManualLayout] Applying layout with 5 markers
  Applied layout for: Chang Dai-chien   … all 5
  Extension lines: 0
  Marker-to-line mappings: 0
    Marker 'Chang Dai-chien' has NO line   … all 5
```

All markers applied, **zero extension lines created**.

**Why the save appeared fine at first:** nothing re-renders from the file immediately after saving —
what you see is still the dragged state. Switching variants is the first replay from saved data, so
the re-render path had been broken all along.

### Root cause

`ProjectSourceExtendedPosition` re-projects the head offset at the **full-map reference scale**
(added 2026-06-23 so a whole-map layout's shaft length stays constant across zoom). A cluster layout
is authored zoomed in: a 59-screen-pixel drag at zoom 55 is ~1.07 **source** pixels. At full-map fit
scale (~0.19×) that becomes **0.46px** — under `ManualLayoutPlacementPolicy.ExtensionLineThreshold`
(5px) — so `RequiresExtensionLine` is false and every marker falls through to
`ApplyAutoStubInstruction`. Measured, not estimated: the regression test reports exactly 0.46px
without the fix.

### Fix

- [x] 1c.1 First attempt — use the full-map reference only for full-map keys — **was wrong**: it
      reintroduced the 2026-06-23 regression (`BuildApplyInstructions_SourceExtendedHead_ShaftLengthIsZoomInvariant`).
      The two goals genuinely conflict on this path.
- [x] 1c.2 Landed fix: keep the full-map reference, but when the projection collapses below the
      extension threshold **and** the marker was saved with a real `LineLength`, fall back to the
      saved angle/length screen geometry. Full-map layouts never hit this branch — their source
      offsets are large enough to survive projection — so zoom-invariance is preserved.
- [x] 1c.3 `BuildApplyInstructions_ClusterLayout_DoesNotShrinkOffsetToStub` reproduces the real
      Taipei numbers and fails without the fix (0.46px).
- [x] 1c.4 Corrected `BuildApplyInstructions_WithSourceExtendedCoords_PreservesFullMapOffset`, which
      asserted full-map reference behavior while passing a **cluster** key (`"group-a"`) — it was
      encoding the bug. Now uses `"fullmap"`.

**Open follow-up:** three tests on this path encoded three different intents about what a saved head
offset means (map-anchored position vs constant screen length). The fallback reconciles them but the
underlying semantics deserve a deliberate decision — see 6.9 on removing `s{W}x{H}` from cluster
keys, which is adjacent.

---

## Phase 2 — P1: "Delete and Recalculate" wipes every saved variant

### Diagnosis

`ManualLayoutManager.DeleteLayout` (`Services/ManualLayoutManager.cs:115`):

```csharp
var removedCount = group.Variants.RemoveAll(v => v.Origin == ManualLayoutOrigin.Manual);
if (removedCount > 0)
{
    if (group.Variants.Count == 0)
        collection.LayoutGroups.Remove(key);
    collection.SelectedVariants.Remove(key);
    ...
}
```

Every manual variant under the key is removed, then the group and its selected-variant entry go
too. The button reaches this via `TryDelete()` (`MainWindow.LayoutEditor.partial.cs:485`). There is
no confirmation dialog anywhere in the path. The doc comment already flags it as "legacy".

A correct single-variant path exists and is used elsewhere: `DeleteVariant` →
`LayoutEditorController.TryDeleteActiveVariant()` (`MainWindow.LayoutEditor.partial.cs:219`).

A non-destructive unload also already exists: `OnUnloadLayoutButtonClick`
(`MainWindow.LayoutEditor.partial.cs:521`), wired to the "Unload Layout" button
(`MainWindow.xaml:337`). It suppresses for the session and leaves the file untouched — which is
the behavior originally wanted from the red button.

### Steps

- [x] 2.1 **Done differently, deliberately.** Repointing the red button at
      `TryDeleteActiveVariant()` would have made it identical to the existing "Delete Variant"
      button, and then 2.4's bulk action needed a third button — three delete controls, two of
      them the same. Instead the existing single-variant button keeps that job (relabelled
      **"Delete This Layout"**) and the red button stays the bulk action, which is what 2.4 asks
      for anyway. The defect was never that bulk delete existed; it was that it was unlabelled,
      unconfirmed, and sitting behind the mildest-sounding name in the panel.
- [x] 2.2 Both delete paths confirm. Single names the variant; bulk lists every variant and states
      the count. Both default to `MessageBoxResult.No`, so a reflexive Enter cancels.
- [x] 2.3 Relabelled: red → **"Delete ALL Saved Layouts"** (stronger than the planned wording,
      since it is the bulk action), variant → **"Delete This Layout"**, unload →
      **"Unload and Recalculate"** at 14pt Bold, the panel's most prominent action.
- [x] 2.4 Bulk delete is the red button, separately confirmed, and its prompt states the count and
      lists the names. When there is nothing to delete it says so and points at
      "Unload and Recalculate" rather than acting.
- [x] 2.5 `Tests/LayoutDeleteActionsTests.cs`: controller-level tests that one delete leaves the
      others and that bulk spares AutoSeed variants, plus source guards that the count precedes the
      prompt, the prompt precedes the deletion, `TryDelete()` is reachable from exactly one handler,
      and no button is labelled "Delete and Recalculate" again. Verified to fail against the
      pre-fix source.

**Exit:** no single click can destroy more than the one named variant it announced.

---

## Phase 3 — P2: `toggle-dev-tools.ps1` never runs

### Diagnosis

Running `.\scripts\toggle-dev-tools.ps1 -State on` from **cmd.exe** (or double-clicking it) makes
Windows show "How do you want to open this file?" — cmd does not execute `.ps1`, it hands the file
to its shell association. The script never runs, `EnableDeveloperTools` stays `false`, and
`run-demo.bat` correctly shows no tools.

Confirmed on disk before the fix: both `bin/Debug/net6.0-windows/visual-config.json` and
`bin/Release/net6.0-windows/visual-config.json` were last written **2026-08-10** and both read
`"EnableDeveloperTools": false`. A successful `-State on` would have set `true` with a current
timestamp. The script's own logic looks correct and has demonstrably worked before (the files carry
PowerShell `ConvertTo-Json` formatting).

Also confirmed: `dotnet build` copies only `visual-config.default.json` to the output
(`InteractiveWorldMap.csproj:31`), so building does **not** clobber the toggled runtime config.

### Steps

- [x] 3.1 Add root `toggle-dev-tools.bat` →
      `powershell -ExecutionPolicy Bypass -File "%~dp0scripts\toggle-dev-tools.ps1" %*`,
      sitting next to `run-demo.bat` where it will actually be found.
- [x] 3.2 Echo the resulting state and the config path written, so a no-op is visible. The
      PowerShell script already did this; the wrapper passes it through unchanged.
- [x] 3.3 Have the script fail loudly if no `visual-config.json` was updated (it currently warns and
      exits 1 — verify that survives the wrapper's exit code).
- [x] 3.4 Verify end-to-end: wrapper from cmd → `run-demo.bat` → tools actually visible.
      Verified from a real cmd.exe: `.\toggle-dev-tools.bat -State off` then `-State on` flipped
      both Debug and Release `visual-config.json` and printed each path. Exit-code propagation
      through the wrapper confirmed separately (a script exiting 1 yields `ERRORLEVEL=1`).
      The final leg — launching the app and seeing the tools — is left to the user.

**Exit:** toggling dev tools works from cmd, PowerShell, and Explorer.

---

## Phase 4 — P3: Root config guide script

Config surface confirmed on merged `main`:

| File | Controls |
|------|----------|
| `visual-config.json` (next to the built exe, seeded at runtime, gitignored) | live visual settings |
| `visual-config.default.json` | tracked defaults / seed source |
| `Images&Content/Demo-Content/locations.json` | markers and locations |
| `Images&Content/Demo-Content/manual-layouts.json` | saved manual layouts |

- [x] 4.1 Add root `configure.ps1` (plus a `configure.bat` wrapper, for the Phase 3 reason — cmd
      and Explorer cannot run a `.ps1`) printing the table above with resolved absolute paths, flagging
      which files exist.
- [x] 4.2 Prompt to run the dev-tools toggle — invoking the **Phase 3 `.bat` wrapper**, not the raw
      `.ps1`.
- [x] 4.3 Make it read-only apart from that opt-in prompt; never edit configs itself.
- [x] 4.4 Match existing root-script conventions (`UpdateLocations.ps1`, `ViewCoordinates.ps1`).

---

## Phase 5 — P2: Known skipped-test bug

One real bug sits behind a skip; the audit of skipped tests is otherwise clean (2 skips total).

- [x] 5.1 `Utilities/RadialExtensionCalculator.cs` — `CalculateMaxLength` enforced a hard 20px floor
      (`Math.Max(20.0, maxLength * 0.9)` plus a second `< 20` clamp), so heads still left the
      canvas when a marker was closer than 20px to an edge. Observed Y≈-6.09 on a 100×100 canvas.
      Both floors are gone. A minimum length is a minimum distance past the edge: the clamp only
      returns a small number because the marker is that close to the edge, and stretching the line
      back to a preferred length puts the head where it is not drawn at all. The one clamp kept is
      for the marker already outside the canvas: the intermediate distance-to-edge candidate comes
      out negative there, which would point the line backwards through its own marker, so
      `DistanceToCanvasEdge` returns 0 rather than that value. It never returns a negative number.
      `CalculateExtensions_WithCanvasBounds_KeepsHeadsInsideBounds` is un-skipped, and
      `CalculateExtensions_MarkerCloserToTheEdgeThanTheOldFloor_...` pins the shape of the bug:
      it requires the line to come out *shorter than 20px*, not merely inside the bounds, so
      re-introducing a floor fails it. Both tests were confirmed to fail against the old clamp.
      The same floor turned out to exist a second time downstream: `RadialExtensionAdjuster` runs
      after the calculator, changes line lengths to separate overlapping heads (it can lengthen),
      and applies `MinimumLineLength` with no canvas to check against, so the calculator's clamp
      was not binding on the pipeline's output. `MarkerPlacementOrchestrator` re-clamps after
      adjustment; `Compute_DenseClusterAgainstTheTopEdge_KeepsEveryHeadOnTheCanvas` covers it and
      produces Y=-55.95 without the clamp. Where the edge is now lives once, in
      `CoordinateMapper.DistanceToCanvasEdge`. Found by the Qodo review of PR #18.
- [x] 5.2 Leave `AdjustExtensions_WithProtectedLocation_DoesNotMoveProtectedExtension` skipped —
      it needs a production seam and is not a product bug. Reason re-read and still accurate; it
      is now the only skipped test in the suite.

---

## Phase 6 — Layout scoping: document it and surface it in the UI

### How scoping actually works (verified in `Services/LayoutKeyGenerator.cs`)

Layouts are **per view**, never global. Saving a layout affects only the view it was saved from.
There are two key shapes:

| Scope | Key | Notes |
|-------|-----|-------|
| Whole map (zoomed out) | `"fullmap"` — `GenerateFullMapGroupKey()` | Deliberately size-independent so a window resize cannot orphan it. Legacy `fullmap_s{W}x{H}` keys still resolve. |
| A cluster (zoomed in) | `{sha256-16 of sorted location names}_z{zoom}_c{cx}_{cy}_s{W}x{H}_m{…}_p{…}_l{…}_n{…}` — `GenerateKey()` | Different location sets hash differently, so New York and Hong Kong are separate layouts. |

So: a New York cluster layout, a Hong Kong cluster layout, and the zoomed-out full-map layout are
three independent saved layouts. `AreKeysCompatible` hard-guarantees a full-map key never matches a
cluster key.

**Trap 1 — config edits silently orphan cluster layouts.** The `m`/`p`/`l`/`n` key components are
`RadialExtensionConfig` values (`MinLocationsForExtension`, `ProximityThresholdPixels`,
`ExtensionLineLength`, `MinimumLineLength`). Changing any of them in `visual-config.json` changes
every cluster key, so saved cluster layouts stop resolving. They are not deleted — just unfindable.
Presents to the user as "my layouts vanished." Full-map layouts are unaffected.

**Trap 2 — compatibility is looser than the key.** `AreKeysCompatible` compares only the location
hash and zoom (±0.1 tolerance). Viewport center and size are in the key but not in the check, so a
cluster layout is intentionally reused across pan positions at the same zoom.

### Empirical check against the live file (2026-08-18)

`Images&Content/Demo-Content/manual-layouts.json` contains **16 group keys** — 4 location hashes ×
4 viewport sizes — confirming per-view scoping in practice:

```
81a3da9f73684ffa_z55.00_c6228.33_2988.67_s{149x112|161x101|179x101|241x101}_m3_p10.0_l50.0_n13.0
9b0462aa56dd19ae_z55.00_c1490.50_2650.50_s{…}
a8bdc43c8f9c007f_z55.00_c6375.80_2933.40_s{…}
e82e7d66910b25bb_z55.00_c2458.10_2571.57_s{…}
```

There is **no `"fullmap"` entry** and no `SelectedVariants` section; every stored layout is an
auto-generated cluster seed.

**Why users report "the same layouts everywhere":** all 32 stored entries have
`"DisplayName": "Generated Seed"` and `"VariantId": "seed-default"`, and the dropdown template
(`MainWindow.xaml:176-185`) renders only `DisplayName` and `Origin`. Every cluster therefore shows
one identical-looking item, `Generated Seed [AutoSeed]`. The layouts are distinct; the labels are
not. This is a UI defect, not a scoping defect — and it is the single most misleading thing in the
editor today.

**Trap 3 — the two lookup paths disagree about viewport size.** `LoadLayout` falls back through
`FindCompatibleGroup` → `AreKeysCompatible`, which compares only hash and zoom and **ignores** the
`s{W}x{H}` component. But `ListVariants` (`Services/ManualLayoutManager.cs:207`) does a bare
`LayoutGroups.TryGetValue` with **no compatibility fallback**. At any window size outside the four
seeded sizes, the layout is applied to the map while the dropdown reports no variants at all.

### UI gap

`MainWindow.xaml:149-190` shows only `"EDIT MODE ACTIVE"` and a `Variants:` dropdown. Nothing names
the scope being edited, so two different clusters are visually identical. The dropdown lists only
the current key's variants, so changing view silently swaps the entire list with no explanation.

The panel carries five actions — Save, Save As Variant, `Delete This Layout` (`:246`),
`Delete ALL Saved Layouts` (`:298`), `Unload and Recalculate` (`:336`). Phase 2 relabelled these and
put a confirmation on both delete paths, so the remaining 6.3 work is arrangement and grouping
rather than disambiguation.

### Steps

- [x] 6.1 Show the active scope in the edit panel: `Editing: Whole map` or
      `Editing: Cluster: New York, Newark, +2 more` (location names, not the hash). Derived from
      `LayoutEditSession.ScopeDescription` rather than rebuilt in the panel — the session is the
      object a save writes through, so the label cannot name a scope the save will not reach.
      Cleared on `EndEditSession`, so a scope never outlives the session that could act on it.
      Past three locations the rest become a count: the panel is a fixed-width overlay, and a
      dozen names would push the buttons off it.
- [x] 6.2 Label the variant dropdown `Saved layouts for this view:`, and name the reason the list
      is empty. An empty dropdown over a blank status line was the "my layouts are gone" report.
      Two causes, kept apart by `Utilities/VariantStatusDescriber`: `None saved for this view yet`
      only when nothing is loaded either, and `Layout loaded, but not listed at this window size`
      when Trap 3 has hidden an applied layout from the picker. Saying "none saved" over a visibly
      applied layout would be worse than the blank line it replaced — it invites the user to redo
      work that already exists. Caught by the Qodo review of PR #19.
- [x] 6.3 Regrouped the five actions: Save and Save As, a separator captioned
      `Stop using this layout`, then Unload, Delete This Layout, Delete ALL. Previously the two
      saves were separated by a delete, so the column read as five similar options. Unload leads
      its group because it is what reaching for the red button usually meant.
- [x] 6.4 Written as [`docs/reference/manual-layout-scoping.md`](../../reference/manual-layout-scoping.md)
      (under `reference/`, matching the other layout docs, rather than at `docs/` root). Covers the
      two key shapes, all three traps, which file the app actually reads, and where
      `Generated Seed` comes from. Linked from `docs/index.md` and from `CLAUDE.md`'s key
      conventions.
- [ ] 6.5 Have the Phase 4 `configure.ps1` warn that editing the `RadialExtension` config values
      orphans saved cluster layouts (Trap 1) — that is the most likely way a user destroys their
      own work without touching the editor.
- [ ] 6.6 Test: full-map and cluster keys never collide; two distinct clusters produce distinct
      keys; changing a `RadialExtensionConfig` value changes the cluster key (pin the trap so it is
      a known, documented property rather than a surprise).
- [ ] 6.7 **Make variant labels distinguishable.** Seeds are all named `Generated Seed`, so every
      view's dropdown looks the same. Include the scope (cluster location names, or "Whole map") in
      the generated seed's `DisplayName`, and/or render scope in the dropdown item template. Covers
      existing files too — do not rely on regenerating seeds.
- [ ] 6.8 **Fix Trap 3:** give `ListVariants` the same compatibility fallback `LoadLayout` already
      uses, so the dropdown never reports "no variants" for a layout that is actively applied.
      Test at a window size outside the seeded `s{W}x{H}` set.
**Where the size variants actually come from (user, 2026-08-19):** running the app on a different
monitor — docking/undocking, or a different display — changes the window size, which changes the
viewport dimensions baked into `s{W}x{H}`. That explains the observed spread in the live file:
`taipei1`/`taipei2`/`nynyj1` under `s161x101`, `taiwan1`/`newyork1` under `s175x101`. Nothing is
broken by this on its own, and it is not the cause of the stub bug (Phase 1c).

It does have a real consequence, though, which is why 6.8/6.9 matter: because `ListVariants` matches
the key exactly while `LoadLayout` falls back compatibly, **a layout saved on one monitor can vanish
from the variant dropdown on another** while still being applied to the map. The data is fine; the
dropdown just cannot find it. Worth confirming during the 6.8 fix that a dock/undock round trip
keeps every variant listed.

- [ ] 6.9 Consider whether `s{W}x{H}` belongs in the cluster key at all. `AreKeysCompatible` already
      ignores it and full-map keys deliberately dropped it for exactly this reason; keeping it
      fragments every cluster into one group per window size (4× duplication in the current file).
      Removing it is a persistence-format change — needs a migration path for existing keys.

---

## Where "Generated Seed" comes from

Users never type this name. `Tools/ManualLayoutSeedGenerator/ManualLayoutSeedGenerator.cs:127`
hardcodes `DisplayName = "Generated Seed"`, and `ManualLayoutManager.cs:668` renders
`ManualLayoutOrigin.AutoSeed` with the same string. Seeds are precomputed from the coordinates
spreadsheet by `scripts/generate_manual_layout_seeds.ps1`, which writes
`Images&Content/Demo-Content/manual-layouts.json` — the same file user saves go to.

Checked and **not** a bug: the generator calls `CloneWithoutAutoSeeds(existing)` (line 48), stripping
only AutoSeed entries and preserving manual variants. Re-running it does not destroy saved layouts.

Document this in Phase 6.4 — "where did these layouts I never made come from" is a predictable
question, and the shared output file makes it look more dangerous than it is.

---

## Sequencing

Phase 0 first and alone — it is the leading cause of the reported data loss and its fix is a
prerequisite for confirming whether Phase 1's race contributes at all. Phase 1 second. Phase 2 next,
same category, lower frequency. Phases 3 and 4 are independent of the layout work and can be done in
either order; 4 depends on 3's wrapper existing. Phase 5 is standalone.

Suggested commit boundaries: one per phase, with Phase 1 possibly split (diagnosis/guards, then the
`Clear()` race fix).

## Verification

`.\scripts\verify.ps1` green before each commit. Phases 1–2 additionally need manual confirmation in
the running app — the failure mode is a render/save race that unit tests can approximate but not
fully reproduce. Phase 3 needs verification from a real cmd.exe window specifically.

## Open questions

- ~~Phase 2.4: keep a bulk "delete all variants" action?~~ **Resolved 2026-08-18: keep it, behind
  its own confirmation stating the number of variants affected.**
- Phase 1.6: is a re-render during an active edit session ever legitimate? A render pass calls
  `ExtensionLineRenderer.Clear()`, which empties `_markerToLine` — the only store of each pin's true
  endpoint — before repopulating it. A save landing inside that window loses every endpoint at once.
  If no such pass is ever legitimate mid-edit, suppressing renders while `IsEditMode` closes the
  window entirely; otherwise the save boundary must guard instead.
