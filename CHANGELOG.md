# Changelog

All notable changes to this project are documented here. Format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/). Versioning follows [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

- `global.json` — pins .NET 6 SDK for local/CI alignment; documents side-by-side install with newer SDKs
- `Models/ContentFileNames.cs` — canonical content filenames (resolves TD-002)
- `Tests/Architecture/GoldenPrincipleTests.cs` — Views must not use `Images&Content` paths or `JObject`
- `scripts/doc_gardening.py` and `.github/workflows/doc-gardening.yml` — weekly doc drift checks
- `docs/agent-failures.md` — harness feedback log for repeatable agent mistakes
- `scripts/compute_pin_part_geometry.ps1` and `Images&Content/Pins_v2/parts/pin_part_geometry.json` — derive shaft endpoints and head centers for cropped pin part PNGs in both local and original pin coordinates
- `docs/exec-plans/active/pin-parts-composite-placement-plan.md` — plan the shift from single-bitmap pins to composite shaft/head placement using `Pins_v2/parts`

 - `Models/PinPartConfig.cs`, `Models/PinPartGeometry.cs`, `Models/PinPlacementTarget.cs`, and `Services/PinPartPlacementCalculator.cs` â€” add typed pin-part metadata/config and deterministic pair-selection logic
 - `Models/CompositePinRenderPlan.cs`, `Services/CompositePinRenderPlanBuilder.cs`, and `Views/CompositePinMarker.xaml*` â€” add the first isolated composite-pin render path with segmented shaft layers and rotated head placement
 - `Tests/PinPartPlacementCalculatorTests.cs`, `Tests/CompositePinRenderPlanBuilderTests.cs`, and updated `Tests/ContentLoaderTests.cs` â€” cover selection, metadata loading, and exact render-plan anchoring

### Changed

- `ManualLayout` storage and loading - support grouped layout variants with explicit origins (`AutoSeed`, `Manual`, `Imported`) so generated seeds and user-adjusted layouts stay distinct

- `docs/PIN_IMAGE_PLACEMENT_ASSESSMENT.md` and `docs/exec-plans/active/pin-parts-composite-placement-plan.md` - clarify that the automatic radial endpoint distributor and manual layout edit/save/load workflow are still present and should remain the upstream source of endpoint placement
- `MainWindow` and `ManualLayoutManager` â€” respect `ManualLayoutEditor.LayoutStoragePath` and fall back to compatible layout keys so generated manual-layout seeds can be found and applied more reliably
- `scripts/generate_manual_layout_seeds.ps1` and `Images&Content/manual-layouts.json` â€” add a rough manual-layout seed generator and save an initial seed set for multi-location clusters

- `LayerDependencyTests` — scans type references, not only `using` directives
- `ClusterMarker` — stamp image supplied by MainWindow via `ContentLoader` (no path construction in Views)
- `ContentLoader` — `GetWorldMapPath`, `ResolveContentFilePath`, `TryLoadContentBitmap`
- `StartupValidator` and `MainWindow` — use `ContentFileNames` for map and content paths
- `scripts/verify_taste.py` — Views `Images&Content` and `JObject` checks; removed `ClusterMarker` console grandfather
- `.github/workflows/ci.yml` — headless startup validation step (parity with `verify.ps1`)
- `AGENTS.md` — merge gate note and link to agent failure log
- `InteractiveWorldMap.csproj` — exclude `backups/` from compilation
- `Tests/InteractiveWorldMap.Tests.csproj` — `LangVersion` 11 for test project build
- `docs/exec-plans/active/README.md` — placeholder so doc links to active plans resolve

### Changed

- `.cursor/hooks.json` — removed per-turn `stop` verify reminder (too noisy); verification stays at task completion in `AGENTS.md`
- `scripts/verify.ps1` — fall back to `py -3` when `python` is missing or unavailable (e.g. unconfigured pyenv)
- `Tests` — C# 10-compatible JSON fixtures (no `LangVersion` 11 required on .NET 6 SDK)
- `ContentLoader` — optional `ExcelCoordinateFilePath` for isolated location-loading tests
- `ContentLoaderTests` — skip repo Excel via `ExcelCoordinateFilePath` override
- `README.md`, `docs/SETUP_GUIDE.md`, `AGENTS.md` — .NET 6 SDK install and `global.json` guidance
- `docs/TO_DO.md` — item to consider .NET 8 LTS upgrade

- `scripts/compute_pin_part_geometry.ps1` and `pin_part_geometry.json` Ã¢â‚¬â€ now emit head attach points, stub directions, shaft image sizes, and segmented-shaft heuristics for composite rendering
- `visual-config.json` and `Models/VisualConfig.cs` Ã¢â‚¬â€ add `PinParts` configuration for geometry loading and staged composite rendering
- `docs/exec-plans/active/pin-parts-composite-placement-plan.md` Ã¢â‚¬â€ now tracks the completed isolated render slice and the remaining exact-fit vs clamp integration gap
- `MainWindow.xaml.cs` Ã¢â‚¬â€ now swaps extended image pins onto the composite shaft/head renderer behind the `PinParts.UseCompositeRendering` gate and restores legacy marker visuals automatically when extensions are not active

### Impact

- Minor harness release: stricter CI and mechanical golden-principle enforcement; no user-visible UI behavior change intended.

## [0.2.0] - 2026-06-04

### Verified

- Harness checks on macOS (2026-06-04): `verify_doc_links.py` passed (33 files), `verify_taste.py` passed
- `dotnet build`/`test` require Windows (WPF `net6.0-windows`); full verification via `scripts/verify.ps1` or GitHub Actions `windows-latest` CI

### Added

- Agent harness per OpenAI harness engineering principles
- `AGENTS.md` — agent entry map with progressive disclosure pointers
- `ARCHITECTURE.md` — layer model, invariants, domain map
- `docs/index.md`, `docs/QUALITY_SCORE.md`, `docs/RELIABILITY.md`, `docs/SECURITY.md`
- `docs/exec-plans/` — active, completed, and tech-debt tracker
- `docs/design-docs/` — golden principles and design index
- `docs/agent-workflows.md` — Ralph Wiggum agent loop
- `InteractiveWorldMap.sln` and GitHub Actions CI (Windows)
- `scripts/verify.ps1` and `scripts/verify.sh` — unified verification
- `scripts/verify_taste.py` — taste invariant checks
- `scripts/query_logs.ps1` — agent-queryable log tail/filter
- `scripts/validate_startup.ps1` — headless startup validation
- `Tests/Architecture/LayerDependencyTests.cs` — structural layer enforcement
- `Tests/StartupValidatorTests.cs` and `Tests/ContentLoaderTests.cs`
- `.cursor/rules/project-harness.mdc` and `wpf-architecture.mdc`
- `.editorconfig` for deterministic formatting
- `.cursor/hooks.json` — format reminder on agent stop

### Changed

- `README.md` — updated development status to reflect MVP and harness docs
- `scripts/verify.sh` — discovers Homebrew `dotnet@6`; falls back to harness-only mode on macOS when WPF build is unavailable

### Impact

- **Minor version bump (0.2.0):** New developer/agent tooling and CI; no breaking API changes to the desktop app.

## [0.1.0] - 2025-12-01

### Added

- Initial MVP: WPF interactive world map with markers, clustering, zoom, content popups
- Excel coordinate loading, visual config, manual layout editor
- xUnit tests for coordinate utilities and clustering
- Kiro spec workflow in `.kiro/specs/interactive-world-map/`
