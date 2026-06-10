---
status: active
owner: agent
started: 2026-06-08
requirements_ref: pin-rendering-improvements
parent_program: composite-pins-program.md
---

# Pin Rendering Improvements Plan

Program dashboard: [composite-pins-program.md](composite-pins-program.md)

## Overview

Two independent improvements:

1. **Shaft anti-aliasing** — reduce jagged edges / interpolation artifacts on pin shafts and heads when they are rotated and scaled.
2. **Perspective depth sorting** — render "interior" pins (physically closer to the viewer in the implied 3D scene) on top of the pins they overlap. ✅ Completed 2026-06-10.

---

## Part 1 — Shaft/Head Anti-Aliasing

**Status:** Part 1A completed 2026-06-10. Part 1B remains optional pending visual review.

### Root-cause analysis

The current rendering pipeline applies a `MatrixTransform` (RenderTransform) per layer on top of a native-sized `Image` element.  Three things fight against clean edges:

| Issue | Location | Effect |
|---|---|---|
| `SnapsToDevicePixels="True"` on the UserControl | `CompositePinMarker.xaml` root | Snaps the element's layout boundary to the physical-pixel grid. When a continuous MatrixTransform rotates the image, this discrete snap fights the smooth sub-pixel positions implied by the transform, producing a jagged outline on the shaft. |
| `UseLayoutRounding="True"` on the UserControl | `CompositePinMarker.xaml` root | Rounds element sizes to the nearest integer pixel; same problem as above. |
| Per-segment clip boundary aliasing | `CompositePinRenderPlanBuilder.cs` → `CompositePinMarker.xaml.cs` | The clip polygon for each shaft segment (TipCap, Body, HeadCap) is a PathGeometry clipped at a line. WPF clips geometry without anti-aliasing the clip edge itself, so the seam boundary between segments can appear as a hard-aliased line, especially when viewed at an angle. The existing 1.5 px `SeamOverlapPx` eliminates *gaps* but not *aliasing at the clip edge*. |
| `BitmapScalingMode="HighQuality"` may fall back | `CompositePinMarker.xaml` | `HighQuality` asks WPF to use a higher-quality path, but it is not guaranteed to apply when the element also has a non-uniform MatrixTransform (especially body-stretch). `Fant` mode explicitly specifies the resampling algorithm. |

### Approach A — XAML property changes ✅ (2026-06-10)

Changes to `CompositePinMarker.xaml`:

1. Remove `SnapsToDevicePixels="True"` from the `<UserControl>` element.
2. Remove `UseLayoutRounding="True"` from the `<UserControl>` element.
3. Change `RenderOptions.BitmapScalingMode` from `"HighQuality"` to `"Fant"` on all four `<Image>` elements (`ShaftTipCapImage`, `ShaftBodyImage`, `ShaftHeadCapImage`, `HeadImage`).
4. Add `RenderOptions.EdgeMode="Unspecified"` explicitly on each `<Image>` (default, but explicit is safer — this is the mode that allows anti-aliased geometry).

Expected outcome: the shaft silhouette edges and the clip seam lines should be noticeably smoother. The head and tip should also improve.

Risk: *none* — these are rendering hints only; they do not change geometry or layout behaviour.

### Approach B — Pre-rasterization (if A is insufficient)

Instead of letting WPF composite four independently-transformed images at draw time, render the full composite pin to a single off-screen `RenderTargetBitmap` and display it as one image with no clip or transform.

New method on `CompositePinMarker` (or extracted as a helper):

```
RenderTargetBitmap FlattenToRasterBitmap(double dpiX = 96, double dpiY = 96)
{
    var rtb = new RenderTargetBitmap(
        (int)Math.Ceiling(Width), (int)Math.Ceiling(Height),
        dpiX, dpiY, PixelFormats.Pbgra32);
    rtb.Render(RootCanvas);   // Renders all layers with their MatrixTransforms
    rtb.Freeze();
    return rtb;
}
```

In `ApplyRenderPlanToMarker` (MainWindow), after `SetCompositeImages` succeeds:
- Call `FlattenToRasterBitmap()`.
- Replace `marker.Content` with a simple `Image { Source = flatBitmap, Width = plan.Width, Height = plan.Height }`.
- Continue to set `Canvas.SetLeft/Top` using `plan.TipAnchorLocal`.

Benefits:
- All four layers are composited in software at full sub-pixel accuracy before display.
- No clip-boundary aliasing — the clips are resolved during the off-screen render.
- The on-screen display is a single GPU-textured quad with no per-pixel compositing overhead.

Costs:
- Slightly higher peak memory (one RGBA bitmap per pin at screen resolution).
- Hover/click animations currently animate `MarkerTransform` (ScaleTransform on RootCanvas). After flattening, `MarkerTransform` is on the now-unused `RootCanvas`, not the displayed image. The animation would need to be applied to the replacement `Image` element or to a `ScaleTransform` wrapping it.
- The `DebugOverlayCanvas` would not appear in the flattened result. Keep it as a separate overlay canvas positioned on top of the replacement image if debug overlays are needed.

> **Recommendation**: Implement Approach A first (one XAML file, no code changes). If visual quality is still not acceptable at steep angles, implement Approach B.

---

## Part 2 — Perspective Depth Sorting

**Status:** Completed 2026-06-10. `CompositePinDepthSorter` sorts model-level `CompositePinDepthItem` records to preserve layer boundaries; MainWindow adapts visible composite markers into depth items and applies increasing `ZIndex` values.

### Definition

Given two placed pins A and B, their shaft direction vectors (tip → head, in screen coordinates) are:

```
d_A = normalize(extendedPos_A − tipPos_A)
d_B = normalize(extendedPos_B − tipPos_B)
```

**Pin A is "interior" to pin B** when both of the following hold:

1. `dot(d_A, d_B) > 0`  — the shafts point in broadly the same direction (angle < 90°).
2. `dot(tipPos_A − tipPos_B, d_B) < 0`  — A's tip lies "behind" B's tip along B's axis direction (A is lower/further-back in B's axial direction).

When A is interior to B, A should render **on top** of B (higher `ZIndex`).

**Example (user-given):** both pins vertical (heads above tips, `d = (0, −1)` in screen coords).  Pin A's tip is below pin B's tip on screen.  Condition 1: `dot((0,−1),(0,−1)) = 1 > 0` ✓.  Condition 2: `dot(tipA − tipB, (0,−1)) = −(tipA.Y − tipB.Y) < 0` when `tipA.Y > tipB.Y` ✓.  So A renders on top, giving proper perspective (lower pins appear in front).

### Algorithm

1. Collect all visible individual markers whose `Content` is a `CompositePinMarker`.
2. For each such marker, compute:
   - `tipScreen` = `(Canvas.GetLeft(marker) + plan.TipAnchorLocal.X, Canvas.GetTop(marker) + plan.TipAnchorLocal.Y)`
   - `shaftDir`  = `normalize(plan.JoinAnchorLocal − plan.TipAnchorLocal)` (already in local canvas = screen space because the canvas has no additional transform)
3. Build a directed acyclic graph (DAG): add edge `A → B` meaning "A must have higher ZIndex than B" whenever A is interior to B (as defined above).
4. Detect and break cycles (possible with near-perpendicular or crossing pins) using tip screen-Y as tiebreaker: if A.tipScreen.Y > B.tipScreen.Y assign edge A → B.
5. Topological-sort the graph to get a render order list `[p₀, p₁, … pₙ₋₁]` where `p₀` is the bottommost (background) pin.
6. Assign `Panel.SetZIndex(pᵢ.marker, BaseZIndex + i)` for `i = 0 … N−1`, where `BaseZIndex = 2000` preserves compatibility with the existing z-layer strategy.

The dragged-marker override (z=2000 during drag, z=0 on release) should temporarily override the sorted value and restore it on release.

### Data needed per marker

The `CompositePinRenderPlan` stored on `CompositePinMarker.RenderPlan` provides everything needed:

| Needed | Source |
|---|---|
| `tipScreen` | `Canvas.GetLeft(marker) + plan.TipAnchorLocal.X / Y` |
| `shaftDir` | `normalize(plan.JoinAnchorLocal − plan.TipAnchorLocal)` |

No new fields are needed on `ManualLayoutMarker` or any model.

### New code components

#### `CompositePinDepthSorter` (new service class, `Services/`)

```csharp
public class CompositePinDepthSorter
{
    // Returns items in ascending ZIndex order (index 0 = background).
    public IReadOnlyList<CompositePinDepthItem> Sort(IEnumerable<CompositePinDepthItem> items);

    // Exposed for tests.
    public static bool IsInterior(
        Point tipA, Vector dirA,
        Point tipB, Vector dirB);
}
```

`IsInterior` implements the two dot-product conditions.  `Sort` builds the graph, resolves cycles by screen-Y tiebreaker, and returns a topological order.

#### Integration in `MainWindow`

Add a private helper:

```csharp
private void ApplyCompositePinDepthSort()
{
    var compositeMarkers = _individualMarkers
        .Where(m => m.Visibility == Visibility.Visible
                 && m.Content is CompositePinMarker)
        .ToList();

    var sorted = _depthSorter.Sort(compositeMarkers);

    for (int i = 0; i < sorted.Count; i++)
        Panel.SetZIndex(sorted[i], 2000 + i);
}
```

Call sites:
- End of `ApplyManualLayout`, after the main loop and after `ReapplyPendingOverrides`.
- End of `ReassignPins` (the "Reassign Pins" button path).
- When drag ends (`OnMarkerDragEnd`), after restoring z-index to 0: re-run sort to restore depth-correct indices.

### Edge-case handling

| Case | Handling |
|---|---|
| Fewer than 2 composite pins | Skip sort; no relationships to compute. |
| Cycle in graph | Break by secondary key: `tipScreen.Y` (higher Y = closer to viewer = higher z). If still tied, use `Location.Name` alphabetically for determinism. |
| Non-composite (legacy) markers mixed in | Leave their ZIndex untouched (they are at z=0 or z=1000 and are already below composite pins). |
| Dragged marker | During drag: override to ZIndex=2000+N+1 (above all). On release: restore via `ApplyCompositePinDepthSort()`. |
| Pins with angle > 90° between shafts | `dot(d_A,d_B) ≤ 0` → neither is interior to the other; no edge in graph; their relative order is determined by other relationships or left at insertion order. |

---

## File change summary

| File | Change | Part |
|---|---|---|
| `Views/CompositePinMarker.xaml` | Remove `SnapsToDevicePixels`, `UseLayoutRounding`; change `BitmapScalingMode` to `Fant`; add `EdgeMode` | 1A |
| `Views/CompositePinMarker.xaml.cs` | (Approach B only) Add `FlattenToRasterBitmap()` | 1B |
| `MainWindow.xaml.cs` | (Approach B only) Flatten after `SetCompositeImages`; update animation targets | 1B |
| `Models/CompositePinDepthItem.cs` | New model-level sort input so Services do not reference Views | 2 |
| `Services/CompositePinDepthSorter.cs` | New file — depth sort service | 2 |
| `MainWindow.xaml.cs`, `MainWindow.CompositePins.partial.cs`, `MainWindow.LayoutEditor.partial.cs` | Add sorter field; add `ApplyCompositePinDepthSort()`; call after viewport update, layout apply, reassign, shaft override, and drag-end | 2 |
| `Tests/CompositePinDepthSorterTests.cs` | New file — unit tests for `IsInterior` and `Sort` | 2 |

---

## Implementation order

1. **Part 1A** — XAML changes only (5 min, zero risk). ✅ Completed 2026-06-10; automated structural test added.
2. **Part 2** — depth sorter service + MainWindow integration. ✅ Completed 2026-06-10; service tests added.
3. **Part 1B** — only if Part 1A did not sufficiently improve quality.
