# InteractiveWorldMap — Agent Quick Start

Windows desktop app (WPF / .NET 6 / C#) — full-screen interactive world map with clickable location markers and content popups.

## Verify before merging

```
.\scripts\verify.ps1        # Windows: build + test + taste + doc links + headless startup
./scripts/verify.sh         # macOS/Linux: build + test only (not sufficient alone before merge)
```

## Build / test / run

```
dotnet build InteractiveWorldMap.sln
dotnet test Tests/InteractiveWorldMap.Tests.csproj
dotnet run --project InteractiveWorldMap.csproj   # Windows UI only
.\run-demo.bat                                    # Windows: build + launch
```

## Architecture — do not violate (enforced by LayerDependencyTests.cs)

| Layer | May reference |
|-------|---------------|
| `Models/` | nothing upstream |
| `Utilities/`, `Services/` | Models only (Services may use Utilities) |
| `Views/` | Models only |
| `MainWindow` / `App` | orchestrates all layers |

## Key conventions

- Config: `VisualConfig.LoadFromFile()` only — never raw `JObject` in Views
- Content paths: `IContentLoader` / `ContentLoader` only — never raw string concat
- Coordinate math: `Utilities/CoordinateMapper` only
- Logging: `ILogger` injected — no `Console.WriteLine` in Services/Views
- File size: keep `.cs` files under 800 lines; split when larger

## Full harness guide

- [AGENTS.md](AGENTS.md) — workflow, exec plans, completion checklist, terminology
- [docs/index.md](docs/index.md) — full documentation catalog
- [docs/exec-plans/active/](docs/exec-plans/active/) — current in-progress work
