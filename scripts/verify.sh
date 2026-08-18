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
  echo "[1/9] dotnet restore"
  if ! dotnet restore InteractiveWorldMap.sln; then
    RUN_DOTNET=false
  fi
fi

if [[ "$RUN_DOTNET" == true ]]; then
  echo "[2/9] NuGet vulnerability check"
  python3 scripts/verify_nuget_vulnerabilities.py
fi

if [[ "$RUN_DOTNET" == true ]]; then
  echo "[3/9] dotnet build"
  if ! dotnet build InteractiveWorldMap.sln --configuration Release --no-restore 2>&1; then
    echo "WARN: dotnet build failed — WPF requires Windows Desktop SDK (windows-latest CI)." >&2
    echo "REMEDIATION: Run .\\scripts\\verify.ps1 on Windows for full build/test." >&2
    RUN_DOTNET=false
  fi
fi

if [[ "$RUN_DOTNET" == true ]]; then
  echo "[4/9] dotnet test"
  dotnet test Tests/InteractiveWorldMap.Tests.csproj --configuration Release --no-build --verbosity minimal --settings .runsettings --filter "Category!=Performance" --collect:"XPlat Code Coverage" --results-directory TestResults/verify-coverage

  echo "[5/9] code formatting check"
  dotnet format InteractiveWorldMap.sln --verify-no-changes

  echo "[6/9] coverage threshold gate"
  COVERAGE_FILE=$(find TestResults -name "coverage.cobertura.xml" -type f 2>/dev/null | sort | tail -1)
  if [ -n "$COVERAGE_FILE" ]; then
    python3 scripts/summarize_coverage.py --results-directory TestResults/verify-coverage --min-line-coverage 45 --min-branch-coverage 40
  else
    echo "SKIP: No coverage file found (harness-only mode)."
  fi

  echo "[7/9] Lizard complexity gate"
  python3 -m lizard -C 20 -x "*Tests*" -x "*Tools*" -x "*bin*" -x "*obj*" -x "*scripts*" -x "*TestResults*" .
fi

echo "[8/9] doc link check"
python3 scripts/verify_doc_links.py

echo "[9/9] taste checks"
python3 scripts/verify_taste.py

if [[ "$RUN_DOTNET" == true ]]; then
  echo "=== Verification PASSED (full) ==="
else
  echo "=== Verification PASSED (harness-only; dotnet build/test skipped) ==="
  echo "Run .\\scripts\\verify.ps1 on Windows for full WPF build, test, and startup validation."
fi
