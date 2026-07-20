# Agent Map — Interactive World Map

> **Humans steer. Agents execute.** When something fails, ask: what capability is missing, and how do we make it legible and enforceable?

## Project

Windows desktop app (WPF / .NET 6 / C#) displaying a full-screen interactive world map with clickable location markers and content popups.

## Non-Negotiable Finish Bookkeeping

Every agent must keep completion state current before handing work back:

- Remove completed `docs/TO_DO.md` bullets instead of leaving stale checked items.
- Narrow partially completed `docs/TO_DO.md` bullets to only the remaining scope.
- Move parked work to Deferred/Inactive with a short reason.
- Archive completed exec plans to `docs/exec-plans/completed/` and update active registries.
- Add or update the `[Unreleased]` `CHANGELOG.md` entry for user-visible or workflow-visible changes.

## Quick Commands

```bash
# Build and test (run before claiming work is done)
./scripts/verify.sh          # macOS / Linux (build + test + harness checks)
.\scripts\verify.ps1         # Windows (full verification)
.\scripts\verify_manual_layout_seeds.ps1 # Windows: seed generator/load sanity check

# Manual steps
dotnet build InteractiveWorldMap.sln
dotnet test Tests/InteractiveWorldMap.Tests.csproj
dotnet run --project InteractiveWorldMap.csproj   # Windows UI only
.\run-demo.bat                                    # Windows: build + launch
.\scripts\validate_startup.ps1                  # Headless startup check (Windows)
```

**Platform note:** WPF requires Windows for UI. macOS can build and run unit tests only.

**SDK:** `global.json` pins **.NET 6 SDK** for this repo (side-by-side with newer SDKs). If `dotnet test` fails locally, install .NET 6 SDK — see [docs/guides/SETUP_GUIDE.md](docs/guides/SETUP_GUIDE.md).

**Merge gate:** GitHub Actions on `windows-latest` runs build, test, NuGet vulnerability scan, doc links, taste checks, and headless startup validation; **Gitleaks** runs separately on Ubuntu. Local Windows gate: `.\scripts\verify.ps1`. macOS `verify.sh` may pass in harness-only mode — not sufficient alone before merge.

**Python (optional tooling):** Harness scripts use stdlib only. On Windows use `py -3 scripts\...`; `verify.ps1` prefers `py -3` before bare `python` because local pyenv shims may exist without a selected version. On macOS/Linux use `python3 scripts/...`. Pin-extraction scripts need **Pillow, numpy, scipy**; use the local venv at `scripts/venv/` (gitignored, not committed). Fresh setup: `py -3 -m venv scripts\venv` then `pip install -r scripts\requirements.txt`. See [docs/guides/SETUP_GUIDE.md](docs/guides/SETUP_GUIDE.md) and [scripts/README.md](scripts/README.md).

## Repository Layout

| Path | Purpose |
|------|---------|
| `Models/` | Data models, config types, event args — no upstream project refs |
| `Utilities/` | Pure helpers: coordinates, clustering, Excel reading |
| `Services/` | Content loading, logging, navigation, layout, validation |
| `Views/` | WPF UserControls and windows — Models only |
| `Tests/` | xUnit tests including architecture structural tests |
| `scripts/` | Verification, log query, Python tooling — see [scripts/README.md](scripts/README.md) |
| `Images&Content/` | `Assets/` (maps, pin parts), `Demo-Content/` / `Production-Content/` (Excel, `locations.json`, location folders), `Extras/` — see [CONTENT_SETS.md](docs/guides/CONTENT_SETS.md) |
| `visual-config.json` | Machine-readable UI/debug config (deserialize to `Models/VisualConfig`) |

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
| Quality grades | [docs/reference/QUALITY_SCORE.md](docs/reference/QUALITY_SCORE.md) |
| Logging & startup | [docs/reference/RELIABILITY.md](docs/reference/RELIABILITY.md) |
| Security | [docs/reference/SECURITY.md](docs/reference/SECURITY.md) |
| Agent workflow | [docs/agent-workflows.md](docs/agent-workflows.md) |
| Agent failure log | [docs/agent-failures.md](docs/agent-failures.md) |
| Golden principles | [docs/design-docs/golden-principles.md](docs/design-docs/golden-principles.md) |
| Human backlog | [docs/TO_DO.md](docs/TO_DO.md) (short bullets; detail in exec plans) |
| Active work | [docs/exec-plans/active/](docs/exec-plans/active/) — composite pins: [composite-pins-program.md](docs/exec-plans/active/composite-pins-program.md) |
| Tech debt | [docs/exec-plans/tech-debt-tracker.md](docs/exec-plans/tech-debt-tracker.md) |
| Doc maintenance rules | [docs/agent-workflows.md](docs/agent-workflows.md#documentation-maintenance) |
| Formal spec | [.kiro/specs/interactive-world-map/](.kiro/specs/interactive-world-map/) |
| Feature guides | [docs/guides/](docs/guides/) — `VISUAL_CONFIG`, `CONTENT_FEATURES`, `MANUAL_LAYOUT_EDITOR`, … |
| Demo checklist | [docs/guides/DEMO_INSTRUCTIONS.md](docs/guides/DEMO_INSTRUCTIONS.md) |
| Changelog | [CHANGELOG.md](CHANGELOG.md) |

## Agent Workflow (summary)

1. Read this file → relevant [exec plan](docs/exec-plans/active/) → design doc
2. Implement smallest vertical slice
3. Run `scripts/verify.sh` or `scripts/verify.ps1`
4. Self-review diff against acceptance criteria
5. Update exec plan progress, program dashboard if applicable, and [CHANGELOG.md](CHANGELOG.md) — follow [doc maintenance rules](docs/agent-workflows.md#documentation-maintenance)
6. Loop until verification passes — do not ask humans to "try harder"

Full loop: [docs/agent-workflows.md](docs/agent-workflows.md)

## Key Conventions

- Parse `visual-config.json` through `VisualConfig.LoadFromFile()` — never raw `JObject` in Views
- Resolve content paths only through `ContentLoader`
- Coordinate math lives in `Utilities/CoordinateMapper`
- Use `ILogger` / `FileLogger` in Services; avoid `Console.WriteLine` in Services/Views
- Keep `.cs` files under 800 lines; split when larger
- Exec plans that touch large files, composition roots, or shared workflows must include modularity/file-size impact: expected file growth, ownership boundaries, extraction points, and tests
- Scripts for test runners go in `scripts/`; test projects in `Tests/`

## Pin/Location Terminology

Use [docs/reference/GLOSSARY.md](docs/reference/GLOSSARY.md) terms consistently in docs, plans, tests, and code comments.

## When You Finish

- [ ] `scripts/verify` passes
- [ ] Exec plan updated (if applicable)
- [ ] Finished exec plan archived to `docs/exec-plans/completed/` and active registries updated, if the plan is complete
- [ ] `docs/TO_DO.md` updated: remove completed items, narrow partial items to remaining scope, or move parked items to Deferred/Inactive
- [ ] `CHANGELOG.md` entry under `[Unreleased]` or new version
