# Tech Debt Tracker

Thin index of known debt. Detail and checklists live in linked plans — not duplicated here.

Sources: [TO_DO.md](../TO_DO.md), [REFACTORING_ASSESSMENT.md](../assessments/REFACTORING_ASSESSMENT.md), [LARGE_FILE_REFACTORING_ASSESSMENT.md](../assessments/LARGE_FILE_REFACTORING_ASSESSMENT.md)

## High Priority

| ID | Item | Plan / doc | Status |
|----|------|------------|--------|
| TD-001 | `MainWindow.xaml.cs` god object | [LARGE_FILE_REFACTORING_ASSESSMENT.md](../assessments/LARGE_FILE_REFACTORING_ASSESSMENT.md) §1, [refactoring-assessment-followthrough-plan.md](active/refactoring-assessment-followthrough-plan.md) | Resolved (2026-06-08) — primary ~732 lines; partials for layout, composite pins, navigation, content |
| TD-013 | `Tools/PinDebugger/Program.cs` | [LARGE_FILE_REFACTORING_ASSESSMENT.md](../assessments/LARGE_FILE_REFACTORING_ASSESSMENT.md) §2 | Resolved (2026-06-08) |
| TD-003 | Property-based tests (FsCheck) | [.kiro/specs/.../tasks.md](../../.kiro/specs/interactive-world-map/tasks.md) `*` items | Open |
| TD-004 | UI/integration tests | [TO_DO.md](../TO_DO.md) | Open |

## Medium Priority

| ID | Item | Plan / doc | Status |
|----|------|------------|--------|
| TD-005 | Empty `ViewModels/` — MVVM incomplete | Project structure | Open |
| TD-006 | Marker distortion at 50x+ zoom | [TO_DO.md](../TO_DO.md) | Open |
| TD-018 | Unbounded `_contentCache` in `LoadLocationContentAsync` (UI gallery path uncached — separate) | [refactoring-assessment-followthrough-plan.md](active/refactoring-assessment-followthrough-plan.md) Phase 16 | Open |
| TD-008 | README staleness | README.md | Resolved (2026-06-04) |

## Low Priority / Features

| ID | Item | Plan / doc | Status |
|----|------|------------|--------|
| TD-009 | Home/welcome screen before map | [TO_DO.md](../TO_DO.md) | Open |
| TD-010 | Subwindow opens near pin, not center | [TO_DO.md](../TO_DO.md) | Open |
| TD-011 | Touch screen support | [TOUCH_SCREEN_SUPPORT.md](../guides/TOUCH_SCREEN_SUPPORT.md) | Open |

## Resolved

| ID | Item | Resolved |
|----|------|----------|
| TD-002 | Map filename inconsistency | 2026-06-04 — `Models/ContentFileNames.cs` |
| TD-007 | Misplaced markers when zoomed out | 2026-06-06 — [UNZOOMED_MARKER_OFFSET_ASSESSMENT.md](../assessments/UNZOOMED_MARKER_OFFSET_ASSESSMENT.md) |
| TD-012 | No agent harness (AGENTS.md, CI, verify scripts) | 2026-06-04 |
| TD-014 | Hard-coded map dimensions / wrong validator ceilings | 2026-07-30 — `MapMetadata`; display-space `StartupValidator` ceilings |
| TD-017 | Orphan `Models/ApplicationState` | 2026-07-30 — deleted unused type |
| TD-015 | `LocationClusterer` O(n²) neighbor scan | 2026-07-30 — `SpatialGrid` + 3×3 neighbor query |
| TD-016 | `ExcelCoordinateReader` `XmlDocument` DOM parse | 2026-07-30 — stream shared strings + worksheet rows via `XmlReader` |

## Promotion Rule

When the same debt causes two agent failures, promote the fix from this tracker into:
1. [golden-principles.md](../design-docs/golden-principles.md), then
2. Structural test or `scripts/verify_taste.py` rule
