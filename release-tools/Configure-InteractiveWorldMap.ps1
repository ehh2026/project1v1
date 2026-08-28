#requires -Version 5.1
<#
.SYNOPSIS
    Inspect or change the configuration of an extracted portable release.
.DESCRIPTION
    Run with no arguments (or double-click the .bat) for a menu. Pass -DeveloperTools
    to make a change without prompting, which is the form to use from a script.
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
function Read-Line([string]$Prompt) {
    # UserInteractive stays true in a -NonInteractive host, where Read-Host throws instead
    # of returning. Treat that as 'nobody is there' rather than failing the helper.
    if (-not [Environment]::UserInteractive) { return $null }
    try { return Read-Host $Prompt } catch { return $null }
}

function Wait-ForClose {
    if ($NoPause) { return }
    $null = Read-Line 'Press Enter to close'
}
trap {
    Write-Host "Configuration helper failed: $($_.Exception.Message)" -ForegroundColor Red
    Wait-ForClose
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

function Get-DeveloperTools($Config) {
    if ($Config.PSObject.Properties.Name -contains 'EnableDeveloperTools') {
        return [bool]$Config.EnableDeveloperTools
    }
    return $false
}

function Set-DeveloperTools($Config, [bool]$Value) {
    if ($Config.PSObject.Properties.Name -contains 'EnableDeveloperTools') {
        $Config.EnableDeveloperTools = $Value
    } else {
        $Config | Add-Member -NotePropertyName EnableDeveloperTools -NotePropertyValue $Value
    }
    $Config | ConvertTo-Json -Depth 32 | Set-Content -LiteralPath $runtimeConfig -Encoding UTF8
    if ($Value) { $state = 'ON' } else { $state = 'OFF' }
    Write-Host "Developer tools changed to: $state" -ForegroundColor Green
    Write-Host 'Restart the application to use the changed setting.' -ForegroundColor DarkGray
}

function Show-Status($Config) {
    if (Get-DeveloperTools $Config) { $tools = 'ON' } else { $tools = 'OFF' }
    if (Test-ValidContent $production) {
        $active = 'Production'
    } elseif (Test-ValidContent $demo) {
        $active = 'Demo'
    } else {
        $active = 'none valid'
    }
    Write-Host ''
    Write-Host "Portable app root: $appRoot"
    Write-Host "Runtime config:    $runtimeConfig"
    Write-Host "Developer tools:   $tools"
    Write-Host "Active content on next launch: $active"
}

$config = Read-RuntimeConfig
Show-Status $config

# An explicit -DeveloperTools value is a scripted change: apply it and do not prompt.
if ($DeveloperTools -ne 'report') {
    switch ($DeveloperTools) {
        'on'     { Set-DeveloperTools $config $true }
        'off'    { Set-DeveloperTools $config $false }
        'toggle' { Set-DeveloperTools $config (-not (Get-DeveloperTools $config)) }
    }
    Wait-ForClose
    exit 0
}

# No arguments: a double-clicked helper that can only report is not much of a helper,
# so offer the same changes the flags make.
if ($NoPrompt -or -not [Environment]::UserInteractive) {
    Wait-ForClose
    exit 0
}

while ($true) {
    Write-Host ''
    Write-Host '  [1] Turn developer tools ON'
    Write-Host '  [2] Turn developer tools OFF'
    Write-Host '  [3] Toggle developer tools'
    Write-Host '  [R] Re-read and show current settings'
    Write-Host '  [Q] Quit'
    $answer = Read-Line 'Choose an option'
    if ($null -eq $answer) { exit 0 }
    $choice = $answer.Trim().ToLowerInvariant()

    switch ($choice) {
        '1' { Set-DeveloperTools $config $true }
        '2' { Set-DeveloperTools $config $false }
        '3' { Set-DeveloperTools $config (-not (Get-DeveloperTools $config)) }
        'r' { $config = Read-RuntimeConfig; Show-Status $config }
        'q' { exit 0 }
        ''  { }
        default { Write-Host "Not an option: $choice" -ForegroundColor Yellow }
    }
}
