@echo off
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Configure-InteractiveWorldMap.ps1" %*
exit /b %ERRORLEVEL%
