$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent $PSScriptRoot
Set-Location $Root

$tempDir = Join-Path $Root "temp"
if (-not (Test-Path $tempDir)) {
    New-Item -ItemType Directory -Path $tempDir | Out-Null
}

$outputPath = Join-Path $tempDir "manual-layouts.verify.json"
if (Test-Path $outputPath) {
    Remove-Item $outputPath
}

dotnet run --project "Tools\ManualLayoutSeedGenerator\ManualLayoutSeedGenerator.csproj" -- `
    --config "visual-config.json" `
    --excel "Coordinates for map.xlsx" `
    --map-image "Images&Content\World Map Extra Large.jpg" `
    --output $outputPath

if (-not (Test-Path $outputPath)) {
    Write-Error "Manual layout seed generator did not create $outputPath"
    exit 1
}

$json = Get-Content $outputPath -Raw | ConvertFrom-Json
if ($null -eq $json.LayoutGroups) {
    Write-Error "Manual layout seed output does not contain LayoutGroups"
    exit 1
}

$groupProperties = @($json.LayoutGroups.PSObject.Properties)
if ($groupProperties.Count -eq 0) {
    Write-Error "Manual layout seed output contains no layout groups"
    exit 1
}

foreach ($property in $groupProperties) {
    $group = $property.Value
    $seedVariants = @($group.Variants | Where-Object {
        $_.Origin -eq "AutoSeed" -and $_.VariantId -eq "seed-default" -and $_.IsDefault -eq $true
    })

    if ($seedVariants.Count -lt 1) {
        Write-Error "Layout group '$($property.Name)' has no default AutoSeed seed-default variant"
        exit 1
    }
}

Write-Host "Manual layout seed verification passed: $($groupProperties.Count) group(s)"
