#!/usr/bin/env python3
"""
doc_gardening.py — Harness entropy check for documentation drift.

Inputs: repository root (auto-detected)
Outputs: exit 0 when clean, exit 1 with remediation messages when issues found
Requirements: Python 3.8+ (stdlib only)
"""

from __future__ import annotations

import re
import subprocess
import sys
from datetime import datetime, timedelta
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parent.parent
MAX_AGENTS_LINES = 150
MAX_TODO_LINES = 120
MAX_ACTIVE_PLAN_DAYS = 30
ACTIVE_README = REPO_ROOT / "docs" / "exec-plans" / "active" / "README.md"
README_LINK_PATTERN = re.compile(r"\[[^\]]+\]\(([^)]+\.md)\)")


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
            "REMEDIATION: Run py -3 scripts/verify_doc_links.py on Windows or python3 scripts/verify_doc_links.py on macOS/Linux and fix paths."
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


def check_todo_size(errors: list[str]) -> None:
    todo = REPO_ROOT / "docs" / "TO_DO.md"
    if not todo.exists():
        return
    lines = todo.read_text(encoding="utf-8").count("\n") + 1
    if lines > MAX_TODO_LINES:
        errors.append(
            f"docs/TO_DO.md has {lines} lines (max {MAX_TODO_LINES}). "
            "REMEDIATION: Keep TO_DO.md as a short backlog; move detail to exec-plans/active/."
        )


def check_stale_active_plans(warnings: list[str]) -> None:
    """Warn on incomplete active plans older than MAX_ACTIVE_PLAN_DAYS.

    Lingering incomplete work is advisory only — do not fail gardening/CI.
    Agents must surface these warnings to the user.
    """
    active_dir = REPO_ROOT / "docs" / "exec-plans" / "active"
    if not active_dir.exists():
        return
    cutoff = datetime.now() - timedelta(days=MAX_ACTIVE_PLAN_DAYS)
    for plan in active_dir.glob("*.md"):
        if plan.name == "README.md":
            continue
        mtime = datetime.fromtimestamp(plan.stat().st_mtime)
        content = plan.read_text(encoding="utf-8", errors="replace")
        if mtime < cutoff and "status: completed" not in content:
            rel = plan.relative_to(REPO_ROOT)
            warnings.append(
                f"{rel}: incomplete active plan older than {MAX_ACTIVE_PLAN_DAYS} days "
                "(lingering; non-blocking). "
                "AGENTS: Report this warning to the user and ask whether to continue the plan, "
                "park it under docs/exec-plans/inactive/, or archive to docs/exec-plans/completed/. "
                "REMEDIATION (optional): refresh the plan, finish remaining work, or move it out of active/."
            )


def _plans_linked_in_active_readme() -> set[str]:
    if not ACTIVE_README.exists():
        return set()
    names: set[str] = set()
    for match in README_LINK_PATTERN.findall(ACTIVE_README.read_text(encoding="utf-8")):
        link = match.split("#")[0].strip()
        if link and not link.startswith(("http://", "https://", "../")):
            names.add(Path(link).name)
    return names


def check_active_plans_listed(errors: list[str]) -> None:
    active_dir = REPO_ROOT / "docs" / "exec-plans" / "active"
    if not active_dir.exists():
        return
    listed = _plans_linked_in_active_readme()
    for plan in sorted(active_dir.glob("*.md")):
        if plan.name == "README.md":
            continue
        if plan.name not in listed:
            rel = plan.relative_to(REPO_ROOT)
            errors.append(
                f"{rel} is not linked from docs/exec-plans/active/README.md. "
                "REMEDIATION: Add a row to the Active plans table."
            )


def check_active_plan_front_matter(errors: list[str]) -> None:
    active_dir = REPO_ROOT / "docs" / "exec-plans" / "active"
    if not active_dir.exists():
        return
    for plan in active_dir.glob("*.md"):
        if plan.name == "README.md":
            continue
        text = plan.read_text(encoding="utf-8")
        if not text.startswith("---\n") or "status: active" not in text.split("---", 2)[1]:
            rel = plan.relative_to(REPO_ROOT)
            errors.append(
                f"{rel} missing YAML front-matter with status: active. "
                "REMEDIATION: Add front-matter per docs/exec-plans/active/README.md."
            )


def check_duplicate_active_completed_plans(errors: list[str]) -> None:
    active_dir = REPO_ROOT / "docs" / "exec-plans" / "active"
    completed_dir = REPO_ROOT / "docs" / "exec-plans" / "completed"
    if not active_dir.exists() or not completed_dir.exists():
        return
    active_names = {p.name for p in active_dir.glob("*.md") if p.name != "README.md"}
    for completed in completed_dir.glob("*.md"):
        if completed.name in active_names:
            errors.append(
                f"{completed.name} exists in both active/ and completed/. "
                "REMEDIATION: Remove the active copy or rename the completed archive."
            )


def main() -> int:
    errors: list[str] = []
    warnings: list[str] = []
    run_doc_link_check(errors)
    check_agents_size(errors)
    check_todo_size(errors)
    check_stale_active_plans(warnings)
    check_active_plans_listed(errors)
    check_active_plan_front_matter(errors)
    check_duplicate_active_completed_plans(errors)

    if warnings:
        print("Doc gardening WARNINGS (non-blocking):", file=sys.stderr)
        for warn in warnings:
            print(f"  - {warn}", file=sys.stderr)

    if errors:
        print("Doc gardening FAILED:", file=sys.stderr)
        for err in errors:
            print(f"  - {err}", file=sys.stderr)
        return 1

    if warnings:
        print("Doc gardening passed with warnings.")
    else:
        print("Doc gardening passed.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
