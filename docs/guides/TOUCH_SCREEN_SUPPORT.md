# Touch Screen Support Analysis

## Current State

The app is built on WPF, which provides automatic touch-to-mouse event promotion for single-finger taps. This means basic touch interaction works without any changes.

### What works out of the box

- Tapping markers and cluster markers — WPF promotes single-finger tap to `MouseLeftButtonDown`, so all click handlers fire normally
- Tapping the Back button
- Tapping outside a content subwindow to close it (via `PreviewMouseLeftButtonDown`)

### What doesn't work

- **Pinch-to-zoom** — zoom is only triggered by tapping a cluster marker, not by a pinch gesture. Touch users expect pinch-to-zoom on the map itself.
- **Pan/drag** — there is no map panning implemented at all. After zooming in, users have no way to move around the map with touch.
- **Hover effects** — the `IsMouseOver` trigger on the Back button uses mouse hover state, which is never set by touch input. The hover style won't apply.

### Potential gotcha

If `IsManipulationEnabled = true` is ever set on any element in the visual tree, WPF stops promoting touch events to mouse events entirely. This would silently break all existing tap handlers. Avoid setting this unless you also add full manipulation event handling.

---

## How to Add Full Touch Support

### 1. Enable manipulations on the map

In `MainWindow.xaml`, add `IsManipulationEnabled="True"` to the `MapDisplayControl`:

```xml
<views:MapDisplayControl x:Name="MapDisplay"
                         IsManipulationEnabled="True"
                         ... />
```

Then in `MainWindow.xaml.cs`, wire up the manipulation events:

```csharp
MapDisplay.ManipulationDelta += OnMapManipulationDelta;
MapDisplay.ManipulationCompleted += OnMapManipulationCompleted;
MapDisplay.ManipulationStarting += OnMapManipulationStarting;
```

### 2. Handle ManipulationStarting

This sets the coordinate space for the manipulation:

```csharp
private void OnMapManipulationStarting(object sender, ManipulationStartingEventArgs e)
{
    e.ManipulationContainer = this;
    e.Handled = true;
}
```

### 3. Handle ManipulationDelta (pan + pinch)

This is where pan and pinch-to-zoom are applied. The delta gives you translation and scale changes since the last event:

```csharp
private void OnMapManipulationDelta(object sender, ManipulationDeltaEventArgs e)
{
    var delta = e.DeltaManipulation;

    // Apply pan
    MapDisplay.TranslateTransform.X += delta.Translation.X;
    MapDisplay.TranslateTransform.Y += delta.Translation.Y;

    // Apply pinch-to-zoom, scaling around the pinch origin
    var scale = delta.Scale.X; // Scale.X and Scale.Y are typically equal for pinch
    var origin = e.ManipulationOrigin;

    var currentScale = MapDisplay.ScaleTransform.ScaleX;
    var newScale = Math.Clamp(currentScale * scale, 1.0, ZoomScale * 2);

    // Adjust translation so zoom is centered on the pinch point
    MapDisplay.TranslateTransform.X = origin.X - (origin.X - MapDisplay.TranslateTransform.X) * (newScale / currentScale);
    MapDisplay.TranslateTransform.Y = origin.Y - (origin.Y - MapDisplay.TranslateTransform.Y) * (newScale / currentScale);

    MapDisplay.ScaleTransform.ScaleX = newScale;
    MapDisplay.ScaleTransform.ScaleY = newScale;

    // Counter-scale markers to keep them a consistent visual size
    CounterScaleMarkers(1.0 / newScale);

    e.Handled = true;
}
```

### 4. Handle ManipulationCompleted (inertia / snap)

Optionally add inertia so the map glides after a fast swipe, and snap back to bounds if the user has panned too far:

```csharp
private void OnMapManipulationCompleted(object sender, ManipulationCompletedEventArgs e)
{
    // Snap scale back to 1.0 if user pinched below minimum
    if (MapDisplay.ScaleTransform.ScaleX < 1.0)
    {
        MapDisplay.ScaleTransform.ScaleX = 1.0;
        MapDisplay.ScaleTransform.ScaleY = 1.0;
        MapDisplay.TranslateTransform.X = 0;
        MapDisplay.TranslateTransform.Y = 0;
        CounterScaleMarkers(1.0);
    }

    e.Handled = true;
}
```

To enable inertia (the glide effect), handle `ManipulationInertiaStarting` instead of letting it fall through:

```csharp
MapDisplay.ManipulationInertiaStarting += (s, e) =>
{
    e.TranslationBehavior.DesiredDeceleration = 10.0 * 96.0 / (1000.0 * 1000.0);
    e.Handled = true;
};
```

### 5. Fix hover styles for touch

Replace the `IsMouseOver` trigger on the Back button with a style that also responds to touch. The simplest approach is to use a `Pressed` visual state or handle `TouchEnter`/`TouchLeave` events manually. Alternatively, remove the hover style entirely since it's a minor cosmetic issue on touch-only screens.

### 6. Marker tap targets

On a large touch screen, small tap targets are a usability problem. Consider increasing marker sizes or their hit test areas when running in touch mode. You can detect touch capability at startup:

```csharp
bool isTouchEnabled = Tablet.TabletDevices.Cast<TabletDevice>()
    .Any(t => t.Type == TabletDeviceType.Touch);
```

Then conditionally apply a larger marker size or hit area.

---

## Summary of Changes Required

| Feature | File(s) to change | Effort |
|---|---|---|
| Pan with finger drag | `MainWindow.xaml`, `MainWindow.xaml.cs` | Low |
| Pinch-to-zoom | `MainWindow.xaml.cs` | Medium |
| Inertia/glide after swipe | `MainWindow.xaml.cs` | Low |
| Snap to bounds after pan | `MainWindow.xaml.cs` | Medium |
| Fix hover styles | `MainWindow.xaml` | Low |
| Larger touch targets for markers | `Views/LocationMarker.xaml`, `Views/ClusterMarker.xaml` | Low |
