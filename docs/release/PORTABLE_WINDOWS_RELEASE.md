# Interactive World Map portable Windows release

This download is a self-contained `win-x64` build. Extract the entire zip to a writable folder (for example, `C:\InteractiveWorldMap`) and run `InteractiveWorldMap.exe`. Do not run it from inside the zip or install it under `C:\Program Files`: the application creates `visual-config.json` next to the executable.

No .NET SDK or .NET Desktop Runtime installation is required. The first public portable release is unsigned, so Windows SmartScreen may ask for confirmation; obtain the download only from the project's GitHub Releases page.

## Configuration and developer tools

Double-click `Tools\Configure-InteractiveWorldMap.bat`. It shows the resolved package paths and the
current developer-tools setting, then offers a menu to turn developer tools on, off, or toggle them.

Run the `.bat` rather than the `.ps1`: double-clicking a `.ps1` in Explorer either opens it in an editor
or is blocked by the execution policy, which is why it appears to flash and vanish. The `.bat` launches
PowerShell with the right switches.

The same changes can be made without the menu. Add `-NoPrompt` when calling from a script, so it neither asks nor waits:

```bat
Tools\Configure-InteractiveWorldMap.bat -DeveloperTools on -NoPrompt
Tools\Configure-InteractiveWorldMap.bat -DeveloperTools off -NoPrompt
Tools\Configure-InteractiveWorldMap.bat -DeveloperTools toggle -NoPrompt
```

The helper seeds a missing runtime config. If it reports malformed JSON, re-run it with `-ResetMalformedConfig`; the broken file is renamed alongside the replacement so it can be recovered. Restart the app after configuration changes.

## Supplying Production content

The download includes app-owned `Images&Content\Assets` and a `Demo-Content` fallback. It deliberately does not include Production data.

To use your own data, create or replace `Images&Content\Production-Content`. It must contain either `locations.json` or `Coordinates for map.xlsx` plus the location folders and content described in [Content sets](https://github.com/ehh2026/project1v1/blob/main/docs/guides/CONTENT_SETS.md) and [Content features](https://github.com/ehh2026/project1v1/blob/main/docs/guides/CONTENT_FEATURES.md). Restart the app after changing content. A valid Production set automatically wins over Demo.

Keep `Assets` unchanged. To return to the bundled demo, rename `Production-Content` to `Production-Content.disabled` and restart.

User-specific manual layouts and caches are stored under `%AppData%\InteractiveWorldMap`, namespaced by the active content set.

## If something looks wrong

IF-SOMETHING-LOOKS-WRONG.md, next to this file, is a one-page card for whoever looks after the machine day to day: how to restart it, which oddities are not faults, and how to find the log file to send on. It assumes no technical knowledge.
