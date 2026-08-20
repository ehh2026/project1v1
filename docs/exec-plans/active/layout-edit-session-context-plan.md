---
status: active
owner: agent
started: 2026-08-20
---

# Replace Ambient Layout-Key State With an Edit-Session Context

**Goal:** Remove `LayoutEditorController.CurrentLayoutKey` as ambient mutable state. An edit session
captures its scope once, immutably, on entry; every save, delete and validation reads from that
capture. Display and replay stop sharing that state and pass their key explicitly.

**Architecture:** The session type is a `Models/` record (no upstream references). `Services/` owns
session lifetime and validation; `MainWindow` partials create a session on editor entry and pass it
back in. No new layer edges — `LayerDependencyTests.cs` must stay green.

**Tech Stack:** .NET 6, WPF, xUnit, `.\scripts\verify.ps1`.

---

## Why now

PR #13 fixed four real data-loss defects, but every one traced to the same ambient state, and the
fixes were guards around it rather than a fix at source. The pattern is now well evidenced:

| Round | What happened |
|-------|---------------|
| Phase 0 | Editor inherited a stale key; fixing it exposed the display/editor precedence conflict (user's Ohio report) |
| Phase 1 | Guarded `OnSaveLayoutButtonClick`; **Save As was a second unguarded copy** — user's `taipei1` failure |
| Phase 1 | `OnSizeChanged` guard fixed one corruption and **introduced** the stale-coordinate one |
| Phase 1b | Collapse backstop then wrongly refused a deliberate all-anchor drag |
| Qodo round 2 | **Two of four findings were defects introduced by round one's fixes** |

That last row is the signal. Each layer of guards defends state that six call sites can write, and
infers user intent from final coordinates. The guards work and are verified, but they keep
generating edge cases at the seams.

**Current surface:** 6 `SetLayoutKey` writers, 55 `CurrentLayoutKey` reads in production, and 19
references to guard machinery that exists only because the key is ambient
(`CurrentLayoutKeyMatchesView`, `DeriveCurrentViewLayoutKey`, `_editGeometryStaleReason`).

---

## The root confusion

`CurrentLayoutKey` serves two unrelated jobs:

1. **Edit scope** — which layout this editing session will write to.
2. **Display/replay key** — which layout to load and apply while navigating, and the group key for
   the composite-pin plan cache (`ApplyManualLayout`, `MainWindow.LayoutEditor.partial.cs:544`).

Navigation legitimately updates (2) constantly — zoom animations, full-map probes, cluster entry.
Sharing one field means navigation silently mutates (1). **That is the defect class**, not any
individual call site. Splitting the two is the fix; the immutable session is how (1) is expressed.

---

## Target shape

```csharp
// Models/LayoutEditSession.cs
public sealed record LayoutEditSession(
    string LayoutKey,
    LayoutScope Scope,                       // FullMap | Cluster
    IReadOnlyList<Location> ScopeLocations,  // empty for FullMap
    ViewportState Viewport,                  // captured at entry
    double ContainerWidth,
    double ContainerHeight);
```

Created once in `OnEditLayoutButtonClick`, held for the session, discarded on exit. Saves take it as
an argument instead of reading a field.

**What this deletes rather than adds** — the measure of whether the refactor is worth it:

| Removed | Because |
|---------|---------|
| `SetLayoutKey` and its 6 writers | Nothing mutates edit scope after entry |
| `CurrentLayoutKeyMatchesView`, `DeriveCurrentViewLayoutKey` | The session *is* the expected key; nothing to re-check |
| `ExtensionCollectionStatus.WrongLayout` | Unrepresentable — a session cannot point elsewhere |
| `_editGeometryStaleReason` flag | Becomes derived: `session.Viewport != current` |
| Variant-identity clearing in `SetLayoutKey` | Identity lives in the session; no cross-scope leak possible |

If a phase ends without deleting its listed items, the refactor is not paying for itself — stop and
reassess rather than layering more.

---

## Phase A — Introduce the session alongside existing state ✅ done 2026-08-20

Additive only; `CurrentLayoutKey` still exists and still works. No behaviour change.
Verified: `.\scripts\verify.ps1` **PASSED**, all 11 steps, 915 passed / 2 known skips.

- [x] A.1 `Models/LayoutEditSession.cs` holds both `LayoutScope` and the session record.
- [x] A.2 Add `LayoutEditorController.BeginEditSession(LayoutEditSession)` / `EndEditSession()`, with
      `ActiveSession` exposed read-only. `EnterEditMode`/`ExitEditMode` keep working.
- [x] A.3 `OnEditLayoutButtonClick` builds the session from the view on screen — the derivation
      already exists in `LayoutKeyGenerator.DeriveEditSessionKey` — and begins it. Keep the existing
      `SetLayoutKey` call for now so display paths are untouched.
- [x] A.4 Unit-test session construction for both scopes, including that a cluster session's key
      equals `DeriveEditSessionKey` for the same inputs.

**Exit:** session exists and is populated; suite green; no behaviour change.

## Phase B — Move the *edit* paths onto the session ✅ done 2026-08-20

- [x] B.1 `TryCollectCurrentExtensions` takes the session and uses `session.LayoutKey`,
      `session.Viewport`, `session.ContainerWidth/Height` instead of reading ambient state and the
      live viewport. Delete the `WrongLayout` branch and `CurrentLayoutKeyMatchesView`.
- [x] B.2 Derive staleness instead of flagging it: compare `session.Viewport` and container size
      against current. Delete `MarkEditSessionGeometryStale` and `_editGeometryStaleReason`; keep
      the user-facing `✗ SAVE ABORTED — VIEW CHANGED` message and its smoke (S6).
- [x] B.3 `TrySave`, `TrySaveAsVariant`, `TryDelete`, `TryDeleteActiveVariant`, `SwitchToVariant`,
      `GetVariants` take the session (or a key argument) rather than reading `CurrentLayoutKey`.
- [x] B.4 Move active-variant identity (`ActiveVariantId`/`Origin`/`DisplayName`) into session state,
      which makes the cross-scope leak unrepresentable. **Closes 0.9 structurally** and removes the
      clearing logic in `SetLayoutKey`.
- [x] B.5 Make `TryLoad` side-effect free and have callers update session variant identity
      explicitly. **Closes 0.10** — probe loads during navigation can no longer desync it.
- [x] B.6 Port the existing tests. **Audited 2026-08-20: 27 `SetLayoutKey` call sites across 24
      tests, all in `Tests/LayoutEditorControllerTests.cs`, with no fixture choke point — the port
      is per test.** 19 are the simple "set a key, then save/load/delete" shape and convert
      mechanically. Four need judgement, because they encode behaviour the session model makes
      unrepresentable:
      - `SetLayoutKey_ChangingKey_ClearsActiveVariantIdentity` and
        `TrySave_AfterKeyChange_DoesNotWriteIntoPreviousKeysVariant` change scope *mid-test*. You
        cannot do that with an immutable session, which is the point — they become "a new session
        does not inherit the previous one's variant identity", or are deleted as testing an
        impossible state. Decide deliberately; do not silently drop them, they cover a real past bug.
      - `SetLayoutKey_SameKey_PreservesActiveVariantIdentity` tests the setter's internal
        same-key short-circuit. That logic disappears with the setter.
      - `SetLayoutKey_Null_ClearsKey` becomes `EndEditSession`.
      Three tests assert on `CurrentLayoutKey` directly and six assert on variant identity; both
      groups move to the session.
- [x] B.7 Re-target the meta-test at `Tests/LayoutEditorKeyDerivationTests.cs:98`, which asserts by
      source-text search that `TryLoadFullMapManualLayoutForAnimation` does not call `SetLayoutKey`.
      Once the method is gone the string search passes vacuously — it must assert the replacement
      property or be removed rather than left as a test that can no longer fail.

**Exit:** no edit path reads `CurrentLayoutKey`; the guards listed above are deleted, not disabled.

## Phase C — Give display/replay its own explicit key

- [ ] C.1 Thread the group key explicitly into `ApplyManualLayout` instead of
      `CurrentLayoutKey ?? layout.GroupKey`. It already has `layout.GroupKey`; the ambient read is
      the fallback that lets navigation state leak into the plan-cache key.
- [ ] C.2 `TryApplyFullMapManualLayout`, `TryApplyFullMapLayoutForZoomedSingle`, `ShowZoomedView`
      and `ApplyManualLayoutDuringAnimation` use locals for their key and stop calling
      `SetLayoutKey`. Precedence behaviour (Manual zoomed → Manual full-map → seed) must not change —
      it has a test and a smoke (S9).
- [ ] C.3 Delete `SetLayoutKey` and `CurrentLayoutKey`.
- [ ] C.4 Check the zoom-animation hot path for regressions: `ApplyManualLayoutDuringAnimation` runs
      per frame, so the key must not be recomputed per frame. Hoist it out of the frame callback.

**Exit:** `CurrentLayoutKey` no longer exists; navigation cannot influence edit scope by
construction.

## Phase D — Fold in the deferred hardening

- [ ] D.1 Revisit the collapse backstop. With an immutable session and per-marker drag tracking,
      check whether the coordinate-based heuristic is still needed or whether session state answers
      "was this arrangement deliberate?" directly. Prefer deleting it to keeping it.
- [ ] D.2 Re-examine `HasManualVariant`'s loader-alignment compromise (PR #13 Qodo findings 2 vs 4,
      which contradicted each other). A session that records its own scope may make the question
      unambiguous.
- [ ] D.3 Update `docs/reference/layout-editor-known-issues.md` if any user-visible message changes.

---

## Sequencing and risk

Phases in order; each is independently shippable and leaves the suite green. **Phase C is the risky
one** — it touches navigation and the zoom hot path, which have the least automated coverage and the
most manual-smoke dependence.

**Prerequisite:** run smokes **S8** and **S10** on the merged main *before* starting. They are the
only unrun checks covering false-refusal behaviour, and a known-good baseline is what makes a
post-refactor regression attributable. Starting without them risks confusing a pre-existing problem
for one this refactor caused.

**Rollback:** each phase is a separate commit; Phase A and B are additive/local. If Phase C shows
navigation regressions that the smokes cannot pin down, stop at B — the edit paths are already off
ambient state by then, which is where the data-loss defects lived.

## Verification

- `.\scripts\verify.ps1` green before each commit.
- Every deletion in the target-shape table actually happens; if a phase cannot delete its items,
  record why in the progress log instead of proceeding.
- Manual smokes after Phase C: **S1, S3, S4, S5, S6, S9** (scope, save, replay, resize, precedence).
  S5 and S9 matter most — they cover the two failures the user actually hit.
- Watch the zoom animation for frame-time regressions after C.4.

## Open questions

- Should a session survive `ExitEditMode` → re-enter, or always be rebuilt? Rebuilding is simpler
  and matches the current staleness fix; surviving would preserve variant selection across a
  toggle. Defaulting to rebuild unless it proves annoying in use.
- Does anything outside the editor legitimately need to know the *edit* scope? If not, `ActiveSession`
  can stay internal to the controller and the MainWindow partials.
