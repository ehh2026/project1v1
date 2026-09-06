#!/usr/bin/env python3
"""Pre-bake web assets for the gallery map website (web-map-plan.md, Stage 1).

Reads a content set (Excel first — mirroring ContentLoader — locations.json as
fallback) and writes a static, self-contained web/ payload:

  web/images/map-base.jpg        intermediate base map (~4096 px, progressive)
  web/images/content/<Name>/…    bounded popup derivatives (max 1600 px, q80)
  web/data/locations.json        locations with normalized coords + altText

Coordinate contract (supersedes the plan's earlier lat/lon framing): the
source data is PIXEL coordinates on the map image, not geographic lat/lon.
  - Excel columns E/F ("Coordinate X/Y halfsize") are the primary frame,
    interpreted against the 8198×5542 base image (ContentLoader prefers E/F).
  - Excel columns B/C (and locations.json PixelX/PixelY) are the fallback frame,
    interpreted against the 16397×11085 full-res master.
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
import xml.etree.ElementTree as ET

from PIL import Image

Image.MAX_IMAGE_PIXELS = None  # the 181 MP master trips Pillow's default guard

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
    try:
        data = zf.read("xl/sharedStrings.xml")
    except KeyError:
        return []
    root = ET.fromstring(data)
    return ["".join(t.text or "" for t in si.iter(f"{NS}t")) for si in root.iter(f"{NS}si")]


def sheet_paths(zf: zipfile.ZipFile) -> list[str]:
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
        x = row.get("E", "").strip()  # half-size frame preferred (ContentLoader parity)
        y = row.get("F", "").strip()
        frame = (BASE_W, BASE_H)
        if not x or not y:
            x, y = row.get("B", "").strip(), row.get("C", "").strip()
            frame = (MASTER_W, MASTER_H)
        try:
            px, py = float(x), float(y)
        except ValueError:
            continue
        locations.append({
            "name": name,
            "nx": px / frame[0],
            "ny": py / frame[1],
            "address": row.get("D", "").strip(),
            "bio": bio_by_name.get(name, ""),
            "captions": captions.get(name, {}),
            "_raw_row": row,
        })
    return locations


def parse_locations_json(path: str) -> list[dict]:
    with open(path, encoding="utf-8") as fh:
        data = json.load(fh)
    return [{
        "name": item.get("Name", ""),
        "nx": float(item.get("PixelX", 0)) / MASTER_W,
        "ny": float(item.get("PixelY", 0)) / MASTER_H,
        "address": item.get("ContentFilePath", ""),
        "bio": "",
        "captions": {},
        "_raw_row": {},
    } for item in data if item.get("Name")]


def find_excel_image_names(excel_path: str) -> dict[str, list[str]]:
    """Return {location_name: [image file names…]} from the location sheet,
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
    """Excel first, locations.json fallback — ContentLoader precedence."""
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


def prepare_base_map(out_dir: str, base_width: int) -> tuple[int, int, int]:
    print(f"Generating base map from {os.path.basename(MASTER_MAP)}…")
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
    os.makedirs(dst_dir, exist_ok=True)
    out = os.path.join(dst_dir, os.path.basename(src))
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
    os.makedirs(web_images, exist_ok=True)
    os.makedirs(web_data, exist_ok=True)

    base_w, base_h, base_bytes = prepare_base_map(web_images, WEB_BASE_WIDTH)

    locations, source = load_locations(content_dir)
    print(f"Loaded {len(locations)} locations from {source} in {content_dir}")

    out_locations = []
    total_popup_bytes = 0
    for i, loc in enumerate(locations, start=1):
        safe = re.sub(r"[^\w\-. ]", "_", loc["name"]).strip() or f"loc_{i:03d}"
        folder = os.path.join(content_dir, loc["name"])
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
            if not os.path.isfile(src):
                print(f"  WARNING: listed image missing on disk: {loc['name']}/{name}")
                alt = loc["captions"].get(name) or f"{loc['name']} image {j}"
                images_out.append({"file": name, "alt": alt, "missing": True})
                continue
            dst_rel, size = derivative(src, os.path.join(web_images, "content", safe))
            total_popup_bytes += size
            alt = loc["captions"].get(name) or f"{loc['name']} image {j}"
            print(f"  {loc['name']}/{name} -> {size / 1024:.0f} KB")
            images_out.append({
                "file": os.path.relpath(dst_rel, args.out).replace("\\", "/"),
                "alt": alt,
            })

        out_locations.append({
            "id": f"loc_{i:03d}",
            "name": loc["name"],
            "nx": loc["nx"],
            "ny": loc["ny"],
            "address": loc["address"],
            "bio": loc["bio"],
            "images": images_out,
        })

    loc_path = os.path.join(web_data, "locations.json")
    with open(loc_path, "w", encoding="utf-8") as fh:
        json.dump({
            "map": {"width": base_w, "height": base_h, "image": "images/map-base.jpg"},
            "locations": out_locations,
            "provenance": {
                "contentSet": os.path.relpath(content_dir, REPO_ROOT).replace("\\", "/"),
                "source": source,
            },
        }, fh, indent=2, ensure_ascii=False)

    total_mb = (total_popup_bytes + base_bytes) / 1_048_576
    print()
    print(f"Wrote {loc_path} ({len(out_locations)} locations)")
    print(f"Popup derivatives: {total_popup_bytes / 1_048_576:.2f} MB total; "
          f"payload with base map: {total_mb:.2f} MB")
    return 0


if __name__ == "__main__":
    sys.exit(main())
