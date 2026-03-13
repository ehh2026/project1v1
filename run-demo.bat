@echo off
echo Building Interactive World Map...
dotnet build InteractiveWorldMap.csproj

if %ERRORLEVEL% EQU 0 (
    echo.
    echo Build successful! Starting application...
    echo.
    echo ========================================
    echo Application Output:
    echo ========================================
    echo.
    REM Run the application and wait for it to complete
    "bin\Debug\net6.0-windows\InteractiveWorldMap.exe"
    echo.
    echo ========================================
    echo Application exited with code: %ERRORLEVEL%
    echo ========================================
    pause
) else (
    echo.
    echo Build failed! Please check the errors above.
    pause
)
