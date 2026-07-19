# validate_startup.ps1 — Headless startup validation (no UI)
# Usage: .\scripts\validate_startup.ps1
# Exit: 0 pass, 1 validation errors, 2 build failure

$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent $PSScriptRoot
Set-Location $Root

Write-Host "=== Headless Startup Validation ==="

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    Write-Error "dotnet SDK not found."
    exit 2
}

Write-Host "[1/4] Build"
dotnet build InteractiveWorldMap.sln --configuration Release --verbosity quiet
if ($LASTEXITCODE -ne 0) {
    Write-Error "Build failed. REMEDIATION: Run dotnet build and fix compile errors."
    exit 2
}

Write-Host "[2/4] Content folder"
$ContentPath = Join-Path $Root "Images&Content"
if (-not (Test-Path $ContentPath)) {
    Write-Error "Images&Content not found. REMEDIATION: Ensure content folder exists at repo root."
    exit 1
}

Write-Host "[3/4] visual-config.default.json"
$ConfigPath = Join-Path $Root "visual-config.default.json"
if (-not (Test-Path $ConfigPath)) {
    Write-Error "visual-config.default.json missing. REMEDIATION: Add default config at repo root."
    exit 1
}
try {
    $null = Get-Content $ConfigPath -Raw | ConvertFrom-Json
} catch {
    Write-Error "visual-config.default.json invalid JSON. REMEDIATION: Fix JSON syntax."
    exit 1
}

Write-Host "[4/4] Harness tests"
dotnet test Tests/InteractiveWorldMap.Tests.csproj `
    --configuration Release `
    --no-build `
    --filter "FullyQualifiedName~StartupValidationHarness" `
    --verbosity minimal
if ($LASTEXITCODE -ne 0) {
    Write-Error "Startup harness tests failed. REMEDIATION: See test output above."
    exit 1
}

Write-Host "=== Startup Validation PASSED ==="
exit 0
