#requires -Version 5.1
<#
.SYNOPSIS
    Turn the app's developer tools (Edit Layout, tuning panel, debug overlays, content-image
    diagnostics, etc.) on or off without hand-editing JSON.

.DESCRIPTION
    Flips "EnableDeveloperTools" in the runtime visual-config.json that the app reads — the local
    user config next to the built executable (bin\<Config>\net6.0-windows\visual-config.json). That
    file is git-ignored, so this never changes the shipped defaults or affects other machines.

    If the runtime config does not exist yet (app not built/run), it is seeded from the sibling
    visual-config.default.json. Changes take effect the next time the app launches.

.PARAMETER State
    on | off | toggle (default: toggle). "toggle" flips whatever the config currently has.

.EXAMPLE
    .\scripts\toggle-dev-tools.ps1            # flip it
    .\scripts\toggle-dev-tools.ps1 -State on  # force on
#>
[CmdletBinding()]
param(
    [ValidateSet('on', 'off', 'toggle')]
    [string]$State = 'toggle'
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot

# Runtime configs live next to the built exe. Update every build output that exists so it doesn't
# matter whether the user runs Debug or Release.
$configPaths = @(
    Join-Path $repoRoot 'bin\Debug\net6.0-windows\visual-config.json'
    Join-Path $repoRoot 'bin\Release\net6.0-windows\visual-config.json'
)

$updated = @()
foreach ($configPath in $configPaths) {
    $dir = Split-Path -Parent $configPath
    if (-not (Test-Path $dir)) {
        continue  # this build flavor hasn't been produced yet
    }

    # Seed from the shipped default if the user config isn't there yet (mirrors the app's own behavior).
    if (-not (Test-Path $configPath)) {
        $defaultPath = Join-Path $dir 'visual-config.default.json'
        if (Test-Path $defaultPath) {
            Copy-Item $defaultPath $configPath
        } else {
            '{}' | Set-Content -Path $configPath -Encoding utf8
        }
    }

    $json = Get-Content -Raw -Path $configPath | ConvertFrom-Json

    $current = $false
    if ($json.PSObject.Properties.Name -contains 'EnableDeveloperTools') {
        $current = [bool]$json.EnableDeveloperTools
    }

    $desired = switch ($State) {
        'on'     { $true }
        'off'    { $false }
        'toggle' { -not $current }
    }

    if ($json.PSObject.Properties.Name -contains 'EnableDeveloperTools') {
        $json.EnableDeveloperTools = $desired
    } else {
        $json | Add-Member -NotePropertyName 'EnableDeveloperTools' -NotePropertyValue $desired
    }

    ($json | ConvertTo-Json -Depth 64) | Set-Content -Path $configPath -Encoding utf8
    $updated += [pscustomobject]@{ Path = $configPath; From = $current; To = $desired }
}

if ($updated.Count -eq 0) {
    Write-Warning "No build output found under bin\. Build or run the app once, then re-run this script."
    exit 1
}

foreach ($u in $updated) {
    Write-Host ("Developer tools {0} -> {1}  ({2})" -f `
        ($(if ($u.From) { 'ON' } else { 'OFF' })), `
        ($(if ($u.To)   { 'ON' } else { 'OFF' })), `
        $u.Path)
}
Write-Host "Relaunch the app for the change to take effect."
