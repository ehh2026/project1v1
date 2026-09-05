#!/usr/bin/env python3
"""Audit Images&Content for files that no reference source mentions.

Read-only. Emits a summary plus a CSV of candidate "never-referenced" assets
for human review before any deletion or web-bundle exclusion.

Reference sources scanned for each file's name (case-insensitive):
  - every *.json under Images&Content/ (locations.json, manual-layouts.json, ...)
  - every *.xlsx under Images&Content/ (sharedStrings inside the zip)
  - repo code/config: *.cs, *.xaml, *.json outside Images&Content/ and bin/obj

Implicitly referenced (never flagged):
  - Files inside a location content folder (Demo-Content|Production-Content/<Name>/...):
    ContentLoader enumerates these directories at runtime.
  - Everything under Assets/Pins_v2/ (pin composition is rule-driven from code).
  - Non-image sidecar files (.json, .xlsx, .gitkeep, .zip, .md, .txt).

Matching rules (per CodeRabbit review PR #34):
  1. A file is referenced when its normalized relative path (case-insensitive,
     forward slashes) appears in a reference source — or when a referenced path
     ends with that file's name (partial path in config like "Assets/x.png").
  2. A bare basename match counts only when that basename is unique across the
     whole content tree — otherwise `Extras/foo.png` would be wrongly suppressed
     by a reference to `Assets/foo.png` (false negative) or vice versa.

Usage (Windows):
    py -3 scripts/audit_unused_assets.py
    py -3 scripts/audit_unused_assets.py --csv TestResults/unused-assets.csv
"""

from __future__ import annotations

import argparse
import csv
import os
import re
import sys
import zipfile

REPO_ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
CONTENT_DIR = os.path.join(REPO_ROOT, "Images&Content")
SIDECAR_EXTENSIONS = {".json", ".xlsx", ".gitkeep", ".zip", ".md", ".txt"}
CODE_EXTENSIONS = {".cs", ".xaml", ".json", ".csproj"}
SKU_DIRS = ("Demo-Content", "Production-Content")


def collect_reference_text() -> str:
    """Concatenate every text source that can reference a content file name."""
    chunks: list[str] = []

    # JSON configs inside the content tree (locations.json, manual-layouts.json, ...)
    for root, _, files in os.walk(CONTENT_DIR):
        for name in files:
            if name.lower().endswith(".json"):
                path = os.path.join(root, name)
                try:
                    with open(path, "r", encoding="utf-8", errors="replace") as fh:
                        chunks.append(fh.read())
                except OSError:
                    continue

    # Excel workbooks: scan shared strings and sheet XML for file names.
    for root, _, files in os.walk(CONTENT_DIR):
        for name in files:
            if not name.lower().endswith(".xlsx"):
                continue
            path = os.path.join(root, name)
            try:
                with zipfile.ZipFile(path) as zf:
                    for member in zf.namelist():
                        if member.endswith(".xml") and (
                            "sharedStrings" in member or member.startswith("xl/worksheets/")
                        ):
                            chunks.append(zf.read(member).decode("utf-8", errors="replace"))
            except (OSError, zipfile.BadZipFile):
                continue

    # Repo code/config outside the content tree.
    for root, dirs, files in os.walk(REPO_ROOT):
        rel = os.path.relpath(root, REPO_ROOT)
        parts = rel.split(os.sep)
        if any(p in (".git", "bin", "obj", "TestResults") for p in parts):
            dirs[:] = []
            continue
        if parts[0] == "Images&Content":
            dirs[:] = []
            continue
        dirs[:] = [d for d in dirs if d not in (".git", "bin", "obj", "TestResults")]
        for name in files:
            ext = os.path.splitext(name)[1].lower()
            if ext in CODE_EXTENSIONS:
                path = os.path.join(root, name)
                try:
                    with open(path, "r", encoding="utf-8", errors="replace") as fh:
                        chunks.append(fh.read())
                except OSError:
                    continue

    return "\n".join(chunks).lower()


def is_location_folder_file(path: str) -> bool:
    """Files directly inside Demo-Content/<Location>/ or Production-Content/<Location>/
    are discovered by directory enumeration at runtime."""
    rel = os.path.relpath(path, CONTENT_DIR)
    parts = rel.split(os.sep)
    return len(parts) >= 3 and parts[0] in SKU_DIRS


def is_pin_part(path: str) -> bool:
    rel = os.path.relpath(path, CONTENT_DIR).replace("\\", "/").lower()
    return rel.startswith("assets/pins_v2/")


def extract_reference_tokens(reference_text: str) -> set[str]:
    """Pull every file-like token (with extension) from the reference text."""
    tokens: set[str] = set()
    for match in re.finditer(r"[\w\-.()\[\] ]+?\.(?:png|jpe?g|gif|webp|bmp|tiff?)", reference_text):
        token = match.group(0).strip().strip('"\'').lstrip("./\\").replace("\\", "/").lower()
        if token:
            tokens.add(token)
    return tokens


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--csv", metavar="PATH", help="Also write the unreferenced-candidate list to CSV.")
    args = parser.parse_args()

    if not os.path.isdir(CONTENT_DIR):
        print(f"Content directory not found: {CONTENT_DIR}", file=sys.stderr)
        return 2

    print("Scanning reference sources (json, xlsx, code)...")
    reference_text = collect_reference_text()
    tokens = extract_reference_tokens(reference_text)

    # First pass: inventory all audit-eligible files so we know basename uniqueness.
    eligible: list[tuple[str, str, int]] = []  # (rel_path_norm, full_path, size)
    for root, _, files in os.walk(CONTENT_DIR):
        for name in files:
            path = os.path.join(root, name)
            ext = os.path.splitext(name)[1].lower()
            if ext in SIDECAR_EXTENSIONS or name == ".gitkeep":
                continue
            if is_pin_part(path) or is_location_folder_file(path):
                continue
            rel = os.path.relpath(path, CONTENT_DIR).replace("\\", "/").lower()
            eligible.append((rel, path, os.path.getsize(path)))

    basename_counts: dict[str, int] = {}
    for rel, _, _ in eligible:
        base = os.path.basename(rel)
        basename_counts[base] = basename_counts.get(base, 0) + 1

    def is_referenced(rel: str) -> bool:
        base = os.path.basename(rel)
        for token in tokens:
            if "/" in token:
                # Path-like reference: exact match or suffix-of-rel match.
                if rel == token or rel.endswith("/" + token):
                    return True
            else:
                # Bare basename only counts when unique in the tree.
                if base == token and basename_counts[base] == 1:
                    return True
        return False

    unreferenced: list[tuple[str, int, str]] = []
    totals = {"candidates_bytes": 0, "candidates": 0, "referenced": 0, "implicit": 0, "sidecar": 0}

    for root, _, files in os.walk(CONTENT_DIR):
        for name in files:
            path = os.path.join(root, name)
            ext = os.path.splitext(name)[1].lower()
            if ext in SIDECAR_EXTENSIONS or name == ".gitkeep":
                totals["sidecar"] += 1
                continue
            if is_pin_part(path) or is_location_folder_file(path):
                totals["implicit"] += 1
                continue
            rel_norm = os.path.relpath(path, CONTENT_DIR).replace("\\", "/").lower()
            if is_referenced(rel_norm):
                totals["referenced"] += 1
                continue
            size = os.path.getsize(path)
            totals["candidates"] += 1
            totals["candidates_bytes"] += size
            unreferenced.append((rel_norm, size, os.path.dirname(rel_norm)))

    unreferenced.sort(key=lambda row: -row[1])

    print()
    print(f"Files referenced by name in json/xlsx/code: {totals['referenced']}")
    print(f"Files implicitly referenced (location folders, pin parts): {totals['implicit']}")
    print(f"Sidecar/non-image files skipped: {totals['sidecar']}")
    print(f"CANDIDATES never referenced: {totals['candidates']} files, "
          f"{totals['candidates_bytes'] / 1_048_576:.1f} MB")
    print()
    print("Candidates (confirm by hand before excluding from any bundle):")
    for rel, size, _ in unreferenced:
        print(f"  {size / 1_048_576:8.2f} MB  {rel}")

    if args.csv:
        os.makedirs(os.path.dirname(os.path.abspath(args.csv)), exist_ok=True)
        with open(args.csv, "w", newline="", encoding="utf-8") as fh:
            writer = csv.writer(fh)
            writer.writerow(["relative_path", "size_bytes", "size_mb"])
            for rel, size, _ in unreferenced:
                writer.writerow([rel, size, f"{size / 1_048_576:.3f}"])
        print(f"\nCSV written to {args.csv}")

    return 0


if __name__ == "__main__":
    sys.exit(main())
