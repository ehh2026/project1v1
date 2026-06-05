"""
extract_pins.py — Pin Background Removal and Extraction Script

Loads a composite pin image (Pins.jpg), removes the white background using
configurable thresholds, detects individual pin blobs via connected-component
labeling, and exports each pin as a separate transparent PNG file.

Inputs:
    - Images&Content/Pins.jpg (4500x4500 JPEG with 11 pins on a white background)

Outputs:
    - Images&Content/Pins/threshold_{T}/pin_01.png .. pin_NN.png
    - Images&Content/Pins/threshold_{T}/composite_preview.png

Requirements:
    - Pillow, numpy, scipy (see scripts/requirements.txt)

Usage:
    cd scripts/
    source venv/bin/activate
    python extract_pins.py
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

THRESHOLDS = [230, 235, 240, 245, 250]
PADDING_PX = 15
MIN_BLOB_AREA = 90_000
SOFT_EDGE_RANGE = 50
SOFT_EDGE_SPATIAL_PX = 20

SCRIPT_DIR = os.path.dirname(os.path.abspath(__file__))
INPUT_PATH = os.path.join(SCRIPT_DIR, "..", "Images&Content", "Pins.jpg")
OUTPUT_BASE_DIR = os.path.join(SCRIPT_DIR, "..", "Images&Content", "Pins")

PREVIEW_TILE_MAX = 400
PREVIEW_COLUMNS = 4
CHECKER_SIZE = 32


# ─────────────────────────────────────────────────────────────
# Background Removal
# ─────────────────────────────────────────────────────────────

def remove_background(pixels: np.ndarray, threshold: int) -> np.ndarray:
    """Replace border-connected white regions with transparency.

    Uses a border flood-fill approach: only white connected components that
    touch the image border are treated as background. Interior white pixels
    (specular highlights / glints on pin heads) remain fully opaque.

    A two-factor soft alpha falloff is applied in the near-white zone
    bordering the background:
      1. Brightness factor — pixels closer to white get lower alpha
      2. Spatial factor — pixels closer to the background boundary get
         lower alpha (via Euclidean distance transform)
    The two factors are multiplied, producing smooth semi-transparent
    edges that eliminate the white fringe around shadows.

    Args:
        pixels: (H, W, 4) uint8 RGBA array.
        threshold: Pixels with R, G, B all above this value are candidate white.

    Returns:
        Modified copy of the RGBA array with background set to transparent.
    """
    result = pixels.copy()
    r, g, b = result[:, :, 0], result[:, :, 1], result[:, :, 2]
    h, w = result.shape[:2]

    white_mask = (r > threshold) & (g > threshold) & (b > threshold)

    labeled_white, num_white = ndimage.label(white_mask)

    border_labels = set()
    border_labels.update(labeled_white[0, :].ravel())      # top row
    border_labels.update(labeled_white[h - 1, :].ravel())   # bottom row
    border_labels.update(labeled_white[:, 0].ravel())        # left col
    border_labels.update(labeled_white[:, w - 1].ravel())    # right col
    border_labels.discard(0)

    background_mask = np.isin(labeled_white, list(border_labels))

    result[background_mask, 3] = 0

    if SOFT_EDGE_RANGE > 0:
        low = threshold - SOFT_EDGE_RANGE
        channel_max = np.maximum(np.maximum(r, g), b)

        near_white = (channel_max >= low) & (channel_max <= threshold) & (~background_mask)

        dist_from_bg = ndimage.distance_transform_edt(~background_mask)
        spatial_zone = dist_from_bg <= SOFT_EDGE_SPATIAL_PX

        soft_zone = near_white & spatial_zone

        if np.any(soft_zone):
            brightness_alpha = (
                (threshold - channel_max[soft_zone].astype(np.float64))
                / SOFT_EDGE_RANGE
            )

            spatial_alpha = dist_from_bg[soft_zone] / SOFT_EDGE_SPATIAL_PX

            combined_alpha = 255.0 * np.clip(brightness_alpha * spatial_alpha, 0.0, 1.0)
            result[soft_zone, 3] = combined_alpha.astype(np.uint8)

    return result


# ─────────────────────────────────────────────────────────────
# Blob Detection
# ─────────────────────────────────────────────────────────────

def detect_blobs(rgba: np.ndarray) -> list[tuple[slice, slice]]:
    """Find individual pin blobs via connected-component labeling.

    Creates a binary mask from the alpha channel, labels connected regions,
    filters by minimum area, and returns padded bounding-box slices sorted
    top-to-bottom then left-to-right.

    Args:
        rgba: (H, W, 4) uint8 RGBA array with background already transparent.

    Returns:
        List of (row_slice, col_slice) tuples defining each blob's bounding box.
    """
    alpha = rgba[:, :, 3]
    binary = (alpha > 0).astype(np.uint8)
    labeled, num_features = ndimage.label(binary)
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
# Checkerboard Preview
# ─────────────────────────────────────────────────────────────

def _make_checkerboard(width: int, height: int) -> Image.Image:
    """Create an RGBA checkerboard background image.

    Args:
        width: Width in pixels.
        height: Height in pixels.

    Returns:
        PIL Image with alternating light/dark gray squares.
    """
    board = np.zeros((height, width, 4), dtype=np.uint8)
    for y in range(height):
        for x in range(width):
            if (y // CHECKER_SIZE + x // CHECKER_SIZE) % 2 == 0:
                board[y, x] = [200, 200, 200, 255]
            else:
                board[y, x] = [240, 240, 240, 255]
    return Image.fromarray(board, "RGBA")


def _make_checkerboard_fast(width: int, height: int) -> Image.Image:
    """Create an RGBA checkerboard using vectorized numpy operations."""
    ys = np.arange(height) // CHECKER_SIZE
    xs = np.arange(width) // CHECKER_SIZE
    grid = (ys[:, None] + xs[None, :]) % 2

    board = np.zeros((height, width, 4), dtype=np.uint8)
    board[grid == 0] = [200, 200, 200, 255]
    board[grid == 1] = [240, 240, 240, 255]
    return Image.fromarray(board, "RGBA")


# ─────────────────────────────────────────────────────────────
# Export
# ─────────────────────────────────────────────────────────────

def export_pins(
    rgba: np.ndarray,
    blobs: list[tuple[slice, slice]],
    output_dir: str,
) -> list[dict]:
    """Crop and save each detected blob as a transparent PNG.

    Also generates a composite preview image showing all pins tiled on a
    checkerboard background for quick visual assessment.

    Args:
        rgba: (H, W, 4) uint8 RGBA array with background removed.
        blobs: List of (row_slice, col_slice) bounding boxes from detect_blobs.
        output_dir: Directory path where PNGs will be saved.

    Returns:
        List of dicts with pin metadata (index, filename, dimensions, file size).
    """
    os.makedirs(output_dir, exist_ok=True)
    pin_info = []
    pin_images = []

    for idx, (row_sl, col_sl) in enumerate(blobs, start=1):
        crop = rgba[row_sl, col_sl].copy()
        pin_img = Image.fromarray(crop, "RGBA")
        filename = f"pin_{idx:02d}.png"
        filepath = os.path.join(output_dir, filename)
        pin_img.save(filepath, "PNG")

        file_size = os.path.getsize(filepath)
        pin_info.append({
            "index": idx,
            "filename": filename,
            "width": pin_img.width,
            "height": pin_img.height,
            "file_size_kb": round(file_size / 1024, 1),
        })
        pin_images.append(pin_img)

        print(f"  pin_{idx:02d}.png  {pin_img.width}x{pin_img.height}  "
              f"({file_size / 1024:.1f} KB)")

    _generate_composite(pin_images, output_dir)
    return pin_info


def _generate_composite(pin_images: list[Image.Image], output_dir: str) -> None:
    """Tile all pin images onto a checkerboard preview and save it.

    Each pin is scaled to fit within PREVIEW_TILE_MAX pixels on its largest
    dimension, arranged in a grid of PREVIEW_COLUMNS columns.

    Args:
        pin_images: List of PIL RGBA Images.
        output_dir: Directory to save composite_preview.png.
    """
    if not pin_images:
        return

    cell_size = PREVIEW_TILE_MAX + 20
    cols = PREVIEW_COLUMNS
    rows = (len(pin_images) + cols - 1) // cols
    canvas_w = cols * cell_size + 20
    canvas_h = rows * cell_size + 20

    canvas = _make_checkerboard_fast(canvas_w, canvas_h)

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

    preview_path = os.path.join(output_dir, "composite_preview.png")
    canvas.save(preview_path, "PNG")
    preview_kb = os.path.getsize(preview_path) / 1024
    print(f"  composite_preview.png  {canvas_w}x{canvas_h}  ({preview_kb:.1f} KB)")


# ─────────────────────────────────────────────────────────────
# Main
# ─────────────────────────────────────────────────────────────

def main() -> None:
    """Run pin extraction at each configured threshold.

    Loads the source image once, then for each threshold value:
    removes the background, detects blobs, exports individual PNGs,
    and prints summary statistics.
    """
    input_path = os.path.normpath(INPUT_PATH)
    if not os.path.isfile(input_path):
        print(f"ERROR: Input image not found: {input_path}", file=sys.stderr)
        sys.exit(1)

    print(f"Loading {input_path} ...")
    src_img = Image.open(input_path).convert("RGBA")
    pixels = np.array(src_img)
    print(f"Image size: {src_img.width}x{src_img.height}\n")

    for threshold in THRESHOLDS:
        t_start = time.time()
        print(f"{'=' * 60}")
        print(f"Threshold: {threshold}")
        print(f"{'=' * 60}")

        out_dir = os.path.join(
            os.path.normpath(OUTPUT_BASE_DIR), f"threshold_{threshold}"
        )

        print("  Removing background ...")
        processed = remove_background(pixels, threshold)

        print("  Detecting blobs ...")
        blobs = detect_blobs(processed)
        print(f"  Found {len(blobs)} pin(s) (min area = {MIN_BLOB_AREA:,})\n")

        print("  Exporting pins:")
        pin_info = export_pins(processed, blobs, out_dir)

        elapsed = time.time() - t_start
        total_kb = sum(p["file_size_kb"] for p in pin_info)
        print(f"\n  Summary: {len(pin_info)} pins, "
              f"{total_kb:.1f} KB total, "
              f"{elapsed:.1f}s elapsed\n")


if __name__ == "__main__":
    main()
