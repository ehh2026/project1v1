# Interactive World Map - Demo Instructions

## Current Status

The application is now functional and ready for a demo! Here's what's been implemented:

### Completed Features ✓

1. **Full-Screen World Map Display**
   - High-resolution map rendering with aspect ratio preservation
   - Automatic scaling for different screen resolutions

2. **Interactive Location Markers**
   - 5 sample locations: New York, London, Tokyo, Sydney, Paris
   - Smooth hover animations (scale to 1.2x)
   - Click pulse animations
   - Visual feedback with radial gradient styling

3. **Content Subwindows**
   - Click any marker to view location content
   - Smooth open/close animations
   - Displays images from the Images&Content folder
   - Centered positioning with drop shadow

4. **User Interactions**
   - Click markers to open content
   - Click outside subwindow to close it
   - Press Escape to close subwindow or exit app
   - Window resizing updates marker positions

## How to Run the Demo

### Option 1: Using the Batch File (Easiest)
Simply double-click `run-demo.bat` in the project root folder. This will:
1. Build the project
2. Automatically launch the application

### Option 2: Using Visual Studio
1. Open `InteractiveWorldMap.csproj` in Visual Studio
2. Press F5 or click "Start"
3. The application will launch in full-screen mode

### Option 3: Using Command Line
```bash
# Build first
dotnet build InteractiveWorldMap.csproj

# Then run
dotnet run --project InteractiveWorldMap.csproj
```

### Option 4: Run the Executable Directly
```bash
.\bin\Debug\net6.0-windows\InteractiveWorldMap.exe
```

## Troubleshooting

### If you get file not found errors:
1. Make sure you've built the project at least once: `dotnet build`
2. Verify the `Images&Content` folder exists in `bin/Debug/net6.0-windows/`
3. Check that all image files are present:
   - Large_World_Map_bright.jpg
   - letter-product-pic.jpg
   - v4-460px-Write-a-Friendly-Letter-Step-4-Version-6.jpg
   - locations.json

### To view detailed logs:
Check the log file at: `%APPDATA%\InteractiveWorldMap\logs\app.log`

You can open it with:
```bash
notepad %APPDATA%\InteractiveWorldMap\logs\app.log
```

## What to Try

1. **View the Map**: The world map displays in full-screen with a black background
2. **Find Markers**: Look for the circular markers at major cities
3. **Hover Effect**: Move your mouse over a marker to see it scale up
4. **Click to View**: Click a marker to open a content subwindow with an image
5. **Close Subwindow**: Click anywhere outside the subwindow to close it
6. **Exit**: Press Escape to exit the application

## Sample Locations

The demo includes 5 locations:
- **New York City** (40.71°N, 74.01°W)
- **London** (51.51°N, 0.13°W)
- **Tokyo** (35.68°N, 139.65°E)
- **Sydney** (33.87°S, 151.21°E)
- **Paris** (48.86°N, 2.35°E)

## Known Limitations

- Currently uses placeholder images for all locations
- Text content type not yet fully implemented
- No loading indicators for async operations
- Limited error handling UI

## Next Steps

To complete the full implementation:
- Add unit tests for UI components
- Implement property-based tests
- Add performance optimizations
- Create comprehensive error handling
- Add more sample content

## Troubleshooting

### If you get file not found errors:
1. Make sure you've built the project at least once: `dotnet build`
2. Verify the `Images&Content` folder exists in `bin/Debug/net6.0-windows/`
3. Check that all image files are present:
   - Large_World_Map_bright.jpg
   - letter-product-pic.jpg
   - v4-460px-Write-a-Friendly-Letter-Step-4-Version-6.jpg
   - locations.json

### To view detailed logs:
Check the log file at: `%APPDATA%\InteractiveWorldMap\logs\app.log`

You can open it with:
```bash
notepad %APPDATA%\InteractiveWorldMap\logs\app.log
```

### Common Issues:
- **Black screen**: The map image may not have loaded. Check the logs.
- **No markers visible**: Verify locations.json is valid JSON and in the correct format.
- **Application crashes on startup**: Check that all required files are in the Images&Content folder.
