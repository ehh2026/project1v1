---
status: active
owner: agent
started: 2026-06-08
requirements_ref: remove-pins-jpg
supersedes_partial: composite-pins-unzoomed-plan.md
parent_program: composite-pins-program.md
parent_plan: pin-parts-composite-placement-plan.md
---

# Remove `pins.jpg` Legacy Path — Drawn vs Composite Only

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Eliminate the legacy `pins.jpg` sprite-sheet rendering path (`ImagePinMarker` / `PinImages` config) so pin-style markers are only **drawn** (`PinMarker`) or **composite** (`CompositePinMarker`).

**Architecture:** Collapse the three-way pin switch (`PinImages` vs drawn vs composite) into a two-way switch gated by existing `PinParts` flags. Composite becomes the primary “rich pin” renderer; drawn pins remain the lightweight fallback when composite is disabled or assets fail. Non-pin mode (`UsePinMarkers = false`) keeps simple circular `LocationMarker` dots unchanged.

**Tech stack:** WPF / .NET 6 / C#, `visual-config.json`, `Pins_v2/parts` composite assets, existing `CompositePinPlanningService` pipeline.

---

## Problem

The app currently has **four** marker visual paths, three of which involve pin styling:

| Path | Trigger | Renderer |
|------|---------|----------|
| Circular dots | `UsePinMarkers = false` | `LocationMarker` default content |
| Legacy image pins | `UsePinMarkers = true` + `PinImages.Enabled = true` | `ImagePinMarker` crops from `pins.jpg` |
| Drawn pins | `UsePinMarkers = true` + `PinImages.Enabled = false` | `PinMarker` |
| Composite pins | Above + `PinParts.Enabled` + `PinParts.UseCompositeRendering` | `CompositePinMarker` (extended markers only today) |

Pain points:

1. **Composite is coupled to legacy image pins** — `CanUseCompositePins()` requires `PinImages.Enabled`, and `ApplyCompositePinToMarker()` refuses markers whose base content is not `ImagePinMarker`.
2. **Non-extended markers still use `pins.jpg`** even when composite is enabled globally.
3. **Edit mode depends on `ImagePinMarker`** as the draggable wrapper; composite is torn down on enter and legacy image pins are rebuilt.
4. **Config is confusing** — `PinImages.Enabled` sounds like “use image pins” but really means “use the obsolete sprite sheet”; drawn pins are reached only by disabling it.
5. **`visual-config.json` ships a large `PinImages.Pins` rectangle table** that exists solely for `pins.jpg` cropping.

## Target behavior

After this plan:

| `UsePinMarkers` | `PinParts.Enabled` + `UseCompositeRendering` | Result |
|-----------------|---------------------------------------------|--------|
| `false` | (ignored) | Circular `LocationMarker` |
| `true` | `false` | Drawn `PinMarker` everywhere |
| `true` | `true` | `CompositePinMarker` for every **individual** location marker at all zoom levels |

**Fallback policy (explicit):**

- Composite asset/planning failure → **drawn `PinMarker`**, not `pins.jpg`.
- Extension line still drawn when composite cannot render an extended endpoint (existing `ExtensionLineRenderer` behavior preserved).

**Config surface (post-change):**

- **Remove** entire `PinImages` section from `visual-config.json`.
- **Remove** `Models/PinImageConfig.cs`, `Models/PinImageInfo` (in same file).
- **Keep** `UsePinMarkers`, `PinMarkers`, `PinParts` as the only pin controls.
- Document the two pin modes clearly in `docs/guides/VISUAL_CONFIG.md`.

Optional future polish (out of scope unless trivial): add a string enum `PinRenderingMode: "drawn" | "composite"` that maps 1:1 to `PinParts.UseCompositeRendering`. Not required if the boolean pair is documented well.

## Relationship to other plans

| Plan | Relationship |
|------|--------------|
| [pin-parts-composite-placement-plan.md](../completed/pin-parts-composite-placement-plan.md) | Parent — composite pipeline already built |
| [composite-pins-unzoomed-plan.md](composite-pins-unzoomed-plan.md) | **Partially superseded** — Phases 2–3 (all-marker composite rollout) become prerequisites of this plan, not a separate legacy fallback |
| [composite-pins-manual-layout-phases-plan.md](../completed/composite-pins-manual-layout-phases-plan.md) | **Must revise** — edit-mode drag wrapper must stop depending on `ImagePinMarker` |
| [refactoring-assessment-followthrough-plan.md](refactoring-assessment-followthrough-plan.md) | Marker factory extraction should target the new two-path factory |

When this plan completes, mark the “replace `ImagePinMarker` everywhere” items in `docs/TO_DO.md` and `composite-pins-unzoomed-plan.md` as done or redirect here.

Also update conflicting active-plan language in `composite-pins-unzoomed-plan.md` and `composite-pins-manual-layout-phases-plan.md` so the active plan directory no longer tells future agents to keep an `ImagePinMarker` wrapper.

---

## Current code map (delete / refactor targets)

### Delete or fully retire

| File / asset | Role today |
|--------------|------------|
| `Views/ImagePinMarker.xaml` | Legacy pin UserControl |
| `Views/ImagePinMarker.xaml.cs` | Crop, hover, click, `GetConnectionPoint()` |
| `Models/PinImageConfig.cs` | `PinImageConfig`, `PinImageInfo` |
| `visual-config.json` → `PinImages` block | 12 crop rectangles + `MasterImagePath` |
| `Images&Content/pins.jpg` (if present) | Master sprite sheet |
| `MainWindow.xaml.cs` → `_masterPinImage`, `LoadMasterPinImage()`, `CreateImagePinMarker()` | Loader + factory |

### Refactor (core)

| File | Changes |
|------|---------|
| `MainWindow.xaml.cs` | `CreatePinMarker()` → drawn or composite-ready stub; decouple `CanUseCompositePins()` from `PinImages`; relax `ApplyCompositePinToMarker()` base-content check |
| `MainWindow.xaml.cs` | Redefine `_baseMarkerVisuals` / restore flow so normal composite markers are not restored back to drawn stubs on each positioning pass |
| `MainWindow.xaml.cs` | Make normal-marker positioning anchor-aware for `CompositePinMarker` (`TipAnchorLocal`), not centered on `LocationMarkerSize` |
| `Models/VisualConfig.cs` | Remove `PinImages` property |
| `Views/ExtensionLineRenderer.cs` | Confirm extension-line fallback when composite returns false (no `PinImages` branch) |
| `scripts/verify_taste.py` | Remove `Views/ImagePinMarker.xaml.cs` from allowed-large-file list (or delete entry when file gone) |

### Docs / harness

| File | Changes |
|------|---------|
| `docs/guides/VISUAL_CONFIG.md` | Rewrite “Pin Rendering Modes” for two pin types |
| `docs/TO_DO.md` | Close or redirect legacy-image-pin items |
| `ARCHITECTURE.md` | Drop `PinImageConfig` from config table |
| `CHANGELOG.md` | Breaking config change under `[Unreleased]` |
| `docs/exec-plans/active/README.md` | Add this plan |
| `docs/archive/planning/PIN_IMAGE_PLACEMENT_ASSESSMENT.md` | Add deprecation note at top (historical reference only) |

### Scripts (non-runtime)

| File | Action |
|------|--------|
| `scripts/extract_pins.py` | Archive note in `scripts/README.md` or move to `scripts/archive/` — no longer part of active pipeline |

---

## Phase 0 — Policy gate (no code)

**Deliverable:** Confirm decisions before implementation.

- [x] **Non-extended composite policy:** Adopt **stub segment** (Option A) from [composite-pins-unzoomed-plan.md Phase 0](composite-pins-unzoomed-plan.md#phase-0--policy-decision--2026-06-09) — `DefaultStubLengthPixels = 24`, screen-up stub for unzoomed individual markers only; unzoomed `ClusterMarker` aggregates excluded. Recorded 2026-06-09.
- [x] **Edit-mode drag target:** Dragging stays attached to the `LocationMarker` canvas item. The edit-mode mouse-down policy already lets marker-level drag handlers start, so no replacement wrapper is introduced.
- [x] **Composite failure fallback:** Always `PinMarker`, never reintroduce a third image path. Composite apply now accepts drawn/composite pin-style bases only.

---

## Phase 1 — Config model cleanup

**Deliverables:** Config deserializes without `PinImages`; defaults produce drawn or composite only.

| Action | File |
|--------|------|
| Remove `PinImages` property | `Models/VisualConfig.cs` |
| Delete model file | `Models/PinImageConfig.cs` |
| Remove `PinImages` JSON block | `visual-config.json` |
| Update startup/harness config load tests if any reference `PinImages` | `Tests/StartupValidationHarnessTests.cs` (grep first) |

**Acceptance:**

- [x] `VisualConfig.LoadFromFile("visual-config.json")` succeeds with no `PinImages` key.
- [x] `dotnet build` succeeds (fix any compile errors from removed types).

---

## Phase 2 — Marker factory: drawn vs composite stub

**Deliverables:** `CreatePinMarker()` no longer calls `CreateImagePinMarker()`.

### Task 2a — Simplify `CreatePinMarker`

**File:** `MainWindow.xaml.cs`

Replace the `PinImages.Enabled` branch with:

```csharp
private LocationMarker CreatePinMarker(Location location)
{
    if (_visualConfig.PinParts.Enabled && _visualConfig.PinParts.UseCompositeRendering)
        return CreateCompositeReadyPinMarker(location);

    return CreateDrawnPinMarker(location);
}
```

`CreateCompositeReadyPinMarker` initially creates a drawn `PinMarker` wrapper (same as today’s fallback) so behavior is safe before Phase 3 wires full composite at create time.

### Task 2b — Delete legacy image path

**File:** `MainWindow.xaml.cs`

- [x] Delete `CreateImagePinMarker()`, `LoadMasterPinImage()`, field `_masterPinImage`.
- [x] Remove `case ImagePinMarker` from click-animation switch (replace with `CompositePinMarker` / `PinMarker` only).

**Acceptance:**

- [x] Grep for `ImagePinMarker`, `PinImages`, `_masterPinImage`, `pins.jpg` in `*.cs` → zero hits outside deleted files.

---

## Phase 3 — Composite for all individual markers

**Deliverables:** Composite renders at full-map zoom and for non-extended cluster members — absorbs [composite-pins-unzoomed-plan.md](composite-pins-unzoomed-plan.md) Phases 1–3.

| Create | `Services/CompositePinTargetBuilder.cs` |
| Create | `Tests/CompositePinTargetBuilderTests.cs` |
| Modify | `MainWindow.xaml.cs` — marker create, position, rebuild paths |
| Modify | `Models/PinPartConfig.cs` — `DefaultStubLengthPixels` |
| Modify | `visual-config.json` — stub default |

### Task 3a — Target builder

`CompositePinTargetBuilder.Build(location, viewport, containerSize, radialExtension?, pinPartsConfig)` returns `(Point start, Point end, double angleDeg)`:

- Extended marker → use existing extension endpoints.
- Non-extended / unzoomed → stub segment from original position upward by `DefaultStubLengthPixels`.

### Task 3a.1 — Restore/cache semantics

**File:** `MainWindow.xaml.cs`

Current positioning starts by calling `RestoreBaseMarkerVisuals()`, which restores `marker.Content`, `marker.Width`, and `marker.Height` from `_baseMarkerVisuals`. That is correct for today's "temporarily replace legacy image pin with composite" flow, but wrong once composites become the normal content.

- [ ] Decide whether `_baseMarkerVisuals` remains a **fallback visual cache** only or is replaced by an explicit `RebuildMarkerVisualForCurrentMode(...)` helper.
- [ ] Ensure normal update passes do not restore a successfully applied `CompositePinMarker` back to a drawn `PinMarker` stub.
- [ ] Ensure composite failure restores/leaves a drawn `PinMarker` fallback with matching `marker.Width`, `marker.Height`, and `marker.Tag`.
- [ ] Add a focused test or harness assertion for this behavior if practical; otherwise include it in the manual smoke checklist.

### Task 3b — Decouple composite gating

**File:** `MainWindow.xaml.cs`

```csharp
private bool CanUseCompositePins()
{
    return _visualConfig.UsePinMarkers &&
           _visualConfig.PinParts.Enabled &&
           _visualConfig.PinParts.UseCompositeRendering &&
           !_layoutEditor.IsEditMode;
}
```

Remove `PinImages.Enabled` check.

### Task 3c — Relax apply guard

**File:** `MainWindow.xaml.cs` — `ApplyCompositePinToMarker`

Replace:

```csharp
baseState.Content is not ImagePinMarker
```

with acceptance of any pin-style base (`PinMarker`, `CompositePinMarker`, or marker with no composite yet applied). Composite apply replaces `marker.Content` with `CompositePinMarker` as today.

### Task 3d — Create-time composite

When composite mode is on, after creating `LocationMarker`, call target builder + `ApplyCompositePinToMarker` (or inline equivalent) for both normal and extended positions.

**Fallback:** On failure, leave or restore `PinMarker` content.

### Task 3e — Anchor-aware normal positioning

**File:** `MainWindow.xaml.cs` — `PositionMarkerNormally`

Do not center composite pins using `_visualConfig.LocationMarkerSize`. For composite mode, build the stub target and position by the render plan's `TipAnchorLocal`, the same anchoring rule already used by `ApplyCompositePinToMarker` for extended markers.

- Drawn fallback: keep center-based `LocationMarkerSize` placement.
- Composite success: place `Canvas.Left = target.Start.X - plan.TipAnchorLocal.X`, `Canvas.Top = target.Start.Y - plan.TipAnchorLocal.Y`.
- Composite failure: keep/restore drawn fallback and center it by `LocationMarkerSize`.

**Acceptance:**

- [ ] Full-map view: all individual locations show composite pins (stub shaft).
- [ ] Zoomed cluster: extended markers use real extension segment; non-extended use stub or no extension per layout.
- [ ] Panning/zooming does not cause composite markers to flicker back to drawn pins.
- [ ] Non-extended composite tips remain anchored on the original map coordinate, not visually centered around it.
- [ ] `CompositePinTargetBuilderTests` green.
- [x] No reference to `ImagePinMarker` in composite apply path.

---

## Phase 4 — Edit mode without `ImagePinMarker`

**Deliverables:** Manual layout edit mode works without legacy image pins — revises [composite-pins-manual-layout-phases-plan.md](../completed/composite-pins-manual-layout-phases-plan.md) assumptions.

| Modify | `MainWindow.xaml.cs` — enter/exit edit mode, drag handlers |
| Modify | `Services/LayoutEditorController.cs` — if any ImagePin assumptions |
| Modify | `Views/CompositePinMarker.xaml.cs` — drag hit targets if needed |

### Task 4a — Drag geometry on `LocationMarker`

- [x] Keep drag handlers attached to `LocationMarker`; this is already the current event target, so do not invent a new `ImagePinMarker` replacement wrapper just for input.
- [ ] Normalize drag math so saved manual-layout endpoints use the active visual's intended anchor:
  - drawn fallback → center by `LocationMarkerSize`
  - composite → use the render plan tip/head anchor as appropriate for endpoint semantics
- [ ] Enter edit mode: swap to draggable representation (drawn pin or composite overlay per Phase 0 decision) without losing marker-level handlers.
- [ ] Exit edit mode: replay manual layout → re-apply composite.

### Task 4b — Reassign Pins

- [x] `OnReassignPinsButtonClick` continues to bypass edit-mode composite gate; must not require `ImagePinMarker` wrapper.

**Acceptance:**

- [ ] Edit → drag → save → exit → composite pins at saved endpoints.
- [ ] Reassign Pins reshuffles shaft/head without detaching drag handlers.
- [ ] Dragging a composite marker moves the intended endpoint, not the wrapper's top-left or old `LocationMarkerSize` center.

---

## Phase 5 — Delete legacy view + docs

**Deliverables:** Repo contains no `pins.jpg` code path.

- [x] Delete `Views/ImagePinMarker.xaml` and `.xaml.cs`
- [x] Remove from `.csproj` if explicit Compile entries exist (SDK-style projects usually auto-glob)
- [x] Update `docs/guides/VISUAL_CONFIG.md` — two pin modes + `UsePinMarkers = false` dots
- [x] Update `ARCHITECTURE.md`, `CHANGELOG.md`, `docs/TO_DO.md`
- [x] Update `composite-pins-unzoomed-plan.md` and `composite-pins-manual-layout-phases-plan.md` to remove or redirect stale `ImagePinMarker` wrapper instructions.
- [x] Update `scripts/verify_taste.py` if it whitelists `ImagePinMarker.xaml.cs`
- [x] Optional: remove `pins.jpg` from `Images&Content/` and note in changelog

**Acceptance:**

- [x] Code/config grep: `rg -i "pins\.jpg|PinImages|ImagePinMarker|PinImageConfig" --glob '!docs/**' --glob '!*.md'` → zero runtime/config hits, except deleted-file history outside the working tree.
- [x] Docs grep: `rg -i "pins\.jpg|PinImages|ImagePinMarker|PinImageConfig" docs ARCHITECTURE.md CHANGELOG.md` → only this plan, changelog/TO_DO redirects, active-plan redirect notes, and explicitly historical/deprecated docs.

---

## Phase 6 — Verification

**Deliverables:** Harness green; manual smoke checklist complete.

### Automated

```powershell
.\scripts\verify.ps1
```

Expected: build, all tests, doc links, taste checks, startup validation pass.

Result 2026-06-12: `.\scripts\verify.ps1` passed (Release build, 291 tests, doc links, taste checks, startup validation).

### Manual smoke matrix

| Config | View | Expected |
|--------|------|----------|
| `UsePinMarkers=false` | Full map | Red circles |
| `PinParts.UseCompositeRendering=false` | Full map | Drawn `PinMarker` |
| `PinParts.UseCompositeRendering=true` | Full map | Composite stub pins |
| Composite on | Zoomed cluster | Composite on extensions |
| Composite on, assets missing | Any | Drawn fallback + log warning |
| Manual layout + composite | Edit/save/exit | Saved positions preserved |
| Composite on + pan/zoom | Full map and zoomed cluster | Markers remain composite; no restore-to-drawn flicker |

### Debug overlay spot-check

- [ ] `Debug.ShowCompositePinDebugOverlay: true` at 0°, 45°, 90°, 135° extension angles
- [ ] Capture screenshots → `docs/screenshots/remove-pins-jpg/` (or existing composite folder)

---

## Test plan (new / updated tests)

| Test file | What to add |
|-----------|-------------|
| `Tests/CompositePinTargetBuilderTests.cs` | Stub segment geometry; extended segment passthrough |
| `Tests/VisualConfigDeserializationTests.cs` (create if missing) | Load config without `PinImages`; defaults for pin modes |
| `Tests/StartupValidationHarnessTests.cs` | Ensure harness config path still valid |

No tests should reference `PinImageConfig` after Phase 1.

---

## Migration notes for operators

1. **Before upgrade:** If a deployment relied on `PinImages.Enabled = true`, switch to `PinParts.UseCompositeRendering = true` (composite) or `PinParts.UseCompositeRendering = false` (drawn).
2. **Remove** `PinImages` block from any forked `visual-config.json` — unknown JSON keys are ignored today, but the section is dead weight.
3. **`pins.jpg` is no longer loaded** — safe to delete from content packages to save disk space.
4. **Breaking change** — semver minor or major depending on project policy; document in `CHANGELOG.md`.

---

## Risks and mitigations

| Risk | Mitigation |
|------|------------|
| Edit-mode regression when `ImagePinMarker` removed | Phase 0 drag-target decision; Phase 4 dedicated acceptance |
| Unzoomed composite perf (many markers) | Bitmap cache already in `LoadPinPartBitmap`; monitor startup logs |
| Manual layouts saved under legacy visuals | Replay uses angle/length — unaffected by visual content type |
| Large `MainWindow.xaml.cs` diff | Keep changes surgical; defer `MarkerFactory` extraction to refactoring plan |

---

## Execution checklist (summary)

- [x] Phase 0 — Policy decisions recorded
- [x] Phase 1 — Config model cleanup
- [x] Phase 2 — Marker factory (drawn vs composite stub)
- [ ] Phase 3 — Composite for all individual markers
- [ ] Phase 4 — Edit mode without `ImagePinMarker`
- [x] Phase 5 — Delete legacy view + docs
- [ ] Phase 6 — `verify.ps1` + manual smoke (automated verification passed; manual smoke remains)

---

## Open decisions

1. **Stub direction for non-extended pins** — default upward vs map-north vs per-location hash (recommend: upward stub, configurable length).
2. **Whether to add explicit `PinRenderingMode` enum** — convenience vs minimal config churn (recommend: defer; document `PinParts.UseCompositeRendering` as the switch).
3. **Delete `scripts/extract_pins.py` vs archive** — decided 2026-06-12: deleted with obsolete `Pins.jpg` source asset; current part/variant tooling remains.
