# NEEDS REVIEW — Content Folder Reorganization Plan

> Status: **Draft, not started.** Revised 2026-07-19 per
> `tmp/content-folder-reorg-assessment-20260719.md`. Awaiting human review of the
> folder taxonomy (esp. hyphenated vs spaced names) and the shared-resolver approach
> before implementation.

## Goal

Restructure `Images&Content/` into three subfolders so that:

- **Static app assets** (map image, pin-part art, cluster stamp) are separated from
  user data.
- **Demo** and **Production** content sets are cleanly separated, each self-contained
  with its own coordinate Excel + `locations.json` + location subfolders.
- The app reads from **Production** when it is present and contains a coordinate source,
  otherwise falls back to **Demo**. Map/pin assets always come from `Assets/`.

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
├── Demo-Content/                   # sample dataset (the CURRENT data moves here)
│   ├── Coordinates for map.xlsx    # moved from repo root
│   ├── locations.json              # moved from Images&Content root
│   ├── manual-layouts.json         # moved from Images&Content root (per-set, see Q1)
│   ├── Kevin/                      # location subfolders (5 files)
│   ├── Test/                       # (11 files)
│   ├── Test2/                      # (2 files)
│   ├── Wang Chuang-wei/            # (3 files)
│   └── letter-product-pic.jpg      # stray sample (see Q3)
│
├── Production-Content/             # real deployment content; starts absent/empty
│   ├── Coordinates for map.xlsx
│   ├── locations.json
│   ├── manual-layouts.json
│   └── <location subfolders>/
│
└── Extras/                         # unused / unclear-purpose files; never deleted
    └── v4-460px-Write-a-Friendly-Letter-Step-4-Version-6.jpg
```

### Naming (resolved)

`"Demo-Content"` / `"Production-Content"` use hyphenated names (no spaces) to avoid
quoting friction in scripts, batch files, CLI args, and config paths. The
`ContentFileNames` constants are `DemoContentFolderName = "Demo-Content"` and
`ProductionContentFolderName = "Production-Content"`.

### Ambiguous / unused files (never deleted)

- `letter-product-pic.jpg` (and `v4-460px-Write-a-Friendly-Letter-Step-4-Version-6.jpg`):
  stray sample images. `locations.json` currently references `letter-product-pic.jpg`
  via `ContentFilePath`. **Move `letter-product-pic.jpg` into `Demo-Content/`** (Q3).
  `v4-460px...jpg` is unreferenced — **move it to `Extras/`** (do NOT delete).
- `README.md` in `Images&Content/`: rewrite to describe the new folder layout.
- **No files are deleted by this plan.** Anything whose purpose is unclear is relocated
  to `Extras/` rather than removed.
- **`locations.json` path fix:** because `letter-product-pic.jpg` moves into
  `Demo-Content/`, the single location entry whose `ContentFilePath` is
  `letter-product-pic.jpg` must be updated to reflect the new location (either a
  content-set-relative path or the file kept alongside the entry). Content resolution
  already keys off the active content set, so once both `locations.json` and the image
  live under `Demo-Content/` the relative resolution holds — but the JSON must be
  re-validated after the move.

---

## Code changes

### 1. `Models/ContentFileNames.cs`

Add constants (single source of truth, used by `ContentLoader`, `StartupValidator`,
and tools):

```csharp
public const string AssetsFolderName = "Assets";
public const string DemoContentFolderName = "Demo-Content";
public const string ProductionContentFolderName = "Production-Content";
public const string ExtrasFolderName = "Extras";
public const string ExcelCoordinateFileName = "Coordinates for map.xlsx";
```

### 2. New shared resolver — `Utilities/ContentSetResolver.cs`

**Both `ContentLoader` and `StartupValidator` need the same content-set selection
logic.** Extract it into a small pure helper so the rule lives in one place:

```csharp
public static class ContentSetResolver
{
    /// <returns>Production path if it contains a coordinate source, else Demo,
    /// else the bare content root (legacy fallback for tests/old deployments).</returns>
    public static string ResolveActiveContentSet(string contentRoot)
    {
        var production = Path.Combine(contentRoot, ContentFileNames.ProductionContentFolderName);
        if (HasCoordinateSource(production))
            return production;

        var demo = Path.Combine(contentRoot, ContentFileNames.DemoContentFolderName);
        if (HasCoordinateSource(demo))
            return demo;

        return contentRoot; // legacy-root fallback
    }

    /// <summary>A content set is valid only if it has an explicit coordinate source.</summary>
    public static bool HasCoordinateSource(string folder) =>
        Directory.Exists(folder) &&
        (File.Exists(Path.Combine(folder, ContentFileNames.ExcelCoordinateFileName)) ||
         File.Exists(Path.Combine(folder, ContentFileNames.LocationsJsonFileName)));
}
```

> **Design fix (assessment #6):** the "any directory" clause is **removed**. A set is
> valid only with an explicit coordinate source (Excel or JSON). This prevents an
> unrelated subfolder (`.git`, `__pycache__`, `Thumbs.db`) from making an empty
> production set look valid.

### 3. Asset resolution with legacy fallback — `Services/ContentLoader.cs`

**Design fix (assessment #1, #11):** the asset resolvers must check `Assets/` first,
then the bare `ContentFolderPath` as a legacy fallback, so existing tests/harness that
place the map at the temp-dir root keep working.

```csharp
private string ResolveAssetPath(string fileName)
{
    var underAssets = Path.Combine(ContentFolderPath, ContentFileNames.AssetsFolderName, fileName);
    if (File.Exists(underAssets))
        return underAssets;
    var legacy = Path.Combine(ContentFolderPath, fileName);   // legacy-root fallback
    return legacy;
}

public string GetWorldMapPath() => ResolveAssetPath(ContentFileNames.WorldMapFileName);
public string GetFullResolutionWorldMapPath() => ResolveAssetPath(ContentFileNames.FullResolutionWorldMapFileName);
public string ResolvePinPartPath(string relativePath) =>
    ResolveAssetPath(relativePath);   // pin parts are assets; relative to Assets/
```

- **`LoadLocationsAsync`** (assessment #8): replace `BaseDirectory` Excel resolution with
  content-set resolution. Excel path =
  `ExcelCoordinateFilePath ?? Path.Combine(ContentSetResolver.ResolveActiveContentSet(ContentFolderPath), ExcelCoordinateFileName)`;
  `locations.json` path =
  `Path.Combine(ContentSetResolver.ResolveActiveContentSet(ContentFolderPath), LocationsJsonFileName)`.
  This also fixes the *existing* inconsistency where Excel used `BaseDirectory` but JSON
  used `ContentFolderPath`.
- **Location content resolution** (`LoadLocationContentAsync`,
  `LoadAllLocationImagesWithTranslationsAsync`, `ResolveLocationImageFiles`,
  `LoadDidacticTextAsync`): replace `Path.Combine(ContentFolderPath, location.Name)`
  with `Path.Combine(ContentSetResolver.ResolveActiveContentSet(ContentFolderPath), location.Name)`.
- **`ValidateContentFolder`**: require world map (via `ResolveAssetPath`); require the
  active content set to contain a coordinate source.
- **Log which set was selected** at startup (Demo vs Production vs legacy fallback).

### 4. `IContentLoader` exposes the active set (assessment #9 — now required)

```csharp
public enum ContentSetMode { Production, Demo, LegacyRoot }
string ActiveContentSetPath { get; }   // or ContentSetMode ActiveSet { get; }
```

`ContentLoader` sets `ActiveContentSetPath` once (lazily, after first resolve) so
MainWindow / status bars / debug overlays can display and react to the choice. Not
optional.

### 5. `Services/StartupValidator.cs` — previously omitted (assessment #2, #12)

Now uses `ContentFileNames` constants and the shared resolver:

| Line | Current | Change to |
|------|---------|-----------|
| 50 | `Path.Combine(_contentFolderPath, WorldMapFileName)` | `Path.Combine(_contentFolderPath, AssetsFolderName, WorldMapFileName)` (use the same `ResolveAssetPath` idea, or check `Assets/` then root) |
| 62 | `Path.Combine(_contentFolderPath, LocationsJsonFileName)` | `Path.Combine(ContentSetResolver.ResolveActiveContentSet(_contentFolderPath), LocationsJsonFileName)` |
| 76 | `Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Coordinates for map.xlsx")` | `Path.Combine(ContentSetResolver.ResolveActiveContentSet(_contentFolderPath), ExcelCoordinateFileName)` |

Warns (not errors) when the chosen set lacks a coordinate source, consistent with
current behavior.

### 6. `Models/PinPartConfig.cs` comment fix (assessment #10)

`PinPartConfig.cs:11` says *"Paths are relative to the Images&Content folder"*. After
the reorg the parts resolve relative to `Assets/`. **Update the comment** (and any
`visual-config` docs) to say *"relative to the `Assets/` subfolder of the content root"*.
Defaults (`Pins_v2/parts`, `pin_part_geometry.json`) stay valid as Assets-relative.

### 7. Downstream tools / config (assessment #4, #13, #15)

- `Tools/ManualLayoutSeedGenerator/Program.cs` (lines 106-107): change the default
  constants explicitly —
  - `MapImagePath` → `Path.Combine("Images&Content", "Assets", "World Map Extra Large.jpg")`
  - `OutputPath` → `Path.Combine("Images&Content", "Demo-Content", "manual-layouts.json")`
    (or active set).
- `Tools/MapResamplerComparison/Program.cs:12`: source →
  `Path.Combine(root, "Images&Content", "Assets", "World Map 1976.jpg")`.
- `Tools/PinDebugger/PinDebuggerContext.cs:27`: default parts dir →
  `Path.Combine("Images&Content", "Assets", "Pins_v2", "parts")`.
- `Utilities/UpdateLocationsFromExcel.cs:23`: read `Coordinates for map.xlsx` and write
  `locations.json` **inside the resolved active content set** (do not hardcode
  `BaseDirectory`). Use `ContentSetResolver` to pick the target set.
- `Models/VisualConfig.cs` `LayoutStoragePath` (currently
  `"Images&Content/manual-layouts.json"`) and `MainWindow.ResolveLayoutStoragePath`
  (xaml.cs:418-444): repoint to the active content set. See complexity note below.

### 8. `MainWindow.ResolveLayoutStoragePath` complexity (assessment #5)

`manual-layouts.json` is per content set (Q1). The current resolver seeds from a
bundled path into `AppData` and resolves relative paths against `BaseDirectory`. After
the reorg the seed/copy source moves under `Demo-Content/` (or active set). The plan
acknowledges this needs care:

- Seed source = `Path.Combine(activeContentSet, "manual-layouts.json")`.
- Per-set copy logic: when production is active, seed/copy from the production set, not
  demo. Confirm whether user layouts should be shared across sets or scoped per set;
  default to **scoped per active set** (copy lives under each set's folder / AppData key
  namespaced by set). Resolve during implementation; note the decision in the exec plan
  progress.

---

## Tests & harness (assessment #1, #11, #12)

- **No mass breakage expected** thanks to the asset legacy fallback (#3) and the
  legacy-root content fallback (#2). Existing `ContentLoaderTests.CreateContentFolderWithMap()`
  (map at temp-dir root) and `StartupValidationHarnessTests` continue to work.
- Still update:
  - `Tests/StartupValidationHarnessTests.cs`: optionally assert map can live under
    `Assets/`; current root-based checks remain valid via fallback.
  - `Tests/ContentLoaderTests.cs`, `PinPartVariantCatalogTests.cs`, `CompositePin*Tests.cs`:
    keep passing via fallback; add explicit `Assets/`-rooted fixtures for the new tests.
  - `Tests/Architecture/GoldenPrincipleTests.cs`: unaffected (Views still must not build
    `Images&Content` paths — routing stays through `ContentLoader`).
- **New tests** for `ContentSetResolver` / `ContentLoader.ActiveContentSetPath`:
  - Production present + has Excel/JSON → Production.
  - Production absent / empty → Demo.
  - Neither present → legacy-root fallback.
  - Asset resolver: `Assets/` wins; bare root used when `Assets/` absent.
- `scripts/verify_manual_layout_seeds.ps1` and any `scripts/` / `docs/guides/SETUP_GUIDE.md`
  literal referencing `Coordinates for map.xlsx` at repo root must point into the
  content set.

---

## Open questions — resolved per assessment

| # | Question | Resolution |
|---|----------|------------|
| 1 | `manual-layouts.json` placement | **Per-set is correct** (user-authored, part of the content set). Reconcile seed/copy logic (§8). |
| 2 | Empty-production definition | **Require a coordinate source file** (Excel or JSON). Drop "any directory" clause (#6). |
| 3 | Stray images | **Move `letter-product-pic.jpg` to `Demo-Content/`** (it is referenced by `locations.json`); move `v4-460px...jpg` to `Extras/` (never delete). |
| 4 | Force mode | **Defer.** Auto-fallback serves v1; add `VisualConfig.ContentSetMode` later when dual-set deployment is real. |

---

## Verification

- `dotnet build InteractiveWorldMap.sln`
- `dotnet test Tests/InteractiveWorldMap.Tests.csproj`
- `.\scripts\verify.ps1` (Windows) / `./scripts/verify.sh` (macOS harness-only)
- Manual: app launches and loads **Demo** when `Production-Content/` absent; switches to
  **Production** when a valid `Production-Content/` (with Excel or JSON) is added;
  assets load from `Assets/`; legacy flat folder still works via fallback.
- Update `CHANGELOG.md` `[Unreleased]` and this plan's status on completion.
