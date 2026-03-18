# Interactive World Map

A Windows desktop application that displays a full-screen, high-resolution world map with interactive location markers. Users can click on geographic locations to view detailed content in popup subwindows.

## How to Use

Use Excel file with labeled locations/people and add pixel coordinates.

Name subfolders in Images&Content to match.

Put content in there.

Currently if an image and a txt file have the same name and are in the same folder, it will register as a translation. It respects line breaks in the txt file. Could probably enrich formatting if necessary.

Run run-demo.bat in main directory - eventually can make an exe file.

Set parameters in visual-config.json - now includes a manal layout mode where you can drag markers for extended lines and save layout (keyed to zoom level, pixel threshold, other parameters).

## System Requirements

- **Operating System**: Windows 10 or later
- **.NET SDK**: .NET 6.0 or later
- **Framework**: Windows Presentation Foundation (WPF)

## Installation

### Prerequisites

1. Install .NET 6.0 SDK or later from: https://dotnet.microsoft.com/download
2. Verify installation by running: `dotnet --version`

### Building the Application

```bash
# Restore NuGet packages
dotnet restore

# Build the project
dotnet build

# Run the application
dotnet run
```

## Project Structure

```
InteractiveWorldMap/
├── Models/              # Data model classes
├── ViewModels/          # MVVM ViewModel classes
├── Views/               # WPF UserControls and custom controls
├── Services/            # Service classes (logging, content loading)
│   ├── ILogger.cs       # Logger interface
│   └── FileLogger.cs    # File-based logger implementation
├── Utilities/           # Utility classes and helpers
├── Images&Content/      # Content folder with map and location data
│   ├── world_map.png    # Main world map image (required)
│   ├── locations.json   # Location configuration (required)
│   └── README.md        # Content folder documentation
├── App.xaml             # Application entry point
├── MainWindow.xaml      # Main window UI
└── InteractiveWorldMap.csproj  # Project file
```

## Dependencies

- **Newtonsoft.Json** (v13.0.3): JSON parsing for location data

## Logging

Application logs are written to:
- **Location**: `%APPDATA%\InteractiveWorldMap\logs\app.log`
- **Levels**: ERROR, WARNING, INFO
- **Console**: Logs are also output to console during development

To view logs:
```bash
# Windows Command Prompt
type "%APPDATA%\InteractiveWorldMap\logs\app.log"

# PowerShell
Get-Content "$env:APPDATA\InteractiveWorldMap\logs\app.log" | Select-Object -Last 50
```

## Debugging

For troubleshooting issues:
1. Check the log file at `%APPDATA%\InteractiveWorldMap\logs\app.log`
2. Console output is available when running from terminal
3. Enable debug logging in `visual-config.json`:
   ```json
   {
     "Debug": {
       "LogRadialExtensionCalculation": true
     }
   }
   ```

## Content Folder Structure

The `Images&Content` folder must contain:

1. **world_map.png** (or .jpg): High-resolution world map image
2. **locations.json**: Location configuration file
3. Content files referenced in locations.json (images or text files)

See `Images&Content/README.md` for detailed format specifications.

## Development Status

This project is currently in development. Task 1 (project structure and dependencies) has been completed.

### Completed Tasks
- ✅ WPF project setup targeting .NET 6.0+
- ✅ NuGet package configuration (Newtonsoft.Json)
- ✅ Folder structure creation (Models, ViewModels, Views, Services, Utilities)
- ✅ Logging infrastructure (ILogger interface and FileLogger implementation)
- ✅ Content folder structure with placeholder files

### Next Steps
- Implement core data models and validation
- Implement coordinate mapping system
- Implement content loading system
- Build UI components (MapDisplay, LocationMarker, ContentSubwindow)

## Architecture

The application follows a layered architecture:

- **Presentation Layer**: Full-screen rendering, UI components, visual feedback
- **Interaction Layer**: User input handling, click detection, event routing
- **Content Layer**: Map and location content management
- **Coordinate System**: Geographic to screen coordinate translation

## Technology Stack

- **Framework**: WPF with .NET 6.0+
- **Language**: C# 10.0+
- **Graphics**: WPF Image controls with hardware acceleration
- **Animations**: WPF Storyboard and DoubleAnimation
- **Pattern**: MVVM (Model-View-ViewModel)

## License

[License information to be added]

## Contributing

[Contribution guidelines to be added]
