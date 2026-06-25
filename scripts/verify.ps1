# verify.ps1 — Unified agent verification (Windows)
# Usage: .\scripts\verify.ps1
# Exit: 0 pass, non-zero on failure with remediation hints

$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent $PSScriptRoot
Set-Location $Root

function Invoke-HarnessPython {
    param([string]$RelativeScript)
    $script = Join-Path $Root $RelativeScript
    $hasPython = $null -ne (Get-Command python -ErrorAction SilentlyContinue)
    $hasPyLauncher = $null -ne (Get-Command py -ErrorAction SilentlyContinue)

    if ($hasPyLauncher) {
        & py -3 $script
        if ($LASTEXITCODE -eq 0) { return }
    }

    if ($hasPython) {
        & python $script
        if ($LASTEXITCODE -eq 0) { return }
    }

    if (-not $hasPython -and -not $hasPyLauncher) {
        Write-Error "Python 3 not found. REMEDIATION: Install Python 3 or use Windows py launcher (py -3)."
        exit 2
    }

    exit $LASTEXITCODE
}

Write-Host "=== Interactive World Map - Harness Verification ==="

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    Write-Error "dotnet SDK not found. REMEDIATION: Install .NET 6 SDK from https://dotnet.microsoft.com/download"
    exit 2
}

Write-Host "[1/8] dotnet restore"
dotnet restore InteractiveWorldMap.sln

Write-Host "[2/8] NuGet vulnerability check"
Invoke-HarnessPython "scripts/verify_nuget_vulnerabilities.py"

Write-Host "[3/8] dotnet build"
dotnet build InteractiveWorldMap.sln --configuration Release --no-restore

Write-Host "[4/8] dotnet test"
dotnet test Tests/InteractiveWorldMap.Tests.csproj --configuration Release --no-build --verbosity minimal

Write-Host "[5/8] manual layout seed verification"
& "$PSScriptRoot\verify_manual_layout_seeds.ps1"
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host "[6/8] doc link check"
Invoke-HarnessPython "scripts/verify_doc_links.py"

Write-Host "[7/8] taste checks"
Invoke-HarnessPython "scripts/verify_taste.py"

Write-Host "[8/8] headless startup validation"
& "$PSScriptRoot\validate_startup.ps1"
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host "=== Verification PASSED ==="
