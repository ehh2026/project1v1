# Updating Map Coordinates

## Overview

The application automatically reads location coordinates from the Excel spreadsheet when it starts. You don't need to manually update the locations.json file.

## How It Works

1. The application looks for `Coordinates for map.xlsx` in the application directory
2. If found, it reads coordinates from the Excel file (takes priority)
3. If not found, it falls back to `Images&Content/locations.json`

## Excel File Format

The Excel spreadsheet should have the following columns:

- **Column A**: Name (location name)
- **Column D**: Address (optional, used for content folder name)
- **Column E**: Coordinate X halfsize (pixel X coordinate)
- **Column F**: Coordinate Y halfsize (pixel Y coordinate)

The first row is treated as a header and is skipped.

## Adding New Locations

1. Open `Coordinates for map.xlsx`
2. Add a new row with the location information:
   - Name: The display name for the location
   - Address: The folder name in `Images&Content/` (optional)
   - X coordinate: Horizontal pixel position on the map
   - Y coordinate: Vertical pixel position on the map
3. Save the Excel file
4. Run the application - it will automatically load the new coordinates

## Map Dimensions

The map image dimensions are:
- Width: 8198 pixels
- Height: 5542 pixels

Use these dimensions when calculating pixel coordinates for new locations.

## Clustering

Locations that are close together will automatically be clustered. The clustering algorithm:
- Groups nearby locations based on zoom level
- Shows cluster markers with location counts
- Expands to individual markers when zoomed in
- Caches clustering results for performance

## Testing New Coordinates

1. Save your changes to the Excel file
2. Run the application: `dotnet run`
3. The map will display with the updated markers
4. Click on markers to verify they're positioned correctly
5. Check that content folders match the location names

## Troubleshooting

If markers don't appear:
- Check the application logs for parsing errors
- Verify the Excel file format matches the expected structure
- Ensure X and Y coordinates are numeric values
- Confirm coordinates are within the map bounds (0-8198 for X, 0-5542 for Y)

## Content Folders

For each location, create a folder in `Images&Content/` with the same name as the location. The folder should contain:
- Numbered image files (e.g., `1-image.jpg`, `2-image.jpg`)
- Optional `didactic.txt` file for informational text
- Optional `.txt` files matching image names for translations
