# Reliability — Logging, Startup, and Error Handling

How agents inspect and verify application behavior without manual QA.

## Logging

### Location

```
%APPDATA%\InteractiveWorldMap\logs\app.log
```

On Windows PowerShell:

```powershell
.\scripts\query_logs.ps1 -Last 50
.\scripts\query_logs.ps1 -Filter "ERROR"
.\scripts\query_logs.ps1 -Filter "zoom" -Last 100 -Json
```

### Log Levels

| Level | Prefix | When used |
|-------|--------|-----------|
| ERROR | `[ERROR]` | Failures, missing files, exceptions |
| WARNING | `[WARNING]` | Degraded behavior, missing optional content |
| INFO | `[INFO]` | Normal operations, load counts, validation |

### Common Patterns

| Pattern | Meaning |
|---------|---------|
| `=== APPLICATION STARTUP ===` | App boot began |
| `=== Starting Environment Validation ===` | StartupValidator running |
| `Content folder not found` | `Images&Content` missing from output dir |
| `World map image not found` | Map JPEG missing or wrong filename |
| `Successfully loaded N locations` | Coordinate load succeeded |
| `Clustering complete` | Marker clustering finished |
| `FATAL ERROR during application startup` | Unrecoverable boot failure |

### Debug Logging

Enable in `visual-config.json`:

```json
{
  "Debug": {
    "LogRadialExtensionCalculation": true
  }
}
```

See [VISUAL_CONFIG.md](VISUAL_CONFIG.md) for all debug flags.

### Agent Guidance

- Use `scripts/query_logs.ps1` instead of asking humans to paste logs
- On macOS, logs are unavailable (app is Windows-only); use unit tests and `validate_startup` on Windows CI
- Prefer `ILogger` over `Console.WriteLine` in Services and Views

## Startup Validation

### Headless Check (no UI)

```powershell
.\scripts\validate_startup.ps1
```

Validates:

1. Project builds
2. `Images&Content` folder exists in output
3. `visual-config.json` deserializes to `VisualConfig`
4. `StartupValidator` reports environment status

Exit codes: `0` = pass, `1` = validation errors, `2` = build failure.

### StartupValidator

- Class: `Services/StartupValidator.cs`
- Checks content folder, map image (`ContentFileNames.WorldMapFileName`), `locations.json` format, Excel file presence
- Returns `ValidationResult` with `Errors` and `Warnings` lists

### ContentLoader.ValidateContentFolder()

Lighter check used at runtime: content folder + canonical map file from `ContentFileNames.WorldMapFileName`.

**Note:** Map filenames differ between StartupValidator and ContentLoader — known inconsistency tracked in [exec-plans/tech-debt-tracker.md](exec-plans/tech-debt-tracker.md).

## Error Handling

| Scenario | Behavior |
|----------|----------|
| Missing content folder | StartupValidator error; app may not load map |
| Missing location content | Warning log; empty popup or skip |
| Invalid JSON in locations.json | StartupValidator warning/error |
| Corrupt image | ContentLoader logs error; returns null/empty |
| Unhandled startup exception | MessageBox + `Shutdown(1)` in App.xaml.cs |

## Verification Checklist

Before claiming a reliability fix is done:

- [ ] `dotnet test` passes (including `StartupValidatorTests`, `ContentLoaderTests`)
- [ ] `scripts/validate_startup.ps1` exits 0 on Windows
- [ ] Relevant log patterns documented above if new paths added
