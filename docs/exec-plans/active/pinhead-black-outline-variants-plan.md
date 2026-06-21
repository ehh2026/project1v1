---
status: active
owner: agent
started: 2026-06-12
requirements_ref: pinhead-black-outline-variants
parent_program: composite-pins-program.md
parent_plan: pin-parts-composite-placement-plan.md
---

# Pinhead Black Outline Variants Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Generate black-outline composite pin head variants at 2, 4, 6, 8, 10, 12, and 14 px total stroke width, with each stroke split half outside and half inside the detected pinhead edge, and make the variants selectable through visual config.

**Architecture:** Add a head-only asset generator parallel to the existing shaft-variant tooling. Store generated assets under `Images&Content/Pins_v2/parts/head_variants/<variant>/`, add `PinParts.HeadAssetVariant` as a config-only selector, and route composite render plans through a small head path resolver. Manual layout head selection continues to select the pin head identity by file name; the active config controls whether that identity uses base head art or a head-variant folder.

**Tech Stack:** WPF / .NET 6 / C# for render-plan config and tests; Python 3 with Pillow, numpy, and scipy from `scripts/requirements.txt` for deterministic PNG asset generation.

---

## Requirements

1. Generate variants named `outline_black_2px`, `outline_black_4px`, `outline_black_6px`, `outline_black_8px`, `outline_black_10px`, `outline_black_12px`, and `outline_black_14px`.
2. For each total width `N`, draw `N / 2` px outward from the detected head alpha silhouette and `N / 2` px inward from that silhouette.
3. Generate one PNG per existing base head file: `pin_01_head.png` through `pin_12_head.png`.
4. Generate `preview_heads.png` in each variant folder for quick visual comparison.
5. Add optional config key `PinParts.HeadAssetVariant`, resolving `pin_01_head.png` to `Pins_v2/parts/head_variants/<variant>/pin_01_head.png` when non-empty.
6. Keep current behavior when `HeadAssetVariant` is empty.
7. Include `HeadAssetVariant` in composite layout config hashing so changing variants invalidates stale cached render plans.

## File Structure

- Create `scripts/create_head_asset_variants.py`: head-only asset generator and preview writer. Keep it separate from `scripts/create_shaft_asset_variants.py` because head strokes use centered silhouette strokes while shaft variants support outer halos, inner darkening, and bright-core combinations.
- Modify `Models/PinPartConfig.cs`: add `HeadAssetVariant` next to `ShaftAssetVariant`.
- Modify `Services/CompositePinRenderPlanBuilder.cs`: add `ResolveHeadPath(config, geometry)` beside `ResolveShaftPath`.
- Modify `Services/CompositePinPlanningService.cs`: allow saved manual-layout `HeadSourcePath` values from either the base head path or a configured `head_variants/<variant>/` path to map back to the correct `PinPartGeometryEntry`.
- Modify `Services/CompositePinLayoutContentHasher.cs`: include `HeadAssetVariant` in the config hash string.
- Modify tests: `Tests/CompositePinRenderPlanBuilderTests.cs`, `Tests/CompositePinPlanningServiceTests.cs`, `Tests/CompositePinLayoutContentHasherTests.cs`, and `Tests/VisualConfigServiceTests.cs`.
- Modify docs: `docs/guides/VISUAL_CONFIG.md`, `scripts/README.md`, `docs/TO_DO.md`, and `CHANGELOG.md`.

## Modularity / File Size Impact

- Do not change `MainWindow.xaml.cs` or any view file for this work.
- Keep `CompositePinRenderPlanBuilder.cs` changes to a new private helper and one call-site replacement. If this pushes the file near the 800-line repo limit, split path resolution into a focused helper before adding more logic.
- Keep `CompositePinPlanningService.cs` changes focused on source-path matching only; do not move render-plan path construction into the planning service.
- Keep the Python generator self-contained and below 300 lines. If shared morphology helpers are needed later, extract them to `scripts/pin_asset_masks.py` instead of expanding both asset generator scripts independently.

## Variant Algorithm

For each base head image:

1. Load the source as RGBA.
2. Build a boolean silhouette mask from alpha: `mask = alpha > 0`.
3. For total width `N`, compute `radius = N // 2`.
4. Build a circular structuring element:

```python
def disk(radius: int) -> np.ndarray:
    yy, xx = np.ogrid[-radius:radius + 1, -radius:radius + 1]
    return (xx * xx + yy * yy) <= radius * radius
```

5. Compute the centered stroke mask:

```python
outer = ndimage.binary_dilation(mask, structure=disk(radius))
inner = ndimage.binary_erosion(mask, structure=disk(radius))
stroke = outer & ~inner
```

6. Composite the stroke as solid black:

```python
result = source.copy()
rgba = np.array(result)
rgba[stroke, 0] = 0
rgba[stroke, 1] = 0
rgba[stroke, 2] = 0
rgba[stroke, 3] = 255
```

7. Save the result as `pin_XX_head.png` in the matching variant folder.

This intentionally blacks both the outward halo and inward edge band. The untouched interior remains the original head art.

## Tasks

### Task 1: Add config and render-plan path coverage

> **Status: COMPLETE** — Steps 1–6 were implemented by a prior agent. Verified in the codebase:
> - `Models/PinPartConfig.cs` L50: `HeadAssetVariant` property exists.
> - `Services/CompositePinRenderPlanBuilder.cs` L278, L374: `ResolveHeadPath()` call-site and helper exist.
> - `Tests/CompositePinRenderPlanBuilderTests.cs` L121: render-plan test exists.
> - `Tests/VisualConfigServiceTests.cs` L234, L254: both visual-config tests exist.
> Skip to Task 2.

**Files:**
- Modify: `Tests/CompositePinRenderPlanBuilderTests.cs`
- Modify: `Tests/VisualConfigServiceTests.cs`
- Modify: `Models/PinPartConfig.cs`
- Modify: `Services/CompositePinRenderPlanBuilder.cs`

- [x] **Step 1: Add failing render-plan test** *(done)*

Added after `BuildPlan_WhenShaftAssetVariantConfigured_UsesVariantShaftPath`:

```csharp
[Fact]
public void BuildPlan_WhenHeadAssetVariantConfigured_UsesVariantHeadPath()
{
    var builder = new CompositePinRenderPlanBuilder();
    var target = new PinPlacementTarget
    {
        StartScreen = new Point(100, 320),
        EndScreen = new Point(100, 100),
        LocationId = "loc-head-variant",
        GroupId = 1
    };
    var placement = new PinPartPlacementResult
    {
        PairId = "pin_a",
        PairGeometry = CreateVerticalGeometry(),
        TargetAngleDeg = 0.0,
        TargetLengthPx = 220.0
    };
    var config = new PinPartConfig
    {
        PartsFolderPath = "Pins_v2/parts",
        HeadAssetVariant = "outline_black_6px"
    };

    var plan = builder.BuildPlan(target, placement, config);

    Assert.Equal(@"Pins_v2/parts\head_variants\outline_black_6px\pin_a_head.png", plan.HeadSourcePath);
    Assert.Equal(plan.HeadSourcePath, plan.HeadLayer.SourcePath);
    Assert.Equal(@"Pins_v2/parts\pin_a_shaft.png", plan.ShaftSourcePath);
}
```

- [x] **Step 2: Add failing visual-config tests** *(done)*

Added next to the existing `ShaftAssetVariant` tests:

```csharp
[Fact]
public void Load_PinPartsHeadAssetVariant_Deserializes()
{
    var path = Path.GetTempFileName();
    try
    {
        File.WriteAllText(path, @"{ ""PinParts"": { ""HeadAssetVariant"": ""outline_black_6px"" } }");

        var config = VisualConfig.LoadFromFile(path);

        Assert.Equal("outline_black_6px", config.PinParts.HeadAssetVariant);
    }
    finally
    {
        File.Delete(path);
    }
}

[Fact]
public void Load_PinPartsHeadAssetVariant_UsesDefaultWhenOmitted()
{
    var path = Path.GetTempFileName();
    try
    {
        File.WriteAllText(path, @"{ ""PinParts"": { } }");

        var config = VisualConfig.LoadFromFile(path);

        Assert.Equal(string.Empty, config.PinParts.HeadAssetVariant);
    }
    finally
    {
        File.Delete(path);
    }
}
```

- [x] **Step 3: Run focused tests and confirm failure** *(done — tests confirmed passing after Steps 4–6)*

- [x] **Step 4: Add config property** *(done — `Models/PinPartConfig.cs` L50)*

- [x] **Step 5: Add render-plan resolver** *(done — `ResolveHeadPath` helper at L374, call-site at L278)*

- [x] **Step 6: Run focused tests and confirm pass** *(done)*

### Task 2: Keep manual layout head identity and cache hashing correct

**Files:**
- Modify: `Tests/CompositePinPlanningServiceTests.cs`
- Modify: `Tests/CompositePinLayoutContentHasherTests.cs`
- Modify: `Services/CompositePinPlanningService.cs`
- Modify: `Services/CompositePinLayoutContentHasher.cs`

- [ ] **Step 1: Add failing planning-service test**

Add **two** tests to `Tests/CompositePinPlanningServiceTests.cs`:

Test 1 — variant-folder path resolves to matching geometry:

```csharp
[Fact]
public void BuildPlan_WhenPreferredHeadPathUsesVariantFolder_SelectsMatchingHeadGeometry()
{
    var service = MakeService();
    var (target, candidates, config) = MakeFixture("loc-preferred-variant-head");
    candidates["pin_b"] = CloneGeometry(candidates["pin_a"], "pin_b", "pin_b_head.png");
    config.HeadAssetVariant = "outline_black_6px";

    var result = service.BuildPlan(
        target,
        candidates,
        config,
        preferredPairId: "pin_a",
        preferredHeadSourcePath: @"Pins_v2/parts\head_variants\outline_black_6px\pin_b_head.png");

    Assert.Equal(@"Pins_v2/parts\head_variants\outline_black_6px\pin_b_head.png", result.RenderPlan.HeadSourcePath);
}
```

Test 2 — base-folder path (no variant) resolves to the same geometry:

```csharp
[Fact]
public void BuildPlan_WhenPreferredHeadPathUsesBaseFolder_SelectsMatchingHeadGeometry()
{
    var service = MakeService();
    var (target, candidates, config) = MakeFixture("loc-preferred-base-head");
    candidates["pin_b"] = CloneGeometry(candidates["pin_a"], "pin_b", "pin_b_head.png");
    // No HeadAssetVariant — base folder path only.

    var result = service.BuildPlan(
        target,
        candidates,
        config,
        preferredPairId: "pin_a",
        preferredHeadSourcePath: @"Pins_v2/parts\pin_b_head.png");

    // File-name-only match: base path resolves to pin_b geometry, then
    // ResolveHeadPath with empty variant produces the base-folder path.
    Assert.Equal(@"Pins_v2/parts\pin_b_head.png", result.RenderPlan.HeadSourcePath);
}
```

> **Note:** `ResolveHeadPath` uses only file I/O from `Path.Combine` — it does not open any PNG file. Both tests are pure path-string assertions; no assets need to exist on disk.

Add this helper near `MakeFixture`:

```csharp
private static PinPartGeometryEntry CloneGeometry(PinPartGeometryEntry source, string pairId, string headFile)
{
    return new PinPartGeometryEntry
    {
        HeadFile = headFile,
        ShaftFile = $"{pairId}_shaft.png",
        Head = source.Head,
        Shaft = source.Shaft
    };
}
```

- [ ] **Step 2: Add failing cache-hash test**

Add this test to `Tests/CompositePinLayoutContentHasherTests.cs`:

```csharp
[Fact]
public void ComputeConfigHash_Changes_WhenHeadAssetVariantChanges()
{
    var baseline = new PinPartConfig
    {
        HeadAssetVariant = string.Empty
    };
    var changed = new PinPartConfig
    {
        HeadAssetVariant = "outline_black_6px"
    };

    Assert.NotEqual(
        CompositePinLayoutContentHasher.ComputeConfigHash(baseline),
        CompositePinLayoutContentHasher.ComputeConfigHash(changed));
}
```

- [ ] **Step 3: Run focused tests and confirm failure**

Run:

```powershell
dotnet test Tests/InteractiveWorldMap.Tests.csproj --filter "FullyQualifiedName~CompositePinPlanningServiceTests|FullyQualifiedName~CompositePinLayoutContentHasherTests" --no-restore
```

Expected: at least one test fails because variant head paths do not resolve back to the saved head identity and the config hash ignores `HeadAssetVariant`.

- [ ] **Step 4: Match preferred head paths by file name**

In `Services/CompositePinPlanningService.cs`, inside `ResolveHeadGeometry`, replace the existing block:

```csharp
if (preferredHeadSourcePath != null)
{
    var match = candidates.Values.FirstOrDefault(e =>
        string.Equals(
            System.IO.Path.Combine(config.PartsFolderPath, e.HeadFile),
            preferredHeadSourcePath,
            StringComparison.OrdinalIgnoreCase));
    if (match != null)
        return match;
}
```

with:

```csharp
if (preferredHeadSourcePath != null)
{
    var preferredHeadFile = System.IO.Path.GetFileName(preferredHeadSourcePath);
    var match = candidates.Values.FirstOrDefault(e =>
        string.Equals(e.HeadFile, preferredHeadFile, StringComparison.OrdinalIgnoreCase));
    if (match != null)
        return match;
}
```

This makes the match file-name-only, so a `preferredHeadSourcePath` pointing to any variant folder (`head_variants/outline_black_6px/pin_b_head.png`) or the base folder (`Pins_v2/parts/pin_b_head.png`) both resolve to the correct `PinPartGeometryEntry`.

- [ ] **Step 5: Include head variant in config hash**

In `Services/CompositePinLayoutContentHasher.cs`, replace the entire `key` assignment inside `ComputeConfigHash`. The current code (L57–64) ends with `$"{config.ShaftAssetVariant}"` and a `;` on the same line that terminates the `var key = …` statement. Replace the whole block with:

```csharp
var key = $"{config.SelectionMode}:" +
          $"{config.MaxResidualRotationDeg:F2}:" +
          $"{config.MinStretchFactor:F3}:" +
          $"{config.MaxStretchFactor:F3}:" +
          $"{config.TargetHeadRadiusPx:F2}:" +
          $"{config.TargetShaftHalfWidthPx:F2}:" +
          $"{config.UseLitShafts}:" +
          $"{config.ShaftAssetVariant}:" +
          $"{config.HeadAssetVariant}";
```

This adds one line (`HeadAssetVariant`) and moves the statement-terminating `;` to the new last line.

- [ ] **Step 6: Update `composite-pins-program.md` status to `In Progress`**

In `docs/exec-plans/active/composite-pins-program.md`, change the Head visibility row status from `Planned` to `In Progress` now that Task 2 is underway.

- [ ] **Step 7: Run focused tests and confirm pass**

Run:

```powershell
dotnet test Tests/InteractiveWorldMap.Tests.csproj --filter "FullyQualifiedName~CompositePinPlanningServiceTests|FullyQualifiedName~CompositePinLayoutContentHasherTests" --no-restore
```

Expected: PASS.

### Task 3: Add the head-outline asset generator

**Files:**
- Create: `scripts/create_head_asset_variants.py`
- Modify: `scripts/README.md`

> **Dependency note:** This script uses `scipy.ndimage` for binary dilation/erosion, unlike `create_shaft_asset_variants.py` which uses PIL-only `MaxFilter`/`MinFilter`. Both scripts share the same venv (`scripts/venv`) and `requirements.txt` (which already includes scipy). Do **not** add scipy to requirements.txt again.

- [ ] **Step 1: Create the generator script**

Create `scripts/create_head_asset_variants.py` with these public behaviors:

```python
PIN_COUNT = 12
DEFAULT_WIDTHS = [2, 4, 6, 8, 10, 12, 14]
VARIANT_PATTERN = re.compile(r"^outline_black_(\d+)px$")
```

Required command line:

```powershell
py -3 scripts\create_head_asset_variants.py --parts-dir "Images&Content\Pins_v2\parts"
```

Required optional command line:

```powershell
py -3 scripts\create_head_asset_variants.py --variant outline_black_6px --parts-dir "Images&Content\Pins_v2\parts"
```

Required implementation details:

```python
def make_variant(source: Image.Image, total_width_px: int) -> Image.Image:
    if total_width_px < 2 or total_width_px % 2 != 0:
        raise ValueError(f"Head outline width must be an even integer >= 2px; got {total_width_px}")

    radius = total_width_px // 2
    rgba = source.convert("RGBA")
    alpha = np.array(rgba.getchannel("A"))
    mask = alpha > 0

    structure = disk(radius)
    outer = ndimage.binary_dilation(mask, structure=structure)
    inner = ndimage.binary_erosion(mask, structure=structure)
    stroke = outer & ~inner

    result = np.array(rgba)
    result[stroke, 0] = 0
    result[stroke, 1] = 0
    result[stroke, 2] = 0
    result[stroke, 3] = 255
    return Image.fromarray(result, "RGBA")
```

Required imports at the top of the file:

```python
from __future__ import annotations

import argparse
import re
import sys
from pathlib import Path

import numpy as np
from PIL import Image
from scipy import ndimage
```

Use the same `make_preview` / grid layout as `create_shaft_asset_variants.py` but write previews to `preview_heads.png`. The `generate` loop should iterate over `pin_XX_head.png` source files (not `_shaft_lit.png`) and end with:

```python
def generate(parts_dir: Path, variants: list[str]) -> None:
    for variant in variants:
        output_dir = parts_dir / "head_variants" / variant
        output_dir.mkdir(parents=True, exist_ok=True)
        preview_images: list[Image.Image] = []

        for index in range(1, PIN_COUNT + 1):
            source_path = parts_dir / f"pin_{index:02d}_head.png"
            source = Image.open(source_path).convert("RGBA")
            total_width_px = int(VARIANT_PATTERN.match(variant).group(1))
            result = make_variant(source, total_width_px)
            result.save(output_dir / f"pin_{index:02d}_head.png", "PNG")
            preview_images.append(result)

        make_preview(preview_images, output_dir / "preview_heads.png")
        print(f"Wrote {PIN_COUNT} heads + preview to {output_dir}")
```

This exact print format is what Task 4 Step 2 validates against.

- [ ] **Step 2: Add a self-test mode**

Add `--self-test` to the argument parser and short-circuit before any file I/O:

```python
def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Generate composite pin head black-outline asset variants.")
    parser.add_argument("--parts-dir", default="Images&Content/Pins_v2/parts")
    parser.add_argument(
        "--variant",
        action="append",
        help="Variant folder name, e.g. outline_black_6px. Repeatable. Default: all widths.",
    )
    parser.add_argument(
        "--self-test",
        action="store_true",
        help="Run built-in correctness check and exit without touching the filesystem.",
    )
    return parser.parse_args()


def main() -> None:
    args = parse_args()
    if args.self_test:
        run_self_test()
        print("Head asset variant self-test passed")
        sys.exit(0)
    variants = args.variant or [f"outline_black_{w}px" for w in DEFAULT_WIDTHS]
    generate(Path(args.parts_dir), variants)
```

Implement `run_self_test` as:

```python
def run_self_test() -> None:
    img = Image.new("RGBA", (9, 9), (0, 0, 0, 0))
    px = img.load()
    for y in range(3, 6):
        for x in range(3, 6):
            px[x, y] = (200, 50, 50, 255)

    result = np.array(make_variant(img, 2))
    black = (
        (result[:, :, 0] == 0)
        & (result[:, :, 1] == 0)
        & (result[:, :, 2] == 0)
        & (result[:, :, 3] == 255)
    )

    assert black[2, 3], "2px stroke must grow one pixel outward"
    assert black[3, 3], "2px stroke must draw one pixel inward"
    assert not black[4, 4], "2px stroke must preserve the interior center"
```

- [ ] **Step 3: Confirm venv has required deps**

Run **before** the self-test to avoid missing-module errors:

```powershell
scripts\venv\Scripts\python.exe -c "import PIL, numpy, scipy; print('image deps ok')"
```

If the venv is missing, run:

```powershell
py -3 -m venv scripts\venv
scripts\venv\Scripts\python.exe -m pip install -r scripts\requirements.txt
```

- [ ] **Step 4: Run self-test**

Run:

```powershell
scripts\venv\Scripts\python.exe scripts\create_head_asset_variants.py --self-test
```

Expected: exits 0 and prints `Head asset variant self-test passed`.

- [ ] **Step 5: Document the script**

In `scripts/README.md`, update the prose line that lists composite-pin asset tooling scripts:

```markdown
**Composite-pin asset tooling** (`split_pin_parts.py`, `create_shaft_asset_variants.py`, `create_head_asset_variants.py`) requires the venv:
```

Add a row to the script catalog table:

```markdown
| `create_head_asset_variants.py` | Manual | venv | Generate black outline head variants (`outline_black_2px` through `outline_black_14px`); writes per-variant `preview_heads.png` grids |
```

### Task 4: Generate and inspect the assets

**Files:**
- Create: `Images&Content/Pins_v2/parts/head_variants/outline_black_2px/`
- Create: `Images&Content/Pins_v2/parts/head_variants/outline_black_4px/`
- Create: `Images&Content/Pins_v2/parts/head_variants/outline_black_6px/`
- Create: `Images&Content/Pins_v2/parts/head_variants/outline_black_8px/`
- Create: `Images&Content/Pins_v2/parts/head_variants/outline_black_10px/`
- Create: `Images&Content/Pins_v2/parts/head_variants/outline_black_12px/`
- Create: `Images&Content/Pins_v2/parts/head_variants/outline_black_14px/`

> **Note:** The venv check was already completed in Task 3 Step 3. Skip Step 1 below if Task 3 is already done in the same session.

- [ ] **Step 1: Confirm Python image dependencies are available**

Run:

```powershell
scripts\venv\Scripts\python.exe -c "import PIL, numpy, scipy; print('image deps ok')"
```

Expected: prints `image deps ok`.

If the venv is missing, run:

```powershell
py -3 -m venv scripts\venv
scripts\venv\Scripts\python.exe -m pip install -r scripts\requirements.txt
```

- [ ] **Step 2: Generate all variants**

Run:

```powershell
scripts\venv\Scripts\python.exe scripts\create_head_asset_variants.py --parts-dir "Images&Content\Pins_v2\parts"
```

Expected: seven lines of output, one per variant folder, each reporting `12 heads + preview`.

- [ ] **Step 3: Verify expected file count**

Run:

```powershell
Get-ChildItem "Images&Content\Pins_v2\parts\head_variants" -Recurse -File -Filter "*.png" |
    Group-Object { $_.Directory.Name } |
    Select-Object Name,Count
```

Expected: every `outline_black_*px` folder has `13` PNG files: 12 heads plus `preview_heads.png`.

- [ ] **Step 4: Visual inspect preview grids**

Open these preview files and compare outline weights:

```text
Images&Content/Pins_v2/parts/head_variants/outline_black_2px/preview_heads.png
Images&Content/Pins_v2/parts/head_variants/outline_black_6px/preview_heads.png
Images&Content/Pins_v2/parts/head_variants/outline_black_10px/preview_heads.png
Images&Content/Pins_v2/parts/head_variants/outline_black_14px/preview_heads.png
```

Acceptance:
- 2px is visible but subtle.
- 6px and 8px are likely default candidates for app review.
- 10px through 14px are intentionally heavy review variants.
- The center of each head remains colored, not fully black.

### Task 5: Wire config docs and optional default

**Files:**
- Modify: `visual-config.json`
- Modify: `docs/guides/VISUAL_CONFIG.md`
- Modify: `docs/TO_DO.md`
- Modify: `CHANGELOG.md`

- [ ] **Step 1: Add config key with no visual behavior change**

In `visual-config.json`, add the key under `PinParts` near `ShaftAssetVariant`:

```json
"HeadAssetVariant": "",
```

This keeps the shipped default on base head art until manual visual review selects a head-outline width.

- [ ] **Step 2: Document the config key**

In `docs/guides/VISUAL_CONFIG.md`, add:

```markdown
- `HeadAssetVariant` - optional folder under `Images&Content/Pins_v2/parts/head_variants/`; when empty, heads load from the base parts folder. Use `outline_black_2px`, `outline_black_4px`, `outline_black_6px`, `outline_black_8px`, `outline_black_10px`, `outline_black_12px`, or `outline_black_14px` to load generated black-outline head assets. Generate variants with `scripts/create_head_asset_variants.py`.
```

- [ ] **Step 3: Update backlog status** — **DEFERRED to Task 6 Step 1a** (must run after `verify.ps1` passes)

- [ ] **Step 4: Add changelog entry**

Under `CHANGELOG.md` -> `[Unreleased]` -> `Changed`, add:

```markdown
- **Pinhead outline variants:** Added generated black-outline head asset variants (`outline_black_2px` through `outline_black_14px`) plus config-gated `PinParts.HeadAssetVariant` support. Default remains base head art until a reviewed variant is selected.
```

> **Note:** Do not flip `docs/TO_DO.md` to `[x]` until `verify.ps1` passes in Task 6. That update has moved to Task 6 Step 1a below.

### Task 6: Verification and completion

**Files:**
- Modify: `docs/exec-plans/active/composite-pins-program.md`
- Modify: `docs/exec-plans/active/README.md`
- Move on completion: `docs/exec-plans/active/pinhead-black-outline-variants-plan.md` to `docs/exec-plans/completed/pinhead-black-outline-variants-plan.md`

- [ ] **Step 1: Run full verification**

Run:

```powershell
.\scripts\verify.ps1
```

Expected: build, unit tests, doc links, taste checks, and startup validation pass.

> **verify.ps1 scope note:** The script runs `dotnet build/test`, NuGet vuln scan, doc link check, `verify_taste.py` (checks `.cs` file sizes, `Console.WriteLine`, `JObject` in Views, active plan staleness), and headless startup validation. None of these steps check for or whitelist specific subdirectories under `Images&Content/`. The new `head_variants/` PNG assets will not cause any `verify.ps1` step to fail.

- [ ] **Step 1a: Update backlog status (after verify passes)**

Change the `docs/TO_DO.md` pinhead item to:

```markdown
- [x] Add pinhead variants with black outlines - generated `outline_black_2px`, `outline_black_4px`, `outline_black_6px`, `outline_black_8px`, `outline_black_10px`, `outline_black_12px`, and `outline_black_14px` under `Images&Content/Pins_v2/parts/head_variants/`
```

- [ ] **Step 2: Review changed files**

Run:

```powershell
git status --short
git diff --stat
```

Expected changed scope:
- one new generator script
- new generated head variant assets
- focused config/render-plan/cache tests
- focused config/render-plan/cache implementation
- docs and changelog updates

- [ ] **Step 3: Archive the plan**

Move this file to:

```text
docs/exec-plans/completed/pinhead-black-outline-variants-plan.md
```

Update `docs/exec-plans/active/README.md` by removing the active row and adding a completed row:

```markdown
- `pinhead-black-outline-variants-plan.md` - generated black-outline head variants and config-gated `HeadAssetVariant`; `verify.ps1` passed 2026-06-12
```

Update `docs/exec-plans/active/composite-pins-program.md` with status `Complete` and next action `Review/select default head variant`.

- [ ] **Step 4: Commit**

Run:

```powershell
git add Models/PinPartConfig.cs Services/CompositePinRenderPlanBuilder.cs Services/CompositePinPlanningService.cs Services/CompositePinLayoutContentHasher.cs Tests/CompositePinRenderPlanBuilderTests.cs Tests/CompositePinPlanningServiceTests.cs Tests/CompositePinLayoutContentHasherTests.cs Tests/VisualConfigServiceTests.cs scripts/create_head_asset_variants.py scripts/README.md visual-config.json "Images&Content/Pins_v2/parts/head_variants" docs/TO_DO.md docs/guides/VISUAL_CONFIG.md docs/exec-plans/active/composite-pins-program.md docs/exec-plans/active/README.md docs/exec-plans/completed/pinhead-black-outline-variants-plan.md CHANGELOG.md
git commit -m "Add pinhead outline variants"
git push
```

Expected: commit succeeds and branch pushes to `origin/WIP`.
