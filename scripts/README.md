# Scripts Index

Python and PowerShell tooling for verification, CI harness checks, and optional composite-pin asset processing.

## Python environment

| Path | Purpose |
|------|---------|
| `scripts/requirements.txt` | Composite-pin asset tooling deps: Pillow, numpy, scipy |
| `scripts/venv/` | Local venv (gitignored — create per machine) |

**Harness scripts** (`verify_*.py`, `doc_gardening.py`) use the **stdlib only**. On Windows, run them with `py -3`; `verify.ps1` also prefers `py -3` before bare `python` so pyenv shims without a selected version do not break the harness. On macOS/Linux, use `python3`. No venv is required for harness scripts.

**Composite-pin asset tooling** (`split_pin_parts.py`, `create_shaft_asset_variants.py`) requires the venv:

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
| `verify_taste.py` | `verify.ps1`, `verify.sh` | stdlib | Architecture taste invariants (Views, JObject, etc.) |
| `doc_gardening.py` | Weekly CI | stdlib | Doc drift: links, AGENTS/TO_DO size, active plan registry, front-matter |
| `split_pin_parts.py` | Manual | venv | Split extracted pins into parts |
| `create_shaft_asset_variants.py` | Manual | venv | Generate shaft contrast variants: outer (`outline_dark_7px`), inner (`inner_dark_3px`), or combo (`outline_dark_6px_in2px`); writes preview grids |

## Related docs

- [AGENTS.md](../AGENTS.md) — agent entry map
- [docs/index.md](../docs/index.md) — full documentation catalog
- [docs/exec-plans/completed/pin-extraction-script.md](../docs/exec-plans/completed/pin-extraction-script.md) — pin extraction design notes
