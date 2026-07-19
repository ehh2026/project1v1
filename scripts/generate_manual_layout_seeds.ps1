param(
    [string]$ConfigPath = "visual-config.json",
    [string]$ExcelPath = "Coordinates for map.xlsx",
    [string]$MapImagePath = "Images&Content\World Map Extra Large.jpg",
    [string]$OutputPath = "Images&Content\manual-layouts.json"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

dotnet run --project "Tools\ManualLayoutSeedGenerator\ManualLayoutSeedGenerator.csproj" -- `
    --config $ConfigPath `
    --excel $ExcelPath `
    --map-image $MapImagePath `
    --output $OutputPath
