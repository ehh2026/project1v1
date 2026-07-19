#!/usr/bin/env python3
"""
Advisory code-health report for line counts and approximate C# method complexity.

This script is intentionally non-blocking by default. Use --fail-on-findings when
you want to turn advisory findings into a local failure.
"""

from __future__ import annotations

import argparse
import os
import re
import sys
from dataclasses import dataclass
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parent.parent
EXCLUDED_PARTS = {".git", "bin", "obj", "backups", "cache", "imports", "scripts/venv", "TestResults"}
DEFAULT_TOP_FILES = 10
DEFAULT_MAX_METHOD_LINES = 80
DEFAULT_MAX_COMPLEXITY = 12
DEFAULT_WARN_FILE_LINES = 600
DEFAULT_HARD_FILE_LINES = 800

METHOD_PATTERN = re.compile(
    r"""
    ^\s*
    (?:(?:public|private|protected|internal|static|virtual|override|async|sealed|partial|extern|unsafe|new)\s+)*
    (?P<return_type>[\w<>\[\],\s?.]+)\s+
    (?P<name>[A-Za-z_]\w*)\s*
    \([^;{}]*\)\s*
    (?:where\s+[^{]+)?
    \{
    """,
    re.MULTILINE | re.VERBOSE,
)

COMPLEXITY_PATTERNS = (
    re.compile(r"\bif\s*\("),
    re.compile(r"\bfor\s*\("),
    re.compile(r"\bforeach\s*\("),
    re.compile(r"\bwhile\s*\("),
    re.compile(r"\bcase\b"),
    re.compile(r"\bcatch\s*(?:\(|\{)"),
    re.compile(r"\?\s*[^?:]+:"),
    re.compile(r"&&"),
    re.compile(r"\|\|"),
)
CONTROL_KEYWORDS = {
    "catch",
    "else",
    "for",
    "foreach",
    "if",
    "lock",
    "switch",
    "using",
    "while",
}


@dataclass(frozen=True)
class FileMetric:
    relative_path: str
    line_count: int


@dataclass(frozen=True)
class MethodMetric:
    relative_path: str
    method_name: str
    line_number: int
    line_count: int
    complexity: int


@dataclass(frozen=True)
class HealthReport:
    largest_files: list[FileMetric]
    large_files: list[FileMetric]
    long_or_complex_methods: list[MethodMetric]


def is_excluded(path: Path, root: Path) -> bool:
    rel_parts = path.relative_to(root).parts
    normalized_parts = {part.replace("\\", "/") for part in rel_parts}
    return bool(normalized_parts & EXCLUDED_PARTS)


def count_lines(text: str) -> int:
    if not text:
        return 0
    return text.count("\n") + (0 if text.endswith("\n") else 1)


def iter_csharp_files(root: Path) -> list[Path]:
    return sorted(path for path in root.rglob("*.cs") if not is_excluded(path, root))


def find_matching_brace(source: str, open_brace_index: int) -> int | None:
    depth = 0
    in_string = False
    escape = False
    quote = ""

    for index in range(open_brace_index, len(source)):
        char = source[index]
        if in_string:
            if escape:
                escape = False
            elif char == "\\":
                escape = True
            elif char == quote:
                in_string = False
            continue

        if char in ('"', "'"):
            quote = char
            in_string = True
            continue
        if char == "{":
            depth += 1
        elif char == "}":
            depth -= 1
            if depth == 0:
                return index

    return None


def estimate_complexity(method_source: str) -> int:
    complexity = 1
    for pattern in COMPLEXITY_PATTERNS:
        complexity += len(pattern.findall(method_source))
    return complexity


def analyze_csharp_methods(
    relative_path: str,
    source: str,
    max_method_lines: int = DEFAULT_MAX_METHOD_LINES,
    max_complexity: int = DEFAULT_MAX_COMPLEXITY,
) -> list[MethodMetric]:
    metrics: list[MethodMetric] = []
    for match in METHOD_PATTERN.finditer(source):
        if match.group("name") in CONTROL_KEYWORDS:
            continue

        open_brace = source.find("{", match.start())
        close_brace = find_matching_brace(source, open_brace)
        if close_brace is None:
            continue

        method_source = source[match.start() : close_brace + 1]
        line_count = count_lines(method_source)
        complexity = estimate_complexity(method_source)
        if line_count > max_method_lines or complexity > max_complexity:
            metrics.append(
                MethodMetric(
                    relative_path=relative_path,
                    method_name=match.group("name"),
                    line_number=source.count("\n", 0, match.start()) + 1,
                    line_count=line_count,
                    complexity=complexity,
                )
            )

    return metrics


def analyze_repository(
    root: Path = REPO_ROOT,
    top_files: int = DEFAULT_TOP_FILES,
    warn_file_lines: int = DEFAULT_WARN_FILE_LINES,
    max_method_lines: int = DEFAULT_MAX_METHOD_LINES,
    max_complexity: int = DEFAULT_MAX_COMPLEXITY,
) -> HealthReport:
    file_metrics: list[FileMetric] = []
    method_metrics: list[MethodMetric] = []

    for path in iter_csharp_files(root):
        rel = path.relative_to(root).as_posix()
        source = path.read_text(encoding="utf-8", errors="replace")
        line_count = count_lines(source)
        file_metrics.append(FileMetric(rel, line_count))
        method_metrics.extend(analyze_csharp_methods(rel, source, max_method_lines, max_complexity))

    largest_files = sorted(file_metrics, key=lambda item: item.line_count, reverse=True)[:top_files]
    large_files = [item for item in file_metrics if item.line_count > warn_file_lines]
    large_files.sort(key=lambda item: item.line_count, reverse=True)
    method_metrics.sort(key=lambda item: (item.complexity, item.line_count), reverse=True)
    return HealthReport(largest_files, large_files, method_metrics)


def format_report(report: HealthReport, hard_file_lines: int = DEFAULT_HARD_FILE_LINES) -> str:
    lines: list[str] = ["# Advisory Code Health", ""]
    lines.append("Approximate static heuristics only; use findings as refactoring prompts, not verdicts.")
    lines.append("")

    lines.append("## Largest C# Files")
    if report.largest_files:
        lines.append("| Lines | File |")
        lines.append("|------:|------|")
        for metric in report.largest_files:
            marker = " warning" if metric.line_count > hard_file_lines else ""
            lines.append(f"| {metric.line_count}{marker} | `{metric.relative_path}` |")
    else:
        lines.append("No C# files found.")
    lines.append("")

    lines.append("## Files Above Advisory Size")
    if report.large_files:
        lines.append("| Lines | File |")
        lines.append("|------:|------|")
        for metric in report.large_files:
            lines.append(f"| {metric.line_count} | `{metric.relative_path}` |")
    else:
        lines.append("No files above the advisory size threshold.")
    lines.append("")

    lines.append("## Long Or Complex Methods")
    if report.long_or_complex_methods:
        lines.append("| Complexity | Lines | Method |")
        lines.append("|-----------:|------:|--------|")
        for metric in report.long_or_complex_methods[:25]:
            lines.append(
                f"| {metric.complexity} | {metric.line_count} | "
                f"`{metric.relative_path}:{metric.line_number}` `{metric.method_name}` |"
            )
    else:
        lines.append("No methods above advisory line-count or complexity thresholds.")

    return "\n".join(lines)


def parse_args(argv: list[str]) -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Report advisory C# line-count and complexity findings.")
    parser.add_argument("--root", type=Path, default=REPO_ROOT, help="Repository root to scan.")
    parser.add_argument("--top-files", type=int, default=DEFAULT_TOP_FILES, help="Number of largest files to list.")
    parser.add_argument("--warn-file-lines", type=int, default=DEFAULT_WARN_FILE_LINES)
    parser.add_argument("--hard-file-lines", type=int, default=DEFAULT_HARD_FILE_LINES)
    parser.add_argument("--max-method-lines", type=int, default=DEFAULT_MAX_METHOD_LINES)
    parser.add_argument("--max-complexity", type=int, default=DEFAULT_MAX_COMPLEXITY)
    parser.add_argument("--fail-on-findings", action="store_true", help="Exit 1 if advisory findings are present.")
    return parser.parse_args(argv)


def main(argv: list[str] | None = None) -> int:
    args = parse_args(argv or sys.argv[1:])
    report = analyze_repository(
        args.root.resolve(),
        top_files=args.top_files,
        warn_file_lines=args.warn_file_lines,
        max_method_lines=args.max_method_lines,
        max_complexity=args.max_complexity,
    )
    output = format_report(report, hard_file_lines=args.hard_file_lines)
    print(output)

    summary_path = os.environ.get("GITHUB_STEP_SUMMARY")
    if summary_path:
        with open(summary_path, "a", encoding="utf-8") as summary:
            summary.write(output)
            summary.write("\n")

    has_findings = bool(report.large_files or report.long_or_complex_methods)
    return 1 if args.fail_on_findings and has_findings else 0


if __name__ == "__main__":
    sys.exit(main())
