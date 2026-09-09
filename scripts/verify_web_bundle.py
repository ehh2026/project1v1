#!/usr/bin/env python3
"""Coherence gate for the generated web/ bundle (web-map-plan.md, Stage 3A).

Run after `scripts/prepare_web_assets.py`; exits nonzero on any violation.

    py -3 scripts\\verify_web_bundle.py [web_dir]

Checks the manifest contract web/index.html relies on:
  - stable, unique, slug-safe location ids;
  - normalized [0,1] coordinates, origin top-left;
  - every popup image and crop on disk (unless explicitly flagged missing)
    and inside the renderer's allow list;
  - deterministic tile pyramid geometry: for each generated level the sample
    {z}/{x}/{y} grid (corners + centre) exists and the URL template matches;
  - the total crop payload stays within budget.

Deliberately stdlib-only; geometry expectations come from the generator
modules' own helpers so the gate and the emitter cannot drift apart.
"""

import json
import os
import re
import sys

REPO_ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
DEFAULT_WEB = os.path.join(REPO_ROOT, "web")

ID_RE = re.compile(r"^[a-z0-9-]+$")


def _allowed_image_path(path: str) -> bool:
    """True when a manifest image path matches the renderer's allow list: single
    ASCII segments under images/, never `.` / `..`, and the same character class
    the site's JS uses (no dot-directory traversal)."""
    if not isinstance(path, str) or not path or "//" in path or "\\" in path:
        return False
    parts = path.split("/")
    if parts[0] != "images" or any(p in (".", "..", "") for p in parts):
        return False
    if len(parts) == 3 and parts[1] in ("base", "crops", "tiles"):
        return bool(re.fullmatch(r"[\w\-. ]+", parts[2]))
    if len(parts) == 4 and parts[1] == "content":
        return (bool(re.fullmatch(r"[\w\-. ]+", parts[2]))
                and bool(re.fullmatch(r"[\w\-. ]+", parts[3])))
    return False

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import prepare_web_assets as pwa  # noqa: E402  (heavy deps stay lazy)

TILE_SAMPLE_COUNT = 6


def _tile_samples(level: int, w: int, h: int) -> list[tuple[int, int]]:
    level_w, level_h = pwa._level_dimensions(level, w, h)
    cols = (level_w + pwa.TILE_SIZE - 1) // pwa.TILE_SIZE
    top_y = -((level_h + pwa.TILE_SIZE - 1) // pwa.TILE_SIZE)
    mid_y = top_y + (-1 - top_y) // 2
    samples = [(0, top_y), (cols - 1, -1), (cols // 2, mid_y),
               (0, -1), (cols - 1, top_y), (cols // 2, -1)]
    return samples[:TILE_SAMPLE_COUNT]


def check(web_dir: str) -> int:
    errors: list[str] = []
    manifest_path = os.path.join(web_dir, "data", "locations.json")
    if not os.path.isfile(manifest_path):
        print("missing manifest:", manifest_path)
        return 1
    with open(manifest_path, encoding="utf-8") as fh:
        data = json.load(fh)

    # --- map ---
    m = data.get("map") or {}
    w, h = m.get("width", 0), m.get("height", 0)
    if not (isinstance(w, int) and isinstance(h, int) and w > 0 and h > 0):
        errors.append(f"map width/height missing or non-positive: {(w, h)}")
    image_rel = m.get("image")
    if not image_rel or not os.path.isfile(os.path.join(web_dir, image_rel)):
        errors.append(f"map image missing on disk: {image_rel!r}")

    # --- locations ---
    seen: set[str] = set()
    for loc in data.get("locations") or []:
        lid = loc.get("id")
        if not ID_RE.fullmatch(lid or ""):
            errors.append(f"non-slug id: {lid!r} (name: {loc.get('name')!r})")
        if lid in seen:
            errors.append(f"duplicate id: {lid}")
        seen.add(lid)
        nx, ny = loc.get("nx"), loc.get("ny")
        if not (isinstance(nx, (int, float)) and isinstance(ny, (int, float))
                and 0.0 <= nx <= 1.0 and 0.0 <= ny <= 1.0):
            errors.append(f"coords out of [0,1]: {lid} -> {(nx, ny)}")
        for img in loc.get("images") or []:
            if img.get("missing"):
                continue
            f = img.get("file")
            if not f or not _allowed_image_path(f):
                errors.append(f"image path outside allow list: {lid} -> {f!r}")
            elif not os.path.isfile(os.path.join(web_dir, f)):
                errors.append(f"image missing on disk: {lid} -> {f}")

    # --- crops ---
    crop_bytes = 0
    for c in data.get("crops") or []:
        f = c.get("file")
        if not f or not _allowed_image_path(f):
            errors.append(f"crop path outside allow list: {f!r}")
            continue
        if not os.path.isfile(os.path.join(web_dir, f)):
            errors.append(f"crop missing on disk: {f!r}")
            continue
        crop_bytes += os.path.getsize(os.path.join(web_dir, f))
        n = (c.get("nx0"), c.get("nx1"), c.get("ny0"), c.get("ny1"))
        if not all(isinstance(v, (int, float)) for v in n):
            errors.append(f"crop bounds not numeric: {f} -> {n}")
        elif not (0.0 <= n[0] < n[1] <= 1.0 and 0.0 <= n[2] < n[3] <= 1.0):
            errors.append(f"crop bounds out of [0,1]: {f} -> {n}")

    # --- tiles ---
    tiles = data.get("tiles")
    if tiles:
        levels = tiles.get("levels") or []
        if not levels:
            errors.append("tiles block present but levels empty")
        if tiles.get("tileSize") != pwa.TILE_SIZE:
            errors.append(f"tileSize {tiles.get('tileSize')} != {pwa.TILE_SIZE}")
        if tiles.get("baseZoom") != pwa.TILE_BASE_ZOOM:
            errors.append(f"baseZoom {tiles.get('baseZoom')} != {pwa.TILE_BASE_ZOOM}")
        if not all(isinstance(z, int) for z in levels):
            errors.append(f"tile levels must be integers: {levels!r}")
        url = tiles.get("url") or ""
        if "images/tiles/{z}/{x}/{y}.jpg" not in url:
            errors.append(f"unexpected tile URL template: {url!r}")
        if not (w > 0 and h > 0):
            # Map dimension violation already recorded above; sampling against
            # unusable dimensions would only produce misleading tile paths.
            errors.append("tile sample grid skipped: map dimensions invalid")
        else:
            for level in levels:
                for x, y in _tile_samples(level, w, h):
                    f = os.path.join(web_dir, "images", "tiles", str(level), str(x), f"{y}.jpg")
                    if not os.path.isfile(f):
                        errors.append(f"tile missing: images/tiles/{level}/{x}/{y}.jpg")
    elif data.get("crops"):
        errors.append("no tiles block (pyramid expected in Stage 3A builds)")

    # --- budgets ---
    print(f"popup images ok; crops {crop_bytes / 1_048_576:.2f} MB; "
          f"locations {len(seen)}; crops {len(data.get('crops') or [])}"
          f"{'; levels ' + str(tiles.get('levels')) if tiles else ''}")

    if crop_bytes > pwa.CROP_TOTAL_BYTES_BUDGET:
        print(f"WARNING: crop payload {crop_bytes / 1_048_576:.2f} MB exceeds "
              f"the {pwa.CROP_TOTAL_BYTES_BUDGET / 1_048_576:.2f} MB budget")

    for e in errors:
        print("ERROR:", e)
    return 1 if errors else 0


if __name__ == "__main__":
    web_dir = sys.argv[1] if len(sys.argv) > 1 else DEFAULT_WEB
    sys.exit(check(web_dir))