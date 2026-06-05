# verify.ps1 — Unified agent verification (Windows)
# Usage: .\scripts\verify.ps1
# Exit: 0 pass, non-zero on failure with remediation hints

$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent $PSScriptRoot
Set-Location $Root

Write-Host "=== Interactive World Map — Harness Verification ==="

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    Write-Error "dotnet SDK not found. REMEDIATION: Install .NET 6 SDK from https://dotnet.microsoft.com/download"
    exit 2
}

Write-Host "[1/6] dotnet restore"
dotnet restore InteractiveWorldMap.sln

Write-Host "[2/6] dotnet build"
dotnet build InteractiveWorldMap.sln --configuration Release --no-restore

Write-Host "[3/6] dotnet test"
dotnet test Tests/InteractiveWorldMap.Tests.csproj --configuration Release --no-build --verbosity minimal

Write-Host "[4/6] doc link check"
python scripts/verify_doc_links.py
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host "[5/6] taste checks"
python scripts/verify_taste.py
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host "[6/6] headless startup validation"
& "$PSScriptRoot\validate_startup.ps1"
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host "=== Verification PASSED ==="
