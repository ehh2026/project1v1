# Update Marker Coordinates

## Quick Steps

1. **Edit Excel File**: Open `Coordinates for map.xlsx` and add/update coordinates
2. **Run Application**: Execute `dotnet run` or use `RunWithUpdatedCoordinates.ps1`
3. **Verify**: Check that markers appear at correct locations on the map

## Excel Format

- **Column A**: Location Name
- **Column D**: Content Folder Name (optional)
- **Column E**: X Coordinate (0-8198 pixels)
- **Column F**: Y Coordinate (0-5542 pixels)

## Map Dimensions

- Width: 8198 pixels
- Height: 5542 pixels

## Scripts Available

- `RunWithUpdatedCoordinates.ps1` - Build and run with coordinate loading info
- `ViewCoordinates.ps1` - View the application log to see loaded coordinates

## Automatic Processing

The application automatically:
- Reads Excel file on startup
- Parses all coordinate rows
- Creates location clusters
- Displays markers on map
- Logs all loaded coordinates

## Verification

Check the log file at: `%AppData%\InteractiveWorldMap\logs\app.log`

The log shows each location loaded with its coordinates.
