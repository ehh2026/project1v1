# Interactive World Map portable Windows release

This download is a self-contained `win-x64` build. Extract the entire zip to a writable folder (for example, `C:\InteractiveWorldMap`) and run `InteractiveWorldMap.exe`. Do not run it from inside the zip or install it under `C:\Program Files`: the application creates `visual-config.json` next to the executable.

No .NET SDK or .NET Desktop Runtime installation is required. The first public portable release is unsigned, so Windows SmartScreen may ask for confirmation; obtain the download only from the project's GitHub Releases page.

## Configuration and developer tools

`Tools\Configure-InteractiveWorldMap.bat` shows the resolved package paths and the current developer-tools setting. It also supports explicit changes:

```bat
Tools\Configure-InteractiveWorldMap.bat -DeveloperTools on
Tools\Configure-InteractiveWorldMap.bat -DeveloperTools off
Tools\Configure-InteractiveWorldMap.bat -DeveloperTools toggle
```

The helper seeds a missing runtime config. If it reports malformed JSON, re-run it with `-ResetMalformedConfig`; the broken file is renamed alongside the replacement so it can be recovered. Restart the app after configuration changes.

## Supplying Production content

The download includes app-owned `Images&Content\Assets` and a `Demo-Content` fallback. It deliberately does not include Production data.

To use your own data, create or replace `Images&Content\Production-Content`. It must contain either `locations.json` or `Coordinates for map.xlsx` plus the location folders and content described in [Content sets](https://github.com/ehh2026/project1v1/blob/main/docs/guides/CONTENT_SETS.md) and [Content features](https://github.com/ehh2026/project1v1/blob/main/docs/guides/CONTENT_FEATURES.md). Restart the app after changing content. A valid Production set automatically wins over Demo.

Keep `Assets` unchanged. To return to the bundled demo, rename `Production-Content` to `Production-Content.disabled` and restart.

User-specific manual layouts and caches are stored under `%AppData%\InteractiveWorldMap`, namespaced by the active content set.
