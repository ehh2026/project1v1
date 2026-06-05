# Zoomed Region Cache Regression Assessment

Date: 2026-06-05

## Summary

Clicking a dense-region marker could animate toward the expected cluster, then finish on the wrong map region. The root cause was the zoomed-region cache being wired to the display map image instead of the intended full-resolution map image.

The intended source split is:

- Display/viewport source: `Images&Content/World Map Extra Large.jpg` (`8198x5542`)
- High-quality final zoom source: `Images&Content/World Map 1976.jpg` (`16397x11085`)

The March 18 version of `MainWindow.xaml.cs` constructed `ZoomedRegionCache` with `Images&Content/World Map 1976.jpg`. A later refactor changed the path to `ContentLoader.GetWorldMapPath()`, which resolves to `World Map Extra Large.jpg`.

## Evidence

- `git show 5f32adb:MainWindow.xaml.cs` shows the March 18 code using `World Map 1976.jpg` for `ZoomedRegionCache`.
- Current code before this fix used `_contentLoader.GetWorldMapPath()`, the display image path.
- `Images&Content/World Map 1976.jpg` still exists and is tracked.
- Current image dimensions:
  - `World Map 1976.jpg`: `16397x11085`
  - `World Map Extra Large.jpg`: `8198x5542`
  - `World Map Large.jpg`: `4099x2771`
- The Excel coordinate reader loads half-size coordinates from columns E/F. `ZoomedRegionCache` historically scaled the final crop into the full-resolution source.

## Why It Looked Like A Wrong Zoom Target

The animation path used the display image and viewport state, so it could appear to head toward the clicked cluster. At completion, `ShowZoomedView` replaced the displayed image with a cached/generated high-quality crop. When the cache source path was wrong, that final crop no longer represented the intended full-resolution counterpart of the viewport.

This made the final image disagree with marker placement and could look like the app zoomed into a far-away region.

## Fix

- Added `ContentFileNames.FullResolutionWorldMapFileName = "World Map 1976.jpg"`.
- Added `ContentLoader.GetFullResolutionWorldMapPath()`.
- Restored `MainWindow` to initialize `ZoomedRegionCache` with the full-resolution map path.
- Changed `ZoomedRegionCache` to compute crop scale from actual image dimensions instead of hard-coding `2x`.
- Bumped the zoomed-region cache version from `6` to `7`, forcing stale cached PNGs to clear.
- Added regression coverage:
  - same-size full-res source does not accidentally double crop coordinates
  - canonical full-resolution map filename remains `World Map 1976.jpg`

## Notes

`ZoomScale` is still `55.0`, and it was already `55.0` on March 18. The perception that the zoom is too aggressive is likely a separate tuning issue, not caused by this regression.
