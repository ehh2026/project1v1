# Map Render Quality Design

**Date:** July 1, 2026
**Status:** Approved for implementation planning

## Goal

Prevent thin map lines and letter strokes from dropping out in the settled
full-map view while preserving the current full-screen `Fill` presentation and
the existing zoom behavior.

## Evidence and Current Rendering Paths

The checked-in display map is `World Map Extra Large.jpg` at 8198 x 5542
pixels. On the current 2560 x 1440 display, the settled full-map path reduces
roughly 3.2 source pixels to each horizontal display pixel and 3.85 source
pixels to each vertical display pixel.

`MapDisplayControl` currently requests `NearestNeighbor` scaling in both XAML
and code. That filter selects source samples without area averaging. Thin
features can therefore disappear when no selected source sample lands on them.
An equivalent screen-resolution rendering reproduced the reported broken
stroke in the `U` of `SOUTH AMERICA`; a high-quality downsample retained the
stroke coverage.

The relevant paths are different:

- **Settled full map:** `World Map Extra Large.jpg` -> `CroppedBitmap` ->
  `MapImage` stretched to the full control. This currently uses
  `NearestNeighbor`.
- **Zoom animation:** keyframes crop the display image and materialize a
  `TransformedBitmap`/`WriteableBitmap`. The scaling policy is not explicit at
  the transformation boundary, while `MapImage` still carries the
  nearest-neighbor override.
- **Settled zoom:** `ZoomedRegionCache` loads a crop from the 16397 x 11085
  `World Map 1976.jpg` source and materializes it with `Fant`. At the configured
  55x zoom this is generally an upscale from a small source crop to the display,
  not the strong downscale used by the full map.

## Rendering Policy

Use an explicit quality policy for each phase:

| Phase | Scaling mode | Reason |
|---|---|---|
| Settled full map | `Fant` | Area-aware high-quality reduction preserves subpixel stroke coverage. |
| Zoom animation keyframes | `Linear` | Avoids nearest-neighbor dropout and blockiness without paying settled-quality cost on every keyframe. |
| Settled zoom | `Fant` | Preserve the existing high-resolution crop path and its high-quality materialization. |

`MapDisplayControl.xaml` will keep:

```xml
Stretch="Fill"
SnapsToDevicePixels="True"
```

The local `RenderOptions.EdgeMode="Aliased"` override will be removed.
`EdgeMode` controls how WPF rasterizes drawing edges; it is not the correct
quality control for bitmap minification. The default `Unspecified` edge mode
avoids forcing aliasing, while `BitmapScalingMode` remains the authoritative
bitmap-resampling setting.

The root-window `HighQuality` setting is not relied upon because local image
settings and bitmap materialization boundaries must remain explicit.

## Full-Map Presentation Contract

This work intentionally preserves `Stretch="Fill"` and the current behavior:

- the complete map fills the display;
- no letterboxing is added;
- no source content is cropped;
- the existing non-uniform aspect-ratio stretch remains;
- marker placement, hit testing, manual layouts, and viewport coordinate
  mapping are unchanged.

`Uniform`, `UniformToFill`, and other display choices are deferred. They require
shared destination-bounds geometry for the image, marker canvases, hit targets,
screen/source conversion, and animation frames. They must not be introduced as
a cosmetic `Image.Stretch` change.

## Animation-Frame Cache Compatibility

Changing keyframe resampling changes cached pixel output. If the existing disk
`AnimationFrameCache` remains in use when this work lands, increment its cache
version so previously materialized frames cannot bypass the new policy.

The separate open decision about retaining or removing animation-frame disk I/O
stays owned by `zoom-performance-appearance-plan.md`. This rendering-quality
slice does not resolve that performance decision.

## Deferred Settled Full-Map Render Cache

Do not add a monitor-sized settled-map cache in this slice. First measure the
cost of direct `Fant` rendering on target hardware.

Track a follow-up that becomes actionable when either condition is observed:

- a settled full-map update or resize consistently takes more than 33 ms; or
- returning to or resizing the full map produces repeated UI-thread frame gaps
  greater than 33 ms.

The follow-up cache must be keyed by source-image identity, physical output
pixel dimensions, DPI scale, and presentation mode. It must be invalidated on
source changes, monitor/DPI changes, and relevant window-size changes.

## Error Handling and Fallbacks

The existing `UpdateViewport` exception boundary remains. This slice does not
add a silent fallback to nearest-neighbor: a rendering failure should retain
the existing logged/debug-visible failure path rather than reintroduce the
quality defect.

Animation-cache version cleanup remains best-effort as it is today. A cache
cleanup failure may reduce performance but must not prevent uncached frame
generation.

## Testing

Automated coverage will enforce:

- `MapImage` remains `Stretch="Fill"`;
- `SnapsToDevicePixels` remains enabled;
- `EdgeMode="Aliased"` is absent;
- settled `MapDisplayControl` rendering uses `Fant` and no longer resets
  nearest-neighbor during viewport updates;
- keyframe transformation explicitly selects `Linear` before bitmap
  materialization;
- the animation-frame cache version changes with the keyframe pixel policy;
- `ZoomedRegionCache` continues to select `Fant` for settled high-resolution
  zoom crops;
- no full-map viewport or coordinate-mapping behavior changes.

Manual Windows verification will cover full-map settle, zoom-in animation,
settled zoom, zoom-out animation, and return-to-full-map at 1080p, 1440p, and
4K where hardware is available. The `SOUTH AMERICA` label and representative
thin grid/coastline details are the visual reference regions.

The full `.\scripts\verify.ps1` gate must pass before completion.

## Ownership and Modularity

- `Views/MapDisplayControl.xaml(.cs)` owns settled display-image presentation.
- `MainWindow.Navigation.partial.cs` owns animation-frame materialization.
- `Services/ZoomedRegionCache.cs` owns settled high-resolution zoom crops.
- `Services/AnimationFrameCache.cs` owns compatibility/versioning for
  materialized animation frames.

No new service or cross-layer dependency is needed. The changes are small
policy corrections in existing owners, and touched C# files remain below the
800-line limit.

## Documentation and Completion

Implementation must:

- update the rendering-quality items in
  `docs/exec-plans/active/zoom-performance-appearance-plan.md`;
- narrow the corresponding `docs/TO_DO.md` bullet as phases complete;
- retain separate backlog bullets for aspect/display modes and the evidence-
  gated settled full-map render cache;
- add a user-visible `[Unreleased]` changelog entry;
- run focused tests and `.\scripts\verify.ps1`;
- archive the active exec plan only when all of its remaining unrelated phases
  are complete.

## Out of Scope

- `Uniform`, `UniformToFill`, letterboxing, or cropping modes;
- Runtime Tuning controls for map presentation;
- changing the map source assets or JPEG encoding;
- adding a settled full-map render cache now;
- resolving whether the animation-frame disk cache should be retained;
- changing zoom level, viewport math, marker geometry, or manual layouts.
