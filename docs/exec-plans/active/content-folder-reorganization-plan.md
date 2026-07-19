# NEEDS REVIEW — Content Folder Reorganization Plan

> Status: **Draft, not started.** Awaiting human review of the folder taxonomy, the
> "valid production set" definition, and whether `manual-layouts.json` belongs in the
> content set. Do not implement until approved.

## Goal

Restructure `Images&Content/` into three subfolders so that:

- **Static app assets** (map image, pin-part art, cluster stamp) are separated from
  user data.
- **Demo** and **Production** content sets are cleanly separated, each self-contained
  with its own coordinate Excel + `locations.json` + location subfolders.
- The app reads from **Production** when it is present and non-empty, otherwise
  falls back to **Demo**. Map/pin assets always come from `Assets/`.

The current data (location subfolders, `locations.json`, `Coordinates for map.xlsx`)
becomes the **Demo** set.

---

## Proposed layout

```
Images&Content/
├── Assets/                         # static app assets (ships with the app, never "content")
│   ├── World Map Extra Large.jpg   # primary map (WorldMapFileName)
│   ├── World Map 1976.jpg          # full-res zoom source (FullResolutionWorldMapFileName)
│   ├── World Map Large.jpg
│   ├── World Map Extra Large copy.jpg
│   ├── Large_World_Map_bright.jpg
│   ├── stamp_demo.png              # cluster marker stamp (ClusterStampFileName)
│   ├── 1612...Vector pushpins...jpg
│   ├── 1612...Vector pushpins...zip
│   ├── Pins/                       # legacy pin art (65 files)
│   └── Pins_v2/                    # pin-part art (1040 files; parts/ resolved from here)
│
├── Demo Content/                   # sample dataset (the CURRENT data moves here)
│   ├── Coordinates for map.xlsx    # moved from repo root
│   ├── locations.json              # moved from Images&Content root
│   ├── manual-layouts.json         # moved from Images&Content root (see Open Question)
│   ├── Kevin/                      # location subfolders (5 files)
│   ├── Test/                       # (11 files)
│   ├── Test2/                      # (2 files)
│   └── Wang Chuang-wei/            # (3 files)
│
└── Production Content/             # real deployment content; starts absent/empty
    ├── Coordinates for map.xlsx
    ├── locations.json
    ├── manual-layouts.json
    └── <location subfolders>/
```

### Ambiguous files (flag for reviewer)

- `letter-product-pic.jpg`, `v4-460px-Write-a-Friendly-Letter-Step-4-Version-6.jpg`:
  appear to be stray sample images, not app assets. Proposal: move into
  `Demo Content/` (or delete if unused). Confirm with human.
- `README.md` in `Images&Content/`: rewrite to describe the new three-folder layout.

---

## Code changes

### 1. `Models/ContentFileNames.cs`

Add constants (single source of truth):

```csharp
public const string AssetsFolderName = "Assets";
public const string DemoContentFolderName = "Demo Content";
public const string ProductionContentFolderName = "Production Content";
public const string ExcelCoordinateFileName = "Coordinates for map.xlsx";
```

### 2. `Services/ContentLoader.cs` — content-set selection

Add a method that picks the active content set:

```csharp
private string GetActiveContentSetPath()
{
    var production = Path.Combine(ContentFolderPath, ContentFileNames.ProductionContentFolderName);
    if (IsValidContentSet(production))
        return production;

    var demo = Path.Combine(ContentFolderPath, ContentFileNames.DemoContentFolderName);
    if (IsValidContentSet(demo))
        return demo;

    // Back-compat fallback for tests / old deployments: bare content root.
    return ContentFolderPath;
}

private bool IsValidContentSet(string folder) =>
    Directory.Exists(folder) &&
    (File.Exists(Path.Combine(folder, ContentFileNames.ExcelCoordinateFileName)) ||
     File.Exists(Path.Combine(folder, ContentFileNames.LocationsJsonFileName)) ||
     Directory.EnumerateDirectories(folder).Any());
```

- **`LoadLocationsAsync`**: Excel path = `ExcelCoordinateFilePath
  ?? Path.Combine(GetActiveContentSetPath(), ExcelCoordinateFileName)`;
  `locations.json` path = `Path.Combine(GetActiveContentSetPath(), LocationsJsonFileName)`.
- **Location content resolution** (`LoadLocationContentAsync`,
  `LoadAllLocationImagesWithTranslationsAsync`, `ResolveLocationImageFiles`,
  `LoadDidacticTextAsync`): replace `Path.Combine(ContentFolderPath, location.Name)`
  with `Path.Combine(GetActiveContentSetPath(), location.Name)`.
- **`GetWorldMapPath` / `GetFullResolutionWorldMapPath`**: resolve under `Assets/`
  (e.g. `Path.Combine(ContentFolderPath, AssetsFolderName, WorldMapFileName)`).
- **`ResolvePinPartPath`**: resolve under `Assets/` (pin parts are assets, not content).
  This keeps `PinPartConfig` defaults (`Pins_v2/parts`, `pin_part_geometry.json`)
  valid as relative-to-Assets paths.
- **`ValidateContentFolder`**: require world map under `Assets/`; require the active
  content set to contain a coordinate source.
- Log which set was selected at startup (Demo vs Production vs fallback).

### 3. `Services/IContentLoader.cs`

Expose selection state if needed by the UI/status (e.g. `ContentSetMode ActiveSet`
enum: `Production | Demo | LegacyRoot`). Optional.

### 4. Optional config override — `Models/VisualConfig.cs`

Add `ContentSetMode` (`Auto | Demo | Production`) so a deployment can force a set.
Default `Auto` (production-if-valid else demo). `ContentLoader.GetActiveContentSetPath`
honors the override before applying the auto rule.

### 5. Downstream tools / config referencing old paths

- `Tools/ManualLayoutSeedGenerator/Program.cs`: map path → `Assets/World Map Extra Large.jpg`;
  output → `Demo Content/manual-layouts.json` (or active set).
- `Tools/MapResamplerComparison/Program.cs`: source → `Assets/World Map 1976.jpg`.
- `Tools/PinDebugger/PinDebuggerContext.cs`: default parts dir → `Assets/Pins_v2/parts`.
- `Utilities/UpdateLocationsFromExcel.cs`: read `Coordinates for map.xlsx` and write
  `locations.json` inside the active content set, not the bare `Images&Content` root.
- `Models/VisualConfig.cs` `LayoutStoragePath`: repoint to active content set
  (see Open Question).
- `ManualLayoutPinMarker` / manual-layout load/save: resolve `manual-layouts.json`
  via the active content set.

### 6. Tests & harness (must update)

- `Tests/StartupValidationHarnessTests.cs`: now expects world map under `Assets/` and
  content under `Demo Content/`; update `contentPath` expectations.
- `Tests/ContentLoaderTests.cs`, `PinPartVariantCatalogTests.cs`,
  `CompositePin*Tests.cs`: temp-dir fixtures place `Pins_v2/parts` under `Assets/`
  and `locations.json` under `Demo Content/` (or rely on the legacy-root fallback).
- `Tests/Architecture/GoldenPrincipleTests.cs`: unaffected (Views still must not build
  `Images&Content` paths — keep routing through `ContentLoader`).
- Add new tests for `GetActiveContentSetPath` / `IsValidContentSet`:
  - Production present + valid → Production.
  - Production empty/absent → Demo.
  - Neither present → legacy root fallback.
  - Force Demo / Force Production via `VisualConfig` override.
- `scripts/verify_manual_layout_seeds.ps1` and any path literals in `scripts/` /
  `docs/guides/SETUP_GUIDE.md` referencing `Coordinates for map.xlsx` at repo root.

---

## Open questions for reviewer

1. **`manual-layouts.json` placement** — is it per-content-set user data (move into
   `Demo Content/` / `Production Content/`) or app-level state (keep at
   `Images&Content/` root)? Plan currently assumes per-set.
2. **"Empty production" definition** — plan uses: contains the Excel, `locations.json`,
   or any location subfolder. Acceptable?
3. **Stray sample images** (`letter-product-pic.jpg`, `v4-460px...jpg`) — move to Demo
   or delete?
4. **Force mode** — is `VisualConfig.ContentSetMode` wanted, or is auto-fallback
   (production-if-valid else demo) sufficient for v1?

---

## Verification

- `dotnet build InteractiveWorldMap.sln`
- `dotnet test Tests/InteractiveWorldMap.Tests.csproj`
- `.\scripts\verify.ps1` (Windows) / `./scripts/verify.sh` (macOS harness-only)
- Manual: confirm app launches, loads Demo when `Production Content/` absent, and
  switches to Production when a valid `Production Content/` is added.
- Update `CHANGELOG.md` `[Unreleased]` and this plan's status on completion.
