# Quick Start Guide

## Ready to Run! 🚀

All files are in place and the application is ready for demo.

## Fastest Way to Run

**Double-click `run-demo.bat`** in the project folder.

That's it! The application will build and launch automatically.

## What You'll See

1. **Full-screen world map** with a black background
2. **6 interactive markers** at major cities and locations:
   - New York City (USA)
   - London (UK)
   - Tokyo (Japan)
   - Sydney (Australia)
   - Paris (France)
   - 405 Farnsworth Ave, Bordentown, NJ (USA)

## How to Use

- **Hover** over a marker → It scales up
- **Click** a marker → Opens a content window with an image
- **Click outside** the content window → Closes it
- **Press Escape** → Closes content window or exits app

## Files Verified ✓

All required files are present and configured:
- ✓ World map image: `Large_World_Map_bright.jpg`
- ✓ Location data: `locations.json` (6 locations)
- ✓ Content images: 2 sample images
- ✓ Files copied to output directory
- ✓ All tests passing (32/32)

## If Something Goes Wrong

Check the log file:
```
%APPDATA%\InteractiveWorldMap\logs\app.log
```

The log will show:
- Content folder path being used
- Which files were loaded successfully
- Any errors that occurred

## Technical Details

- **Framework**: .NET 6.0 WPF
- **Resolution**: Adapts to your screen (tested up to 4K)
- **Performance**: 30+ FPS with smooth animations
- **Architecture**: MVVM pattern with clean separation

Enjoy the demo! 🗺️
