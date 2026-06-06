# Agent Map — Interactive World Map

> **Humans steer. Agents execute.** When something fails, ask: what capability is missing, and how do we make it legible and enforceable?

## Project

Windows desktop app (WPF / .NET 6 / C#) displaying a full-screen interactive world map with clickable location markers and content popups.

## Quick Commands

```bash
# Build and test (run before claiming work is done)
./scripts/verify.sh          # macOS / Linux (build + test + harness checks)
.\scripts\verify.ps1         # Windows (full verification)

# Manual steps
dotnet build InteractiveWorldMap.sln
dotnet test Tests/InteractiveWorldMap.Tests.csproj
dotnet run --project InteractiveWorldMap.csproj   # Windows UI only
.\run-demo.bat                                    # Windows: build + launch
.\scripts\validate_startup.ps1                  # Headless startup check (Windows)
```

**Platform note:** WPF requires Windows for UI. macOS can build and run unit tests only.

**SDK:** `global.json` pins **.NET 6 SDK** for this repo (side-by-side with newer SDKs). If `dotnet test` fails locally, install .NET 6 SDK — see [docs/SETUP_GUIDE.md](docs/SETUP_GUIDE.md).

**Merge gate:** GitHub Actions on `windows-latest` runs build, test, NuGet vulnerability scan, doc links, taste checks, and headless startup validation; **Gitleaks** runs separately on Ubuntu. Local Windows gate: `.\scripts\verify.ps1`. macOS `verify.sh` may pass in harness-only mode — not sufficient alone before merge.

## Repository Layout

| Path | Purpose |
|------|---------|
| `Models/` | Data models, config types, event args — no upstream project refs |
| `Utilities/` | Pure helpers: coordinates, clustering, Excel reading |
| `Services/` | Content loading, logging, navigation, layout, validation |
| `Views/` | WPF UserControls and windows — Models only |
| `Tests/` | xUnit tests including architecture structural tests |
| `scripts/` | Verification, log query, Python tooling |
| `Images&Content/` | Map image, location folders, `locations.json` |
| `visual-config.json` | Machine-readable UI/debug config (deserialize to `Models/VisualConfig`) |
| `Coordinates for map.xlsx` | Primary coordinate source at startup |

## Architecture Rules

See [ARCHITECTURE.md](ARCHITECTURE.md) for layer diagram and invariants. Summary:

- **Models** → nothing upstream
- **Utilities / Services** → Models only (Services may use Utilities)
- **Views** → Models only (no Services or Utilities in Views)
- **MainWindow / App** → orchestrates all layers

Violations are caught by `Tests/Architecture/LayerDependencyTests.cs`.

## Where to Look Next (progressive disclosure)

| Need | Read |
|------|------|
| Doc catalog | [docs/index.md](docs/index.md) |
| Architecture detail | [ARCHITECTURE.md](ARCHITECTURE.md) |
| Quality grades | [docs/QUALITY_SCORE.md](docs/QUALITY_SCORE.md) |
| Logging & startup | [docs/RELIABILITY.md](docs/RELIABILITY.md) |
| Security | [docs/SECURITY.md](docs/SECURITY.md) |
| Agent workflow | [docs/agent-workflows.md](docs/agent-workflows.md) |
| Agent failure log | [docs/agent-failures.md](docs/agent-failures.md) |
| Golden principles | [docs/design-docs/golden-principles.md](docs/design-docs/golden-principles.md) |
| Active work | [docs/exec-plans/active/](docs/exec-plans/active/) |
| Tech debt | [docs/exec-plans/tech-debt-tracker.md](docs/exec-plans/tech-debt-tracker.md) |
| Formal spec | [.kiro/specs/interactive-world-map/](.kiro/specs/interactive-world-map/) |
| Feature docs | [docs/VISUAL_CONFIG.md](docs/VISUAL_CONFIG.md), [docs/CONTENT_FEATURES.md](docs/CONTENT_FEATURES.md) |
| Demo checklist | [docs/DEMO_INSTRUCTIONS.md](docs/DEMO_INSTRUCTIONS.md) |
| Changelog | [CHANGELOG.md](CHANGELOG.md) |

## Agent Workflow (summary)

1. Read this file → relevant [exec plan](docs/exec-plans/active/) → design doc
2. Implement smallest vertical slice
3. Run `scripts/verify.sh` or `scripts/verify.ps1`
4. Self-review diff against acceptance criteria
5. Update exec plan progress and [CHANGELOG.md](CHANGELOG.md)
6. Loop until verification passes — do not ask humans to "try harder"

Full loop: [docs/agent-workflows.md](docs/agent-workflows.md)

## Key Conventions

- Parse `visual-config.json` through `VisualConfig.LoadFromFile()` — never raw `JObject` in Views
- Resolve content paths only through `ContentLoader`
- Coordinate math lives in `Utilities/CoordinateMapper`
- Use `ILogger` / `FileLogger` in Services; avoid `Console.WriteLine` in Services/Views
- Keep `.cs` files under 800 lines; split when larger
- Scripts for test runners go in `scripts/`; test projects in `Tests/`

## When You Finish

- [ ] `scripts/verify` passes
- [ ] Exec plan updated (if applicable)
- [ ] `CHANGELOG.md` entry under `[Unreleased]` or new version
