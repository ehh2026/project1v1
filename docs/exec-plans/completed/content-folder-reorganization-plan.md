# Content Folder Reorganization Plan

> Status: **Ready for implementation.** Revised 2026-07-19 (fourth review:
> `tmp/content-folder-reorg-assessment-20260719-161530.md`; prior reviews:
> `tmp/content-folder-reorg-assessment-20260719.md`,
> `tmp/content-folder-reorg-assessment-20260719-134709.md`,
> `tmp/content-folder-reorg-assessment-20260719-151747.md`). All blockers and open
> design decisions landed: per-set AppData namespacing (§7), cluster cache namespacing
> (§8), `git mv` order (§Migration), warn-vs-error contract (§3), new-tests file
> location (§"Tests & harness"), stamp / asset routing (§3), geometry hash path fix
> (§7, pre-existing bug), `PinPartVariantCatalog` routing (§9, option b),
> `ContentSetResolver` as non-static with structured result (§2), `ActiveContentSetPath`
> contract (§3), and composition root wiring (§11). Awaiting final human sign-off
> before implementation.

## Goal

Restructure `Images&Content/` into subfolders so that:

- **Static app assets** (map image, pin-part art, cluster stamp) are separated from
  user data.
- **Demo** and **Production** content sets are cleanly separated, each self-contained
  with its own coordinate Excel + `locations.json` + location subfolders + manual
  layouts.
- The app reads from **Production** when it is present and contains a coordinate source,
  otherwise falls back to **Demo**. Assets always come from `Assets/`. The legacy flat
  layout is supported only as a **developer/test convenience** (see D5) — production
  deployments adopt the new structure wholesale.

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
│   └── Pins_v2/                    # pin-part art (resolved from here)
│       ├── parts/                  # 41 part files + pin_part_geometry.json
│       ├── pin_01.png … pin_12.png # 12 standalone legacy pin PNGs
│       ├── composite_preview.png, preview_on_beige.png, preview_on_blue.png
│       └── compare_A_conservative/ # (plus compare_B_moderate, C_aggressive, D_hybrid)
│           compare_B_moderate/     #   from the pin-extraction pipeline (scripts/split_pin_parts.py)
│           compare_C_aggressive/
│           compare_D_hybrid/
│
├── Demo-Content/                   # sample dataset (the CURRENT data moves here)
│   ├── Coordinates for map.xlsx    # moved from repo root
│   ├── locations.json              # moved from Images&Content root
│   ├── manual-layouts.json         # moved from Images&Content root (per-set, see C3)
│   ├── Kevin/                      # location subfolders (5 files)
│   ├── Test/                       # (11 files)
│   ├── Test2/                      # (2 files)
│   ├── Wang Chuang-wei/            # (3 files)
│   └── letter-product-pic.jpg      # referenced by locations.json (see A4/C8)
│
├── Production-Content/             # real deployment content; starts absent/empty
│   ├── Coordinates for map.xlsx
│   ├── locations.json
│   ├── manual-layouts.json
│   └── <location subfolders>/
│
├── Extras/                         # unused / unclear-purpose files; never deleted
│   ├── Pins/                       # legacy pin folder, unreferenced by code (C1)
│   └── v4-460px-Write-a-Friendly-Letter-Step-4-Version-6.jpg
└── README.md                       # rewritten to describe the new layout
```

### Naming (resolved)

`Demo-Content` / `Production-Content` use hyphenated names (no spaces) to avoid quoting
friction. Constants: `DemoContentFolderName = "Demo-Content"`,
`ProductionContentFolderName = "Production-Content"`, `AssetsFolderName = "Assets"`,
`ExtrasFolderName = "Extras"`.

### Files that are never deleted

Nothing in this plan is deleted. Unused/unclear files (`Pins/` legacy, `v4-460px...jpg`)
move to `Extras/`. `letter-product-pic.jpg` is **referenced** (by `locations.json`
`ContentFilePath`, by `docs/guides/CONTENT_FEATURES.md` and `DEMO_INSTRUCTIONS.md`, and
by `ExcelCoordinateReaderTests` fixture naming) — it moves into `Demo-Content/`.

### Known pre-existing bug the reorg exposes (A4 / C8)

`Utilities/ExcelCoordinateReader.cs` writes `ImageFileNames` such as
`1-letter-product-pic.jpg` / `2-second-image.jpg` from the Excel, but no file with
those exact names exists on disk today (the on-disk file is `letter-product-pic.jpg`
under the repo root, and `Test/` holds `3-letter-product-pic.jpg`,
`8-letter-product-pic - Copy.jpg`). The JSON path uses `ContentFilePath` instead. The
reorg does not create this bug, but **moving content makes the dangling references
visible** (warnings / missing images at runtime). Required migration step (see §Migration):
verify every location's referenced image files exist under its content-set folder, and
reconcile the Excel image columns (or the on-disk files) so references resolve.

### Known pre-existing bug: geometry hash always `"geometry-missing"` (third review)

Both `MainWindow.CompositePins.partial.cs:144-145` and
`MainWindow.LayoutEditor.partial.cs:640-642` build the geometry file path as
`BaseDirectory + "Pins_v2/parts/pin_part_geometry.json"`, but the file lives at
`BaseDirectory + "Images&Content/Pins_v2/parts/pin_part_geometry.json"`.
`CompositePinLayoutContentHasher.ComputeGeometryHash` (line 43-44) returns
`"geometry-missing"` when the file doesn't exist. The composite-pin plan cache has
been working with a constant placeholder hash — it never invalidates on geometry
changes. **Fix both paths as part of this reorg** (§7).

---

## Migration steps (ordered)

1. **Land the code changes first** (sections 1-9 below) on a branch — the legacy-root
   fallback in §2 and the asset legacy fallback in §3 mean the code is fully
   backward-compatible with the **existing** flat layout. Build and run tests on the
   pre-move tree to confirm green.
2. **`git mv`** (not plain `mv`) every file so history is preserved — `Images&Content/`
   subfolders, `Pins_v2/`, `locations.json`, `Coordinates for map.xlsx`,
   `manual-layouts.json`.
3. Create `Assets/`, `Demo-Content/`, `Production-Content/`, `Extras/`.
4. Move assets into `Assets/` (map images, stamp, pushpin jpg/zip, `Pins_v2/`).
5. Move current location subfolders + `locations.json` + `Coordinates for map.xlsx` +
   `manual-layouts.json` into `Demo-Content/`; copy `letter-product-pic.jpg` there.
6. Move unreferenced `Pins/` and `v4-460px...jpg` into `Extras/`.
7. **Reference reconciliation** (A4/C8): run a check that, for each location in the
   active set, every `ContentFilePath` / `ImageFileNames` entry resolves to a real file
   under that location's folder; fix dangling references (rename files or trim Excel
   columns). `locations.json`'s `letter-product-pic.jpg` entry must resolve after the move.
8. Rewrite `Images&Content/README.md`.
9. Run the full verification gate (§"Verification" below). Confirm the **post-move**
   build output contains `bin\Debug\net6.0-windows\Images&Content\Assets\...` and
   `bin\Debug\net6.0-windows\Images&Content\Demo-Content\Coordinates for map.xlsx`.

---

## Code changes

### 1. `Models/ContentFileNames.cs`

```csharp
public const string AssetsFolderName = "Assets";
public const string DemoContentFolderName = "Demo-Content";
public const string ProductionContentFolderName = "Production-Content";
public const string ExtrasFolderName = "Extras";
public const string ExcelCoordinateFileName = "Coordinates for map.xlsx";
```

### 2. New shared resolver — `Utilities/ContentSetResolver.cs`

Single source of truth for selection, used by `ContentLoader` **and** `StartupValidator`.
Implemented as a non-static class implementing `IContentSetResolver` so consumers can
mock it in tests (static classes with filesystem I/O are fragile to unit test):

```csharp
public enum ContentSetKind { Production, Demo, Legacy }

public static class ContentSetKindExtensions
{
    public static string ToSuffix(this ContentSetKind kind) => kind switch
    {
        ContentSetKind.Production => "production",
        ContentSetKind.Demo => "demo",
        ContentSetKind.Legacy => "legacy",
        _ => throw new ArgumentOutOfRangeException(nameof(kind))
    };
}

public record ContentSetResolution(string Path, ContentSetKind Kind);

public interface IContentSetResolver
{
    ContentSetResolution ResolveActiveContentSet(string contentRoot);
    bool HasCoordinateSource(string folder);
}

public class ContentSetResolver : IContentSetResolver
{
    public ContentSetResolution ResolveActiveContentSet(string contentRoot)
    {
        var production = Path.Combine(contentRoot, ContentFileNames.ProductionContentFolderName);
        if (HasCoordinateSource(production))
            return new ContentSetResolution(production, ContentSetKind.Production);

        var demo = Path.Combine(contentRoot, ContentFileNames.DemoContentFolderName);
        if (HasCoordinateSource(demo))
            return new ContentSetResolution(demo, ContentSetKind.Demo);

        return new ContentSetResolution(contentRoot, ContentSetKind.Legacy);
    }

    public bool HasCoordinateSource(string folder) =>
        Directory.Exists(folder) &&
        (File.Exists(Path.Combine(folder, ContentFileNames.ExcelCoordinateFileName)) ||
         File.Exists(Path.Combine(folder, ContentFileNames.LocationsJsonFileName)));
}
```

A set is valid **only with an explicit coordinate source** (no loose "any directory"
clause) so an unrelated subfolder can't fake a valid production set. Note: validity is
defined by file **presence**, not file **content** — an empty or malformed
`locations.json` still counts as a coordinate source (the loader will fail later with a
parse error, which is the correct failure mode).

The structured `ContentSetResolution` result makes the set kind explicit so callers
(e.g. `ContentLoader`, `MainWindow.ResolveLayoutStoragePath`) can derive the AppData
suffix without re-parsing the path.

### 3. Asset resolution with legacy fallback — `Services/ContentLoader.cs`

**Constructor change (fourth review):** add `IContentSetResolver` parameter and reorder
so `ContentFolderPath` is set before `ClusterCache` (which needs the set suffix):

```csharp
public ContentLoader(ILogger logger, IContentSetResolver contentSetResolver)
{
    _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    _contentSetResolver = contentSetResolver ?? throw new ArgumentNullException(nameof(contentSetResolver));
    _contentCache = new Dictionary<string, BitmapImage>();
    _clusterer = new LocationClusterer();
    ContentFolderPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ContentFileNames.ContentFolderName);
    var activeSet = _contentSetResolver.ResolveActiveContentSet(ContentFolderPath);
    _clusterCache = new ClusterCache(logger, activeSet.Kind.ToSuffix());
    _logger.LogInfo($"ContentLoader initialized with path: {ContentFolderPath}, active set: {activeSet.Kind}");
}
```

Asset resolvers check `Assets/` first, then bare `ContentFolderPath` (legacy fallback)
so existing tests/harness keep working. The existing
`ResolveContentFilePath(string fileName)` (used by `TryLoadContentBitmap` for the
cluster stamp `stamp_demo.png`) is also routed through `Assets/` via
`ResolveAssetPath` so a single helper covers all static assets:

```csharp
private string ResolveAssetPath(string fileName)
{
    var underAssets = Path.Combine(ContentFolderPath, ContentFileNames.AssetsFolderName, fileName);
    if (File.Exists(underAssets)) return underAssets;
    return Path.Combine(ContentFolderPath, fileName); // legacy-root fallback
}

public string GetWorldMapPath() => ResolveAssetPath(ContentFileNames.WorldMapFileName);
public string GetFullResolutionWorldMapPath() => ResolveAssetPath(ContentFileNames.FullResolutionWorldMapFileName);
public string ResolvePinPartPath(string relativePath) => ResolveAssetPath(relativePath);
public string ResolveContentFilePath(string fileName) => ResolveAssetPath(fileName);
```

- **`LoadLocationsAsync`** (fixes the existing Excel/`BaseDirectory` inconsistency):
  Excel path = `ExcelCoordinateFilePath ?? Path.Combine(_contentSetResolver.ResolveActiveContentSet(ContentFolderPath).Path, ExcelCoordinateFileName)`;
  JSON path = `Path.Combine(_contentSetResolver.ResolveActiveContentSet(ContentFolderPath).Path, LocationsJsonFileName)`.
- **Location content resolution** (`LoadLocationContentAsync`,
  `LoadAllLocationImagesWithTranslationsAsync`, `ResolveLocationImageFiles`,
  `LoadDidacticTextAsync`): use `Path.Combine(_contentSetResolver.ResolveActiveContentSet(ContentFolderPath).Path, location.Name)`.
- **`ValidateContentFolder`** contract (C5):
  - Require world map via `ResolveAssetPath` — missing map is an **error**.
  - When the active set is `Demo-Content` or `Production-Content` and lacks a coordinate
    source, return **error** (unrecoverable — the user would see a blank map).
  - When the active set resolved to the **legacy root** (`Kind == ContentSetKind.Legacy`,
    neither Production nor Demo has a coordinate source), require the legacy root to
    contain `locations.json` or `Coordinates for map.xlsx`; if neither, return **error**
    with a remediation message that names the new layout and the rename workaround (§10).
    This is the merge-gate behavior `StartupValidationHarnessTests` already exercises.
  - Log which set was selected (Demo / Production / legacy) at the start of validation.
- Set `ActiveContentSetPath` once (lazy) and **expose it via `IContentLoader`** (C9 from
  first review — required, not optional) so MainWindow/status/overlays can display it.
  **Contract:** `string ActiveContentSetPath { get; }` — a read-only property computed
  lazily on first access (thread-safe via `Lazy<string>` or equivalent). Once resolved,
  the value is stable for the session; it does not change if the filesystem is mutated
  after startup. In legacy-root mode, returns `ContentFolderPath`. The
  `ContentSetKind` from the resolver is also stored so `ResolveLayoutStoragePath` (§7)
  can derive the AppData suffix without re-parsing the path.

### 4. `Services/StartupValidator.cs` (previously omitted)

**Constructor change (fourth review):** add `IContentSetResolver` parameter:

```csharp
public StartupValidator(ILogger logger, string contentFolderPath, IContentSetResolver contentSetResolver)
{
    _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    _contentFolderPath = contentFolderPath ?? throw new ArgumentNullException(nameof(contentFolderPath));
    _contentSetResolver = contentSetResolver ?? throw new ArgumentNullException(nameof(contentSetResolver));
}
```

Use `ContentFileNames` + the shared resolver:

| Line | Current | Change to |
|------|---------|-----------|
| 50 | `Path.Combine(_contentFolderPath, WorldMapFileName)` | `Path.Combine(_contentFolderPath, AssetsFolderName, WorldMapFileName)` (or `ResolveAssetPath` equivalent) |
| 62 | `Path.Combine(_contentFolderPath, LocationsJsonFileName)` | `Path.Combine(_contentSetResolver.ResolveActiveContentSet(_contentFolderPath).Path, LocationsJsonFileName)` |
| 76 | `Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Coordinates for map.xlsx")` | `Path.Combine(_contentSetResolver.ResolveActiveContentSet(_contentFolderPath).Path, ExcelCoordinateFileName)` |

Missing coordinate source in the chosen set → **error** with a clear remediation message
(C5).

### 5. `Models/PinPartConfig.cs` comment + constant (C2)

Line 11 says *"Paths are relative to the Images&Content folder"* → update to *"relative
to the `Assets/` subfolder of the content root"*. Same for `GeometryMetadataPath`
(line 17). Defaults (`Pins_v2/parts`, `pin_part_geometry.json`) remain valid as
Assets-relative after `ResolvePinPartPath` prepends `Assets/`.

### 6. `Models/VisualConfig.cs` + `visual-config.default.json` (B2, landed)

Final value (no longer pending):
- `Models/VisualConfig.cs:141` → `LayoutStoragePath = "Images&Content/Demo-Content/manual-layouts.json"`.
- `visual-config.default.json:104` → same value; add a commented example:
  `// "LayoutStoragePath": "Images&Content/Production-Content/manual-layouts.json"`.

### 7. `MainWindow` touches

- `MainWindow.xaml.cs:264` error message → *"Please ensure Images&Content/Demo-Content
  (or Production-Content) exists with a coordinate source (locations.json or 'Coordinates
  for map.xlsx') and that Images&Content/Assets/ contains the world map image."* (B5)
- **`MainWindow.CompositePins.partial.cs:144-145`** builds the geometry hash path
  directly from `IOPath.Combine(AppDomain.CurrentDomain.BaseDirectory, _visualConfig.PinParts.GeometryMetadataPath)` —
  it does **not** call `ResolvePinPartPath`. After the reorg, `GeometryMetadataPath`
  (`Pins_v2/parts/pin_part_geometry.json`) must be resolved through `ContentLoader`
  (e.g. `_contentLoader.ResolvePinPartPath(...)`) so the `Assets/` prefix is applied.
  **Same fix needed at `MainWindow.LayoutEditor.partial.cs:640-642`** — a second
  instance of the same `BaseDirectory + GeometryMetadataPath` pattern, passed to
  `CompositePinApplicationService.BuildApplyInstructions` and then to
  `TryCacheLoad` → `ComputeGeometryHash`.
- **Pre-existing bug: geometry hash is always `"geometry-missing"` today.** Both
  sites above build `BaseDirectory + "Pins_v2/parts/pin_part_geometry.json"` but the
  file lives at `BaseDirectory + "Images&Content/Pins_v2/parts/pin_part_geometry.json"`.
  `CompositePinLayoutContentHasher.ComputeGeometryHash` returns `"geometry-missing"`
  when the file doesn't exist (line 43-44). The composite-pin plan cache has been
  working with a constant placeholder hash — it never invalidates on geometry changes.
  **Fix both paths as part of this reorg** (route through `_contentLoader.ResolvePinPartPath`
  or `_contentLoader.GetFullResolutionWorldMapPath`-style helper). After the fix, the
  geometry hash changes from the constant `"geometry-missing"` placeholder to a real
  file hash, so cached plans keyed on it are invalidated once — **one-time re-render
  on first run after upgrade** (B3/D6). Note: the cached plans store **relative** paths
  (`Pins_v2/parts/...`), not absolute paths; the re-render is triggered by the hash
  change, not a root change.
- `MainWindow.ResolveLayoutStoragePath` (xaml.cs:418-444) — **per-set decision landed
  (C3)**: saved layouts are scoped per active content set. Concrete behavior:
  - Resolve the active set via `_contentSetResolver.ResolveActiveContentSet(...)` first
    (add the resolver to MainWindow's construction path or inject the result via
    `IContentLoader.ActiveContentSetPath`). The `ContentSetKind` from the resolution
    directly provides the set suffix without path parsing.
  - AppData file name = `manual-layouts.<set-suffix>.json`, where `<set-suffix>` is
    `demo` (set = `Demo-Content`), `production` (set = `Production-Content`), or
    `legacy` (set = bare content root).
  - **Migration on first run after upgrade:** if the un-namespaced
    `manual-layouts.json` exists in `%AppData%\InteractiveWorldMap\` and no
    `manual-layouts.demo.json` exists, copy the un-namespaced file to
    `manual-layouts.demo.json` once and continue. Log the migration.
  - Seed source (`bundledPath`) follows the active content set
    (`activeSet\manual-layouts.json`). Production deployments ship without a seed by
    design (D3).
  - Expose a `ManualLayoutEditorConfig.SetAwareStorage` (or equivalent) on `VisualConfig`
    so operators can opt out of the namespacing if they need a single shared file.
  - **Rationale:** per-set namespacing is designed for developers who run both Demo and
    Production locally on the same machine. Production deployments will only ever use one
    set and will never exercise the switching path. The migration code (copy un-namespaced
    → namespaced) is a one-time code path that must be tested on the supported OS.

### 8. Cache strategies (C4, B4)

- **`ClusterCache`** (`Services/ClusterCache.cs`): currently keyed by a hash of location
  data only. **Add the active content-set identifier to the key** (namespace the cache
  file by set) so switching sets never reuses the wrong set's clusters. Concrete change:
  - Move cache directory from `%AppData%\InteractiveWorldMap\cluster_cache.json` to
    `%AppData%\InteractiveWorldMap\clusters\<set-suffix>.json` where `<set-suffix>` is
    `demo` / `production` / `legacy` (matches §7's suffix rule).
  - Pass the active set suffix into the `ClusterCache` constructor
    (e.g. `new ClusterCache(logger, contentSetSuffix)`); resolve via
    `_contentSetResolver` at `ContentLoader` construction time.
  - Keep the existing `ComputeHash(locations, threshold)` for content invalidation
    within a set; the set suffix is the outer namespace.
  - **Migration on first run after upgrade:** if the un-namespaced
    `cluster_cache.json` exists and no `clusters\demo.json` exists, copy it to
    `clusters\demo.json` and delete the original. Log the migration.
- **Composite-pin plan cache** (`%AppData%/InteractiveWorldMap/composite_pin_plan_cache/`):
  path-rooted entries go stale on upgrade → acceptable one-time recompute (B3/D6).
  Optionally apply the same `<set-suffix>.json` per-file rule for symmetry; not required.

### 9. Downstream tools / scripts (B1, B8, B14)

- `InteractiveWorldMap.csproj` (lines ~25-35): the recursive `Images&Content\**\*` glob
  already copies the new subfolders; **remove or repoint the separate
  `None Update="Coordinates for map.xlsx"` rule** (repo root) so the build output is
  correct. Verify `bin\...\Images&Content\Demo-Content\Coordinates for map.xlsx` is
  produced.
- `Tools/ManualLayoutSeedGenerator/Program.cs`:
  - line 105: `ExcelPath` default → `Images&Content/Demo-Content/Coordinates for map.xlsx`.
  - line 106: `MapImagePath` → `Images&Content/Assets/World Map Extra Large.jpg`.
  - line 107: `OutputPath` → `Images&Content/Demo-Content/manual-layouts.json`.
- `Tools/MapResamplerComparison/Program.cs:12` source →
  `Images&Content/Assets/World Map 1976.jpg`.
- `Tools/PinDebugger/PinDebuggerContext.cs:27` →
  `Images&Content/Assets/Pins_v2/parts`.
- `scripts/verify_manual_layout_seeds.ps1` (line ~18): currently points at
  `Images&Content\World Map Extra Large.jpg` and `manual-layouts.json` → update to the
  `Assets/` and `Demo-Content/` paths.
- `scripts/verify_taste.py` (107-110): the new resolver (`Utilities/`) and
  `Models/ContentFileNames` constants legitimately mention folder names; **re-run the
  taste check and add the resolver to the allowlist if it flags the new strings** (B8/D7).
- **`Services/PinPartVariantCatalog.cs` + `MainWindow.DeveloperTuning.partial.cs:30-40`**
  (third review): `RefreshTuningPanelVariantOptions` calls
  `_variantCatalog.ListVariants(_contentLoader.ContentFolderPath, _visualConfig.PinParts.PartsFolderPath, ...)`.
  `ListVariants` (line 24) builds `Path.Combine(contentFolderPath, partsFolderPath, subfolderName)` =
  `Images&Content/Pins_v2/parts/shaft_variants`. After the reorg, `Pins_v2/parts` moves
  to `Assets/Pins_v2/parts`, so this path becomes stale and the Developer Tuning panel's
  shaft/head variant dropdowns will show empty. **Fix (option b, fourth review):** change
  the two call sites in `DeveloperTuning.partial.cs:31,37` to pass
  `Path.Combine(_contentLoader.ContentFolderPath, ContentFileNames.AssetsFolderName)` as
  the content-root argument. This leaves `PinPartVariantCatalog` and its tests untouched
  (the catalog remains a pure path combiner).
- **`Utilities/UpdateLocationsFromExcel.cs`** (third review): currently uses
  `AppDomain.CurrentDomain.BaseDirectory` for both the Excel read path (line 22) and
  the JSON write path (line 23). The fix should accept the target content-set path as
  a parameter (or inject `ContentSetResolver`) rather than hardcoding `BaseDirectory`.
  The utility is in `Utilities/` which may reference `Models` and other `Utilities`
  classes, so a `ContentSetResolver` dependency is architecturally valid.

### 10. Force-Demo workaround (C6)

Selection is deployment-time (path-based), force mode is deferred. Document the
workaround: rename `Production-Content/` → `Production-Content.disabled` to force Demo.
Surface this in the new `docs/guides/CONTENT_SETS.md` (E1) so operators know how to
flip sets without editing the binary. The `ValidateContentFolder` remediation message
(B5) also names the workaround.

### 11. Composition root wiring (fourth review)

The `IContentSetResolver` must be instantiated once and injected into both `ContentLoader`
and `StartupValidator`. Update the instantiation sites:

- **`MainWindow.xaml.cs:166`** — `new ContentLoader(_logger)` becomes
  `new ContentLoader(_logger, _contentSetResolver)`.
- **`App.xaml.cs` or `MainWindow` constructor** — create the single resolver instance:
  `var contentSetResolver = new ContentSetResolver();` and pass it to both `ContentLoader`
  and `StartupValidator`.
- **`StartupValidator` creation site** (search for `new StartupValidator`) — add the
  resolver parameter.

The resolver is stateless and thread-safe, so a single instance shared across the app
is correct.

---

## Tests & harness

- **No mass breakage** thanks to the asset legacy fallback (§3) and legacy-root content
  fallback (§2). Existing `ContentLoaderTests.CreateContentFolderWithMap()` and
  `StartupValidationHarnessTests` keep working. **However (fourth review):** the
  constructor signature change (§3) means every `new ContentLoader(new MockLogger())`
  call in tests must become `new ContentLoader(new MockLogger(), new ContentSetResolver())`.
  This is a mechanical find-and-replace across `ContentLoaderTests`, `StartupValidationHarnessTests`,
  and any other test that constructs a `ContentLoader`. The resolver is stateless so
  `new ContentSetResolver()` is safe in every test context.
- **`Tests/StartupValidationHarnessTests.cs:44,60`** exercises both the **source tree**
  (`RepoRoot\Images&Content`) and the **build output**
  (`RepoRoot\bin\Debug\net6.0-windows\Images&Content`). After reorg both should resolve
  to the new structure: `Repo_StartupValidator_RunsWithoutCrash` will return errors
  rather than pass if `Demo-Content\` has no coordinate source — keep the existing
  loose `Assert.NotNull(result)` contract (line 53-54, "known debt TD-002") and confirm
  the test still doesn't assert `IsValid`. `Repo_ContentLoader_ValidatesWhenBuiltOutputPresent`
  similarly should remain loose. Confirm both pass via `verify.ps1` (B6/D4 — this is
  in the merge gate).
- **`Tests/ExcelCoordinateReaderTests.cs:33`** is in-memory (fixture), unaffected; mention
  for completeness.
- **New tests** in `Tests/ContentSetResolverTests.cs` (new file under
  `Tests/Utilities/` or `Tests/Services/`, matching the `Utilities/` namespace):
  - `ResolveActiveContentSet_ProductionWithExcel_ReturnsProduction`
  - `ResolveActiveContentSet_ProductionWithJsonOnly_ReturnsProduction`
  - `ResolveActiveContentSet_ProductionPresentButNoSource_ReturnsDemo`
  - `ResolveActiveContentSet_OnlyDemoWithSource_ReturnsDemo`
  - `ResolveActiveContentSet_NeitherSetHasSource_ReturnsLegacyRoot`
  - `ResolveActiveContentSet_RandomSubfolderAloneDoesNotCountAsSet` (regression: empty
    Production with only `__pycache__` / `.git` falls through to Demo)
  - `ResolveActiveContentSet_ReturnsCorrectKind_ForEachBranch` (verify the
    `ContentSetKind` enum in the structured result matches the resolved path)
- **New tests** in `Tests/ContentLoaderTests.cs` for asset resolution and the new
  `ActiveContentSetPath`:
  - `GetWorldMapPath_AssetsFolderPresent_ReturnsAssetsPath`
  - `GetWorldMapPath_AssetsFolderMissing_LegacyRootFallback`
  - `ResolvePinPartPath_RelativePath_RoutesThroughAssets`
  - `ActiveContentSetPath_AfterFirstResolve_IsStableForSession`
  - `ValidateContentFolder_MissingCoordinateSource_ReturnsFalse`
- Update `Tests/ContentLoaderTests.cs::CreateContentFolderWithMap` to add a sibling
  helper `CreateContentFolderWithAssets()` (creates `Assets/` subfolder with the map)
  so the new asset-resolver tests are realistic; keep the old helper for the
  legacy-fallback tests.
- `Tests/Architecture/GoldenPrincipleTests.cs`: unaffected (Views still must not build
  `Images&Content` paths).
- **`Tests/PinPartVariantCatalogTests.cs`** (third review, updated fourth review): no
  changes needed. The §9 fix (option b) changes only the call site in
  `DeveloperTuning.partial.cs`, leaving `PinPartVariantCatalog` and its tests untouched.
- **`Tests/CompositePinPlacementPolicyTests.cs:136,143`** (third review): two lines
  hardcode `"Images&Content/Pins_v2/parts/heads/pin_01.png"` as source paths. These are
  test fixture strings (not filesystem paths), but they encode the old layout assumption.
  Update to `"Images&Content/Assets/Pins_v2/parts/heads/pin_01.png"` or confirm they are
  cosmetic and don't affect test behavior.

---

## Documentation updates (B9–B12, E1, E4, B13)

| File | Change |
|------|--------|
| `docs/guides/SETUP_GUIDE.md` (56,60,63,135) | world map + manual-layouts + Excel paths → `Assets/` and `Demo-Content/` |
| `docs/guides/MANUAL_LAYOUT_EDITOR.md` (33,252,266) | `manual-layouts.json` path → content set |
| `docs/guides/DEMO_INSTRUCTIONS.md` (64,118) | `letter-product-pic.jpg` → `Demo-Content/<folder>/...` |
| `docs/guides/CONTENT_FEATURES.md` (30) | `letter-product-pic.jpg` path; add Demo/Production/legacy selection section (E1) |
| `docs/index.md:68` | link/description of `Images&Content/` layout |
| `AGENTS.md:53` | `Images&Content/` layout description |
| `docs/reference/GLOSSARY.md` | `verify_manual_layout_seeds.ps1` wording (B14); add **Content set** and **Active content set** terms (E4) |
| `visual-config.default.json:104` | landed value + commented Production example (§6, E2) |
| `CHANGELOG.md [Unreleased]` | specific user-visible note: Demo/Production selection now works; legacy flat layout still supported (dev only); one-time composite-pin re-render after upgrade (B13/D6); geometry hash now reads the real file (previously always `"geometry-missing"`) |

New guide (E1): `docs/guides/CONTENT_SETS.md` explaining how the app picks a set, how to
switch locally, what the legacy fallback does, and the per-set layouts expectation.

---

## Operational notes

- **D1 `git mv`** for all moved files to preserve history.
- **D2 `.gitignore`**: new folders are content (not ignored); confirm the gitignored
  `bin\...\Images&Content\` output populates correctly and `StartupValidationHarnessTests`
  passes against it.
- **D3 seed copy**: `MainWindow.xaml.cs:432-435` seeds from `bundledPath` =
  `baseDir + configuredLayoutPath`; with §6 value, seed exists only when the Demo bundle
  ships — correct for production (no seed → no copy).
- **D4 CI**: `scripts/validate_startup.ps1` and harness tests are in the merge gate; a
  path failure blocks merge.
- **D5 legacy fallback is dev/test-only**; production deployments adopt the new structure
  wholesale. State this explicitly so the fallback isn't treated as long-term support.
- **C7 (optional dev)**: Windows directory junctions (`mklink /J`) can point
  `bin\...\Images&Content\Demo-Content` back to the repo source to avoid copying the
  content tree on every clean build.
- **D8 user's local `visual-config.json` migration** (third review): the plan updates
  `visual-config.default.json` (§6) but users with a customized `visual-config.json`
  that overrides `LayoutStoragePath` to `"Images&Content/manual-layouts.json"` will have
  a stale path after the reorg. The app will fail to find the seed file. Document in
  `docs/guides/CONTENT_SETS.md`: "If you have a custom `LayoutStoragePath` in your
  `visual-config.json`, update it to the new path or delete the override to pick up the
  new default."

---

## Open questions — resolved

| # | Question | Resolution |
|---|----------|------------|
| 1 | `manual-layouts.json` placement | Per-set (C3 landed): AppData namespaced by set; existing layouts → Demo key. |
| 2 | Empty-production definition | Require a coordinate source file (Excel or JSON). |
| 3 | Stray images | `letter-product-pic.jpg` → `Demo-Content/`; `v4-460px...jpg` → `Extras/`; never delete. |
| 4 | Force mode | Deferred; document rename workaround (C6). |

---

## Rollback (E3)

Keep the reorg in a single reviewable commit (or small numbered series). Rollback =
`git revert <sha>`. The csproj + resolver changes are the only runtime-path-affecting
edits; reverting them restores the flat layout.

**Rollback testing:** after a revert, the code expects the flat layout but the AppData
files may have been migrated to namespaced names (`manual-layouts.demo.json`,
`clusters\demo.json`). The reverted code won't find them. Document: "After rollback,
delete the namespaced AppData files or rename them back to the un-namespaced names."

---

## Completion bookkeeping

- On completion: **remove the TO_DO entry** (`docs/TO_DO.md`) per AGENTS.md (B15), and
  archive this plan to `docs/exec-plans/completed/`.

## Verification

- `git mv` all content; build.
- `dotnet build InteractiveWorldMap.sln`
- Inspect `bin\Debug\net6.0-windows\Images&Content\` to confirm `Assets/`,
  `Demo-Content/Coordinates for map.xlsx`, and `manual-layouts.json` are present.
  Also confirm `Assets/Pins_v2/parts/shaft_variants/` and
  `Assets/Pins_v2/parts/head_variants/` exist and contain subdirectories (critical
  for the Developer Tuning panel — empty dropdowns are a silent failure).
- `dotnet test Tests/InteractiveWorldMap.Tests.csproj`
- `.\scripts\verify.ps1` (Windows) / `./scripts/verify.sh` (macOS harness-only)
- `scripts/verify_taste.py` re-run; allowlist resolver if needed.
- Manual: app loads **Demo** when `Production-Content/` absent; switches to
  **Production** when a valid `Production-Content/` (Excel or JSON) is added; assets
  load from `Assets/`; legacy flat folder still works via fallback; one-time
  composite-pin re-render observed; geometry hash now reads the real file (verify in
  logs that the hash is no longer `"geometry-missing"`); Developer Tuning panel shaft
  and head variant dropdowns populate correctly; switching to a Production set with a
  stale `%AppData%\InteractiveWorldMap\clusters\demo.json` cache does **not** replay
  Demo clusters; renaming `Production-Content` → `Production-Content.disabled` forces
  Demo.
- Update `CHANGELOG.md` `[Unreleased]` (specific) and archive plan; remove TO_DO bullet.
