#!/usr/bin/env python3
"""Pre-bake web assets for the gallery map website (web-map-plan.md, Stage 1).

Reads a content set (Excel first -- mirroring ContentLoader -- locations.json as
fallback) and writes a static, self-contained web/ payload:

  web/images/map-base.jpg        intermediate base map (~4096 px, progressive)
  web/images/content/<Name>/...   bounded popup derivatives (max 1600 px, q80)
  web/data/locations.json        locations with normalized coords + altText

Coordinate contract (supersedes the plan's earlier lat/lon framing): the
source data is PIXEL coordinates on the map image, not geographic lat/lon.
  - Excel columns E/F ("Coordinate X/Y halfsize") are the primary frame,
    interpreted against the 8198 x 5542 base image (ContentLoader prefers E/F).
  - Excel columns B/C (and locations.json PixelX/PixelY) are the fallback frame,
    interpreted against the 16397 x 11085 full-res master.
This script normalizes both into [0,1] fractions (nx, ny, origin top-left) so
the web renderer never has to think about which frame a value came from.
The site's Leaflet map uses CRS.Simple with bounds [[0,0],[height,width]] in
base-image pixels: marker lng = nx*W, lat = (1-ny)*H. Tested by
web/test-projection.html.

Usage (Windows, repo venv):
    .\\scripts\\venv\\Scripts\\python.exe scripts\\prepare_web_assets.py
    .\\scripts\\venv\\Scripts\\python.exe scripts\\prepare_web_assets.py --content-set "Images&Content\\Demo-Content"
"""

from __future__ import annotations

import argparse
import json
import os
import re
import sys
import zipfile
# Heavy deps are imported lazily inside the functions that use them, so the
# module (and its tests) import cleanly without Pillow/defusedxml installed.
# When they are used, Image.MAX_IMAGE_PIXELS is set to a finite cap just above
# the known 181 MP master so Pillow still rejects bomb tensors.

REPO_ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
ASSETS_DIR = os.path.join(REPO_ROOT, "Images&Content", "Assets")
DEFAULT_CONTENT = os.path.join(REPO_ROOT, "Images&Content", "Demo-Content")
MASTER_MAP = os.path.join(ASSETS_DIR, "World Map 1976.jpg")

MASTER_W, MASTER_H = 16397.0, 11085.0   # full-res frame (Excel B/C, locations.json)
BASE_W, BASE_H = 8198.0, 5542.0         # half-size frame (Excel E/F)
WEB_BASE_WIDTH = 4096                   # ~11.3 MP, under the iPhone ~16.7 MP limit
POPUP_MAX_EDGE = 1600
POPUP_QUALITY = 80

NS = "{http://schemas.openxmlformats.org/spreadsheetml/2006/main}"


def read_shared_strings(zf: zipfile.ZipFile) -> list[str]:
    import defusedxml.ElementTree as ET
    try:
        data = zf.read("xl/sharedStrings.xml")
    except KeyError:
        return []
    root = ET.fromstring(data)
    return ["".join(t.text or "" for t in si.iter(f"{NS}t")) for si in root.iter(f"{NS}si")]


def sheet_paths(zf: zipfile.ZipFile) -> list[str]:
    import defusedxml.ElementTree as ET
    workbook = ET.fromstring(zf.read("xl/workbook.xml"))
    rels = ET.fromstring(zf.read("xl/_rels/workbook.xml.rels"))
    rel_map = {r.get("Id"): r.get("Target", "") for r in rels}
    paths = []
    for sheet in workbook.iter(f"{NS}sheet"):
        rid = sheet.get("{http://schemas.openxmlformats.org/officeDocument/2006/relationships}id")
        target = rel_map.get(rid, "").replace("\\", "/")
        if target:
            paths.append("xl/" + target)
    return paths


def read_sheet(zf: zipfile.ZipFile, path: str, shared: list[str]) -> list[dict[str, str]]:
    import defusedxml.ElementTree as ET
    rows = []
    root = ET.fromstring(zf.read(path))
    for row in root.iter(f"{NS}row"):
        cells: dict[str, str] = {}
        for c in row.iter(f"{NS}c"):
            ref = c.get("r", "")
            col = "".join(ch for ch in ref if ch.isalpha())
            ctype = c.get("t", "")
            value = "".join(v.text or "" for v in c.iter(f"{NS}v"))
            inline = "".join(t.text or "" for t in c.iter(f"{NS}t"))
            if ctype == "inlineStr":
                cells[col] = inline
            elif ctype == "s":
                try:
                    cells[col] = shared[int(value)]
                except (ValueError, IndexError):
                    cells[col] = value
            else:
                cells[col] = value
        rows.append(cells)
    return rows


def parse_excel(excel_path: str) -> list[dict]:
    locations = []
    with zipfile.ZipFile(excel_path) as zf:
        shared = read_shared_strings(zf)
        sheets = sheet_paths(zf)
        if not sheets:
            return []
        loc_rows = read_sheet(zf, sheets[0], shared)[1:]  # header row
        bio_rows = read_sheet(zf, sheets[1], shared)[1:] if len(sheets) > 1 else []
        cap_rows = read_sheet(zf, sheets[2], shared)[1:] if len(sheets) > 2 else []

    bio_by_name = {r.get("A", ""): r.get("B", "") for r in bio_rows if r.get("A", "").strip()}
    captions: dict[str, dict[str, str]] = {}
    for r in cap_rows:
        name = r.get("A", "").strip()
        if name:
            captions.setdefault(name, {})[r.get("B", "")] = r.get("C", "")

    for row in loc_rows:
        name = row.get("A", "").strip()
        if not name:
            continue
        coords = _validated_coords(
            row.get("E", ""), row.get("F", ""), BASE_W, BASE_H,
            row.get("B", ""), row.get("C", ""), MASTER_W, MASTER_H, name)
        if coords is None:
            continue
        locations.append({
            "name": name,
            "nx": coords[0],
            "ny": coords[1],
            "address": row.get("D", "").strip(),
            "bio": bio_by_name.get(name, ""),
            "captions": captions.get(name, {}),
            "_raw_row": row,
        })
    return locations


def _validated_coords(px_a: str, py_a: str, w_a: float, h_a: float,
                      px_b: str, py_b: str, w_b: float, h_b: float,
                      name: str) -> tuple[float, float] | None:
    """Return normalized (nx, ny) from the primary frame, falling back to the
    secondary for both missing and out-of-frame values. Skips only when both fail."""
    for label, xs, ys, fw, fh in (("primary", px_a, py_a, w_a, h_a),
                                  ("secondary", px_b, py_b, w_b, h_b)):
        try:
            x, y = float(xs), float(ys)
        except (TypeError, ValueError):
            continue
        if 0.0 <= x <= fw and 0.0 <= y <= fh:
            return x / fw, y / fh
        print(f"  WARNING: {name}: {label}-frame coordinate ({x}, {y}) outside {fw:.0f}x{fh:.0f}; trying fallback")
    print(f"  WARNING: {name}: no usable coordinates; skipping")
    return None


def web_safe_name(original_basename: str) -> str:
    """Renderer-whitelist-safe derivative name. Names that survive sanitization
    unchanged pass through; names that lose characters get a short digest of the
    original appended, so two different sources can never collide on output."""
    import hashlib
    stem, ext = os.path.splitext(original_basename)
    safe_stem = re.sub(r"[^\w\-. ]", "_", stem)
    if safe_stem == stem:
        return original_basename
    digest = hashlib.sha1(original_basename.encode("utf-8")).hexdigest()[:8]
    return f"{safe_stem}.{digest}{ext.lower()}"


def parse_locations_json(path: str) -> list[dict]:
    with open(path, encoding="utf-8") as fh:
        data = json.load(fh)
    # Accept either a bare list or a wrapper containing the list.
    if isinstance(data, dict):
        for key in ("locations", "Locations"):
            if isinstance(data.get(key), list):
                data = data[key]
                break
    if not isinstance(data, list):
        raise SystemExit(f"locations.json at {path} must be a list of location objects (or {{'locations': [...]}})")
    out = []
    for item in data:
        name = item.get("Name", "").strip()
        if not name:
            continue
        coords = _validated_coords("", "", 0, 0,
                                   str(item.get("PixelX", "")), str(item.get("PixelY", "")),
                                   MASTER_W, MASTER_H, name)
        if coords is None:
            continue
        out.append({
            "name": name,
            "nx": coords[0],
            "ny": coords[1],
            "address": item.get("ContentFilePath", ""),
            "bio": "",
            "captions": {},
            "_raw_row": {},
        })
    return out


def find_excel_image_names(excel_path: str) -> dict[str, list[str]]:
    """Return {location_name: [image file names...]} from the location sheet,
    using the 'Image N filename' headers (ContentLoader parity)."""
    with zipfile.ZipFile(excel_path) as zf:
        shared = read_shared_strings(zf)
        sheets = sheet_paths(zf)
        if not sheets:
            return {}
        rows = read_sheet(zf, sheets[0], shared)
    if len(rows) < 2:
        return {}
    header = rows[0]
    image_cols = [col for col, text in header.items() if text.strip().lower().startswith("image ")]
    result: dict[str, list[str]] = {}
    for row in rows[1:]:
        name = row.get("A", "").strip()
        if name:
            result[name] = [row[c].strip() for c in image_cols if row.get(c, "").strip()]
    return result


def load_locations(content_dir: str) -> tuple[list[dict], str]:
    """Excel first, locations.json fallback -- ContentLoader precedence."""
    excel = os.path.join(content_dir, "Coordinates for map.xlsx")
    if os.path.isfile(excel):
        locs = parse_excel(excel)
        if locs:
            images = find_excel_image_names(excel)
            for loc in locs:
                loc["images"] = images.get(loc["name"], [])
            return locs, "excel"
    json_path = os.path.join(content_dir, "locations.json")
    locs = parse_locations_json(json_path)
    for loc in locs:
        loc["images"] = []
    return locs, "json"


def compute_dense_clusters(locations: list[dict], radius_px: float = 500,
                           min_members: int = 1) -> list[list[dict]]:
    """Union-find over normalized coords: two locations cluster if within
    radius_px master pixels (~0.0305 of width) of an existing member.
    min_members=1 means even an isolated pin gets its own crop — visitors zoom
    into single pins, so every location should resolve sharply."""
    radius_norm = radius_px / MASTER_W
    groups: list[list[dict]] = []
    for loc in locations:
        placed = False
        for group in groups:
            if any(abs(g["nx"] - loc["nx"]) <= radius_norm and
                   abs(g["ny"] - loc["ny"]) <= radius_norm * (MASTER_W / MASTER_H)
                   for g in group):
                group.append(loc)
                placed = True
                break
        if not placed:
            groups.append([loc])
    # merge groups that became connected transitively
    merged = True
    while merged:
        merged = False
        for i in range(len(groups)):
            for j in range(i + 1, len(groups)):
                if any(abs(a["nx"] - b["nx"]) <= radius_norm and
                       abs(a["ny"] - b["ny"]) <= radius_norm * (MASTER_W / MASTER_H)
                       for a in groups[i] for b in groups[j]):
                    groups[i].extend(groups[j])
                    del groups[j]
                    merged = True
                    break
            if merged:
                break
    return [g for g in groups if len(g) >= min_members]


def cut_crops(clusters: list[list[dict]], out_dir: str) -> list[dict]:
    """Cut one full-res crop per dense cluster from the 16397×11085 master.
    Returns manifest entries with normalized bounds for the renderer."""
    from PIL import Image
    Image.MAX_IMAGE_PIXELS = 200_000_000
    if not clusters:
        return []
    os.makedirs(out_dir, exist_ok=True)
    crops = []
    with Image.open(MASTER_MAP) as master:
        for idx, group in enumerate(clusters, start=1):
            # Pad the bounding box generously so pins are not on the crop edge.
            pad = 800.0 / MASTER_W  # 800 master px
            nx0 = max(0.0, min(l["nx"] for l in group) - pad)
            nx1 = min(1.0, max(l["nx"] for l in group) + pad)
            ny0 = max(0.0, min(l["ny"] for l in group) - pad)
            ny1 = min(1.0, max(l["ny"] for l in group) + pad)
            box = (round(nx0 * master.width), round(ny0 * master.height),
                   round(nx1 * master.width), round(ny1 * master.height))
            crop = master.crop(box).convert("RGB")
            name = f"crop_{idx:02d}.jpg"
            path = os.path.join(out_dir, name)
            crop.save(path, "JPEG", quality=85, progressive=True, optimize=True)
            crops.append({
                "file": f"images/crops/{name}",
                "nx0": nx0, "ny0": ny0, "nx1": nx1, "ny1": ny1,
                "members": [l["name"] for l in group],
            })
            print(f"  crop {name}: {(box[2]-box[0])}x{(box[3]-box[1])} px, "
                  f"{os.path.getsize(path) / 1_048_576:.2f} MB, {len(group)} pins")
    return crops


def prepare_base_map(out_dir: str, base_width: int) -> tuple[int, int, int]:
    import PIL.Image as _PILImage
    _PILImage.MAX_IMAGE_PIXELS = 200_000_000  # finite cap above the 181 MP master
    Image = _PILImage
    print(f"Generating base map from {os.path.basename(MASTER_MAP)}...")
    with Image.open(MASTER_MAP) as img:
        w, h = img.size
        if w != MASTER_W or h != MASTER_H:
            print(f"  WARNING: master measured {w}x{h}, expected {MASTER_W:.0f}x{MASTER_H:.0f}")
        new_h = round(h * base_width / w)
        base = img.convert("RGB").resize((base_width, new_h), Image.LANCZOS)
        path = os.path.join(out_dir, "map-base.jpg")
        base.save(path, "JPEG", quality=82, progressive=True, optimize=True)
    size = os.path.getsize(path)
    print(f"  map-base.jpg: {base_width}x{new_h}, {size / 1_048_576:.2f} MB")
    return base_width, new_h, size


def derivative(src: str, dst_dir: str) -> tuple[str, int]:
    from PIL import Image
    Image.MAX_IMAGE_PIXELS = 200_000_000
    os.makedirs(dst_dir, exist_ok=True)
    # Sanitize the basename: renderer's src whitelist is [\w\-. ] only -- raw
    # artwork names with '(', ')' etc. would be silently dropped otherwise.
    out = os.path.join(dst_dir, web_safe_name(os.path.basename(src)))
    with Image.open(src) as img:
        img = img.convert("RGB")
        if max(img.size) > POPUP_MAX_EDGE:
            ratio = POPUP_MAX_EDGE / max(img.size)
            img = img.resize((round(img.width * ratio), round(img.height * ratio)), Image.LANCZOS)
        img.save(out, "JPEG", quality=POPUP_QUALITY, progressive=True, optimize=True)
    return out, os.path.getsize(out)


def main() -> int:
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--content-set", default=DEFAULT_CONTENT,
                    help="Content set folder (contains Excel and location folders)")
    ap.add_argument("--out", default=os.path.join(REPO_ROOT, "web"), help="Output web/ directory")
    args = ap.parse_args()

    content_dir = args.content_set
    if not os.path.isdir(content_dir):
        print(f"Content set not found: {content_dir}", file=sys.stderr)
        return 2

    web_images = os.path.join(args.out, "images")
    web_data = os.path.join(args.out, "data")
    # Clear generated outputs so reruns can't leave stale dirs behind.
    for stale in (web_images, web_data):
        if os.path.isdir(stale):
            import shutil
            shutil.rmtree(stale)
    os.makedirs(web_images, exist_ok=True)
    os.makedirs(web_data, exist_ok=True)

    base_w, base_h, base_bytes = prepare_base_map(web_images, WEB_BASE_WIDTH)

    locations, source = load_locations(content_dir)
    print(f"Loaded {len(locations)} locations from {source} in {content_dir}")

    out_locations = []
    total_popup_bytes = 0
    for i, loc in enumerate(locations, start=1):
        safe = re.sub(r"[^\w\-. ]", "_", loc["name"]).strip() or f"loc_{i:03d}"
        safe = f"{safe}__loc_{i:03d}"  # collision-proof even for duplicate names
        folder = os.path.join(content_dir, loc["name"])
        folder_real = os.path.realpath(folder)
        image_names = loc["images"]
        if not image_names and os.path.isdir(folder):
            image_names = sorted(os.listdir(folder))
            image_names = [n for n in image_names
                           if os.path.splitext(n)[1].lower() in (".jpg", ".jpeg", ".png")]

        images_out = []
        for j, name in enumerate(image_names, start=1):
            src = os.path.join(folder, name)
            ext = os.path.splitext(name)[1].lower()
            if ext not in (".jpg", ".jpeg", ".png"):
                continue
            # Containment: the referenced file must live inside the location folder.
            src_real = os.path.realpath(src)
            if not (src_real == folder_real or src_real.startswith(folder_real + os.sep)):
                print(f"  WARNING: {loc['name']}/{name}: path escapes location folder; skipping")
                continue
            if not os.path.isfile(src_real):
                print(f"  WARNING: listed image missing on disk: {loc['name']}/{name}")
                alt = loc["captions"].get(name) or f"{loc['name']} image {j}"
                images_out.append({"file": name, "altText": alt, "missing": True})
                continue
            dst_rel, size = derivative(src_real, os.path.join(web_images, "content", safe))
            total_popup_bytes += size
            alt = loc["captions"].get(name) or f"{loc['name']} image {j}"
            print(f"  {loc['name']}/{name} -> {size / 1024:.0f} KB")
            images_out.append({
                "file": os.path.relpath(dst_rel, args.out).replace("\\", "/"),
                "altText": alt,
            })

        # Copy text sidecars (didactic + caption files) unchanged; renderer
        # consumes the pre-baked values, but keeping them documents provenance.
        if os.path.isdir(folder):
            sidecar_dir = os.path.join(web_images, "content", safe)
            for f in os.listdir(folder):
                if f.endswith(".txt") and ("didactic" in f.lower() or "caption" in f.lower()):
                    os.makedirs(sidecar_dir, exist_ok=True)
                    src_txt = os.path.realpath(os.path.join(folder, f))
                    if src_txt.startswith(folder_real + os.sep):
                        with open(src_txt, "rb") as sf, open(os.path.join(sidecar_dir, f), "wb") as df:
                            df.write(sf.read())

        out_locations.append({
            "id": f"loc_{i:03d}",
            "name": loc["name"],
            "nx": loc["nx"],
            "ny": loc["ny"],
            "address": loc["address"],
            "bio": loc["bio"],
            "images": images_out,
        })

    # Regional high-res crops for dense clusters (so zoomed pin areas stay sharp).
    clusters = compute_dense_clusters(out_locations)
    crops = cut_crops(clusters, os.path.join(web_images, "crops"))

    loc_path = os.path.join(web_data, "locations.json")
    with open(loc_path, "w", encoding="utf-8") as fh:
        json.dump({
            "map": {"width": base_w, "height": base_h, "image": "images/map-base.jpg"},
            "crops": crops,
            "locations": out_locations,
            "provenance": {
                "contentSet": os.path.relpath(content_dir, REPO_ROOT).replace("\\", "/"),
                "source": source,
            },
        }, fh, indent=2, ensure_ascii=False)

    crop_bytes = sum(os.path.getsize(os.path.join(REPO_ROOT, "web", c["file"]))
                     for c in crops if os.path.isfile(os.path.join(REPO_ROOT, "web", c["file"])))
    total_mb = (total_popup_bytes + base_bytes + crop_bytes) / 1_048_576
    print()
    print(f"Wrote {loc_path} ({len(out_locations)} locations, {len(crops)} crops)")
    print(f"Popup derivatives: {total_popup_bytes / 1_048_576:.2f} MB; "
          f"crops: {crop_bytes / 1_048_576:.2f} MB; total: {total_mb:.2f} MB")
    return 0


if __name__ == "__main__":
    sys.exit(main())
