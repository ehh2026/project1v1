---
status: completed
owner: agent
started: 2026-06-01
completed: 2026-06-04
requirements_ref: pin-extraction
---

# Pin Extraction Script

Extract individual pin PNGs from `Images&Content/Pins.jpg` with transparent backgrounds.

## Outcome

- Script: `scripts/extract_pins.py`
- Output: `Images&Content/Pins/threshold_*/pin_NN.png` + composite previews
- Dependencies: Pillow, numpy, scipy in `scripts/requirements.txt`

## Original plan

See [.cursor/plans/pin_extraction_script_a7871bc8.plan.md](../../../.cursor/plans/pin_extraction_script_a7871bc8.plan.md)
