@echo off
setlocal

rem Starts the map and restarts it if it closes, so an unattended machine recovers from a crash or
rem from someone quitting the app. "start /wait" blocks until the app exits, so only ever one copy
rem runs at a time -- this loop cannot produce a second one.
rem
rem It cannot detect a hang: an app that is frozen but still running has not exited, so nothing
rem restarts it. That case still needs a person.
rem
rem To stop: close this window. To stop it starting automatically: delete the shortcut from the
rem Startup folder (Windows key + R, then: shell:startup).

set "APP=%~dp0..\InteractiveWorldMap.exe"

if not exist "%APP%" (
    echo Could not find InteractiveWorldMap.exe next to the Tools folder.
    echo Expected: "%APP%"
    echo.
    echo Run this from inside the extracted package, not from a copy of the Tools folder.
    pause
    exit /b 1
)

:run
echo [%date% %time%] Starting Interactive World Map...
start /wait "" "%APP%"

rem The pause matters: without it, an app that fails immediately would be restarted flat out, which
rem makes the machine unusable and floods the log. Five seconds is long enough to close this window.
echo [%date% %time%] The map closed. Restarting in 5 seconds -- close this window to stop.
timeout /t 5 /nobreak >nul
goto run
