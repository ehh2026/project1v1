#!/usr/bin/env python3
"""
Summarize Cobertura coverage files emitted by dotnet test / coverlet.collector.

The summary is advisory and exits 0 when no coverage file is found so it can be
used in non-blocking CI jobs.
"""

from __future__ import annotations

import argparse
import os
import sys
import xml.etree.ElementTree as ET
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parent.parent


def find_coverage_files(root: Path) -> list[Path]:
    if root.is_file():
        return [root]
    return sorted(root.rglob("coverage.cobertura.xml")) if root.exists() else []


def percentage(value: str | None) -> str:
    try:
        return f"{float(value or 0) * 100:.1f}%"
    except ValueError:
        return "n/a"


def summarize_file(path: Path) -> str:
    root = ET.parse(path).getroot()
    line_rate = percentage(root.attrib.get("line-rate"))
    branch_rate = percentage(root.attrib.get("branch-rate"))
    lines_valid = root.attrib.get("lines-valid", "n/a")
    lines_covered = root.attrib.get("lines-covered", "n/a")
    return f"- `{path}`: line {line_rate} ({lines_covered}/{lines_valid}), branch {branch_rate}"


def build_summary(paths: list[Path]) -> str:
    lines = ["# Advisory Test Coverage", ""]
    if not paths:
        lines.append("No Cobertura coverage files found.")
    else:
        lines.extend(summarize_file(path) for path in paths)
    return "\n".join(lines)


def parse_args(argv: list[str]) -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Summarize Cobertura coverage output.")
    parser.add_argument("path", nargs="?", type=Path, default=REPO_ROOT / "TestResults")
    return parser.parse_args(argv)


def main(argv: list[str] | None = None) -> int:
    args = parse_args(argv or sys.argv[1:])
    summary = build_summary(find_coverage_files(args.path))
    print(summary)

    summary_path = os.environ.get("GITHUB_STEP_SUMMARY")
    if summary_path:
        with open(summary_path, "a", encoding="utf-8") as handle:
            handle.write(summary)
            handle.write("\n")

    return 0


if __name__ == "__main__":
    sys.exit(main())
