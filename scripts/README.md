# Scripts Index

Python and PowerShell tooling for verification, CI harness checks, and optional pin image processing.

## Python environment

| Path | Purpose |
|------|---------|
| `scripts/requirements.txt` | Pin-extraction deps: Pillow, numpy, scipy |
| `scripts/venv/` | Local venv (gitignored — create per machine) |

**Harness scripts** (`verify_*.py`, `doc_gardening.py`) use the **stdlib only**. `verify.ps1` and `verify.sh` invoke system `python` / `python3` / `py -3` — no venv required.

**Pin tooling** (`extract_pins.py`, `extract_pins_v2.py`, `split_pin_parts.py`) requires the venv:

```powershell
py -3 -m venv scripts\venv
.\scripts\venv\Scripts\Activate.ps1
pip install -r scripts\requirements.txt
```

Full setup: [docs/SETUP_GUIDE.md](../docs/SETUP_GUIDE.md#python-harness-and-optional-tooling).

## Script catalog

| Script | Used by | Deps | Description |
|--------|---------|------|-------------|
| `verify.ps1` | Agents, CI (Windows) | — | Full build, test, harness, startup validation |
| `verify.sh` | Agents, CI (macOS/Linux) | — | Build, test, harness (no WPF startup on non-Windows) |
| `validate_startup.ps1` | `verify.ps1` | — | Headless WPF startup check (Windows) |
| `verify_nuget_vulnerabilities.py` | `verify.ps1`, `verify.sh`, CI | stdlib | Fail on High/Critical NuGet advisories |
| `verify_doc_links.py` | `verify.ps1`, `verify.sh`, doc-gardening CI | stdlib | Markdown link integrity |
| `verify_taste.py` | `verify.ps1`, `verify.sh` | stdlib | Architecture taste invariants (Views, JObject, etc.) |
| `doc_gardening.py` | Weekly CI | stdlib | Doc drift: links, AGENTS size, stale active plans |
| `extract_pins.py` | Manual | venv | Extract pin blobs from source image at configurable thresholds |
| `extract_pins_v2.py` | Manual | venv | Alternate pin extraction pipeline |
| `split_pin_parts.py` | Manual | venv | Split extracted pins into parts |

## Related docs

- [AGENTS.md](../AGENTS.md) — agent entry map
- [docs/index.md](../docs/index.md) — full documentation catalog
- [docs/exec-plans/completed/pin-extraction-script.md](../docs/exec-plans/completed/pin-extraction-script.md) — pin extraction design notes
