#requires -Version 5.1
<#
.SYNOPSIS
    Inspect or change the configuration of an extracted portable release.
#>
[CmdletBinding()]
param(
    [ValidateSet('report', 'on', 'off', 'toggle')]
    [string]$DeveloperTools = 'report',

    [switch]$ResetMalformedConfig,

    [switch]$NoPrompt,

    [switch]$NoPause
)

$ErrorActionPreference = 'Stop'
trap {
    Write-Host "Configuration helper failed: $($_.Exception.Message)" -ForegroundColor Red
    if (-not $NoPause -and [Environment]::UserInteractive) { Read-Host 'Press Enter to close' | Out-Null }
    exit 1
}

$appRoot = Split-Path -Parent $PSScriptRoot
$exePath = Join-Path $appRoot 'InteractiveWorldMap.exe'
$defaultConfig = Join-Path $appRoot 'visual-config.default.json'
$runtimeConfig = Join-Path $appRoot 'visual-config.json'
$contentRoot = Join-Path $appRoot 'Images&Content'
$production = Join-Path $contentRoot 'Production-Content'
$demo = Join-Path $contentRoot 'Demo-Content'

if (-not (Test-Path -LiteralPath $exePath -PathType Leaf)) { throw "Expected sibling executable not found: $exePath" }
if (-not (Test-Path -LiteralPath $defaultConfig -PathType Leaf)) { throw "Expected default config not found: $defaultConfig" }

function Test-ValidContent([string]$Path) {
    return (Test-Path -LiteralPath (Join-Path $Path 'locations.json') -PathType Leaf) -or
        (Test-Path -LiteralPath (Join-Path $Path 'Coordinates for map.xlsx') -PathType Leaf)
}

function Read-RuntimeConfig {
    if (-not (Test-Path -LiteralPath $runtimeConfig)) {
        Copy-Item -LiteralPath $defaultConfig -Destination $runtimeConfig
        Write-Host "Seeded runtime config: $runtimeConfig" -ForegroundColor Green
    }
    try {
        return Get-Content -LiteralPath $runtimeConfig -Raw | ConvertFrom-Json
    } catch {
        if (-not $ResetMalformedConfig) {
            throw "Runtime config is not valid JSON. Re-run with -ResetMalformedConfig to move it aside and seed a fresh copy."
        }
        $backup = "$runtimeConfig.broken-$(Get-Date -Format 'yyyyMMdd-HHmmss')"
        Move-Item -LiteralPath $runtimeConfig -Destination $backup
        Copy-Item -LiteralPath $defaultConfig -Destination $runtimeConfig
        Write-Host "Moved malformed config to: $backup" -ForegroundColor Yellow
        return Get-Content -LiteralPath $runtimeConfig -Raw | ConvertFrom-Json
    }
}

$config = Read-RuntimeConfig
$currentTools = if ($config.PSObject.Properties.Name -contains 'EnableDeveloperTools') { [bool]$config.EnableDeveloperTools } else { $false }
Write-Host "Portable app root: $appRoot"
Write-Host "Runtime config: $runtimeConfig"
Write-Host "Developer tools: $(if ($currentTools) { 'ON' } else { 'OFF' })"
Write-Host "Active content on next launch: $(if (Test-ValidContent $production) { 'Production' } elseif (Test-ValidContent $demo) { 'Demo' } else { 'none valid' })"

if ($DeveloperTools -ne 'report') {
    $nextTools = switch ($DeveloperTools) {
        'on' { $true }
        'off' { $false }
        'toggle' { -not $currentTools }
    }
    if ($config.PSObject.Properties.Name -contains 'EnableDeveloperTools') {
        $config.EnableDeveloperTools = $nextTools
    } else {
        $config | Add-Member -NotePropertyName EnableDeveloperTools -NotePropertyValue $nextTools
    }
    $config | ConvertTo-Json -Depth 32 | Set-Content -LiteralPath $runtimeConfig -Encoding UTF8
    Write-Host "Developer tools changed to: $(if ($nextTools) { 'ON' } else { 'OFF' })" -ForegroundColor Green
    Write-Host 'Restart the application to use the changed setting.' -ForegroundColor DarkGray
}

if (-not $NoPrompt -and -not $NoPause -and [Environment]::UserInteractive) {
    Read-Host 'Press Enter to close' | Out-Null
}
