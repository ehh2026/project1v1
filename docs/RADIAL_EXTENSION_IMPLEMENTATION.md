# Radial Extension Lines - Implementation Plan

## Overview

This document provides a detailed, step-by-step implementation plan for adding radial extension lines to handle densely packed markers at high zoom levels. This feature will spread markers outward along lines radiating from their cluster center, ensuring no line crossings and maintaining visual clarity.

## Project Scope

**Goal**: Implement radial extension lines for dense marker groups (3-72 markers within 10 pixels at high zoom)

**Out of Scope** (for this phase):
- Marker stacking with carousel (future enhancement)
- Alternative visualization methods
- Touch gesture support beyond existing functionality

## Prerequisites

- Existing viewport-based zoom system (✓ implemented)
- Location clustering system (✓ implemented)
- Visual configuration system (✓ implemented)
- Marker positioning system (✓ implemented)

## Implementation Phases

### Phase 1: Configuration and Models (Days 1-2)

#### Phase 1 Checklist

**Configuration Updates:**
- [ ] Add `RadialExtensionConfig` class to `Models/RadialExtensionConfig.cs`
- [ ] Add `RadialExtension` property to `VisualConfig` class
- [ ] Update `visual-config.json` with default radial extension settings
- [ ] Add JSON deserialization support for nested config object
- [ ] Test configuration loading with valid values
- [ ] Test configuration loading with missing values (use defaults)
- [ ] Test configuration loading with invalid values (validation)

**New Model Classes:**
- [ ] Create `Models/DenseMarkerGroup.cs`
- [ ] Create `Models/RadialExtension.cs`
- [ ] Add properties: Location, OriginalPosition, ExtendedPosition, Angle
- [ ] Add XML documentation comments to all model classes
- [ ] Add unit tests for model instantiation

**Validation:**
- [ ] Verify config loads correctly in MainWindow constructor
- [ ] Log loaded config values to debug output
- [ ] Verify default values are applied when config is missing


### Phase 2: Core Algorithm Implementation (Days 3-5)

#### Phase 2 Checklist

**Create RadialExtensionCalculator Utility:**
- [ ] Create `Utilities/RadialExtensionCalculator.cs`
- [ ] Implement `DetectDenseGroups()` method
  - [ ] Accept list of marker screen positions
  - [ ] Accept proximity threshold from config
  - [ ] Return list of DenseMarkerGroup objects
- [ ] Implement spatial proximity detection
  - [ ] Use Euclidean distance in screen space
  - [ ] Group markers within threshold distance
  - [ ] Filter groups with < MinLocationsForExtension
- [ ] Implement `CalculateRadialExtensions()` method
  - [ ] Calculate geometric center of group
  - [ ] Calculate natural angle for each marker from center
  - [ ] Sort markers by natural angle (0° = north, clockwise)
  - [ ] Calculate ideal angle separation (360° / markerCount)
  - [ ] Distribute angles maintaining natural order
  - [ ] Generate extended positions using angle and line length
- [ ] Implement `ValidateNoCrossings()` method
  - [ ] Verify angles are monotonically increasing
  - [ ] Check minimum separation (5° hard minimum)
  - [ ] Return true if valid, false if crossings detected
- [ ] Implement boundary collision detection
  - [ ] Check if extended position is within canvas bounds
  - [ ] Adjust extension length if needed
  - [ ] Maintain minimum extension length (20px)
- [ ] Add comprehensive XML documentation

**Unit Tests:**
- [ ] Test dense group detection with 3 markers
- [ ] Test dense group detection with 8 markers
- [ ] Test dense group detection with 24 markers
- [ ] Test dense group detection with 72 markers
- [ ] Test angle distribution for 3 markers (expect ~120° separation)
- [ ] Test angle distribution for 24 markers (expect ~15° separation)
- [ ] Test angle distribution for 72 markers (expect ~5° separation)
- [ ] Test no-crossing validation with valid angles
- [ ] Test no-crossing validation with crossing angles
- [ ] Test boundary collision detection at screen edges
- [ ] Test with markers at exact same position
- [ ] Test with markers in a line (edge case)
- [ ] Test with markers in a tight cluster


### Phase 3: Visual Components (Days 6-7)

#### Phase 3 Checklist

**Extension Line View:**
- [ ] Create `Views/ExtensionLine.xaml`
- [ ] Create `Views/ExtensionLine.xaml.cs`
- [ ] Add Line element with binding properties
  - [ ] X1, Y1 (start point - original position)
  - [ ] X2, Y2 (end point - extended position)
  - [ ] Stroke color (from config)
  - [ ] StrokeThickness (from config)
- [ ] Set StrokeDashArray="2,2" for dashed line style
- [ ] Set Opacity to 0.8 for semi-transparency
- [ ] Set IsHitTestVisible="False" (lines not clickable)
- [ ] Add DropShadowEffect for subtle depth
- [ ] Test line rendering at various angles
- [ ] Test line rendering at various lengths

**Small Dot Marker (Optional Enhancement):**
- [ ] Create `Views/OriginDot.xaml` (small circle at original position)
- [ ] Set size to 4-6 pixels
- [ ] Use semi-transparent fill
- [ ] Position at line start point
- [ ] Test visibility at different zoom levels

**Styling:**
- [ ] Verify line color matches config
- [ ] Verify line thickness matches config
- [ ] Test visual appearance on light map areas
- [ ] Test visual appearance on dark map areas
- [ ] Ensure lines don't obscure map details
- [ ] Ensure lines are visible but not distracting


### Phase 4: MainWindow Integration (Days 8-10)

#### Phase 4 Checklist

**Add Fields to MainWindow:**
- [ ] Add `private List<DenseMarkerGroup> _denseGroups = new List<DenseMarkerGroup>();`
- [ ] Add `private List<Line> _extensionLines = new List<Line>();`
- [ ] Add `private RadialExtensionCalculator _extensionCalculator;`
- [ ] Initialize calculator in constructor with config

**Modify UpdateMarkerPositions():**
- [ ] Add call to `ClearExtensionLines()` at start
- [ ] Calculate screen positions for all visible markers
- [ ] Check if radial extension is enabled in config
- [ ] Check if zoom level > threshold (e.g., 10x)
- [ ] Call `DetectDenseGroups()` with screen positions
- [ ] For each dense group:
  - [ ] Call `CalculateRadialExtensions()`
  - [ ] Call `ValidateNoCrossings()`
  - [ ] If valid, call `ApplyRadialExtensions()`
  - [ ] If invalid, log warning and use normal positioning
- [ ] For non-dense markers, use normal positioning
- [ ] Update marker positions based on extension state

**Implement ClearExtensionLines():**
- [ ] Iterate through `_extensionLines` list
- [ ] Remove each line from `MapDisplay.Markers.Children`
- [ ] Clear the `_extensionLines` list
- [ ] Verify no memory leaks

**Implement ApplyRadialExtensions():**
- [ ] Accept DenseMarkerGroup parameter
- [ ] For each RadialExtension in group:
  - [ ] Create Line element
  - [ ] Set X1, Y1 to original position
  - [ ] Set X2, Y2 to extended position
  - [ ] Apply styling from config
  - [ ] Add line to canvas: `MapDisplay.Markers.Children.Add(line)`
  - [ ] Add line to `_extensionLines` list
  - [ ] Find corresponding LocationMarker
  - [ ] Position marker at extended position
  - [ ] Center marker on extended position (subtract half width/height)
- [ ] Log number of extensions applied

**Implement Helper Methods:**
- [ ] `CalculateMarkerScreenPositions()` - returns Dictionary<Location, Point>
- [ ] `FindMarkerForLocation(Location)` - returns LocationMarker or null
- [ ] `IsZoomLevelSufficientForExtensions()` - checks zoom threshold

**Integration Points:**
- [ ] Ensure extensions are cleared on zoom out
- [ ] Ensure extensions are recalculated on zoom level change
- [ ] Ensure extensions are cleared when returning to full map view
- [ ] Ensure extensions work with existing marker click handlers
- [ ] Ensure extensions don't interfere with content subwindow


### Phase 5: Animation (Days 11-12)

#### Phase 5 Checklist

**Extension Line Animation:**
- [ ] Create `AnimateExtensionLines()` method in MainWindow
- [ ] Accept list of Line elements
- [ ] For each line:
  - [ ] Store final X2, Y2 values
  - [ ] Set initial X2, Y2 to X1, Y1 (line starts at zero length)
  - [ ] Create DoubleAnimation for X2 property
  - [ ] Create DoubleAnimation for Y2 property
  - [ ] Set duration from config (ExtensionAnimationMs)
  - [ ] Use EasingFunction (QuadraticEase, EaseOut)
  - [ ] Begin animations
- [ ] Stagger animation start times (optional, 20ms delay between lines)
- [ ] Test animation smoothness
- [ ] Test animation with 3 lines
- [ ] Test animation with 24 lines
- [ ] Test animation with 72 lines

**Marker Slide Animation:**
- [ ] Create `AnimateMarkerSlide()` method
- [ ] Accept LocationMarker and target position
- [ ] Animate Canvas.Left property
- [ ] Animate Canvas.Top property
- [ ] Synchronize with line animation
- [ ] Use same duration and easing
- [ ] Test marker follows line endpoint

**Animation Sequence:**
- [ ] Zoom animation completes
- [ ] Brief pause (50ms)
- [ ] Extension lines grow from center
- [ ] Markers slide along lines simultaneously
- [ ] Total animation duration matches config
- [ ] Test full sequence smoothness

**Configuration Support:**
- [ ] Check `AnimateExtension` flag before animating
- [ ] If false, show lines and markers at final positions immediately
- [ ] Test both animated and non-animated modes

**Performance:**
- [ ] Profile animation with 72 lines
- [ ] Ensure 60fps during animation
- [ ] Optimize if frame drops detected


### Phase 6: Testing and Refinement (Days 13-15)

#### Phase 6 Checklist

**Unit Testing:**
- [ ] Run all RadialExtensionCalculator unit tests
- [ ] Verify 100% pass rate
- [ ] Add additional edge case tests as needed
- [ ] Test configuration loading edge cases
- [ ] Test model validation

**Integration Testing:**
- [ ] Test with real location data from Excel
- [ ] Test with Chang Dai-chien location cluster (if dense)
- [ ] Test with multiple dense groups on screen simultaneously
- [ ] Test zoom in to dense area
- [ ] Test zoom out from dense area
- [ ] Test rapid zoom in/out (stress test)
- [ ] Test window resize during extension display
- [ ] Test marker click at extended position
- [ ] Test content subwindow opens correctly
- [ ] Test back button navigation
- [ ] Test escape key closes subwindow

**Visual Testing:**
- [ ] Verify no line crossings in all test cases
- [ ] Verify lines are visible but not distracting
- [ ] Verify markers are clickable at extended positions
- [ ] Verify animation is smooth
- [ ] Verify lines clear properly on zoom out
- [ ] Test on different screen resolutions
- [ ] Test on different DPI settings

**Edge Case Testing:**
- [ ] Dense group at top-left corner of map
- [ ] Dense group at top-right corner of map
- [ ] Dense group at bottom-left corner of map
- [ ] Dense group at bottom-right corner of map
- [ ] Dense group at exact center of map
- [ ] Two dense groups very close together
- [ ] Dense group with 3 markers (minimum)
- [ ] Dense group with 72 markers (maximum)
- [ ] All markers at exact same position
- [ ] Markers in a perfect line
- [ ] Markers in a perfect circle

**Performance Testing:**
- [ ] Measure extension calculation time for 72 markers
- [ ] Verify < 50ms calculation time
- [ ] Measure rendering time for 72 lines
- [ ] Verify no frame drops during animation
- [ ] Test with 10 dense groups on screen (stress test)
- [ ] Profile memory usage
- [ ] Verify no memory leaks after multiple zoom cycles

**Configuration Testing:**
- [ ] Test with MinLocationsForExtension = 2
- [ ] Test with MinLocationsForExtension = 5
- [ ] Test with ProximityThresholdPixels = 5
- [ ] Test with ProximityThresholdPixels = 20
- [ ] Test with ExtensionLineLength = 20
- [ ] Test with ExtensionLineLength = 80
- [ ] Test with MinimumAngleSeparation = 10
- [ ] Test with MinimumAngleSeparation = 30
- [ ] Test with AnimateExtension = false
- [ ] Test with Enabled = false (feature disabled)


### Phase 7: Documentation and Polish (Days 16-17)

#### Phase 7 Checklist

**Code Documentation:**
- [ ] Add XML documentation to all public methods
- [ ] Add XML documentation to all public properties
- [ ] Add inline comments for complex algorithms
- [ ] Document the no-crossing constraint
- [ ] Document the angle distribution algorithm
- [ ] Add usage examples in comments

**User Documentation:**
- [ ] Update README.md with radial extension feature
- [ ] Document configuration options in visual-config.json
- [ ] Add screenshots showing radial extensions
- [ ] Create before/after comparison images
- [ ] Document recommended settings for different use cases

**Configuration Documentation:**
- [ ] Add comments to visual-config.json explaining each parameter
- [ ] Document valid ranges for each parameter
- [ ] Provide example configurations (conservative, balanced, aggressive)
- [ ] Document performance implications of settings

**Logging:**
- [ ] Add debug logging for dense group detection
- [ ] Add debug logging for extension calculation
- [ ] Add debug logging for line crossing validation
- [ ] Add info logging for feature enable/disable
- [ ] Add warning logging for edge cases
- [ ] Ensure logs are helpful for troubleshooting

**Code Cleanup:**
- [ ] Remove debug Console.WriteLine statements
- [ ] Remove commented-out code
- [ ] Ensure consistent code formatting
- [ ] Run code analysis and fix warnings
- [ ] Verify no unused using statements
- [ ] Verify no unused variables

**Final Verification:**
- [ ] Run full application end-to-end
- [ ] Test all major user workflows
- [ ] Verify no regressions in existing features
- [ ] Verify performance is acceptable
- [ ] Get peer code review
- [ ] Address review feedback


## Detailed Implementation Specifications

### Configuration Structure

**visual-config.json additions:**
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
    "ProximityThresholdPixels": 10.0,
    "ExtensionLineLength": 40.0,
    "MinimumAngleSeparation": 15.0,
    "HardMinimumAngleSeparation": 5.0,
    "LineColor": "#80808080",
    "LineThickness": 1.5,
    "AnimateExtension": true,
    "ExtensionAnimationMs": 250,
    "ZoomThresholdForExtensions": 10.0
  }
}
```

### Model Class Specifications

**Models/RadialExtensionConfig.cs:**
```csharp
namespace InteractiveWorldMap.Models
{
    /// <summary>
    /// Configuration for radial extension lines that spread dense markers.
    /// </summary>
    public class RadialExtensionConfig
    {
        /// <summary>
        /// Master toggle for radial extension feature.
        /// </summary>
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// Minimum number of locations within proximity threshold to trigger extension.
        /// </summary>
        public int MinLocationsForExtension { get; set; } = 3;

        /// <summary>
        /// Distance threshold in screen pixels to consider locations densely packed.
        /// </summary>
        public double ProximityThresholdPixels { get; set; } = 10.0;

        /// <summary>
        /// Length of extension lines in screen pixels.
        /// </summary>
        public double ExtensionLineLength { get; set; } = 40.0;

        /// <summary>
        /// Preferred minimum degrees between adjacent extension lines.
        /// This is a target value; actual separation may be smaller for large groups.
        /// </summary>
        public double MinimumAngleSeparation { get; set; } = 15.0;

        /// <summary>
        /// Absolute minimum degrees between adjacent extension lines.
        /// Below this value, visual overlap occurs.
        /// </summary>
        public double HardMinimumAngleSeparation { get; set; } = 5.0;

        /// <summary>
        /// Color of extension lines in ARGB hex format.
        /// </summary>
        public string LineColor { get; set; } = "#80808080";

        /// <summary>
        /// Thickness of extension lines in pixels.
        /// </summary>
        public double LineThickness { get; set; } = 1.5;

        /// <summary>
        /// Whether to animate lines extending outward.
        /// </summary>
        public bool AnimateExtension { get; set; } = true;

        /// <summary>
        /// Duration of extension animation in milliseconds.
        /// </summary>
        public int ExtensionAnimationMs { get; set; } = 250;

        /// <summary>
        /// Minimum zoom level to enable radial extensions.
        /// Below this zoom level, extensions are not shown.
        /// </summary>
        public double ZoomThresholdForExtensions { get; set; } = 10.0;
    }
}
```


**Models/DenseMarkerGroup.cs:**
```csharp
using System.Collections.Generic;
using System.Windows;

namespace InteractiveWorldMap.Models
{
    /// <summary>
    /// Represents a group of markers that are densely packed in screen space.
    /// </summary>
    public class DenseMarkerGroup
    {
        /// <summary>
        /// Locations in this dense group.
        /// </summary>
        public List<Location> Locations { get; set; } = new List<Location>();

        /// <summary>
        /// Geometric center point of the group in screen coordinates.
        /// </summary>
        public Point CenterPoint { get; set; }

        /// <summary>
        /// Calculated radial extensions for each location.
        /// </summary>
        public List<RadialExtension> Extensions { get; set; } = new List<RadialExtension>();

        /// <summary>
        /// Number of locations in this group.
        /// </summary>
        public int Count => Locations.Count;
    }
}
```

**Models/RadialExtension.cs:**
```csharp
using System.Windows;

namespace InteractiveWorldMap.Models
{
    /// <summary>
    /// Represents a single radial extension line with marker positioning.
    /// </summary>
    public class RadialExtension
    {
        /// <summary>
        /// The location being extended.
        /// </summary>
        public Location Location { get; set; } = null!;

        /// <summary>
        /// Original position in screen coordinates (where marker would normally be).
        /// </summary>
        public Point OriginalPosition { get; set; }

        /// <summary>
        /// Extended position in screen coordinates (where marker will be placed).
        /// </summary>
        public Point ExtendedPosition { get; set; }

        /// <summary>
        /// Angle in degrees from center (0° = north, clockwise).
        /// </summary>
        public double Angle { get; set; }
    }
}
```

### Algorithm Specifications

**Dense Group Detection Algorithm:**
```
Input: List<(Location, Point)> markerPositions, double proximityThreshold
Output: List<DenseMarkerGroup>

1. Initialize empty list: groups = []
2. Initialize empty set: processed = {}

3. For each (location, position) in markerPositions:
   a. If location.Id in processed, skip
   
   b. Initialize cluster = [location]
   c. Initialize queue = [location]
   
   d. While queue is not empty:
      - current = queue.dequeue()
      - For each (otherLocation, otherPosition) in markerPositions:
        * If otherLocation.Id in processed or cluster, skip
        * distance = EuclideanDistance(position, otherPosition)
        * If distance <= proximityThreshold:
          - Add otherLocation to cluster
          - Add otherLocation to queue
   
   e. If cluster.Count >= MinLocationsForExtension:
      - Calculate center = Average(all positions in cluster)
      - Create DenseMarkerGroup with cluster and center
      - Add group to groups
      - Add all location IDs to processed

4. Return groups
```


**Radial Extension Calculation Algorithm:**
```
Input: DenseMarkerGroup group, RadialExtensionConfig config
Output: List<RadialExtension>

1. Initialize extensions = []
2. center = group.CenterPoint
3. markerCount = group.Locations.Count

4. Calculate natural angles for each location:
   For each location in group.Locations:
     dx = location.ScreenPosition.X - center.X
     dy = location.ScreenPosition.Y - center.Y
     angle = Atan2(dy, dx) converted to degrees
     angle = (angle + 90) % 360  // 0° = north, clockwise
     Store (location, angle) pair

5. Sort locations by natural angle (ascending)

6. Calculate angle separation:
   idealSeparation = 360.0 / markerCount
   
   If idealSeparation >= config.MinimumAngleSeparation:
     actualSeparation = idealSeparation
   Else if idealSeparation >= config.HardMinimumAngleSeparation:
     actualSeparation = idealSeparation
   Else:
     // Too many markers, cannot fit without overlap
     Return empty list (trigger fallback)

7. Distribute angles evenly:
   startAngle = sortedLocations[0].naturalAngle
   
   For i = 0 to markerCount - 1:
     extensionAngle = (startAngle + i * actualSeparation) % 360
     location = sortedLocations[i].location
     
     // Calculate extended position
     angleRadians = extensionAngle * (PI / 180)
     extendedX = center.X + config.ExtensionLineLength * Sin(angleRadians)
     extendedY = center.Y - config.ExtensionLineLength * Cos(angleRadians)
     
     // Check boundary collision
     If extendedX < 0 or extendedX > canvasWidth or
        extendedY < 0 or extendedY > canvasHeight:
       // Reduce extension length to fit
       adjustedLength = CalculateMaxLength(center, angleRadians, canvasBounds)
       extendedX = center.X + adjustedLength * Sin(angleRadians)
       extendedY = center.Y - adjustedLength * Cos(angleRadians)
     
     // Create extension
     extension = new RadialExtension {
       Location = location,
       OriginalPosition = location.ScreenPosition,
       ExtendedPosition = (extendedX, extendedY),
       Angle = extensionAngle
     }
     
     extensions.Add(extension)

8. Return extensions
```

**No-Crossing Validation Algorithm:**
```
Input: List<RadialExtension> extensions
Output: bool (true if valid, false if crossings detected)

1. If extensions.Count < 2:
   Return true  // Cannot cross with 0 or 1 line

2. For i = 0 to extensions.Count - 2:
   angle1 = extensions[i].Angle
   angle2 = extensions[i + 1].Angle
   
   // Calculate angular difference (accounting for 360° wrap)
   diff = (angle2 - angle1 + 360) % 360
   
   If diff < config.HardMinimumAngleSeparation:
     Log warning: "Insufficient angle separation"
     Return false

3. Return true
```


## File Structure

```
InteractiveWorldMap/
├── Models/
│   ├── RadialExtensionConfig.cs          [NEW]
│   ├── DenseMarkerGroup.cs               [NEW]
│   ├── RadialExtension.cs                [NEW]
│   └── VisualConfig.cs                   [MODIFY - add RadialExtension property]
├── Utilities/
│   └── RadialExtensionCalculator.cs      [NEW]
├── Views/
│   ├── ExtensionLine.xaml                [NEW]
│   └── ExtensionLine.xaml.cs             [NEW]
├── MainWindow.xaml.cs                    [MODIFY - integrate extensions]
├── visual-config.json                    [MODIFY - add RadialExtension section]
└── Tests/                                [NEW - if not exists]
    └── RadialExtensionCalculatorTests.cs [NEW]
```

## Integration Points

### MainWindow.xaml.cs Changes

**New Fields:**
```csharp
private List<DenseMarkerGroup> _denseGroups = new List<DenseMarkerGroup>();
private List<Line> _extensionLines = new List<Line>();
private RadialExtensionCalculator? _extensionCalculator;
```

**Constructor Modification:**
```csharp
// After loading _visualConfig
if (_visualConfig.RadialExtension.Enabled)
{
    _extensionCalculator = new RadialExtensionCalculator(_visualConfig.RadialExtension);
    _logger.LogInfo("RadialExtensionCalculator initialized");
}
```

**UpdateMarkerPositions() Modification:**
```csharp
private void UpdateMarkerPositions()
{
    var viewport = MapDisplay.CurrentViewport;
    if (viewport == null)
        return;

    // Clear existing extensions
    ClearExtensionLines();

    var containerWidth = MapDisplay.ActualWidth;
    var containerHeight = MapDisplay.ActualHeight;

    // Check if we should apply radial extensions
    bool shouldApplyExtensions = _visualConfig.RadialExtension.Enabled &&
                                  _extensionCalculator != null &&
                                  viewport.ZoomLevel >= _visualConfig.RadialExtension.ZoomThresholdForExtensions;

    if (shouldApplyExtensions)
    {
        // Calculate screen positions for all visible markers
        var markerPositions = CalculateMarkerScreenPositions(viewport, containerWidth, containerHeight);

        // Detect dense groups
        _denseGroups = _extensionCalculator.DetectDenseGroups(markerPositions);

        if (_denseGroups.Any())
        {
            _logger.LogInfo($"Detected {_denseGroups.Count} dense marker groups");

            foreach (var group in _denseGroups)
            {
                // Calculate extensions
                var extensions = _extensionCalculator.CalculateRadialExtensions(
                    group, 
                    containerWidth, 
                    containerHeight);

                // Validate no crossings
                if (_extensionCalculator.ValidateNoCrossings(extensions))
                {
                    group.Extensions = extensions;
                    ApplyRadialExtensions(group);
                }
                else
                {
                    _logger.LogWarning($"Line crossings detected for group with {group.Count} markers");
                    // Fall back to normal positioning for this group
                    ApplyNormalPositioning(group.Locations, viewport, containerWidth, containerHeight);
                }
            }

            // Position markers not in dense groups normally
            var markersInGroups = _denseGroups.SelectMany(g => g.Locations).ToHashSet();
            var normalMarkers = _individualMarkers
                .Where(m => !markersInGroups.Contains(m.Location))
                .ToList();
            
            foreach (var marker in normalMarkers)
            {
                PositionMarkerNormally(marker, viewport, containerWidth, containerHeight);
            }

            return;
        }
    }

    // Normal positioning (no extensions)
    foreach (var marker in _individualMarkers.Where(m => m.Visibility == Visibility.Visible))
    {
        PositionMarkerNormally(marker, viewport, containerWidth, containerHeight);
    }

    foreach (var marker in _clusterMarkers.Where(m => m.Visibility == Visibility.Visible))
    {
        PositionClusterMarkerNormally(marker, viewport, containerWidth, containerHeight);
    }
}
```


**New Helper Methods:**

```csharp
/// <summary>
/// Calculates screen positions for all visible markers.
/// </summary>
private Dictionary<Location, Point> CalculateMarkerScreenPositions(
    ViewportState viewport, 
    double containerWidth, 
    double containerHeight)
{
    var positions = new Dictionary<Location, Point>();

    foreach (var marker in _individualMarkers.Where(m => m.Visibility == Visibility.Visible))
    {
        var screenPos = viewport.SourceToScreen(
            marker.Location.PixelX,
            marker.Location.PixelY,
            containerWidth,
            containerHeight);
        
        positions[marker.Location] = screenPos;
    }

    return positions;
}

/// <summary>
/// Clears all extension lines from the canvas.
/// </summary>
private void ClearExtensionLines()
{
    foreach (var line in _extensionLines)
    {
        MapDisplay.Markers.Children.Remove(line);
    }
    _extensionLines.Clear();
}

/// <summary>
/// Applies radial extensions to a dense marker group.
/// </summary>
private void ApplyRadialExtensions(DenseMarkerGroup group)
{
    foreach (var extension in group.Extensions)
    {
        // Create extension line
        var line = CreateExtensionLine(extension);
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

    // Animate if configured
    if (_visualConfig.RadialExtension.AnimateExtension)
    {
        AnimateExtensionLines(_extensionLines.TakeLast(group.Extensions.Count).ToList());
    }
}

/// <summary>
/// Creates a visual extension line.
/// </summary>
private Line CreateExtensionLine(RadialExtension extension)
{
    var line = new Line
    {
        X1 = extension.OriginalPosition.X,
        Y1 = extension.OriginalPosition.Y,
        X2 = extension.ExtendedPosition.X,
        Y2 = extension.ExtendedPosition.Y,
        Stroke = new SolidColorBrush(
            (Color)ColorConverter.ConvertFromString(_visualConfig.RadialExtension.LineColor)),
        StrokeThickness = _visualConfig.RadialExtension.LineThickness,
        StrokeDashArray = new DoubleCollection { 2, 2 },
        Opacity = 0.8,
        IsHitTestVisible = false
    };

    // Add subtle shadow
    line.Effect = new DropShadowEffect
    {
        Color = Colors.Black,
        Direction = 270,
        ShadowDepth = 1,
        BlurRadius = 2,
        Opacity = 0.3
    };

    return line;
}

/// <summary>
/// Finds the LocationMarker for a given Location.
/// </summary>
private LocationMarker? FindMarkerForLocation(Location location)
{
    return _individualMarkers.FirstOrDefault(m => m.Location == location);
}

/// <summary>
/// Positions a marker at its normal (non-extended) location.
/// </summary>
private void PositionMarkerNormally(
    LocationMarker marker, 
    ViewportState viewport, 
    double containerWidth, 
    double containerHeight)
{
    var screenPos = viewport.SourceToScreen(
        marker.Location.PixelX,
        marker.Location.PixelY,
        containerWidth,
        containerHeight);

    var markerSize = _visualConfig.LocationMarkerSize;
    Canvas.SetLeft(marker, screenPos.X - markerSize / 2);
    Canvas.SetTop(marker, screenPos.Y - markerSize / 2);
}

/// <summary>
/// Applies normal positioning to a list of locations (fallback).
/// </summary>
private void ApplyNormalPositioning(
    List<Location> locations, 
    ViewportState viewport, 
    double containerWidth, 
    double containerHeight)
{
    foreach (var location in locations)
    {
        var marker = FindMarkerForLocation(location);
        if (marker != null)
        {
            PositionMarkerNormally(marker, viewport, containerWidth, containerHeight);
        }
    }
}
```


**Animation Method:**

```csharp
/// <summary>
/// Animates extension lines growing from center.
/// </summary>
private void AnimateExtensionLines(List<Line> lines)
{
    var duration = TimeSpan.FromMilliseconds(_visualConfig.RadialExtension.ExtensionAnimationMs);
    var easing = new QuadraticEase { EasingMode = EasingMode.EaseOut };

    for (int i = 0; i < lines.Count; i++)
    {
        var line = lines[i];
        
        // Store final positions
        var finalX2 = line.X2;
        var finalY2 = line.Y2;

        // Set initial positions (line starts at zero length)
        line.X2 = line.X1;
        line.Y2 = line.Y1;

        // Create animations
        var animX2 = new DoubleAnimation
        {
            From = line.X1,
            To = finalX2,
            Duration = duration,
            EasingFunction = easing,
            BeginTime = TimeSpan.FromMilliseconds(i * 10) // Stagger by 10ms
        };

        var animY2 = new DoubleAnimation
        {
            From = line.Y1,
            To = finalY2,
            Duration = duration,
            EasingFunction = easing,
            BeginTime = TimeSpan.FromMilliseconds(i * 10)
        };

        // Apply animations
        line.BeginAnimation(Line.X2Property, animX2);
        line.BeginAnimation(Line.Y2Property, animY2);
    }
}
```

## Testing Specifications

### Unit Test Examples

**Test: Dense Group Detection with 3 Markers**
```csharp
[Test]
public void DetectDenseGroups_ThreeMarkersWithin10Pixels_ReturnsOneGroup()
{
    // Arrange
    var config = new RadialExtensionConfig 
    { 
        ProximityThresholdPixels = 10.0,
        MinLocationsForExtension = 3
    };
    var calculator = new RadialExtensionCalculator(config);
    
    var positions = new Dictionary<Location, Point>
    {
        { new Location { Id = "1" }, new Point(100, 100) },
        { new Location { Id = "2" }, new Point(105, 102) },
        { new Location { Id = "3" }, new Point(98, 103) }
    };

    // Act
    var groups = calculator.DetectDenseGroups(positions);

    // Assert
    Assert.AreEqual(1, groups.Count);
    Assert.AreEqual(3, groups[0].Count);
}

[Test]
public void DetectDenseGroups_TwoMarkersWithin10Pixels_ReturnsNoGroups()
{
    // Arrange (MinLocationsForExtension = 3)
    var config = new RadialExtensionConfig 
    { 
        ProximityThresholdPixels = 10.0,
        MinLocationsForExtension = 3
    };
    var calculator = new RadialExtensionCalculator(config);
    
    var positions = new Dictionary<Location, Point>
    {
        { new Location { Id = "1" }, new Point(100, 100) },
        { new Location { Id = "2" }, new Point(105, 102) }
    };

    // Act
    var groups = calculator.DetectDenseGroups(positions);

    // Assert
    Assert.AreEqual(0, groups.Count);
}
```


**Test: Angle Distribution for 24 Markers**
```csharp
[Test]
public void CalculateRadialExtensions_24Markers_Returns15DegreesSeparation()
{
    // Arrange
    var config = new RadialExtensionConfig 
    { 
        MinimumAngleSeparation = 15.0,
        ExtensionLineLength = 40.0
    };
    var calculator = new RadialExtensionCalculator(config);
    
    var group = new DenseMarkerGroup
    {
        CenterPoint = new Point(500, 500),
        Locations = CreateLocationsInCircle(24, new Point(500, 500), 5)
    };

    // Act
    var extensions = calculator.CalculateRadialExtensions(group, 1000, 1000);

    // Assert
    Assert.AreEqual(24, extensions.Count);
    
    // Verify angles are evenly distributed
    for (int i = 0; i < extensions.Count - 1; i++)
    {
        double angleDiff = (extensions[i + 1].Angle - extensions[i].Angle + 360) % 360;
        Assert.AreEqual(15.0, angleDiff, 0.1, $"Angle separation at index {i}");
    }
}

[Test]
public void CalculateRadialExtensions_72Markers_Returns5DegreesSeparation()
{
    // Arrange
    var config = new RadialExtensionConfig 
    { 
        MinimumAngleSeparation = 15.0,
        HardMinimumAngleSeparation = 5.0,
        ExtensionLineLength = 40.0
    };
    var calculator = new RadialExtensionCalculator(config);
    
    var group = new DenseMarkerGroup
    {
        CenterPoint = new Point(500, 500),
        Locations = CreateLocationsInCircle(72, new Point(500, 500), 5)
    };

    // Act
    var extensions = calculator.CalculateRadialExtensions(group, 1000, 1000);

    // Assert
    Assert.AreEqual(72, extensions.Count);
    
    // Verify angles use hard minimum
    for (int i = 0; i < extensions.Count - 1; i++)
    {
        double angleDiff = (extensions[i + 1].Angle - extensions[i].Angle + 360) % 360;
        Assert.AreEqual(5.0, angleDiff, 0.1, $"Angle separation at index {i}");
    }
}
```

**Test: No-Crossing Validation**
```csharp
[Test]
public void ValidateNoCrossings_ValidAngles_ReturnsTrue()
{
    // Arrange
    var config = new RadialExtensionConfig { HardMinimumAngleSeparation = 5.0 };
    var calculator = new RadialExtensionCalculator(config);
    
    var extensions = new List<RadialExtension>
    {
        new RadialExtension { Angle = 0 },
        new RadialExtension { Angle = 15 },
        new RadialExtension { Angle = 30 },
        new RadialExtension { Angle = 45 }
    };

    // Act
    var result = calculator.ValidateNoCrossings(extensions);

    // Assert
    Assert.IsTrue(result);
}

[Test]
public void ValidateNoCrossings_InsufficientSeparation_ReturnsFalse()
{
    // Arrange
    var config = new RadialExtensionConfig { HardMinimumAngleSeparation = 5.0 };
    var calculator = new RadialExtensionCalculator(config);
    
    var extensions = new List<RadialExtension>
    {
        new RadialExtension { Angle = 0 },
        new RadialExtension { Angle = 3 },  // Only 3° separation
        new RadialExtension { Angle = 30 }
    };

    // Act
    var result = calculator.ValidateNoCrossings(extensions);

    // Assert
    Assert.IsFalse(result);
}

[Test]
public void ValidateNoCrossings_NonMonotonicAngles_ReturnsFalse()
{
    // Arrange
    var config = new RadialExtensionConfig { HardMinimumAngleSeparation = 5.0 };
    var calculator = new RadialExtensionCalculator(config);
    
    var extensions = new List<RadialExtension>
    {
        new RadialExtension { Angle = 0 },
        new RadialExtension { Angle = 30 },
        new RadialExtension { Angle = 15 }  // Out of order - would cause crossing
    };

    // Act
    var result = calculator.ValidateNoCrossings(extensions);

    // Assert
    Assert.IsFalse(result);
}
```


## Risk Assessment and Mitigation

### Technical Risks

| Risk | Probability | Impact | Mitigation |
|------|-------------|--------|------------|
| Performance degradation with 72 lines | Medium | Medium | Profile early, optimize algorithm, use spatial indexing |
| Line crossing bugs in edge cases | Medium | High | Comprehensive unit tests, validation checks, extensive manual testing |
| Animation stuttering | Low | Medium | Use WPF's built-in animation system, test on lower-end hardware |
| Configuration parsing errors | Low | Low | Provide defaults, validate on load, log errors clearly |
| Boundary collision edge cases | Medium | Medium | Test all corners and edges, implement robust collision detection |
| Memory leaks from line objects | Low | High | Proper cleanup in ClearExtensionLines(), test with repeated zoom cycles |

### Implementation Risks

| Risk | Probability | Impact | Mitigation |
|------|-------------|--------|------------|
| Scope creep (adding carousel, etc.) | Medium | Medium | Strict adherence to this plan, defer enhancements to Phase 2 |
| Integration conflicts with existing code | Low | Medium | Careful review of MainWindow.xaml.cs, incremental integration |
| Insufficient testing time | Medium | High | Allocate full 3 days for testing, automate where possible |
| Configuration complexity | Low | Low | Provide sensible defaults, document thoroughly |

## Success Criteria

### Functional Requirements
- [ ] Dense groups of 3+ markers within 10px are detected correctly
- [ ] Extension lines radiate from group center
- [ ] Lines never cross each other
- [ ] Markers are positioned at line endpoints
- [ ] Markers are clickable at extended positions
- [ ] Content subwindow opens correctly when clicking extended markers
- [ ] Extensions clear when zooming out
- [ ] Extensions recalculate when zooming in further
- [ ] Feature can be disabled via configuration
- [ ] All configuration parameters work as documented

### Performance Requirements
- [ ] Extension calculation completes in < 50ms for 72 markers
- [ ] No visible frame drops during animation
- [ ] No memory leaks after 100 zoom cycles
- [ ] Smooth 60fps animation

### Quality Requirements
- [ ] All unit tests pass
- [ ] Code coverage > 80% for new code
- [ ] No compiler warnings
- [ ] Code follows existing project style
- [ ] All public APIs documented with XML comments
- [ ] User documentation updated

## Timeline Summary

| Phase | Duration | Key Deliverables |
|-------|----------|------------------|
| Phase 1: Configuration and Models | 2 days | Config classes, model classes, unit tests |
| Phase 2: Core Algorithm | 3 days | RadialExtensionCalculator, comprehensive tests |
| Phase 3: Visual Components | 2 days | ExtensionLine view, styling |
| Phase 4: MainWindow Integration | 3 days | Integration code, helper methods |
| Phase 5: Animation | 2 days | Line and marker animations |
| Phase 6: Testing and Refinement | 3 days | Full test suite, bug fixes |
| Phase 7: Documentation and Polish | 2 days | Documentation, cleanup, final review |
| **Total** | **17 days** | **Fully functional radial extension feature** |

## Post-Implementation

### Future Enhancements (Not in Scope)
- Marker stacking with carousel for 73+ markers
- Touch gesture support for extension interaction
- Configurable line styles (solid, dotted, dashed)
- Color-coded lines based on location type
- Hover effects on lines
- Click lines to select marker
- Animated marker icons at extended positions

### Maintenance Considerations
- Monitor performance with real-world data
- Collect user feedback on angle separation preferences
- Consider adding telemetry for dense group statistics
- Plan for configuration UI in future release

## Appendix: Quick Reference

### Key Files to Create
1. `Models/RadialExtensionConfig.cs`
2. `Models/DenseMarkerGroup.cs`
3. `Models/RadialExtension.cs`
4. `Utilities/RadialExtensionCalculator.cs`
5. `Views/ExtensionLine.xaml`
6. `Views/ExtensionLine.xaml.cs`
7. `Tests/RadialExtensionCalculatorTests.cs`

### Key Files to Modify
1. `Models/VisualConfig.cs` - Add RadialExtension property
2. `MainWindow.xaml.cs` - Add integration code
3. `visual-config.json` - Add RadialExtension section

### Key Methods to Implement
1. `RadialExtensionCalculator.DetectDenseGroups()`
2. `RadialExtensionCalculator.CalculateRadialExtensions()`
3. `RadialExtensionCalculator.ValidateNoCrossings()`
4. `MainWindow.ClearExtensionLines()`
5. `MainWindow.ApplyRadialExtensions()`
6. `MainWindow.CreateExtensionLine()`
7. `MainWindow.AnimateExtensionLines()`

### Configuration Parameters
- `Enabled`: true/false
- `MinLocationsForExtension`: 3
- `ProximityThresholdPixels`: 10.0
- `ExtensionLineLength`: 40.0
- `MinimumAngleSeparation`: 15.0
- `HardMinimumAngleSeparation`: 5.0
- `LineColor`: "#80808080"
- `LineThickness`: 1.5
- `AnimateExtension`: true
- `ExtensionAnimationMs`: 250
- `ZoomThresholdForExtensions`: 10.0

---

**Document Version**: 1.0  
**Last Updated**: 2026-03-17  
**Status**: Ready for Implementation
