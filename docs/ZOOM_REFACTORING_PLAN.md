# Zoom Refactoring Plan

## Problem Analysis

The current implementation has markers on a separate overlay layer from the map. When the map zooms, we try to manually calculate where markers should be positioned, which leads to:
1. Complex transform math that's error-prone
2. Markers scaling with transforms (appearing too large when zoomed)
3. Positioning errors due to transform origin issues
4. Difficult to maintain and debug

## Root Cause

The fundamental issue is architectural: **markers and map are in separate visual trees with separate coordinate spaces**.

Current structure:
```
Grid (RootGrid)
├── MapDisplayControl (transforms applied here)
│   └── Image (map)
└── MarkerLayerControl (separate overlay, no automatic transform sync)
    └── Markers (positioned manually)
```

## Proposed Solution

Move markers INSIDE the map's visual tree so they transform automatically with the map.

New structure:
```
Grid (RootGrid)
├── MapDisplayControl
│   └── Grid (with RenderTransform)
│       ├── Image (map)
│       └── Canvas (marker container - transforms WITH the map)
│           └── Markers (positioned once, transform automatically)
└── BackButton
```

## Implementation Steps

### Step 1: Modify MapDisplayControl.xaml
- Wrap Image in a Grid
- Add RenderTransform to the Grid (not just the Image)
- Add a Canvas for markers as a sibling to the Image
- Canvas uses same coordinate space as the map image

### Step 2: Modify MapDisplayControl.xaml.cs
- Expose the marker Canvas as a public property
- Keep existing transform properties
- Add methods to add/remove/clear markers directly

### Step 3: Update MainWindow
- Remove separate MarkerLayerControl overlay
- Access marker canvas through MapDisplay
- Markers now automatically transform with map

### Step 4: Simplify Marker Positioning
- Calculate marker positions ONCE in map image coordinates
- No need to recalculate on zoom - transforms handle it automatically
- Markers stay same size (not scaled) by counter-scaling them

## Benefits

1. **Automatic Transform Sync**: Markers transform with map automatically
2. **Simpler Code**: No manual transform calculations needed
3. **Correct Positioning**: Markers always in correct position relative to map
4. **Better Performance**: WPF handles transform efficiently
5. **Maintainable**: Standard WPF pattern, easier to understand

## Counter-Scaling Markers

To keep markers the same visual size when zoomed:
```xaml
<Ellipse Width="24" Height="24">
    <Ellipse.RenderTransform>
        <ScaleTransform ScaleX="{Binding InverseScale}" ScaleY="{Binding InverseScale}"/>
    </Ellipse.RenderTransform>
</Ellipse>
```

Where `InverseScale = 1 / ZoomScale` (e.g., 1/3.5 = 0.286 when zoomed 3.5x)

## Migration Path

1. Create new MapDisplayControl structure
2. Test with simple markers first
3. Migrate cluster markers
4. Remove old MarkerLayerControl
5. Update all references

## Estimated Time

- Step 1-2: 30 minutes (modify MapDisplayControl)
- Step 3: 20 minutes (update MainWindow)
- Step 4: 20 minutes (simplify positioning logic)
- Testing: 30 minutes
- **Total**: ~2 hours

## Alternative: Keep Current Structure

If we want to keep the overlay approach:
1. Apply EXACT same transform to MarkerLayer as MapDisplay
2. Set correct transform origin (center of zoom point, not 0,0)
3. Counter-scale individual markers to keep them same size

This is more complex and error-prone than the proposed solution.

## Recommendation

**Proceed with refactoring to put markers inside MapDisplayControl**. This is the clean, maintainable solution that follows WPF best practices.
