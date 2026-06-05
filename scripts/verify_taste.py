#!/usr/bin/env python3
"""
verify_taste.py — Mechanical taste invariant checks for the agent harness.

Inputs: repository root (auto-detected from script location)
Outputs: exit 0 on pass, exit 1 with remediation messages on failure
Requirements: Python 3.8+ (stdlib only)
"""

from __future__ import annotations

import re
import sys
from datetime import datetime, timedelta
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parent.parent
MAX_CS_LINES = 800
MAX_ACTIVE_PLAN_DAYS = 30

EXCLUDE_DIRS = {"bin", "obj", "scripts/venv", ".git", "Tests"}

# Pre-harness debt — remove entries as files are cleaned up (see docs/exec-plans/tech-debt-tracker.md)
FILE_SIZE_GRANDFATHER = {
    "MainWindow.xaml.cs",  # TD-001: extract services from god object
}
CONSOLE_GRANDFATHER = {
    "Services/FileLogger.cs",  # intentional console mirror for dev
    "Views/LocationMarker.xaml.cs",
    "Views/ImagePinMarker.xaml.cs",
    "Views/ClusterMarker.xaml.cs",
}


def find_cs_files() -> list[Path]:
    files = []
    for path in REPO_ROOT.rglob("*.cs"):
        parts = set(path.relative_to(REPO_ROOT).parts)
        if parts & {"bin", "obj", "Tests"}:
            continue
        files.append(path)
    return files


def check_file_sizes(errors: list[str]) -> None:
    for path in find_cs_files():
        rel = path.relative_to(REPO_ROOT)
        if rel.as_posix() in FILE_SIZE_GRANDFATHER or path.name in FILE_SIZE_GRANDFATHER:
            continue
        lines = path.read_text(encoding="utf-8", errors="replace").count("\n") + 1
        if lines > MAX_CS_LINES:
            errors.append(
                f"{rel}: {lines} lines exceeds {MAX_CS_LINES}. "
                "REMEDIATION: Split into focused partial classes or extract Services."
            )


def check_console_writeline(errors: list[str]) -> None:
    pattern = re.compile(r"Console\.WriteLine\s*\(")
    for folder in ("Services", "Views"):
        dir_path = REPO_ROOT / folder
        if not dir_path.exists():
            continue
        for path in dir_path.rglob("*.cs"):
            rel = path.relative_to(REPO_ROOT)
            if rel.as_posix() in CONSOLE_GRANDFATHER:
                continue
            content = path.read_text(encoding="utf-8", errors="replace")
            if pattern.search(content):
                errors.append(
                    f"{rel}: Console.WriteLine found in {folder}/. "
                    "REMEDIATION: Use ILogger / FileLogger instead."
                )


def check_visual_config_model(errors: list[str]) -> None:
    config_path = REPO_ROOT / "visual-config.json"
    model_path = REPO_ROOT / "Models" / "VisualConfig.cs"
    if config_path.exists() and not model_path.exists():
        errors.append(
            "visual-config.json exists but Models/VisualConfig.cs is missing. "
            "REMEDIATION: Add typed VisualConfig model with Load() method."
        )


def check_stale_active_plans(errors: list[str]) -> None:
    active_dir = REPO_ROOT / "docs" / "exec-plans" / "active"
    if not active_dir.exists():
        return
    cutoff = datetime.now() - timedelta(days=MAX_ACTIVE_PLAN_DAYS)
    for plan in active_dir.glob("*.md"):
        mtime = datetime.fromtimestamp(plan.stat().st_mtime)
        if mtime < cutoff:
            content = plan.read_text(encoding="utf-8", errors="replace")
            if "status: completed" in content:
                continue
            rel = plan.relative_to(REPO_ROOT)
            errors.append(
                f"{rel}: active plan older than {MAX_ACTIVE_PLAN_DAYS} days. "
                "REMEDIATION: Complete or move to docs/exec-plans/completed/."
            )


def check_agents_md_size(errors: list[str]) -> None:
    agents = REPO_ROOT / "AGENTS.md"
    if not agents.exists():
        errors.append("AGENTS.md missing. REMEDIATION: Create agent entry map.")
        return
    lines = agents.read_text(encoding="utf-8").count("\n") + 1
    if lines > 150:
        errors.append(
            f"AGENTS.md has {lines} lines (max 150). "
            "REMEDIATION: Move detail to docs/; keep AGENTS.md as table of contents only."
        )


def main() -> int:
    errors: list[str] = []
    check_file_sizes(errors)
    check_console_writeline(errors)
    check_visual_config_model(errors)
    check_stale_active_plans(errors)
    check_agents_md_size(errors)

    if errors:
        print("Taste check FAILED:", file=sys.stderr)
        for err in errors:
            print(f"  - {err}", file=sys.stderr)
        return 1

    print("Taste check passed.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
