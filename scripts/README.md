# Scripts Index

Python and PowerShell tooling for verification, CI harness checks, and optional composite-pin asset processing.

## Python environment

| Path | Purpose |
|------|---------|
| `scripts/requirements.txt` | Composite-pin asset tooling deps: Pillow, numpy, scipy |
| `scripts/venv/` | Local venv (gitignored — create per machine) |

**Harness scripts** (`verify_*.py`, `doc_gardening.py`) use the **stdlib only**. On Windows, run them with `py -3`; `verify.ps1` also prefers `py -3` before bare `python` so pyenv shims without a selected version do not break the harness. On macOS/Linux, use `python3`. No venv is required for harness scripts.

**Composite-pin asset tooling** (`split_pin_parts.py`, `create_shaft_asset_variants.py`, `create_head_asset_variants.py`) requires the venv:

```powershell
py -3 -m venv scripts\venv
.\scripts\venv\Scripts\Activate.ps1
pip install -r scripts\requirements.txt
```

Full setup: [docs/guides/SETUP_GUIDE.md](../docs/guides/SETUP_GUIDE.md#python-harness-and-optional-tooling).

## Script catalog

| Script | Used by | Deps | Description |
|--------|---------|------|-------------|
| `verify.ps1` | Agents, CI (Windows) | — | Full build, test, harness, startup validation |
| `verify.sh` | Agents, CI (macOS/Linux) | — | Build, test, harness (no WPF startup on non-Windows) |
| `validate_startup.ps1` | `verify.ps1` | — | Headless WPF startup check (Windows) |
| `verify_nuget_vulnerabilities.py` | `verify.ps1`, `verify.sh`, CI | stdlib | Fail on High/Critical NuGet advisories |
| `verify_doc_links.py` | `verify.ps1`, `verify.sh`, doc-gardening CI | stdlib | Markdown link integrity |
| `verify_taste.py` | `verify.ps1`, `verify.sh`, pre-push | stdlib | Architecture taste invariants (Views, JObject, etc.). Incomplete active plans older than 30 days warn only (non-blocking); agents must report those warnings to the user. |
| `advisory_code_health.py` | Pre-push, advisory CI | stdlib | Non-blocking largest-file, advisory size, and approximate method complexity report |
| `summarize_coverage.py` | Advisory CI | stdlib | Summarize Cobertura coverage emitted by `dotnet test --collect:"XPlat Code Coverage"` |
| `install_git_hooks.ps1` | Manual | Git | Configure local `core.hooksPath` to use `.githooks/pre-push` |
| `toggle-dev-tools.ps1` | Manual (via root `toggle-dev-tools.bat`) | — | Turn `EnableDeveloperTools` on/off in every runtime `visual-config.json` next to a built/published exe (`-State on\|off\|toggle`; `-PublishDir <path>` for an external publish) |
| `advisory_code_health_tests.py` | Manual | stdlib | Unit checks for the advisory code-health parser |
| `doc_gardening.py` | Weekly CI | stdlib | Doc drift: links, AGENTS/TO_DO size, active plan registry, front-matter. Incomplete active plans older than 30 days warn only (same policy as `verify_taste.py`). |
| `split_pin_parts.py` | Manual | venv | Split extracted pins into parts |
| `create_shaft_asset_variants.py` | Manual | venv | Generate shaft contrast variants: outer (`outline_dark_7px`), inner (`inner_dark_3px`), or combo (`outline_dark_6px_in2px`); writes preview grids |
| `create_head_asset_variants.py` | Manual | venv | Generate black-outline head variants (`outline_black_2px` through `outline_black_14px`); writes per-variant `preview_heads.png` grids |

## Advisory hooks and health checks

Install the repo-local pre-commit and pre-push hooks:

```powershell
.\scripts\install_git_hooks.ps1
```

The pre-commit hook verifies formatting with `dotnet format --verify-no-changes`.
The pre-push hook runs restore/build, doc links, taste checks, and the advisory code-health report before push.
These hooks do not replace the merge gate; run `.\scripts\verify.ps1` before merge-ready pushes.
In emergencies, bypass hooks with `git commit --no-verify` or `git push --no-verify`.

Generate the advisory report directly:

```powershell
py -3 scripts\advisory_code_health.py
```

Generate local coverage and summarize it:

```powershell
dotnet test Tests\InteractiveWorldMap.Tests.csproj --settings .runsettings --collect:"XPlat Code Coverage" --results-directory TestResults\coverage-advisory
py -3 scripts\summarize_coverage.py --results-directory TestResults\coverage-advisory
py -3 scripts\summarize_coverage.py --results-directory TestResults\coverage-advisory --min-line-coverage 45 --min-branch-coverage 40
```

Run the local complexity gate directly:

```powershell
py -3 -m pip install lizard
py -3 -m lizard -C 20 -x "*Tests*" -x "*Tools*" -x "*bin*" -x "*obj*" -x "*scripts*" -x "*TestResults*" .
```

## Related docs

- [AGENTS.md](../AGENTS.md) — agent entry map
- [docs/index.md](../docs/index.md) — full documentation catalog

## Zoomed-map resampler comparison

Generate ignored 1080p, 1440p, and 4K comparisons for the `SOUTH` label crop:

```powershell
dotnet run --project Tools\MapResamplerComparison\MapResamplerComparison.csproj -- `
  --source "Images&Content\Assets\World Map 1976.jpg" `
  --crop "5160,7390,358,202" `
  --output "temp\map-resampler-comparison"
```

Compare letter edges, thin borders, softness, blockiness, ringing, and halos.
The generated PNGs and timing CSV are local evidence, not product assets.
- [docs/exec-plans/completed/pin-extraction-script.md](../docs/exec-plans/completed/pin-extraction-script.md) — pin extraction design notes
