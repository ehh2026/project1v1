# Interactive World Map - TO DO List

## Current Status: MVP Complete ✅

The application is functional and ready for demo! Core features are implemented and working.

---

## USER-ADDED TO-DO ITEMS

- [ ] Make the manual-layout seed generator use the same placement algorithm the app uses at runtime
  - Current state: `scripts/generate_manual_layout_seeds.ps1` is a PowerShell port of the radial placement logic, not a shared code path
  - Goal: extract or expose the placement math so runtime placement and seed generation cannot drift
  - Add verification that generated layout keys and endpoints match the app's current `RadialExtensionCalculator` and `LayoutKeyGenerator` behavior
- [ ] Make generated manual-layout seeds reliably load in the app
  - Current state: the app can read saved layouts through `ManualLayoutManager`, and `MainWindow` now respects `ManualLayoutEditor.LayoutStoragePath`
  - Remaining work: make sure generator output keys are compatible with actual zoomed cluster views and that seeded layouts are found/applied without manual intervention
  - Verify with `Images&Content/manual-layouts.json` produced by the generator
- [x] Implement composite pin rendering from pin parts — [pin-parts-composite-placement-plan.md](exec-plans/active/pin-parts-composite-placement-plan.md)
  - Phases 1–3 complete: metadata, config/loading, and composite marker rendering
  - Phase 4 complete: placement calculator with nearest-fit selection and segmented shaft stretch
  - Phase 5 partially complete: composite pins live for extended markers in the radial-extension pipeline, gated by `PinParts.Enabled` + `PinParts.UseCompositeRendering`
  - Edit mode falls back to legacy draggable markers; exiting edit mode refreshes composite rendering
  - **Remaining work to finish making composite pins live:**
- [x] Route manual-layout replay through composite path — [plan Phase 5](exec-plans/active/pin-parts-composite-placement-plan.md#phase-5-mainwindow-integration)
  - Fixed 2026-06-06: `ApplyManualLayout()` now calls `TryApplyCompositePinMarker()` first; falls back to legacy only when composite rendering is disabled or assets are missing
  - Original position is now re-projected from source coords (consistent with `ApplyRadialExtensions` path)
- [ ] Add derived composite-placement caching for saved manual layouts — [plan lines 167–199](exec-plans/active/pin-parts-composite-placement-plan.md#canonical-endpoint-data-vs-saved-composite-placement-results)
  - Currently every view rebuild recalculates pair selection and transforms from scratch
  - Persist `selected_pair_id`, `head_rotation_deg`, `body_stretch_factor`, local anchors, etc.
  - Add invalidation keyed on layout variant id, viewport size, and geometry metadata version
- [ ] Decide non-extended pin rendering policy — [plan lines 769–771](exec-plans/active/pin-parts-composite-placement-plan.md#open-decisions)
  - Currently non-extended image pins always use legacy `ImagePinMarker`
  - Plan recommends keeping legacy for non-extended until extended-marker behavior is stable
- [ ] Run Phase 6 verification — [plan Phase 6](exec-plans/active/pin-parts-composite-placement-plan.md#phase-6-verification-and-tuning)
  - Spot-check composite rendering with `Debug.ShowCompositePinDebugOverlay: true` at common extension angles
  - Capture before/after screenshots for representative angles
  - Run `scripts/verify.ps1` end-to-end on a .NET 6 SDK machine
- [ ] Make manual edit mode available and verified for composite pin layouts
  - Edit mode currently forces rebuild to legacy; composite hit-testing exists but needs integration verification
  - Verify drag behavior, hit testing, save/load, and visual feedback in zoomed cluster views
- [ ] Add support for multiple saved layout variants per cluster/viewport and a way to choose between them
  - Current state: layouts are keyed and persisted, but effectively one saved layout is selected per generated key
  - Goal: support named alternatives or slots so different pin arrangements can be saved and loaded intentionally
  - Distinguish `AutoSeed` layouts from user-adjusted `Manual` layouts so generated starting points are never silently treated as user-authored layouts
  - Add UI and storage model changes for save-as, list available layouts, load selected layout, and delete obsolete variants
- [ ] Instead of always opening subwindow at center of screen, have it open close to (but not on top of) the location pin?
- [ ] Add and wire in actual locations and content
- [ ] Make home screen that has text explaining what the application is/does and shows some pictures of the artist/collector, then a button to open the map. Closing the map then should take the user back to the home screen
- [ ] make popup windows larger?
- [ ] Make UI look better
- [ ] Converting list of people/addresses to Excel file or table with column headings like "Address", "Pixel Coordinates", anything else useful (eg, "Accession Numbers")
- [ ] Recommend putting all images/content in subfolders grouped by Accession Number or some other key, then copy to subfolder of code
- [ ] Get map file decided so you can get pixel coordinates
- [ ] consider addressing issues in REFACTOR ASSESSMENT
- [ ] *Add welcome / instructions screen*
- [ ] Add support for ordering images/content
- [ ] Add explanatory/bio popup window per marker
- [ ] Fix marker distortion when highly zoomed (50x+)
- [ ] Fix some markers being misplaced on zoomed-out map — see **High Priority → Bugs** (unzoomed marker offset)
- [ ] Don't animate extension lines being drawn until fully zoomed in
- [ ] Improve filtering of logging for easier debugging
- [ ] Improve smoothness and quickness of zooming (check logs, cache/load, deltas; increase number of frames, decrease interval if rate is throttled?)


---

## High Priority 🔴

### Platform & Toolchain
- [ ] **Consider upgrading from .NET 6 to .NET 8 LTS**
  - .NET 6 is out of support; .NET 8 is the current LTS for desktop/WPF
  - Update `TargetFramework` in `InteractiveWorldMap.csproj` and `Tests/InteractiveWorldMap.Tests.csproj`
  - Update `global.json`, `.github/workflows/ci.yml` (`dotnet-version`), and docs (`README`, `AGENTS.md`, `SETUP_GUIDE`)
  - Run full `.\scripts\verify.ps1` and fix any breaking API/package changes
  - Decide whether to jump to .NET 10 or standardize on .NET 8 LTS first

### Security & CI

- [x] **Add security CI (Dependabot, Gitleaks, NuGet audit)** — [security-ci-plan.md](exec-plans/completed/security-ci-plan.md) (Phases 1–3 complete; CodeQL deferred)
  - P0: Dependabot for NuGet (`/` + `Tests/`) and GitHub Actions; Gitleaks workflow on push/PR; `dotnet list package --vulnerable` in CI and `verify.ps1`
  - P1 (optional): CodeQL for C#
  - Defer: Grype/Trivy (no containers), Snyk, SBOM until dependency surface or release pipeline grows
  - Phase 0 first: local Gitleaks + vulnerability baseline so first CI run is not a surprise

### Bugs
- [x] **Fix marker / map centering offset at initial far-out (unzoomed) view** — [UNZOOMED_MARKER_OFFSET_ASSESSMENT.md](UNZOOMED_MARKER_OFFSET_ASSESSMENT.md)
  - Fixed 2026-06-06: `SourceToScreen` and `ScreenToSource` in `Models/ViewportState.cs` now derive scale from `GetSourceRect()` (the actual integer crop) instead of the virtual letterbox viewport. `CroppedBitmap` always uses integer pixel bounds so the marker transform must match.
  - 18 regression tests added in `Tests/ViewportStateTests.cs` covering wide/tall containers, center mapping, and round-trips.

### Navigation & Zoom
- [ ] **Audit intended vs operational zoom levels** — [ZOOM_LEVELS_AUDIT_ASSESSMENT.md](ZOOM_LEVELS_AUDIT_ASSESSMENT.md) ✅ investigated 2026-06-06
  - **Finding:** two user-facing resting states (full map `ZoomLevel ≈ 1.0`, cluster zoom `ZoomLevel = ZoomScale` **55.0**); no discrete multi-level UI; animation interpolates 1→55 only during transitions
  - **March 18 (`5f32adb`):** same two-state model and same `ZoomScale: 55.0`; core viewport files unchanged since then; docs still cite obsolete 3.0–3.5× examples
  - **Remaining follow-up:** doc cleanup and optional `ZoomScale` tuning — not missing zoom levels; see assessment for dead APIs and navigation-stack notes

### Testing & Quality Assurance
- [ ] **Resolve nullable reference warnings (CS8602 / CS8604)**
  - Current state: Release build reports 14 warnings (7 unique sites × WPF temp + main project); build still succeeds and CI is not blocked
  - `MainWindow.xaml.cs`: `marker.Cluster` dereferences in cluster-marker positioning loops (~737, ~854, ~891); `_extensionCalculator` use after compound null check (~769); `viewport` passed to `ApplyManualLayout` inside guarded block (~1183)
  - `Utilities/ExcelCoordinateReader.cs`: XML node indexing after `Count > 0` checks (~126, ~216)
  - Priority: guard or narrow `ClusterMarker.Cluster` first (type allows null even though markers are created with `Cluster` set); then clean up flow-analysis gaps for `viewport` and `_extensionCalculator`; Excel reader last (lowest runtime risk)
  - Acceptance: `dotnet build -c Release` with no CS8602/CS8604 in these files, or explicit null-forgiving only where invariants are documented; run `.\scripts\verify.ps1`

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

- ~~**Unzoomed marker offset:**~~ Fixed 2026-06-06 — `ViewportState.SourceToScreen` now uses `GetSourceRect()` scale. See [UNZOOMED_MARKER_OFFSET_ASSESSMENT.md](UNZOOMED_MARKER_OFFSET_ASSESSMENT.md).

---

## Notes

- All core functionality is complete and tested
- The application is ready for demo and user testing
- Focus should be on testing and polish for production readiness
- Optional tasks can be deferred to future releases

**Last Updated:** June 5, 2026
