#!/usr/bin/env python3
"""
doc_gardening.py — Harness entropy check for documentation drift.

Inputs: repository root (auto-detected)
Outputs: exit 0 when clean, exit 1 with remediation messages when issues found
Requirements: Python 3.8+ (stdlib only)
"""

from __future__ import annotations

import subprocess
import sys
from datetime import datetime, timedelta
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parent.parent
MAX_AGENTS_LINES = 150
MAX_ACTIVE_PLAN_DAYS = 30


def run_doc_link_check(errors: list[str]) -> None:
    script = REPO_ROOT / "scripts" / "verify_doc_links.py"
    result = subprocess.run(
        [sys.executable, str(script)],
        cwd=REPO_ROOT,
        capture_output=True,
        text=True,
    )
    if result.returncode != 0:
        errors.append(
            "Broken documentation links detected. "
            "REMEDIATION: Run python scripts/verify_doc_links.py and fix paths."
        )


def check_agents_size(errors: list[str]) -> None:
    agents = REPO_ROOT / "AGENTS.md"
    if not agents.exists():
        errors.append("AGENTS.md missing. REMEDIATION: Create agent entry map.")
        return
    lines = agents.read_text(encoding="utf-8").count("\n") + 1
    if lines > MAX_AGENTS_LINES:
        errors.append(
            f"AGENTS.md has {lines} lines (max {MAX_AGENTS_LINES}). "
            "REMEDIATION: Move detail to docs/; keep AGENTS.md as table of contents."
        )


def check_stale_active_plans(errors: list[str]) -> None:
    active_dir = REPO_ROOT / "docs" / "exec-plans" / "active"
    if not active_dir.exists():
        return
    cutoff = datetime.now() - timedelta(days=MAX_ACTIVE_PLAN_DAYS)
    for plan in active_dir.glob("*.md"):
        mtime = datetime.fromtimestamp(plan.stat().st_mtime)
        if mtime < cutoff and "status: completed" not in plan.read_text(encoding="utf-8"):
            rel = plan.relative_to(REPO_ROOT)
            errors.append(
                f"{rel}: active plan older than {MAX_ACTIVE_PLAN_DAYS} days. "
                "REMEDIATION: Complete or move to docs/exec-plans/completed/."
            )


def main() -> int:
    errors: list[str] = []
    run_doc_link_check(errors)
    check_agents_size(errors)
    check_stale_active_plans(errors)

    if errors:
        print("Doc gardening FAILED:", file=sys.stderr)
        for err in errors:
            print(f"  - {err}", file=sys.stderr)
        return 1

    print("Doc gardening passed.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
