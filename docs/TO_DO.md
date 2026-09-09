# Interactive World Map — Backlog

Human steering list. Implementation detail lives in [exec-plans/active/](exec-plans/active/) or, when paused, [exec-plans/inactive/](exec-plans/inactive/).

**Last updated:** September 9, 2026

## Website (TOP PRIORITY — do this next)

- [ ] Gallery website version of the map — [staged plan](exec-plans/active/web-map-plan.md) and [assessment](assessments/WEB_ADAPTATION_ASSESSMENT.md). **Stage 3A engineering is code-complete:** lazy size-bounded crops, 8192 px tile pyramid (native level opt-in), stable `#location=` deep links, accessibility basics, gallery-resilience states, portrait-phone popups, viewport-derived zoom-out + Reset, and automated regression gates. Remaining Stage 3A: human browser/phone checks (tile seams/fetching, deep-link back/forward, keyboard/focus return, failed-image state, portrait popup scroll), Stage 0 brand answers, and production-content rerun. **Stage 3B:** optional clustering/lightbox only if production review justifies them. **Stage 4:** deploy and validate the actual gallery embed.

## Zoom & animation

- [ ] Review zoom-out implementation against zoom-in: compare rendering path, animation timing, smoothness, and image quality; optimize or share behavior where appropriate.
- [ ] Consider additional full-map presentation modes (`Uniform`, `UniformToFill`, letterboxed, or cropped) behind config/Runtime Tuning. Keep `Fill` as the current default; any implementation must share destination bounds across the image, marker and hit-target placement, source/screen conversion, and zoom animation frames.
- [ ] Consider a DPI- and physical-resolution-aware settled full-map render cache if direct `Fant` rendering consistently exceeds 33 ms on target hardware or produces repeated resize/return-to-map frame gaps above 33 ms. Key it by source identity, output pixel size, DPI, and presentation mode; invalidate it when any key input changes.

## Inactive (optional polish)

- [ ] On hold: finish smooth/fast zoom performance + appearance — [plan](exec-plans/inactive/zoom-performance-appearance-plan.md). Phase 1, Phase 2a, and high-quality scaling are complete; remaining rendering confirmation and polish await reprioritization.
- [ ] On hold: composite-pin/manual-layout program — [dashboard](exec-plans/inactive/composite-pins-program.md). This includes shadow/stretch follow-up, generated AutoSeed/persistence GUI smoke ([seed plan](exec-plans/inactive/manual-layout-seed-alignment-plan.md)), composite/drawn pin appearance pickers ([appearance plan](exec-plans/inactive/manual-layout-pin-appearance-plan.md)), and drawn-pin cap visual acceptance ([cap plan](exec-plans/inactive/drawn-pin-tip-cap-plan.md)).

- [ ] Manual GUI smoke only: verify content click/tap presentation, Translate independence, companion hiding/restoration, and Back cleanup in the running WPF app — [plan](exec-plans/inactive/content-presentation-mode-plan.md). Core code and automated verification are complete; parked because Windows app-control approval timed out.
- [ ] Composite head visual polish — shaft collar clip (§8.4 step 4), pin_09/10 shading (step 5), `TargetHeadRadiusPx` tuning (step 6) — [composite-pin-head-placement-fix-plan.md](exec-plans/inactive/composite-pin-head-placement-fix-plan.md)

## Refactoring & quality

Assessment: [LARGE_FILE_REFACTORING_ASSESSMENT.md](assessments/LARGE_FILE_REFACTORING_ASSESSMENT.md) — Phases 1–4 complete (2026-06-08). Refactoring assessment follow-through archived (2026-07-30) — [refactoring-assessment-followthrough-plan.md](exec-plans/completed/refactoring-assessment-followthrough-plan.md).

- [ ] Zoom-level doc cleanup — [ZOOM_LEVELS_AUDIT_ASSESSMENT.md](assessments/ZOOM_LEVELS_AUDIT_ASSESSMENT.md)

## Developer tooling

- [ ] Build the first portable Windows release: self-contained `win-x64` zip, package-local config helper, user-provided `Production-Content`, and tag/manual GitHub Actions publishing — [windows-portable-release-plan.md](exec-plans/active/windows-portable-release-plan.md). Deferred: code signing, installer/updater, `win-arm64`, and .NET 8 migration.
- [ ] Increase test coverage from the latest full-run snapshot of **49.4% line / 44.7% branch** toward 50% line / 45% branch (then 60%/50%): MainWindow source guards, radial adjuster/calculator, and ContentLoader didactic/caption/error-path expansions are merged (tests-only); Qodo review hardening is verified. Still ~0.6pp line / ~0.3pp branch short of the next ratchet. Blocking gates remain 45%/40%.
- [ ] Optional kiosk hardening beyond the gallery's daily restart and periodic checks: auto-logon, single-instance enforcement, hang detection, and an idle reset back to the full map — [unattended-kiosk.md](reference/unattended-kiosk.md). Basic automatic restart after exit, a non-blocking automatic-startup failure path, and bounded crash logs are shipped. Turning off `EnableDeveloperTools` is the one item that matters even with staff present.
- [ ] Known logging limitation, low priority: when the log file cannot be opened (most likely a second copy of the app holding it), the writer holds up to 2000 lines and writes them at the next log message or at shutdown. It does not wake on its own while idle, so those lines are delayed until something is next logged, and are lost if the process is killed rather than closed. Fixing it means replacing the consumer loop with a timed wait and hand-rolling the shutdown semantics `GetConsumingEnumerable` currently provides — judged not worth the risk to that code. Single-instance enforcement would remove the likeliest cause instead; see [unattended-kiosk.md](reference/unattended-kiosk.md).
- [ ] Crash-log rotation, low priority: serialize the threshold check, rotation, and append across processes so simultaneous terminal crashes cannot discard `app.crash.log.1`. Defer because the watchdog's `start /wait` path runs one app process at a time; the realistic consequence is weaker forensic logs, not an unavailable exhibit. If revisited, use an inter-process mutex or lock file and add a cross-process test.
- [ ] Expose content-window appearance controls in Runtime Tuning/config: border thickness and color, corner roundness, font family, font size, and font color.

## User ideas (product)

- [ ] Main content display: consider sizing the display window to the selected content's aspect ratio.
- [ ] Main content images: support image zoom/pan within the content viewer for closer inspection.
- [ ] When content is maximized, provide a tap-and-hold magnifier for close inspection; define release/cancel behavior and interaction with existing tap-to-restore presentation mode.
- [ ] Subwindow opens near pin, not screen center
- [ ] Keep popup windows contained within the main map window, with popup position and scale derived from the main window's current bounds (lower priority for the full-screen gallery deployment, but required for windowed use).
- [ ] Home / welcome screen before map
- [ ] Larger popup windows; general UI polish
- [ ] Wire in actual locations and content; accession-number folder structure
- [ ] Consider and optimize how images, didactic (bio) text, and captions are stored and read in. Likely keep images in the existing Excel file as extra columns alongside names and coordinates (`image 1 filename`, `image 2 filename`, etc.), then add two new Excel sheets: one for bio text with `Name` and `Bio Text` columns, and one for captions with `Name`, `Image filename`, and `Caption text` columns.
- [ ] Welcome / instructions screen
- [ ] Content ordering; bio popup per marker
- [ ] Don't animate extension lines until fully zoomed in
- [ ] Consider intermediate zoom levels and free panning — explore discrete zoom steps between full map and cluster zoom, plus drag-to-pan on the map canvas (today: cluster click zoom + back only)
- [ ] Better logging filters; smoother zoom

## High priority

- [ ] Consider .NET 8 LTS upgrade (from .NET 6)
- [ ] Marker distortion at 50x+ zoom
- [ ] Manual acceptance for settled zoomed-map rendering: compare the five Runtime Tuning modes on the target monitor, choose the default, and decide whether the measured first-generation custom-filter delay (about 1.7-3.3 seconds at 1440p here; cached afterward) needs further optimization — [plan](superpowers/plans/2026-07-01-zoomed-map-upscaling.md)

## Medium priority

- [ ] App.xaml styling and resource cleanup
- [ ] Sample content expansion; README screenshots
- [ ] Error handling infrastructure (startup dialog, runtime notifications)

## Low priority

- [ ] Cross-platform / resolution manual testing
- [ ] Search, pan, categories, export — future enhancements

## Deferred

- [ ] Zoomed-map source/scale alternatives: evaluate a lower default zoom or native-resolution zoom cap after the resampler comparison.
- [ ] Zoomed-map source quality: seek a substantially higher-resolution lossless or vector replacement for the raster JPEG.
- [ ] Zoomed-map hybrid rendering: consider vector overlays for labels and country borders over the raster base.
- [ ] Zoomed-map neural super-resolution: evaluate offline only, with strict rejection criteria for invented geography, halos, and deformed text.
- [ ] Tuning panel: variant search/filter — type-to-filter or grouping for 60+ shaft variant folders in combo pickers. Deferred from [tuning-panel-dropdowns-plan.md](exec-plans/completed/tuning-panel-dropdowns-plan.md) v1; dropdown picker basics are complete, and this is parked until the list size becomes a real workflow drag.
- [ ] Intermittent divot cap inside a stub-looking pin head near Japan/China — not currently reproducible after stale-cap refresh and head-layer safeguards; revisit if observed again.
