#!/usr/bin/env python3
"""
verify_nuget_vulnerabilities.py — Fail the harness on High/Critical NuGet advisories.

Inputs: repository root (InteractiveWorldMap.sln restored)
Outputs: exit 0 when clean, exit 1 with remediation hints
Requirements: .NET SDK, Python 3.8+ (stdlib only)

Note: `dotnet list package --vulnerable` always exits 0 even when findings exist;
this script parses CLI output and enforces policy.
"""

from __future__ import annotations

import re
import subprocess
import sys
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parent.parent
SOLUTION = "InteractiveWorldMap.sln"
FAIL_SEVERITIES = frozenset({"high", "critical"})
VULN_BLOCK = re.compile(r"has the following vulnerable packages", re.I)
ROW_PATTERN = re.compile(
    r"^\s*>\s+(?P<package>\S+)\s+(?P<version>\S+)\s+(?P<severity>\w+)\s+",
    re.I,
)


def run_vulnerable_list() -> str:
    result = subprocess.run(
        [
            "dotnet",
            "list",
            SOLUTION,
            "package",
            "--vulnerable",
            "--include-transitive",
        ],
        cwd=REPO_ROOT,
        capture_output=True,
        text=True,
        check=False,
    )
    combined = (result.stdout or "") + (result.stderr or "")
    if result.returncode not in (0,):
        print(combined, file=sys.stderr)
        print(
            "REMEDIATION: Ensure `dotnet restore` succeeded and the .NET SDK is installed.",
            file=sys.stderr,
        )
        sys.exit(result.returncode if result.returncode != 0 else 1)
    return combined


def parse_findings(output: str) -> list[tuple[str, str, str, str]]:
    """Return (project, package, version, severity) tuples above policy threshold."""
    findings: list[tuple[str, str, str, str]] = []
    current_project = "unknown"
    in_block = False

    for line in output.splitlines():
        project_match = re.search(r"Project `([^`]+)`", line)
        if project_match:
            current_project = project_match.group(1)
            in_block = False
            continue

        if VULN_BLOCK.search(line):
            in_block = True
            continue

        if not in_block:
            continue

        row = ROW_PATTERN.match(line)
        if not row:
            continue

        severity = row.group("severity").lower()
        if severity in FAIL_SEVERITIES:
            findings.append(
                (
                    current_project,
                    row.group("package"),
                    row.group("version"),
                    row.group("severity"),
                )
            )

    return findings


def main() -> int:
    output = run_vulnerable_list()
    findings = parse_findings(output)

    if not findings:
        print(
            "NuGet vulnerability check passed "
            f"(policy: fail on {', '.join(sorted(FAIL_SEVERITIES))}; "
            "direct + transitive)."
        )
        return 0

    print("NuGet vulnerabilities at or above policy threshold:", file=sys.stderr)
    for project, package, version, severity in findings:
        print(
            f"  - {project}: {package} {version} ({severity})",
            file=sys.stderr,
        )
    print(
        "REMEDIATION: Upgrade or pin affected packages; run "
        "`dotnet list InteractiveWorldMap.sln package --vulnerable --include-transitive`.",
        file=sys.stderr,
    )
    return 1


if __name__ == "__main__":
    sys.exit(main())
