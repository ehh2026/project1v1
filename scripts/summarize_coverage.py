#!/usr/bin/env python3
"""Summarize Cobertura coverage files emitted by dotnet test / coverlet.collector."""

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


def rate_to_percentage(value: str | None) -> float:
    try:
        return float(value or 0) * 100.0
    except ValueError:
        return 0.0


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
    parser.add_argument("--results-directory", type=Path, default=None)
    parser.add_argument("--min-line-coverage", type=float, default=None)
    parser.add_argument("--min-branch-coverage", type=float, default=None)
    args = parser.parse_args(argv)
    if args.results_directory is not None and args.path != REPO_ROOT / "TestResults":
        parser.error("pass either positional path or --results-directory, not both")
    if args.results_directory is not None:
        args.path = args.results_directory
    return args


def check_thresholds(args: argparse.Namespace, paths: list[Path]) -> int:
    if args.min_line_coverage is None and args.min_branch_coverage is None:
        return 0

    if not paths:
        print("Coverage threshold check failed: no Cobertura coverage files found.", file=sys.stderr)
        return 1

    newest = max(paths, key=lambda path: path.stat().st_mtime)
    root = ET.parse(newest).getroot()
    line_coverage = rate_to_percentage(root.attrib.get("line-rate"))
    branch_coverage = rate_to_percentage(root.attrib.get("branch-rate"))

    failed = False
    if args.min_line_coverage is not None and line_coverage < args.min_line_coverage:
        print(
            f"Line coverage {line_coverage:.1f}% is below threshold {args.min_line_coverage:.1f}%.",
            file=sys.stderr)
        failed = True
    if args.min_branch_coverage is not None and branch_coverage < args.min_branch_coverage:
        print(
            f"Branch coverage {branch_coverage:.1f}% is below threshold {args.min_branch_coverage:.1f}%.",
            file=sys.stderr)
        failed = True

    return 1 if failed else 0


def main(argv: list[str] | None = None) -> int:
    args = parse_args(argv or sys.argv[1:])
    paths = find_coverage_files(args.path)
    summary = build_summary(paths)
    print(summary)

    summary_path = os.environ.get("GITHUB_STEP_SUMMARY")
    if summary_path:
        with open(summary_path, "a", encoding="utf-8") as handle:
            handle.write(summary)
            handle.write("\n")

    return check_thresholds(args, paths)


if __name__ == "__main__":
    sys.exit(main())
