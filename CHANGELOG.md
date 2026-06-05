# Changelog

All notable changes to this project are documented here. Format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/). Versioning follows [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

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
