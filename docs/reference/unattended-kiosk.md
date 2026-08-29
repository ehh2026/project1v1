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
exception on a background thread still ends the process, but it is first written straight to
disk as `app.crash.log`, so the reason survives a shutdown that abandons the ordinary log
writer.

**A failed zoom lands rather than freezes.** If a frame of a zoom animation throws, the animation
stops and the map jumps to where it was heading, so navigation stays usable.

**A failed action does not block later ones.** Zoom, Back and Escape reset their state after an
error, so one bad interaction cannot wedge navigation for the rest of the session.

**The log has a ceiling.** It rotates at 10 MB and keeps three older copies, so a machine left on for
months cannot fill its disk with log text.

**Caches are bounded in practice.** The animation-frame and zoomed-region caches are keyed by the
zoom targets, and the app has no free pan or zoom — so with a fixed set of locations and a fixed
window size the cache fills once and stops growing. Editing locations or changing the display
resolution leaves orphaned entries behind; nothing deletes them. Every cache write failure is caught
and logged, so a full disk degrades to slower zooms rather than a crash.

## What is missing for a genuinely unattended install

Roughly in the order that matters.

**Automatic restart.** Nothing brings the app back if the process ends. The usual approach is a
Scheduled Task set to run at logon and to restart on failure, or Windows' built-in Assigned Access
kiosk mode. Neither is a code change.

**Automatic logon and a locked-down desktop.** A gallery machine that reboots overnight comes back to
a login screen. Assigned Access, or auto-logon plus a shell replacement, covers this.

**Power and display settings.** Sleep, screen blanking and the screen saver all have to be disabled,
or the map is a black rectangle by the second morning.

**Developer tools turned off.** `visual-config.default.json` ships with `EnableDeveloperTools: true`,
which leaves F12 tuning, Edit Layout and debug overlays reachable by anyone who touches the machine.
Turn it off with `Tools\Configure-InteractiveWorldMap.bat` before an unattended run — this is the one
item on this list that is a real risk with a visitor at the keyboard rather than an inconvenience.

**Single-instance enforcement.** Nothing stops two copies running at once and fighting over the same
log file and caches.

**Startup failures need a visitor-safe path.** A failure during startup shows a message box, which
waits forever for a click that nobody is there to give. Unattended, it should log and exit so the
restart mechanism can try again.

**Recovery from a wedged display.** The dispatcher handler keeps the app alive after an error, but it
does not put the view back to a known-good state — no "return to the full map after N minutes idle".
An idle reset would also return the map to a sensible starting point between visitors, which is worth
having for its own sake.

## Related

- [RELIABILITY.md](RELIABILITY.md) — where the log lives and how to read it
- [../release/PORTABLE_WINDOWS_RELEASE.md](../release/PORTABLE_WINDOWS_RELEASE.md) — the recipient
  guide, including the developer-tools switch
