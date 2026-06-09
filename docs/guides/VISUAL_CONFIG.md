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
  "ClusterMarkerSize": 25.0,
  "ClusterBadgeSize": 12.0,
  "ClusterCountFontSize": 11.0,
  "ZoomScale": 55.0,
  "AnimationDurationMs": 390
}
```

This top-level sample is intentionally abbreviated. The actual file also includes nested sections such as:

- `PinImages`
- `PinParts`
- `PinMarkers`
- `RadialExtension`
- `ManualLayoutEditor`
- `Debug`

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

## Pin Rendering Modes

The app currently has three relevant pin-rendering paths:

1. `UsePinMarkers = false`
   - Individual locations use the simple circular `LocationMarker`
   - No `pins.jpg` image pins are used

2. `UsePinMarkers = true` and `PinImages.Enabled = true`
   - Individual locations use cropped image pins from `pins.jpg`
   - This is the current default path

3. `UsePinMarkers = true`, `PinImages.Enabled = true`, `PinParts.Enabled = true`, and `PinParts.UseCompositeRendering = true`
   - Non-extended markers still use the legacy `pins.jpg` path
   - Extended markers in the radial-extension view switch to composite shaft/head rendering from `Pins_v2/parts`
   - If composite planning or asset loading fails, the app falls back to the legacy `pins.jpg` extended-marker path

If `UsePinMarkers = true` but `PinImages.Enabled = false`, the app falls back to the older drawn `PinMarker` control rather than `pins.jpg`.

## Current Default Behavior

With the repository's current `visual-config.json` defaults:

- `UsePinMarkers = true`
- `PinImages.Enabled = true`
- `PinParts.Enabled = false`
- `PinParts.UseCompositeRendering = false`

that means:

- normal individual markers use cropped pins from `pins.jpg`
- extended markers also use that same legacy image-pin path
- radial extensions draw a separate shaft-like line and then place the image pin at the extended endpoint
- the composite pin-part renderer is present in code but gated off

## PinImages

`PinImages` config controls the legacy image-pin system based on the master `pins.jpg` sprite sheet.

Key fields:

- `Enabled`
- `MasterImagePath`
- `UseRandomSelection`
- `ScaleFactor`
- `Pins`

When this section is enabled, the app crops the configured rectangles from `pins.jpg` and uses those bitmaps as marker visuals.

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

Important behavior:

- `PinParts.Enabled = true` alone does not turn on composite marker rendering
- the live renderer is only used when `PinParts.UseCompositeRendering = true`
- current rollout scope is extended image pins only
- edit mode currently stays on the legacy marker path even if composite rendering is enabled

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
