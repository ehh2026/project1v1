#!/usr/bin/env python3
"""
verify_doc_links.py — Validate relative markdown links in docs/ and AGENTS.md.

Inputs: repository root
Outputs: exit 0 on pass, exit 1 with broken link report
Requirements: Python 3.8+ (stdlib only)
"""

from __future__ import annotations

import re
import sys
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parent.parent
LINK_PATTERN = re.compile(r"\[([^\]]+)\]\(([^)]+)\)")
SKIP_PREFIXES = ("http://", "https://", "mailto:", "#")


def collect_markdown_files() -> list[Path]:
    files = [REPO_ROOT / "AGENTS.md", REPO_ROOT / "ARCHITECTURE.md", REPO_ROOT / "README.md"]
    docs = REPO_ROOT / "docs"
    if docs.exists():
        files.extend(docs.rglob("*.md"))
    return [f for f in files if f.exists()]


def resolve_link(source: Path, target: str) -> Path:
    target = target.split("#")[0].strip()
    if not target:
        return source
    if source.parent == REPO_ROOT:
        return (REPO_ROOT / target).resolve()
    return (source.parent / target).resolve()


def main() -> int:
    broken: list[str] = []

    for md_file in collect_markdown_files():
        content = md_file.read_text(encoding="utf-8", errors="replace")
        for _text, link in LINK_PATTERN.findall(content):
            link = link.strip()
            if any(link.startswith(p) for p in SKIP_PREFIXES):
                continue
            resolved = resolve_link(md_file, link)
            if not resolved.exists():
                rel_src = md_file.relative_to(REPO_ROOT)
                broken.append(f"{rel_src} -> {link} (resolved: {resolved})")

    if broken:
        print("Broken doc links:", file=sys.stderr)
        for item in broken:
            print(f"  - {item}", file=sys.stderr)
        print("REMEDIATION: Fix relative paths or create missing target files.", file=sys.stderr)
        return 1

    print(f"Doc link check passed ({len(collect_markdown_files())} files).")
    return 0


if __name__ == "__main__":
    sys.exit(main())
