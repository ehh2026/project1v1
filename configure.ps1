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
    Print the table and exit without offering to run the developer-tools toggle, and without
    the "Press Enter to close" at the end. Use this when running from a script or a
    non-interactive shell.

.EXAMPLE
    .\configure.ps1
    .\configure.ps1 -NoPrompt
#>
[CmdletBinding()]
param(
    [switch]$NoPrompt
)

$ErrorActionPreference = 'Stop'

# Same reason as toggle-dev-tools.ps1: under 'Stop' any failure terminates before the pause at the
# bottom, and this script is the one most likely to be double-clicked, so the window would close
# on the error. Write-Host rather than Write-Error, which under 'Stop' would itself terminate.
trap {
    Write-Host ""
    Write-Host "configure failed: $($_.Exception.Message)" -ForegroundColor Red
    if ($_.InvocationInfo) {
        Write-Host "  at line $($_.InvocationInfo.ScriptLineNumber): $($_.InvocationInfo.Line.Trim())" -ForegroundColor DarkGray
    }
    if (-not $NoPrompt -and [Environment]::UserInteractive) {
        Read-Host "Press Enter to close" | Out-Null
    }
    exit 1
}
$repoRoot = $PSScriptRoot
$exeName = 'InteractiveWorldMap.exe'

function Write-Heading([string]$text) {
    Write-Host ''
    Write-Host $text -ForegroundColor Cyan
    Write-Host ('-' * $text.Length) -ForegroundColor DarkGray
}

function Write-ConfigEntry([string]$path, [string]$controls, [string]$note) {
    $exists = Test-Path $path
    $tag = if ($exists) { '[found]  ' } else { '[missing]' }
    $color  = if ($exists) { 'Green' } else { 'DarkYellow' }

    Write-Host "  $tag " -ForegroundColor $color -NoNewline
    Write-Host $controls -ForegroundColor White
    Write-Host "            $path" -ForegroundColor Gray
    if ($note) {
        Write-Host "            $note" -ForegroundColor DarkGray
    }
    Write-Host ''
}

function Get-DeveloperToolsState([string]$runtimeConfigPath) {
    if (-not (Test-Path $runtimeConfigPath)) {
        return 'not created yet - seeded from the defaults on first run'
    }
    try {
        $json = Get-Content -Raw -Path $runtimeConfigPath | ConvertFrom-Json
        if ($json.PSObject.Properties.Name -contains 'EnableDeveloperTools') {
            if ([bool]$json.EnableDeveloperTools) { return 'ON' } else { return 'OFF' }
        }
        return 'OFF (setting absent)'
    } catch {
        return 'unreadable - the file may not be valid JSON'
    }
}

Write-Heading 'Interactive World Map - configuration files'

Write-Host '  The app reads everything from the folder its .exe is in, never from the repo root.' -ForegroundColor DarkGray
Write-Host '  Both are listed below, because which one to edit depends on what you are doing.' -ForegroundColor DarkGray

# --- Source copies, tracked in git -------------------------------------------------------------
# These are what a build copies into the output folder. Editing one here changes what *future*
# builds get; it does not change a build that already exists until the next build runs.
Write-Heading 'Source copies (tracked in git - change these for good)'

$contentDir = Join-Path $repoRoot 'Images&Content\Demo-Content'

Write-ConfigEntry `
    (Join-Path $contentDir 'locations.json') `
    'Locations - the points the map shows a pin for, and where they sit' `
    'Names here are what layout keys are derived from; renaming a location orphans its saved layouts.'

Write-ConfigEntry `
    (Join-Path $contentDir 'manual-layouts.json') `
    'Saved manual layouts - hand-placed pin positions' `
    'Careful: the app writes the output copy, not this one. A build can overwrite in-app work (see below).'

Write-ConfigEntry `
    (Join-Path $repoRoot 'visual-config.default.json') `
    'Shipped defaults - the seed for a new runtime config' `
    'For a local-only change, edit the runtime visual-config.json below instead.'

# --- What the running app actually reads -------------------------------------------------------
# Every path the app resolves is relative to AppDomain.CurrentDomain.BaseDirectory, i.e. the folder
# holding the .exe. There may be several (Debug, Release, a publish output), each with its own
# independent copies, so list every one that exists rather than guessing at a single path.
Write-Heading 'What the running app reads (next to each built .exe)'

$binRoot = Join-Path $repoRoot 'bin'
# @(...) so a single match stays an array rather than collapsing to a scalar.
$exeDirs = @()
if (Test-Path $binRoot) {
    $exeDirs = @(
        Get-ChildItem -Path $binRoot -Recurse -File -Filter $exeName -ErrorAction SilentlyContinue |
            ForEach-Object { $_.DirectoryName } |
            Sort-Object -Unique
    )
}

if ($exeDirs.Count -eq 0) {
    Write-Host '  [missing]  No built executable found under bin\.' -ForegroundColor DarkYellow
    Write-Host '             Build once (run-demo.bat, or dotnet build) and re-run this script.' -ForegroundColor DarkGray
    Write-Host ''
} else {
    foreach ($dir in $exeDirs) {
        Write-Host "  $dir" -ForegroundColor Cyan
        Write-Host ''

        $runtimeConfig = Join-Path $dir 'visual-config.json'
        Write-ConfigEntry `
            $runtimeConfig `
            ("visual-config.json - live settings. Developer tools: " + (Get-DeveloperToolsState $runtimeConfig)) `
            'Git-ignored, and the only one of these a build never touches. Safe to edit by hand.'

        Write-ConfigEntry `
            (Join-Path $dir 'visual-config.default.json') `
            'visual-config.default.json - the defaults this build actually falls back to' `
            'A copy. Editing it works until the next build replaces it from the repo-root source.'

        $outputContent = Join-Path $dir 'Images&Content\Demo-Content'
        Write-ConfigEntry `
            (Join-Path $outputContent 'locations.json') `
            'locations.json - the locations this build shows' `
            'A copy, same caveat.'

        Write-ConfigEntry `
            (Join-Path $outputContent 'manual-layouts.json') `
            'manual-layouts.json - where the in-app editor saves your layouts' `
            'This is the file that grows as you use Edit Layout. Back it up before editing the source copy.'
    }

    Write-Host '  Copies are refreshed from the repo-root source whenever that source is newer, so' -ForegroundColor DarkYellow
    Write-Host '  editing the source manual-layouts.json and rebuilding will discard layouts saved' -ForegroundColor DarkYellow
    Write-Host '  in the app. To keep them, copy the output file back over the source instead.' -ForegroundColor DarkYellow
    Write-Host ''
}

Write-Heading 'Developer tools'
Write-Host '  Turns on the Edit Layout button, the tuning panel, and the debug overlays.' -ForegroundColor DarkGray
Write-Host '  Toggle it any time with:  .\toggle-dev-tools.bat -State on' -ForegroundColor DarkGray
Write-Host ''

# No early returns past this point: the closing pause has to be reached on every path, or a
# double-click closes the window before any of the above can be read.
$exitCode = 0

if (-not $NoPrompt) {
    # Only prompt when there is a console to answer on; otherwise Read-Host reads EOF and loops.
    if (-not [Environment]::UserInteractive) {
        Write-Host '  Non-interactive session - skipping the toggle prompt.' -ForegroundColor DarkGray
    } else {
        $answer = Read-Host '  Run the developer-tools toggle now? (on / off / no)'
        $state = switch ($answer.Trim().ToLowerInvariant()) {
            'on'  { 'on' }
            'off' { 'off' }
            default { $null }
        }

        if ($state) {
            # Go through the .bat wrapper rather than the .ps1 directly: it is the supported entry
            # point, and using it here means this script exercises the same path a user would.
            # -NoPause because the pause below already covers this run.
            Write-Host ''
            & (Join-Path $repoRoot 'toggle-dev-tools.bat') -State $state -NoPause
            $exitCode = $LASTEXITCODE

            # The toggle exits 1 when it found nothing to write. Saying so here matters: the only
            # action this script can take would otherwise appear to have succeeded, and the exit
            # code has to reach the caller for a script to notice at all.
            if ($exitCode -ne 0) {
                Write-Host ''
                Write-Host '  The developer-tools toggle failed - see its message above.' -ForegroundColor Red
            }
        } else {
            Write-Host '  No changes made.' -ForegroundColor DarkGray
        }

        # Always pause. Telling a double-click apart from a console you are already sitting in is
        # not reliably possible: explorer.exe is an ancestor of both, and a double-click's command
        # line is near-identical to what PowerShell produces when you run the wrapper from a prompt.
        # An extra keypress in a console is a small cost; a window that closes before it can be read
        # makes the script useless for the people most likely to double-click it.
        Write-Host ''
        Read-Host '  Press Enter to close' | Out-Null
    }
}

exit $exitCode
