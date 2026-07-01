# Content Window Heading Cleanup Design

**Date:** June 30, 2026  
**Status:** Approved

## Goal

Simplify the three content companion windows:

- The left didactic window shows the selected location/person name instead of the generic `Information` heading.
- The main content window has no heading.
- The thumbnail window has no `Images` heading.

The selected name is the existing `Location.Name` value populated from the Excel coordinate list. A blank or whitespace-only name hides the didactic heading and its spacing.

## Design

`MainWindow.Content.partial.cs` passes `location.Name` to `DidacticTextWindow` together with the didactic text. `DidacticTextWindow` owns heading presentation: it trims and displays a nonblank name, or collapses the heading when the name is blank.

The main content and thumbnail headings are static removals. Their title controls and dedicated grid rows are removed from XAML, and the remaining content moves into the first row so no empty header space remains. Window shell `Title` properties may remain for diagnostics because these borderless windows do not render native title bars.

No new Excel lookup, binding layer, or view model is introduced.

## Testing

- A didactic-window STA test verifies a supplied location name is displayed.
- A didactic-window STA test verifies a blank name collapses the heading.
- XAML structure tests verify the main content window has no `TitleText` element or header row.
- XAML structure tests verify the thumbnail window contains no `Images` heading or header row.
- Existing content-presentation tests are updated to stop depending on the removed main title.
- Full repository verification remains the completion gate.

## Bookkeeping

On completion, remove the three implemented bullets from `docs/TO_DO.md` and add a user-visible entry under `CHANGELOG.md` `[Unreleased]`. No active exec plan is needed beyond the implementation plan for this small slice.
