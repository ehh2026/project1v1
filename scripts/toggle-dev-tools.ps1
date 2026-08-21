#requires -Version 5.1
<#
.SYNOPSIS
    Turn the app's developer tools (Edit Layout, tuning panel, debug overlays, content-image
    diagnostics, etc.) on or off without hand-editing JSON.

.DESCRIPTION
    Flips "EnableDeveloperTools" in the runtime visual-config.json that the app reads — the local
    user config next to the built executable. That file is git-ignored, so this never changes the
    shipped defaults or affects other machines.

    Every folder under the repo that contains the built InteractiveWorldMap.exe is updated, so it
    does not matter whether you run a Debug build, a Release build, or a published/self-contained
    executable (e.g. bin\<Config>\net6.0-windows\publish\). Use -PublishDir to also target a
    publish output written outside the repo.

    If the runtime config does not exist yet next to an exe, it is seeded from the sibling
    visual-config.default.json. Changes take effect the next time the app launches.

.PARAMETER State
    on | off | toggle (default: toggle). "toggle" flips whatever the config currently has.

.PARAMETER PublishDir
    Optional extra folder to include (e.g. a publish output written outside the repo with
    `dotnet publish -o`). The folder itself and any subfolders containing the exe are updated.

.EXAMPLE
    .\scripts\toggle-dev-tools.ps1                              # flip it
    .\scripts\toggle-dev-tools.ps1 -State on                    # force on
    .\scripts\toggle-dev-tools.ps1 -State on -PublishDir D:\Gallery\App  # include an external publish
#>
[CmdletBinding()]
param(
    [ValidateSet('on', 'off', 'toggle')]
    [string]$State = 'toggle',

    [string]$PublishDir
)

$ErrorActionPreference = 'Stop'

function Test-LaunchedFromExplorer {
    # A double-click gets a console that closes the instant this script ends, so the lines below
    # would flash past unread. A console you were already in keeps them. The command line cannot
    # tell those apart -- running the .bat wrapper from a PowerShell prompt produces almost exactly
    # what Explorer produces -- but the process that started us can.
    try {
        $id = $PID
        for ($hop = 0; $hop -lt 4 -and $id; $hop++) {
            $proc = Get-CimInstance Win32_Process -Filter "ProcessId=$id" -ErrorAction Stop
            if (-not $proc) { return $false }
            if ($proc.Name -eq 'explorer.exe') { return $true }
            $id = $proc.ParentProcessId
        }
    } catch {
        return $false
    }
    return $false
}

$repoRoot = Split-Path -Parent $PSScriptRoot
$exeName = 'InteractiveWorldMap.exe'

# Runtime configs live next to the built exe. Discover every folder that actually contains the exe
# (Debug, Release, publish/, self-contained output, ...) so it doesn't matter how the app was built
# or run. This is more robust than hardcoding Debug/Release and covers published executables.
$searchRoots = @(Join-Path $repoRoot 'bin')
if ($PublishDir) {
    $searchRoots += $PublishDir
}

$configPaths = @()
foreach ($root in $searchRoots) {
    if (-not (Test-Path $root)) {
        continue
    }
    # A publish/exe folder may hold only the exe (config seeded on first run); match on the exe.
    $configPaths += Get-ChildItem -Path $root -Recurse -File -Filter $exeName -ErrorAction SilentlyContinue |
        ForEach-Object { Join-Path $_.DirectoryName 'visual-config.json' }
}
# An external -PublishDir may be the exe folder itself.
if ($PublishDir -and (Test-Path (Join-Path $PublishDir $exeName))) {
    $configPaths += Join-Path $PublishDir 'visual-config.json'
}
$configPaths = $configPaths | Sort-Object -Unique

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
    Write-Warning "No $exeName found under bin\ (or -PublishDir). Build/publish the app once, then re-run this script."
    if (Test-LaunchedFromExplorer) { Read-Host "Press Enter to close" | Out-Null }
    exit 1
}

foreach ($u in $updated) {
    Write-Host ("Developer tools {0} -> {1}  ({2})" -f `
        ($(if ($u.From) { 'ON' } else { 'OFF' })), `
        ($(if ($u.To)   { 'ON' } else { 'OFF' })), `
        $u.Path)
}
Write-Host "Relaunch the app for the change to take effect."

if (Test-LaunchedFromExplorer) {
    Write-Host ""
    Read-Host "Press Enter to close" | Out-Null
}
