# Pin Image Placement Strategy - Feasibility Assessment

> **Archived / historical.** Superseded by [pin-parts-composite-placement-plan.md](../../exec-plans/active/pin-parts-composite-placement-plan.md). Kept for reference only.

## Current State Summary

### What We Have

1. **12 extracted pin PNG images** at `Images&Content/Pins/threshold_*/pin_NN.png`
   - Extracted from a downloaded `Pins.jpg` source image (not generated/rendered by us)
   - 5 threshold variants exist (230-250) from tuning the extraction; only one set is needed for production (235 recommended)
   - Transparent backgrounds, ~400-600px wide, varying aspect ratios
   - Each pin has a **different angle and orientation** baked into the image:
     - Pins 01, 03: ~30-40 degrees tilted left (tip bottom-right, head top-left)
     - Pin 02: nearly vertical, slight right tilt
     - Pin 04: large teal pin, nearly vertical, slight right tilt
     - Pin 05: nearly vertical, slight right tilt
     - Pin 06: red/orange, nearly vertical
     - Pin 07: large blue, tip pointing bottom-left, head right
     - Pin 08: pink, tip pointing right (nearly horizontal)
     - Pin 09: teal, tip pointing straight up (inverted - head below, tip above)
     - Pin 10: blue, nearly vertical
     - Pin 11: pink, large, nearly vertical
     - Pin 12: red, tip pointing upper-right, head lower-left

2. **Coordinate data** in `Coordinates for map.xlsx` - pixel positions (X, Y) on an 8198x5542 map

3. **Radial extension system** (`RadialExtensionCalculator.cs`) that computes:
   - `OriginalPosition`: the map coordinate where the pin "touches" the map (tip)
   - `ExtendedPosition`: where the marker label/head should appear
   - `Angle`: the angle from center (0=north, clockwise)
   - Extension line length: 50px default, 13px minimum
   - Dense-group endpoint placement is still handled automatically in code, including angle ordering, angle nudging, convergence checks, and line-intersection adjustment passes

4. **Manual layout editor** infrastructure that stores per-marker `originalPosition`, `extendedPosition`, `angle`, and `lineLength`
   - The code path is still present and enabled in config
   - Saved layouts are loaded when entering a zoomed cluster view and can be edited, saved, re-applied, and deleted
   - The old statement that `manual-layouts.json` did not exist is stale; the current code can create and persist layouts through `ManualLayoutManager`

5. **Current pin rendering** crops pins from the master `pins.jpg` using rectangles defined in `visual-config.json`, scales them to 3% (ScaleFactor=0.03), and positions them at map coordinates. The "connection point" is hardcoded to bottom-center at 90% height.

---

## The Proposed Idea

Annotate each pin image with two pixel coordinates (tip and head-center), then overlay them so the tip aligns with a map location's `OriginalPosition` and the head aligns with the `ExtendedPosition`. Optionally match pin angle/length to the radial extension angle/length by finding the best-fit pin, with potential scaling/rotation/stretching.

## Feasibility: What Works

### The core geometry is sound
Each pin image does have a clear visual axis from tip to head. You *can* define a `(tipX, tipY)` and `(headX, headY)` for each pin in its own image coordinate space. This gives you:
- **Pin angle** = `atan2(headX - tipX, tipY - headY)` (matching the app's 0=north convention)
- **Pin length** = Euclidean distance between tip and head

The radial extension system already computes an `OriginalPosition` (tip), `ExtendedPosition` (head), and `Angle` for each location. So the mapping is conceptually clean:

```
Pin tip pixel  -->  OriginalPosition (map coordinate)
Pin head pixel -->  ExtendedPosition (radial endpoint)
```

### Matching by angle and length is reasonable
With 12 pin variants covering a range of angles (roughly vertical, tilted left, tilted right, nearly horizontal, even one inverted), you have decent angular coverage. A best-fit selection algorithm is straightforward:

```
For each location:
  desired_angle = radial extension angle
  desired_length = radial extension line length
  For each pin:
    pin_angle, pin_length = from annotated coordinates
    angle_error = angular_distance(desired_angle, pin_angle)
    length_error = |desired_length - pin_length| / desired_length
    score = w1 * angle_error + w2 * length_error
  Select pin with lowest score
```

### Scaling is safe and easy
WPF `Image` controls support `ScaleTransform` natively. Uniform scaling (same X and Y factor) to match the desired length to the pin's natural length preserves the pin's visual quality well, especially since these are already being rendered at 3% of their original ~500px size (~15px rendered). Scaling between 50-150% of that base size would look fine.

### Rotation is feasible but has caveats
WPF `RotateTransform` around the tip point is straightforward. However:
- **Lighting/shadow inconsistency**: The pins have baked-in specular highlights and shadows. Rotating a pin 90 degrees makes the lighting look wrong (highlight on the wrong side, shadow underneath instead of beside).
- **For small rotations (< 30 degrees)**, this is barely noticeable at 3% scale.
- **For large rotations (> 45 degrees)**, it will look visibly off.

---

## Feasibility: What's Problematic

### 1. Limited angular coverage without rotation
The 12 pins cover roughly these angles (estimated from visual inspection):

| Pin | Approximate native angle (tip-to-head, 0=north CW) |
|-----|------------------------------------------------------|
| 01  | ~210 (head upper-left, tip lower-right)              |
| 02  | ~160 (head upper-right, tip lower-center)            |
| 03  | ~200 (similar to 01)                                 |
| 04  | ~350 (nearly vertical, head top-right)               |
| 05  | ~170 (head top, tip bottom-right)                    |
| 06  | ~180 (nearly vertical, head top)                     |
| 07  | ~120 (head right, tip lower-left)                    |
| 08  | ~90 (head left, tip right - nearly horizontal)       |
| 09  | ~180 (inverted - special case)                       |
| 10  | ~190 (head top-left, tip bottom)                     |
| 11  | ~170 (nearly vertical)                               |
| 12  | ~320 (head lower-left, tip upper-right)              |

That's heavy coverage in the 150-210 range (mostly "pointing down") and gaps in 0-90 and 240-320. The radial extension system distributes angles evenly around 360 degrees for dense clusters, so without rotation many locations would get poorly-matched pins.

### 2. Stretching is a bad idea
Non-uniform scaling (different X and Y factors) to match length while preserving angle would distort the spherical pin heads into ellipses and make the metallic shafts look warped. At any visible size this will look wrong. **Recommendation: avoid stretching entirely.**

### 3. Annotation effort
Manually identifying (tipX, tipY) and (headX, headY) for 12 pins is tedious but doable, especially with a small Python script that shows each image and lets you click two points. It's a one-time cost. (Only one threshold variant needs annotation - pick 235 and use that set.)

### 4. Existing endpoint data is split between automatic calculation and optional saved layouts
The repo still has both of these mechanisms:

- automatic runtime endpoint calculation from `RadialExtensionCalculator`
- optional persisted manual overrides via `ManualLayoutManager`

So the relevant question is no longer "does endpoint data exist at all", but rather which source of truth the new composite-pin pipeline should use in each state:

- default: use the algorithmic endpoints directly
- when a saved manual layout is active: use the saved `ExtendedPosition` values instead

That means the new part-based pin pipeline should be designed to consume the same endpoint data the drawn-pin system already consumes, rather than inventing a separate layout concept.

---

## Recommended Approach

### Option A: Rotation + Uniform Scaling (Recommended)

Rather than matching pins by angle (which has coverage gaps), **always rotate** the pin to match the desired angle, and **uniformly scale** to match the desired length.

**Why this works better:**
- At 3% scale (~15px rendered height), lighting inconsistencies from rotation are nearly invisible
- You get perfect angular alignment for every location
- Uniform scaling preserves pin proportions
- You can still randomly select which pin *color/style* to use for visual variety
- Simpler algorithm - no angle-matching heuristic needed

**Algorithm:**
```
For each location with radial extension:
  1. Select a pin (random or by color preference)
  2. Load pin's annotated tip and head coordinates
  3. Calculate pin's native angle and length
  4. Calculate desired angle (from radial extension) and desired length
  5. rotation = desired_angle - pin_native_angle
  6. scale = desired_length / pin_native_length * base_scale_factor
  7. Apply RotateTransform(rotation) around tip point
  8. Apply ScaleTransform(scale) uniformly
  9. Position so tip coincides with OriginalPosition on map
```

**WPF implementation sketch:**
```csharp
var pinImage = new Image { Source = pinBitmap };
var transformGroup = new TransformGroup();

// Scale uniformly
double scale = desiredLength / pinNativeLength * baseScale;
transformGroup.Children.Add(new ScaleTransform(scale, scale, tipX, tipY));

// Rotate around tip
double rotation = desiredAngle - pinNativeAngle;
transformGroup.Children.Add(new RotateTransform(rotation, tipX * scale, tipY * scale));

pinImage.RenderTransform = transformGroup;

// Position tip at map coordinate
Canvas.SetLeft(pinImage, mapX - tipX * scale);
Canvas.SetTop(pinImage, mapY - tipY * scale);
```

### Option B: Angle-Matching + Minimal Rotation (Fallback)

If you want to preserve the baked-in lighting as much as possible:

1. Annotate all 12 pins with tip/head coordinates
2. For each location, find the pin whose native angle is closest to the desired angle
3. Apply only the small residual rotation needed (ideally < 20 degrees)
4. Apply uniform scale for length

**Downside:** Some locations will still need large rotations due to coverage gaps, or you'll get visually inconsistent pin selections (e.g., all pins in one quadrant use the same color because only one pin covers that angle range).

### Option C: Pre-Render Rotated Variants (NOT Recommended)

The idea would be to use a script (e.g., Pillow in Python) to pre-generate rotated versions of each pin at fixed angle increments, then pick the closest variant at runtime.

**This does NOT actually solve the lighting problem.** Rotating pixel data in a script produces the exact same result as rotating it at runtime in WPF - the specular highlights and shadows are baked into the pixels and rotate with them either way. A photograph of a 3D object rotated 90 degrees still has the light coming from the wrong direction.

The only ways to get truly correct lighting at all angles would be:
- **Re-render from a 3D model** at each desired angle (we don't have one - these pins were extracted from a downloaded JPG)
- **Find/purchase a pin asset pack** photographed or rendered at multiple angles with consistent lighting
- **Use a simpler pin style** (flat/matte design with no specular highlights or directional shadows) where rotation is visually indistinguishable

Since Option C offers no quality advantage over Option A while adding ~288 PNG files and more complex asset management, it is not recommended.

---

## Implementation Steps (for Option A)

### Phase 1: Pin Annotation (Python script)
1. Write a script that displays each pin image and lets you click the tip and head center
2. Output a JSON file with `{pin_id, tip: {x, y}, head: {x, y}, native_angle, native_length}` for each pin
3. Pick one threshold variant (235 recommended based on extraction quality)

### Phase 2: Config Extension
1. Extend `PinImageInfo` model to include `TipX`, `TipY`, `HeadX`, `HeadY` properties
2. Update `visual-config.json` with the annotated coordinates
3. Add a `PinImageFolder` config option to point to extracted PNGs instead of master crop rects

### Phase 3: Rendering Update
1. Modify `ImagePinMarker` to load individual PNG files instead of cropping from master
2. Add `RotateTransform` and `ScaleTransform` based on desired vs native angle/length
3. Update `GetConnectionPoint()` to return the actual annotated tip position (not hardcoded 90% height)
4. Wire into radial extension system: use `RadialExtension.Angle` as the desired angle

### Phase 4: Integration
1. In `MainWindow.xaml.cs`, when creating an `ImagePinMarker`:
   - Get the `RadialExtension` for this location (angle + length)
   - Pass desired angle/length to the marker
   - Let the marker compute and apply its own transforms
2. For locations without radial extensions (isolated pins), use a default angle (e.g., pointing "into" the map from the nearest edge)

---

## Risk Assessment

| Risk | Severity | Mitigation |
|------|----------|------------|
| Lighting looks wrong after rotation | Low at 3% scale | Invisible at small sizes; for large zoom, would need new source assets (3D renders or flat design) |
| Pin tip/head annotation is inaccurate | Medium | Use script with zoom + crosshair; test overlay |
| Scaling makes pins too small/large | Low | Clamp scale factor to 0.5x-2.0x of base |
| Dense clusters have overlapping pin heads | Medium | Already handled by radial extension nudging |
| Performance with 37+ transformed images | Low | WPF handles this fine; freeze BitmapSources |
| Transform math off by rotation direction | Low | Test with one pin first; WPF uses clockwise degrees |

## Conclusion

**The idea is feasible and good.** The best path is Option A (rotate + uniform scale every pin to match radial extension geometry). The annotation step is a small one-time cost. At the rendered size (~15px), rotation artifacts will be invisible. The radial extension system already provides the exact angle and length data needed. The main work is:

1. ~1 hour: Python annotation script + annotate 12 pins
2. ~2 hours: Extend models and config
3. ~3 hours: Update rendering pipeline
4. ~1 hour: Testing and tuning

The angle-matching idea (Option B) is a valid refinement to add later if visual quality at higher zoom levels matters, but it's not necessary for the first pass.

Note: Option C (pre-rendered rotated variants) was considered but does not actually solve lighting artifacts - rotating pixel data in a script produces the same result as rotating at runtime. Since the pins were extracted from a downloaded photograph (not rendered from a 3D model), there is no way to get correct lighting at arbitrary angles from these source assets. If lighting fidelity at high zoom ever becomes important, the solution would be new source assets (3D-rendered pin set or flat/matte pin designs), not pre-rotation of the existing ones.
