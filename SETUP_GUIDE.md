# Setup Guide - New Pixel-Based System

## Prerequisites

1. **Excel File**: `Coordinates for map.xlsx` in the project root
   - Column A: Name (person's name)
   - Column B: PixelX (X coordinate on map)
   - Column C: PixelY (Y coordinate on map)
   - Column D: Address (optional)

2. **Map Image**: `Images&Content/World Map 1976.jpg` (16397 x 11085 pixels)

## Folder Structure Setup

Create the following structure in `Images&Content/`:

```
Images&Content/
├── World Map 1976.jpg
├── New York City/
│   ├── photo1.jpg
│   ├── photo2.jpg
│   └── photo3.jpg
├── London/
│   ├── photo1.jpg
│   └── photo2.jpg
├── Tokyo/
│   └── photo1.jpg
└── ... (one folder per location)
```

**Important**: 
- Folder names MUST match the "Name" column in the Excel file exactly (case-sensitive)
- Each folder should contain at least one image file (.jpg, .png, or .jpeg)
- The first image found in the folder will be displayed

## Excel File Format

Example `Coordinates for map.xlsx`:

| Name | PixelX | PixelY | Address |
|------|--------|--------|---------|
| New York City | 4920 | 5153 | 405 Farnsworth Ave, Bordentown, NJ |
| London | 7200 | 3800 | 10 Downing Street |
| Tokyo | 12500 | 4200 | 1-1 Kasumigaseki, Chiyoda Ward |

## Troubleshooting

### No markers appear
1. Check the log file: `%APPDATA%\InteractiveWorldMap\logs\app.log`
2. Verify Excel file is in the project root
3. Verify folder names match Excel "Name" column exactly
4. Ensure images exist in the location folders

### "Content not available" message
1. Check that a folder exists with the location name
2. Verify the folder contains image files (.jpg, .png, .jpeg)
3. Check file permissions

### Excel parsing errors
1. Ensure Excel file has headers in row 1
2. Verify data starts in row 2
3. Check that PixelX and PixelY columns contain numbers
4. Look for error details in the log file

## Running the Application

```bash
# Build
dotnet build

# Run
dotnet run --project InteractiveWorldMap.csproj

# Or use the batch file
.\run-demo.bat
```

## Debugging

To see detailed logs:
1. Run the application
2. Open the log file: `%APPDATA%\InteractiveWorldMap\logs\app.log`
3. Look for entries marked with ✓ (success) or ✗ (error)

Example log output:
```
=== Starting Environment Validation ===
Base directory: C:\path\to\project\bin\Debug\net6.0-windows\
Content folder path: C:\path\to\project\bin\Debug\net6.0-windows\Images&Content
✓ Content folder found: ...
✓ World map image found: ...
✓ Excel file found: ...
Reading Excel file: ...
Loaded 5 shared strings
Found 6 rows in worksheet
Skipping header row
Parsed location: New York City at (4920, 5153)
...
```
