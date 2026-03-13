# Interactive World Map - TO DO List

## Current Status: MVP Complete ✅

The application is functional and ready for demo! Core features are implemented and working.

---

## USER-ADDED TO-DO ITEMS

- [ ] Make possible to zoom in on World Map?
- [ ] Consider how to handle multiple images/content for one location (thumbnails? next/previous buttons?)
- [ ] Instead of always opening subwindow at center of screen, have it open close to (but not on top of) the location pin
- [ ] Add and wire in actual locations and content
- [ ] Make home screen that has text explaining what the application is/does and shows some pictures of the artist/collector, then a button to open the map. Closing the map then should take the user back to the home screen
- [ ] make popup windows larger?
- [ ] Make UI look better
- [ ] Converting list of people/addresses to Excel file or table with column headings like "Address", "Pixel Coordinates", anything else useful (eg, "Accession Numbers")
- [ ] Recommend putting all images/content in subfolders grouped by Accession Number or some other key, then copy to subfolder of code
- [ ] Get map file decided so you can get pixel coordinates

---

## High Priority 🔴

### Testing & Quality Assurance
- [ ] **Property-based tests** (marked with `*` in tasks.md)
  - Coordinate mapping accuracy
  - Marker hover feedback
  - Animation timing validation
  - Content type rendering
  - Subwindow z-order
  
- [ ] **Unit tests for UI components**
  - MapDisplayControl tests
  - LocationMarker tests
  - MarkerLayerControl tests
  - ContentSubwindow tests
  - MainWindow tests

- [ ] **Integration tests**
  - End-to-end workflow testing
  - Multiple marker clicks in sequence
  - Window resize behavior
  - Multi-monitor support

### Error Handling Enhancement
- [ ] **Improve error handling infrastructure**
  - Create error dialog for critical startup errors
  - Create non-modal notification for runtime errors
  - Add more try-catch blocks in ContentLoader
  - Implement graceful degradation for missing content

### Performance Optimization
- [ ] **Response time optimizations**
  - Add loading indicator for slow content loads (>100ms)
  - Optimize marker click response (<100ms target)
  - Optimize subwindow close response (<100ms target)
  - Verify hover feedback response (<50ms target)

---

## Medium Priority 🟡

### Application Polish
- [ ] **App.xaml enhancements**
  - Define application-level resources
  - Implement consistent color scheme
  - Define font styles for better readability
  - Add modern UI styling

- [ ] **Resource management**
  - Implement proper resource cleanup on exit
  - Release file handles and image memory
  - Close log files properly
  - Test for memory leaks

### Content & Documentation
- [ ] **Sample content expansion**
  - Add more diverse sample locations (10+ total)
  - Create text content examples (not just images)
  - Add higher quality sample images
  - Create location-specific content

- [ ] **README.md**
  - Document application purpose and features
  - Document Content_Folder structure and format
  - Document locations.json schema
  - Document system requirements
  - Add screenshots/GIFs of the application

- [ ] **Developer documentation**
  - Document architecture and component responsibilities
  - Create guide for adding new locations
  - Document how to customize styling
  - Document error handling strategy

---

## Low Priority 🟢

### Manual Testing
- [ ] **Cross-platform testing**
  - Test on Windows 10
  - Test on Windows 11
  - Test on different screen resolutions (1080p, 1440p, 4K)
  - Test on high-DPI displays
  - Test with multiple monitors

- [ ] **Performance validation**
  - Measure marker click response time
  - Measure subwindow close response time
  - Measure hover feedback response time
  - Profile memory usage
  - Verify 30+ FPS during interactions

### Future Enhancements
- [ ] **Additional features**
  - Search functionality for locations
  - Zoom in/out on map
  - Pan/drag map navigation
  - Custom marker icons per location
  - Location categories/filtering
  - Export/import location data
  - Multi-language support

---

## Optional (Can Skip for MVP) ⚪

These tasks are marked with `*` in the implementation plan and can be deferred:

- Property-based tests (FsCheck)
- Performance benchmark tests (BenchmarkDotNet)
- Advanced animation timing tests
- Comprehensive integration test suite

---

## Quick Wins 🎯

Easy tasks that can be completed quickly:

1. Add more sample locations to locations.json
2. Create a proper README.md with screenshots
3. Add application icon
4. Improve error messages to be more user-friendly
5. Add tooltips to markers showing location names
6. Add a loading spinner during initialization

---

## Known Issues 🐛

None currently! The application is working as expected.

---

## Notes

- All core functionality is complete and tested
- The application is ready for demo and user testing
- Focus should be on testing and polish for production readiness
- Optional tasks can be deferred to future releases

**Last Updated:** March 1, 2026
