# verify.ps1 — Unified agent verification (Windows)
# Usage: .\scripts\verify.ps1
# Exit: 0 pass, non-zero on failure with remediation hints

$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent $PSScriptRoot
Set-Location $Root

function Invoke-HarnessPython {
    param(
        [string]$RelativeScript,
        [string]$ScriptArgs = ""
    )
    $script = Join-Path $Root $RelativeScript
    $argList = if ($ScriptArgs) { $ScriptArgs -split ' ' } else { @() }
    $hasPython = $null -ne (Get-Command python -ErrorAction SilentlyContinue)
    $hasPyLauncher = $null -ne (Get-Command py -ErrorAction SilentlyContinue)

    if ($hasPyLauncher) {
        & py -3 $script @argList
        if ($LASTEXITCODE -eq 0) { return }
    }

    if ($hasPython) {
        & python $script @argList
        if ($LASTEXITCODE -eq 0) { return }
    }

    if (-not $hasPython -and -not $hasPyLauncher) {
        Write-Error "Python 3 not found. REMEDIATION: Install Python 3 or use Windows py launcher (py -3)."
        exit 2
    }

    exit $LASTEXITCODE
}

function Invoke-HarnessPythonModule {
    param(
        [string]$ModuleName,
        [string]$ScriptArgs = ""
    )
    $argList = if ($ScriptArgs) { $ScriptArgs -split ' ' } else { @() }
    $hasPython = $null -ne (Get-Command python -ErrorAction SilentlyContinue)
    $hasPyLauncher = $null -ne (Get-Command py -ErrorAction SilentlyContinue)

    if ($hasPyLauncher) {
        & py -3 -m $ModuleName @argList
        if ($LASTEXITCODE -eq 0) { return }
    }

    if ($hasPython) {
        & python -m $ModuleName @argList
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

Write-Host "[1/11] dotnet restore"
dotnet restore InteractiveWorldMap.sln
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host "[2/11] NuGet vulnerability check"
Invoke-HarnessPython "scripts/verify_nuget_vulnerabilities.py"

Write-Host "[3/11] dotnet build"
dotnet build InteractiveWorldMap.sln --configuration Release --no-restore
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host "[4/11] dotnet test"
dotnet test Tests/InteractiveWorldMap.Tests.csproj --configuration Release --no-build --verbosity minimal --settings .runsettings --filter "Category!=Performance" --collect:"XPlat Code Coverage" --results-directory TestResults\verify-coverage
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host "[5/11] manual layout seed verification"
& "$PSScriptRoot\verify_manual_layout_seeds.ps1"
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host "[6/11] doc link check"
Invoke-HarnessPython "scripts/verify_doc_links.py"

Write-Host "[7/11] taste checks"
Invoke-HarnessPython "scripts/verify_taste.py"

Write-Host "[8/11] headless startup validation"
& "$PSScriptRoot\validate_startup.ps1"
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host "[9/11] code formatting check"
dotnet format InteractiveWorldMap.sln --verify-no-changes
if ($LASTEXITCODE -ne 0) { Write-Error "Formatting verification failed."; exit 1 }

Write-Host "[10/11] coverage threshold gate"
Invoke-HarnessPython "scripts/summarize_coverage.py" "--results-directory TestResults\verify-coverage --min-line-coverage 45 --min-branch-coverage 40"
if ($LASTEXITCODE -ne 0) { Write-Error "Coverage gates failed."; exit 1 }

Write-Host "[11/11] Lizard complexity gate"
Invoke-HarnessPythonModule "lizard" "-C 20 -x *Tests* -x *Tools* -x *bin* -x *obj* -x *scripts* -x *TestResults* ."
if ($LASTEXITCODE -ne 0) { Write-Error "Lizard complexity gate failed."; exit 1 }

Write-Host "=== Verification PASSED ==="
