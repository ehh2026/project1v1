#requires -Version 5.1
<#
.SYNOPSIS
    Shows which files control what in this app, where they are on this machine, and offers to
    turn the developer tools on or off.

.DESCRIPTION
    Read-only. This script never edits a config itself - it prints the config surface with
    resolved absolute paths and flags which files exist, so you know what to open. The one
    action it can take is running the developer-tools toggle, and only if you say yes.

.PARAMETER NoPrompt
    Print the table and exit without offering to run the developer-tools toggle. Use this when
    running from a script or a non-interactive shell.

.EXAMPLE
    .\configure.ps1
    .\configure.ps1 -NoPrompt
#>
[CmdletBinding()]
param(
    [switch]$NoPrompt
)

$ErrorActionPreference = 'Stop'
$repoRoot = $PSScriptRoot
$exeName = 'InteractiveWorldMap.exe'

function Write-Heading([string]$text) {
    Write-Host ''
    Write-Host $text -ForegroundColor Cyan
    Write-Host ('-' * $text.Length) -ForegroundColor DarkGray
}

function Write-ConfigEntry([string]$path, [string]$controls, [string]$note) {
    $exists = Test-Path $path
    $marker = if ($exists) { '[found]  ' } else { '[missing]' }
    $color  = if ($exists) { 'Green' } else { 'DarkYellow' }

    Write-Host "  $marker " -ForegroundColor $color -NoNewline
    Write-Host $controls -ForegroundColor White
    Write-Host "            $path" -ForegroundColor Gray
    if ($note) {
        Write-Host "            $note" -ForegroundColor DarkGray
    }
    Write-Host ''
}

Write-Heading 'Interactive World Map - configuration files'

Write-Host '  Edit these with any text editor. Paths below are resolved for this machine.' -ForegroundColor DarkGray
Write-Host ''

# Content lives under a tracked folder, so these paths are stable.
$contentDir = Join-Path $repoRoot 'Images&Content\Demo-Content'

Write-ConfigEntry `
    (Join-Path $contentDir 'locations.json') `
    'Markers and locations - what appears on the map, and where' `
    'Names here are also what layout keys are derived from; renaming a location orphans its saved layouts.'

Write-ConfigEntry `
    (Join-Path $contentDir 'manual-layouts.json') `
    'Saved manual layouts - hand-placed pin positions' `
    'Written by the in-app layout editor. Hand-editing is possible but rarely necessary.'

Write-ConfigEntry `
    (Join-Path $repoRoot 'visual-config.default.json') `
    'Shipped defaults - tracked in git, seeds a new runtime config' `
    'Change this to alter the defaults everyone gets. For a local-only change, use the runtime config below.'

# The runtime config is the one the app actually reads, and it lives next to whichever exe was
# built. There may be several (Debug, Release, publish output), so list every one that exists
# rather than guessing at a single path.
Write-Heading 'Live visual settings (visual-config.json, next to the built exe)'
Write-Host '  Git-ignored, seeded from the defaults on first run. This is the file the app reads.' -ForegroundColor DarkGray
Write-Host ''

$binRoot = Join-Path $repoRoot 'bin'
$exeDirs = @()
if (Test-Path $binRoot) {
    $exeDirs = Get-ChildItem -Path $binRoot -Recurse -File -Filter $exeName -ErrorAction SilentlyContinue |
        ForEach-Object { $_.DirectoryName } |
        Sort-Object -Unique
}

if ($exeDirs.Count -eq 0) {
    Write-Host '  [missing]  No built executable found under bin\.' -ForegroundColor DarkYellow
    Write-Host '             Build once (run-demo.bat, or dotnet build) and re-run this script.' -ForegroundColor DarkGray
    Write-Host ''
} else {
    foreach ($dir in $exeDirs) {
        $runtimeConfig = Join-Path $dir 'visual-config.json'
        $devTools = 'unknown'
        if (Test-Path $runtimeConfig) {
            try {
                $json = Get-Content -Raw -Path $runtimeConfig | ConvertFrom-Json
                if ($json.PSObject.Properties.Name -contains 'EnableDeveloperTools') {
                    $devTools = if ([bool]$json.EnableDeveloperTools) { 'ON' } else { 'OFF' }
                } else {
                    $devTools = 'OFF (setting absent)'
                }
            } catch {
                $devTools = 'unreadable - the file may not be valid JSON'
            }
        }
        Write-ConfigEntry $runtimeConfig "Developer tools: $devTools" ''
    }
}

Write-Heading 'Developer tools'
Write-Host '  Turns on the Edit Layout button, the tuning panel, and the debug overlays.' -ForegroundColor DarkGray
Write-Host '  Toggle it any time with:  .\toggle-dev-tools.bat -State on' -ForegroundColor DarkGray
Write-Host ''

if ($NoPrompt) {
    return
}

# Only prompt when there is a console to answer on; otherwise Read-Host reads EOF and loops.
if ([Environment]::UserInteractive -eq $false) {
    Write-Host '  Non-interactive session - skipping the toggle prompt.' -ForegroundColor DarkGray
    return
}

$answer = Read-Host '  Run the developer-tools toggle now? (on / off / no)'
switch ($answer.Trim().ToLowerInvariant()) {
    'on'  { $state = 'on' }
    'off' { $state = 'off' }
    default {
        Write-Host '  No changes made.' -ForegroundColor DarkGray
        return
    }
}

# Go through the .bat wrapper rather than the .ps1 directly: it is the supported entry point,
# and using it here means this script exercises the same path a user would.
Write-Host ''
& (Join-Path $repoRoot 'toggle-dev-tools.bat') -State $state
