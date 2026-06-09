# Quick Start Guide

## Running the Application

To run the application with your updated coordinates:

```bash
dotnet run
```

The application will automatically:
1. Read coordinates from `Coordinates for map.xlsx`
2. Generate location clusters
3. Display markers on the map

## What's New

### Recent Updates

1. **Zoom Level**: Markers now zoom to 12x (increased from 10x)
2. **Content Window**: 10% wider and 20% taller for better viewing
3. **Image Ordering**: Images are sorted by filename (use numeric prefixes like `1-`, `2-`, `3-`)
4. **Didactic Text**: Automatic display of `didactic.txt` content in a side window
5. **Dynamic Sizing**: Didactic window automatically sizes to fit content

### Coordinate Updates

Your new coordinates from the Excel spreadsheet will be loaded automatically when you run the application. No manual JSON editing required!

## Testing Your Changes

1. Run the application: `dotnet run`
2. Verify markers appear at the correct locations
3. Click markers to test zoom and content display
4. Check that clustering works for nearby locations

## File Structure

```
Project1v1/
├── Coordinates for map.xlsx          # Your coordinate data (auto-loaded)
├── Images&Content/
│   ├── locations.json                # Backup (Excel takes priority)
│   ├── [LocationName]/               # Content folders
│   │   ├── 1-first-image.jpg        # Numbered images
│   │   ├── 2-second-image.jpg
│   │   ├── didactic.txt             # Optional info text
│   │   └── 1-first-image.txt        # Optional translation
│   └── World Map Extra Large.jpg     # Main map image
└── docs/
    ├── UPDATING_COORDINATES.md       # Detailed coordinate guide
    └── CONTENT_FEATURES.md           # Content display features
```

## Need Help?

- See `docs/guides/UPDATING_COORDINATES.md` for coordinate details
- See `docs/guides/CONTENT_FEATURES.md` for content setup
- Check application logs for any errors during startup
