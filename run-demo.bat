@echo off
echo Building Interactive World Map...
dotnet build InteractiveWorldMap.csproj

if %ERRORLEVEL% EQU 0 (
    echo.
    echo Build successful! Starting application...
    echo.
    start "" "bin\Debug\net6.0-windows\InteractiveWorldMap.exe"
) else (
    echo.
    echo Build failed! Please check the errors above.
    pause
)
