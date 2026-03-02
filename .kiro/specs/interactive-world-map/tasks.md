# Implementation Plan: Interactive World Map

## Overview

This implementation plan breaks down the Interactive World Map application into discrete coding tasks. The application is a WPF desktop application built with C# that displays a full-screen world map with interactive location markers. The implementation follows the MVVM pattern with clear separation between presentation, interaction, and content management layers.

## Tasks

- [x] 1. Set up project structure and dependencies
  - Create WPF application project targeting .NET 6.0+
  - Add required NuGet packages (Newtonsoft.Json for location data parsing)
  - Create folder structure: Models, ViewModels, Views, Services, Utilities
  - Set up logging infrastructure (ILogger interface and file logger implementation)
  - Create Content_Folder structure with placeholder files
  - _Requirements: 5.1, 5.2, 8.1_

- [ ] 2. Implement core data models and validation
  - [x] 2.1 Create core data model classes
    - Implement Location class with Id, Name, Latitude, Longitude, ContentFilePath, ContentType properties
    - Implement LocationContentType enum (Image, Text)
    - Implement ApplicationState class for managing app state
    - Implement event argument classes (LocationClickedEventArgs, MarkerHoverEventArgs, LoadErrorEventArgs)
    - _Requirements: 2.1, 3.1_

  - [ ]* 2.2 Write property test for coordinate validation
    - **Property 3: Coordinate Mapping Accuracy**
    - **Validates: Requirements 2.2, 2.5**

  - [x] 2.3 Implement CoordinateValidator utility class
    - Implement IsValid method to check latitude (-90 to 90) and longitude (-180 to 180) ranges
    - Implement Clamp method to constrain coordinates to valid ranges
    - _Requirements: 2.2, 2.5_

  - [x] 2.4 Write unit tests for CoordinateValidator
    - Test edge cases: poles, date line, out-of-range values
    - Test clamping behavior
    - _Requirements: 2.2_

- [ ] 3. Implement coordinate mapping system
  - [x] 3.1 Create CoordinateMapper class
    - Implement LatLongToScreen method using Equirectangular projection
    - Implement ScreenToLatLong method for reverse mapping
    - Implement UpdateProjection method to handle window resize
    - Add MapBounds and ScreenSize properties
    - _Requirements: 2.2, 2.5_

  - [ ]* 3.2 Write property test for coordinate mapping accuracy
    - **Property 3: Coordinate Mapping Accuracy**
    - **Validates: Requirements 2.2, 2.5**

  - [ ]* 3.3 Write unit tests for CoordinateMapper
    - Test specific known coordinates (equator, prime meridian, poles)
    - Test round-trip conversion accuracy
    - Test behavior with different screen resolutions
    - _Requirements: 2.2, 2.5_

- [ ] 4. Implement content loading system
  - [x] 4.1 Create ContentLoader service class
    - Implement LoadMapImageAsync method to load world map from Content_Folder
    - Implement LoadLocationsAsync method to parse locations.json
    - Implement LoadLocationContentAsync method with caching
    - Implement ValidateContentFolder method for startup validation
    - Add ContentFolderPath property and IsInitialized flag
    - _Requirements: 5.1, 5.2, 5.5_

  - [x] 4.2 Implement StartupValidator class
    - Implement ValidateEnvironment method to check folder structure
    - Check for Content_Folder existence
    - Check for world_map.png existence
    - Validate locations.json format
    - Return ValidationResult with errors and warnings
    - _Requirements: 5.3, 5.4_

  - [ ]* 4.3 Write unit tests for content loading
    - Test missing Content_Folder error handling
    - Test missing world map error handling
    - Test corrupted JSON handling
    - Test supported image format loading (PNG, JPG, BMP)
    - _Requirements: 5.3, 5.4, 5.5_

  - [ ]* 4.4 Write property test for image format support
    - **Property 12: Image Format Support**
    - **Validates: Requirements 5.5**

- [ ] 5. Checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [ ] 6. Implement MapDisplayControl component
  - [x] 6.1 Create MapDisplayControl UserControl
    - Create XAML layout with Image control using Stretch.Uniform
    - Implement MapImage property (ImageSource)
    - Implement LoadMapImage method
    - Implement GetMapPosition method for coordinate-to-screen conversion
    - Implement IsPointOnMap method for bounds checking
    - Calculate and expose ActualMapSize and MapBounds properties
    - _Requirements: 1.1, 1.2, 1.3, 1.4_

  - [ ]* 6.2 Write property test for aspect ratio preservation
    - **Property 1: Aspect Ratio Preservation During Scaling**
    - **Validates: Requirements 1.2, 1.3**

  - [ ]* 6.3 Write unit tests for MapDisplayControl
    - Test map loading with various image sizes
    - Test aspect ratio preservation at different screen resolutions
    - Test bounds calculation
    - _Requirements: 1.2, 1.3_

- [ ] 7. Implement LocationMarker component
  - [x] 7.1 Create LocationMarker custom control
    - Create XAML template with Ellipse shape (12x12 pixels)
    - Apply radial gradient brush (white center, accent edge)
    - Implement Location and ScreenPosition properties
    - Implement IsHovered property with visual state changes
    - Implement ContainsPoint method for hit testing
    - _Requirements: 2.1, 2.3_

  - [x] 7.2 Implement marker animations
    - Create AnimateHover method with 200ms ease-out scale to 1.2x
    - Create AnimateClick method with 100ms pulse effect
    - Use WPF Storyboard and DoubleAnimation
    - _Requirements: 2.4, 6.2, 7.3_

  - [ ]* 7.3 Write property test for marker hover feedback
    - **Property 4: Marker Hover Feedback**
    - **Validates: Requirements 2.4**

  - [ ]* 7.4 Write property test for marker animation timing
    - **Property 17: Hover Feedback Response Time**
    - **Validates: Requirements 7.3**

  - [ ]* 7.5 Write unit tests for LocationMarker
    - Test hover animation triggers
    - Test click animation triggers
    - Test hit testing accuracy
    - _Requirements: 2.4, 6.2_

- [ ] 8. Implement MarkerLayerControl component
  - [x] 8.1 Create MarkerLayerControl Canvas control
    - Create XAML Canvas for absolute positioning
    - Implement Markers ObservableCollection property
    - Implement AddMarker method to create and position markers
    - Implement RemoveMarker method
    - Implement UpdateMarkerPositions method for window resize
    - Wire up CoordinateMapper for positioning
    - _Requirements: 2.1, 2.2_

  - [x] 8.2 Implement marker interaction handling
    - Implement HitTest method using VisualTreeHelper
    - Wire up mouse click events to MarkerClicked event
    - Wire up mouse hover events to MarkerHovered event
    - _Requirements: 2.4, 3.1_

  - [ ]* 8.3 Write property test for marker rendering completeness
    - **Property 2: Marker Rendering Completeness**
    - **Validates: Requirements 2.1**

  - [ ]* 8.4 Write unit tests for MarkerLayerControl
    - Test marker addition and removal
    - Test hit testing with multiple markers
    - Test position updates on resize
    - _Requirements: 2.1, 2.2_

- [ ] 9. Checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [ ] 10. Implement ContentSubwindow component
  - [x] 10.1 Create ContentSubwindow Window class
    - Create XAML with borderless window style and drop shadow
    - Set window size to 400x300 pixels
    - Implement Content property for dynamic content
    - Implement AssociatedLocation property
    - Implement ContainsPoint method for click detection
    - _Requirements: 3.2, 3.5_

  - [x] 10.2 Implement content rendering logic
    - Implement ShowContent method to load content based on type
    - Use Image control for LocationContentType.Image
    - Use TextBlock for LocationContentType.Text
    - Implement positioning logic to center on screen or near marker
    - _Requirements: 3.3, 3.4_

  - [x] 10.3 Implement subwindow animations
    - Create AnimateOpen method with 150ms fade-in and scale (0.9 to 1.0)
    - Create AnimateClose method with 100ms fade-out
    - Use WPF Storyboard with DoubleAnimation for opacity and scale
    - _Requirements: 6.3_

  - [ ]* 10.4 Write property test for content type rendering
    - **Property 7: Content Type Rendering**
    - **Validates: Requirements 3.3, 3.4**

  - [ ]* 10.5 Write property test for subwindow z-order
    - **Property 6: Subwindow Z-Order**
    - **Validates: Requirements 3.2**

  - [ ]* 10.6 Write property test for subwindow animation timing
    - **Property 14: Subwindow Animation on State Change**
    - **Validates: Requirements 6.3**

  - [ ]* 10.7 Write unit tests for ContentSubwindow
    - Test content loading for images and text
    - Test positioning logic
    - Test animation completion
    - _Requirements: 3.3, 3.4, 6.3_

- [ ] 11. Implement MainWindow component
  - [ ] 11.1 Create MainWindow XAML and code-behind
    - Set WindowState to Maximized and WindowStyle to None for full-screen
    - Add MapDisplayControl to window
    - Add MarkerLayerControl overlay
    - Add placeholder for ContentSubwindow
    - Implement MapDisplay, MarkerLayer, and ActiveSubwindow properties
    - _Requirements: 1.1, 3.2_

  - [ ] 11.2 Implement window initialization logic
    - Create Initialize method to load map and locations
    - Call ContentLoader to load map image and location data
    - Populate MarkerLayer with location markers
    - Handle startup validation errors
    - _Requirements: 5.1, 5.2, 5.3, 5.4_

  - [ ] 11.3 Implement subwindow management
    - Create ShowContentForLocation method to open subwindow
    - Create CloseActiveSubwindow method
    - Implement HandleOutsideClick method for click detection
    - Ensure only one subwindow is active at a time
    - Implement focus management on subwindow close
    - _Requirements: 3.1, 4.1, 4.2, 4.3_

  - [ ] 11.4 Implement keyboard shortcuts
    - Handle Escape key to close application
    - Handle Alt+F4 to close application
    - _Requirements: 8.3_

  - [ ]* 11.5 Write property test for marker click opens subwindow
    - **Property 5: Marker Click Opens Subwindow**
    - **Validates: Requirements 3.1**

  - [ ]* 11.6 Write property test for outside click closes subwindow
    - **Property 9: Outside Click Closes Subwindow**
    - **Validates: Requirements 4.1**

  - [ ]* 11.7 Write property test for marker click replaces subwindow
    - **Property 10: Marker Click Replaces Active Subwindow**
    - **Validates: Requirements 4.2**

  - [ ]* 11.8 Write property test for focus return
    - **Property 11: Focus Return on Subwindow Close**
    - **Validates: Requirements 4.3**

  - [ ]* 11.9 Write property test for map visibility behind subwindow
    - **Property 8: Map Visibility Behind Subwindow**
    - **Validates: Requirements 3.6**

  - [ ]* 11.10 Write unit tests for MainWindow
    - Test initialization with valid content
    - Test subwindow open and close
    - Test keyboard shortcuts
    - Test outside click detection
    - _Requirements: 3.1, 4.1, 4.2, 8.3_

- [ ] 12. Checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [ ] 13. Implement error handling and logging
  - [ ] 13.1 Create error handling infrastructure
    - Implement ILogger interface with LogError, LogWarning, LogInfo methods
    - Create FileLogger implementation writing to %APPDATA%/InteractiveWorldMap/logs/app.log
    - Create error dialog for critical startup errors
    - Create non-modal notification for runtime errors
    - _Requirements: 5.3, 5.4_

  - [ ] 13.2 Add error handling to ContentLoader
    - Wrap file I/O in try-catch blocks
    - Handle FileNotFoundException with placeholder content
    - Handle UnauthorizedAccessException with error notification
    - Log all errors with context
    - _Requirements: 5.3, 5.4_

  - [ ] 13.3 Add error handling to MainWindow
    - Handle InvalidOperationException in subwindow display
    - Implement graceful degradation for missing location content
    - Show "Content not available" message for missing files
    - _Requirements: 5.3, 5.4_

  - [ ]* 13.4 Write unit tests for error handling
    - Test missing folder error display
    - Test missing map error display
    - Test missing content file graceful degradation
    - Test error logging
    - _Requirements: 5.3, 5.4_

- [ ] 14. Implement application entry point and resource management
  - [ ] 14.1 Create App.xaml and App.xaml.cs
    - Define application-level resources (colors, styles, brushes)
    - Implement consistent color scheme for modern UI
    - Define font styles for readable text
    - _Requirements: 6.1, 6.4, 6.5_

  - [ ] 14.2 Implement application startup logic
    - Override OnStartup to run StartupValidator
    - Display critical error dialog if validation fails
    - Initialize MainWindow if validation succeeds
    - _Requirements: 5.3, 5.4_

  - [ ] 14.3 Implement resource cleanup
    - Override OnExit to dispose resources
    - Release file handles and image memory
    - Close log files
    - _Requirements: 8.5_

  - [ ]* 14.4 Write property test for resource cleanup
    - **Property 19: Resource Cleanup on Exit**
    - **Validates: Requirements 8.5**

  - [ ]* 14.5 Write property test for window management support
    - **Property 18: Window Management Support**
    - **Validates: Requirements 8.2**

  - [ ]* 14.6 Write unit tests for application lifecycle
    - Test startup with valid environment
    - Test startup with invalid environment
    - Test resource disposal on exit
    - _Requirements: 8.5_

- [ ] 15. Implement response time optimizations
  - [ ] 15.1 Optimize marker click response
    - Ensure subwindow opens within 100ms of click
    - Use async/await for content loading without blocking UI
    - Show loading indicator if content takes longer than 100ms
    - _Requirements: 7.1_

  - [ ] 15.2 Optimize subwindow close response
    - Ensure close animation starts within 100ms of click
    - Use immediate animation start without delays
    - _Requirements: 7.2_

  - [ ]* 15.3 Write property test for marker click response time
    - **Property 15: Marker Click Response Time**
    - **Validates: Requirements 7.1**

  - [ ]* 15.4 Write property test for subwindow close response time
    - **Property 16: Subwindow Close Response Time**
    - **Validates: Requirements 7.2**

  - [ ]* 15.5 Write performance benchmark tests
    - Benchmark marker click to subwindow display (target: <100ms)
    - Benchmark outside click to subwindow close (target: <100ms)
    - Benchmark hover to visual feedback (target: <50ms)
    - Use BenchmarkDotNet for accurate measurements
    - _Requirements: 7.1, 7.2, 7.3_

- [ ] 16. Checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [ ] 17. Create sample content and documentation
  - [ ] 17.1 Create sample Content_Folder structure
    - Add sample world_map.png (high-resolution world map image)
    - Create locations.json with 5-10 sample locations
    - Add sample content files (images and text) for each location
    - _Requirements: 5.1, 5.2_

  - [ ] 17.2 Create README.md
    - Document application purpose and features
    - Document Content_Folder structure and format
    - Document locations.json schema
    - Document system requirements (Windows 10+, .NET 6.0+)
    - Document how to run the application
    - _Requirements: 8.1_

  - [ ] 17.3 Create developer documentation
    - Document architecture and component responsibilities
    - Document how to add new locations
    - Document how to customize styling
    - Document error handling strategy
    - _Requirements: 6.1_

- [ ] 18. Integration testing and final validation
  - [ ]* 18.1 Write integration tests for end-to-end workflows
    - Test complete workflow: launch → click marker → view content → close subwindow
    - Test multiple marker clicks in sequence
    - Test window resize behavior
    - Test multi-monitor support
    - _Requirements: 1.1, 2.1, 3.1, 4.1, 8.4_

  - [ ] 18.2 Manual testing checklist
    - Test on Windows 10 and Windows 11
    - Test on different screen resolutions (1920x1080, 2560x1440, 3840x2160)
    - Test on high-DPI displays
    - Test with multiple monitors
    - Test all keyboard shortcuts
    - Test error scenarios (missing files, corrupted data)
    - Verify frame rate during interactions (target: 30+ FPS)
    - _Requirements: 1.4, 7.4, 8.1, 8.4_

  - [ ] 18.3 Performance validation
    - Measure and verify marker click response time (<100ms)
    - Measure and verify subwindow close response time (<100ms)
    - Measure and verify hover feedback response time (<50ms)
    - Profile memory usage and check for leaks
    - _Requirements: 7.1, 7.2, 7.3, 8.5_

- [ ] 19. Final checkpoint - Ensure all tests pass and application is ready
  - Ensure all tests pass, ask the user if questions arise.

## Notes

- Tasks marked with `*` are optional and can be skipped for faster MVP
- Each task references specific requirements for traceability
- Property-based tests use FsCheck for .NET with minimum 100 iterations
- Unit tests target 80% code coverage minimum
- Checkpoints ensure incremental validation throughout implementation
- All property tests must include comment tags referencing design document properties
- Performance requirements (7.1, 7.2, 7.3) are validated through benchmark tests and manual testing
- The application uses WPF with .NET 6.0+ and C# 10.0+
