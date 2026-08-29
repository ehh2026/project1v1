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

## Leaving it running unattended

`Tools\Run-Unattended.bat` starts the map and restarts it if it closes, so the machine recovers from a crash or from someone quitting the app. It waits for the app to exit before restarting, then waits five seconds before the next attempt; it keeps trying until its own window is closed. It cannot detect a freeze — an app that is hung has not exited — so that case still needs a person.

To have it start by itself:

1. Right-click `Tools\Run-Unattended.bat` and choose **Show more options** then **Create shortcut**.
2. Press `Windows`+`R`, type `shell:startup`, press Enter.
3. Move the shortcut into that folder.
4. Optional: right-click the shortcut, **Properties**, set **Run** to **Minimized**, so its window stays out of the way.

Also set, in Windows **Settings**:

- **System → Power** — screen and sleep both set to **Never**.
- **Personalization → Lock screen → Screen saver** — set to **(None)**.

To use the helper only for the current session, double-click `Tools\Run-Unattended.bat` in the
extracted package. Do not copy the batch file somewhere else: it finds the map EXE from its own
`Tools` folder. It starts automatically after login only when its **shortcut** is in Startup.

A blank screen the morning after is far more often the display sleeping than the map failing.

**To stop it starting by itself:** delete the shortcut from the Startup folder. Nothing was
installed, so there is nothing to uninstall. To stop the loop while it is running, close its
window. If the map does not appear after a few attempts, close the **Run-Unattended** window and
open `InteractiveWorldMap.exe` directly; its error message is the useful thing to report.

The power and screen-saver settings above are the exception — they are Windows settings, not
part of the map, and they stay as you set them until you change them back in **Settings**.
