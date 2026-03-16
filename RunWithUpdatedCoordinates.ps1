#!/usr/bin/env pwsh
# Script to run the application with updated coordinates from Excel

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Interactive World Map - Coordinate Update" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Check if Excel file exists
if (Test-Path "Coordinates for map.xlsx") {
    Write-Host "[OK] Excel file found: Coordinates for map.xlsx" -ForegroundColor Green
} else {
    Write-Host "[ERROR] Excel file not found!" -ForegroundColor Red
    Write-Host "Please ensure 'Coordinates for map.xlsx' exists in the project directory." -ForegroundColor Yellow
    exit 1
}

Write-Host ""
Write-Host "Building application..." -ForegroundColor Yellow
dotnet build --verbosity quiet

if ($LASTEXITCODE -ne 0) {
    Write-Host "[ERROR] Build failed!" -ForegroundColor Red
    exit 1
}

Write-Host "[OK] Build successful" -ForegroundColor Green
Write-Host ""
Write-Host "Starting application..." -ForegroundColor Yellow
Write-Host "The application will automatically:" -ForegroundColor Cyan
Write-Host "  1. Read coordinates from Excel file" -ForegroundColor White
Write-Host "  2. Generate location clusters" -ForegroundColor White
Write-Host "  3. Display markers on the map" -ForegroundColor White
Write-Host ""
Write-Host "Check the log file for detailed coordinate loading information." -ForegroundColor Yellow
Write-Host ""

# Run the application
dotnet run
