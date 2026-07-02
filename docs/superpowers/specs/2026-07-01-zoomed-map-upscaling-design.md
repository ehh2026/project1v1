# Zoomed Map Upscaling Design

**Date:** July 1, 2026
**Status:** Approved for implementation planning

## Goal

Improve settled zoomed-map quality by generating the cached image at the
monitor's physical pixel size, making cached output unambiguously compatible
with its source and rendering settings, and providing several resampling
algorithms for direct comparison in Runtime Tuning.

The work must reduce avoidable softness and stale-cache risk without changing
viewport geometry, marker placement, the configured 55x zoom, or the `Linear`
animation-frame policy.

## Current Behavior and Constraints

The application already anti-aliases the settled zoomed map.
`ZoomedRegionCache.ScaleBitmap` applies WPF `BitmapScalingMode.Fant`, which
blends neighboring source pixels and therefore creates intermediate colors at
high-contrast edges. The JPEG source also contains its own antialiasing and
compression gradients. `RenderOptions.EdgeMode` is not a replacement for a
bitmap resampling algorithm and will remain unspecified.

The current settled zoom path is:

1. `MainWindow.Navigation.partial.cs` calculates a source crop using the
   half-resolution 8198 x 5542 display image.
2. `ZoomedRegionCache` maps that crop into the 16397 x 11085
   `World Map 1976.jpg` source when available.
3. The crop is resized with `Fant`.
4. The resulting bitmap is stored as PNG and assigned to
   `MapDisplay.DisplayImage.Source`.

At `ZoomScale = 55` and a 2560 x 1440 output, the full-resolution crop is only
approximately 358 x 202 pixels and is enlarged about 7.1x. Better filters can
change perceived sharpness and edge character, but cannot reconstruct source
detail that is not present.

Two avoidable correctness risks exist:

- `ActualWidth` and `ActualHeight` are WPF device-independent units. At a DPI
  scale above 100%, using them directly as bitmap pixel dimensions creates a
  settled bitmap smaller than the physical render target and WPF enlarges it
  again.
- The cache key covers center, zoom, and output dimensions, but not the active
  source identity, DPI, resampler, or resampler parameters. A bitmap generated
  from an older source or the half-resolution fallback can therefore remain
  eligible after rendering inputs change.

## Chosen Architecture

### Physical-pixel render request

`MainWindow` remains the composition owner. At settle time it obtains
`VisualTreeHelper.GetDpi(MapDisplay)` and builds a request containing:

- center and zoom level;
- physical output width and height, calculated by rounding the control's
  device-independent dimensions multiplied by `DpiScaleX` and `DpiScaleY`;
- both DPI scale values;
- selected resampling mode;
- the existing half-resolution crop rectangle.

Logical viewport and marker calculations continue using device-independent
units. Only the pixels generated for `MapImage.Source` use physical dimensions,
so this change does not alter source/screen coordinate conversion.

Zero, non-finite, or unavailable dimensions do not enter the cache. The caller
retains the current displayed crop and logs a warning until valid dimensions
are available.

### Configuration and Runtime Tuning

Add `ZoomedMapRenderConfig` under `VisualConfig` with a string-serialized
`ResamplingMode` enum. The supported values are:

| Mode | Purpose |
|---|---|
| `Fant` | Existing WPF output and compatibility baseline. |
| `Lanczos3` | Sharpest classical option; may show ringing near black/white edges. |
| `MitchellNetravali` | Balanced sharpness with less ringing than Lanczos3. |
| `Bicubic` | Conservative smooth cubic baseline. |
| `BicubicSharpened` | Bicubic followed by restrained unsharp masking. |

`Fant` remains the checked-in default until on-device comparisons justify a
different shipping default.

The Map category in Runtime Tuning receives a `Zoomed map resampling` combo
box. Applying a different mode while settled in a zoomed view immediately
re-enters `ShowZoomedView` through the existing tuning replay path. The mode is
part of the cache key, so switching modes neither clears unrelated cache files
nor reuses pixels from another mode. Save and Reload persist the selected enum
through `visual-config.json`.

The control does not expose sharpening strength in this slice. A fixed,
conservative kernel keeps the comparison matrix bounded and avoids turning the
tuning panel into an image editor.

### Resampler boundary

Create a focused `ZoomedMapResampler` service. It accepts a cropped
`BitmapSource`, output pixel dimensions, and a `ZoomedMapResamplingMode`, and
returns a frozen `BitmapSource`.

- `Fant` retains the existing WPF `TransformedBitmap` materialization.
- The four comparison modes use an internal, separable CPU resampler over a
  normalized 32-bit pixel buffer.
- Horizontal and vertical contributor tables are computed once per resize.
- Source coordinates outside the crop clamp to the nearest edge pixel.
- Kernel weights are normalized per destination sample.
- Output channels clamp to `[0, 255]`.
- The sharpened mode applies a small fixed unsharp mask after bicubic resize,
  clamps overshoot, and does not alter image dimensions.

The implementation will not add ImageSharp, SkiaSharp, Magick.NET, or another
graphics dependency. This avoids licensing questions, native deployment
payloads, and package-specific output changing underneath the cache. The
service boundary keeps a future library-backed implementation possible without
changing `ZoomedRegionCache` or the tuning contract.

Resampling runs only when a settled cache entry is missing. Animation frames
remain `Linear`, and cached settled images remain PNG.

### Cache identity

Replace the positional cache-key argument list with one immutable render
descriptor. Its canonical key input includes:

- cache schema version;
- center X and Y using round-trip invariant formatting;
- zoom level using round-trip invariant formatting;
- physical output width and height;
- DPI scale X and Y using round-trip invariant formatting;
- resampling enum;
- a resampler-policy version, including the fixed sharpen parameters;
- selected source role: `full-resolution` or `fallback`;
- selected source's normalized absolute path;
- source file length;
- source file `LastWriteTimeUtc` ticks.

The canonical input is SHA-256 hashed for the filename. Exact round-trip
formatting replaces the current one-decimal coordinate formatting so distinct
requests do not collapse accidentally.

`ZoomedRegionCache` receives both the full-resolution and fallback source paths
at construction. It fingerprints only the source actually used. Consequently,
a cache entry produced while the full-resolution file was missing cannot be
returned once that file becomes available.

The existing cache-version file remains as coarse cleanup. The stronger
content-addressed filename provides correctness even if old files cannot be
deleted. Bumping the cache schema version retires all current version-7 output.

## Data Flow

1. Zoom animation completes with the existing `Linear` keyframe.
2. `ShowZoomedView` reads the current DPI and converts the map control's
   device-independent size to physical output pixels.
3. It constructs the settled render request using the selected tuning mode.
4. `ZoomedRegionCache.TryLoadRegion` derives the source role and fingerprint,
   then looks for the exact request key.
5. On a miss, the cache maps the crop into the chosen source and delegates the
   resize to `ZoomedMapResampler`.
6. The frozen result is encoded to PNG, cached, and displayed.
7. A mode change repeats steps 3-6 and leaves animation rendering unchanged.

## Error Handling and Fallbacks

- If the full-resolution source is missing or fails to load, use the existing
  half-resolution fallback and key it explicitly as fallback output.
- If a custom mode fails, log the mode and exception, generate `Fant` output,
  and cache it under a key that identifies the actual `Fant` fallback rather
  than mislabeling it as the requested custom algorithm.
- If PNG persistence fails after successful rendering, display the in-memory
  result and log the cache-write failure.
- A corrupt cached PNG is deleted best-effort and regenerated once. If deletion
  fails, render uncached rather than repeatedly trying to decode it.
- Unknown config enum strings fall back to `Fant` through the existing config
  validation path and produce a warning.

## Comparison and Acceptance Criteria

The implementation must produce a repeatable comparison set from the same
source rectangle at 1080p, 1440p, and 4K physical output dimensions where
hardware or generated fixtures are available. Compare:

- letter interiors and diagonals in `SOUTH AMERICA`;
- thin country borders and grid/coastline strokes;
- blockiness;
- edge-transition width;
- ringing or halos;
- text deformation;
- first-generation settle time;
- cache-hit settle time.

No mode is declared universally best in code. Runtime Tuning exists so the
human reviewer can choose based on the actual gallery monitor. `Fant` remains
the default until that review is recorded.

Automated acceptance requires:

- DPI conversion produces exact physical output dimensions;
- 100% DPI preserves existing output dimensions;
- cache keys differ by source fingerprint, source role, output size, DPI,
  algorithm, and policy version;
- fallback-generated cache entries cannot mask a newly available full source;
- every mode returns the requested dimensions and a frozen bitmap;
- every mode is deterministic for a fixed fixture;
- constant-color and one-pixel images remain stable;
- borders are clamped without transparent or dark seams;
- custom kernels do not produce channel overflow;
- the sharpened mode increases fixture edge acutance without exceeding its
  fixed halo threshold;
- tuning Apply, Save, and Reload preserve the selected mode;
- animation keyframes remain `Linear`;
- viewport, marker, and hit-target geometry remain unchanged.

The full `.\scripts\verify.ps1` gate must pass. Final acceptance also requires a
live Windows comparison because unit metrics cannot decide which edge character
looks best on the target display.

## Ownership and Modularity

- `Models/ZoomedMapRenderConfig.cs` owns the persisted enum and config object.
- `Models/ZoomedRegionRenderRequest.cs` owns immutable cache/render inputs.
- `Services/ZoomedMapResampler.cs` owns pixel resampling and sharpening.
- `Services/ZoomedRegionCache.cs` owns source selection, crop mapping, cache
  identity, PNG persistence, and fallback orchestration.
- `MainWindow.Navigation.partial.cs` owns DPI-aware request construction.
- `Views/DeveloperTuningPanel.xaml(.cs)` owns mode selection UI only.
- `MainWindow.DeveloperTuning.partial.cs` copies validated tuning values and
  replays the current view.

The resampler and request types keep kernel math and cache identity out of
`MainWindow`. `ZoomedRegionCache.cs` should not absorb the convolution
implementation; if its source/cache orchestration approaches 400 lines during
implementation, extract cache-key/source-fingerprint construction into a
separate service before proceeding. No View may reference Services, preserving
the repository's architecture rules.

## Deferred Alternatives

The following remain backlog options rather than part of this implementation:

- reduce the configured zoom or add a native-resolution zoom cap;
- obtain a substantially higher-resolution lossless map source;
- replace the raster map with a vector source;
- overlay text and country borders as vectors while retaining a raster base;
- evaluate neural super-resolution offline, with strict checks for invented
  geography and deformed text.

## Documentation and Completion

Implementation must:

- link its implementation plan from the zoom-quality item in `docs/TO_DO.md`;
- update item 2.7 in
  `docs/exec-plans/active/zoom-performance-appearance-plan.md`;
- narrow or remove completed TO_DO scope and retain only unimplemented
  alternatives;
- update `[Unreleased]` in `CHANGELOG.md`;
- run focused tests and `.\scripts\verify.ps1`;
- record the live comparison outcome and chosen default before declaring the
  rendering slice complete.
