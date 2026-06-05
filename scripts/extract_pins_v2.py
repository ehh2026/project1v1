"""
extract_pins_v2.py — Improved Pin Extraction with Proper Shadow Handling

Key improvements over v1:
  1. Shadow pixels are "un-premultiplied from white" so they darken backgrounds
     instead of producing white haze.
  2. Better classification of shadow vs pin-body pixels using brightness +
     saturation heuristics, so metallic pin shafts stay fully opaque.
  3. Interior near-white cleanup: removes trapped white/gray zones between
     pin body and shadow that aren't border-connected background.

Runs multiple parameter variants for comparison when COMPARE_MODE is True.

Inputs:
    - Images&Content/Pins.jpg

Outputs:
    - Images&Content/Pins_v2/pin_01.png .. pin_NN.png  (best variant)
    - Images&Content/Pins_v2/composite_preview.png
    - Images&Content/Pins_v2/preview_on_beige.png
    - Images&Content/Pins_v2/preview_on_blue.png
    When COMPARE_MODE:
    - Images&Content/Pins_v2/compare_<name>/*.png  (each variant)

Usage:
    cd scripts/
    python extract_pins_v2.py
"""

import os
import sys
import time

import numpy as np
from PIL import Image
from scipy import ndimage

# ─────────────────────────────────────────────────────────────
# Configuration
# ─────────────────────────────────────────────────────────────

COMPARE_MODE = True            # True = run all variants for comparison

THRESHOLD = 235                # white background detection threshold
PADDING_PX = 15
MIN_BLOB_AREA = 90_000

SCRIPT_DIR = os.path.dirname(os.path.abspath(__file__))
INPUT_PATH = os.path.join(SCRIPT_DIR, "..", "Images&Content", "Pins.jpg")
OUTPUT_BASE = os.path.join(SCRIPT_DIR, "..", "Images&Content", "Pins_v2")

PREVIEW_TILE_MAX = 400
PREVIEW_COLUMNS = 4
CHECKER_SIZE = 32

# Parameter variants to compare
VARIANTS = {
    "A_conservative": {
        "brightness_floor": 200,   # only very bright pixels = shadow
        "brightness_range": 40,    # ramp from 200 to 240
        "saturation_cap": 40,      # low sat = shadow
        "shadow_spatial_px": 35,
        "soft_edge_spatial_px": 20,
        "interior_cleanup": True,
        "interior_threshold": 230, # interior pixels brighter than this get cleaned
    },
    "B_moderate": {
        "brightness_floor": 185,
        "brightness_range": 50,
        "saturation_cap": 50,
        "shadow_spatial_px": 40,
        "soft_edge_spatial_px": 25,
        "interior_cleanup": True,
        "interior_threshold": 225,
    },
    "C_aggressive": {
        "brightness_floor": 170,
        "brightness_range": 60,
        "saturation_cap": 60,
        "shadow_spatial_px": 45,
        "soft_edge_spatial_px": 30,
        "interior_cleanup": True,
        "interior_threshold": 220,
    },
    "D_hybrid": {
        # Conservative shadow classification (protect shafts) +
        # aggressive interior cleanup (remove trapped white under pin 07) +
        # distance-based shadow confidence boost (pixels right next to
        # background are more likely shadow regardless of brightness)
        "brightness_floor": 200,
        "brightness_range": 40,
        "saturation_cap": 40,
        "shadow_spatial_px": 35,
        "soft_edge_spatial_px": 20,
        "interior_cleanup": True,
        "interior_threshold": 215,  # aggressive interior cleanup
        "interior_sat_cap": 20,     # stricter: only very gray interior pixels
        "proximity_shadow_boost": True,  # pixels within 5px of bg get shadow boost
    },
}

# Which variant to use for the main Pins_v2 output (when not in compare mode,
# or as the "best" output alongside comparisons)
BEST_VARIANT = "D_hybrid"


# ─────────────────────────────────────────────────────────────
# Background Removal with Shadow Recovery
# ─────────────────────────────────────────────────────────────

def remove_background(pixels: np.ndarray, threshold: int, params: dict) -> np.ndarray:
    """Remove white background, clean interior white zones, recover shadow colors.

    Args:
        pixels: (H, W, 4) uint8 RGBA array.
        threshold: Border-connected white detection threshold.
        params: Dict with tuning parameters (see VARIANTS).

    Returns:
        Modified RGBA array with proper transparency and shadow colors.
    """
    result = pixels.copy()
    r = result[:, :, 0].astype(np.float64)
    g = result[:, :, 1].astype(np.float64)
    b = result[:, :, 2].astype(np.float64)
    h, w = result.shape[:2]

    brightness_floor = params["brightness_floor"]
    brightness_range = params["brightness_range"]
    saturation_cap = params["saturation_cap"]
    shadow_spatial_px = params["shadow_spatial_px"]
    soft_edge_spatial_px = params["soft_edge_spatial_px"]
    do_interior_cleanup = params.get("interior_cleanup", False)
    interior_thresh = params.get("interior_threshold", 230)

    # ── Phase 1: Border flood-fill background removal ──
    white_mask = (r > threshold) & (g > threshold) & (b > threshold)
    labeled_white, _ = ndimage.label(white_mask)

    border_labels = set()
    border_labels.update(labeled_white[0, :].ravel())
    border_labels.update(labeled_white[h - 1, :].ravel())
    border_labels.update(labeled_white[:, 0].ravel())
    border_labels.update(labeled_white[:, w - 1].ravel())
    border_labels.discard(0)

    background_mask = np.isin(labeled_white, list(border_labels))
    result[background_mask, 3] = 0

    # ── Phase 2: Interior white zone cleanup ──
    # Some near-white pixels get trapped between pin body and shadow
    # (not border-connected). Detect and make them transparent too.
    if do_interior_cleanup:
        channel_min_all = np.minimum(np.minimum(r, g), b)
        channel_max_all = np.maximum(np.maximum(r, g), b)
        sat_all = channel_max_all - channel_min_all
        interior_sat_cap = params.get("interior_sat_cap", 15)

        # Interior white: very bright, very low saturation, NOT background,
        # and close to background boundary (within shadow_spatial_px)
        dist_from_bg = ndimage.distance_transform_edt(~background_mask)
        interior_white = (
            (~background_mask) &
            (channel_max_all > interior_thresh) &
            (sat_all < interior_sat_cap) &
            (dist_from_bg <= shadow_spatial_px)
        )

        if np.any(interior_white):
            # These pixels are essentially trapped background - make transparent
            # Smooth falloff based on how white they are
            iw_brightness = channel_max_all[interior_white]
            iw_alpha = np.clip((interior_thresh - iw_brightness) / 20.0, 0.0, 1.0) * 255.0
            result[interior_white, 3] = iw_alpha.astype(np.uint8)

            # Expand background mask to include cleaned interior pixels
            background_mask = background_mask | interior_white

            # Recompute distance from expanded background
            dist_from_bg = ndimage.distance_transform_edt(~background_mask)
    else:
        dist_from_bg = ndimage.distance_transform_edt(~background_mask)

    # ── Phase 3: Shadow recovery via un-premultiply from white ──
    shadow_zone = (~background_mask) & (dist_from_bg <= shadow_spatial_px)

    if np.any(shadow_zone):
        channel_min = np.minimum(np.minimum(r, g), b)
        channel_max = np.maximum(np.maximum(r, g), b)

        sz_r = r[shadow_zone]
        sz_g = g[shadow_zone]
        sz_b = b[shadow_zone]
        sz_min = channel_min[shadow_zone]
        sz_max = channel_max[shadow_zone]
        sz_dist = dist_from_bg[shadow_zone]

        # Classify: shadow vs pin body
        saturation = sz_max - sz_min
        brightness = sz_max

        # Shadow confidence: high when pixel is VERY bright AND gray
        # brightness_floor=200 means only pixels brighter than 200 start
        # being considered as shadow candidates (shaft pixels at ~150-180 excluded)
        brightness_conf = np.clip(
            (brightness - brightness_floor) / brightness_range, 0.0, 1.0
        )
        gray_conf = np.clip(1.0 - (saturation / saturation_cap), 0.0, 1.0)
        shadow_conf = brightness_conf * gray_conf

        # Proximity boost: pixels within a few px of background are almost
        # certainly edge/shadow fringe, not pin body interior.
        # This helps clean up the transition zone without affecting shafts
        # (which are farther from background).
        if params.get("proximity_shadow_boost", False):
            proximity_boost = np.clip(1.0 - (sz_dist / 6.0), 0.0, 0.5)
            # Only boost for bright-ish gray pixels (don't boost dark pin edges)
            boost_eligible = (brightness > 160) & (saturation < saturation_cap)
            shadow_conf = np.where(
                boost_eligible,
                np.clip(shadow_conf + proximity_boost, 0.0, 1.0),
                shadow_conf
            )

        # Shadow alpha from darkness relative to white
        raw_alpha = 255.0 - sz_min

        # Spatial falloff near background boundary
        spatial_factor = np.clip(sz_dist / soft_edge_spatial_px, 0.0, 1.0)

        # Shadow alpha with spatial falloff
        shadow_alpha = raw_alpha * spatial_factor
        shadow_alpha = np.clip(shadow_alpha, 0.0, 255.0)

        # Blended alpha: shadow pixels get computed alpha, pin body stays 255
        final_alpha = shadow_conf * shadow_alpha + (1.0 - shadow_conf) * 255.0

        # Un-premultiply RGB from white for shadow pixels
        a_norm = np.clip(shadow_alpha / 255.0, 0.01, 1.0)
        unpremult_r = np.clip((sz_r - 255.0 * (1.0 - a_norm)) / a_norm, 0.0, 255.0)
        unpremult_g = np.clip((sz_g - 255.0 * (1.0 - a_norm)) / a_norm, 0.0, 255.0)
        unpremult_b = np.clip((sz_b - 255.0 * (1.0 - a_norm)) / a_norm, 0.0, 255.0)

        # Blend RGB based on classification
        final_r = shadow_conf * unpremult_r + (1.0 - shadow_conf) * sz_r
        final_g = shadow_conf * unpremult_g + (1.0 - shadow_conf) * sz_g
        final_b = shadow_conf * unpremult_b + (1.0 - shadow_conf) * sz_b

        # Write back RGB
        result_r = result[:, :, 0].astype(np.float64)
        result_g = result[:, :, 1].astype(np.float64)
        result_b = result[:, :, 2].astype(np.float64)

        result_r[shadow_zone] = np.clip(final_r, 0.0, 255.0)
        result_g[shadow_zone] = np.clip(final_g, 0.0, 255.0)
        result_b[shadow_zone] = np.clip(final_b, 0.0, 255.0)

        result[:, :, 0] = result_r.astype(np.uint8)
        result[:, :, 1] = result_g.astype(np.uint8)
        result[:, :, 2] = result_b.astype(np.uint8)

        # Write back alpha (skip negligible values)
        final_alpha = np.where(final_alpha > 3.0, final_alpha, 0.0)

        alpha_channel = result[:, :, 3].astype(np.float64)
        alpha_channel[shadow_zone] = np.clip(final_alpha, 0.0, 255.0)
        result[:, :, 3] = alpha_channel.astype(np.uint8)

    return result


# ─────────────────────────────────────────────────────────────
# Blob Detection
# ─────────────────────────────────────────────────────────────

def detect_blobs(rgba: np.ndarray) -> list[tuple[slice, slice]]:
    """Find individual pin blobs via connected-component labeling."""
    alpha = rgba[:, :, 3]
    binary = (alpha > 0).astype(np.uint8)
    labeled, _ = ndimage.label(binary)
    h, w = alpha.shape

    slices = ndimage.find_objects(labeled)
    blobs = []

    for i, obj_slices in enumerate(slices, start=1):
        if obj_slices is None:
            continue
        area = np.sum(labeled[obj_slices] == i)
        if area < MIN_BLOB_AREA:
            continue

        row_slice, col_slice = obj_slices
        r0 = max(0, row_slice.start - PADDING_PX)
        r1 = min(h, row_slice.stop + PADDING_PX)
        c0 = max(0, col_slice.start - PADDING_PX)
        c1 = min(w, col_slice.stop + PADDING_PX)

        center_y = (row_slice.start + row_slice.stop) / 2.0
        center_x = (col_slice.start + col_slice.stop) / 2.0
        blobs.append((center_y, center_x, (slice(r0, r1), slice(c0, c1))))

    blobs.sort(key=lambda b: (b[0], b[1]))
    return [(b[2][0], b[2][1]) for b in blobs]


# ─────────────────────────────────────────────────────────────
# Preview helpers
# ─────────────────────────────────────────────────────────────

def _make_checkerboard(width: int, height: int) -> Image.Image:
    ys = np.arange(height) // CHECKER_SIZE
    xs = np.arange(width) // CHECKER_SIZE
    grid = (ys[:, None] + xs[None, :]) % 2
    board = np.zeros((height, width, 4), dtype=np.uint8)
    board[grid == 0] = [200, 200, 200, 255]
    board[grid == 1] = [240, 240, 240, 255]
    return Image.fromarray(board, "RGBA")


def _make_colored_background(width: int, height: int, color: tuple) -> Image.Image:
    bg = np.zeros((height, width, 4), dtype=np.uint8)
    bg[:, :] = [color[0], color[1], color[2], 255]
    return Image.fromarray(bg, "RGBA")


# ─────────────────────────────────────────────────────────────
# Export
# ─────────────────────────────────────────────────────────────

def export_pins(
    rgba: np.ndarray,
    blobs: list[tuple[slice, slice]],
    output_dir: str,
    save_individual: bool = True,
) -> tuple[list[dict], list[Image.Image]]:
    """Crop and save each detected blob as a transparent PNG."""
    os.makedirs(output_dir, exist_ok=True)
    pin_info = []
    pin_images = []

    for idx, (row_sl, col_sl) in enumerate(blobs, start=1):
        crop = rgba[row_sl, col_sl].copy()
        pin_img = Image.fromarray(crop, "RGBA")
        filename = f"pin_{idx:02d}.png"

        if save_individual:
            filepath = os.path.join(output_dir, filename)
            pin_img.save(filepath, "PNG")
            file_size = os.path.getsize(filepath)
        else:
            file_size = 0

        pin_info.append({
            "index": idx,
            "filename": filename,
            "width": pin_img.width,
            "height": pin_img.height,
            "file_size_kb": round(file_size / 1024, 1),
        })
        pin_images.append(pin_img)

        if save_individual:
            print(f"  pin_{idx:02d}.png  {pin_img.width}x{pin_img.height}  "
                  f"({file_size / 1024:.1f} KB)")

    return pin_info, pin_images


def generate_previews(
    pin_images: list[Image.Image],
    output_dir: str,
    label: str = "",
) -> None:
    """Generate composite previews on multiple backgrounds."""
    backgrounds = [
        ("composite_preview.png", _make_checkerboard),
        ("preview_on_beige.png",
         lambda w, h: _make_colored_background(w, h, (210, 195, 160))),
        ("preview_on_blue.png",
         lambda w, h: _make_colored_background(w, h, (100, 140, 180))),
    ]

    for filename, bg_factory in backgrounds:
        _generate_composite(pin_images, output_dir, filename, bg_factory, label)


def _generate_composite(
    pin_images: list[Image.Image],
    output_dir: str,
    filename: str,
    bg_factory,
    label: str = "",
) -> None:
    """Tile all pin images onto a background and save."""
    if not pin_images:
        return

    cell_size = PREVIEW_TILE_MAX + 20
    cols = PREVIEW_COLUMNS
    rows = (len(pin_images) + cols - 1) // cols
    canvas_w = cols * cell_size + 20
    canvas_h = rows * cell_size + 20

    canvas = bg_factory(canvas_w, canvas_h)

    for i, img in enumerate(pin_images):
        scale = min(PREVIEW_TILE_MAX / img.width, PREVIEW_TILE_MAX / img.height)
        new_w = int(img.width * scale)
        new_h = int(img.height * scale)
        thumb = img.resize((new_w, new_h), Image.LANCZOS)

        col = i % cols
        row = i // cols
        x = 10 + col * cell_size + (PREVIEW_TILE_MAX - new_w) // 2
        y = 10 + row * cell_size + (PREVIEW_TILE_MAX - new_h) // 2

        canvas.paste(thumb, (x, y), thumb)

    preview_path = os.path.join(output_dir, filename)
    canvas.save(preview_path, "PNG")
    preview_kb = os.path.getsize(preview_path) / 1024
    prefix = f"  [{label}] " if label else "  "
    print(f"{prefix}{filename}  {canvas_w}x{canvas_h}  ({preview_kb:.1f} KB)")


# ─────────────────────────────────────────────────────────────
# Main
# ─────────────────────────────────────────────────────────────

def run_variant(
    pixels: np.ndarray,
    name: str,
    params: dict,
    output_dir: str,
    save_individual: bool = True,
) -> list[Image.Image]:
    """Run extraction with a specific parameter set."""
    print(f"\n{'=' * 60}")
    print(f"Variant: {name}")
    print(f"  brightness_floor={params['brightness_floor']}, "
          f"brightness_range={params['brightness_range']}, "
          f"saturation_cap={params['saturation_cap']}")
    print(f"  shadow_spatial={params['shadow_spatial_px']}px, "
          f"soft_edge_spatial={params['soft_edge_spatial_px']}px, "
          f"interior_cleanup={params.get('interior_cleanup', False)}")
    print(f"{'=' * 60}")

    t_start = time.time()

    processed = remove_background(pixels, THRESHOLD, params)
    blobs = detect_blobs(processed)
    print(f"  Found {len(blobs)} pin(s)")

    pin_info, pin_images = export_pins(processed, blobs, output_dir, save_individual)
    generate_previews(pin_images, output_dir, name)

    elapsed = time.time() - t_start
    print(f"  Elapsed: {elapsed:.1f}s")

    return pin_images


def main() -> None:
    """Run pin extraction - compare mode or single best variant."""
    input_path = os.path.normpath(INPUT_PATH)
    if not os.path.isfile(input_path):
        print(f"ERROR: Input image not found: {input_path}", file=sys.stderr)
        sys.exit(1)

    print(f"Loading {input_path} ...")
    src_img = Image.open(input_path).convert("RGBA")
    pixels = np.array(src_img)
    print(f"Image size: {src_img.width}x{src_img.height}")
    print(f"Threshold: {THRESHOLD}")
    print(f"Compare mode: {COMPARE_MODE}")

    if COMPARE_MODE:
        # Run all variants for comparison
        for name, params in VARIANTS.items():
            variant_dir = os.path.join(OUTPUT_BASE, f"compare_{name}")
            run_variant(pixels, name, params, variant_dir, save_individual=True)

        print(f"\n{'=' * 60}")
        print(f"Comparison complete. Check folders in:")
        print(f"  {os.path.normpath(OUTPUT_BASE)}/compare_*/")
        print(f"Compare preview_on_beige.png and preview_on_blue.png across variants.")
    else:
        # Run only the best variant
        params = VARIANTS[BEST_VARIANT]
        run_variant(pixels, BEST_VARIANT, params, OUTPUT_BASE, save_individual=True)


if __name__ == "__main__":
    main()
