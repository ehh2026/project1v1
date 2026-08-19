# Layout Editor — What Was Broken, and What Changes

**Started:** 2026-08-18 · **Last updated:** 2026-08-19 · **Status:** in progress

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

## 1. Saving could destroy your layout ✅ partly fixed

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
redraw — every pin looked like it was sitting on its own map dot, and got saved as a stub. The save
now checks that it actually knows where each pin points, and refuses with
`✗ SAVE ABORTED — GEOMETRY UNAVAILABLE, RETRY` rather than writing guesses. Simply pressing Save
again a moment later works.

**Also:** every save now keeps a copy of the previous layout file alongside it, ending in `.bak`,
so a bad save can be recovered by hand.

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

## 3. "Delete and Recalculate" deleted more than expected ⬜ not started

**What you saw:** the button deleted your saved layout — and every other saved version for that
view — with no confirmation.

**What it should do:**

| Button | Behavior |
|--------|----------|
| **Unload and Recalculate** | Reverts to automatic placement for now. Your saved layout stays on disk and comes back next time. Non-destructive. |
| **Delete Saved Layout** | Deletes only the one version you are looking at, after asking. |
| **Delete All Versions** | Deletes everything saved for this view, after a separate confirmation that says how many. |

A non-destructive "Unload Layout" button already exists today and does the safe thing — it is just
not the obvious one to reach for.

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

---

## 6. Changing settings can make layouts "disappear" ⬜ not started

**What you may see:** you edit `visual-config.json`, restart, and your saved **cluster** layouts are
gone. Whole-map layouts are unaffected.

**Why:** four values under `RadialExtension` — `MinLocationsForExtension`,
`ProximityThresholdPixels`, `ExtensionLineLength`, `MinimumLineLength` — are part of how a cluster
layout is identified. Change one and the app looks for a layout under a different name.

**Important:** nothing is deleted. The layouts are still in the file; the app just cannot find them.
Restoring the old values brings them back.

**What it should do:** warn before you change these, and a planned config helper script will call
this out.

---

## 7. The developer tools toggle did not work ⬜ not started

**What you saw:** running `.\scripts\toggle-dev-tools.ps1 -State on` made Windows ask "How do you
want to open this file?", and developer tools stayed off afterwards.

**Why:** the script was being run from a `cmd.exe` window, which cannot execute PowerShell scripts —
it hands them to Windows to open instead. The script never ran, so nothing changed.

**Workaround today**, from a PowerShell window at the project root:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\toggle-dev-tools.ps1 -State on
```

**What it should do:** a `toggle-dev-tools.bat` at the project root, next to `run-demo.bat`, that
works from anywhere you double-click or type it.

---

## Status at a glance

| # | Issue | Status |
|---|-------|--------|
| 1 | Saving destroys the layout | ✅ Fixed 2026-08-19 — needs real-app confirmation |
| 2 | Same layout name in every view | ◐ Cause fixed; labels still unclear |
| 3 | "Delete and Recalculate" deletes everything | ⬜ Not started |
| 4 | "Generated Seed" layouts | ℹ️ Working as intended |
| 5 | Resize while editing loses positions | ✅ Fixed 2026-08-19 |
| 6 | Config changes hide cluster layouts | ⬜ Not started |
| 7 | Dev tools toggle unrunnable from cmd | ⬜ Not started |

**Not yet confirmed in the real app.** Both causes of item 1 are covered by automated tests, but the
original failure was intermittent and has not been reproduced by hand since the fixes. If you still
see pins collapse to stubs after saving, please report it — and note whether a
`✗ SAVE ABORTED` message appeared, since that distinguishes "the guard caught it" from "there is a
third cause we have not found".
