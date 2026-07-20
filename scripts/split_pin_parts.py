"""
split_pin_parts.py — Split pins into circular heads and shafts

Takes already-extracted pin PNGs and splits each into:
  - pin_NN_head.png  (sphere only, clean circular mask)
  - pin_NN_shaft.png (everything outside the circle: shaft + tip disc + shadow)

Head detection uses saturation + circle fitting.
Shaft is defined geometrically as "opaque pixels outside the fitted circle",
so shaft pixels are never lost to the saturation filter.

Inputs:
    - Images&Content/Assets/Pins_v2/compare_C_aggressive/pin_NN.png

Outputs:
    - Images&Content/Assets/Pins_v2/parts/pin_NN_head.png
    - Images&Content/Assets/Pins_v2/parts/pin_NN_shaft.png
    - Images&Content/Assets/Pins_v2/parts/pin_parts_manifest.json
    - Images&Content/Assets/Pins_v2/parts/preview_heads.png
    - Images&Content/Assets/Pins_v2/parts/preview_shafts.png

Usage:
    cd scripts/
    python split_pin_parts.py
"""

import json
import math
import os
import sys

import numpy as np
from PIL import Image
from scipy import ndimage

# ─────────────────────────────────────────────────────────────
# Configuration
# ─────────────────────────────────────────────────────────────

SCRIPT_DIR = os.path.dirname(os.path.abspath(__file__))
INPUT_DIR = os.path.join(
    SCRIPT_DIR, "..", "Images&Content", "Assets", "Pins_v2", "compare_C_aggressive"
)
OUTPUT_DIR = os.path.join(
    SCRIPT_DIR, "..", "Images&Content", "Assets", "Pins_v2", "parts"
)

NUM_PINS = 12

# Saturation threshold (0-255) for detecting the colored sphere
SAT_THRESHOLD = 45

# Extra radius pixels to add beyond detected edge (anti-aliasing capture)
RADIUS_PAD = 4

# Soft edge width at circle boundary for smooth anti-aliasing
SOFT_EDGE_PX = 3

# How many pixels to grow the shaft cutout circle outward.
# Positive = shaft starts slightly outside the head edge (small gap, head covers it).
# Zero = shaft starts exactly at the head circle edge.
SHAFT_INSET_PX = 6  # dilation of exclusion zone around head

# Shaft highlight: lighten the central axis
SHAFT_HIGHLIGHT_STRENGTH = 0.45  # max brightness boost (0-1) at dead center
SHAFT_HIGHLIGHT_FALLOFF = 0.35   # fraction of half-width where highlight reaches zero (smaller = tighter)

PREVIEW_TILE = 300
PREVIEW_COLUMNS = 6
CHECKER_SIZE = 16


# ─────────────────────────────────────────────────────────────
# Extraction
# ─────────────────────────────────────────────────────────────

def extract_parts(pin_rgba: np.ndarray) -> tuple[np.ndarray, np.ndarray, dict]:
    """Split a pin into head (circle) and shaft (everything else).

    Returns:
        (head_cropped, shaft_cropped, geometry)
    """
    h, w = pin_rgba.shape[:2]
    r = pin_rgba[:, :, 0].astype(np.float64)
    g = pin_rgba[:, :, 1].astype(np.float64)
    b = pin_rgba[:, :, 2].astype(np.float64)
    a = pin_rgba[:, :, 3]

    opaque = a > 10

    # ── Detect sphere via saturation ──
    c_max = np.maximum(np.maximum(r, g), b)
    c_min = np.minimum(np.minimum(r, g), b)
    delta = c_max - c_min
    sat = np.where(c_max > 0, (delta / c_max) * 255.0, 0.0)

    sphere_raw = (sat > SAT_THRESHOLD) & opaque
    sphere_filled = ndimage.binary_fill_holes(sphere_raw)

    labeled, num = ndimage.label(sphere_filled)
    if num > 0:
        sizes = ndimage.sum(np.ones_like(labeled), labeled, range(1, num + 1))
        biggest = np.argmax(sizes) + 1
        sphere_mask = (labeled == biggest)
    else:
        sphere_mask = sphere_filled

    # Erode to remove shaft-edge contamination for centroid/radius
    sphere_eroded = ndimage.binary_erosion(sphere_mask, iterations=5)
    sphere_eroded = ndimage.binary_fill_holes(sphere_eroded)

    labeled_e, num_e = ndimage.label(sphere_eroded)
    if num_e > 0:
        sizes_e = ndimage.sum(np.ones_like(labeled_e), labeled_e, range(1, num_e + 1))
        biggest_e = np.argmax(sizes_e) + 1
        sphere_core = (labeled_e == biggest_e)
    else:
        sphere_core = sphere_eroded if np.any(sphere_eroded) else sphere_mask

    coords = np.argwhere(sphere_core)
    if len(coords) == 0:
        coords = np.argwhere(sphere_mask)
    if len(coords) == 0:
        return pin_rgba, pin_rgba, {"error": "no sphere detected"}

    cy = float(np.mean(coords[:, 0]))
    cx = float(np.mean(coords[:, 1]))

    area = float(np.sum(sphere_core))
    radius_from_area = math.sqrt(area / math.pi) + 5  # +5 to undo erosion

    # The area-based radius is robust because the eroded core removes
    # shaft contamination. But for slightly non-circular spheres,
    # also check the median distance (not max/99th - those catch outliers).
    core_dists = np.sqrt((coords[:, 0] - cy) ** 2 + (coords[:, 1] - cx) ** 2)
    # Median distance for a filled circle is r * sqrt(0.5) ≈ 0.707r
    radius_from_median = float(np.median(core_dists)) / 0.707 + 5

    radius = max(radius_from_area, radius_from_median) + RADIUS_PAD

    # ── Distance from center for all pixels ──
    yy, xx = np.mgrid[:h, :w]
    dist = np.sqrt((xx - cx) ** 2 + (yy - cy) ** 2)

    # ── HEAD: pixels inside circle ──
    head_alpha = np.clip((radius - dist) / SOFT_EDGE_PX, 0.0, 1.0)
    head_alpha *= a.astype(np.float64) / 255.0

    head = pin_rgba.copy()
    head[:, :, 3] = (head_alpha * 255.0).astype(np.uint8)

    # Crop head to circle bounding box
    margin = 2
    hx0 = max(0, int(cx - radius - margin))
    hy0 = max(0, int(cy - radius - margin))
    hx1 = min(w, int(cx + radius + margin + 1))
    hy1 = min(h, int(cy + radius + margin + 1))
    head_cropped = head[hy0:hy1, hx0:hx1]

    # ── SHAFT: pixels outside the head exclusion zone ──
    # Combine circle-based and mask-based exclusion:
    # - Circle catches the geometric sphere shape
    # - Dilated high-saturation mask catches any non-circular protrusions
    # Only dilate high-sat pixels (not shaft pixels that leaked through)
    # by re-thresholding at a higher saturation to get just the sphere core
    high_sat = (sat > 80) & opaque  # stricter threshold for shaft exclusion
    high_sat_filled = ndimage.binary_fill_holes(high_sat)
    high_sat_dilated = ndimage.binary_dilation(high_sat_filled, iterations=SHAFT_INSET_PX)

    # Circle exclusion zone
    circle_exclusion = dist < (radius + SHAFT_INSET_PX)

    # Union: exclude anything that's either inside the enlarged circle
    # or near high-saturation pixels
    exclusion_zone = circle_exclusion | high_sat_dilated

    shaft_dist = ndimage.distance_transform_edt(~exclusion_zone)
    shaft_alpha = np.clip(shaft_dist / SOFT_EDGE_PX, 0.0, 1.0)
    shaft_alpha *= a.astype(np.float64) / 255.0

    shaft = pin_rgba.copy()
    shaft[:, :, 3] = (shaft_alpha * 255.0).astype(np.uint8)

    # Crop shaft to its bounding box
    shaft_img = Image.fromarray(shaft, "RGBA")
    shaft_bbox = shaft_img.getbbox()
    if shaft_bbox:
        shaft_cropped = np.array(shaft_img.crop(shaft_bbox))
        shaft_crop_offset = {"x": shaft_bbox[0], "y": shaft_bbox[1]}
    else:
        shaft_cropped = shaft
        shaft_crop_offset = {"x": 0, "y": 0}

    # ── Shaft geometry via PCA ──
    # Use opaque pixels outside the full circle (no overlap) for clean PCA
    shaft_geo_mask = opaque & (dist > radius)
    shaft_geo_coords = np.argwhere(shaft_geo_mask)

    tip = {"x": 0, "y": 0}
    shaft_angle = 0.0
    shaft_length = 0.0

    if len(shaft_geo_coords) > 10:
        centroid_s = np.mean(shaft_geo_coords, axis=0)
        centered_s = shaft_geo_coords - centroid_s
        cov = np.cov(centered_s.T)
        eigenvalues, eigenvectors = np.linalg.eigh(cov)
        axis_dir = eigenvectors[:, np.argmax(eigenvalues)]

        projections = centered_s @ axis_dir
        min_idx = np.argmin(projections)
        max_idx = np.argmax(projections)

        ep_a = shaft_geo_coords[min_idx]
        ep_b = shaft_geo_coords[max_idx]

        da = math.sqrt((ep_a[0] - cy) ** 2 + (ep_a[1] - cx) ** 2)
        db = math.sqrt((ep_b[0] - cy) ** 2 + (ep_b[1] - cx) ** 2)
        tip_pt = ep_a if da > db else ep_b

        tip = {"x": int(tip_pt[1]), "y": int(tip_pt[0])}

        dx = cx - tip_pt[1]
        dy = cy - tip_pt[0]
        shaft_angle = float(math.degrees(math.atan2(dx, -dy))) % 360.0
        shaft_length = float(math.sqrt(dx * dx + dy * dy))

    print(f" c=({cx:.0f},{cy:.0f}) r={radius:.0f} "
          f"shaft={shaft_angle:.0f}deg len={shaft_length:.0f}", end="")

    geometry = {
        "head_center": {"x": round(cx, 1), "y": round(cy, 1)},
        "head_radius": round(radius, 1),
        "head_crop_box": {"x0": hx0, "y0": hy0, "x1": hx1, "y1": hy1},
        "shaft_crop_offset": shaft_crop_offset,
        "tip": tip,
        "shaft_angle_deg": round(shaft_angle, 1),
        "shaft_length": round(shaft_length, 1),
        "image_size": {"w": w, "h": h},
    }

    return head_cropped, shaft_cropped, geometry


# ─────────────────────────────────────────────────────────────
# Shaft Highlight
# ─────────────────────────────────────────────────────────────

def lighten_shaft_axis(shaft_rgba: np.ndarray) -> np.ndarray:
    """Fill transparent center of shaft and add a specular highlight.

    The background removal step makes the shaft's central specular highlight
    transparent (it was near-white in the original photo). This function:
    1. Scans perpendicular cross-sections along the PCA axis
    2. For each cross-section, finds the two opaque edges and fills
       transparent pixels between them with interpolated color
    3. Applies a brightness boost concentrated at the center

    Args:
        shaft_rgba: (H, W, 4) uint8 RGBA array.

    Returns:
        Modified copy with filled center and specular highlight.
    """
    result = shaft_rgba.copy()
    h, w = result.shape[:2]
    a = result[:, :, 3]

    # Only process substantially opaque pixels (skip shadow/fringe)
    opaque = a > 128
    coords = np.argwhere(opaque)
    if len(coords) < 20:
        return result

    # PCA to find shaft axis
    centroid = np.mean(coords, axis=0)
    centered = coords - centroid
    cov = np.cov(centered.T)
    eigenvalues, eigenvectors = np.linalg.eigh(cov)

    axis_dir = eigenvectors[:, np.argmax(eigenvalues)]   # major axis (along shaft)
    perp_dir = eigenvectors[:, np.argmin(eigenvalues)]   # minor axis (across shaft)

    perp_dists = np.abs(centered @ perp_dir)
    half_width = float(np.percentile(perp_dists, 95))
    if half_width < 3:
        return result

    # ── Step 1: Fill transparent center by scanning cross-sections ──
    # Project all pixels onto the axis to get positions along the shaft
    axis_proj = centered @ axis_dir
    axis_min = float(np.min(axis_proj))
    axis_max = float(np.max(axis_proj))

    # Build a grid of all pixel positions relative to PCA frame
    yy, xx = np.mgrid[:h, :w]
    all_centered_y = yy - centroid[0]
    all_centered_x = xx - centroid[1]
    # Project every pixel onto axis and perp directions
    all_axis = all_centered_y * axis_dir[0] + all_centered_x * axis_dir[1]
    all_perp = all_centered_y * perp_dir[0] + all_centered_x * perp_dir[1]

    # Scan along the axis in small steps, filling cross-section gaps
    step = 1.0
    num_steps = int((axis_max - axis_min) / step) + 1
    fill_mask = np.zeros((h, w), dtype=bool)

    for i in range(num_steps):
        t = axis_min + i * step
        # Select pixels in this thin cross-section slice
        slice_mask = (np.abs(all_axis - t) < step * 0.6) & opaque
        if not np.any(slice_mask):
            continue

        # Get perpendicular positions of opaque pixels in this slice
        slice_perp = all_perp[slice_mask]
        p_min = float(np.min(slice_perp))
        p_max = float(np.max(slice_perp))

        # Only fill if there's a gap (span > 3 pixels)
        if (p_max - p_min) < 3:
            continue

        # Fill: any pixel in this slice that's between the edges but transparent
        in_slice = np.abs(all_axis - t) < step * 0.6
        in_span = (all_perp >= p_min) & (all_perp <= p_max)
        fill_mask |= (in_slice & in_span & (~opaque))

    # Also exclude shadow pixels (low alpha, far from shaft body)
    # Shadow tends to have alpha < 128 and be spatially separated
    fill_coords = np.argwhere(fill_mask)

    if len(fill_coords) > 0:
        # For each fill pixel, interpolate color from nearest opaque pixels
        dist_to_opaque, nearest_indices = ndimage.distance_transform_edt(
            ~opaque, return_distances=True, return_indices=True
        )

        fy = fill_coords[:, 0]
        fx = fill_coords[:, 1]

        # Get nearest opaque pixel coordinates
        nearest_y = nearest_indices[0][fy, fx]
        nearest_x = nearest_indices[1][fy, fx]

        # Use nearest opaque color as base, then lighten based on
        # how far inside the shaft this pixel is (center = brighter)
        base_r = result[nearest_y, nearest_x, 0].astype(np.float64)
        base_g = result[nearest_y, nearest_x, 1].astype(np.float64)
        base_b = result[nearest_y, nearest_x, 2].astype(np.float64)

        # Perpendicular position of fill pixels (how close to center)
        fill_perp = np.abs(all_perp[fy, fx])
        # Normalize: 0 = center of shaft, 1 = edge
        fill_norm = fill_perp / max(half_width, 1.0)

        # Lighten more toward center: center pixels get brighter fill
        center_boost = np.clip(1.0 - fill_norm, 0.0, 1.0) * 0.4

        result[fy, fx, 0] = np.clip(base_r + center_boost * (255.0 - base_r), 0, 255).astype(np.uint8)
        result[fy, fx, 1] = np.clip(base_g + center_boost * (255.0 - base_g), 0, 255).astype(np.uint8)
        result[fy, fx, 2] = np.clip(base_b + center_boost * (255.0 - base_b), 0, 255).astype(np.uint8)
        result[fy, fx, 3] = 255

        # Update opaque mask
        a = result[:, :, 3]
        opaque = a > 128

    # ── Step 2: Apply brightness boost along center (not edges) ──
    coords = np.argwhere(opaque)
    centered = coords - centroid
    perp_dists_all = np.abs(centered @ perp_dir)

    norm_dist = perp_dists_all / (half_width * SHAFT_HIGHLIGHT_FALLOFF)
    highlight = np.clip(1.0 - norm_dist ** 2, 0.0, 1.0) * SHAFT_HIGHLIGHT_STRENGTH

    r = result[:, :, 0].astype(np.float64)
    g = result[:, :, 1].astype(np.float64)
    b = result[:, :, 2].astype(np.float64)

    oy = coords[:, 0]
    ox = coords[:, 1]

    r[oy, ox] = np.clip(r[oy, ox] + highlight * (255.0 - r[oy, ox]), 0, 255)
    g[oy, ox] = np.clip(g[oy, ox] + highlight * (255.0 - g[oy, ox]), 0, 255)
    b[oy, ox] = np.clip(b[oy, ox] + highlight * (255.0 - b[oy, ox]), 0, 255)

    result[:, :, 0] = r.astype(np.uint8)
    result[:, :, 1] = g.astype(np.uint8)
    result[:, :, 2] = b.astype(np.uint8)

    return result


# ─────────────────────────────────────────────────────────────
# Preview
# ─────────────────────────────────────────────────────────────

def _make_checkerboard(width: int, height: int) -> Image.Image:
    ys = np.arange(height) // CHECKER_SIZE
    xs = np.arange(width) // CHECKER_SIZE
    grid = (ys[:, None] + xs[None, :]) % 2
    board = np.zeros((height, width, 4), dtype=np.uint8)
    board[grid == 0] = [180, 180, 180, 255]
    board[grid == 1] = [220, 220, 220, 255]
    return Image.fromarray(board, "RGBA")


def _generate_grid_preview(
    images: list[Image.Image], output_dir: str, filename: str,
) -> None:
    """Tile images on a checkerboard."""
    n = len(images)
    cols = PREVIEW_COLUMNS
    rows = (n + cols - 1) // cols
    cell = PREVIEW_TILE + 10
    cw = cols * cell + 10
    ch = rows * cell + 10

    canvas = _make_checkerboard(cw, ch)

    for i, img in enumerate(images):
        scale = min(PREVIEW_TILE / img.width, PREVIEW_TILE / img.height, 1.0)
        nw = max(1, int(img.width * scale))
        nh = max(1, int(img.height * scale))
        thumb = img.resize((nw, nh), Image.LANCZOS)

        col = i % cols
        row = i // cols
        x = 10 + col * cell + (PREVIEW_TILE - nw) // 2
        y = 10 + row * cell + (PREVIEW_TILE - nh) // 2
        canvas.paste(thumb, (x, y), thumb)

    path = os.path.join(output_dir, filename)
    canvas.save(path, "PNG")
    print(f"  {filename}  {cw}x{ch}  ({os.path.getsize(path)/1024:.0f} KB)")


# ─────────────────────────────────────────────────────────────
# Main
# ─────────────────────────────────────────────────────────────

def main() -> None:
    input_dir = os.path.normpath(INPUT_DIR)
    output_dir = os.path.normpath(OUTPUT_DIR)
    os.makedirs(output_dir, exist_ok=True)

    print(f"Input:  {input_dir}")
    print(f"Output: {output_dir}")
    print(f"Sat threshold: {SAT_THRESHOLD}, radius pad: {RADIUS_PAD}, "
          f"soft edge: {SOFT_EDGE_PX}px, shaft inset: {SHAFT_INSET_PX}px\n")

    manifest = {}
    heads = []
    shafts = []
    shafts_lit = []

    for idx in range(1, NUM_PINS + 1):
        filename = f"pin_{idx:02d}.png"
        filepath = os.path.join(input_dir, filename)

        if not os.path.isfile(filepath):
            print(f"  SKIP: {filename}")
            continue

        print(f"  {filename}", end="")
        pin_img = Image.open(filepath).convert("RGBA")
        pin_arr = np.array(pin_img)

        head_arr, shaft_arr, geometry = extract_parts(pin_arr)

        # Lighten shaft center axis
        shaft_lit_arr = lighten_shaft_axis(shaft_arr)

        head_img = Image.fromarray(head_arr, "RGBA")
        shaft_img = Image.fromarray(shaft_arr, "RGBA")
        shaft_lit_img = Image.fromarray(shaft_lit_arr, "RGBA")

        head_path = os.path.join(output_dir, f"pin_{idx:02d}_head.png")
        shaft_path = os.path.join(output_dir, f"pin_{idx:02d}_shaft.png")
        shaft_lit_path = os.path.join(output_dir, f"pin_{idx:02d}_shaft_lit.png")
        head_img.save(head_path, "PNG")
        shaft_img.save(shaft_path, "PNG")
        shaft_lit_img.save(shaft_lit_path, "PNG")

        hkb = os.path.getsize(head_path) / 1024
        skb = os.path.getsize(shaft_path) / 1024

        print(f"  -> head {head_img.width}x{head_img.height} ({hkb:.0f}KB)"
              f"  shaft {shaft_img.width}x{shaft_img.height} ({skb:.0f}KB)")

        manifest[f"pin_{idx:02d}"] = {
            "head_file": f"pin_{idx:02d}_head.png",
            "shaft_file": f"pin_{idx:02d}_shaft.png",
            "shaft_lit_file": f"pin_{idx:02d}_shaft_lit.png",
            **geometry,
        }
        heads.append(head_img)
        shafts.append(shaft_img)
        shafts_lit.append(shaft_lit_img)

    manifest_path = os.path.join(output_dir, "pin_parts_manifest.json")
    with open(manifest_path, "w") as f:
        json.dump(manifest, f, indent=2)
    print(f"\n  Manifest: {manifest_path}")

    print("  Generating previews ...")
    _generate_grid_preview(heads, output_dir, "preview_heads.png")
    _generate_grid_preview(shafts, output_dir, "preview_shafts.png")
    _generate_grid_preview(shafts_lit, output_dir, "preview_shafts_lit.png")

    print(f"\nDone. {len(manifest)} pins split.")


if __name__ == "__main__":
    main()
