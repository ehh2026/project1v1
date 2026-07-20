"""
Generate baked composite-pin head asset variants with black outline strokes.

Inputs:
  - Base head PNGs under Images&Content/Assets/Pins_v2/parts/pin_XX_head.png

Outputs:
  - Per-head PNGs under parts/head_variants/<variant>/
  - preview_heads.png grid for visual review

Variant names: outline_black_2px through outline_black_14px (even widths).
Each stroke uses symmetric disk morphology: N/2 px outward + N/2 px inward.

Requirements: Pillow, numpy, scipy (scripts/venv — see scripts/README.md).
"""
from __future__ import annotations

import argparse
import re
import sys
from pathlib import Path

import numpy as np
from PIL import Image
from scipy import ndimage

PIN_COUNT = 12
DEFAULT_WIDTHS = [2, 4, 6, 8, 10, 12, 14]
VARIANT_PATTERN = re.compile(r"^outline_black_(\d+)px$")


def disk(radius: int) -> np.ndarray:
    yy, xx = np.ogrid[-radius : radius + 1, -radius : radius + 1]
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


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description="Generate composite pin head black-outline asset variants."
    )
    parser.add_argument("--parts-dir", default="Images&Content/Assets/Pins_v2/parts")
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
