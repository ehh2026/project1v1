# Viewport-Based Zoom Implementation Plan

## Problem Statement

Current implementation uses `RenderTransform` to scale the entire 16397×11085 pixel image (181 megapixels), causing 2-3 second freezes during zoom animations. Even with `NearestNeighbor` scaling mode, WPF must process the entire bitmap despite only ~1/12th being visible on screen at 3.5x zoom.

## Solution Overview

Implement a viewport-based rendering system that only displays the visible portion of the map. Instead of transforming the entire image, we'll:
1. Keep the full source image in memory (one-time decode cost)
2. Calculate which rectangle of the source image is visible
3. Extract and display only that rectangle using `CroppedBitmap` or `WriteableBitmap`
4. Update the viewport during zoom/pan animations

**Performance gain:** Process ~2 megapixels (viewport) instead of 181 megapixels (full image) = 90x reduction

## Architecture Changes

### Current Architecture
```
Image (16397×11085) 
  → RenderTransform (ScaleTransform + TranslateTransform)
  → GPU processes entire 181MP image
  → Display visible portion
```

### New Architecture
```
BitmapSource (16397×11085, loaded once)
  → Calculate visible rectangle based on zoom/pan
  → CroppedBitmap (extract visible ~1920×1080 region)
  → Image displays only cropped region
  → GPU processes ~2MP
```

---

## Phase 1: Viewport Infrastructure

### 1.1 Create Viewport State Model
- [x] Create `Models/ViewportState.cs`
- [x] Properties:
  - [x] `SourceImageWidth` (double) - Full image width (16397)
  - [x] `SourceImageHeight` (double) - Full image height (11085)
  - [x] `ViewportX` (double) - Top-left X coordinate in source image space
  - [x] `ViewportY` (double) - Top-left Y coordinate in source image space
  - [x] `ViewportWidth` (double) - Width of visible rectangle in source image space
  - [x] `ViewportHeight` (double) - Height of visible rectangle in source image space
  - [x] `ZoomLevel` (double) - Current zoom level (1.0 = full map, 3.5 = zoomed)
- [x] Methods:
  - [x] `GetSourceRect()` - Returns `Int32Rect` for `CroppedBitmap`
  - [x] `SourceToScreen(double x, double y)` - Convert source coords to screen coords
  - [x] `ScreenToSource(double x, double y)` - Convert screen coords to source coords
  - [x] `CreateFullMapView(double containerWidth, double containerHeight)` - Factory for unzoomed state
  - [x] `CreateZoomedView(double centerX, double centerY, double zoomLevel, double containerWidth, double containerHeight)` - Factory for zoomed state

### 1.2 Create Viewport Calculator Service
- [x] Create `Services/ViewportCalculator.cs`
- [x] Method: `Interpolate(ViewportState start, ViewportState end, double progress)` - Interpolate between states for animation
- [x] Method: `CalculateZoomToPoint(double sourceX, double sourceY, double zoomLevel, double containerWidth, double containerHeight)` - Calculate viewport to center on a point
- [x] Note: ClampViewport is implemented as private static method in ViewportState

### 1.3 Update MapDisplayControl
- [x] Add `BitmapSource _sourceImage` field to store full image
- [x] Add `ViewportState _currentViewport` field
- [x] Remove `RenderTransform` from XAML (no more ScaleTransform/TranslateTransform)
- [x] Change `Image` to display cropped bitmap instead of full image
- [x] Add `UpdateViewport(ViewportState viewport)` method to update displayed region
- [x] Modify `LoadMapImage()` to store source and initialize viewport

---

## Phase 2: Viewport Rendering

### 2.1 Implement CroppedBitmap Rendering
- [x] In `MapDisplayControl.UpdateViewport()`:
  - [x] Calculate `Int32Rect` from `ViewportState`
  - [x] Handle edge cases (viewport extends beyond image bounds)
  - [x] Create `CroppedBitmap` from source image
  - [x] Set `MapImage.Source` to cropped bitmap
  - [x] Update `MapImage.Stretch` to `Fill` (viewport already sized correctly)
- [x] Add error handling for invalid rectangles
- [x] Add logging for viewport updates

### 2.2 Update Coordinate Mapping
- [x] Modify `GetMapPosition()` to use viewport coordinates
- [x] Update to convert from source image coords to viewport screen coords
- [x] Add `GetSourcePosition()` method (inverse of `GetMapPosition`)
- [x] Remove old transform-based coordinate calculations

### 2.3 Update MapBounds Property
- [x] Change `MapBounds` to return viewport bounds instead of full image bounds
- [x] Update to reflect current viewport state
- [x] Ensure marker positioning uses viewport coordinates

---

## Phase 3: Zoom Animation with Viewport

### 3.1 Replace Transform-Based Zoom
- [x] In `MainWindow.AnimateZoomToCluster()`:
  - [x] Remove `ScaleTransform` and `TranslateTransform` animations
  - [x] Calculate target `ViewportState` for cluster center
  - [x] Create viewport interpolation animation using CompositionTarget.Rendering
  - [x] In animation frame handler, interpolate viewport and call `UpdateViewport()`
  - [x] Keep same 400ms duration and CubicEase easing
- [x] Remove `MapDisplay.SetAnimating()` calls (no longer needed)
- [x] Remove pre-rendering warmup (no longer needed)

### 3.2 Replace Transform-Based Zoom Out
- [x] In `MainWindow.AnimateZoomOut()`:
  - [x] Remove `ScaleTransform` and `TranslateTransform` animations
  - [x] Calculate target `ViewportState` for full map view
  - [x] Create viewport interpolation animation using CompositionTarget.Rendering
  - [x] In animation frame handler, interpolate viewport and call `UpdateViewport()`
  - [x] Keep same 400ms duration and CubicEase easing

### 3.3 Update Animation Frame Handler
- [x] Use `CompositionTarget.Rendering` event for smooth frame updates
- [x] Calculate interpolation progress from animation timeline
- [x] Call `ViewportCalculator.Interpolate()` to get intermediate state
- [x] Call `MapDisplay.UpdateViewport()` to render
- [x] Keep frame logging for debugging

---

## Phase 4: Marker Positioning with Viewport

### 4.1 Update Marker Canvas Sizing
- [x] In `AddClustersToMap()`:
  - [x] Canvas fills entire control (handled by XAML Stretch)
  - [x] Remove canvas transform (markers positioned in screen space now)

### 4.2 Update Marker Positioning Logic
- [x] In `AddIndividualMarker()`:
  - [x] Initial position set to (0,0), updated by UpdateMarkerPositions()
  - [x] Position markers in screen space relative to viewport
- [x] In `AddClusterMarker()`:
  - [x] Initial position set to (0,0), updated by UpdateMarkerPositions()
  - [x] Position markers in screen space relative to viewport

### 4.3 Dynamic Marker Updates
- [x] Add `UpdateMarkerPositions()` method in `MainWindow`
- [x] Call after each viewport update during animation
- [x] Recalculate screen positions for all visible markers
- [x] Update `Canvas.SetLeft()` and `Canvas.SetTop()` for each marker

### 4.4 Remove Counter-Scaling
- [x] Delete `CounterScaleMarkers()` method (no longer needed)
- [x] Remove `RenderTransform` from markers
- [x] Markers stay constant size naturally (no transform applied)

---

## Phase 5: Testing and Validation

### 5.1 Viewport Calculation Tests
- [ ] Create `Tests/ViewportCalculatorTests.cs`
- [ ] Test full map viewport calculation
- [ ] Test zoomed viewport calculation
- [ ] Test viewport clamping at image edges
- [ ] Test viewport interpolation
- [ ] Test coordinate conversions (source ↔ screen)

### 5.2 Integration Testing
- [ ] Test zoom-in animation smoothness
- [ ] Test zoom-out animation smoothness
- [ ] Test marker positioning at full map view
- [ ] Test marker positioning at zoomed view
- [ ] Test marker positioning during animation
- [ ] Test multiple zoom in/out cycles
- [ ] Test edge cases (zoom to corners, edges)

### 5.3 Performance Validation
- [ ] Measure frame times during zoom animation
- [ ] Verify no multi-second freezes
- [ ] Target: 60fps (16ms per frame) or 30fps (33ms per frame)
- [ ] Log viewport update times
- [ ] Compare before/after performance metrics

---

## Phase 6: Cleanup and Polish

### 6.1 Remove Old Transform Code
- [ ] Remove `ScaleTransform` and `TranslateTransform` from XAML
- [ ] Remove `MapScaleTransform` and `MapTranslateTransform` properties
- [ ] Remove `SetAnimating()` method from `MapDisplayControl`
- [ ] Remove `IsZoomed` property (use `ViewportState.ZoomLevel` instead)
- [ ] Clean up unused transform-related code

### 6.2 Update Logging
- [ ] Add viewport state logging (viewport rect, zoom level)
- [ ] Log viewport updates during animation
- [ ] Remove transform-related logging
- [ ] Add performance metrics (viewport update time)

### 6.3 Code Documentation
- [ ] Document `ViewportState` class with XML comments
- [ ] Document `ViewportCalculator` methods
- [ ] Update `MapDisplayControl` documentation
- [ ] Add architecture diagram to this document
- [ ] Document coordinate system conversions

---

## Phase 7: Future Enhancements (Optional)

### 7.1 Viewport Caching
- [ ] Cache multiple viewport crops at different zoom levels
- [ ] Reuse cached crops during animation
- [ ] Implement LRU cache eviction

### 7.2 Progressive Loading
- [ ] Load lower resolution version first for instant display
- [ ] Load full resolution in background
- [ ] Swap when ready

### 7.3 Smooth Panning (Future Feature)
- [ ] Add mouse drag support for panning
- [ ] Update viewport during drag
- [ ] Add momentum/inertia to panning

---

## Risk Assessment

### High Risk
- **Coordinate system complexity**: Converting between source, viewport, and screen coordinates requires careful math
  - Mitigation: Comprehensive unit tests, visual debugging
- **Marker positioning during animation**: Markers must update every frame
  - Mitigation: Optimize marker update loop, consider only updating visible markers

### Medium Risk
- **CroppedBitmap performance**: Creating new CroppedBitmap each frame might be slow
  - Mitigation: Profile and consider WriteableBitmap if needed
- **Edge case handling**: Viewport at image boundaries
  - Mitigation: Robust clamping logic, extensive edge case testing

### Low Risk
- **Animation smoothness**: Viewport updates should be fast
  - Mitigation: Profile viewport calculation and rendering

---

## Success Criteria

- [ ] Zoom animation completes in 400ms with no freezes
- [ ] Frame rate during animation: minimum 30fps (33ms per frame)
- [ ] Markers positioned correctly at all zoom levels
- [ ] Markers stay aligned with map during animation
- [ ] Image quality remains sharp (no interpolation artifacts)
- [ ] All existing tests pass
- [ ] New viewport tests pass

---

## Estimated Timeline

- Phase 1: 2-3 hours (infrastructure)
- Phase 2: 2-3 hours (rendering)
- Phase 3: 2-3 hours (animation)
- Phase 4: 2-3 hours (markers)
- Phase 5: 2-3 hours (testing)
- Phase 6: 1-2 hours (cleanup)

**Total: 11-17 hours**

---

## Notes

- Keep old transform-based code in git history for reference
- Test on actual hardware to validate performance improvements
- Consider making viewport size configurable for different screen resolutions
- Document the coordinate system clearly for future maintenance
