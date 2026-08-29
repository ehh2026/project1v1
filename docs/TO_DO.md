# Interactive World Map — Backlog

Human steering list. Implementation detail lives in [exec-plans/active/](exec-plans/active/). Composite-pin work is coordinated in [composite-pins-program.md](exec-plans/active/composite-pins-program.md).

**Last updated:** August 27, 2026

## Zoom & animation

- [ ] Finish smooth/fast zoom performance + appearance — [zoom-performance-appearance-plan.md](exec-plans/active/zoom-performance-appearance-plan.md). Phase 1, Phase 2a, and the high-quality full-map/animation scaling code are complete; the rendering slice still needs live full-map and zoom-in/out visual confirmation after Windows app-control approval timed out. Other remaining work includes shadow/effect cost, the keyframe bitmap I/O decision, marker crispness, shadow-opacity consistency, and noisy-warning cleanup. Rendering verification: [2026-07-01-map-render-quality.md](superpowers/plans/2026-07-01-map-render-quality.md).
- [ ] Review zoom-out implementation against zoom-in: compare rendering path, animation timing, smoothness, and image quality; optimize or share behavior where appropriate.
- [ ] Consider additional full-map presentation modes (`Uniform`, `UniformToFill`, letterboxed, or cropped) behind config/Runtime Tuning. Keep `Fill` as the current default; any implementation must share destination bounds across the image, marker and hit-target placement, source/screen conversion, and zoom animation frames.
- [ ] Consider a DPI- and physical-resolution-aware settled full-map render cache if direct `Fant` rendering consistently exceeds 33 ms on target hardware or produces repeated resize/return-to-map frame gaps above 33 ms. Key it by source identity, output pixel size, DPI, and presentation mode; invalidate it when any key input changes.

## Composite pins & manual layouts

Dashboard: [composite-pins-program.md](exec-plans/active/composite-pins-program.md)

- [ ] Fix or remove composite pins that overstretch shadow
- [ ] Manual GUI smoke only: generated AutoSeed loading — [manual-layout-seed-alignment-plan.md](exec-plans/active/manual-layout-seed-alignment-plan.md), Phase 3. Code and automated shared-path/load-key coverage are done; confirm in the running app for at least two seeded clusters.
- [ ] Manual GUI smoke only: layout persistence robustness — [manual-layout-seed-alignment-plan.md](exec-plans/active/manual-layout-seed-alignment-plan.md), Phase 5. Per-user storage, crash-proof load, size-independent full-map keys, and source-space saved positions are code complete; confirm a full-map layout survives resize and lands correctly.
- [ ] Explore post-render smooth black outline on composite pins (runtime, after shaft+head compose) — assess vs baked `outline_dark_*` asset variants; see feasibility notes in [composite-pins-program.md](exec-plans/active/composite-pins-program.md) or new exec plan if pursued
- [ ] Composite mode: user UI to reassign pin head asset (`HeadSourcePath` / `pin_XX_head.png` — effectively head color) — [manual-layout-pin-appearance-plan.md](exec-plans/active/manual-layout-pin-appearance-plan.md) (today heads are auto-picked by location hash; only **shaft** has right-click override; verify reassigned head persists on manual layout save/reload; infrastructure exists: `ManualLayoutMarker.HeadSourcePath`, enricher on save, replay via `preferredHeadSourcePath`; missing: head picker UI like shaft menu)
- [ ] Drawn mode: user UI to pick pin head color from a fixed palette and persist per location in manual layout save — [manual-layout-pin-appearance-plan.md](exec-plans/active/manual-layout-pin-appearance-plan.md) (today: random color at create; `SetPinColor` exists but no picker or layout field)
- [ ] Do not use bright yellow pin heads unless manually assigned
- [ ] Manual visual acceptance: drawn-pin divot caps — [drawn-pin-tip-cap-plan.md](exec-plans/active/drawn-pin-tip-cap-plan.md). Code and automated coverage are complete; compare `ScreenHorizontal` and `ShaftAligned` on normal/inverted/angled pins, then smoke drag/hover/zoom behavior.

## Inactive (optional polish)

- [ ] Manual GUI smoke only: verify content click/tap presentation, Translate independence, companion hiding/restoration, and Back cleanup in the running WPF app — [plan](exec-plans/inactive/content-presentation-mode-plan.md). Core code and automated verification are complete; parked because Windows app-control approval timed out.
- [ ] Composite head visual polish — shaft collar clip (§8.4 step 4), pin_09/10 shading (step 5), `TargetHeadRadiusPx` tuning (step 6) — [composite-pin-head-placement-fix-plan.md](exec-plans/inactive/composite-pin-head-placement-fix-plan.md)

## Refactoring & quality

Assessment: [LARGE_FILE_REFACTORING_ASSESSMENT.md](assessments/LARGE_FILE_REFACTORING_ASSESSMENT.md) — Phases 1–4 complete (2026-06-08). Refactoring assessment follow-through archived (2026-07-30) — [refactoring-assessment-followthrough-plan.md](exec-plans/completed/refactoring-assessment-followthrough-plan.md).

- [ ] Zoom-level doc cleanup — [ZOOM_LEVELS_AUDIT_ASSESSMENT.md](assessments/ZOOM_LEVELS_AUDIT_ASSESSMENT.md)

## Developer tooling

- [ ] Build the first portable Windows release: self-contained `win-x64` zip, package-local config helper, user-provided `Production-Content`, and tag/manual GitHub Actions publishing — [windows-portable-release-plan.md](exec-plans/active/windows-portable-release-plan.md). Deferred: code signing, installer/updater, `win-arm64`, and .NET 8 migration.
- [ ] Increase test coverage from the latest full-run snapshot of **49.4% line / 44.7% branch** toward 50% line / 45% branch (then 60%/50%): MainWindow source guards, radial adjuster/calculator, and ContentLoader didactic/caption/error-path expansions are merged (tests-only); Qodo review hardening is verified. Still ~0.6pp line / ~0.3pp branch short of the next ratchet. Blocking gates remain 45%/40%.
- [ ] Decide whether the gallery installation runs unattended. If it does, the setup work is listed in [unattended-kiosk.md](reference/unattended-kiosk.md): automatic restart, auto-logon, disabled sleep/screen blanking, single-instance enforcement, a startup failure path that exits instead of showing a message box, and an idle reset back to the full map. Mostly Windows configuration rather than code. Turning off `EnableDeveloperTools` is the one item that matters even with staff present.
- [ ] Known logging limitation, low priority: when the log file cannot be opened (most likely a second copy of the app holding it), the writer holds up to 2000 lines and writes them at the next log message or at shutdown. It does not wake on its own while idle, so those lines are delayed until something is next logged, and are lost if the process is killed rather than closed. Fixing it means replacing the consumer loop with a timed wait and hand-rolling the shutdown semantics `GetConsumingEnumerable` currently provides — judged not worth the risk to that code. Single-instance enforcement would remove the likeliest cause instead; see [unattended-kiosk.md](reference/unattended-kiosk.md).
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
- [ ] Look into adapting the app to be able to run in a browser over the internet

## Deferred

- [ ] Zoomed-map source/scale alternatives: evaluate a lower default zoom or native-resolution zoom cap after the resampler comparison.
- [ ] Zoomed-map source quality: seek a substantially higher-resolution lossless or vector replacement for the raster JPEG.
- [ ] Zoomed-map hybrid rendering: consider vector overlays for labels and country borders over the raster base.
- [ ] Zoomed-map neural super-resolution: evaluate offline only, with strict rejection criteria for invented geography, halos, and deformed text.
- [ ] Tuning panel: variant search/filter — type-to-filter or grouping for 60+ shaft variant folders in combo pickers. Deferred from [tuning-panel-dropdowns-plan.md](exec-plans/completed/tuning-panel-dropdowns-plan.md) v1; dropdown picker basics are complete, and this is parked until the list size becomes a real workflow drag.
- [ ] Intermittent divot cap inside a stub-looking pin head near Japan/China — not currently reproducible after stale-cap refresh and head-layer safeguards; revisit if observed again.
