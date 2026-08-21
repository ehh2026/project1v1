@echo off
setlocal enabledelayedexpansion

REM Shows which config file controls what, where each one is on this machine, and offers to turn
REM the developer tools on or off.
REM
REM Same reason toggle-dev-tools.bat exists: cmd.exe cannot execute a .ps1, so typing
REM "configure.ps1" in a cmd window or double-clicking it in Explorer makes Windows ask "How do
REM you want to open this file?" and nothing runs. This is the more discovery-oriented of the two
REM scripts, so it is the one people are likelier to double-click.
REM
REM Usage (arguments are passed straight through to configure.ps1):
REM   configure.bat              print the config guide, then offer to toggle developer tools
REM   configure.bat -NoPrompt    print the guide and exit, no questions asked

set "ARGS=%*"

REM When Explorer runs a .bat, the console closes the moment it finishes -- which would make the
REM output flash past unread, and this script exists to be read. %cmdcmdline% holds the command
REM line that started this cmd.exe: Explorer's contains the script name, an already-open console's
REM does not. (A deliberate "cmd /c configure.bat" from a script also matches; pass -NoPrompt
REM there.)
set "CMDLINE=%cmdcmdline%"
set "DOUBLECLICKED="
if not "!CMDLINE:%~nx0=!"=="!CMDLINE!" set "DOUBLECLICKED=1"

powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0configure.ps1" %*
set "EXITCODE=%ERRORLEVEL%"

REM Only when no arguments were passed: a double-click cannot supply any, so any argument at
REM all (-NoPrompt included) means a caller that must not be left waiting on a prompt.
if defined DOUBLECLICKED if not defined ARGS (
    echo.
    pause
)

exit /b %EXITCODE%
