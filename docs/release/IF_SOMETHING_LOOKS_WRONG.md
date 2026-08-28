# If something looks wrong

A one-page card for whoever is looking after the map. Nothing here can damage anything — the worst
case is closing the app and opening it again.

## First, try this

**Press `Esc`.** Once returns from a zoomed-in view to the whole map. Again from the whole map closes
the application.

**If the screen is frozen or looks strange, close it and reopen it.** `Esc` from the full map, or
`Alt`+`F4`. Then start it the same way you did the first time. Nothing is lost — the map has nothing
to save.

**If it will not close,** hold `Ctrl`+`Shift`+`Esc` to open Task Manager, find *Interactive World
Map* in the list, and click **End task**. Then reopen it.

## Things that are not faults

**A zoom that jumps instead of gliding.** The application does this deliberately when a zoom cannot
be drawn smoothly. It has landed where it was going; carry on using it.

**A stamp or picture missing from a location.** That location has no image file, or the file has a
different name than expected. The map keeps working. Worth reporting, not worth restarting for.

**The window opening a little slowly the first time.** Large images are being prepared. Later zooms
to the same place are faster because the result is kept.

**A black screen after the gallery has been closed overnight.** That is usually Windows blanking the
display, not the application. Move the mouse or touch the screen first.

## If you need to report it

There is a file that records what the application was doing. Send it and it will usually be clear
what happened.

1. Hold the **Windows key** and press **R**.
2. Type this and press Enter:

   ```
   %APPDATA%\InteractiveWorldMap\logs
   ```

3. Attach `app.log` to your message. If there are files named `app.log.1`, `app.log.2` and so on,
   include the most recent one as well. If you see `app.crash.log`, always include it — it only
   exists if the application closed unexpectedly, and it says why.

Along with the file, it helps enormously to say:

- **What was on screen** — the whole map, a zoomed-in area, or a picture open?
- **What you had just done** — which location, and whether anything was clicked twice quickly.
- **Roughly when**, so the right part of the file can be found.

## Turning the settings menu on or off

The map ships with its developer tools switched **off**, which is what you want with visitors around:
it stops anyone rearranging the pins or opening tuning panels by accident.

If it ever needs turning back on, open the `Tools` folder and double-click
**Configure-InteractiveWorldMap.bat**. A small menu offers on, off, or toggle. Choose, then restart
the map. Use the `.bat` file, not the `.ps1` one — the `.ps1` will appear to flash and vanish.
