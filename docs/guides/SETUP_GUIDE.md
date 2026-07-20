# Setup Guide - New Pixel-Based System

## .NET SDK (build and test)

The app targets **`net6.0-windows`**. Repo root **`global.json`** pins the **.NET 6 SDK** so `dotnet build` / `dotnet test` use the same toolchain as GitHub Actions CI.

| Situation | What to do |
|-----------|------------|
| Only .NET 8/10 installed | Also install [.NET 6 SDK](https://dotnet.microsoft.com/download/dotnet/6.0) — side-by-side with newer SDKs is supported |
| `dotnet --version` shows 10.x in this repo | Install .NET 6 SDK; `global.json` will select 6.0.x automatically |
| Tests fail: missing `Microsoft.NETCore.App 6.0.x` | Install .NET 6 SDK or 6.0 runtime (SDK is preferred) |
| Full verification | `.\scripts\verify.ps1` (Windows) or `./scripts/verify.sh` (macOS/Linux harness checks) |

```powershell
dotnet --list-sdks
dotnet --list-runtimes
dotnet build InteractiveWorldMap.sln
dotnet test Tests/InteractiveWorldMap.Tests.csproj
```

Future: consider upgrading to **.NET 8 LTS** — tracked in [TO_DO.md](../TO_DO.md).

## Python (harness and optional tooling)

The app is .NET/WPF; Python is used only for CI harness checks and optional composite-pin asset tooling under `scripts/`.

| Task | Python to use | Dependencies |
|------|---------------|--------------|
| `verify.ps1` / `verify.sh` harness | Windows: `py -3`; macOS/Linux: `python3` | None (stdlib only) |
| Composite-pin asset tooling (`split_pin_parts.py`, `create_shaft_asset_variants.py`) | `scripts/venv/` | `scripts/requirements.txt` |

**Create or refresh the venv** (not in git — recreate on each machine):

```powershell
py -3 -m venv scripts\venv
.\scripts\venv\Scripts\Activate.ps1
pip install -r scripts\requirements.txt
```

```bash
python3 -m venv scripts/venv
source scripts/venv/bin/activate
pip install -r scripts/requirements.txt
```

Script catalog: [scripts/README.md](../../scripts/README.md).

## Content prerequisites

1. **Excel File**: `Images&Content/Demo-Content/Coordinates for map.xlsx` (or under `Production-Content/` when that set is active)
   - Column A: Name (person's name)
   - Column B: PixelX (X coordinate on map)
   - Column C: PixelY (Y coordinate on map)
   - Column D: Address (optional)

2. **Map Image**: `Images&Content/Assets/World Map Extra Large.jpg` (canonical name in `Models/ContentFileNames.cs`)

Content-set selection (Demo vs Production vs legacy) is documented in [CONTENT_SETS.md](CONTENT_SETS.md).

## Folder Structure Setup

Create the following structure in `Images&Content/`:

```
Images&Content/
├── Assets/
│   └── World Map Extra Large.jpg
├── Demo-Content/
│   ├── Coordinates for map.xlsx
│   ├── locations.json
│   ├── manual-layouts.json
│   ├── New York City/
│   │   ├── photo1.jpg
│   │   ├── photo2.jpg
│   │   └── photo3.jpg
│   ├── London/
│   │   ├── photo1.jpg
│   │   └── photo2.jpg
│   └── Tokyo/
│       └── photo1.jpg
└── ... (one location folder per Excel Name under the active content set)
```

**Important**: 
- Folder names MUST match the "Name" column in the Excel file exactly (case-sensitive)
- Each folder should contain at least one image file (.jpg, .png, or .jpeg)
- The first image found in the folder will be displayed

## Excel File Format

Example `Images&Content/Demo-Content/Coordinates for map.xlsx`:

| Name | PixelX | PixelY | Address |
|------|--------|--------|---------|
| New York City | 4920 | 5153 | 405 Farnsworth Ave, Bordentown, NJ |
| London | 7200 | 3800 | 10 Downing Street |
| Tokyo | 12500 | 4200 | 1-1 Kasumigaseki, Chiyoda Ward |

## Troubleshooting

### No markers appear
1. Check the log file: `%APPDATA%\InteractiveWorldMap\logs\app.log`
2. Verify Excel file is under the active content set (`Demo-Content/` or `Production-Content/`)
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
