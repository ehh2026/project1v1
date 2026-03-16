#!/usr/bin/env pwsh
# Script to view what coordinates will be loaded from Excel

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Coordinate Preview" -ForegroundColor Cyan  
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

$excelFile = "Coordinates for map.xlsx"

if (-not (Test-Path $excelFile)) {
    Write-Host "[ERROR] Excel file not found: $excelFile" -ForegroundColor Red
    exit 1
}

Write-Host "[OK] Found Excel file: $excelFile" -ForegroundColor Green
Write-Host ""
Write-Host "To see the coordinates that will be loaded:" -ForegroundColor Yellow
Write-Host "  1. Run the application: dotnet run" -ForegroundColor White
Write-Host "  2. Check the log file at:" -ForegroundColor White
Write-Host "     %AppData%\InteractiveWorldMap\logs\app.log" -ForegroundColor Cyan
Write-Host ""
Write-Host "The log will show:" -ForegroundColor Yellow
Write-Host "  - Each location parsed from Excel" -ForegroundColor White
Write-Host "  - Pixel coordinates (X, Y)" -ForegroundColor White
Write-Host "  - Total number of locations loaded" -ForegroundColor White
Write-Host "  - Clustering statistics" -ForegroundColor White
Write-Host ""

$logPath = Join-Path $env:APPDATA "InteractiveWorldMap\logs\app.log"
if (Test-Path $logPath) {
    Write-Host "Opening log file..." -ForegroundColor Green
    notepad $logPath
} else {
    Write-Host "Log file not found yet. Run the application first." -ForegroundColor Yellow
}
