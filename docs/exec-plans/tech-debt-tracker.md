# Tech Debt Tracker

Known debt distilled from [TO_DO.md](../TO_DO.md) and [REFACTORING_ASSESSMENT.md](../REFACTORING_ASSESSMENT.md).

## High Priority

| ID | Item | Source | Status |
|----|------|--------|--------|
| TD-001 | `MainWindow.xaml.cs` god object (~680+ lines) | REFACTORING_ASSESSMENT | Open |
| TD-002 | Map filename inconsistency (`World Map 1976.jpg` vs `World Map Extra Large.jpg`) | StartupValidator vs ContentLoader | Open |
| TD-003 | Property-based tests (FsCheck) from Kiro design | tasks.md `*` items | Open |
| TD-004 | UI/integration tests | TO_DO.md | Open |

## Medium Priority

| ID | Item | Source | Status |
|----|------|--------|--------|
| TD-005 | Empty `ViewModels/` — MVVM incomplete | Project structure | Open |
| TD-006 | Marker distortion at 50x+ zoom | TO_DO.md | Open |
| TD-007 | Misplaced markers when zoomed out | TO_DO.md | Open |
| TD-008 | README staleness | README.md | Resolved (2026-06-04) |

## Low Priority / Features

| ID | Item | Source | Status |
|----|------|--------|--------|
| TD-009 | Home/welcome screen before map | TO_DO.md | Open |
| TD-010 | Subwindow opens near pin, not center | TO_DO.md | Open |
| TD-011 | Touch screen support | TOUCH_SCREEN_SUPPORT.md | Open |

## Resolved

| ID | Item | Resolved |
|----|------|----------|
| TD-012 | No agent harness (AGENTS.md, CI, verify scripts) | 2026-06-04 |

## Promotion Rule

When the same debt causes two agent failures, promote the fix from this tracker into:
1. [golden-principles.md](../design-docs/golden-principles.md), then
2. Structural test or `scripts/verify_taste.py` rule
