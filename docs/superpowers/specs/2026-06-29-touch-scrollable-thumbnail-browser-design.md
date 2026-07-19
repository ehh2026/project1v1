# Touch-Scrollable Thumbnail Browser Design

## Goal

Allow users to browse every image in the right-side thumbnail window with a
mouse wheel, an automatically displayed scrollbar, or a vertical touchscreen
swipe, without a swipe accidentally loading a thumbnail into the main content
window.

## Current Behavior

`Views/ThumbnailBrowserWindow.xaml` renders the thumbnails in a plain
`ItemsControl`. The list has no bounded scrolling surface, and thumbnail
selection runs on `MouseLeftButtonDown`, before WPF can distinguish a tap from
a drag.

## Interaction Design

- The thumbnail list occupies the existing bounded content row.
- Vertical overflow is handled by a `ScrollViewer`.
- The vertical scrollbar appears automatically when content exceeds the
  available height and remains hidden otherwise.
- Horizontal scrolling and horizontal panning are disabled.
- A user may begin a vertical swipe over the panel background or directly over
  a thumbnail.
- A vertical drag scrolls the list and does not select the thumbnail under the
  initial contact point.
- A completed tap or mouse click selects that thumbnail and preserves the
  existing `ThumbnailSelected` event flow that updates the center content
  window.
- Mouse-wheel scrolling remains available.

## Architecture

Keep scrolling and gesture arbitration inside
`Views/ThumbnailBrowserWindow`. Wrap `ThumbnailList` in a WPF `ScrollViewer`
configured for vertical touch panning and automatic vertical scrollbar
visibility. Replace the image's press-time mouse handler with a button-style
completed-click command or handler.

The `ScrollViewer` remains responsible for recognizing and consuming drag
manipulations. The thumbnail item raises selection only after WPF recognizes a
completed click, so the window does not need a parallel custom touch-distance
state machine. `MainWindow.Content.partial.cs` and the existing
`ThumbnailSelected` event contract remain unchanged.

## Failure and Edge Behavior

- When all thumbnails fit, the panel behaves as it does now and shows no
  scrollbar.
- At the first and last item, additional swipes stop at the scroll boundary
  without moving or resizing the window.
- A diagonal gesture whose dominant movement is vertical scrolls; horizontal
  movement does not pan the list.
- Brief contact with meaningful movement is treated as scrolling, not
  selection.
- A stationary tap selects exactly one thumbnail.
- Selection styling continues to follow `SetSelectedIndex`.

## Testing

Add focused structural tests that load the thumbnail window XAML and verify:

- `ThumbnailList` is hosted by a `ScrollViewer`.
- Vertical scrollbar visibility is automatic.
- Horizontal scrolling is disabled.
- Vertical touch panning is enabled.
- Thumbnail activation no longer uses `MouseLeftButtonDown`.
- Thumbnail activation uses completed-click semantics.

Run the focused test project and the full Windows verification gate. Perform a
manual WPF smoke check with enough thumbnails to overflow:

1. Mouse-wheel and scrollbar movement reveal the final thumbnail.
2. A normal click loads the chosen thumbnail in the center content window.
3. A swipe beginning on panel background scrolls.
4. A swipe beginning directly on a thumbnail scrolls without loading it.
5. A stationary touchscreen tap loads exactly one thumbnail.

The last three checks require a real touchscreen or equivalent Windows touch
input; automated XAML tests cannot fully validate WPF's hardware gesture
arbitration.

## Modularity / File Size Impact

The implementation should remain confined to
`Views/ThumbnailBrowserWindow.xaml`, its short code-behind file if a completed
click handler is retained, and focused tests. No new service, model, config
option, or `MainWindow` orchestration is needed. The touched C# files remain
well below the 800-line limit, and the Views layer keeps depending only on WPF
and Models.

## Documentation Follow-Through

When implementation is complete:

- Remove the completed thumbnail scrolling bullet from `docs/TO_DO.md`.
- Add the user-visible change under `[Unreleased]` in `CHANGELOG.md`.
- Archive the implementation plan under `docs/exec-plans/completed/` and update
  `docs/exec-plans/active/README.md`.
