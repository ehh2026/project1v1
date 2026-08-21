@echo off
REM Shows which config file controls what, where each one is on this machine, and offers to turn
REM the developer tools on or off.
REM
REM Same reason toggle-dev-tools.bat exists: cmd.exe cannot execute a .ps1, so typing
REM "configure.ps1" in a cmd window or double-clicking it in Explorer makes Windows ask "How do
REM you want to open this file?" and nothing runs. This is the more discovery-oriented of the two
REM scripts, so it is the one people are likelier to double-click.
REM
REM Keeping the window open after a double-click is handled inside configure.ps1, which can see
REM who launched it. A batch file only sees a command line, and a double-click's is nearly
REM identical to the one PowerShell produces when you run this wrapper from a prompt.
REM
REM Usage (arguments are passed straight through to configure.ps1):
REM   configure.bat              print the config guide, then offer to toggle developer tools
REM   configure.bat -NoPrompt    print the guide and exit, no questions asked

powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0configure.ps1" %*

exit /b %ERRORLEVEL%
