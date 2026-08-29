# Running the map unattended

What the application already does when something goes wrong, and what would still have to be set up
before it could be left running in a gallery with nobody watching it.

Nothing here is required for a staffed installation, where someone can restart the app if it closes.
It is written down so the gap is a decision rather than a surprise.

## What the application does today

**Failures are logged instead of silent.** Logs live in
`%APPDATA%\InteractiveWorldMap\logs` — see [RELIABILITY.md](RELIABILITY.md). An exception that
escapes a UI event handler no longer closes the window; it is logged to `app.log` and the app
keeps running, which may leave the display in an odd state but keeps the map on screen. An
exception that ends the process is written straight to disk as `app.crash.log` first, so
the reason survives a shutdown that abandons the ordinary log writer. A background task
that fails without anyone waiting on it is logged to `app.log` and does not end the process.

**A failed zoom lands rather than freezes.** If a frame of a zoom animation throws, the animation
stops and the map jumps to where it was heading, so navigation stays usable.

**A failed action does not block later ones.** Zoom, Back and Escape reset their state after an
error, so one bad interaction cannot wedge navigation for the rest of the session.

**Logs have ceilings.** `app.log` rotates at 10 MB and keeps three older copies. The separate
`app.crash.log` rotates at 1 MB and keeps one older copy, so a repeated failed automatic start does
not fill the disk with the same startup record.

**Caches are bounded in practice.** The animation-frame and zoomed-region caches are keyed by the
zoom targets, and the app has no free pan or zoom — so with a fixed set of locations and a fixed
window size the cache fills once and stops growing. Editing locations or changing the display
resolution leaves orphaned entries behind; nothing deletes them. Every cache write failure is caught
and logged, so a full disk degrades to slower zooms rather than a crash.

## What an unattended install needs

Roughly in the order that matters. The first — the launcher and its Startup shortcut — needs no
administrator rights, no particular edition of Windows and installs nothing, which is most of the
benefit. The rest are worth knowing about and mostly worth skipping; automatic logon in particular
does need administrator rights, since it writes under `HKEY_LOCAL_MACHINE`.

**Automatic restart — covered.** `Tools\Run-Unattended.bat` starts the map and restarts it when it
exits, so a crash or an accidental quit recovers by itself. It waits five seconds between attempts
and keeps trying until someone closes its window. A shortcut to it in the Startup folder
(`shell:startup`) brings the map up after a reboot. The batch file stays in the package's `Tools`
folder; put a shortcut there, not a copy of the batch file, into Startup. It needs no administrator
rights and installs nothing; removing the shortcut undoes automatic launch. It cannot detect a hang
— a frozen app has not exited — so freezes still need a person, and detecting them would mean
health polling rather than a launcher. Setup steps are in the packaged guide.

The launcher starts the map with `--unattended`, which suppresses the dialog a fatal startup
error would otherwise show. A dialog waits for a click, and the launcher waits for the process
to exit, so the two together would leave the machine sitting on an error box instead of trying
again. The failure is written to `app.crash.log` either way.

**Automatic logon and a locked-down desktop.** A gallery machine that reboots overnight comes back to
a login screen, and the Startup shortcut only fires once somebody logs in. Auto-logon means storing
the account password where Windows can read it back, which is a poor trade for a machine in a public
room. Active Hours narrows when Windows may restart for an update but does not prevent it: the
window is at most 18 hours, and restarts are scheduled outside it. Pausing updates (Settings,
Windows Update) is blunter, but it lapses after about five weeks and has to be set again
before it does, or updates resume and a reboot can follow.

Windows' own kiosk modes are a larger setup than the launcher, and on Pro a weaker one than
they sound. Shell Launcher — the one meant for a single desktop application — needs
Enterprise or Education. Assigned Access does run on Pro, but auto-starting a desktop
application there means its multi-app restricted-user configuration: an XML file applied
with PowerShell, not anything in the Settings app, which offers only Store apps and Edge.
On Pro it also cannot block `Ctrl`+`Alt`+`Del` or `Alt`+`F4`, since the keyboard filter that
would do it is Enterprise and Education only — so a visitor at the keyboard can still leave the
map. Worth the effort only if the machine turns out to be Enterprise or Education.

**Power and display settings.** Sleep, screen blanking and the screen saver all have to be disabled,
or the map is a black rectangle by the second morning. Settings-app clicks, listed in the packaged
guide alongside the launcher steps.

**Developer tools turned off.** `visual-config.default.json` ships with `EnableDeveloperTools: true`,
which leaves F12 tuning, Edit Layout and debug overlays reachable by anyone who touches the machine.
Turn it off with `Tools\Configure-InteractiveWorldMap.bat` before an unattended run — this is the one
item on this list that is a real risk with a visitor at the keyboard rather than an inconvenience.

**Single-instance enforcement.** Nothing stops two copies running at once and fighting over the same
log file and caches.

**Startup failures, when the map is started by hand.** Started from the launcher the failure is
logged and the process exits, so the loop tries again. Started by double-clicking the map itself
there is still a message box waiting for a click — which is what you want in front of a person,
and is only a problem if somebody sets the map to start directly rather than through the launcher.

**Recovery from a wedged display.** The dispatcher handler keeps the app alive after an error, but it
does not put the view back to a known-good state — no "return to the full map after N minutes idle".
An idle reset would also return the map to a sensible starting point between visitors, which is worth
having for its own sake.

## Related

- [RELIABILITY.md](RELIABILITY.md) — where the log lives and how to read it
- [../release/PORTABLE_WINDOWS_RELEASE.md](../release/PORTABLE_WINDOWS_RELEASE.md) — the recipient
  guide, including the developer-tools switch
