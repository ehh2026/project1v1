---
status: active
owner: agent
started: 2026-06-05
requirements_ref: pin-parts-composite-placement
---

# Pin Parts Composite Placement Plan

Use the split `Pins_v2/parts` assets to render composite pins where:

- the shaft tip lands on a map start point
- the shaft/head junction lands on a map end point
- the head is rotated so its residual stub sits on top of the shaft
- shaft choice favors the closest native angle/length, with optional residual rotation

This plan supersedes the single-bitmap assumptions in [docs/PIN_IMAGE_PLACEMENT_ASSESSMENT.md](../../PIN_IMAGE_PLACEMENT_ASSESSMENT.md) where useful.

## Execution Checklist

### Phase Status

- [x] Phase 1: Metadata completion
- [ ] Phase 2: Config and loading model
- [x] Phase 3: Composite marker rendering
- [ ] Phase 4: Placement calculator
- [ ] Phase 5: MainWindow integration
- [ ] Phase 6: Verification and tuning

### Recommended Execution Order

- [x] Complete metadata required for deterministic shaft/head alignment and segmented shaft stretching
- [x] Add runtime/config loading seams for part metadata without breaking current image-pin loading
- [x] Render one isolated composite pin correctly in a local test harness or dev-only path
- [x] Add deterministic pair/transform selection logic and unit coverage
- [x] Integrate composite pins into extended image-pin rendering only
- [x] Preserve edit mode, manual layout replay, and variant-aware auto-load behavior
- [ ] Add verification overlays and screenshots for common extension angles

### Transition Strategy

- [x] Keep the current single-bitmap pin path available during rollout
- [x] Gate composite-pin rendering behind an explicit config switch or staged integration seam
- [x] Use composite pins first for extended markers only
- [ ] Leave non-extended pins on legacy rendering until extended-marker behavior is stable
- [x] Avoid replacing edit-mode or manual-layout workflows until composite hit-testing and anchoring are verified

## Goal

Replace the current "crop one pin from `pins.jpg` and center it on the extension endpoint" approach with a part-based composite renderer that can:

- choose a shaft variant that best matches a desired segment
- stretch only the shaft along its native axis to fit the target span
- rotate and place the corresponding head so its stub aligns with the shaft
- support either:
  - Option A: always rotate/stretch to the exact requested segment
  - Option B: choose the best native shaft/head pair and minimize residual rotation/stretch

## Current Code Seams

- `Views/ImagePinMarker.xaml.cs`
  - currently renders one bitmap
  - still assumes the connection point is `Width / 2, Height * 0.9`
- `MainWindow.xaml.cs`
  - `CreateImagePinMarker()` still crops from `_masterPinImage`
  - `ApplyRadialExtensions()` still draws a `Line` and then centers the marker on `ExtendedPosition`
- `Utilities/RadialExtensionCalculator.cs`
  - already contains the current automatic endpoint-distribution logic for dense groups
  - preserves angular order and performs angle nudging plus anti-convergence / anti-intersection adjustment passes
- `Models/PinImageConfig.cs`
  - only knows about `pins.jpg` crop rectangles
- `Models/RadialExtension.cs`
  - already carries `OriginalPosition`, `ExtendedPosition`, and `Angle`
- `Services/ManualLayoutManager.cs`
  - saved endpoint overrides still exist and can be loaded, applied, saved, and deleted
- `MainWindow.xaml.cs`
  - already loads saved manual layouts for zoomed clusters and applies them after the automatic extension pass
- `Images&Content/Pins_v2/parts/pin_parts_manifest.json`
  - contains original full-pin head center, shaft tip, and shaft head-side geometry
- `Images&Content/Pins_v2/parts/pin_part_geometry.json`
  - contains cropped-part local coordinates plus original mapped coordinates

## Existing Placement Intelligence To Preserve

The composite-pin work should reuse the current endpoint-placement workflow rather than replace it conceptually.

The repo still has:

- automatic dense-group endpoint placement
- optional saved manual endpoint overrides
- edit mode for dragging visible endpoints and resaving them

So the composite-pin renderer should be downstream of endpoint selection.

In other words:

- the current automatic/manual system should continue deciding `OriginalPosition` and `ExtendedPosition`
- the new composite-pin renderer should consume those positions and render shaft/head assets onto them

That keeps the old drawn-pin intelligence intact and reduces the risk of regressions in cluster layout behavior.

### Layout variants and provenance must remain explicit

The endpoint system should not assume there is only one saved layout per cluster/view forever.

The storage model should support:

- auto-generated seed layouts
- manually adjusted user layouts
- future imported/shared layouts

Those should be separate variants under the same logical layout group, not silent overwrites of one another.

At minimum each saved variant should carry:

- `GroupKey`
- `VariantId`
- `DisplayName`
- `Origin` (`AutoSeed`, `Manual`, `Imported`)
- `IsDefault`
- `CreatedUtc`
- `UpdatedUtc`
- optional lineage such as `BasedOnVariantId` / `BasedOnKey`

Auto-load policy should prefer:

1. exact default manual variant
2. exact default auto-seed variant
3. compatible default manual variant
4. compatible default auto-seed variant

That gives the app a sensible fallback path while still preserving the distinction between machine-generated starting points and user-curated layouts.

### Edit mode and variant-aware behavior to preserve

Composite pins must not break the current layout editing workflow.

The plan should explicitly preserve:

- dragging visible endpoints in edit mode
- saving a user-adjusted layout as a manual variant
- replaying a previously chosen manual variant
- keeping auto-seeds available as fallback defaults rather than overwriting them

That means composite-pin rendering should remain downstream of endpoint selection even in edit mode:

- edit mode continues to modify endpoint placement data
- layout save/load continues to operate on endpoint variants
- composite pins simply re-render from the selected endpoint variant

The UI work for browsing/selecting multiple variants can remain a later slice, but the storage and runtime model should assume variants exist from the start.

### Canonical endpoint data vs saved composite placement results

The plan should distinguish between:

- canonical saved layout intent
- derived composite render results

Recommended rule:

- the canonical saved data remains endpoint placement
  - `OriginalPosition`
  - `ExtendedPosition`
  - variant metadata / provenance
- the composite choice and transforms are derived from that endpoint segment

However, for manual layouts it is reasonable to persist the derived composite result as a cache so the app does not have to rerun candidate matching and transform planning every time the same saved layout is replayed.

Recommended saved derived fields:

- selected pair id
- selected shaft file
- selected head file
- target segment length / angle at time of planning
- chosen placement mode
- residual diagnostics from selection
- exact render-plan outputs needed for replay
  - head rotation
  - body stretch factor
  - local anchors
  - local bounds

Recommended invalidation inputs for that cache:

- layout variant id
- layout key / viewport compatibility key
- window size or other screen-space replay dimensions
- pin-part geometry metadata version or file hash
- relevant visual-config fields affecting selection / rendering

Recommended runtime policy:

1. load the manual layout endpoints
2. try to load a saved composite-placement snapshot compatible with the current viewport/config state
3. if compatible, replay it directly
4. if not compatible, rerun planning once and save the new derived result

That keeps user intent stable while still allowing performance optimization and avoiding stale composite placements when geometry/config changes.

## Current Zoom and Visibility Behavior

The shipped app is not using continuous marker re-clustering by zoom level. It is effectively operating in two display states:

1. Full-map cluster view
   - multi-location clusters show a cluster marker
   - single-location clusters show an individual marker
2. Zoomed cluster view
   - after clicking a cluster, the app zooms to a fixed `ZoomScale`
   - only that cluster's individual markers are shown
   - all cluster markers are hidden

Radial extensions are then applied only when:

- radial extensions are enabled
- the viewport zoom is above `RadialExtension.ZoomThresholdForExtensions`
- dense groups are detected among the visible individual markers

Important consequence: individual marker size is currently screen-space fixed, not map-scale fixed.

That is true for:

- regular location markers via `LocationMarkerSize`
- cluster markers via `ClusterMarkerSize`
- drawn pin markers
- current image pin markers, whose width/height are set once from `PinImages.ScaleFactor`

`UpdateMarkerPositions()` recomputes marker screen position from the viewport, but it does not rescale marker visuals as zoom changes.

## Zoom Policy for Composite Pins

The plan should preserve the current interaction model unless explicitly changed later.

### Recommended default

Composite pins should also be screen-space fixed.

That means:

- the marker's visual size stays constant in pixels while visible
- only its screen position changes as the viewport changes
- any radial-extension target segment is interpreted in screen space
- shaft stretch and head placement are solved in screen pixels

This is the lowest-risk integration because the current overlap logic and extension math already assume screen-space marker sizes.

### Where composite pins should appear

For the MVP:

- full-map cluster view:
  - keep existing cluster markers for multi-location clusters
  - keep existing individual marker behavior for single-location clusters
- zoomed cluster view:
  - use composite pins for visible individual markers inside the selected cluster
- radial-extension view:
  - use composite pins for the markers participating in extensions

This avoids introducing composite pins into the zoomed-out cluster-marker state, where they are not currently needed.

### Pins shown at multiple zoom levels

If a location is visible as an individual marker in more than one zoom state, the composite pin should remain the same screen-space size across those states by default.

That keeps behavior consistent with the current code and avoids these problems:

- overlap calculations drifting with zoom
- cluster-to-individual transitions changing marker size and position simultaneously
- extension line lengths and marker radii no longer matching

## Why screen-space fixed size is the right default

Several current systems already assume fixed pixel sizes:

- `LocationMarkerSize`
- `ClusterMarkerSize`
- radial-extension line length
- overlap detection thresholds in `MainWindow`
- hit-testing and wrapper centering

If composite pins were instead map-scale fixed, their on-screen size would change with zoom, and the following would need rework:

- overlap resolution
- extension target lengths
- hit boxes
- dense-group detection vs rendered size
- manual layout replay behavior

That is a substantially larger design change than the pin-parts work itself.

## Future Zoom Variants

If map-scale-aware sizing is desired later, treat it as a separate feature flag with explicit policy choices, for example:

- `FixedScreenSize`
- `ZoomResponsiveSize`
- `HybridClamp`

`HybridClamp` would usually be the only defensible non-default option:

- keep a base screen-space size
- allow mild growth only in a bounded zoom range
- clamp to a minimum and maximum rendered size

That would preserve usability while avoiding giant markers at high zoom.

This should not be part of the initial composite-pin rollout.

## Recommended Rendering Model

Create an explicit composite-pin pipeline instead of bolting more logic into `ImagePinMarker`.

### Runtime model

For each visible extended marker, produce a `PinPlacementTarget`:

- `StartScreen`: shaft tip target on screen
- `EndScreen`: shaft/head junction target on screen
- `AngleDeg`
- `LengthPx`
- `LocationId`
- `GroupId`

That target should be ephemeral runtime state, rebuilt whenever marker positions are recomputed.

### Asset model

Introduce structured metadata for each shaft/head pair:

- `ShaftId`
- `HeadId`
- `PairId`
- `ShaftTipLocal`
- `ShaftJoinLocal`
- `ShaftNativeAngleDeg`
- `ShaftNativeLength`
- `HeadCenterLocal`
- `HeadAttachLocal`
- `HeadNativeStubAngleDeg`
- `BaseScale`
- optional style tags: color family, size class, lighting family

The important addition is `HeadAttachLocal`. `head_center` alone is not enough for compositing.

## Critical Missing Components

### 1. Head attachment metadata is missing

Current generated data gives:

- head center
- shaft tip
- shaft head-side endpoint

What is still missing for correct compositing is the attachment point inside each cropped head image:

- where the shaft should meet the head
- what the native stub direction is inside that head crop

Without that, the head can be centered approximately, but the stub will not reliably sit on the shaft.

#### Solution

Generate and store, for each head:

- `HeadAttachLocal`
- `HeadStubDirectionDeg`

This can be derived automatically from existing data:

1. take the original full-pin `head_center`
2. take the original full-pin `shaft head_side`
3. project `head_side` into the head crop's local coordinate system
4. store that projected point as `HeadAttachLocal`
5. store the vector from `HeadAttachLocal` to `HeadCenterLocal` or vice versa as the head's native orientation reference

### 2. Stretching the entire shaft bitmap is not safe

The split shaft asset still includes:

- the sharp tip
- the metallic body
- the head-side cap/shadow region

If the whole bitmap is stretched along its axis, the tip and end-cap will elongate too. That will look wrong.

#### Solution

Do not treat the shaft PNG as one uniformly stretchable sprite.

Make segmented shaft rendering the primary implementation:

1. Define shaft cap metadata and stretch only the interior band.
   - Add `StretchStartDistance` and `StretchEndDistance` along the shaft axis.
   - Render as a 3-part shaft: tip cap, stretchable body, head-side cap.
2. Use full-shaft bounded stretch only as an emergency fallback.
3. If the requested segment is still too far from native geometry, prefer a different shaft rather than excessive stretch.

Segmented shaft rendering is the MVP, not a later refinement.

#### Practical auto-segmentation

The segmentation does not need pixel-perfect hand annotation. It only needs to avoid stretching the visible end caps and shadow-heavy ends.

A workable script can derive approximate boundaries from the existing shaft geometry:

1. compute the shaft axis from the cropped shaft mask
2. project opaque pixels onto that axis
3. identify:
   - `TipDistance = 0`
   - `JoinDistance = native shaft length`
4. reserve fixed cap zones near both ends
5. define the middle interval as stretchable

Initial heuristics are sufficient, for example:

- `TipCapLength = max(18 px, 0.10 * native length)`
- `HeadCapLength = max(18 px, 0.12 * native length)`
- clamp so the stretchable middle keeps at least `25%` of the native shaft length

Those numbers should be configurable and then tuned visually.

The script output should store:

- `TipCapLength`
- `HeadCapLength`
- `StretchStartDistance`
- `StretchEndDistance`
- optional sampled slice bounds or debug points for overlay verification

This is enough to write code that avoids stretching the end caps without requiring extremely precise manual segmentation.

### 3. Current marker anchoring is incompatible with part-based placement

Right now:

- normal positioning centers markers by `LocationMarkerSize`
- extended positioning centers the whole marker wrapper on `ExtendedPosition`
- `ImagePinMarker.GetConnectionPoint()` is not used to drive extension placement

For a composite pin, the anchor must be explicit:

- shaft tip anchor at start
- shaft join anchor at end
- head attach anchor at end

#### Solution

Introduce a dedicated composite marker view or presenter where placement is driven by explicit local anchors, not by wrapper centering.

Recommended shape:

- `CompositePinMarker` view
  - contains a local `Canvas`
  - child `Image` for shaft
  - child `Image` for head
- `CompositePinPlacement`
  - precomputed local transforms and local offsets
- `MainWindow`
  - computes target segment and applies the composite placement result

### 4. Coordinate ownership is currently mixed

`RadialExtension` uses screen-space positions for rendering, while location coordinates originate in source image space. That is workable for runtime rendering, but it is not a stable persistence format for manual layouts or replay.

#### Solution

Keep two layers of data distinct:

- source-space layout intent
  - persisted if needed later
- screen-space render target
  - rebuilt on every viewport update

For saved endpoint data, persist grouped layout variants plus provenance metadata rather than a single ambiguous flat layout entry. That is especially important now that generated seeds and manual edits both need to coexist.

For this work, use a runtime `PinPlacementTarget` dictionary keyed by location id or location reference:

```text
Dictionary<Location, PinPlacementTarget>
```

This dictionary should be regenerated inside `UpdateMarkerPositions()` or immediately before `ApplyRadialExtensions()`.

### 5. Head and shaft pairing policy is underspecified

The user suggestion allows either:

- using the corresponding head for a chosen shaft
- or using any head if the head rotation is computed correctly

Technically both are possible, but arbitrary mixing may produce visual mismatch:

- color mismatch
- lighting mismatch
- different apparent scale families
- mismatched shadow softness

#### Solution

Start with strict pairing:

- each shaft has one default corresponding head

Then optionally allow controlled mixing only within a compatible style group:

- same color family
- similar sphere size
- similar lighting family

This should be encoded in metadata, not hardcoded heuristics.

## Option Analysis

### Option A: Exact segment fit using parts

Process:

1. compute the exact start/end segment
2. choose a shaft/head pair by style or nearest native angle
3. rotate shaft to target angle
4. stretch only the shaft middle segment along axis to target length
5. rotate head so its stub direction matches the shaft at the endpoint
6. place shaft tip at start and head attach at end

Pros:

- exact geometric fit
- no need to fake the head position with center-based placement
- best use of the parts workflow

Cons:

- requires correct head attach metadata
- requires shaft cap/stretch-band metadata for high-quality stretching
- most implementation work

### Option B: Best native shaft plus minimal residual transform

Process:

1. choose the shaft whose native angle and native length are closest to the target
2. clamp residual rotation and segmented-body stretch
3. use the corresponding head
4. place by explicit tip/join anchors

Pros:

- lower visual risk
- less extreme stretching
- better fit for the photographed/metallic assets

Cons:

- some target segments will still fit poorly
- requires a robust scoring function and fallback behavior

### Recommendation

Implement an Option B MVP on top of an Option A-capable data model, but with segmented shaft rendering from the start.

That means:

- store the metadata needed for exact compositing
- use segmented shaft body stretch rather than full-shaft stretch
- initially choose the nearest shaft/head pair and clamp residual deformation

This reduces visual failure risk while preserving the architecture needed for a later exact-fit mode.

## Proposed Phases

## Phase 1: Metadata completion

Deliverables:

- new metadata file for part-based pins, or extension of `pin_part_geometry.json`
- per-head attach point and stub direction
- per-shaft join point and native angle/length
- shaft cap and stretch-band metadata

Tasks:

- [x] Extend `scripts/compute_pin_part_geometry.ps1` or add a companion script.
- [x] Derive `HeadAttachLocal` from existing original-space shaft/head geometry.
- [x] Add `HeadStubDirectionDeg`.
- [x] Add explicit `ShaftJoinLocal` naming instead of relying on generic `head_side`.
- [x] Derive `StretchStartDistance` and `StretchEndDistance`.
- [x] Add `TipCapLength` and `HeadCapLength`.
- [ ] Emit a debug overlay or preview data so segmentation can be visually checked quickly.

Acceptance:

- one metadata file is sufficient to place both parts and the shaft segmentation without referring back to the old full-pin crop rectangles
- each pair has enough information to align shaft tip and head attach point deterministically

## Phase 2: Config and loading model

Deliverables:

- config support for part-based pins
- content loading path that does not rely on raw view-side file access

Tasks:

- [x] Add `PinPartConfig` and supporting model types under `Models/`.
- [x] Add config fields for:
  - parts folder
  - geometry metadata path
  - selection mode
  - segmented stretch clamp values
  - optional compatible-style groups
- [x] Extend `ContentLoader` to resolve and load part assets plus metadata.
- [ ] Keep `Views/` free of direct path construction.

Acceptance:

- the app can load part definitions and their images through existing app orchestration

## Phase 3: Composite marker rendering

Deliverables:

- new composite marker control
- explicit anchor-based placement

Tasks:

1. Create `CompositePinMarker` rather than expanding `ImagePinMarker` indefinitely.
2. Give it:
   - shaft tip-cap layer
   - shaft body layer
   - shaft head-cap layer
   - head image layer
   - a local canvas coordinate system
3. Add a placement API that accepts:
   - chosen asset pair
   - target start/end points
   - computed transforms
4. Preserve hover/click animation behavior.

Acceptance:

- one composite marker can be placed correctly on an arbitrary start/end segment in isolation without stretching the shaft end caps

Implementation note:

- `CompositePinRenderPlanBuilder` now computes exact tip/join anchoring plus segmented shaft-body stretch and head rotation in isolation.
- `CompositePinMarker` now renders the shaft as tip/body/head-cap layers plus a rotated head image from that render plan.
- Live `MainWindow` integration is still pending.

## Phase 4: Placement calculator

Deliverables:

- deterministic placement-selection logic

Tasks:

- [x] Add a calculator class outside `Views/`, for example `Services/PinPartPlacementCalculator.cs`.
- [x] Inputs:
  - target segment
  - available shaft/head pair metadata
  - selection mode
- [ ] Outputs:
  - chosen pair
  - shaft transform
  - head transform
  - bounding box / local marker size
- [x] Implement scoring:
  - angle error
  - length error
  - style compatibility penalty
  - residual transform penalty
- [x] Clamp rotation and segmented-body stretch in MVP mode.
- [ ] Reconcile selection-time clamp fields with exact segment-fit rendering output before live integration.

Acceptance:

- for a fixed target segment and asset set, selection is deterministic and testable

Open integration gap:

- the current `PinPartPlacementCalculator` clamp fields are useful for ranking and diagnostics, but the live composite renderer still needs exact target-segment output for the chosen pair
- before Phase 5, either the calculator should emit both exact-fit render transforms and bounded-fit diagnostics, or the render-plan builder should become the single exact-fit authority downstream of pair selection

## Phase 5: MainWindow integration

Deliverables:

- part-based markers in the live radial-extension pipeline

Tasks:

1. Replace the current "draw line + center marker on endpoint" path for image pins.
2. Build the runtime target dictionary:
   - start = original location screen position
   - end = extended screen position
3. For non-extended pins, decide one of:
   - keep legacy single-pin rendering
   - create a default short segment pointing inward from the nearest map edge
4. Update marker sizing and hit-testing so wrapper bounds match the composed pin bounds.
5. Preserve existing extension hover highlighting, or redefine it around shaft visuals.
6. Route manual-layout replay through the same composite planner when composite rendering is active.
7. Persist derived composite-placement results for saved manual layouts so replay can skip unnecessary recomputation when viewport/config inputs are unchanged.

Acceptance:

- dense-group image pins render as composite shaft + head rather than line + centered bitmap

Current implementation status:

- composite pins are now wired into the live radial-extension path for extended image pins only
- rollout is gated by `PinParts.Enabled` plus `PinParts.UseCompositeRendering`
- the legacy image-pin path remains available and is still used for non-extended markers, edit mode, and any composite fallback/error case
- entering edit mode now forces an immediate rebuild onto the legacy draggable path, and exiting edit mode refreshes the current view back to the active non-edit rendering path
- the remaining Phase 5 work is mostly about hit-testing, drag/edit semantics, manual-layout replay through the composite path, derived composite-result persistence, and deciding whether any non-extended pins should ever switch to composite rendering

## Phase 6: Verification and tuning

Deliverables:

- repeatable tests and visual checks

Tasks:

1. Add unit tests for the placement calculator.
2. Add geometry tests for metadata completeness and coordinate sanity.
3. Add a visual debug mode that overlays:
   - start point
   - end point
   - shaft tip anchor
   - head attach anchor
   - stretch start
   - stretch end
4. Capture before/after screenshots for representative extension angles.
5. Run `scripts/verify.ps1` on a machine with the .NET 6 SDK installed.

Acceptance:

- no obvious gap between shaft and head
- no obvious endpoint drift at common extension angles
- no architecture rule regressions

Current implementation status:

- composite markers now support an optional debug overlay showing tip, join, stretch-band anchors, and head center
- the overlay is gated by `Debug.ShowCompositePinDebugOverlay`
- screenshot capture is still manual; the current repo does not yet automate those captures

## Specific Risks and Solutions

| Risk | Why it matters | Solution |
|---|---|---|
| Shaft stretch distorts tip or cap | Full-bitmap axial stretch will elongate non-stretchable features | Make segmented shaft rendering the default and store cap/body/cap metadata |
| Head stub does not align after rotation | `head_center` is not the same as the head attach point | Add `HeadAttachLocal` and `HeadStubDirectionDeg` |
| Marker placement still uses wrapper center | Composite pins need explicit anchors, not center-based placement | Introduce `CompositePinMarker` with anchor-based local placement |
| Mixed heads/shafts look inconsistent | Random mixing can produce lighting/color mismatch | Start with strict pairing, then allow only metadata-defined compatible groups |
| Runtime transforms become hard to reason about | Composite WPF transforms can get brittle quickly | Move all transform math into a single calculator that emits final placement data |
| Screen-space only placement blocks future persistence | Manual layouts and replay need stable coordinates | Separate source-space intent from screen-space render targets |

## Open Decisions

1. MVP selection mode:
   - exact-fit Option A
   - nearest-fit Option B with clamps
2. Shaft rendering strategy:
   - heuristic cap/body/cap segmented stretch
   - manual overrides only if heuristics fail
3. Non-extended image pins:
   - keep legacy rendering
   - or convert all image pins to composite markers
4. Head/shaft pairing:
   - strict pairing only
   - or compatible-group mixing

## Recommended MVP Decisions

1. Use Option B selection first.
2. Use strict shaft/head pairing first.
3. Keep legacy rendering for non-extended pins first.
4. Use heuristic segmented shaft rendering first; only fall back to full-shaft clamp temporarily if a specific shaft fails segmentation.

## Definition of Done

- part metadata includes shaft tip, shaft join, head attach point, head stub direction, and shaft stretch-band boundaries
- a composite pin can be anchored exactly to a start/end segment
- the shaft end caps are not stretched during normal placement
- `MainWindow` uses a runtime target dictionary for extended image pins
- image-pin extensions no longer rely on the placeholder `Line` plus centered bitmap approach
- tests cover selection and geometry sanity
- `scripts/verify.ps1` passes on a machine with the .NET 6 SDK installed
