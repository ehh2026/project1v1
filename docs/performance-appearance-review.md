# Performance & Appearance Review — InteractiveWorldMap

**Date:** 2026-06-23
**Scope:** Rendering and animation hot paths, with emphasis on zoom in/out smoothness.
**Method:** Static read of the zoom/animation/marker-placement code paths. No GUI profiling was run (cannot launch WPF here), so the "impact" estimates are reasoned from the code, not measured. Items are ordered by expected benefit-to-effort.

---

## TL;DR — the three changes most likely to make zoom feel smooth

1. **Logging runs synchronously on the UI thread, every log call** — `FileLogger.WriteLog` does `Console.WriteLine` *and* `Debug.WriteLine` before it enqueues (`Services/FileLogger.cs:82-88`). It is only "non-blocking" for the *file* write.
2. **Verbose debug logging is switched on in the shipped config** — `LogRadialExtensionCalculation`, `LogRadialExtensionAngles`, `LogRadialExtensionOverlaps`, `LogMarkerPositioning` are all `true` (`visual-config.json:68-74`). Combined with #1, every zoom frame emits dozens of synchronous console writes.
3. **Animation timing uses `DateTime.Now`** (`MainWindow.Navigation.partial.cs:445,453,457`), whose resolution on Windows is ~15.6 ms. At a 16 ms frame budget this quantizes progress to frame boundaries and produces visible stutter. Use `System.Diagnostics.Stopwatch`.

These three are low-risk and together address the most likely causes of "not smooth."

---

## P0 — High impact, low risk

### 1. Synchronous console/debug writes on every log call
`Services/FileLogger.cs:82-88`
```csharp
private static void WriteLog(string message)
{
    Console.WriteLine(message);                 // synchronous, on caller (UI) thread
    System.Diagnostics.Debug.WriteLine(message); // synchronous, on caller (UI) thread
    _queue.TryAdd(message);                       // the only truly async part
}
```
`Console.WriteLine` is slow when a console/redirect is attached, and `Debug.WriteLine` is slow whenever a debugger or listener is attached. Both run inline on the thread that logged — which during a zoom is the UI thread inside the `CompositionTarget.Rendering` handler. The class comment claims it is "non-blocking," but only the file write is.

**Fix:** Move the `Console.WriteLine`/`Debug.WriteLine` into the background `WriterLoop` (the consumer already pulls every message), or drop them entirely in Release builds. Enqueue-only on the hot path.

**Impact:** Large. This is the single cheapest win for frame smoothness.

### 2. Debug logging enabled in the shipped config
`visual-config.json:68-74`
```json
"LogRadialExtensionCalculation": true,
"LogRadialExtensionAngles": true,
"LogRadialExtensionOverlaps": true,
"LogMarkerPositioning": true,
```
These gate the dense `_logger.LogInfo(...)` calls in `MarkerPlacementOrchestrator.Compute` (e.g. `Services/MarkerPlacementOrchestrator.cs:72-78,100-106,129-143`) and elsewhere. `Compute` runs **once per animation frame** via `UpdateMarkerPositions()`, so each of those flags multiplies per-frame log volume — which then pays the synchronous cost from #1.

**Fix:** Default all four to `false` for normal runs. Leave them as opt-in toggles for debugging.

**Impact:** Large in combination with #1.

### 3. Unconditional per-marker log inside the line factory
`Views/ExtensionLineRenderer.cs:417`
```csharp
_logInfo($"    Created pin extension line pair: ({start.X:F1},{start.Y:F1}) ... core={coreWidth:F1}px");
```
`CreatePinLinePair` is called for every extended marker, and in drawn mode the manual layout is rebuilt **every frame** (see P1 #5), so this logs N markers × ~30 frames per zoom — none of it gated behind a debug flag.

**Fix:** Gate behind `_visualConfig.Debug.LogRadialExtensionCalculation` (the renderer already receives the config), or remove it.

### 4. `DateTime.Now` for animation clock → frame jitter
`MainWindow.Navigation.partial.cs:445,453,457-458`
```csharp
var animStart = DateTime.Now;
...
var now = DateTime.Now;
var elapsed = (now - animStart).TotalMilliseconds;
var progress = Math.Min(1.0, elapsed / AnimationDurationMs);
```
`DateTime.Now` has ~15.6 ms granularity and reads wall-clock/timezone state. Progress therefore advances in ~15 ms steps regardless of real elapsed time, so the keyframe picker keeps selecting the same frame for a beat and then jumps — visible micro-stutter.

**Fix:** Use a single `Stopwatch` started before the loop; read `sw.Elapsed.TotalMilliseconds`. Sub-millisecond resolution, monotonic, cheaper.

**Impact:** Medium-high; directly affects perceived smoothness.

---

## P1 — Medium impact

### 5. Manual layout is fully rebuilt every animation frame (drawn mode)
`MainWindow.Navigation.partial.cs:443,474,492` call `onFrameUpdated` → `ApplyManualLayoutDuringAnimation` → `ApplyManualLayout` (`MainWindow.LayoutEditor.partial.cs:511`). Each invocation:
- `_extensionLineRenderer.Clear()` removes all line children from the canvas (`ExtensionLineRenderer.cs:66-75`),
- recreates them via `CreatePinLinePair`, allocating **2 `Line` + 2 `SolidColorBrush` + 2 `DropShadowEffect`** per marker (`ExtensionLineRenderer.cs:369-419`),
- re-runs `CreateLayoutApplications` + `BuildApplyInstructions` and re-reads geometry path strings,
- calls `ReapplyPendingOverrides` and `ApplyCompositePinDepthSort`.

Doing this ~30× per zoom is heavy allocation churn → GC pressure → dropped frames. The shaft geometry only needs its endpoints moved, not the whole object graph rebuilt.

**Fix (incremental):** During animation, create the extension `Line`s once and per frame only update `X1/Y1/X2/Y2` (and reuse brushes/effects). `MoveLineEndpoint` already shows the per-marker update shape; an animation-specific "reposition existing lines" path would avoid the clear/recreate cycle. This mirrors what the composite/offset path already does for composite pins.

**Impact:** Medium-high for drawn-mode zoom specifically.

### 6. `DropShadowEffect` proliferation
- Every pin ball carries a `DropShadowEffect` (`Views/PinMarker.xaml:28-34`).
- Every extension shaft applies a `DropShadowEffect` to **both** the outline and the core line (`ExtensionLineRenderer.cs:401-413`) — two blurred shader passes per shaft.

`DropShadowEffect` with a non-zero `BlurRadius` is a per-pixel shader pass that defeats caching and re-renders as the element moves (which is every frame during zoom). With many markers this is a real GPU cost.

**Fix options:**
- Use one shadow per shaft, not two (the dark outline already reads as a rim; the core shadow is largely hidden).
- Consider a cheaper static look: a pre-baked shadow in the asset, or `BitmapCache` on the *individual marker* (not the whole map — that was the bug you just removed) so the shadow rasterizes once and only translates during motion.
- Optionally drop shadows entirely while `IsAnimating` and restore them on settle.

**Impact:** Medium; scales with marker count.

### 7. Keyframe pre-render does synchronous disk I/O on the UI thread
`MainWindow.Navigation.partial.cs:504-563` (`PreRenderKeyframes`) runs **before** the animation starts and, on a cache miss, encodes 30 PNGs to disk synchronously (`AnimationFrameCache.SaveFrame`, `Services/AnimationFrameCache.cs:154-173`); on a hit it decodes 30 PNGs from disk (`TryLoadFrame:122-149`). All on the UI thread. This is the stall you see the first time you zoom to a new cluster (and a smaller stall on subsequent zooms from decode). Each frame is also copied into a `WriteableBitmap` (`:539,550`), realizing all 30 into CPU memory up front.

**Fix:**
- Generate keyframes lazily/async, or at least move the PNG encode off the UI thread (the frames are `Freeze()`d and thread-safe).
- Reconsider whether disk persistence earns its cost: the in-memory `CroppedBitmap`→`TransformedBitmap` is cheap to build and the GPU resamples it fine; the PNG round-trip may be slower than just rendering. Worth measuring a "no disk cache" variant.
- Avoid the `new WriteableBitmap(...)` copy where a frozen `TransformedBitmap` would display identically.

**Impact:** Medium; mostly the pre-zoom hitch rather than mid-animation jank.

### 8. O(n) marker lookups inside per-frame loops
`ApplyIndividualPlacements` (`MainWindow.xaml.cs:499-500`) does `_individualMarkers.FirstOrDefault(m => m.Location.Name == placement.LocationName)` for each placement — O(n²) per frame. Same pattern in `ApplyCompositePinsToNormalPlacements` (`MainWindow.CompositePins.partial.cs:498`) and `ExtensionLineRenderer.Apply` (`:120`, `markers.FirstOrDefault(...)`).

**Fix:** Build a `Dictionary<string, LocationMarker>` once per `UpdateMarkerPositions` pass (or keep one cached, invalidated on marker add/remove) and look up by name.

**Impact:** Low for small marker counts, grows quadratically — worth it before the dataset grows.

### 9. Per-frame LINQ allocation in `UpdateMarkerPositions`
`MainWindow.xaml.cs:455-471` allocates two `.Where().Select().ToList()` collections plus the orchestrator allocates several dictionaries/lists (`MarkerPlacementOrchestrator.cs:92-98,123-203`) every frame. Steady-state allocation during a 30-frame animation feeds the GC.

**Fix:** Reuse buffers, or short-circuit during animation (the animating branch in `Compute` already skips extension work — the visible-marker projection could be cached for the duration of the animation since visibility doesn't change mid-zoom).

**Impact:** Low-medium; reduces GC-induced frame drops.

---

## P2 — Lower impact / polish

### 10. Redundant work in `MapDisplayControl.UpdateViewport`
`Views/MapDisplayControl.xaml.cs:86-112` re-sets `RenderOptions.SetBitmapScalingMode` / `SetCachingHint` on every call and builds a fresh `CroppedBitmap`. The render options only need to be set once (at load). Minor.

### 11. Closest-keyframe search is a linear scan each frame
`MainWindow.Navigation.partial.cs:461-467` scans all 30 keyframes every frame to find the nearest progress. Since `keyframeProgress` is linear and monotonic, `frameIndex = (int)Math.Round(progress * (keyframeCount-1))` is exact and O(1). Trivial CPU, but removes a loop from the hot path and is clearer.

### 12. Console writes also happen at startup/Initialize
`FileLogger.cs:37,45` write to console during init; harmless but part of the same "console on the hot path" pattern — fold into the Release-build gating from #1.

---

## Appearance findings

### A1. Animated frames are NearestNeighbor; settled frame is Fant — visible "pop"
The animation keyframes and the live map crop use `BitmapScalingMode.NearestNeighbor` (`MapDisplayControl.xaml.cs:102`; keyframes are plain `TransformedBitmap`), while the final high-quality region uses `Fant` from the full-res source (`ZoomedRegionCache.cs:213`). So during the zoom the map looks blocky and then snaps to crisp at settle. That snap can read as "it reverted/changed."

**Options:** render keyframes with `LowQuality`/`Linear` instead of `NearestNeighbor` for smoother in-flight frames, or cross-fade the last keyframe into the high-quality region at settle so the sharpening isn't an instant pop.

### A2. Sub-pixel marker rendering
`Views/PinMarker.xaml:4-5` sets `SnapsToDevicePixels="False"` and `UseLayoutRounding="False"`. Markers are positioned at fractional `Canvas.Left/Top` (e.g. `MarkerPlacementOrchestrator` math), so the ball/shaft can land on half-pixels and look slightly soft even when static. If crisp edges are wanted at rest, enable `UseLayoutRounding` on the marker (keep it off during animation if it causes shimmer).

### A3. Shadow opacity inconsistency
`visual-config.json:40` sets `ShadowOpacity: 0.55`, but `ExtensionLineRenderer.cs:409` overrides it with `Math.Max(pinConfig.ShadowOpacity, 0.45)` and `PinMarker.xaml:33` hard-codes `Opacity="0.55"` in XAML (not bound to config). The drawn-pin ball shadow therefore ignores the config value. Minor, but means tuning `ShadowOpacity` doesn't fully take effect.

### A4. "Marker not found for location" warning is structural, not just noise
`ExtensionLineRenderer.cs:143` warns when an extension references a location whose marker isn't in the supplied list. This is a *different* site from the `LayoutEditorController` warning already downgraded. During zoom it can fire for markers outside the current view. Consider gating it behind the debug flag like the rest, or downgrading to info, so the console isn't doing extra synchronous writes (ties back to P0 #1) for an expected condition.

---

## Suggested order of work

1. P0 #1–#4 (logging async/off + Stopwatch). Cheap, low risk, biggest smoothness gain. Verify visually.
2. P1 #5 (incremental line reposition during animation) — the main per-frame allocation hog in drawn mode.
3. P1 #6 (shadow passes) and #7 (async keyframe I/O).
4. P1 #8–#9 (lookup/allocation) as the dataset grows.
5. Appearance A1/A2 once smoothness is confirmed.

> Note: items in P1/P2 touch the animation loop and marker placement, which are not unit-testable here and need on-device visual verification. Recommend changing them one at a time and eyeballing a zoom cycle after each, given the rendering subtleties already encountered in this area.
