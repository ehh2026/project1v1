"""
Generate baked composite-pin shaft asset variants for improved contrast.

Inputs:
  - Source lit shaft PNGs under Images&Content/Pins_v2/parts/pin_XX_shaft_lit.png
  - Variant name selecting outline width and/or inner-edge darkening depth

Outputs:
  - Per-pin shaft PNGs under parts/shaft_variants/<variant>/
  - preview_shafts.png grid for visual review

Variant families:
  - outline_dark, outline_dark_bold — legacy outer-outline presets
  - outline_dark_<N>px — outer dark halo (N maps to odd MaxFilter kernel)
  - inner_dark_<N>px — near-black inward edge band (no outer growth)
  - outline_dark_<O>px_in<I>px — outer halo plus near-black inward edge band
  - outline_dark_<O>px_in<I>px_bright — same as combo, but shaft core is lifted/brighter

Requirements: Pillow (scripts/venv — see scripts/README.md).
"""
from __future__ import annotations

import argparse
import re
from pathlib import Path

from PIL import Image, ImageChops, ImageEnhance, ImageFilter, ImageOps

PIN_COUNT = 12
OUTLINE_DARK_PX_PATTERN = re.compile(r"^outline_dark_(\d+)px$")
OUTLINE_INNER_BRIGHT_PATTERN = re.compile(r"^outline_dark_(\d+)px_in(\d+)px_bright$")
OUTLINE_INNER_PATTERN = re.compile(r"^outline_dark_(\d+)px_in(\d+)px$")
INNER_DARK_PX_PATTERN = re.compile(r"^inner_dark_(\d+)px$")

# Default styling for outline_dark_Npx outer halos.
OUTLINE_DARK_PX_CONTRAST = 0.18
OUTLINE_DARK_PX_COLOR = (32, 28, 22, 170)

# Inner-edge band: blend toward pure black at full strength.
INNER_EDGE_DARK_RGB = (0, 0, 0)
INNER_EDGE_DARKEN_STRENGTH = 1.0

# Bright-core styling for *_bright variants (interior inside the dark edge band).
INTERIOR_LIFT_RGB = (252, 250, 244)
INTERIOR_LIFT_STRENGTH = 0.44
INTERIOR_BRIGHTNESS = 1.48
INTERIOR_CONTRAST = 1.12


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


def erosion_kernel_size_from_depth(depth_px: int) -> int:
    """Map inward edge depth (px) to an odd MinFilter kernel size."""
    if depth_px < 1:
        raise ValueError(f"Inner edge depth must be >= 1px; got {depth_px}")
    kernel = depth_px * 2 + 1
    return kernel if kernel % 2 == 1 else kernel + 1


def outline_kernel_size_from_px_label(px: int) -> int:
    """Map folder suffix N in outline_dark_Npx to an odd MaxFilter kernel size."""
    if px < 3:
        raise ValueError(f"Outline width must be >= 3px; got {px}")
    return px if px % 2 == 1 else px + 1


def edge_and_interior_masks(alpha: Image.Image, depth_px: int) -> tuple[Image.Image, Image.Image]:
    kernel = erosion_kernel_size_from_depth(depth_px)
    interior_alpha = alpha.filter(ImageFilter.MinFilter(kernel))
    edge_band = ImageChops.subtract(alpha, interior_alpha)
    return edge_band, interior_alpha


def brighten_interior_core(
    source: Image.Image,
    depth_px: int,
    lift_rgb: tuple[int, int, int] = INTERIOR_LIFT_RGB,
    lift_strength: float = INTERIOR_LIFT_STRENGTH,
    brightness: float = INTERIOR_BRIGHTNESS,
    contrast: float = INTERIOR_CONTRAST,
) -> Image.Image:
    """Lift the shaft core inside the inward edge band; leaves edge band and exterior unchanged."""
    rgba = source.convert("RGBA")
    alpha = rgba.getchannel("A")
    _, interior_alpha = edge_and_interior_masks(alpha, depth_px)

    if interior_alpha.getextrema()[1] == 0:
        return rgba

    rgb = rgba.convert("RGB")
    lifted = Image.blend(rgb, Image.new("RGB", rgba.size, lift_rgb), lift_strength)
    lifted = ImageEnhance.Brightness(lifted).enhance(brightness)
    lifted = ImageEnhance.Contrast(lifted).enhance(contrast)
    merged_rgb = Image.composite(lifted, rgb, interior_alpha)
    return Image.merge("RGBA", (*merged_rgb.split(), alpha))


def darken_inner_edges(
    source: Image.Image,
    depth_px: int,
    dark_rgb: tuple[int, int, int] = INNER_EDGE_DARK_RGB,
    strength: float = INNER_EDGE_DARKEN_STRENGTH,
) -> Image.Image:
    """
    Darken pixels in an inward band along the shaft silhouette without growing alpha outward.

    Uses alpha erosion: edge_band = alpha - MinFilter(alpha). Pixels in the band are
    blended toward dark_rgb; interior and exterior unchanged.
    """
    rgba = source.convert("RGBA")
    alpha = rgba.getchannel("A")
    edge_band, _ = edge_and_interior_masks(alpha, depth_px)

    if edge_band.getextrema()[1] == 0:
        return rgba

    rgb = rgba.convert("RGB")
    dark_fill = Image.new("RGB", rgba.size, dark_rgb)
    darkened = Image.blend(rgb, dark_fill, strength)
    merged_rgb = Image.composite(darkened, rgb, edge_band)
    return Image.merge("RGBA", (*merged_rgb.split(), alpha))


def dark_edges_bright_core(source: Image.Image, depth_px: int) -> Image.Image:
    """Near-black inward edge band with a lifted/brighter shaft interior."""
    bright = brighten_interior_core(source, depth_px=depth_px)
    return darken_inner_edges(bright, depth_px=depth_px)


def apply_outer_outline(source: Image.Image, outline_px: int) -> Image.Image:
    kernel = outline_kernel_size_from_px_label(outline_px)
    contrasted = boost_contrast(source, OUTLINE_DARK_PX_CONTRAST)
    return alpha_outline(contrasted, kernel_size=kernel, color=OUTLINE_DARK_PX_COLOR)


def apply_combo(source: Image.Image, outline_px: int, inner_px: int, bright_core: bool) -> Image.Image:
    with_outer = apply_outer_outline(source, outline_px)
    if bright_core:
        return dark_edges_bright_core(with_outer, depth_px=inner_px)
    return darken_inner_edges(with_outer, depth_px=inner_px)


def make_variant(source: Image.Image, name: str) -> Image.Image:
    if name == "outline_dark":
        contrasted = boost_contrast(source, 0.18)
        return alpha_outline(contrasted, kernel_size=3, color=(32, 28, 22, 170))

    if name == "outline_dark_bold":
        contrasted = boost_contrast(source, 0.28)
        return alpha_outline(contrasted, kernel_size=5, color=(24, 22, 18, 210))

    bright_combo_match = OUTLINE_INNER_BRIGHT_PATTERN.match(name)
    if bright_combo_match:
        return apply_combo(
            source,
            outline_px=int(bright_combo_match.group(1)),
            inner_px=int(bright_combo_match.group(2)),
            bright_core=True,
        )

    inner_match = INNER_DARK_PX_PATTERN.match(name)
    if inner_match:
        depth = int(inner_match.group(1))
        contrasted = boost_contrast(source, OUTLINE_DARK_PX_CONTRAST)
        return darken_inner_edges(contrasted, depth_px=depth)

    combo_match = OUTLINE_INNER_PATTERN.match(name)
    if combo_match:
        return apply_combo(
            source,
            outline_px=int(combo_match.group(1)),
            inner_px=int(combo_match.group(2)),
            bright_core=False,
        )

    px_match = OUTLINE_DARK_PX_PATTERN.match(name)
    if px_match:
        return apply_outer_outline(source, int(px_match.group(1)))

    raise ValueError(
        f"Unknown variant: {name}. "
        "Use outline_dark, outline_dark_bold, outline_dark_<N>px, inner_dark_<N>px, "
        "outline_dark_<O>px_in<I>px, or outline_dark_<O>px_in<I>px_bright."
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
        help=(
            "Variant folder name, e.g. outline_dark_6px_in3px_bright, inner_dark_3px. Repeatable."
        ),
    )
    return parser.parse_args()


def main() -> None:
    args = parse_args()
    variants = args.variant or ["outline_dark", "outline_dark_bold"]
    generate(Path(args.parts_dir), variants)


if __name__ == "__main__":
    main()
