# Architecture Changes - Pixel-Based Coordinate System

## Overview
The application has been refactored to use pixel coordinates instead of latitude/longitude, and to load location data from an Excel file with a new folder structure for content.

## Key Changes

### 1. Location Model (`Models/Location.cs`)
- **Removed**: `Latitude` and `Longitude` properties
- **Added**: `PixelX` and `PixelY` properties
- Coordinates now represent pixel positions on the map image (16397 x 11085 pixels)

### 2. Excel Data Source (`Utilities/ExcelCoordinateReader.cs`)
- New utility class to read location data from Excel files
- Parses `Coordinates for map.xlsx` from the application root directory
- Extracts: Name, PixelX, PixelY, and Address from the Excel sheet
- Includes comprehensive debug logging for troubleshooting

### 3. Content Loader Updates (`Services/ContentLoader.cs`)
- **New behavior**: Loads locations from Excel file if `locations.json` doesn't exist
- **Content structure**: Each location's content is stored in a subfolder named after the location
  - Example: `Images&Content/New York City/` contains images for NYC
- Automatically finds and loads the first image file (jpg, png, jpeg) from the location folder
- Enhanced error logging with stack traces

### 4. Marker Layer Control (`Views/MarkerLayerControl.xaml.cs`)
- Removed dependency on `CoordinateMapper`
- Direct pixel-to-screen coordinate conversion
- Image dimensions hardcoded: 16397 x 11085 pixels
- Calculates screen position by normalizing pixel coordinates to map bounds

### 5. Map Display Control (`Views/MapDisplayControl.xaml.cs`)
- Updated `GetMapPosition()` method signature
- Now accepts: `pixelX`, `pixelY`, `imageWidth`, `imageHeight`
- Returns screen position for rendering markers

### 6. Main Window (`MainWindow.xaml.cs`)
- Updated to pass pixel coordinates and image dimensions when showing content
- Image dimensions: 16397 x 11085

### 7. Startup Validator (`Services/StartupValidator.cs`)
- Enhanced debug logging with visual indicators (✓, ✗)
- Checks for both `locations.json` and Excel file
- Validates pixel coordinates are within valid ranges
- Logs detailed environment information at startup

## Data Flow

```
Excel File (Coordinates for map.xlsx)
    ↓
ExcelCoordinateReader
    ↓
Location objects (with PixelX, PixelY)
    ↓
MarkerLayerControl (positions markers on screen)
    ↓
ContentLoader (loads content from Images&Content/{LocationName}/)
    ↓
ContentSubwindow (displays content)
```

## Folder Structure

```
Images&Content/
├── World Map 1976.jpg
├── New York City/
│   ├── image1.jpg
│   └── image2.jpg
├── London/
│   ├── image1.jpg
│   └── image2.jpg
└── ... (other locations)
```

## Debug Logging

The application now includes comprehensive logging:
- Startup validation with visual indicators
- Excel file parsing with row-by-row details
- Content loading with folder path information
- Error messages with stack traces

Check logs at: `%APPDATA%\InteractiveWorldMap\logs\app.log`

## Migration from JSON

If you have an existing `locations.json` file, it will still be used. To switch to Excel:
1. Delete or rename `Images&Content/locations.json`
2. Ensure `Coordinates for map.xlsx` is in the application root directory
3. Restart the application

## Error Handling

- If Excel file is malformed, the application logs detailed errors and falls back gracefully
- Missing content folders are logged as warnings but don't crash the app
- Invalid pixel coordinates are validated and logged
