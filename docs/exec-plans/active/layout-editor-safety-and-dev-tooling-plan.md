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

## Current State

Branched from `main` @ `9487fc0` (PR #7 + dependabot bumps merged). All five issues below were
reproduced or traced by reading merged `main` — none are fixed yet.

| # | Issue | Severity | Status |
|---|-------|----------|--------|
| 0 | Stale `CurrentLayoutKey` — editor edits/saves the wrong scope's layout | **P0 — data loss** | Core fixed (0.3–0.5, 0.9); 0.6/0.7/0.10 open; manual smoke pending |
| 1 | Saving a layout sometimes rewrites every pin as a short vertical stub | **P0 — data loss** | Not started |
| 2 | "Delete and Recalculate" deletes *all* saved variants for the key | **P1 — data loss** | Not started |
| 3 | `toggle-dev-tools.ps1` cannot be run from cmd.exe / Explorer | P2 | Not started |
| 4 | No root-level guide to which config files to edit | P3 | Not started |
| 5 | `CalculateMaxLength` 20px floor lets heads leave the canvas | P2 | Not started |
| 6 | Layout scoping is undocumented and invisible in the edit panel | P2 | Not started |

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
- [ ] 0.6 Reconsider the `preferFullMapLayout` branch (`Navigation:233-245`): preferring the
      full-map layout for display is defensible, but it must not leave the *editor* pointed at the
      full-map key. **Now mitigated** by 0.3/0.4 (the editor re-derives, the save verifies), but the
      branch itself is still a key writer and deserves a direct fix.
- [ ] 0.7 Narrow the writers. Six call sites mutate `CurrentLayoutKey`. Partially addressed (0.5
      removed one, 0.3/0.4 make the editor independent of it); the remaining writers should funnel
      through one guarded method.
- [ ] 0.10 **(new, found during the audit)** `TryLoad(key)` mutates `ActiveVariantId`/`Origin`/
      `DisplayName` without checking `key == CurrentLayoutKey`. Three probe-loads call it with
      locally computed keys (`Navigation:169`, `Navigation:237`,
      `TryLoadFullMapManualLayoutForAnimation`), desyncing variant identity from the current key.
      Either make probe loads side-effect-free or require the key to match.
- [ ] 0.8 Re-test the stub repro in the running app. If stubs persist, Phase 1's render race is the
      remaining cause. **Requires manual smoke — not yet done.**

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

- [ ] 1.1 Add a failing test: `BuildExtensions` given `markerCenter == originalScreen` currently
      emits a zero-length extension. Pin the *desired* behavior — reject, don't persist.
- [ ] 1.2 Add a failing test for `NaN`/non-finite input coordinates (`Canvas.GetLeft` returns `NaN`
      when never explicitly set) — these must not reach the JSON file.
- [ ] 1.3 Make degeneracy explicit in the model: have `BuildExtensions` return the extensions plus a
      list of markers whose geometry could not be resolved, rather than silently emitting zeros.
- [ ] 1.4 **Refuse the save** when any marker's geometry is unresolved. Surface
      `✗ SAVE ABORTED — geometry unavailable, retry` in `EditModeStatusText` and log the marker
      names. A refused save is always better than a save that destroys the layout.
- [ ] 1.5 Close the lookup gap in `GetMarkerEndpoint`: distinguish "no line registered" (unsafe —
      must abort) from "genuinely at the anchor" (legitimate zero extension). The current silent
      fallback to marker center conflates the two.
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
- [ ] 1.7 Write a pre-save backup of `manual-layouts.json` (timestamped, into `temp/`) so a bad save
      is recoverable. Note `temp/` is gitignored with a 14-day cleanup policy.
- [ ] 1.8 Regression test: a save whose endpoints resolve normally still round-trips angles and
      lengths unchanged.

**Exit:** cannot produce an all-stub layout file through the save path; a save that would do so is
refused with a visible message.

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

- [ ] 2.1 Repoint the red button from `TryDelete()` to `TryDeleteActiveVariant()` — delete the
      active variant only, never the whole group.
- [ ] 2.2 Add a confirmation dialog naming the specific variant about to be deleted.
- [ ] 2.3 Relabel: red button → **"Delete Saved Layout"**; `MainWindow.xaml:337` →
      **"Unload and Recalculate"**, and give it the visual weight of the primary action.
- [ ] 2.4 Keep bulk delete reachable but explicit — a separate, separately-confirmed
      "Delete All Variants for This Map". **Decided 2026-08-18: approved, behind its own
      confirmation.** The confirmation must state how many variants will be destroyed.
- [ ] 2.5 Tests: deleting one variant leaves the others intact; the group survives while any
      variant remains; bulk delete only triggers on the explicit path.

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

- [ ] 3.1 Add root `toggle-dev-tools.bat` →
      `powershell -ExecutionPolicy Bypass -File "%~dp0scripts\toggle-dev-tools.ps1" %*`,
      sitting next to `run-demo.bat` where it will actually be found.
- [ ] 3.2 Echo the resulting state and the config path written, so a no-op is visible.
- [ ] 3.3 Have the script fail loudly if no `visual-config.json` was updated (it currently warns and
      exits 1 — verify that survives the wrapper's exit code).
- [ ] 3.4 Verify end-to-end: wrapper from cmd → `run-demo.bat` → tools actually visible.

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

- [ ] 4.1 Add root `configure.ps1` printing the table above with resolved absolute paths, flagging
      which files exist.
- [ ] 4.2 Prompt to run the dev-tools toggle — invoking the **Phase 3 `.bat` wrapper**, not the raw
      `.ps1`.
- [ ] 4.3 Make it read-only apart from that opt-in prompt; never edit configs itself.
- [ ] 4.4 Match existing root-script conventions (`UpdateLocations.ps1`, `ViewCoordinates.ps1`).

---

## Phase 5 — P2: Known skipped-test bug

One real bug sits behind a skip; the audit of skipped tests is otherwise clean (2 skips total).

- [ ] 5.1 `Utilities/RadialExtensionCalculator.cs` — `CalculateMaxLength` enforces a hard 20px floor
      (`Math.Max(20.0, maxLength * 0.9)` plus a second `< 20` clamp), so heads still leave the
      canvas when a marker is closer than 20px to an edge. Observed Y≈-6.09 on a 100×100 canvas.
      Fix the clamp, then un-skip `RadialExtensionCalculatorTests.cs:253`.
- [ ] 5.2 Leave `AdjustExtensions_WithProtectedLocation_DoesNotMoveProtectedExtension` skipped —
      it needs a production seam and is not a product bug. Confirm the reason is still accurate.

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

The panel also now carries five actions — Save, Save As Variant, `Delete Variant` (`:246`),
`Delete and Recalculate` (`:298`), `Unload Layout` (`:336`). Two already delete, which compounds the
Phase 2 confusion.

### Steps

- [ ] 6.1 Show the active scope in the edit panel: `Editing: Whole map` or
      `Editing: cluster — New York, Newark, …` (location names, not the hash). Derive from the
      current key so it cannot drift from reality.
- [ ] 6.2 Label the variant dropdown with the same scope, so an emptied list reads as
      "no variants for *this view*" rather than "my layouts are gone."
- [ ] 6.3 Rationalize the five actions after Phase 2 relabeling; ensure the two delete actions are
      visually distinct from each other and from unload.
- [ ] 6.4 Write `docs/manual-layout-scoping.md`: the table above, both traps, and which file stores
      what. Link it from `docs/index.md` and from `CLAUDE.md`'s key-conventions list so agents hit
      it before touching layout code.
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
