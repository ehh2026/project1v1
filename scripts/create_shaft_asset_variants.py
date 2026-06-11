"""
Generate baked composite-pin shaft asset variants for improved contrast.

Inputs:
  - Source lit shaft PNGs under Images&Content/Pins_v2/parts/pin_XX_shaft_lit.png
  - Variant name (e.g. outline_dark_7px) selecting outline kernel width

Outputs:
  - Per-pin shaft PNGs under parts/shaft_variants/<variant>/
  - preview_shafts.png grid for visual review

Requirements: Pillow (scripts/venv — see scripts/README.md).
"""
from __future__ import annotations

import argparse
import re
from pathlib import Path

from PIL import Image, ImageChops, ImageFilter, ImageOps

PIN_COUNT = 12
OUTLINE_DARK_PX_PATTERN = re.compile(r"^outline_dark_(\d+)px$")

# Default outline styling for outline_dark_Npx variants (kernel width = N, odd integer).
OUTLINE_DARK_PX_CONTRAST = 0.18
OUTLINE_DARK_PX_COLOR = (32, 28, 22, 170)


def alpha_outline(source: Image.Image, kernel_size: int, color: tuple[int, int, int, int]) -> Image.Image:
    """Expand alpha by a square MaxFilter kernel and composite a dark outline under the shaft."""
    if kernel_size < 3 or kernel_size % 2 == 0:
        raise ValueError(f"Outline kernel size must be an odd integer >= 3; got {kernel_size}")

    rgba = source.convert("RGBA")
    alpha = rgba.getchannel("A")
    grown = alpha.filter(ImageFilter.MaxFilter(kernel_size))
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


def outline_kernel_size_from_px_label(px: int) -> int:
    """Map folder suffix N in outline_dark_Npx to an odd MaxFilter kernel size."""
    if px < 3:
        raise ValueError(f"Outline width must be >= 3px; got {px}")
    return px if px % 2 == 1 else px + 1


def make_variant(source: Image.Image, name: str) -> Image.Image:
    if name == "outline_dark":
        contrasted = boost_contrast(source, 0.18)
        return alpha_outline(contrasted, kernel_size=3, color=(32, 28, 22, 170))

    if name == "outline_dark_bold":
        contrasted = boost_contrast(source, 0.28)
        return alpha_outline(contrasted, kernel_size=5, color=(24, 22, 18, 210))

    px_match = OUTLINE_DARK_PX_PATTERN.match(name)
    if px_match:
        kernel = outline_kernel_size_from_px_label(int(px_match.group(1)))
        contrasted = boost_contrast(source, OUTLINE_DARK_PX_CONTRAST)
        return alpha_outline(contrasted, kernel_size=kernel, color=OUTLINE_DARK_PX_COLOR)

    raise ValueError(
        f"Unknown variant: {name}. Use outline_dark, outline_dark_bold, or outline_dark_<N>px (e.g. outline_dark_7px)."
    )


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
        print(f"Wrote {PIN_COUNT} shafts + preview to {output_dir}")


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Generate composite pin shaft visibility asset variants.")
    parser.add_argument("--parts-dir", default="Images&Content/Pins_v2/parts")
    parser.add_argument(
        "--variant",
        action="append",
        help="Variant folder name (outline_dark, outline_dark_bold, outline_dark_7px, ...). Repeatable.",
    )
    return parser.parse_args()


def main() -> None:
    args = parse_args()
    variants = args.variant or ["outline_dark", "outline_dark_bold"]
    generate(Path(args.parts_dir), variants)


if __name__ == "__main__":
    main()
