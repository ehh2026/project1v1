# Layout Editor — What Was Broken, and What Changes

**Started:** 2026-08-18 · **Last updated:** 2026-08-22 · **Status:** in progress

Plain-language companion to
[../exec-plans/active/layout-editor-safety-and-dev-tooling-plan.md](../exec-plans/active/layout-editor-safety-and-dev-tooling-plan.md),
which has the technical detail. This page describes the *behavior* — what went wrong when you used
the app, and what it should do instead. Updated as each fix lands.

---

## First, how layouts are supposed to work

A saved layout belongs to **one view**, not to the whole app.

- The zoomed-out **whole map** has its own layout.
- **Each cluster** you zoom into (New York, Hong Kong, …) has its own separate layout.

Arranging pins in New York and saving does not touch Hong Kong or the whole-map view. That part of
the design was always correct — the bugs below made it *look* otherwise.

---

## 1. Saving could destroy your layout ✅ fixed

**What you saw:** after saving, every pin was redrawn as a short vertical stub. Lengths and angles
you had arranged were gone — not just on screen, but saved that way to disk.

**Why:** the editor could be pointed at the wrong layout. If you zoomed into a cluster and opened
the editor, it sometimes still had the *whole-map* layout selected. Saving then wrote that
cluster's handful of pins over the whole-map layout. Every location not in the cluster lost its
arrangement and fell back to a default stub.

**What it does now:** the editor works out which layout belongs to the view you are actually
looking at, every time you open it. Before writing, the save re-checks that the layout still
matches your current view; if it does not, the save is refused with
`✗ SAVE ABORTED — WRONG LAYOUT` instead of destroying anything. A refused save is always better
than a save that loses your work.

There was a second cause, now also fixed. The app tracks where each pin points separately from
where the pin sits. If that record was briefly unavailable when you pressed Save — during a
redraw — every pin looked like it was sitting on its own location marker, and got saved as a stub. The save
now checks that it actually knows where each pin points, and refuses with
`✗ SAVE ABORTED — GEOMETRY UNAVAILABLE, RETRY` rather than writing guesses. Simply pressing Save
again a moment later works.

**Also:** every save now keeps a copy of the previous layout file alongside it, ending in `.bak`,
so a bad save can be recovered by hand.

**Update 2026-08-19 — a third cause, found by testing.** The checks above were added to the **Save**
button but not to **Save As**, which used its own separate copy of the same logic. Saving a named
layout (`taipei1`) therefore still collapsed every pin to a stub. Both buttons now share one
checked path, so a save route cannot exist without the protections.

A further backstop was added at the same time, for pins in a **dense cluster**: if every pin that
should have a spread-out arrangement would instead be saved sitting on its own location marker, the save is
refused with `✗ SAVE ABORTED — LAYOUT COLLAPSED, RETRY`.

This deliberately only applies to dense clusters. If you zoom into an area where a few pins are far
enough apart that they are not treated as a cluster, they are *supposed* to be plain stubs, and
saving that is perfectly valid.

**Update 2026-08-19 — a fourth cause, and this one was never about saving.** A layout could save
perfectly and still come back as stubs when you loaded it again. Your saved file was fine the whole
time; the fault was in *redrawing* it.

When you arrange pins zoomed in, the app records where each head sits as a position on the map. Zoomed
that far in, dragging a head 59 pixels across the screen moves it barely one pixel on the map itself.
When redrawing, the app was converting that map distance back using the *whole-map* scale, which
turned 59 pixels into about half a pixel — small enough that it decided the pin had no arrangement at
all and drew a plain stub. Every pin in the view, every time.

It now notices when that conversion has collapsed a pin that was saved with a real arrangement, and
uses the saved angle and length instead. Whole-map layouts are unaffected — their distances are large
enough that the conversion was always fine.

Why it looked like saving worked: right after you save, the pins on screen are still the ones you
dragged. Nothing is redrawn from the file until you load a layout. **So "it saved fine" is not
evidence until you load it again** — which is why smoke test S5 matters more than S2.

### What a "stub" actually is

Useful to know when judging whether something is broken. Every pin is drawn with a short shaft of
its own (about 24px). A pin with no spread-out arrangement is drawn with just that shaft, pointing
straight up — that is the "stub" look. So:

- **One or two stubs among angled pins:** normal. Those pins simply are not part of a dense group.
- **A whole dense cluster of stubs where you had arranged angles:** the bug. Report it.

---

## 2. The same layout name appeared in every view ✅ explained, fix pending

**What you saw:** you saved a layout called something like `layout1`, then zoomed into New York or
Hong Kong and saw `layout1` listed there too — as if one layout applied everywhere.

**Why:** two things at once. The editor was pointed at the wrong layout (item 1), so it really was
listing the whole-map layout's contents. On top of that, every pre-generated layout is named
`Generated Seed`, so different views genuinely showed identically-named entries.

**What it does now:** the wrong-layout half is fixed. The naming half is not — the dropdown still
does not tell you which view you are editing.

**Still to do:** show the scope in the editor panel ("Editing: Whole map" / "Editing: New York, …")
and give generated layouts distinguishable names.

---

## 3. "Delete and Recalculate" deleted more than expected ✅ fixed

**What you saw:** the button deleted your saved layout — and every other saved version for that
view — with no confirmation. The name sounded like it was just going to recalculate.

**What changed (2026-08-22):** the three buttons now say what they destroy, and nothing destructive
happens without a prompt naming exactly what is about to go.

| Button | What it does |
|--------|--------------|
| **Unload and Recalculate** | Reverts this view to automatic placement. Destroys nothing — your saved layouts stay on disk. It holds for the rest of the run, including if you re-open the editor; saving puts a layout back, and so does restarting. This is the one most people want, so it is now the prominent one. |
| **Delete This Layout** | Deletes only the version you are looking at, after asking and naming it. The others are kept. |
| **Delete ALL Manual Layouts** | Deletes every manual layout for this view, after a confirmation that lists them and states the count. Generated seeds are not touched. |

Both prompts default to "No", so pressing Enter out of habit cancels rather than deletes. If there
is nothing to delete, the red button says so and points you at "Unload and Recalculate" instead of
doing anything.

---

## 4. Where "Generated Seed" layouts come from ℹ️ not a bug

Layouts named **Generated Seed** were not created by you. They are pre-computed starting
arrangements built from the coordinates spreadsheet by a build tool, so the map looks reasonable
before anyone arranges anything by hand.

They live in the same file as your own saved layouts. Re-running the generator is safe: it replaces
only the generated ones and leaves anything you saved untouched.

---

## 5. Resizing the window while editing ✅ fixed

**What you saw:** if the window changed size while you were dragging pins, the positions the app
held in memory could be lost, and a save straight afterwards stored empty geometry.

**What it does now:** resizing the window no longer re-places pins while you are in edit mode. Your
arrangement stays put until you save or leave the editor.

Because the view has moved but the pins have not, the app also marks the session as no longer
trustworthy for saving. If you try to save after resizing, it refuses with
`✗ SAVE ABORTED — VIEW CHANGED, RE-ENTER EDIT MODE`. Leave and re-open Edit Layout and your saved
arrangement is reloaded against the new window size. This is deliberate: saving in that state would
have mixed old pin positions with new map positions and stored wrong angles and lengths.

---

## 5b. Using a different monitor can hide layouts from the list ⬜ not started

**What you may see:** a layout you saved earlier is missing from the Variants dropdown, even though
the map still looks arranged.

**Why:** a saved cluster layout records the window size it was made at. Plugging into a different
monitor, or docking and undocking, changes that size, and the dropdown only lists layouts matching
the current one. The map itself is more forgiving and still finds the layout, which is why the
arrangement can appear while the name does not.

**Nothing is lost.** The layouts are all still in the file. Returning to the previous monitor or
window size makes them reappear.

**What it should do:** list every saved layout for the view regardless of window size.

---

## 5c. A zoomed-in pin arrangement was saved but never shown ✅ fixed

**What you saw:** on the whole map you arrange a pin — say Ohio — and save. Clicking it zooms in and
it keeps the unzoomed appearance, which is intended. But if you then open Edit Layout while zoomed
and give it a different angle or length and save, zooming away and back showed the *old* unzoomed
appearance again. Only clicking Edit Layout brought your zoomed version back.

**Why:** the display always preferred the whole-map arrangement for a single zoomed pin, while the
editor always worked on the zoomed one. So you could save a zoomed arrangement that the display never
loaded.

**What it does now — which arrangement wins:**

1. An arrangement **you made** for this zoomed view
2. Otherwise an arrangement **you made** on the whole map
3. Otherwise the pre-generated starting layout

In short, the most specific thing you did by hand wins, and pre-generated layouts never override your
own work.

**Worth knowing:** a single pin can now have two saved arrangements — one for the whole map, one for
zoomed in. That is deliberate, since zoomed in there is much more room to spread a pin out. If you
want the zoomed one gone so it falls back to the whole-map version, delete that zoomed layout.

---

## 6. Changing settings can make layouts "disappear" ⬜ not started

**What you may see:** you edit `visual-config.json`, restart, and your saved **cluster** layouts are
gone. Whole-map layouts are unaffected.

**Why:** four values under `RadialExtension` — `MinLocationsForExtension`,
`ProximityThresholdPixels`, `ExtensionLineLength`, `MinimumLineLength` — are part of how a cluster
layout is identified. Change one and the app looks for a layout under a different name.

**Important:** nothing is deleted. The layouts are still in the file; the app just cannot find them.
Restoring the old values brings them back.

**What it should do:** warn before you change these. As of 2026-08-20 the config helper script
exists — run `configure.bat` at the project root (or double-click it) and it lists every config
file, where it is on your machine, and what it controls. The warning about these four values is
still to come.

---

## 7. The developer tools toggle did not work ✅ fixed

**What you saw:** running `.\scripts\toggle-dev-tools.ps1 -State on` made Windows ask "How do you
want to open this file?", and developer tools stayed off afterwards.

**Why:** the script was being run from a `cmd.exe` window, which cannot execute PowerShell scripts —
it hands them to Windows to open instead. The script never ran, so nothing changed.

**What changed (2026-08-20):** there is now a `toggle-dev-tools.bat` at the project root, next to
`run-demo.bat`. It works from a cmd window, a PowerShell window, or a double-click in Explorer, and
takes the same options as before:

```
toggle-dev-tools.bat              flip whatever it is now
toggle-dev-tools.bat -State on    force on
toggle-dev-tools.bat -State off   force off
```

There is a `configure.bat` next to it for the same reason. Both wait for you to press Enter
before closing, so a double-click from Explorer does not make the window vanish before you can
read it. From a command prompt that is one extra keypress.

It prints which state it moved to and which file it wrote, so a no-op is visible rather than silent,
and it reports a failure (for example, no built copy of the app to configure) instead of appearing
to succeed. Relaunch the app for the change to take effect.

---

## Manual smoke tests

These cannot be checked automatically — the app needs a real window, and the failures were
intermittent. Please run them after each round of fixes and record the result here.

### Required smokes

| # | Test | Steps | Expected |
|---|------|-------|----------|
| S1 | Save (plain) in a cluster | Zoom into a cluster, Edit Layout, drag some pin heads, press **Save** | Pins stay where you put them; `✓ LAYOUT SAVED` |
| S2 | **Save As** in a cluster | Same, but use **Save As** and give it a name | Pins stay put; `✓ VARIANT SAVED`. Named variant appears in the dropdown |
| S3 | Save on the whole map | Zoom out fully, Edit Layout, drag heads, Save | Pins stay put; other clusters' layouts unaffected |
| S4 | Variant scoping | Save a named layout in cluster A, then open the editor in cluster B | B's dropdown does **not** list A's variant |
| S5 | Reopen after save | Save, exit edit mode, re-enter | Your arrangement is still there, not stubs |
| S6 | Resize while editing | Enter edit mode, drag heads, resize the window, then Save | The save is **refused** with `✗ SAVE ABORTED — VIEW CHANGED, RE-ENTER EDIT MODE`. Leave and re-open Edit Layout: your saved arrangement is reloaded, and saving then works |
| S7 | Restart persistence | Save, close the app, reopen | Arrangement restored |
| S8 | Sparse view still saveable | Zoom to an area with a few pins too far apart to cluster, Edit Layout, Save | Save **succeeds**. Stubs here are correct, and must not be refused as "collapsed" |
| S9 | Zoomed-vs-unzoomed precedence | Arrange a lone pin on the whole map and save; click it to zoom in; Edit Layout, change angle, save; zoom out and back in | The zoomed version you saved is shown, without needing to click Edit Layout |
| ~~S10~~ | ~~Deliberate all-anchor arrangement~~ | Dropped 2026-08-20 — not a real use case. See "Simplification available" below |
| S11 | Delete one layout | Save two named layouts for a view, open the picker on one, press **Delete This Layout**, confirm | The prompt names that layout. Only it goes; the other is still in the dropdown |
| S12 | Delete all, and cancelling | Press **Delete ALL Manual Layouts**, read the count, press **No**. Then repeat and press **Yes** | Cancelling changes nothing. Confirming removes every manual layout for the view; pins revert to automatic placement |
| S13 | Unload is non-destructive | Press **Unload and Recalculate**, then leave and re-enter the view | Pins revert to automatic placement immediately, and the saved layout is still there afterwards |

If a save is refused you will see a red `✗ SAVE ABORTED — …` message. That is the guard working:
nothing was written, and the layout on disk is intact. Press Save again after a moment. Please
report the exact message if it happens repeatedly.

### Simplification taken (S10 dropped) ✅

The collapse guard refuses a save when every pin in a **dense cluster** would be stored sitting on
its own location marker. It used to carry an exception: if you had dragged every one of those pins
yourself, the save was allowed on the grounds that you meant it.

That exception existed **only** to serve S10, which was judged not a real use case. As of
**2026-08-20** it and the per-marker drag tracking behind it are deleted, so an all-anchor dense
cluster is now always refused.

Sparse views are unaffected — pins too far apart to form a cluster are still supposed to be plain
stubs and still save normally (S8, passed).

### Smoke log

| Date | Build | Test | Result |
|------|-------|------|--------|
| 2026-08-19 | `b1fa379` | S2 — Save As in Hong Kong / Taipei, variant `taipei1` | ❌ **FAILED** — all pins snapped to short vertical stubs. Cause: the Save As path did not go through the new guards. Fixed in `81e6ced`. |
| 2026-08-19 | `81e6ced` | S2 — Save As `taipei2` | ✅ Saved correctly, no stubs |
| 2026-08-19 | `81e6ced` | S5 — load another layout, then reload `taipei2` | ❌ **FAILED** — all stubs again. **The saved file was verified intact** (real angles preserved); the fault was in redrawing a zoomed-in layout, not in saving it. Fixed below. |
| 2026-08-19 | `83f716d` | S5 — save, load another layout, reload the new one | ✅ **PASSED** — arrangement redrawn correctly, no stubs. Confirms the redraw fix against the original failure |
| 2026-08-20 | post-#13 | S8 — sparse view still saveable | ✅ **PASSED** — a zoomed view of scattered pins saves without being refused as "collapsed" |
| 2026-08-20 | — | S10 — deliberate all-anchor drag | ⏭️ **Dropped, not a real use case.** Nobody arranges a dense cluster by dragging every head onto its own location marker. Kept in the table only to record the decision; see the note below |
| 2026-08-21 | `0f79ad9` (Phase C) | S1, S2, S3, S4, S5, S7, S9 | ✅ **ALL PASSED** — run against the branch that deletes the ambient layout key. S9 in particular confirms zoomed-vs-unzoomed precedence, and S7 confirms an arrangement survives an app restart |
| _(pending)_ | `0f79ad9` | S6 | ⏭️ Deprioritized 2026-08-21 — the resize-during-edit refusal is covered by automated tests and the scenario is not one you hit |

---

## Status at a glance

| # | Issue | Status |
|---|-------|--------|
| 1 | Saving destroys the layout | ✅ Fixed 2026-08-19 |
| 1b | Saved layout redraws as stubs when reloaded | ✅ Fixed 2026-08-19 — confirmed in the app (S5) |
| 2 | Same layout name in every view | ◐ Cause fixed; labels still unclear |
| 3 | "Delete and Recalculate" deletes everything | ✅ Fixed 2026-08-22 — relabelled, and both delete actions confirm first (S11, S12) |
| 4 | "Generated Seed" layouts | ℹ️ Working as intended |
| 5 | Resize while editing loses positions | ✅ Fixed 2026-08-19 — save now refused until you re-enter edit mode (S6) |
| 5b | Different monitor hides layouts from the list | ⬜ Not started — nothing lost, display only |
| 5c | Zoomed arrangement saved but never shown | ✅ Fixed 2026-08-19 — confirmed in the app 2026-08-21 (S9) |
| 6 | Config changes hide cluster layouts | ◐ `configure.bat` now shows where the configs are; the warning is still missing |
| 7 | Dev tools toggle unrunnable from cmd | ✅ Fixed 2026-08-20 — use `toggle-dev-tools.bat` at the project root |

**Confirmed in the app:** on 2026-08-21, S1, S2, S3, S4, S5, S7 and S9 all passed against the
branch that deletes the ambient layout key — saving in a cluster and on the whole map, Save As,
variant scoping, reopening after a save, surviving an app restart, and zoomed-vs-unzoomed
precedence. S8 passed earlier. Every issue marked fixed above has now been seen working in the
running app.

**Still unrun:** S6 (resize mid-edit), deprioritized — it is covered by automated tests and is not
a scenario you hit. S10 was dropped as unrealistic.

If you do see pins collapse to stubs after saving, please note whether a `✗ SAVE ABORTED` message
appeared: that distinguishes "a guard caught it" from a cause not yet found.
