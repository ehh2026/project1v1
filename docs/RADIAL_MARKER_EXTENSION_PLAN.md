# Radial Marker Extension Plan

## Problem Statement

At 50x zoom levels, some geographic areas still have many markers positioned very close together (within 10 pixels), making them difficult to distinguish and interact with individually. This creates usability issues where users cannot accurately click on specific locations or see all available markers in a dense cluster.

## Proposed Solution: Radial Line Extensions

Draw lines extending outward from densely packed marker groups, with markers positioned at the end of these lines. The lines should radiate from the center of the dense area, creating a "spoke" pattern that spreads markers into clickable positions while maintaining visual connection to their original geographic location.

### Core Design Principles

1. **No Line Crossings**: Extension lines from the same dense group must NEVER cross each other. This is an absolute requirement for visual clarity.

2. **Flexible Angle Separation**: The minimum angle separation (default 15°) is a target, not a hard constraint. The algorithm will use smaller angles (down to 5° minimum) when necessary to accommodate all markers without crossing lines.

3. **Automatic Fallback**: When a group has too many markers to distribute without crossings (>72 markers at 5° minimum separation), the system automatically falls back to marker stacking with a carousel interface.

## Current System Analysis

### Existing Architecture

The application uses a viewport-based rendering system with the following key components:

- **LocationClusterer**: Groups locations based on distance threshold (currently 150px from visual-config.json)
- **ViewportState**: Manages zoom levels and coordinate transformations
- **MapDisplayControl**: Canvas-based rendering with marker overlay
- **LocationMarker & ClusterMarker**: Individual marker controls positioned via Canvas.SetLeft/SetTop
- **UpdateMarkerPositions()**: Recalculates marker screen positions based on current viewport

### Current Clustering Behavior

1. At full map view: Locations within 150px are grouped into cluster markers
2. On cluster click: Zooms to 50x magnification and shows individual markers
3. Individual markers are positioned at exact pixel coordinates from Excel data
4. No secondary positioning logic exists for dense areas at high zoom

### Coordinate System

- **Source Coordinates**: Original pixel coordinates (PixelX, PixelY) from Excel, range 0-8198 x 0-5542
- **Viewport Coordinates**: Cropped region of source image based on zoom level
- **Screen Coordinates**: Final display position on canvas, calculated by `ViewportState.SourceToScreen()`

## Recommended Approach: Dynamic Radial Extension

### Configuration Parameters

Add to `visual-config.json`:

```json
{
  "ClusterDistanceThreshold": 150.0,
  "LocationMarkerSize": 12.0,
  "ClusterMarkerSize": 25.0,
  "ClusterBadgeSize": 12.0,
  "ClusterCountFontSize": 12.0,
  "ZoomScale": 50.0,
  "AnimationDurationMs": 390,
  
  "RadialExtension": {
    "Enabled": true,
    "MinLocationsForExtension": 3,
    "ProximityThresholdPixels": 10,
    "ExtensionLineLength": 40,
    "MinimumAngleSeparation": 15,
    "LineColor": "#80808080",
    "LineThickness": 1.5,
    "AnimateExtension": true,
    "ExtensionAnimationMs": 250
  }
}
```

### Configuration Properties Explained

- **Enabled**: Master toggle for radial extension feature
- **MinLocationsForExtension**: Minimum number of locations within proximity threshold to trigger extension (default: 3)
- **ProximityThresholdPixels**: Distance in screen pixels to consider locations "densely packed" (default: 10)
- **ExtensionLineLength**: Length of extension lines in screen pixels (default: 40)
- **MinimumAngleSeparation**: Preferred minimum degrees between adjacent extension lines (default: 15°). Note: This is a target value, not a hard constraint. The algorithm may use smaller angles when necessary to accommodate all markers without line crossings.
- **LineColor**: ARGB hex color for extension lines (default: semi-transparent gray)
- **LineThickness**: Width of extension lines in pixels (default: 1.5)
- **AnimateExtension**: Whether to animate lines extending outward (default: true)
- **ExtensionAnimationMs**: Duration of extension animation (default: 250ms)

### Implementation Architecture

#### 1. New Model Classes

**Models/RadialExtensionConfig.cs**
```csharp
public class RadialExtensionConfig
{
    public bool Enabled { get; set; } = true;
    public int MinLocationsForExtension { get; set; } = 3;
    public double ProximityThresholdPixels { get; set; } = 10.0;
    public double ExtensionLineLength { get; set; } = 40.0;
    public double MinimumAngleSeparation { get; set; } = 15.0;
    public string LineColor { get; set; } = "#80808080";
    public double LineThickness { get; set; } = 1.5;
    public bool AnimateExtension { get; set; } = true;
    public int ExtensionAnimationMs { get; set; } = 250;
}
```

**Models/DenseMarkerGroup.cs**
```csharp
public class DenseMarkerGroup
{
    public List<Location> Locations { get; set; }
    public Point CenterPoint { get; set; }
    public List<RadialExtension> Extensions { get; set; }
}

public class RadialExtension
{
    public Location Location { get; set; }
    public Point OriginalPosition { get; set; }
    public Point ExtendedPosition { get; set; }
    public double Angle { get; set; }
}
```

#### 2. New Utility Class

**Utilities/RadialExtensionCalculator.cs**

Key responsibilities:
- Detect dense marker groups in screen coordinates
- Calculate optimal radial angles to minimize overlap
- **Ensure extension lines never cross each other**
- Generate extension line endpoints
- Handle edge cases (screen boundaries, angle conflicts)
- Dynamically adjust angle separation based on marker count

Algorithm:
```
1. For each visible marker at current zoom level:
   a. Find all markers within ProximityThresholdPixels (screen space)
   b. If count >= MinLocationsForExtension, create DenseMarkerGroup

2. For each DenseMarkerGroup:
   a. Calculate geometric center of all markers
   b. Calculate natural angle for each marker from center
   c. Sort markers by their natural angles (clockwise from north)
   d. Distribute markers to prevent line crossings:
      - Calculate available angular space: 360°
      - Calculate ideal separation: 360° / markerCount
      - Use max(ideal, MinimumAngleSeparation) as target
      - If target * markerCount > 360°:
        * Reduce separation proportionally (may go below MinimumAngleSeparation)
        * Ensure minimum 5° separation to prevent visual overlap
      - Adjust angles to maintain natural ordering (prevent crossings)
   e. For each marker:
      - Calculate extended position: center + (angle, ExtensionLineLength)
      - Verify no line crossings with other extensions in group
      - Check for screen boundary collision, adjust if needed
      - Store as RadialExtension

3. Return list of DenseMarkerGroups with calculated extensions
```

#### 3. New View Component

**Views/ExtensionLine.xaml / ExtensionLine.xaml.cs**

A simple line control that:
- Renders from original marker position to extended position
- Supports animation (line grows from 0 to full length)
- Uses configurable color and thickness
- Positioned absolutely on the marker canvas

XAML structure:
```xml
<Line X1="{Binding StartX}" Y1="{Binding StartY}"
      X2="{Binding EndX}" Y2="{Binding EndY}"
      Stroke="{Binding LineColor}"
      StrokeThickness="{Binding Thickness}"
      StrokeDashArray="2,2"
      Opacity="0.8"/>
```

#### 4. Integration Points

**MainWindow.xaml.cs modifications:**

1. Add field: `private List<DenseMarkerGroup> _denseGroups = new List<DenseMarkerGroup>();`
2. Add field: `private List<Line> _extensionLines = new List<Line>();`
3. Modify `UpdateMarkerPositions()`:
   ```csharp
   private void UpdateMarkerPositions()
   {
       var viewport = MapDisplay.CurrentViewport;
       if (viewport == null) return;

       // Clear existing extensions
       ClearExtensionLines();

       // Calculate screen positions for all visible markers
       var markerScreenPositions = CalculateMarkerScreenPositions();

       // Detect dense groups if enabled and zoomed in
       if (_visualConfig.RadialExtension.Enabled && viewport.ZoomLevel > 10.0)
       {
           _denseGroups = RadialExtensionCalculator.DetectDenseGroups(
               markerScreenPositions, 
               _visualConfig.RadialExtension);

           // Apply extensions
           ApplyRadialExtensions(_denseGroups);
       }
       else
       {
           // Normal positioning without extensions
           ApplyNormalPositioning(markerScreenPositions);
       }
   }
   ```

4. New method: `ApplyRadialExtensions()`
   ```csharp
   private void ApplyRadialExtensions(List<DenseMarkerGroup> groups)
   {
       foreach (var group in groups)
       {
           foreach (var extension in group.Extensions)
           {
               // Draw extension line
               var line = CreateExtensionLine(
                   extension.OriginalPosition, 
                   extension.ExtendedPosition);
               _extensionLines.Add(line);
               MapDisplay.Markers.Children.Add(line);

               // Position marker at extended location
               var marker = FindMarkerForLocation(extension.Location);
               if (marker != null)
               {
                   Canvas.SetLeft(marker, extension.ExtendedPosition.X - marker.Width / 2);
                   Canvas.SetTop(marker, extension.ExtendedPosition.Y - marker.Height / 2);
               }
           }
       }

       // Animate if configured
       if (_visualConfig.RadialExtension.AnimateExtension)
       {
           AnimateExtensionLines(_extensionLines);
       }
   }
   ```

5. New method: `ClearExtensionLines()`
   ```csharp
   private void ClearExtensionLines()
   {
       foreach (var line in _extensionLines)
       {
           MapDisplay.Markers.Children.Remove(line);
       }
       _extensionLines.Clear();
   }
   ```

### Visual Design Considerations

#### Line Styling
- Use dashed lines (StrokeDashArray="2,2") to distinguish from map content
- Semi-transparent (alpha ~50%) to avoid visual clutter
- Thin lines (1-2px) to minimize distraction
- Subtle color (gray or matching marker color with low opacity)

#### Marker Positioning
- Markers remain at extended positions, not original positions
- Original position indicated by line endpoint
- Maintain marker interactivity at extended position
- Consider adding small dot/circle at original position for clarity

#### Animation Sequence
1. Zoom animation completes
2. Markers appear at original positions (50ms)
3. Extension lines grow from center outward (250ms)
4. Markers slide along lines to extended positions (250ms)
5. Total extension animation: 500ms

#### Z-Order
- Extension lines: Lowest layer (behind markers)
- Markers: Top layer (always clickable)
- Ensure lines don't obscure other UI elements

### Critical Constraints

#### Line Crossing Prevention

**Absolute Rule**: Extension lines from the same dense group must NEVER cross each other.

**Why This Matters**:
- Crossing lines create visual confusion about which marker connects to which line
- Reduces clarity and defeats the purpose of spreading markers
- Makes the interface look broken or buggy
- Violates user expectations about radial layouts

**Implementation Strategy**:

1. **Preserve Natural Angular Order**
   - Calculate each marker's natural angle from group center
   - Sort markers by angle (0° = north, clockwise)
   - Assign extension angles in the same order
   - This guarantees no crossings if angles are monotonically increasing

2. **Dynamic Angle Calculation**
   ```csharp
   // Example for 8 markers
   double[] naturalAngles = {15°, 45°, 62°, 88°, 195°, 220°, 310°, 355°};
   
   // Calculate angular spans between consecutive markers
   double totalSpan = 360°;
   int markerCount = 8;
   
   // Ideal separation
   double idealSeparation = totalSpan / markerCount; // 45°
   
   // If idealSeparation < MinimumAngleSeparation (15°), use ideal anyway
   // to fit all markers without crossing
   double actualSeparation = Math.Max(5°, idealSeparation);
   
   // Distribute evenly starting from first marker's natural angle
   double startAngle = naturalAngles[0];
   for (int i = 0; i < markerCount; i++)
   {
       extensionAngles[i] = (startAngle + i * actualSeparation) % 360°;
   }
   ```

3. **Minimum Separation Enforcement**
   - Hard minimum: 5° (prevents visual overlap of markers)
   - Soft minimum: 15° (preferred for clarity)
   - Use soft minimum when possible, fall back to hard minimum when necessary
   - If even 5° separation doesn't fit, trigger fallback to marker stacking

4. **Validation Check**
   ```csharp
   // After calculating all extension angles, verify no crossings
   for (int i = 0; i < extensions.Count - 1; i++)
   {
       double angle1 = extensions[i].Angle;
       double angle2 = extensions[i + 1].Angle;
       
       // Angles should be monotonically increasing (accounting for 360° wrap)
       double diff = (angle2 - angle1 + 360) % 360;
       
       if (diff < 5°)
       {
           // Too close, adjust or trigger fallback
           HandleInsufficientSpace(group);
       }
   }
   ```

**Fallback Scenarios**:
- If 24+ markers in group (360° / 15° = 24 max with preferred separation)
- If < 5° separation required (visual overlap inevitable)
- If screen boundaries prevent proper distribution
- → Switch to marker stacking with carousel

#### Flexible Angle Separation

**Adaptive Separation Logic**:

The `MinimumAngleSeparation` configuration value (default 15°) is a **target**, not a hard constraint. The algorithm adapts based on marker count:

| Markers | Available Space | Ideal Separation | Actual Separation Used |
|---------|----------------|------------------|------------------------|
| 3       | 360°           | 120°             | 120° (well above 15°)  |
| 8       | 360°           | 45°              | 45° (above 15°)        |
| 12      | 360°           | 30°              | 30° (above 15°)        |
| 18      | 360°           | 20°              | 20° (above 15°)        |
| 24      | 360°           | 15°              | 15° (at target)        |
| 30      | 360°           | 12°              | 12° (below target)     |
| 36      | 360°           | 10°              | 10° (below target)     |
| 48      | 360°           | 7.5°             | 7.5° (below target)    |
| 72      | 360°           | 5°               | 5° (hard minimum)      |
| 73+     | 360°           | < 5°             | **Fallback to stack**  |

**Configuration Update**:
```json
"RadialExtension": {
  "MinimumAngleSeparation": 15.0,
  "HardMinimumAngleSeparation": 5.0,
  "MaxMarkersForRadial": 72,
  "FallbackToStackAbove": 72
}
```

**Visual Examples**:

*3 markers (120° separation):*
```
        ●
       /
      /
     ●────────●
```

*8 markers (45° separation):*
```
    ●   ●   ●
     \  |  /
      \ | /
       \|/
    ●───●───●
       /|\
      / | \
     /  |  \
    ●   ●   ●
```

*24 markers (15° separation):*
```
  ● ● ● ● ● ●
 ●           ●
●             ●
●      ●      ●
●             ●
 ●           ●
  ● ● ● ● ● ●
```

*36 markers (10° separation):*
```
  ●●●●●●●●●
 ●         ●
●           ●
●     ●     ●
●           ●
 ●         ●
  ●●●●●●●●●
```

**User Experience Implications**:
- Small groups (3-12 markers): Spacious, easy to click
- Medium groups (13-24 markers): Comfortable, clear separation
- Large groups (25-48 markers): Tighter, but still usable
- Very large groups (49-72 markers): Dense, but no crossings
- Extreme groups (73+ markers): Automatic fallback to carousel

### Edge Cases and Handling

#### 1. Screen Boundary Collisions
**Problem**: Extended marker position falls outside visible canvas area.

**Solution**: 
- Detect collision using canvas bounds
- Reduce extension length for that specific marker
- Or rotate angle slightly to find valid position
- Minimum extension length: 20px (configurable)

#### 2. Overlapping Extensions
**Problem**: Two dense groups are close together, causing extension lines to overlap.

**Solution**:
- Detect overlapping extension zones
- Adjust angles to create "lanes" between groups
- Prioritize larger groups (more markers)
- If unavoidable, reduce extension length

#### 3. Angle Distribution
**Problem**: Uneven marker distribution around center creates visual imbalance.

**Solution**:
- Start from natural angles of markers to preserve spatial relationships
- Distribute evenly around 360° while maintaining angular order (prevents crossings)
- Use dynamic separation based on marker count (may be < 15° for large groups)
- Prefer cardinal directions (N, E, S, W) when possible for small groups
- Avoid angles that point toward other dense groups
- Implement "repulsion" algorithm to maximize spacing between groups

#### 4. Dynamic Zoom Changes
**Problem**: User zooms in/out while extensions are visible.

**Solution**:
- Recalculate extensions on every zoom level change
- Animate transition between extension states
- Cache calculations for common zoom levels
- Disable extensions below certain zoom threshold (e.g., < 10x)

#### 5. Marker Interaction
**Problem**: User clicks on extension line instead of marker.

**Solution**:
- Lines should not be hit-testable (IsHitTestVisible="False")
- Ensure markers have sufficient size at extended positions
- Add hover effect to highlight line + marker together
- Consider making lines clickable to select associated marker

### Performance Considerations

#### Calculation Complexity
- Dense group detection: O(n²) for n visible markers
- Optimization: Use spatial grid to reduce to O(n log n)
- Only recalculate when viewport changes significantly
- Cache results for same viewport state

#### Rendering Performance
- Extension lines are simple WPF Line elements (lightweight)
- Typical dense group: 3-10 markers = 3-10 lines
- Expected total: 20-50 lines maximum at any zoom level
- Negligible performance impact on modern hardware

#### Memory Usage
- Each Line object: ~200 bytes
- 50 lines: ~10 KB
- DenseMarkerGroup objects: ~1 KB each
- Total overhead: < 50 KB (negligible)

### Testing Strategy

#### Unit Tests
- `RadialExtensionCalculator.DetectDenseGroups()` with various marker configurations
- Angle distribution algorithm with different group sizes (3, 8, 24, 36, 72 markers)
- **Line crossing detection and prevention** (critical test)
- Validation that angles maintain monotonic order
- Dynamic angle separation calculation (verify < 15° when needed)
- Boundary collision detection and adjustment
- Configuration loading and validation
- Fallback to stacking when > 72 markers

#### Integration Tests
- Extension rendering at different zoom levels
- Animation timing and smoothness
- Marker interactivity at extended positions
- Performance with 100+ markers

#### Manual Testing Scenarios
1. Zoom to area with 3 markers within 10px → verify extensions appear with wide spacing
2. Zoom to area with 8 markers within 10px → verify even distribution, no crossings
3. Zoom to area with 24 markers within 10px → verify tight but clear distribution, no crossings
4. Zoom to area with 36 markers within 10px → verify < 15° separation used, no crossings
5. Zoom to area with 72 markers within 10px → verify 5° minimum separation, no crossings
6. Zoom to area with 80 markers within 10px → verify fallback to stack marker
7. Zoom to corner of map → verify boundary handling
8. Rapidly zoom in/out → verify no visual glitches
9. Click markers at extended positions → verify content opens correctly
10. Visually inspect all extension lines → verify absolutely no crossings
11. Disable feature in config → verify normal behavior

## Alternative Approaches

### Alternative 1: Hierarchical Clustering at High Zoom

**Concept**: Continue clustering even at high zoom levels, creating sub-clusters.

**Pros**:
- Consistent with existing clustering behavior
- No new visual elements (lines)
- Simpler implementation

**Cons**:
- User may need to click multiple times to reach individual markers
- Loses geographic precision (markers not at true location)
- Frustrating UX for areas with many nearby locations
- Doesn't solve the fundamental problem of dense markers

**Recommendation**: Not ideal. Users expect to see individual markers after zooming to 50x.

### Alternative 2: Marker Stacking with Carousel

**Concept**: Stack overlapping markers vertically, show carousel UI to cycle through them.

**Pros**:
- Keeps markers at exact geographic location
- Familiar pattern (similar to Google Maps)
- No extension lines needed

**Cons**:
- Requires new UI component (carousel/stack viewer)
- Only one marker visible at a time
- Difficult to see all available options at once
- Requires additional clicks to explore all markers

**Recommendation**: Could work as a fallback for extreme density (10+ markers in 5px), but radial extension is better for typical cases.

### Alternative 3: Marker Size Reduction at High Zoom

**Concept**: Automatically reduce marker size when density is high.

**Pros**:
- Very simple implementation
- No new visual elements
- Maintains geographic accuracy

**Cons**:
- Smaller markers are harder to click (accessibility issue)
- Doesn't solve overlap problem, just reduces it
- May become too small to see clearly
- Doesn't scale well (10 markers still overlap even at 5px each)

**Recommendation**: Could be combined with radial extension, but not sufficient alone.

### Alternative 4: Fisheye Distortion

**Concept**: Apply fisheye lens effect to spread markers in dense areas while compressing surrounding space.

**Pros**:
- Elegant mathematical solution
- Maintains relative positioning
- No additional UI elements

**Cons**:
- Complex implementation (requires custom rendering)
- Distorts map image (confusing for users)
- Difficult to calculate click positions
- May cause motion sickness with animations
- Computationally expensive

**Recommendation**: Too complex for the benefit. Radial extension is simpler and more intuitive.

### Alternative 5: Popup List on Hover

**Concept**: Show list of all nearby locations in a popup when hovering over dense area.

**Pros**:
- Simple implementation
- Familiar pattern (tooltips)
- No permanent visual clutter

**Cons**:
- Requires hover (not touch-friendly)
- List may be long and hard to read
- Doesn't show spatial relationships
- Requires additional click after selection

**Recommendation**: Could work as a complementary feature (show list in tooltip), but radial extension provides better spatial awareness.

### Alternative 6: Grid Layout for Dense Areas

**Concept**: Arrange overlapping markers in a grid pattern around the center point.

**Pros**:
- Predictable, organized layout
- Easy to implement
- Clear visual structure

**Cons**:
- Loses radial/directional information
- Grid may not fit well with map aesthetics
- Requires more space than radial layout
- Less intuitive connection to original position

**Recommendation**: Radial layout is more natural for geographic data and uses space more efficiently.

## Implementation Phases

### Phase 1: Core Infrastructure (Week 1)
- Add RadialExtensionConfig to VisualConfig model
- Implement RadialExtensionCalculator utility class
- Add configuration loading and validation
- Unit tests for angle distribution algorithm

### Phase 2: Detection and Calculation (Week 1-2)
- Implement dense group detection in screen space
- Implement radial angle calculation
- Handle boundary collision detection
- Unit tests for edge cases

### Phase 3: Rendering (Week 2)
- Create ExtensionLine view component
- Integrate into UpdateMarkerPositions()
- Implement ApplyRadialExtensions()
- Basic rendering without animation

### Phase 4: Animation and Polish (Week 2-3)
- Implement extension line animation
- Implement marker slide animation
- Add hover effects (highlight line + marker)
- Tune visual styling (colors, thickness, opacity)

### Phase 5: Testing and Optimization (Week 3)
- Performance testing with large datasets
- Manual testing of all edge cases
- User acceptance testing
- Documentation updates

### Phase 6: Configuration and Refinement (Week 3-4)
- Expose all parameters in visual-config.json
- Add runtime configuration UI (optional)
- Fine-tune default values based on testing
- Create user guide documentation

## Configuration Examples

### Conservative Settings (Minimal Extensions)
```json
"RadialExtension": {
  "Enabled": true,
  "MinLocationsForExtension": 5,
  "ProximityThresholdPixels": 5,
  "ExtensionLineLength": 30,
  "MinimumAngleSeparation": 20,
  "AnimateExtension": false
}
```

### Aggressive Settings (Maximum Clarity)
```json
"RadialExtension": {
  "Enabled": true,
  "MinLocationsForExtension": 2,
  "ProximityThresholdPixels": 15,
  "ExtensionLineLength": 60,
  "MinimumAngleSeparation": 10,
  "AnimateExtension": true,
  "ExtensionAnimationMs": 300
}
```

### Disabled (Fallback to Current Behavior)
```json
"RadialExtension": {
  "Enabled": false
}
```

## Success Metrics

### Usability
- Users can click on individual markers in dense areas without misclicks
- All markers in a dense group are visually distinguishable
- Extension lines don't create visual confusion

### Performance
- Extension calculation completes in < 50ms for typical zoom operation
- No visible lag or stutter during zoom animations
- Smooth 60fps animation of extension lines

### Configurability
- All key parameters adjustable via visual-config.json
- Feature can be completely disabled without code changes
- Default values work well for 80% of use cases

## Conclusion

The radial extension approach provides an elegant solution to the dense marker problem at high zoom levels. It maintains geographic accuracy (lines show true position), improves usability (markers spread for easy clicking), and integrates cleanly with the existing viewport-based architecture.

Key advantages:
- **Intuitive**: Lines clearly connect extended markers to original positions
- **No Crossings**: Lines never cross, maintaining visual clarity
- **Flexible**: Adapts angle separation from 5° to 180° based on marker count
- **Configurable**: All parameters exposed in visual-config.json
- **Performant**: Lightweight rendering with minimal overhead
- **Scalable**: Works for 3-72 markers per dense group (with automatic fallback beyond)
- **Accessible**: Larger click targets improve usability

The implementation leverages existing infrastructure (Canvas positioning, viewport calculations, animation system) and adds minimal complexity. Alternative approaches either don't solve the core problem (size reduction, hierarchical clustering) or add excessive complexity (fisheye distortion).

Recommended next steps:
1. Review and approve this plan
2. Update visual-config.json with RadialExtension section
3. Begin Phase 1 implementation
4. Iterate based on testing feedback
