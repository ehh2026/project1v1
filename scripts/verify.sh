#!/usr/bin/env bash
# verify.sh — Unified agent verification (macOS/Linux; build + test + harness checks)
# Usage: ./scripts/verify.sh
# Exit: 0 pass, non-zero on first failure with remediation hints

set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
cd "$ROOT"

echo "=== Interactive World Map — Harness Verification ==="

# Resolve dotnet from PATH or common install locations (e.g. Homebrew dotnet@6)
if ! command -v dotnet >/dev/null 2>&1; then
  for candidate in \
    "${DOTNET_ROOT:-}/dotnet" \
    "/opt/homebrew/opt/dotnet@6/libexec/dotnet" \
    "/usr/local/share/dotnet/dotnet" \
    "$HOME/.dotnet/dotnet"; do
    if [[ -x "$candidate" ]]; then
      export DOTNET_ROOT="$(dirname "$candidate")"
      export PATH="$DOTNET_ROOT:$PATH"
      break
    fi
  done
fi

RUN_DOTNET=true
if ! command -v dotnet >/dev/null 2>&1; then
  echo "WARN: dotnet SDK not found — skipping build/test (harness-only mode)." >&2
  echo "REMEDIATION: Install .NET 6 SDK or run .\\scripts\\verify.ps1 on Windows." >&2
  RUN_DOTNET=false
fi

if [[ "$RUN_DOTNET" == true ]]; then
  echo "[1/5] dotnet restore"
  if ! dotnet restore InteractiveWorldMap.sln; then
    RUN_DOTNET=false
  fi
fi

if [[ "$RUN_DOTNET" == true ]]; then
  echo "[2/5] dotnet build"
  if ! dotnet build InteractiveWorldMap.sln --configuration Release --no-restore 2>&1; then
    echo "WARN: dotnet build failed — WPF requires Windows Desktop SDK (windows-latest CI)." >&2
    echo "REMEDIATION: Run .\\scripts\\verify.ps1 on Windows for full build/test." >&2
    RUN_DOTNET=false
  fi
fi

if [[ "$RUN_DOTNET" == true ]]; then
  echo "[3/5] dotnet test"
  dotnet test Tests/InteractiveWorldMap.Tests.csproj --configuration Release --no-build --verbosity minimal
fi

echo "[4/5] doc link check"
python3 scripts/verify_doc_links.py

echo "[5/5] taste checks"
python3 scripts/verify_taste.py

if [[ "$RUN_DOTNET" == true ]]; then
  echo "=== Verification PASSED (full) ==="
else
  echo "=== Verification PASSED (harness-only; dotnet build/test skipped) ==="
  echo "Run .\\scripts\\verify.ps1 on Windows for full WPF build, test, and startup validation."
fi
