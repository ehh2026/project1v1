---
status: active
owner: agent
started: 2026-06-10
requirements_ref: composite-pin-shaft-visibility
parent_program: composite-pins-program.md
source_assessment: ../../assessments/COMPOSITE_PIN_SHAFT_VISIBILITY_ASSESSMENT.md
---

# Composite Pin Shaft Visibility Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Improve composite pin shaft and stub legibility against busy map backgrounds while keeping per-marker runtime cost close to the current bitmap rendering path.

**Architecture:** Prefer baked shaft asset variants over repeated runtime image processing. Runtime code should only select a configured shaft asset set and then keep using the existing `CompositePinRenderPlanBuilder` and `CompositePinMarker` bitmap transform pipeline. Keep a cheap runtime halo/tint path out of the MVP unless asset variants fail visual review.

**Tech Stack:** WPF / .NET 6 / C#, `visual-config.json`, `PinPartConfig`, `CompositePinRenderPlanBuilder`, Pillow-based optional asset tooling in `scripts/`.

---

## Source Assessment

This plan implements the production-oriented path from [COMPOSITE_PIN_SHAFT_VISIBILITY_ASSESSMENT.md](../../assessments/COMPOSITE_PIN_SHAFT_VISIBILITY_ASSESSMENT.md):

- The issue is contrast / figure-ground separation, not remaining pixelation.
- The weak elements are thin gray shafts and short screen-up stubs over dark labels, blue/green map features, and textured beige areas.
- Asset variants have a runtime advantage because outline/contrast work is paid during asset generation and image decode, not per marker.
- Runtime halo/tint remains useful for exploration, but it is not the preferred MVP if a baked asset style works.

## File Responsibilities

| File | Responsibility |
|------|----------------|
| `scripts/create_shaft_asset_variants.py` | Generate baked shaft variants from existing `Pins_v2/parts/*_shaft_lit.png` assets and write preview grids. |
| `Images&Content/Pins_v2/parts/shaft_variants/outline_dark/*.png` | Conservative dark-outline shaft assets using the original lit shaft as the interior. |
| `Images&Content/Pins_v2/parts/shaft_variants/outline_dark_bold/*.png` | Slightly stronger dark-outline shaft assets for stubs and dense labels. |
| `Models/PinPartConfig.cs` | Add config for selecting a baked shaft variant folder. Keep existing `UseLitShafts` behavior as the default fallback. |
| `Services/CompositePinRenderPlanBuilder.cs` | Resolve the configured shaft asset path without changing head selection or geometry math. |
| `Services/CompositePinLayoutContentHasher.cs` | Include shaft variant config in the render-plan cache key. |
| `Tests/VisualConfigServiceTests.cs` | Verify config deserialization and omitted default. |
| `Tests/CompositePinRenderPlanBuilderTests.cs` | Verify configured variant paths are used for shaft layers. |
| `Tests/CompositePinPlanCacheTests.cs` or `Tests/CompositePinLayoutContentHasherTests.cs` | Verify cache config hash changes when the shaft variant changes. |
| `visual-config.json` | Add the new field, initially empty unless a candidate is enabled for app review. |
| `docs/guides/VISUAL_CONFIG.md` | Document the new setting and expected folder layout. |

## Design Decisions

### Shaft Variant Naming

Use a folder-based asset variant instead of adding more filename suffixes:

```text
Images&Content/Pins_v2/parts/
  pin_01_shaft.png
  pin_01_shaft_lit.png
  shaft_variants/
    outline_dark/
      pin_01_shaft.png
      ...
      pin_12_shaft.png
      preview_shafts.png
    outline_dark_bold/
      pin_01_shaft.png
      ...
      pin_12_shaft.png
      preview_shafts.png
```

Rationale:

- The base geometry metadata can keep using `shaft_file: "pin_01_shaft.png"`.
- Heads stay in `Pins_v2/parts`; only shafts are redirected.
- Cache keys can treat the variant folder name as the relevant render-plan input.
- `UseLitShafts` remains backward compatible for the existing `_lit` files.

### Config Contract

Add one string field:

```csharp
public string ShaftAssetVariant { get; set; } = string.Empty;
```

Resolution rule:

1. If `ShaftAssetVariant` is non-empty, load `PinParts.PartsFolderPath/shaft_variants/<ShaftAssetVariant>/<geometry.ShaftFile>`.
2. Else if `UseLitShafts` is true, load the existing `_lit` filename.
3. Else load the existing base shaft filename.

Do not make `ShaftAssetVariant` an enum in the MVP. Asset folders are content, not compiled app behavior, and a string keeps visual experiments cheap.

## Phase 1 - Config and Runtime Path Selection

**Deliverable:** Runtime can select a baked shaft variant folder with no per-marker image processing.

### Files

| Action | Path |
|--------|------|
| Modify | `Models/PinPartConfig.cs` |
| Modify | `Services/CompositePinRenderPlanBuilder.cs` |
| Modify | `Services/CompositePinLayoutContentHasher.cs` |
| Modify | `Tests/VisualConfigServiceTests.cs` |
| Modify | `Tests/CompositePinRenderPlanBuilderTests.cs` |
| Modify | `Tests/CompositePinPlanCacheTests.cs` or create `Tests/CompositePinLayoutContentHasherTests.cs` |

### Steps

- [x] **Step 1: Add failing config tests**

Add these tests to `Tests/VisualConfigServiceTests.cs` near the existing `PinParts` tests:

```csharp
[Fact]
public void Load_PinPartsShaftAssetVariant_Deserializes()
{
    var tempDir = CreateTempDir();
    try
    {
        var path = Path.Combine(tempDir, "visual-config.json");
        File.WriteAllText(path, @"{ ""PinParts"": { ""ShaftAssetVariant"": ""outline_dark"" } }");
        var service = new VisualConfigService();

        var config = service.Load(path);

        Assert.Equal("outline_dark", config.PinParts.ShaftAssetVariant);
    }
    finally
    {
        Directory.Delete(tempDir, recursive: true);
    }
}

[Fact]
public void Load_PinPartsShaftAssetVariant_UsesDefaultWhenOmitted()
{
    var tempDir = CreateTempDir();
    try
    {
        var path = Path.Combine(tempDir, "visual-config.json");
        File.WriteAllText(path, @"{ ""PinParts"": { ""Enabled"": true } }");
        var service = new VisualConfigService();

        var config = service.Load(path);

        Assert.Equal(string.Empty, config.PinParts.ShaftAssetVariant);
    }
    finally
    {
        Directory.Delete(tempDir, recursive: true);
    }
}
```

Run:

```powershell
dotnet test Tests/InteractiveWorldMap.Tests.csproj --filter "FullyQualifiedName~VisualConfigServiceTests" -c Release
```

Expected: the new tests fail because `PinPartConfig.ShaftAssetVariant` does not exist.

- [x] **Step 2: Add failing render-plan path test**

Add this test to `Tests/CompositePinRenderPlanBuilderTests.cs`:

```csharp
[Fact]
public void BuildPlan_WhenShaftAssetVariantConfigured_UsesVariantShaftPath()
{
    var builder = new CompositePinRenderPlanBuilder();
    var target = new PinPlacementTarget
    {
        StartScreen = new Point(100, 320),
        EndScreen = new Point(100, 100),
        LocationId = "loc-variant",
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
        UseLitShafts = true,
        ShaftAssetVariant = "outline_dark"
    };

    var plan = builder.BuildPlan(target, placement, config);

    Assert.Equal(@"Pins_v2/parts\shaft_variants\outline_dark\pin_a_shaft.png", plan.ShaftSourcePath);
    Assert.Equal(plan.ShaftSourcePath, plan.ShaftTipCapLayer.SourcePath);
    Assert.Equal(plan.ShaftSourcePath, plan.ShaftBodyLayer.SourcePath);
    Assert.Equal(plan.ShaftSourcePath, plan.ShaftHeadCapLayer.SourcePath);
    Assert.Equal(@"Pins_v2/parts\pin_a_head.png", plan.HeadSourcePath);
}
```

Run:

```powershell
dotnet test Tests/InteractiveWorldMap.Tests.csproj --filter "FullyQualifiedName~CompositePinRenderPlanBuilderTests" -c Release
```

Expected: the new test fails because runtime path selection does not know about `ShaftAssetVariant`.

- [x] **Step 3: Add failing cache-hash test**

Create `Tests/CompositePinLayoutContentHasherTests.cs`:

```csharp
using InteractiveWorldMap.Models;
using InteractiveWorldMap.Services;
using Xunit;

namespace InteractiveWorldMap.Tests;

public class CompositePinLayoutContentHasherTests
{
    [Fact]
    public void ComputeConfigHash_Changes_WhenShaftAssetVariantChanges()
    {
        var baseConfig = new PinPartConfig
        {
            TargetHeadRadiusPx = 8.0,
            TargetShaftHalfWidthPx = 1.75,
            UseLitShafts = true,
            ShaftAssetVariant = string.Empty
        };
        var variantConfig = new PinPartConfig
        {
            TargetHeadRadiusPx = 8.0,
            TargetShaftHalfWidthPx = 1.75,
            UseLitShafts = true,
            ShaftAssetVariant = "outline_dark"
        };

        Assert.NotEqual(
            CompositePinLayoutContentHasher.ComputeConfigHash(baseConfig),
            CompositePinLayoutContentHasher.ComputeConfigHash(variantConfig));
    }
}
```

Run:

```powershell
dotnet test Tests/InteractiveWorldMap.Tests.csproj --filter "FullyQualifiedName~CompositePinLayoutContentHasherTests" -c Release
```

Expected: the test fails until the config field and hash input are added.

- [x] **Step 4: Implement `PinPartConfig.ShaftAssetVariant`**

Add this property after `UseLitShafts` in `Models/PinPartConfig.cs`:

```csharp
/// <summary>
/// Optional shaft-only asset variant folder under PartsFolderPath/shaft_variants.
/// When empty, the renderer uses the existing UseLitShafts/base shaft filename behavior.
/// Example: "outline_dark" resolves pin_01_shaft.png to
/// Pins_v2/parts/shaft_variants/outline_dark/pin_01_shaft.png.
/// </summary>
public string ShaftAssetVariant { get; set; } = string.Empty;
```

- [x] **Step 5: Implement shaft path resolution**

In `Services/CompositePinRenderPlanBuilder.cs`, replace the inline `shaftFile` / `shaftPath` logic in `AssembleResult` with a helper:

```csharp
var shaftPath = ResolveShaftPath(config, geometry);
var headPath  = Path.Combine(config.PartsFolderPath, v.HeadEntry.HeadFile);
```

Add the helper near the low-level geometry helpers:

```csharp
private static string ResolveShaftPath(PinPartConfig config, PinPartGeometryEntry geometry)
{
    if (!string.IsNullOrWhiteSpace(config.ShaftAssetVariant))
    {
        return Path.Combine(
            config.PartsFolderPath,
            "shaft_variants",
            config.ShaftAssetVariant.Trim(),
            geometry.ShaftFile);
    }

    var shaftFile = config.UseLitShafts
        ? geometry.ShaftFile.Replace(".png", "_lit.png")
        : geometry.ShaftFile;

    return Path.Combine(config.PartsFolderPath, shaftFile);
}
```

- [x] **Step 6: Include the shaft variant in the cache hash**

In `Services/CompositePinLayoutContentHasher.cs`, add the variant to `ComputeConfigHash`:

```csharp
$"{config.UseLitShafts}:" +
$"{config.ShaftAssetVariant}";
```

Run:

```powershell
dotnet test Tests/InteractiveWorldMap.Tests.csproj --filter "FullyQualifiedName~VisualConfigServiceTests|FullyQualifiedName~CompositePinRenderPlanBuilderTests|FullyQualifiedName~CompositePinLayoutContentHasherTests" -c Release
```

Expected: all targeted tests pass.

## Phase 2 - Generate Baked Shaft Variant Assets

**Deliverable:** Two candidate low-runtime-cost shaft asset sets exist with preview grids.

### Files

| Action | Path |
|--------|------|
| Create | `scripts/create_shaft_asset_variants.py` |
| Create | `Images&Content/Pins_v2/parts/shaft_variants/outline_dark/pin_01_shaft.png` through `pin_12_shaft.png` |
| Create | `Images&Content/Pins_v2/parts/shaft_variants/outline_dark_bold/pin_01_shaft.png` through `pin_12_shaft.png` |
| Create | `Images&Content/Pins_v2/parts/shaft_variants/outline_dark/preview_shafts.png` |
| Create | `Images&Content/Pins_v2/parts/shaft_variants/outline_dark_bold/preview_shafts.png` |
| Modify | `scripts/README.md` |

### Steps

- [x] **Step 1: Add the asset generation script**

Create `scripts/create_shaft_asset_variants.py` with these concrete behaviors:

```python
from __future__ import annotations

import argparse
from pathlib import Path
from PIL import Image, ImageChops, ImageFilter, ImageOps

PIN_COUNT = 12


def alpha_outline(source: Image.Image, radius: int, color: tuple[int, int, int, int]) -> Image.Image:
    rgba = source.convert("RGBA")
    alpha = rgba.getchannel("A")
    grown = alpha.filter(ImageFilter.MaxFilter((radius * 2) + 1))
    outline_alpha = ImageChops.subtract(grown, alpha)
    outline = Image.new("RGBA", rgba.size, color)
    outline.putalpha(ImageChops.multiply(outline_alpha, Image.new("L", rgba.size, color[3])))
    return Image.alpha_composite(outline, rgba)


def boost_contrast(source: Image.Image, amount: float) -> Image.Image:
    rgba = source.convert("RGBA")
    rgb = ImageOps.autocontrast(rgba.convert("RGB"))
    blended = Image.blend(rgba.convert("RGB"), rgb, amount)
    result = Image.merge("RGBA", (*blended.split(), rgba.getchannel("A")))
    return result


def make_variant(source: Image.Image, name: str) -> Image.Image:
    if name == "outline_dark":
        contrasted = boost_contrast(source, 0.18)
        return alpha_outline(contrasted, radius=1, color=(32, 28, 22, 170))
    if name == "outline_dark_bold":
        contrasted = boost_contrast(source, 0.28)
        return alpha_outline(contrasted, radius=2, color=(24, 22, 18, 210))
    raise ValueError(f"Unknown variant: {name}")


def make_preview(images: list[Image.Image], output_path: Path) -> None:
    cell_w = max(img.width for img in images)
    cell_h = max(img.height for img in images)
    cols = 4
    rows = (len(images) + cols - 1) // cols
    preview = Image.new("RGBA", (cols * cell_w, rows * cell_h), (238, 229, 199, 255))
    for index, img in enumerate(images):
        x = (index % cols) * cell_w + ((cell_w - img.width) // 2)
        y = (index // cols) * cell_h + ((cell_h - img.height) // 2)
        preview.alpha_composite(img, (x, y))
    preview.save(output_path, "PNG")


def generate(parts_dir: Path, variants: list[str]) -> None:
    for variant in variants:
        output_dir = parts_dir / "shaft_variants" / variant
        output_dir.mkdir(parents=True, exist_ok=True)
        preview_images: list[Image.Image] = []
        for index in range(1, PIN_COUNT + 1):
            source_path = parts_dir / f"pin_{index:02d}_shaft_lit.png"
            if not source_path.exists():
                source_path = parts_dir / f"pin_{index:02d}_shaft.png"
            source = Image.open(source_path).convert("RGBA")
            result = make_variant(source, variant)
            result.save(output_dir / f"pin_{index:02d}_shaft.png", "PNG")
            preview_images.append(result)
        make_preview(preview_images, output_dir / "preview_shafts.png")


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Generate composite pin shaft visibility asset variants.")
    parser.add_argument("--parts-dir", default="Images&Content/Pins_v2/parts")
    parser.add_argument("--variant", action="append", choices=["outline_dark", "outline_dark_bold"])
    return parser.parse_args()


def main() -> None:
    args = parse_args()
    variants = args.variant or ["outline_dark", "outline_dark_bold"]
    generate(Path(args.parts_dir), variants)


if __name__ == "__main__":
    main()
```

Run:

```powershell
.\scripts\venv\Scripts\python.exe scripts\create_shaft_asset_variants.py
```

Expected:

- `Images&Content/Pins_v2/parts/shaft_variants/outline_dark/` contains 12 shaft PNGs plus `preview_shafts.png`.
- `Images&Content/Pins_v2/parts/shaft_variants/outline_dark_bold/` contains 12 shaft PNGs plus `preview_shafts.png`.

- [x] **Step 2: Document the script**

Add this row to `scripts/README.md`:

```markdown
| `create_shaft_asset_variants.py` | Manual | venv | Generate low-runtime-cost composite shaft contrast variants and preview grids |
```

- [x] **Step 3: Verify generated assets are complete**

Run:

```powershell
rg --files "Images&Content/Pins_v2/parts/shaft_variants" | rg "pin_[0-9][0-9]_shaft\.png|preview_shafts\.png"
```

Expected: 26 files are listed: 12 shaft files plus one preview per variant.

## Phase 3 - Config, Docs, and Candidate Selection

**Deliverable:** The app can be run with either candidate variant through config, and the setting is documented.

### Files

| Action | Path |
|--------|------|
| Modify | `visual-config.json` |
| Modify | `docs/guides/VISUAL_CONFIG.md` |
| Modify | `CHANGELOG.md` |

### Steps

- [x] **Step 1: Add the config field without forcing a default switch**

Add this field near `UseLitShafts` in `visual-config.json`:

```json
"ShaftAssetVariant": "",
```

Manual review can set it to one of:

```json
"ShaftAssetVariant": "outline_dark"
```

or:

```json
"ShaftAssetVariant": "outline_dark_bold"
```

Acceptance: leaving the field empty preserves the current `_lit` behavior when `UseLitShafts` is true.

- [x] **Step 2: Document the field**

In `docs/guides/VISUAL_CONFIG.md`, extend the PinParts section with:

```markdown
- `ShaftAssetVariant` - optional folder under `Images&Content/Pins_v2/parts/shaft_variants/`.
  When empty, shaft selection follows `UseLitShafts`; when set to `outline_dark` or
  `outline_dark_bold`, composite pins use the baked shaft variant while heads remain
  loaded from the base parts folder.
```

- [x] **Step 3: Add implementation changelog note**

Under `[Unreleased]`, add:

```markdown
- **Composite shaft visibility:** Added config-gated baked shaft asset variants for improving shaft/stub contrast without repeated runtime image processing.
```

## Phase 4 - Verification and Visual Review

**Deliverable:** The selected approach is verified by automated checks and screenshot review on text-heavy map regions.

### Steps

- [x] **Step 1: Run targeted tests**

```powershell
dotnet test Tests/InteractiveWorldMap.Tests.csproj --filter "FullyQualifiedName~VisualConfigServiceTests|FullyQualifiedName~CompositePinRenderPlanBuilderTests|FullyQualifiedName~CompositePinLayoutContentHasherTests" -c Release
```

Expected: all targeted tests pass.

- [x] **Step 2: Run full verification**

```powershell
.\scripts\verify.ps1
```

Expected: build, unit tests, doc links, taste checks, and headless startup validation pass.

- [ ] **Step 3: Capture visual comparison**

Manual review on Windows:

1. Set `PinParts.ShaftAssetVariant` to `""`, launch the app, and capture the problem area from the assessment.
2. Set `PinParts.ShaftAssetVariant` to `"outline_dark"`, relaunch, and capture the same area.
3. Set `PinParts.ShaftAssetVariant` to `"outline_dark_bold"`, relaunch, and capture the same area.
4. Compare the short stubs and long shafts over dark labels, beige texture, blue rivers, and green map areas.

Acceptance criteria:

- Stubs remain visible over dark city-label text.
- Long shafts remain visible over beige and blue/green map areas.
- Dense clusters do not become muddy or visually dominant.
- Heads remain unchanged.
- No new per-marker runtime bitmap processing is added.

- [ ] **Step 4: Choose the default**

If a candidate clearly wins, update `visual-config.json`:

```json
"ShaftAssetVariant": "outline_dark"
```

or:

```json
"ShaftAssetVariant": "outline_dark_bold"
```

If neither candidate is acceptable, leave `ShaftAssetVariant` empty and extend this plan with a narrow runtime halo prototype using the assessment's `ShaftHaloEnabled`, `ShaftHaloColor`, `ShaftHaloThicknessPx`, and `ShaftHaloOpacity` fields.

## Out of Scope

- Adaptive map-pixel luminance sampling.
- Runtime blur or dilation effects applied per marker.
- Changing head images.
- Reworking composite pin geometry or depth sorting.
- Applying composite pins to unzoomed cluster aggregate markers.

## Completion Criteria

- `ShaftAssetVariant` is config-gated and backward compatible.
- Render-plan cache keys change when the shaft variant changes.
- At least one complete baked shaft variant set exists for all 12 pin shafts.
- Visual review selects a default or records why asset variants were insufficient.
- `.\scripts\verify.ps1` passes.
- `docs/exec-plans/active/composite-pins-program.md`, `docs/TO_DO.md`, `docs/guides/VISUAL_CONFIG.md`, and `CHANGELOG.md` reflect the implemented outcome.
