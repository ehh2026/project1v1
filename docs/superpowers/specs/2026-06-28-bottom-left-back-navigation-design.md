# Bottom-Left Back Navigation Design

## Goal

Move zoomed-content navigation away from the upper-left Information panel by placing it in the lower-left corner. Rename the button to `← Back to Full Map`. When the manual-layout status is eligible to appear, place it directly above the button.

## Layout

Replace the two independent lower-left overlays with one bottom-aligned vertical `StackPanel`:

1. `ManualLayoutIndicator`
2. `BackButton`

The stack owns the existing 20-pixel outer margin. A small margin on the indicator separates it from the button. Because collapsed WPF elements do not reserve layout space, the back button remains at the same lower-left position whether the indicator is visible or collapsed.

The button keeps its current styling and click behavior. Its label changes from `← Back to Map` to `← Back to Full Map`.

## Developer-Tools Gate

The back button is normal guest navigation and is not gated by developer mode.

`ManualLayoutIndicator` is visible only when all of these are true:

- `EnableDeveloperTools` is `true`;
- a manual layout is active; and
- `ManualLayoutEditor.ShowLayoutIndicator` is `true`.

The existing `AreDeveloperToolsEnabled()` helper remains the single MainWindow gate. No new config option is introduced.

## Implementation Boundaries

- `MainWindow.xaml` owns the bottom-left stack and control order.
- `MainWindow.LayoutEditor.partial.cs` owns the indicator visibility decision.
- Existing navigation state and `OnBackButtonClick` behavior remain unchanged.
- No other overlays or content-pane layout are changed.

## Testing

Add focused structural regression tests that verify:

- the back button is bottom-aligned and labeled `← Back to Full Map`;
- the manual-layout indicator precedes the back button in one bottom-left stack;
- manual-layout indicator visibility requires `AreDeveloperToolsEnabled()`.

Run the focused tests, then the Windows full gate with `.\scripts\verify.ps1`.

## Documentation

After implementation:

- remove the completed Information-panel/back-button overlap bullet from `docs/TO_DO.md`;
- update `[Unreleased]` in `CHANGELOG.md`;
- archive the implementation plan after every acceptance criterion and verification step is complete.
