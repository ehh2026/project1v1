# Visual Configuration Guide

The application now supports runtime configuration of visual parameters through a JSON configuration file.

## Configuration File

The configuration is stored in `visual-config.json` in the application root directory. This file is automatically created with default values on first run if it doesn't exist.

## Available Settings

```json
{
  "ClusterDistanceThreshold": 30.0,
  "LocationMarkerSize": 12.0,
  "UsePinMarkers": true,
  "EnableDeveloperTools": false,
  "AutoOpenSingleLocationContentAfterZoom": false,
  "ClusterMarkerSize": 25.0,
  "ClusterBadgeSize": 12.0,
  "ClusterCountFontSize": 11.0,
  "ZoomScale": 55.0,
  "AnimationDurationMs": 390
}
```

This top-level sample is intentionally abbreviated. The actual file also includes nested sections such as:

- `PinParts`
- `PinMarkers`
- `RadialExtension`
- `ManualLayoutEditor`
- `Debug`

## Developer Tools Master Gate

`EnableDeveloperTools` is the single master switch for in-app developer controls. It defaults to `false` in the model so gallery/guest display configs are safe by default.

When `EnableDeveloperTools` is `false`:

- Edit Layout is hidden and cannot be entered.
- Runtime Tuning and the F12 tuning toggle are disabled.
- Composite debug overlays and verbose debug logging are treated as off.
- Debug-only windowed mode is ignored.

The repository's development `visual-config.json` may set this to `true`; production/gallery deployments should set it to `false`.

### Parameters

- **ClusterDistanceThreshold** (default: 300.0)
  - Distance in pixels for clustering locations together
  - Locations within this distance will be grouped into a single cluster
  - Larger values = more aggressive clustering (fewer clusters)
  - Smaller values = less clustering (more individual markers)

- **LocationMarkerSize** (default: 16.0)
  - Size of individual location markers in pixels
  - Controls both width and height (markers are square)

- **ClusterMarkerSize** (default: 40.0)
  - Size of cluster markers (stamp icons) in pixels
  - Controls both width and height

- **ClusterBadgeSize** (default: 20.0)
  - Size of the white circular badge that displays the count
  - Should be smaller than ClusterMarkerSize

- **ClusterCountFontSize** (default: 12.0)
  - Font size for the number displayed on cluster badges
  - Should be proportional to ClusterBadgeSize

- **ZoomScale** (default: 30.0)
  - Magnification level when zooming into a cluster
  - Higher values = more zoom (e.g., 30.0 = 30x magnification)
  - Lower values = less zoom (e.g., 15.0 = 15x magnification)
  - Note: Changing this will invalidate cached zoom images

- **AnimationDurationMs** (default: 390)
  - Duration of zoom animation in milliseconds
  - Higher values = slower, smoother animation
  - Lower values = faster animation

- **UsePinMarkers** (default: `true`)
  - Master switch for pin-style markers instead of simple circular location dots
  - When `false`, locations use the basic circular `LocationMarker` visuals

- **EnableDeveloperTools** (default: `false`)
  - Master switch for in-app developer controls
  - When `false`, gallery guests cannot access Edit Layout, Runtime Tuning/F12, debug overlays/logging, or debug-only windowed mode
  - Development configs can set this to `true`, then use the nested `ManualLayoutEditor` and `Debug` sub-settings normally

- **AutoOpenSingleLocationContentAfterZoom** (default: `false`)
  - When `true`, clicking a standalone full-map pin zooms into that location and opens its content automatically after zoom settles
  - When `false`, the click still zooms in, but users click the zoomed pin again to open content
  - Also available in the debug-gated Runtime Tuning panel

## Pin Rendering Modes

The app has three marker visual modes:

1. `UsePinMarkers = false`
   - Individual locations use the simple circular `LocationMarker`

2. `UsePinMarkers = true` and `PinParts.UseCompositeRendering = false`
   - Individual locations use the lightweight drawn `PinMarker`

3. `UsePinMarkers = true`, `PinParts.Enabled = true`, and `PinParts.UseCompositeRendering = true`
   - Visible individual markers use composite shaft/head rendering from `Pins_v2/parts` at all zoom levels
   - Extended markers use the actual radial-extension start/end segment
   - Non-extended individual markers use the configured screen-up stub segment
   - Unzoomed cluster aggregate markers remain `ClusterMarker` blobs
   - If composite planning or asset loading fails, the app leaves or restores the drawn `PinMarker` fallback for that marker

## Current Default Behavior

With the repository's current `visual-config.json` defaults:

- `UsePinMarkers = true`
- `PinParts.Enabled = true`
- `PinParts.UseCompositeRendering = true`

that means:

- visible individual markers use composite shaft/head markers
- extended markers use composite pins anchored from the source location to the radial-extension endpoint
- non-extended individual markers use screen-up composite stubs
- unzoomed multi-location cluster aggregates remain stamp-style `ClusterMarker` blobs
- manual layout edit works on composite pins in zoomed clusters and on visible full-map single-location stubs

## PinMarkers (drawn fallback)

When `UseCompositeRendering = false`, individual markers use the vector `PinMarker` control. Key fields:

| Field | Default | Purpose |
|-------|---------|---------|
| `BallSize` | `14` | Pin head diameter (px) |
| `ShaftWidth` | `3` | Core shaft width (px) |
| `ShaftLength` | `24` | Stub shaft length on non-extended pins (px) |
| `ShaftColor` | `#FFD8D8D8` | Light silver shaft core |
| `ShaftOutlineColor` | `#FF1A1A1A` | Dark halo behind shaft (stub + extension lines) |
| `ShaftOutlineThickness` | `1.25` | Extra px on each side of shaft core |
| `BallOutlineColor` | `#FF000000` | Pin head rim |
| `BallOutlineThickness` | `1.5` | Pin head rim width (px) |
| `UseRandomColors` | `true` | Random saturated ball hues (no white/gray/black) |
| `DefaultBallColor` | `#FFE53935` | Used when `UseRandomColors = false` |

**Tuning tips:** Increase `ShaftOutlineThickness` or `BallOutlineThickness` for busy map backgrounds; set `UseRandomColors` to `false` and pick one strong `DefaultBallColor` for a uniform look.

## PinParts

`PinParts` config controls the newer part-based composite renderer.

Key fields:

- `Enabled`
- `PartsFolderPath`
- `GeometryMetadataPath`
- `SelectionMode`
- `MaxResidualRotationDeg`
- `MinStretchFactor`
- `MaxStretchFactor`
- `UseCompositeRendering`
- `UsePrerasterizedRendering` — default `false`; when `true`, each composite pin flattens shaft/head layers to one bitmap inside `CompositePinMarker`
- `DefaultStubLengthPixels` — stub shaft length (screen px) for non-extended individual markers when Option A rollout is active; `0` = head-only
- `TargetHeadRadiusPx`, `TargetShaftHalfWidthPx`, `UseLitShafts`
- `ShaftAssetVariant` — optional folder under `Images&Content/Pins_v2/parts/shaft_variants/`; when empty, shaft selection follows `UseLitShafts`; when set (default: `outline_dark_7px`), composite pins load shafts from that baked variant folder while heads remain in the base parts folder. Generate variants with `scripts/create_shaft_asset_variants.py`.
- `HeadAssetVariant` — optional folder under `Images&Content/Pins_v2/parts/head_variants/`; when empty, heads load from the base parts folder. Use `outline_black_2px`, `outline_black_4px`, `outline_black_6px`, `outline_black_8px`, `outline_black_10px`, `outline_black_12px`, or `outline_black_14px` to load generated black-outline head assets. Generate variants with `scripts/create_head_asset_variants.py`.

  **Outer outline:** `outline_dark`, `outline_dark_bold`, `outline_dark_<N>px` (e.g. `outline_dark_6px`, `outline_dark_7px`) — dark halo grows **outside** the shaft alpha.

  **Inner edge (no outward growth):** `inner_dark_<N>px` (e.g. `inner_dark_2px` … `inner_dark_6px`) — blends an **N** px inward band to **pure black** (100%); does not grow alpha outward.

  **Combined:** `outline_dark_<O>px_in<I>px` — outer **O** = 6–10px; inner **I** = 2–8px (e.g. `outline_dark_9px_in4px`).

  **Combined + bright core:** `outline_dark_<O>px_in<I>px_bright` — lit-core / black-rim look. Outer **O** = 6–10px; inner **I** = 2–8px. Example: `outline_dark_8px_in5px_bright`, `outline_dark_10px_in7px_bright`.

  Preview grids: `Images&Content/Pins_v2/parts/shaft_variants/<variant>/preview_shafts.png`.

Important behavior:

- `PinParts.Enabled = true` alone does not turn on composite marker rendering
- the live renderer is only used when `PinParts.UseCompositeRendering = true`
- pre-rasterized rendering is opt-in via `PinParts.UsePrerasterizedRendering`; leave it `false` unless visual review shows the live layered renderer is still too aliased
- current rollout scope covers **visible individual markers** at all zoom levels when `UseCompositeRendering` is true, including radial-extension composites, screen-up stub composites for non-extended markers, and composite edit mode
- stub policy (Option A): unzoomed **individual** markers get a screen-up stub; unzoomed **cluster aggregate** markers do not
- baked shaft contrast uses `ShaftAssetVariant` (`outline_dark_7px` by default) for improved legibility over busy map backgrounds
- Phase 5 visual acceptance on 2026-06-12 confirmed tip/start, shaft direction, head center, endpoint behavior, and shaft/head gaps for full-map stubs and representative zoomed-cluster angles

## Radial Extension Interaction

When radial extensions are active:

- legacy path:
  - draw a separate extension line
  - move the marker to the extended endpoint
- composite path:
  - replace the extended image pin with a composite shaft/head marker
  - anchor the shaft tip at the original map point
  - anchor the shaft/head join at the extended endpoint

When radial extensions are not active, the app restores the marker's normal non-extended visual path automatically.

## Composite Debug Overlay

For live visual validation of composite placement, the `Debug` section supports:

- `ShowCompositePinDebugOverlay`

When set to `true` and composite rendering is also enabled for the current marker, the composite pin draws a lightweight overlay showing:

- tip anchor
- join / head-attach anchor
- stretch start
- stretch end
- head center

This is intended for tuning and screenshot-based inspection only, and should normally remain `false`.

## How to Use

1. **Edit the config file**: Open `visual-config.json` in the project root directory
2. **Modify values**: Change any of the numeric values to your preference
3. **Save the file**: Save your changes
4. **Rebuild the application**: Build the project to copy the config to the output directory
5. **Run the application**: The new values will be loaded on startup

### Quick Testing (Without Rebuild)

If you want to test changes immediately without rebuilding:
- Edit the config file in the build output directory: `bin\Debug\net6.0-windows\visual-config.json`
- Restart the application
- Note: This file will be overwritten on next build if the source file is newer

## Example Configurations

### Larger Markers (for high-DPI displays)
```json
{
  "ClusterDistanceThreshold": 300.0,
  "LocationMarkerSize": 24.0,
  "ClusterMarkerSize": 60.0,
  "ClusterBadgeSize": 30.0,
  "ClusterCountFontSize": 16.0,
  "ZoomScale": 30.0,
  "AnimationDurationMs": 390
}
```

### Smaller Markers (for compact view)
```json
{
  "ClusterDistanceThreshold": 300.0,
  "LocationMarkerSize": 12.0,
  "ClusterMarkerSize": 32.0,
  "ClusterBadgeSize": 16.0,
  "ClusterCountFontSize": 10.0,
  "ZoomScale": 30.0,
  "AnimationDurationMs": 390
}
```

### More Aggressive Clustering
```json
{
  "ClusterDistanceThreshold": 500.0,
  "LocationMarkerSize": 16.0,
  "ClusterMarkerSize": 40.0,
  "ClusterBadgeSize": 20.0,
  "ClusterCountFontSize": 12.0,
  "ZoomScale": 30.0,
  "AnimationDurationMs": 390
}
```

### Less Clustering (more individual markers)
```json
{
  "ClusterDistanceThreshold": 150.0,
  "LocationMarkerSize": 16.0,
  "ClusterMarkerSize": 40.0,
  "ClusterBadgeSize": 20.0,
  "ClusterCountFontSize": 12.0,
  "ZoomScale": 30.0,
  "AnimationDurationMs": 390
}
```

### Less Zoom (wider view when zoomed)
```json
{
  "ClusterDistanceThreshold": 300.0,
  "LocationMarkerSize": 16.0,
  "ClusterMarkerSize": 40.0,
  "ClusterBadgeSize": 20.0,
  "ClusterCountFontSize": 12.0,
  "ZoomScale": 15.0,
  "AnimationDurationMs": 390
}
```

### Faster Animation
```json
{
  "ClusterDistanceThreshold": 300.0,
  "LocationMarkerSize": 16.0,
  "ClusterMarkerSize": 40.0,
  "ClusterBadgeSize": 20.0,
  "ClusterCountFontSize": 12.0,
  "ZoomScale": 30.0,
  "AnimationDurationMs": 200
}
```

## Tips

- Keep ClusterBadgeSize about 50% of ClusterMarkerSize for best appearance
- Keep ClusterCountFontSize about 60% of ClusterBadgeSize
- If markers appear too small or large, adjust all size values proportionally
- Test different ClusterDistanceThreshold values to find the right balance for your data
- The config file uses JSON format, so ensure proper syntax (commas, quotes, etc.)

## Troubleshooting

- **Config not loading**: Check that the file is named exactly `visual-config.json` and is in the application root directory
- **Values not changing**: Ensure you've restarted the application after editing the file
- **Invalid JSON**: Use a JSON validator if you get errors - common issues are missing commas or extra trailing commas
- **Markers not visible**: Check that size values are reasonable (between 8 and 100 pixels typically)
