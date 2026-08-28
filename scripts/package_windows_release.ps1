#requires -Version 5.1
<#
.SYNOPSIS
    Create and validate the public portable Windows release zip.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$PublishDirectory,

    [Parameter(Mandatory)]
    [string]$OutputDirectory,

    [Parameter(Mandatory)]
    [ValidatePattern('^[0-9][0-9A-Za-z.-]*$')]
    [string]$Version
)

$ErrorActionPreference = 'Stop'
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$artifactsRoot = [IO.Path]::GetFullPath((Join-Path $repoRoot 'artifacts'))
$publishRoot = [IO.Path]::GetFullPath($PublishDirectory)
$outputRoot = [IO.Path]::GetFullPath($OutputDirectory)

function Test-PathInside([string]$Child, [string]$Parent) {
    $childPath = $Child.TrimEnd([IO.Path]::DirectorySeparatorChar, [IO.Path]::AltDirectorySeparatorChar)
    $parentPath = $Parent.TrimEnd([IO.Path]::DirectorySeparatorChar, [IO.Path]::AltDirectorySeparatorChar)
    return $childPath.StartsWith($parentPath + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)
}

function Invoke-PackageValidator([string[]]$Arguments) {
    $validator = Join-Path $PSScriptRoot 'verify_release_package.py'
    if (Get-Command py -ErrorAction SilentlyContinue) {
        & py -3 $validator @Arguments
    } elseif (Get-Command python -ErrorAction SilentlyContinue) {
        & python $validator @Arguments
    } else {
        throw 'Python 3 is required to validate the portable release package.'
    }
    if ($LASTEXITCODE -ne 0) { throw "Release package validation failed ($LASTEXITCODE)." }
}

if (-not (Test-Path -LiteralPath $publishRoot -PathType Container)) {
    throw "Publish directory does not exist: $publishRoot"
}
if (-not (Test-Path -LiteralPath (Join-Path $publishRoot 'InteractiveWorldMap.exe') -PathType Leaf)) {
    throw "Publish directory does not contain InteractiveWorldMap.exe: $publishRoot"
}
foreach ($marker in @('.git', 'InteractiveWorldMap.sln', 'InteractiveWorldMap.csproj')) {
    if (Test-Path -LiteralPath (Join-Path $publishRoot $marker)) {
        throw "Publish directory looks like a checkout, not publish output: $publishRoot"
    }
}
if (-not (Test-PathInside $outputRoot $artifactsRoot)) {
    throw "OutputDirectory must be inside the git-ignored artifacts directory: $artifactsRoot"
}
if ($outputRoot -eq $repoRoot -or $repoRoot.StartsWith($outputRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
    throw "OutputDirectory must not be the repository root or one of its parents: $outputRoot"
}

New-Item -ItemType Directory -Force -Path $outputRoot | Out-Null
$packageName = "InteractiveWorldMap-win-x64-$Version"
$stagingRoot = Join-Path $outputRoot $packageName
$archivePath = Join-Path $outputRoot "$packageName.zip"
if ((Test-Path -LiteralPath $stagingRoot) -or (Test-Path -LiteralPath $archivePath)) {
    throw "A package for version '$Version' already exists in $outputRoot. Choose a new version or remove that known artifact."
}

New-Item -ItemType Directory -Path $stagingRoot | Out-Null
Get-ChildItem -LiteralPath $publishRoot -Force | ForEach-Object {
    Copy-Item -LiteralPath $_.FullName -Destination $stagingRoot -Recurse -Force
}
foreach ($relativePath in @('Images&Content\Production-Content', 'Images&Content\Extras', 'Images&Content\README.md', 'visual-config.json')) {
    $target = Join-Path $stagingRoot $relativePath
    if (Test-Path -LiteralPath $target) {
        Remove-Item -LiteralPath $target -Recurse -Force
    }
}
Get-ChildItem -LiteralPath $stagingRoot -Recurse -File |
    Where-Object { $_.Extension -in @('.pdb', '.xml') } |
    Remove-Item -Force

Copy-Item -LiteralPath (Join-Path $repoRoot 'release-tools') -Destination (Join-Path $stagingRoot 'Tools') -Recurse -Force
Copy-Item -LiteralPath (Join-Path $repoRoot 'docs\release\PORTABLE_WINDOWS_RELEASE.md') -Destination (Join-Path $stagingRoot 'README.md') -Force

Invoke-PackageValidator @('--package-root', $stagingRoot)
Compress-Archive -LiteralPath $stagingRoot -DestinationPath $archivePath -CompressionLevel Optimal
Invoke-PackageValidator @('--zip', $archivePath)
Write-Host "Created portable release package: $archivePath" -ForegroundColor Green

Copy-Item -LiteralPath (Join-Path $repoRoot 'docs\release\IF_SOMETHING_LOOKS_WRONG.md') -Destination (Join-Path $stagingRoot 'IF-SOMETHING-LOOKS-WRONG.md') -Force