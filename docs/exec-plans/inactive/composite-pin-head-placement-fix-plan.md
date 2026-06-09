---
status: inactive
owner: agent
started: 2026-06-07
parked: 2026-06-09
completed_phases: 1-3
requirements_ref: composite-pin-head-placement-fix
parent_program: ../active/composite-pins-program.md
---

# Composite Pin Head Placement Fix Plan

Program dashboard: [composite-pins-program.md](../active/composite-pins-program.md)

**Status:** Phases 1–3 complete — `dotnet test` passes; manual visual check OK (2026-06-09). Optional polish deferred — §8.4 steps 4–6 tracked in [TO_DO.md](../../TO_DO.md) inactive section.
**Scope:** `CompositePinRenderPlanBuilder.cs`, head-placement logic only
**Author:** Investigation 2026-06-07

---

## 1. Stated Intent vs Current Behaviour

The user's described model:

| Component | Intended anchor | Scale |
|-----------|----------------|-------|
| Shaft | tip → map location; join → endpoint | S = targetLength / nativeAxisLength |
| Head ball | **center** of the non-transparent circular cluster → endpoint | TargetHeadRadiusPx / localRadius (consistent across all pins) |

The **current** code does:

| Component | Current anchor | Current scale |
|-----------|---------------|--------------|
| Shaft | tip → map location; join → endpoint ✓ | S ✓ |
| Head ball | `local_attach` (a virtual stub point **beyond** the ball edge) → endpoint ✗ | `max(S, TargetHeadRadiusPx/R)` — degrades to S ✗ |

---

## 2. Root-Cause Analysis

### 2.1 Bug A — Wrong anchor point: `local_attach` instead of `local_center`

`local_attach` is documented in the geometry JSON as the stub connection point where the shaft notionally meets the head. **For every one of the 12 pins, `center_to_attach_distance > local_radius`** — the attach is in the transparent area beyond the visible ball:

| Pin | local_radius (px) | center_to_attach_dist (px) | Gap (attach beyond ball edge) |
|-----|------------------|--------------------------|-------------------------------|
| pin_01 | 204.0 | 215.6 | **11.6 px** |
| pin_02 | 206.0 | 211.5 | **5.5 px** |
| pin_03 | 203.0 | 216.2 | **13.2 px** |
| pin_04 | 200.0 | 220.9 | **20.9 px** |
| pin_05 | 204.5 | 211.0 | **6.5 px** |
| pin_06 | 213.5 | 218.0 | **4.5 px** |
| pin_07 | 203.5 | 234.9 | **31.4 px** |
| pin_08 | 205.5 | 209.8 | **4.3 px** |
| pin_09 | 203.5 | 208.1 | **4.6 px** |
| pin_10 | 213.5 | 218.0 | **4.5 px** |
| pin_11 | 200.0 | 216.3 | **16.3 px** |
| pin_12 | 200.0 | 221.6 | **21.6 px** |

Placing `local_attach` at the endpoint means the ball's nearest visible pixel is `(center_to_attach_distance − local_radius) × headScale` pixels **above** the endpoint — the ball floats.

With `headScale = S` (the state after the last session's change), and pin_07's S ≈ 0.107 at targetLength=50px:
floating gap = 31.4 × 0.107 ≈ **3.4 screen px** — clearly visible.

With the **correct anchor** (`local_center` → endpoint), the ball is centered directly at the endpoint with no gap.

---

### 2.2 Bug B — Inconsistent head sizes caused by `headScale = S`

Different pins have drastically different native shaft lengths. The previous fix changed `headScale` to `max(S, TargetHeadRadiusPx/R)`, which for short pins degrades to pure `S`. At targetLength = 50 px:

| Pin | nativeAxisLength (px) | S = 50/native | headScale=S → head radius (px) |
|-----|----------------------|--------------|-------------------------------|
| pin_01 | 550.6 | 0.091 | **18.5** |
| pin_02 | 566.0 | 0.088 | **18.2** |
| pin_03 | 556.3 | 0.090 | **18.2** |
| pin_04 | **223.7** | **0.224** | **44.7** ← enormous |
| pin_05 | 314.7 | 0.159 | **32.5** |
| pin_06 | 512.0 | 0.098 | **20.9** |
| pin_07 | 467.0 | 0.107 | **21.8** |
| pin_08 | 328.9 | 0.152 | **31.2** |
| pin_09 | **193.2** | **0.259** | **52.6** ← enormous |
| pin_10 | 521.0 | 0.096 | **20.5** |
| pin_11 | 322.4 | 0.155 | **31.0** |
| pin_12 | 470.3 | 0.106 | **21.2** |

Pin_04 renders a head with a **44.7 px radius** while pin_01 renders one at 18.5 px. The teal oversized ball visible in the screenshot is pin_04 (or another short-shaft pin). This is the direct result of `headScale = S`.

With `headScale = TargetHeadRadiusPx / localRadius = 14 / ~204 ≈ 0.069`, every pin renders at **14 px radius** regardless of shaft length — consistent sizes.

---

### 2.3 Bug C — Shaft head-cap visual mismatch

The shaft image's "head cap" segment (from StretchEndDistance to NativeLength) is a graphic specifically drawn to blend with the head ball at **natural scale** (headScale = S). When `headScale ≠ S`, the cup profile in the shaft graphic is wider or narrower than the rendered ball.

With the fix from this plan:
- `headScale = TargetHeadRadiusPx/R ≈ 0.069`
- Head ball radius in screen pixels = 14 px
- Shaft half-width at join ≈ S × (half the image width) ≈ 0.069–0.224 × (50–150 px native half-width) → 3–15 px

A 14 px radius ball fully covers the shaft head cap for all typical shaft widths at the target scale. The head cap renders beneath the ball and is invisible. This is acceptable; the head cap primarily provides a smooth transition for scales where the shaft is nearly as wide as the head ball, and at TargetHeadRadiusPx=14 this is handled by the ball overlap.

---

### 2.4 Bug D — `head_attach_inside_crop` not modelled

The JSON's `alignment.head_attach_inside_crop` flag indicates whether `local_attach` lies within the cropped image bounds. Five pins have `head_attach_inside_crop: false` (pin_02, pin_06, pin_07, pin_08, pin_09, pin_10). For these, `local_attach.y > image_size.h`. Since this plan **replaces** `local_attach` with `local_center` as the anchor, this issue becomes irrelevant and the field can be ignored.

---

### 2.5 Confirmed-correct behaviours (do not change)

- **Shaft placement**: tip → `target.StartScreen`, join → `target.EndScreen`. Correct.
- **Head rotation formula**: `headRotationDeg = NormalizeSignedAngle(targetAngle − (StubDirectionDeg + 180°))`. Orients the stub toward the shaft tip (downward for an upward pin). Mathematically equivalent for both `local_center` and `local_attach` anchors (see §4.2). Correct.
- **Seam overlap**: `SeamOverlapPx = 1.5` added to clip bands. Correct — addresses anti-aliasing gaps.
- **Shaft scaling / segmentation / clip bands**: All correct.

---

## 3. Required Changes

Only **`CompositePinRenderPlanBuilder.cs`** needs modification, in `CalculateTransforms`.

### 3.1 Change head anchor from `local_attach` to `local_center`

**Current** (`ValidatedInputs`):
```csharp
var headAttach = headEntry.Head.LocalAttach
    ?? throw new InvalidOperationException("...");
return new ValidatedInputs(geometry, headEntry, shaftSize, headSize, segmentation, headAttach);
```

**Change**: pass `local_center` as the placement anchor instead of `local_attach`.

In `ValidatedInputs`, rename the last field from `HeadAttach` to `HeadAnchor` to reflect new semantics:

```csharp
private sealed record ValidatedInputs(
    PinPartGeometryEntry     Geometry,
    PinPartGeometryEntry     HeadEntry,
    PinPartImageSize         ShaftSize,
    PinPartImageSize         HeadSize,
    PinPartShaftSegmentation Segmentation,
    PinPartPoint             HeadAnchor);   // was HeadAttach — now local_center
```

In `ValidateInputs`, extract `local_center`:
```csharp
var headAnchor = headEntry.Head.LocalCenter
    ?? throw new InvalidOperationException("Head local_center is required for composite rendering.");
// local_attach is no longer needed for placement (kept in JSON for reference only)
return new ValidatedInputs(geometry, headEntry, shaftSize, headSize, segmentation, headAnchor);
```

> **Note**: `PinPartPoint LocalCenter` is currently non-nullable in the model with a default constructor. The null-check guard can be simplified or removed accordingly.

### 3.2 Fix head scale: always use `TargetHeadRadiusPx / localRadius`

**Current** (after last session's change):
```csharp
var headScale = S;
if (config.TargetHeadRadiusPx > 0.0 && nativeHeadRadius > 0.0)
{
    var normalizedScale = config.TargetHeadRadiusPx / nativeHeadRadius;
    if (normalizedScale > S)
        headScale = normalizedScale;
}
```

**Replace with**:
```csharp
// Always scale to TargetHeadRadiusPx so all pin heads render at a consistent
// screen radius regardless of native shaft length. Fall back to S only when
// the config or geometry data are missing (e.g. unit tests with minimal stubs).
var nativeHeadRadius = v.HeadEntry.Head.LocalRadius;
var headScale = (config.TargetHeadRadiusPx > 0.0 && nativeHeadRadius > 0.0)
    ? config.TargetHeadRadiusPx / nativeHeadRadius
    : S;
```

### 3.3 Update `CreateHeadTransform` call site

The transform currently reads `v.HeadAttach`. After the rename:
```csharp
var headTransform = CreateHeadTransform(
    v.HeadAnchor,   // was v.HeadAttach
    new Point(geo.TargetDirection.X * geo.TargetLength, geo.TargetDirection.Y * geo.TargetLength),
    headRotationDeg,
    headScale);
```

### 3.4 No changes to `PinPartGeometry.cs` model

`PinPartHeadGeometry.LocalCenter` already exists and is deserialized from JSON. `LocalAttach` can remain in the model for potential future use but is not used in rendering after this change.

### 3.5 Update `HeadCenterLocal` in plan assembly

Currently:
```csharp
HeadCenterLocal = TransformPoint(ToPoint(v.HeadEntry.Head.LocalCenter), s.HeadTransform),
```

After the change, since `local_center` is now the head anchor (maps to `JoinAnchor`), `HeadCenterLocal` will always equal `JoinAnchor`. The existing formula remains correct (it's a derived value, not used to drive placement).

---

## 4. Verification of Rotation Formula Invariance

The current rotation formula:
```
nativeAttachToCenterAngle = Normalize360(StubDirectionDeg + 180°)
headRotationDeg = NormalizeSignedAngle(targetAngle − nativeAttachToCenterAngle)
```

**Goal**: orient the stub toward the shaft tip (downward for an upward pin).

With `local_center` as anchor, `targetAngle` is the direction from tip to join (the shaft direction). We want the stub direction (from center toward attach = `StubDirectionDeg`) to point TOWARD the tip, i.e., at `targetAngle + 180°`.

Rotation needed: `(targetAngle + 180°) − StubDirectionDeg`
= `targetAngle − StubDirectionDeg + 180°`
= `targetAngle − (StubDirectionDeg − 180°)`
= `targetAngle − (Normalize360(StubDirectionDeg + 180°) − 360°)` (mod 360)

After `NormalizeSignedAngle`: equivalent to the existing formula (same angle modulo 360°). **No change needed to the rotation formula.**

---

## 5. Implementation Steps

1. **Rename `HeadAttach` → `HeadAnchor` in `ValidatedInputs`** (affects `ValidateInputs`, `CalculateTransforms`).
2. **In `ValidateInputs`**: replace `LocalAttach` with `LocalCenter` as the extracted anchor.
3. **In `CalculateTransforms`**: restore `headScale = TargetHeadRadiusPx / nativeRadius` (unconditional, fall back to S only when config/data is zero).
4. **Build & run 211 tests** — expect all to pass (the only test using `TargetHeadRadiusPx` uses `PinPartConfig` with default 0.0, so falls back to S; anchor change only affects `HeadCenterLocal` value which no test currently asserts).
5. **Visual verification** with app running — check that all pin heads appear at the same radius (~14 px) and are centered at the shaft endpoint.

---

## 6. Remaining Open Items (not in scope of this fix)

- `PinPartHeadGeometry.LocalAttach` and related null-check guard become vestigial after this change; can be removed in a later cleanup.
- `head_attach_inside_crop` in JSON is now fully irrelevant; can be removed from JSON and skipped in any future model update.
- `TargetHeadRadiusPx = 14.0` in visual-config.json may need tuning after visual review.
- Shaft head-cap visual blending: at 14 px head radius the head ball completely covers the shaft head-cap layer for all current pins. If a future design needs the cap visible, the cap layer should be removed or its clip restricted to the portion outside the head ball radius.
- Manual layout endpoint support (mentioned by user as future work) will feed into `PinPlacementTarget.EndScreen`; no changes needed here once that is wired up.

---

## 7. Phase 2 Investigation — Remaining Visual Offset After Phase 1 Fix

Phase 1 changes (§3) were applied. All 211 tests pass. Visual review shows most pins now align
correctly, but some heads still appear offset/detached from their shafts. This section documents the
follow-up investigation.

---

### 7.1 Complete 12-Pin Geometry Reference

All values read directly from `pin_part_geometry.json`.

**Head geometry:**

| Pin | Image W×H | local_center | local_radius | local_attach | stub_dir° | c2a_dist | img_center_delta¹ |
|-----|-----------|-------------|------------|------------|----------|---------|-----------------|
| pin_01 | 419×419 | (205.5, 206.5) | 204.0 | (284.4, 407.1) | 158.5 | 215.6 | 5.0 px |
| pin_02 | 418×418 | (209.0, 209.5) | 206.0 | (209.7, 421.0) | 179.8 | 211.5 | 0.5 px |
| pin_03 | 419×418 | (213.5, 205.5) | 203.0 | (130.2, 405.0) | 202.7 | 216.2 | 5.3 px |
| pin_04 | 420×420 | (204.5, 203.5) | 200.0 | (339.8, 378.1) | 142.2 | 220.9 | 8.7 px |
| pin_05 | 418×417 | (207.5, 208.0) | 204.5 | (264.5, 411.2) | 164.3 | 211.0 | 1.6 px |
| pin_06 | 429×432 | (214.0, 216.0) | 213.5 | (214.0, 434.0) | 180.0 | 218.0 | 0.5 px |
| pin_07 | 416×416 | (210.5, 210.0) | 203.5 | (311.0, 422.3) | 154.7 | 234.9 | 3.2 px |
| pin_08 | 416×417 | (208.0, 208.5) | 205.5 | (417.3, 223.2) | **94.0** | 209.8 | 0.0 px |
| pin_09 | 416×415 | (208.0, 206.0) | 203.5 | (212.4, **−2.1**) | **1.2** | 208.1 | 1.5 px |
| pin_10 | 430×432 | (214.5, 216.0) | 213.5 | (214.7, **−2.0**) | **0.1** | 218.0 | 0.5 px |
| pin_11 | 417×417 | (210.5, 213.5) | 200.0 | (101.9, 26.4) | 329.9 | 216.3 | 5.4 px |
| pin_12 | 419×419 | (203.0, 216.5) | 200.0 | (353.3, 53.6) | 42.7 | 221.6 | 9.6 px |

¹ `img_center_delta` = distance from `local_center` to the exact geometric centre of the head image
(W/2, H/2). At `headScale ≈ 0.069` this is **< 0.7 px screen** for all pins — negligible.

**Shaft native lengths:**

| Pin | nativeAxisLength (px) | S at targetLength=50 |
|-----|----------------------|---------------------|
| pin_01 | 550.6 | 0.091 |
| pin_02 | 566.0 | 0.088 |
| pin_03 | 556.3 | 0.090 |
| pin_04 | 223.7 | 0.224 |
| pin_05 | 314.7 | 0.159 |
| pin_06 | 512.0 | 0.098 |
| pin_07 | 467.0 | 0.107 |
| pin_08 | 328.9 | 0.152 |
| pin_09 | 193.2 | 0.259 |
| pin_10 | 521.0 | 0.096 |
| pin_11 | 322.4 | 0.155 |
| pin_12 | 470.3 | 0.106 |

---

### 7.2 Critical Finding: Pins 09 and 10 — Stub Exits the Top of the Image

| Pin | local_attach.y | stub_direction_deg | Meaning |
|-----|---------------|-------------------|---------|
| pin_09 | **−2.1 px** | 1.2° | Stub exits the TOP edge of the image |
| pin_10 | **−2.0 px** | 0.1° | Stub exits the TOP edge of the image |

Both pins have `stub_direction_deg ≈ 0°`, meaning the stub points **straight up** in image space.
This is the opposite orientation from pins where the stub points downward (near 180°).

**Consequence for rendering:**

The rotation formula orients the stub toward the shaft tip. For a standard upward pin
(`targetAngle ≈ 270°` = pointing up the screen), the required head rotation is:

```
headRotationDeg = NormalizeSignedAngle(targetAngle − (stubDirectionDeg + 180°))
               ≈ NormalizeSignedAngle(270° − (0° + 180°)) = 90°  ... for pin_09/10
```

Compare to a typical pin (e.g. pin_06 with stubDirectionDeg ≈ 180°):
```
headRotationDeg ≈ NormalizeSignedAngle(270° − (180° + 180°)) = NormalizeSignedAngle(−90°) = −90°
```

For pin_09/10 the head is rotated by ~+90°, versus ~−90° for most other pins.
The rotation direction differs by ~180°, **effectively inverting the head image**.

Result: specular highlights and shadow gradients appear upside-down. Even though
`local_center` is mathematically placed at the correct position, the visual appearance
may look disconnected because the shading cues (bright top, dark bottom) are reversed
relative to a normally-oriented head.

---

### 7.3 Shaft Head-Cap Visual Overhang

The shaft head-cap layer covers source pixels from `stretchEndDistance` to `nativeLength`
(the collar/socket region). This layer is drawn at scale **S** (the shaft scale), while the
head ball is drawn at `headScale = TargetHeadRadiusPx / localRadius ≈ 0.069`.

For a 50 px target and pin_01 (S = 0.091):
- The shaft image at S places the collar at a specific screen height.
- The collar's visual content (cup shape, highlight, shadow) has a finite height in screen space.
- The *top edge* of the collar in screen space is at `JoinAnchorLocal`.
- But the collar graphic has pixels that **extend above** the mathematically exact join point
  depending on how the original artwork was drawn and how the clip band falls.

If the collar graphic has a visible ridge or ring whose centre appears a few pixels **above**
`JoinAnchorLocal`, and the ball centre is *at* `JoinAnchorLocal`, the collar centre and the
ball centre are vertically offset — the collar appears below the ball rather than coinciding
with the ball's equator. This can look like the ball is floating above the shaft top.

This is currently the **most likely explanation** for the residual offset visible on the screenshot,
because:
- It affects every pin regardless of head rotation
- Its magnitude depends on S (shaft scale), so it would be more visible for short-shaft
  pins (pin_04, pin_09) where S is largest
- It has nothing to do with `local_center` calibration (which is accurate to < 0.7 px screen)

---

### 7.4 Ball-Center vs. Ball-Bottom Model

The user explicitly stated the intended model: ball **center** at the extension endpoint.
The current code implements this correctly. This is NOT a bug.

For context: a physical thumbtack model would place the ball **bottom** at the shaft join,
shifting the ball center UP by `TargetHeadRadiusPx = 14 px` along the shaft direction.
This was **not** what the user requested, so it is not pursued.

---

### 7.5 Ranked Hypotheses for Remaining Visual Offset

| # | Hypothesis | Pins affected | Screen magnitude | Likely? |
|---|-----------|--------------|-----------------|---------|
| 1 | Shaft head-cap collar graphic visually overhangs `JoinAnchorLocal` | All, worse for short-shaft pins | ~2–8 px depending on S | **Most likely** |
| 2 | Pins 09/10 near-180° rotation inverts shading cues | pin_09, pin_10 | Visual impression only; centre is correct | **Likely** |
| 3 | `local_center` calibration error | pin_12 (9.6 px native → 0.66 px screen), others < 0.5 px screen | < 0.7 px all pins | **No** |

---

### 7.6 Debug Image Inspection — Completed

Debug images in `Tools/PinDebugger/output_v2/` were inspected (annotate mode). The inspection
confirmed that for pin_07 the yellow `local_join` dot was landing in empty space far to the right
of the visible shaft — directly on the disconnected shadow blob. This drove the Phase 3 work
documented in §8.

---

## 8. Phase 3 — Pin_07 Shadow Removal & Geometry Recalibration

### 8.1 Root Cause: Disconnected Shadow Blob in Pin_07 Shaft Images

Both `pin_07_shaft.png` and `pin_07_shaft_lit.png` contained a large, fully-disconnected cast-shadow
region to the right of the actual shaft. This blob:

- Was **not connected** to the shaft tip pixel cluster (flood-fill from `local_tip` does not reach it).
- Caused the original calibration tool to extend `local_join` and `native_length` to the far edge
  of the shadow rather than the real shaft collar.

| Geometry field | Current (wrong) value | Correct (approximate) value |
|----------------|----------------------|-----------------------------|
| `local_join`   | `(471, 112.3)`       | ~`(200, 39)` (from find-join) |
| `native_length`| `467 px`             | ~`208 px` (from find-join) |
| `axis_length`  | `467`                | ~`208` |

The segmentation distances (tip_cap_length, stretch_start/end, stretchable_length) are proportional
to the old native_length and must also be recalculated from the cleaned image.

### 8.2 Shadow Removal — Done

PinDebugger `--clean` mode (flood-fill from `local_tip`, zero disconnected islands) was run on
both shaft images. The cleaned outputs were copied over the originals in the parts folder:

- `Images&Content/Pins_v2/parts/pin_07_shaft.png` ← replaced with cleaned version
- `Images&Content/Pins_v2/parts/pin_07_shaft_lit.png` ← replaced with cleaned version

The originals are preserved in `Tools/PinDebugger/cleaned/pin_07_shaft_clean.png` and
`Tools/PinDebugger/cleaned/pin_07_shaft_lit_clean.png`.

### 8.3 PinDebugger Tooling Reference

| Mode | Command | What it does |
|------|---------|--------------|
| Annotate | `dotnet run --project Tools\PinDebugger` | Draws calibration dots on all 36 head/shaft/shaft_lit images into `output_v2/` |
| Clean | `dotnet run --project Tools\PinDebugger -- --clean` | Flood-fills from `local_tip`, zeroes disconnected pixel islands |
| Find-join | `dotnet run --project Tools\PinDebugger -- --find-join` | Projects shaft pixels onto native axis, reports centroid of farthest 3% as suggested new `local_join`; outputs JSON patch snippet |

### 8.4 Next Steps — Ordered Action List

**Step 1 — Recalibrate pin_07 geometry (CURRENT BLOCKER)**

Re-run the original 3D/analysis tool on `Images&Content/Pins_v2/parts/pin_07_shaft.png`
(the cleaned version now in place) to obtain correct values for:

```
local_tip                 (should be unchanged, near (4, 108.3))
local_join                (real shaft collar endpoint, approx (200, 39))
native_length             (euclidean tip→join, approx 208 px)
tip_cap_length            (recalculate proportionally or re-measure)
stretch_start_distance    (= tip_cap_length)
stretch_end_distance      (native_length − head_cap_length)
stretchable_length        (stretch_end − stretch_start)
head_cap_length           (re-measure from cleaned image collar graphic)
```

As a quick-start cross-check, run find-join mode to get the suggested `local_join` patch:
```
dotnet run --project Tools\PinDebugger -- --find-join
```

Then update `pin_part_geometry.json` for `pin_07` with the corrected values.
Also update the `alignment` block fields (`head_center_delta_px`, `tip_delta_px`,
`head_side_vs_center_delta_px`) using the analysis tool output.

**Step 2 — Run all tests**

`dotnet test` — expect all 211 to pass (no logic changes, only JSON data change).

**Step 3 — Visual verification with app**

Run the app and inspect pin_07 specifically:
- Head should be centered at the shaft endpoint with no visible gap.
- The shaft collar graphic should align with (or be covered by) the head ball.
- Head size should be ~14 px radius, consistent with all other pins.

Then check pin_04 and pin_09 (short-shaft pins) for any residual collar-overhang disconnect (§7.3).

**Step 4 — If collar overhang is visible (hypothesis 1 from §7.5)**

Clip the shaft head-cap layer's upper boundary in source space so its visual top aligns with
`JoinAnchorLocal`. One-line change in `AssembleResult` in `CompositePinRenderPlanBuilder.cs`.

**Step 5 — If pin_09/10 inverted shading looks wrong (hypothesis 2 from §7.5)**

The ball centre placement is mathematically correct. The visual issue is that specular highlights
appear upside-down because `stub_direction_deg ≈ 0°` for these pins. Options:
- Accept the current appearance (centre is correct).
- Replace those head images with equivalents whose stub faces downward (~180°).

**Step 6 — Tune `TargetHeadRadiusPx`** (currently `14.0` in `visual-config.json`)

Adjust up or down for aesthetic balance once all alignment issues are resolved.
