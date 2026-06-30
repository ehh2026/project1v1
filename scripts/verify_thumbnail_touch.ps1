$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent $PSScriptRoot
Set-Location $Root

Write-Host "=== Thumbnail Synthetic Touch Smoke Check ==="
dotnet run --project Tools/ThumbnailTouchSmoke/ThumbnailTouchSmoke.csproj --configuration Release
if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}

Write-Host "=== Synthetic touch behavior PASSED ==="
