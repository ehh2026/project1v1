#!/usr/bin/env python3
"""Validate the checked-in portable Windows release package contract."""

from __future__ import annotations

import argparse
import sys
import zipfile
from pathlib import Path, PurePosixPath


EXPECTED_FILES = {
    "interactiveworldmap.exe",
    "visual-config.default.json",
    "tools/configure-interactiveworldmap.ps1",
    "tools/configure-interactiveworldmap.bat",
    "readme.md",
}
EXPECTED_DIRECTORIES = {"images&content/assets", "images&content/demo-content"}
FORBIDDEN_DIRECTORIES = {
    ".git",
    "bin",
    "obj",
    "testresults",
    "tests",
    "scripts",
    "docs",
    "models",
    "views",
    "services",
    "utilities",
    ".github",
    "artifacts",
}
FORBIDDEN_PATHS = {
    "images&content/production-content",
    "images&content/extras",
    "images&content/visual-config.json",
    "visual-config.json",
}
FORBIDDEN_SUFFIXES = {".pdb", ".xml"}


def _validate_version(version: str) -> list[str]:
    if not version or not version[0].isdigit():
        return ["version must start with a digit"]
    if any(character not in "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789.-" for character in version):
        return ["version contains characters outside letters, digits, dot, and hyphen"]
    return []


def _validate_members(members: set[str], root_name: str) -> list[str]:
    errors: list[str] = []
    normalized = {member.lower().replace("\\", "/").rstrip("/") for member in members if member}

    for expected in EXPECTED_FILES:
        if expected not in normalized:
            errors.append(f"missing required file: {expected}")

    for expected in EXPECTED_DIRECTORIES:
        if not any(member == expected or member.startswith(expected + "/") for member in normalized):
            errors.append(f"missing required directory or content: {expected}")

    for member in normalized:
        parts = member.split("/")
        if parts[0] in FORBIDDEN_DIRECTORIES:
            errors.append(f"forbidden repository/build directory: {member}")
        if member in FORBIDDEN_PATHS or any(member.startswith(path + "/") for path in FORBIDDEN_PATHS):
            errors.append(f"forbidden package content: {member}")
        if Path(member).suffix in FORBIDDEN_SUFFIXES:
            errors.append(f"forbidden release symbol/document: {member}")

    expected_prefix = "interactiveworldmap-win-x64-"
    if not root_name.lower().startswith(expected_prefix):
        errors.append("package root must start with InteractiveWorldMap-win-x64-")
    else:
        errors.extend(_validate_version(root_name[len(expected_prefix) :]))
    return errors


def validate_directory(package_root: Path) -> list[str]:
    if not package_root.is_dir():
        return [f"package directory does not exist: {package_root}"]
    members = {
        path.relative_to(package_root).as_posix()
        for path in package_root.rglob("*")
    }
    return _validate_members(members, package_root.name)


def validate_zip(archive: Path) -> list[str]:
    if not archive.is_file():
        return [f"zip archive does not exist: {archive}"]

    errors: list[str] = []
    with zipfile.ZipFile(archive) as release_zip:
        # Zip members may use either separator; judge them in one normalized form so a
        # Windows-style "sub\..\..\outside" cannot hide its traversal from PurePosixPath.
        names = [name.replace("\\", "/") for name in release_zip.namelist()]
        for name in names:
            path = PurePosixPath(name)
            drive_qualified = bool(path.parts) and ":" in path.parts[0]
            if path.is_absolute() or drive_qualified or ".." in path.parts:
                errors.append(f"unsafe archive member: {name}")

        roots = {PurePosixPath(name).parts[0] for name in names if PurePosixPath(name).parts}
        if len(roots) != 1:
            errors.append("zip must contain exactly one root directory")
            return errors
        root_name = next(iter(roots))
        members = {
            "/".join(PurePosixPath(name).parts[1:])
            for name in names
            if len(PurePosixPath(name).parts) > 1
        }
        errors.extend(_validate_members(members, root_name))
    return errors


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    source = parser.add_mutually_exclusive_group(required=True)
    source.add_argument("--package-root", type=Path, help="Portable package directory to validate.")
    source.add_argument("--zip", type=Path, dest="archive", help="Portable release zip to validate.")
    args = parser.parse_args()

    errors = validate_directory(args.package_root) if args.package_root else validate_zip(args.archive)
    if errors:
        for error in errors:
            print(f"ERROR: {error}", file=sys.stderr)
        return 1
    print("Portable release package validation passed.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
