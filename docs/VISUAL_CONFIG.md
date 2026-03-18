# Visual Configuration Guide

The application now supports runtime configuration of visual parameters through a JSON configuration file.

## Configuration File

The configuration is stored in `visual-config.json` in the application root directory. This file is automatically created with default values on first run if it doesn't exist.

## Available Settings

```json
{
  "ClusterDistanceThreshold": 300.0,
  "LocationMarkerSize": 16.0,
  "ClusterMarkerSize": 40.0,
  "ClusterBadgeSize": 20.0,
  "ClusterCountFontSize": 12.0,
  "ZoomScale": 30.0,
  "AnimationDurationMs": 390
}
```

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
