param(
    [string]$ConfigPath = "visual-config.json",
    [string]$ExcelPath = "Images&Content\Demo-Content\Coordinates for map.xlsx",
    [string]$MapImagePath = "Images&Content\Assets\World Map Extra Large.jpg",
    [string]$OutputPath = "Images&Content\Demo-Content\manual-layouts.json"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

dotnet run --project "Tools\ManualLayoutSeedGenerator\ManualLayoutSeedGenerator.csproj" -- `
    --config $ConfigPath `
    --excel $ExcelPath `
    --map-image $MapImagePath `
    --output $OutputPath
