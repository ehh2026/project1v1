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
