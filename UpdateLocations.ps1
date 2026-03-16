# PowerShell script to build and run the location updater
Write-Host "Building project..." -ForegroundColor Cyan
dotnet build

if ($LASTEXITCODE -eq 0) {
    Write-Host "`nProject built successfully!" -ForegroundColor Green
    Write-Host "`nThe application will automatically read from 'Coordinates for map.xlsx' when you run it." -ForegroundColor Yellow
    Write-Host "The Excel file takes priority over locations.json." -ForegroundColor Yellow
    Write-Host "`nTo run the application and see the new markers:" -ForegroundColor Cyan
    Write-Host "  dotnet run" -ForegroundColor White
} else {
    Write-Host "`nBuild failed!" -ForegroundColor Red
}
