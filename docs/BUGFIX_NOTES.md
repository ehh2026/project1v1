# Bug Fix Notes

## Issue: Clicking Outside Popup Windows Sometimes Doesn't Close Them

### Problem Description
Users reported two related issues:
1. Clicking outside the content subwindow sometimes failed to close it
2. When clicking a marker while a subwindow is open, the new subwindow wouldn't close when clicking outside

### Root Causes

#### Issue 1: Inconsistent Click Detection
The issue was caused by event routing in WPF:

1. **Event Bubbling**: The original implementation used `MouseLeftButtonDown` event, which fires during the bubbling phase (after child controls process the event)
2. **Event Handling**: When clicking on certain UI elements (like the MarkerLayerControl canvas), the event might not bubble up to the MainWindow
3. **Timing**: The event routing order meant that sometimes the click was consumed before reaching the MainWindow handler

#### Issue 2: Async Window Management
When clicking a marker while a subwindow is open:

1. `CloseActiveSubwindow()` was called and started the close animation
2. The new subwindow was created immediately (before the old one finished closing)
3. The `_activeSubwindow` reference was updated to the new window
4. But the old window was still animating and the reference was set to null in the animation callback
5. This caused the `_activeSubwindow` reference to become null after the new window was created

### Solutions

#### Solution 1: Use Preview Events
#### Solution 1: Use Preview Events
Changed from `MouseLeftButtonDown` to `PreviewMouseLeftButtonDown`:

**Before:**
```csharp
MouseLeftButtonDown += OnMouseLeftButtonDown;

private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
{
    var position = e.GetPosition(this);
    HandleOutsideClick(position);
}
```

**After:**
```csharp
PreviewMouseLeftButtonDown += OnPreviewMouseLeftButtonDown;

private void OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
{
    // Only handle if there's an active subwindow
    if (_activeSubwindow == null)
        return;

    // Get the click position relative to the window
    var position = e.GetPosition(this);
    var screenPoint = PointToScreen(position);

    // Check if click is outside the subwindow
    if (!_activeSubwindow.ContainsPoint(screenPoint))
    {
        // Check if the click was on a marker (which will open a new subwindow)
        var markerPosition = e.GetPosition(MarkerLayer);
        var clickedMarker = MarkerLayer.HitTest(markerPosition);
        
        // Only close if not clicking on a marker
        if (clickedMarker == null)
        {
            CloseActiveSubwindow();
            e.Handled = true; // Prevent further processing
        }
    }
}
```

#### Solution 2: Async Window Management
Added proper async handling to wait for window close animation to complete before opening a new one:

**Before:**
```csharp
public async void ShowContentForLocation(Location location)
{
    // Close existing subwindow if any
    CloseActiveSubwindow();
    
    // Immediately create new subwindow (old one still animating!)
    _activeSubwindow = new ContentSubwindow { ... };
}

public void CloseActiveSubwindow()
{
    if (_activeSubwindow != null)
    {
        _activeSubwindow.AnimateClose(() =>
        {
            _activeSubwindow = null; // This runs AFTER new window is created!
        });
    }
}
```

**After:**
```csharp
public async void ShowContentForLocation(Location location)
{
    // Close existing subwindow and WAIT for it to complete
    if (_activeSubwindow != null)
    {
        await CloseActiveSubwindowAsync();
    }
    
    // Now create new subwindow (old one is fully closed)
    _activeSubwindow = new ContentSubwindow { ... };
}

public void CloseActiveSubwindow()
{
    if (_activeSubwindow != null)
    {
        var windowToClose = _activeSubwindow;
        _activeSubwindow = null; // Clear reference immediately
        
        windowToClose.AnimateClose(() =>
        {
            Focus(); // Return focus to main window
        });
    }
}

private Task CloseActiveSubwindowAsync()
{
    if (_activeSubwindow == null)
        return Task.CompletedTask;

    var tcs = new TaskCompletionSource<bool>();
    var windowToClose = _activeSubwindow;
    _activeSubwindow = null; // Clear reference immediately

    windowToClose.AnimateClose(() =>
    {
        Focus();
        tcs.SetResult(true); // Signal completion
    });

    return tcs.Task;
}
```

### Why This Works

#### Solution 1: Preview Events
1. **Preview Events Fire First**: `PreviewMouseLeftButtonDown` fires during the tunneling phase, before any child controls process the event
2. **Guaranteed Execution**: This ensures the MainWindow always gets a chance to check if the click is outside the subwindow
3. **Smart Handling**: The new implementation:
   - Only processes clicks when a subwindow is active
   - Checks if the click is outside the subwindow bounds
   - Verifies the click isn't on a marker (which should open a new subwindow)
   - Marks the event as handled to prevent duplicate processing

#### Solution 2: Async Window Management
1. **Immediate Reference Clearing**: The `_activeSubwindow` reference is set to null immediately when closing starts, not in the animation callback
2. **Async Waiting**: When opening a new window, we wait for the old window's close animation to complete using `TaskCompletionSource`
3. **No Race Conditions**: The new window is only created after the old window is fully closed
4. **Proper Sequencing**: 
   - Old window reference cleared → Animation starts → Animation completes → New window created
   - Instead of: Animation starts → New window created → Animation completes → Reference cleared (wrong!)

### Additional Improvements

The new implementation also:
- Performs explicit hit testing on markers to avoid closing when clicking a marker
- Uses `e.Handled = true` to prevent the click from propagating further
- Converts coordinates properly using `PointToScreen` for accurate bounds checking
- Uses `TaskCompletionSource` for proper async/await pattern with animations
- Stores window reference in local variable before starting animation to avoid race conditions

### Testing
To verify the fixes:
1. Open the application
2. Click any marker to open a subwindow
3. Click anywhere on the map (not on a marker) - subwindow should close ✓
4. Click a marker to open a subwindow
5. Click another marker - old subwindow should close smoothly and new one should open ✓
6. Click outside the new subwindow - it should close ✓
7. Repeat steps 4-6 multiple times rapidly - should work consistently ✓
8. Click on the map background multiple times - should consistently close the subwindow ✓

### Related Files
- `MainWindow.xaml.cs` - Main fix location
- `Views/ContentSubwindow.xaml.cs` - ContainsPoint method used for bounds checking
- `Views/MarkerLayerControl.xaml.cs` - HitTest method used for marker detection

---

**Fixed:** March 1, 2026  
**Status:** ✅ Resolved
