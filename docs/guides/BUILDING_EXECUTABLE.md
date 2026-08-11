# Building a Windows Executable

Information and general instructions for producing a distributable Windows executable from this repo — locally and via GitHub Actions. This is a reference, not an execution plan; see [docs/exec-plans/active/](../exec-plans/active/) when a concrete build-out is scheduled.

## What exists today

- `InteractiveWorldMap.csproj` is a WPF app: `OutputType=WinExe`, `TargetFramework=net6.0-windows`, `UseWPF=true`. Building already produces a launchable `InteractiveWorldMap.exe` in `bin\Debug|Release\net6.0-windows\`.
- Content (`Images&Content\**\*` and `visual-config.default.json`) is copied to the output directory on every build, so a plain build is already a runnable app folder.
- `run-demo.bat` builds Debug and starts the app from `bin\Debug\net6.0-windows\`.
- `.github/workflows/ci.yml` already builds and tests on `windows-latest` with the .NET 6 SDK, but never publishes an artifact.
- `global.json` pins the .NET 6 SDK; .NET 6 itself is **out of support** (EOL Nov 2024) — relevant for the self-contained vs framework-dependent decision below.

## Publishing vs building

`dotnet build` produces a dev/repro output. `dotnet publish` produces the distributable output (RID-specific, optimized, honoring publish settings) and is the right command for anything users will run.

Core command:

```powershell
dotnet publish InteractiveWorldMap.csproj -c Release [-r win-x64] [--self-contained true|false] [-o <folder>]
```

Key options (see [Microsoft publish docs](https://learn.microsoft.com/dotnet/core/deploying/publish-single-file) for details):

| Option | Effect |
|--------|--------|
| `-r win-x64` | Target a concrete runtime; required for self-contained and single-file output |
| `--self-contained true` | Bundles the .NET runtime — no install needed on the target machine; larger output (~60–100 MB) |
| `--self-contained false` | Framework-dependent — small output, but the machine must have the .NET 6 Desktop Runtime installed |
| `-p:PublishSingleFile=true` | Bundle managed assemblies into one `.exe` (works for WPF) |
| `-p:IncludeNativeLibrariesForSelfExtract=true` | Put native libraries in the single file too (recommended for WPF) |
| `-p:EnableCompressionInSingleFile=true` | Smaller single file at the cost of slower startup |
| `-p:PublishReadyToRun=true` | Pre-compile assemblies to native code for faster startup; increases publish size |
| `--no-self-contained` | Shorthand alias for `--self-contained false` |

Publish profiles (`.pubxml` files under `Properties/PublishProfiles/`) can encode the same flags for repeatable builds — `dotnet publish -p:PublishProfile=<name>` — so you don't have to retype the long command each time.

Recommended local command for a distributable build:

```powershell
dotnet publish InteractiveWorldMap.csproj -c Release -r win-x64 --self-contained true `
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
  -o bin\publish\win-x64
```

The publish folder contains the `.exe` plus the copied `Images&Content\` tree and `visual-config.default.json` (both are propagated by `CopyToOutputDirectory`). That whole folder is the distributable unit.

## Runtime configuration

On first launch the app seeds `visual-config.json` beside the executable (see [VISUAL_CONFIG.md](VISUAL_CONFIG.md)). This file can be edited with any text editor to enable/disable features without rebuilding:

| Setting | Effect |
|---------|--------|
| `EnableDeveloperTools` | Master gate for developer features: Edit Layout mode, tuning panel, debug overlays, debug logging. Default `false`. |
| `Debug.EnableTuningPanel` | Shows the F12 runtime tuning panel (requires `EnableDeveloperTools=true`). Allows live adjustment of visual parameters and saving them back to the config. |
| `ContentImages.ShowLoadingStatus` | Shows a "Loading content…" banner when opening content. Independent of developer tools — enable for guest-facing load feedback. Default `false`. |
| `Debug.WindowedMode` | Launch in a resizable window instead of borderless fullscreen kiosk mode. Default `false`. |
| `ManualLayoutEditor.Enabled` | Enables the drag-and-drop marker layout editor (requires `EnableDeveloperTools=true`). |

The tuning panel (F12 when enabled) provides a UI for adjusting cluster distances, marker sizes, pin appearance, shadow settings, and more — changes can be applied immediately and saved back to `visual-config.json`.

### Toggling developer tools

The repo includes `scripts\toggle-dev-tools.ps1` for flipping `EnableDeveloperTools` without hand-editing JSON. However, it only works for **local build outputs** (`bin\Debug\net6.0-windows\` and `bin\Release\net6.0-windows\`) — it does not currently scan publish output folders.

For **published/distributed builds**, either:
1. **Edit `visual-config.json` directly** — it sits next to the `.exe` in the publish folder. Set `"EnableDeveloperTools": true` (or `false`) and relaunch.
2. **Copy the script** — place `toggle-dev-tools.ps1` in the same folder as the published `.exe` (or adjust the hardcoded paths in the script to point at your publish output), then run it from there.

## Decision points (with trade-offs)

1. **Self-contained vs framework-dependent.** Self-contained trades size for zero prerequisites and immunity to .NET 6 EOL concerns (the runtime is bundled). Framework-dependent is a small download but requires the .NET 6 Desktop Runtime on every target machine — and since .NET 6 is out of support, those machines get no security fixes unless the app is later moved to a supported TFM (e.g. `net8.0-windows`).
2. **Single file vs folder.** A single `.exe` is the easiest thing to hand to someone, but the `Images&Content\` folder must still sit next to the exe. A folder/zip keeps content and exe together — `Images&Content` is already structured for copy-to-output, so a zip of the publish folder is the lowest-friction distribution today.
3. **Writable config next to the exe.** At first run the app seeds `visual-config.json` beside the executable. That is fine for a folder/zip anywhere writable, but breaks if someone installs under `C:\Program Files\...` (read-only). If an installer is added later, decide whether the app data should move to `%LocalAppData%`/`%ProgramData%` or the installer should place content in a writable location.
4. **Signing.** An unsigned exe triggers SmartScreen "Unknown publisher" warnings. Options: an OV/EV code-signing certificate, or [Azure Trusted Signing](https://learn.microsoft.com/azure/trusted-signing/) (Microsoft's managed signing service, works in CI). Signing is optional for internal/demo use.
5. **Installer (optional).** If a real installer is wanted, reasonable options for WPF: **Inno Setup** (scriptable, runs on CI), **MSIX** (Store/sideload model), or **Velopack** (successor to Squirrel.Windows, auto-update support). None are required for a usable executable.

## Building locally (one-off)

Before publishing, decide which content set should ship — `Demo-Content` or `Production-Content` (see [CONTENT_SETS.md](CONTENT_SETS.md)). Both live under `Images&Content\` and are copied to the output directory on publish; remove or exclude the set you don't want before running `dotnet publish`.

PowerShell one-liner:

```powershell
dotnet publish InteractiveWorldMap.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o bin\publish\win-x64
```

Sanity-check the result before handing it out:

1. `bin\publish\win-x64\Images&Content\` exists and contains the content set you want (see [CONTENT_SETS.md](CONTENT_SETS.md)).
2. Launch the exe on a clean machine (or a machine without the .NET 6 Desktop Runtime, if self-contained).
3. Confirm `visual-config.json` is seeded next to the exe after first launch.

## Building via GitHub Actions

Add the "publish" as a separate job (CI stays as is). Two natural triggers:

- **On tag push** (e.g. `v1.2.3`) → build and attach the exe to a GitHub Release.
- **On `workflow_dispatch`** with inputs → build on demand, upload artifact or create a draft release.

Sketch of a `publish.yml` job (adjust action major versions to whatever is current):

```yaml
name: Publish

on:
  push:
    tags: ["v*"]
  workflow_dispatch:
    inputs:
      self_contained:
        description: "Bundle the .NET runtime"
        type: boolean
        default: true

jobs:
  publish:
    runs-on: windows-latest
    steps:
      - uses: actions/checkout@v6

      - uses: actions/setup-dotnet@v5
        with:
          dotnet-version: "6.0.x"   # matches global.json

      - name: Publish
        shell: pwsh
        run: |
          dotnet publish InteractiveWorldMap.csproj -c Release -r win-x64 `
            --self-contained ${{ inputs.self_contained }} `
            -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
            -o publish-output

      - name: Compress
        run: Compress-Archive -Path publish-output\* -DestinationPath InteractiveWorldMap-win-x64.zip

      - name: Upload artifact
        uses: actions/upload-artifact@v4
        with:
          name: InteractiveWorldMap-win-x64
          path: InteractiveWorldMap-win-x64.zip

      - name: Create release / attach zip
        if: startsWith(github.ref, 'refs/tags/')
        uses: softprops/action-gh-release@v2   # or: gh release create "${{ github.ref_name }}" InteractiveWorldMap-win-x64.zip
        with:
          files: InteractiveWorldMap-win-x64.zip
```

Notes:

- WPF publish **must** run on `windows-latest` (or `windows-2022`); it will not publish meaningfully on Ubuntu runners.
- The existing CI already proves the restore/build loop works on `windows-latest`, so the publish job is low-risk to add.
- If an installer or signing is added later, those are extra steps inside this same job (choco install innosetup / Trusted Signing action).
- A zip of the publish folder (rather than a bare single file) keeps `Images&Content\` and `visual-config.default.json` with the exe, which the app requires.

## Open questions worth answering before implementing

- Who receives the exe: one machine or many? (drives self-contained vs framework-dependent)
- Is SmartScreen / unsigned-exe warning acceptable, or is signing in scope?
- Is a zip "good enough", or is an installer or MSIX package wanted?
- Should a publish job only build on a tag/release, or should every merge to `main` produce a fresh artifact?

## Related docs

- [SETUP_GUIDE.md](SETUP_GUIDE.md) — .NET SDK setup, `global.json` pin
- [CONTENT_SETS.md](CONTENT_SETS.md) — what ships inside `Images&Content`
- [.github/workflows/ci.yml](../../.github/workflows/ci.yml) — existing Windows build/test pipeline to extend
- [VISUAL_CONFIG.md](VISUAL_CONFIG.md) — how `visual-config.json` is seeded at first run