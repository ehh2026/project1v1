@echo off
REM Turns the app's developer tools (Edit Layout, tuning panel, debug overlays) on or off.
REM
REM This wrapper exists because cmd.exe cannot execute a .ps1 directly: running
REM scripts\toggle-dev-tools.ps1 from a cmd window, or double-clicking it in Explorer, makes
REM Windows show "How do you want to open this file?" and the script never runs. Sitting here
REM next to run-demo.bat, this is also where people actually look for it.
REM
REM Usage (all arguments are passed straight through to the PowerShell script):
REM   toggle-dev-tools.bat                 flip whatever the config currently has
REM   toggle-dev-tools.bat -State on       force on
REM   toggle-dev-tools.bat -State off      force off
REM   toggle-dev-tools.bat -State on -PublishDir D:\Gallery\App

powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\toggle-dev-tools.ps1" %*

REM Propagate the script's exit code so a failure (e.g. no built exe found) is visible to
REM whatever called this, rather than being swallowed by the wrapper.
exit /b %ERRORLEVEL%
