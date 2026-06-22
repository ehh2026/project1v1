---
status: completed
owner: agent
started: 2026-06-12
completed: 2026-06-21
parent_program: composite-pins-program.md
parent_plan: pin-parts-composite-placement-plan.md
parallel_plan: runtime-tuning-panel-plan.md
---

# Pinhead Black Outline Variants Implementation Plan

**Completed 2026-06-21.** Generated `outline_black_2px` through `outline_black_14px` under `Images&Content/Pins_v2/parts/head_variants/` (91 PNGs). Added `scripts/create_head_asset_variants.py`, planning-service filename head matching, `HeadAssetVariant` in `visual-config.json` (default empty), and docs. `verify.ps1` passed (370 tests).

> **Historical:** Task checklist below documents the implementation sequence.

**Goal:** Generate black-outline composite pin head variants at 2, 4, 6, 8, 10, 12, and 14 px total stroke width, with each stroke split half outside and half inside the detected pinhead edge, and make the variants selectable through visual config.

**Architecture:** Add a head-only asset generator parallel to the existing shaft-variant tooling. Store generated assets under `Images&Content/Pins_v2/parts/head_variants/<variant>/`, add `PinParts.HeadAssetVariant` as a config-only selector, and route composite render plans through a small head path resolver. Manual layout head selection continues to select the pin head identity by file name; the active config controls whether that identity uses base head art or a head-variant folder.

**Tech Stack:** WPF / .NET 6 / C# for render-plan config and tests; Python 3 with Pillow, numpy, and scipy from `scripts/requirements.txt` for deterministic PNG asset generation.

---

## Current progress (2026-06-21) — COMPLETE

| Task | Status | Notes |
|------|--------|-------|
| Task 1 — config + render-plan path | **Complete** | `HeadAssetVariant`, `ResolveHeadPath()`, render-plan + visual-config tests |
| Task 2 — planning + cache hash | **Complete** | Filename-based head matching + planning tests; hash via runtime tuning panel |
| Task 3 — Python generator | **Complete** | `scripts/create_head_asset_variants.py` with self-test |
| Task 4 — generated assets | **Complete** | Seven variant folders × 13 PNGs (91 total) |
| Task 5 — config docs | **Complete** | `visual-config.json`, `VISUAL_CONFIG.md`, `CHANGELOG.md` |
| Task 6 — verify + archive | **Complete** | `verify.ps1` passed 2026-06-21 |

### Related work (out of scope for this plan)

- [runtime-tuning-panel-plan.md](../completed/runtime-tuning-panel-plan.md) — debug panel already reads/writes `PinParts.HeadAssetVariant` via free-text `TxtHeadVariant`. Dropdown pickers are tracked separately in [TO_DO.md](../../TO_DO.md) (Developer tooling).
- Selecting a default head-outline width after visual review is a follow-up human decision; shipped default remains empty `HeadAssetVariant` (base head art).

---

## Requirements

1. Generate variants named `outline_black_2px`, `outline_black_4px`, `outline_black_6px`, `outline_black_8px`, `outline_black_10px`, `outline_black_12px`, and `outline_black_14px`.
2. For each total width `N`, draw `N / 2` px outward from the detected head alpha silhouette and `N / 2` px inward from that silhouette (approximated via symmetric disk morphology; see Variant Algorithm).
3. Generate one PNG per existing base head file: `pin_01_head.png` through `pin_12_head.png`.
4. Generate `preview_heads.png` in each variant folder for quick visual comparison.
5. Add optional config key `PinParts.HeadAssetVariant`, resolving `pin_01_head.png` to `Pins_v2/parts/head_variants/<variant>/pin_01_head.png` when non-empty.
6. Keep current behavior when `HeadAssetVariant` is empty.
7. Include `HeadAssetVariant` in composite layout config hashing so changing variants invalidates stale cached render plans.

## File responsibilities

| File | Status | Responsibility |
|------|--------|----------------|
| `Models/PinPartConfig.cs` | Done | `HeadAssetVariant` property beside `ShaftAssetVariant` |
| `Services/CompositePinRenderPlanBuilder.cs` | Done | `ResolveHeadPath(config, geometry)` beside `ResolveShaftPath` |
| `Services/CompositePinPlanningService.cs` | Pending | Match saved `HeadSourcePath` by **file name** so variant-folder paths resolve |
| `Services/CompositePinLayoutContentHasher.cs` | Done | Include `HeadAssetVariant` in `ComputeConfigHash` |
| `Tests/CompositePinRenderPlanBuilderTests.cs` | Done | Variant head path in render plan |
| `Tests/VisualConfigServiceTests.cs` | Done | Deserialize / default for `HeadAssetVariant` |
| `Tests/CompositePinLayoutContentHasherTests.cs` | Done | Hash changes when `HeadAssetVariant` changes |
| `Tests/CompositePinPlanningServiceTests.cs` | Pending | Variant-folder and base-folder preferred head paths |
| `scripts/create_head_asset_variants.py` | Pending | Head-only asset generator + preview grids |
| `Images&Content/Pins_v2/parts/head_variants/` | Pending | Seven variant folders × 13 PNGs each |
| `visual-config.json` | Pending | Add `"HeadAssetVariant": ""` under `PinParts` |
| `docs/guides/VISUAL_CONFIG.md` | Pending | Document the new key |
| `scripts/README.md` | Pending | Catalog the new script |

## Modularity / file size impact

- Do not change `MainWindow.xaml.cs` for this work. The runtime tuning panel (`Views/DeveloperTuningPanel.xaml`) already exposes `HeadAssetVariant`; dropdown UX is a separate backlog item.
- Keep `CompositePinRenderPlanBuilder.cs` changes limited to path resolution (already done). If the file nears the 800-line repo limit, split path resolution into a focused helper before adding more logic.
- Keep `CompositePinPlanningService.cs` changes focused on source-path matching only; do not move render-plan path construction into the planning service.
- Keep the Python generator self-contained and below 300 lines. If shared morphology helpers are needed later, extract them to `scripts/pin_asset_masks.py` instead of expanding both asset generator scripts independently.

## Variant algorithm

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
rgba = np.array(source.convert("RGBA"))
rgba[stroke] = [0, 0, 0, 255]
```

7. Save the result as `pin_XX_head.png` in the matching variant folder.

This intentionally blacks both the outward halo and inward edge band. The untouched interior remains the original head art. Disk morphology is an approximation of “half in / half out” for irregular silhouettes; visual review in Task 4 validates acceptability.

---

## Tasks

### Task 1: Add config and render-plan path coverage — COMPLETE

Verified in codebase (2026-06-21):

- `Models/PinPartConfig.cs` — `HeadAssetVariant` property
- `Services/CompositePinRenderPlanBuilder.cs` — `ResolveHeadPath()` helper and call site
- `Tests/CompositePinRenderPlanBuilderTests.cs` — `BuildPlan_WhenHeadAssetVariantConfigured_UsesVariantHeadPath`
- `Tests/VisualConfigServiceTests.cs` — `Load_PinPartsHeadAssetVariant_Deserializes`, `Load_PinPartsHeadAssetVariant_UsesDefaultWhenOmitted`

- [x] **Step 1:** Render-plan test for variant head path
- [x] **Step 2:** Visual-config deserialize / default tests
- [x] **Step 3:** Config property on `PinPartConfig`
- [x] **Step 4:** `ResolveHeadPath()` in render-plan builder
- [x] **Step 5:** Focused tests pass

Reference — render-plan test:

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

    var expectedHead = System.IO.Path.Combine(
        config.PartsFolderPath, "head_variants", "outline_black_6px", "pin_a_head.png");
    Assert.Equal(expectedHead, plan.HeadSourcePath);
    Assert.Equal(plan.HeadSourcePath, plan.HeadLayer.SourcePath);
    Assert.Equal(System.IO.Path.Combine(config.PartsFolderPath, "pin_a_shaft.png"), plan.ShaftSourcePath);
}
```

> **Cross-platform paths:** Build expected paths with `Path.Combine` (same as production code). Do not hardcode `\` in assertions — `Path.Combine` uses `/` on macOS/Linux and `dotnet test` runs there per [AGENTS.md](../../../AGENTS.md). Existing shaft/head render-plan tests in `CompositePinRenderPlanBuilderTests.cs` still use Windows-style verbatim strings; new tests in this plan should use `Path.Combine`.

### Task 2: Keep manual layout head identity and cache hashing correct — IN PROGRESS

**Why:** Saved manual layouts store `HeadSourcePath` values that may point at `head_variants/<variant>/pin_XX_head.png`. `ResolveHeadGeometry` must match by **head file name** (`pin_b_head.png`), not the full combined path, so variant folders do not break head identity replay.

**Already done** (landed via runtime tuning panel, 2026-06-21):

- [x] **Step 2:** `ComputeConfigHash_Changes_WhenHeadAssetVariantChanges` in `Tests/CompositePinLayoutContentHasherTests.cs`
- [x] **Step 5:** `HeadAssetVariant` appended to `ComputeConfigHash` in `Services/CompositePinLayoutContentHasher.cs`

**Remaining:**

**Files:**

- Modify: `Tests/CompositePinPlanningServiceTests.cs`
- Modify: `Services/CompositePinPlanningService.cs`
- Modify: `docs/exec-plans/active/composite-pins-program.md` (dashboard row)

- [ ] **Step 1: Add failing planning-service tests**

Add **two** tests to `Tests/CompositePinPlanningServiceTests.cs` (after the existing preferred-pair tests; `MakeService` / `MakeFixture` helpers already exist):

Test 1 — variant-folder path resolves to matching geometry:

```csharp
[Fact]
public void BuildPlan_WhenPreferredHeadPathUsesVariantFolder_SelectsMatchingHeadGeometry()
{
    var service = MakeService();
    var (target, candidates, config) = MakeFixture("loc-preferred-variant-head");
    candidates["pin_b"] = CloneGeometry(candidates["pin_a"], "pin_b", "pin_b_head.png");
    config.HeadAssetVariant = "outline_black_6px";

    var preferredHead = System.IO.Path.Combine(
        config.PartsFolderPath, "head_variants", "outline_black_6px", "pin_b_head.png");

    var result = service.BuildPlan(
        target,
        candidates,
        config,
        preferredPairId: "pin_a",
        preferredHeadSourcePath: preferredHead);

    var expectedHead = System.IO.Path.Combine(
        config.PartsFolderPath, "head_variants", "outline_black_6px", "pin_b_head.png");
    Assert.Equal(expectedHead, result.RenderPlan.HeadSourcePath);
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

    var preferredHead = System.IO.Path.Combine(config.PartsFolderPath, "pin_b_head.png");

    var result = service.BuildPlan(
        target,
        candidates,
        config,
        preferredPairId: "pin_a",
        preferredHeadSourcePath: preferredHead);

    Assert.Equal(preferredHead, result.RenderPlan.HeadSourcePath);
}
```

Add helper near `MakeFixture`:

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

> **Note:** Tests are pure path-string assertions; outlined PNG assets do not need to exist on disk.

- [x] **Step 2: Add cache-hash test** *(done — runtime tuning panel)*

- [ ] **Step 3: Run focused tests and confirm planning tests fail**

Run:

```powershell
dotnet test Tests/InteractiveWorldMap.Tests.csproj --filter "FullyQualifiedName~CompositePinPlanningServiceTests|FullyQualifiedName~CompositePinLayoutContentHasherTests" --no-restore
```

Expected: hash test **passes**; the two new planning-service tests **fail** because `ResolveHeadGeometry` still matches full paths.

- [ ] **Step 4: Match preferred head paths by file name**

In `Services/CompositePinPlanningService.cs`, inside `ResolveHeadGeometry`, replace:

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

- [x] **Step 5: Include head variant in config hash** *(done — runtime tuning panel)*

Current `ComputeConfigHash` ends with `$"{config.ShaftAssetVariant}:{config.HeadAssetVariant}"`. No further change needed.

- [ ] **Step 6: Update program dashboard**

In [composite-pins-program.md](../active/composite-pins-program.md), change the Head visibility row from `Planned` to `In Progress` with next action `Finish Task 2 planning tests; generate head variants (Tasks 3–4)`.

- [ ] **Step 7: Run focused tests and confirm pass**

Same filter as Step 3. Expected: **PASS** (all four tests).

### Task 3: Add the head-outline asset generator

**Files:**

- Create: `scripts/create_head_asset_variants.py`
- Modify: `scripts/README.md`

> **Dependency note:** Uses `scipy.ndimage` for binary dilation/erosion. Shaft tooling uses PIL-only filters. Both share `scripts/venv` and `scripts/requirements.txt` (scipy already listed). Do **not** add scipy again.

- [ ] **Step 1: Create the generator script**

Create `scripts/create_head_asset_variants.py` with the structure below. Mirror the entry-point layout of `create_shaft_asset_variants.py` (`parse_args` → `main` → `if __name__ == "__main__"`).

**Required imports** (top of file, before any functions):

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

**Definition order:** module docstring → imports → constants → helpers → `make_variant` → `generate` → `run_self_test` → `parse_args` → `main` → `if __name__ == "__main__": main()`.

Required constants:

```python
PIN_COUNT = 12
DEFAULT_WIDTHS = [2, 4, 6, 8, 10, 12, 14]
VARIANT_PATTERN = re.compile(r"^outline_black_(\d+)px$")
```

Required helpers (include all three in the script):

```python
def disk(radius: int) -> np.ndarray:
    yy, xx = np.ogrid[-radius:radius + 1, -radius:radius + 1]
    return (xx * xx + yy * yy) <= radius * radius


def parse_variant_width(variant: str) -> int:
    match = VARIANT_PATTERN.match(variant)
    if not match:
        raise ValueError(
            f"Unknown variant {variant!r}; expected outline_black_<even_N>px "
            f"(e.g. outline_black_6px)"
        )
    return int(match.group(1))


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
```

Required `make_variant`:

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
    result[stroke] = [0, 0, 0, 255]
    return Image.fromarray(result, "RGBA")
```

> **Note:** Prefer `result[stroke] = [0, 0, 0, 255]` over four channel assignments — equivalent output, cleaner numpy.

Required `generate` loop (sources are base `pin_XX_head.png`, not shaft files):

```python
def generate(parts_dir: Path, variants: list[str]) -> None:
    for variant in variants:
        total_width_px = parse_variant_width(variant)
        output_dir = parts_dir / "head_variants" / variant
        output_dir.mkdir(parents=True, exist_ok=True)
        preview_images: list[Image.Image] = []

        for index in range(1, PIN_COUNT + 1):
            source_path = parts_dir / f"pin_{index:02d}_head.png"
            if not source_path.exists():
                raise FileNotFoundError(f"Missing base head source: {source_path}")
            source = Image.open(source_path).convert("RGBA")
            result = make_variant(source, total_width_px)
            result.save(output_dir / f"pin_{index:02d}_head.png", "PNG")
            preview_images.append(result)

        make_preview(preview_images, output_dir / "preview_heads.png")
        print(f"Wrote {PIN_COUNT} heads + preview to {output_dir}")
```

Required `parse_args` and `main` (must be present — `if __name__ == "__main__": main()` alone is not enough):

```python
def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description="Generate composite pin head black-outline asset variants."
    )
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


if __name__ == "__main__":
    main()
```

Example invocation:

```powershell
py -3 scripts\create_head_asset_variants.py --parts-dir "Images&Content\Pins_v2\parts"
py -3 scripts\create_head_asset_variants.py --variant outline_black_6px --parts-dir "Images&Content\Pins_v2\parts"
```

- [ ] **Step 2: Add self-test implementation**

Implement `run_self_test` (called from `main` above when `--self-test` is passed):

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

On `--self-test` success, `main` prints `Head asset variant self-test passed` and exits 0.

- [ ] **Step 3: Confirm venv has required deps**

```powershell
scripts\venv\Scripts\python.exe -c "import PIL, numpy, scipy; print('image deps ok')"
```

If missing:

```powershell
py -3 -m venv scripts\venv
scripts\venv\Scripts\python.exe -m pip install -r scripts\requirements.txt
```

- [ ] **Step 4: Run self-test**

```powershell
scripts\venv\Scripts\python.exe scripts\create_head_asset_variants.py --self-test
```

Expected: exit 0, print `Head asset variant self-test passed`.

- [ ] **Step 5: Document the script**

In `scripts/README.md`:

1. Add `create_head_asset_variants.py` to the composite-pin asset tooling prose line.
2. Add catalog row: `Generate black-outline head variants (outline_black_2px … outline_black_14px); writes per-variant preview_heads.png grids`.

### Task 4: Generate and inspect the assets

**Prerequisite:** Task 3 complete. Base heads must exist at `Images&Content/Pins_v2/parts/pin_01_head.png` … `pin_12_head.png`.

**Output folders:**

- `Images&Content/Pins_v2/parts/head_variants/outline_black_2px/` … `outline_black_14px/`

- [ ] **Step 1: Generate all variants**

```powershell
scripts\venv\Scripts\python.exe scripts\create_head_asset_variants.py --parts-dir "Images&Content\Pins_v2\parts"
```

Expected: seven lines, each `Wrote 12 heads + preview to …`.

- [ ] **Step 2: Verify file count**

```powershell
Get-ChildItem "Images&Content\Pins_v2\parts\head_variants" -Recurse -File -Filter "*.png" |
    Group-Object { $_.Directory.Name } |
    Select-Object Name, Count
```

Expected: each `outline_black_*px` folder has **13** PNGs (12 heads + `preview_heads.png`). Total **91** PNGs across seven folders.

- [ ] **Step 3: Visual inspect preview grids**

Review:

```text
Images&Content/Pins_v2/parts/head_variants/outline_black_2px/preview_heads.png
Images&Content/Pins_v2/parts/head_variants/outline_black_6px/preview_heads.png
Images&Content/Pins_v2/parts/head_variants/outline_black_10px/preview_heads.png
Images&Content/Pins_v2/parts/head_variants/outline_black_14px/preview_heads.png
```

Acceptance:

- 2px is visible but subtle.
- 6px and 8px are likely default candidates for app review.
- 10px–14px are intentionally heavy review variants.
- Head centers remain colored, not fully black.

Use the runtime tuning panel or set `PinParts.HeadAssetVariant` in `visual-config.json` to spot-check in-app after Task 5.

### Task 5: Wire config docs and optional default

- [ ] **Step 1: Add config key (no visual change)**

In `visual-config.json`, under `PinParts` near `ShaftAssetVariant`:

```json
"HeadAssetVariant": "",
```

- [ ] **Step 2: Document in VISUAL_CONFIG.md**

Add under the `PinParts` section (near `ShaftAssetVariant`):

```markdown
- `HeadAssetVariant` — optional folder under `Images&Content/Pins_v2/parts/head_variants/`; when empty, heads load from the base parts folder. Use `outline_black_2px`, `outline_black_4px`, `outline_black_6px`, `outline_black_8px`, `outline_black_10px`, `outline_black_12px`, or `outline_black_14px` to load generated black-outline head assets. Generate variants with `scripts/create_head_asset_variants.py`.
```

- [ ] **Step 3: Add changelog entry**

Under `CHANGELOG.md` → `[Unreleased]` → `Changed`:

```markdown
- **Pinhead outline variants:** Added generated black-outline head asset variants (`outline_black_2px` through `outline_black_14px`) plus config-gated `PinParts.HeadAssetVariant` support. Default remains base head art until a reviewed variant is selected.
```

> **Note:** Do not mark the [TO_DO.md](../../TO_DO.md) pinhead item `[x]` until Task 6 verification passes.

### Task 6: Verification and completion

- [ ] **Step 1: Run full verification**

```powershell
.\scripts\verify.ps1
```

Expected: build, unit tests, doc links, taste checks, and startup validation pass.

> `verify.ps1` does not enumerate `head_variants/` PNGs; new assets will not fail the harness by path alone.

- [ ] **Step 2: Update backlog**

In `docs/TO_DO.md`, mark the pinhead item `[x]` with paths to generated variants.

- [ ] **Step 3: Archive the plan**

1. Move this file to `docs/exec-plans/completed/pinhead-black-outline-variants-plan.md`.
2. Add completion front-matter (`completed: YYYY-MM-DD`).
3. Update [active/README.md](../active/README.md) — remove active row; add completed row with verify date.
4. Update [composite-pins-program.md](../active/composite-pins-program.md) — Head visibility status `Complete`; next action `Review/select default head variant`.

- [ ] **Step 4: Commit (when requested by human)**

Stage the full scope (code, script, assets, docs). Example:

```powershell
git add Models/PinPartConfig.cs Services/CompositePinRenderPlanBuilder.cs Services/CompositePinPlanningService.cs Services/CompositePinLayoutContentHasher.cs Tests/CompositePinRenderPlanBuilderTests.cs Tests/CompositePinPlanningServiceTests.cs Tests/CompositePinLayoutContentHasherTests.cs Tests/VisualConfigServiceTests.cs scripts/create_head_asset_variants.py scripts/README.md visual-config.json "Images&Content/Pins_v2/parts/head_variants" docs/TO_DO.md docs/guides/VISUAL_CONFIG.md docs/exec-plans/active/composite-pins-program.md docs/exec-plans/active/README.md docs/exec-plans/completed/pinhead-black-outline-variants-plan.md CHANGELOG.md
git commit -m "Add pinhead outline variants"
```

Do **not** push unless explicitly requested.
