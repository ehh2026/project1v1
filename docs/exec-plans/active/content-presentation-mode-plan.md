---
status: active
owner: agent
started: 2026-06-30
---

# Content Presentation Mode Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Let visitors tap or click center content to toggle a borderless, main-window-sized presentation with configurable black background opacity while companion windows temporarily hide.

**Architecture:** `ContentSubwindow` owns saved bounds and presentation styling. `MainWindow.Content.partial.cs` constructs configured content windows and coordinates thumbnail/didactic visibility; `VisualConfig` owns the opacity setting. STA behavior tests cover the WPF state transitions, with XAML/source guards for input and orchestration wiring.

**Tech Stack:** C# 10, .NET 6, WPF, xUnit, Newtonsoft.Json, PowerShell.

**Design:** [2026-06-30-content-presentation-mode-design.md](../../superpowers/specs/2026-06-30-content-presentation-mode-design.md)

---

## File and Ownership Map

| File | Responsibility |
|------|----------------|
| `Models/VisualConfig.cs`, `visual-config.json` | Define, clamp, and configure `MaximizedContentBackgroundOpacity`. |
| `Views/ContentSubwindow.xaml` | Identify the completed-click/tap interaction surface. |
| `Views/ContentSubwindow.xaml.cs` | Save/restore bounds and chrome, apply presentation styling, publish state changes. |
| `MainWindow.Content.partial.cs` | Create configured content windows and coordinate companion visibility. |
| `Tests/VisualConfigServiceTests.cs` | Verify opacity defaults, loading, and clamping. |
| `Tests/ContentPresentationModeTests.cs` | Verify presentation behavior and integration contracts. |

## Modularity and File-Size Impact

- `ContentSubwindow.xaml.cs` grows from about 234 lines but must remain below 380. Direct WPF state belongs here.
- `MainWindow.Content.partial.cs` grows from about 229 lines but must remain below 320. It coordinates windows but contains no presentation styling.
- Do not move WPF window references into Services; that would violate the Views dependency boundary.

---

### Task 1: Add the Background-Opacity Configuration

**Files:**
- Modify: `Models/VisualConfig.cs`
- Modify: `visual-config.json`
- Modify: `Tests/VisualConfigServiceTests.cs`

- [ ] **Step 1: Write failing tests**

Add:

```csharp
[Fact]
public void Load_MaximizedContentBackgroundOpacity_UsesOpaqueDefaultWhenOmitted()
{
    var tempDir = CreateTempDir();
    try
    {
        var path = Path.Combine(tempDir, "visual-config.json");
        File.WriteAllText(path, @"{ ""LocationMarkerSize"": 18.5 }");
        Assert.Equal(
            1.0,
            new VisualConfigService().Load(path)
                .MaximizedContentBackgroundOpacity);
    }
    finally
    {
        Directory.Delete(tempDir, recursive: true);
    }
}

[Theory]
[InlineData(-0.25, 0.0)]
[InlineData(0.4, 0.4)]
[InlineData(1.25, 1.0)]
public void MaximizedContentBackgroundOpacity_Clamps(
    double requested,
    double expected)
{
    var config = new VisualConfig
    {
        MaximizedContentBackgroundOpacity = requested
    };
    Assert.Equal(expected, config.MaximizedContentBackgroundOpacity);
}

[Fact]
public void Load_MaximizedContentBackgroundOpacity_Deserializes()
{
    var tempDir = CreateTempDir();
    try
    {
        var path = Path.Combine(tempDir, "visual-config.json");
        File.WriteAllText(
            path,
            @"{ ""MaximizedContentBackgroundOpacity"": 0.65 }");
        Assert.Equal(
            0.65,
            new VisualConfigService().Load(path)
                .MaximizedContentBackgroundOpacity);
    }
    finally
    {
        Directory.Delete(tempDir, recursive: true);
    }
}
```

- [ ] **Step 2: Verify RED**

Run:

```powershell
dotnet test Tests\InteractiveWorldMap.Tests.csproj --filter "FullyQualifiedName~MaximizedContentBackgroundOpacity"
```

Expected: compilation fails because the property is absent.

- [ ] **Step 3: Implement the property and checked-in value**

Add `using System;` and this member to `VisualConfig`:

```csharp
private double _maximizedContentBackgroundOpacity = 1.0;

public double MaximizedContentBackgroundOpacity
{
    get => _maximizedContentBackgroundOpacity;
    set => _maximizedContentBackgroundOpacity = double.IsNaN(value)
        ? 1.0
        : Math.Clamp(value, 0.0, 1.0);
}
```

Add to `visual-config.json`:

```json
"MaximizedContentBackgroundOpacity": 1.0,
```

- [ ] **Step 4: Verify GREEN and commit**

Run the focused test command from Step 2. Expected: all selected tests pass.

```powershell
git add Models/VisualConfig.cs visual-config.json Tests/VisualConfigServiceTests.cs
git commit -m "feat: configure content presentation background"
```

---

### Task 2: Implement ContentSubwindow Presentation State

**Files:**
- Modify: `Views/ContentSubwindow.xaml`
- Modify: `Views/ContentSubwindow.xaml.cs`
- Create: `Tests/ContentPresentationModeTests.cs`

- [ ] **Step 1: Write failing STA behavior tests**

Create `Tests/ContentPresentationModeTests.cs` with tests that construct `ContentSubwindow` on an STA thread and assert:

```csharp
var window = new ContentSubwindow
{
    Left = 100,
    Top = 120,
    Width = 400,
    Height = 300,
    MaximizedBackgroundOpacity = 0.5
};
var border = Assert.IsType<Border>(window.FindName("ContentBorder"));
var title = Assert.IsType<TextBlock>(window.FindName("TitleText"));
var translate = Assert.IsType<Button>(window.FindName("TranslateButton"));
translate.Visibility = Visibility.Visible;

Assert.True(window.TryTogglePresentationMode(
    new Rect(10, 20, 1000, 700)));
Assert.True(window.IsPresentationMode);
Assert.Equal(new Rect(10, 20, 1000, 700),
    new Rect(window.Left, window.Top, window.Width, window.Height));
Assert.Equal(new Thickness(0), border.BorderThickness);
Assert.Equal(new Thickness(0), border.Padding);
Assert.Equal(new CornerRadius(0), border.CornerRadius);
Assert.Null(border.Effect);
Assert.Equal(Visibility.Collapsed, title.Visibility);
Assert.Equal(Visibility.Visible, translate.Visibility);
Assert.Equal(
    Color.FromArgb(128, 0, 0, 0),
    Assert.IsType<SolidColorBrush>(border.Background).Color);

Assert.True(window.TryTogglePresentationMode(
    new Rect(10, 20, 1000, 700)));
Assert.False(window.IsPresentationMode);
Assert.Equal(new Rect(100, 120, 400, 300),
    new Rect(window.Left, window.Top, window.Width, window.Height));
```

Use the existing `DrawnPinTipCapRendererTests.RunOnStaThread` pattern. Add a separate test asserting zero-width owner bounds return `false` without changing normal mode.

- [ ] **Step 2: Write a failing XAML input contract test**

Parse `Views/ContentSubwindow.xaml` with `XDocument` and assert:

```csharp
Assert.Equal(
    "ContentSurface_MouseLeftButtonUp",
    (string?)contentSurface.Attribute("MouseLeftButtonUp"));
Assert.DoesNotContain(translateButton, contentSurface.Descendants());
Assert.DoesNotContain(
    document.Descendants().Attributes(),
    attribute => attribute.Name.LocalName == "MouseLeftButtonDown");
```

- [ ] **Step 3: Verify RED**

```powershell
dotnet test Tests\InteractiveWorldMap.Tests.csproj --filter "FullyQualifiedName~ContentPresentationModeTests"
```

Expected: missing presentation API and XAML surface failures.

- [ ] **Step 4: Wire the completed-click surface**

Name the row-1 content grid and wire mouse-up:

```xml
<Grid x:Name="ContentInteractionSurface"
      Grid.Row="1"
      Background="Transparent"
      MouseLeftButtonUp="ContentSurface_MouseLeftButtonUp">
```

Keep `TranslateButton` in row 2 outside this grid.

- [ ] **Step 5: Implement presentation transitions**

Add saved normal bounds/style fields and:

```csharp
public event EventHandler? PresentationModeChanged;
public bool IsPresentationMode { get; private set; }
public double MaximizedBackgroundOpacity { get; set; } = 1.0;

public bool TryTogglePresentationMode(Rect ownerBounds)
{
    if (!IsPresentationMode &&
        (ownerBounds.IsEmpty ||
         ownerBounds.Width <= 0 ||
         ownerBounds.Height <= 0))
        return false;

    if (IsPresentationMode)
        ExitPresentationMode();
    else
        EnterPresentationMode(ownerBounds);

    PresentationModeChanged?.Invoke(this, EventArgs.Empty);
    return true;
}
```

Add these snapshot fields and transition methods:

```csharp
private Rect? _normalBounds;
private Brush? _normalBackground;
private Thickness _normalBorderThickness;
private Thickness _normalPadding;
private CornerRadius _normalCornerRadius;
private Effect? _normalEffect;
private Visibility _normalTitleVisibility;

private void EnterPresentationMode(Rect ownerBounds)
{
    _normalBounds = new Rect(Left, Top, Width, Height);
    _normalBackground = ContentBorder.Background;
    _normalBorderThickness = ContentBorder.BorderThickness;
    _normalPadding = ContentBorder.Padding;
    _normalCornerRadius = ContentBorder.CornerRadius;
    _normalEffect = ContentBorder.Effect;
    _normalTitleVisibility = TitleText.Visibility;

    var opacity = Math.Clamp(MaximizedBackgroundOpacity, 0.0, 1.0);
    var alpha = (byte)Math.Round(opacity * byte.MaxValue);
    ContentBorder.Background = new SolidColorBrush(
        Color.FromArgb(alpha, 0, 0, 0));
    ContentBorder.BorderThickness = new Thickness(0);
    ContentBorder.Padding = new Thickness(0);
    ContentBorder.CornerRadius = new CornerRadius(0);
    ContentBorder.Effect = null;
    TitleText.Visibility = Visibility.Collapsed;

    Left = ownerBounds.Left;
    Top = ownerBounds.Top;
    Width = ownerBounds.Width;
    Height = ownerBounds.Height;
    IsPresentationMode = true;
}

private void ExitPresentationMode()
{
    if (_normalBounds is not Rect bounds)
        return;

    Left = bounds.Left;
    Top = bounds.Top;
    Width = bounds.Width;
    Height = bounds.Height;
    ContentBorder.Background = _normalBackground;
    ContentBorder.BorderThickness = _normalBorderThickness;
    ContentBorder.Padding = _normalPadding;
    ContentBorder.CornerRadius = _normalCornerRadius;
    ContentBorder.Effect = _normalEffect;
    TitleText.Visibility = _normalTitleVisibility;
    _normalBounds = null;
    IsPresentationMode = false;
}
```

Add:

```csharp
private void ContentSurface_MouseLeftButtonUp(
    object sender,
    MouseButtonEventArgs e)
{
    if (Owner is not Window owner)
        return;

    var width = owner.ActualWidth > 0 ? owner.ActualWidth : owner.Width;
    var height = owner.ActualHeight > 0 ? owner.ActualHeight : owner.Height;
    if (TryTogglePresentationMode(
        new Rect(owner.Left, owner.Top, width, height)))
        e.Handled = true;
}
```

- [ ] **Step 6: Preserve presentation size on content refresh**

Wrap the `ShowContent` size/position block:

```csharp
if (!IsPresentationMode)
{
    Width = PreferredSize.Width;
    Height = PreferredSize.Height;
    PositionWindow(anchorPosition);
}
```

- [ ] **Step 7: Verify GREEN and commit**

Run the focused Task 2 command. Expected: all presentation tests pass.

```powershell
git add Views/ContentSubwindow.xaml Views/ContentSubwindow.xaml.cs Tests/ContentPresentationModeTests.cs
git commit -m "feat: add borderless content presentation mode"
```

---

### Task 3: Coordinate Companion Windows

**Files:**
- Modify: `MainWindow.Content.partial.cs`
- Modify: `Tests/ContentPresentationModeTests.cs`

- [ ] **Step 1: Write a failing source contract test**

Read `MainWindow.Content.partial.cs` and assert it contains:

```csharp
Assert.Contains("CreateContentSubwindow(Location location)", source);
Assert.Contains(
    "MaximizedBackgroundOpacity = _visualConfig.MaximizedContentBackgroundOpacity",
    source);
Assert.Contains(
    "window.PresentationModeChanged += OnContentPresentationModeChanged;",
    source);
Assert.Contains("_activeThumbnailBrowser.Hide();", source);
Assert.Contains("_activeDidacticWindow.Hide();", source);
Assert.Contains("_activeThumbnailBrowser.Show();", source);
Assert.Contains("_activeDidacticWindow.Show();", source);
Assert.Contains("ResetCompanionPresentationState();", source);
```

- [ ] **Step 2: Verify RED**

```powershell
dotnet test Tests\InteractiveWorldMap.Tests.csproj --filter "FullyQualifiedName~MainWindow_CoordinatesConfiguredContentAndCompanionVisibility"
```

Expected: source contract assertion fails.

- [ ] **Step 3: Centralize content-window creation**

Add two restore flags and:

```csharp
private bool _restoreThumbnailAfterPresentation;
private bool _restoreDidacticAfterPresentation;

private ContentSubwindow CreateContentSubwindow(Location location)
{
    var window = new ContentSubwindow
    {
        AssociatedLocation = location,
        Owner = this,
        MaximizedBackgroundOpacity =
            _visualConfig.MaximizedContentBackgroundOpacity
    };
    window.PresentationModeChanged += OnContentPresentationModeChanged;
    return window;
}
```

Use this factory in both current content-window creation paths.

- [ ] **Step 4: Implement companion visibility coordination**

Add the exact coordinator:

```csharp
private void OnContentPresentationModeChanged(
    object? sender,
    EventArgs e)
{
    if (sender is not ContentSubwindow window ||
        !ReferenceEquals(window, _activeSubwindow))
        return;

    if (window.IsPresentationMode)
    {
        _restoreThumbnailAfterPresentation =
            _activeThumbnailBrowser?.IsVisible == true;
        _restoreDidacticAfterPresentation =
            _activeDidacticWindow?.IsVisible == true;

        if (_restoreThumbnailAfterPresentation)
            _activeThumbnailBrowser!.Hide();
        if (_restoreDidacticAfterPresentation)
            _activeDidacticWindow!.Hide();
        return;
    }

    if (_restoreThumbnailAfterPresentation &&
        _activeThumbnailBrowser != null)
        _activeThumbnailBrowser.Show();

    if (_restoreDidacticAfterPresentation &&
        _activeDidacticWindow != null)
        _activeDidacticWindow.Show();

    ResetCompanionPresentationState();
}

private void ResetCompanionPresentationState()
{
    _restoreThumbnailAfterPresentation = false;
    _restoreDidacticAfterPresentation = false;
}
```

Call it from both `CloseActiveSubwindow` and `CloseActiveSubwindowAsync` after companion cleanup, preventing hidden closed windows from reappearing.

- [ ] **Step 5: Verify GREEN and commit**

```powershell
dotnet test Tests\InteractiveWorldMap.Tests.csproj --filter "FullyQualifiedName~ContentPresentationModeTests|FullyQualifiedName~SingleLocationZoomClickTests|FullyQualifiedName~ThumbnailBrowserWindowTests"
```

Expected: all selected tests pass.

```powershell
git add MainWindow.Content.partial.cs Tests/ContentPresentationModeTests.cs
git commit -m "feat: coordinate content presentation companions"
```

---

### Task 4: Manual Smoke, Bookkeeping, and Completion

**Files:**
- Modify: `docs/TO_DO.md`
- Modify: `docs/exec-plans/active/README.md`
- Move: this plan to `docs/exec-plans/completed/content-presentation-mode-plan.md`
- Modify: `CHANGELOG.md`

- [ ] **Step 1: Run the app and smoke the interaction**

Run `.\run-demo.bat`. Open content with thumbnails, translation, and didactic text where available. Verify click/tap enters a main-window-sized borderless black presentation; title and companions hide; Translate stays usable and does not restore; a second content click restores exact popup bounds/chrome and companions. Enter again and navigate Back; confirm no hidden window reappears.

- [ ] **Step 2: Run full verification**

```powershell
.\scripts\verify.ps1
```

Expected: build, tests, architecture, docs, taste, vulnerability, and startup checks pass.

- [ ] **Step 3: Narrow completed backlog scope**

Remove the active presentation bullet from `docs/TO_DO.md`, retaining only:

```markdown
- [ ] Main content images: support image zoom/pan within the content viewer for closer inspection.
```

- [ ] **Step 4: Record the shipped feature**

Under `[Unreleased]` → `Added`:

```markdown
- **Content presentation mode:** Clicking or tapping center content toggles a borderless main-window-sized view with proportional image scaling, configurable black excess-area opacity, an available Translate control, temporary hiding of thumbnail/didactic companions, and exact popup restoration.
```

- [ ] **Step 5: Archive and recheck documentation**

Move this plan to `docs/exec-plans/completed/`, remove its active table row, and add it under “Recently completed” with move date `2026-06-30`. Run:

```powershell
py -3 scripts\verify_doc_links.py
py -3 scripts\doc_gardening.py --check
git diff --check
```

Expected: all exit successfully.

- [ ] **Step 6: Commit completion bookkeeping**

```powershell
git add docs/TO_DO.md docs/exec-plans/active/README.md docs/exec-plans/completed/content-presentation-mode-plan.md CHANGELOG.md
git commit -m "docs: complete content presentation mode"
```

---

## Acceptance Criteria

- [ ] Completed click/tap toggles presentation mode; Translate clicks do not.
- [ ] The content window uses the main map window's current bounds.
- [ ] Presentation has no border, rounded corners, shadow, title, or outer padding.
- [ ] Images remain proportional and centered; excess area is configurable black, default opacity `1.0`.
- [ ] Thumbnail and didactic companions hide and restore only if previously visible.
- [ ] Exact popup bounds and styling restore.
- [ ] Close/back cannot resurrect hidden companions.
- [ ] Focused tests and `.\scripts\verify.ps1` pass.
- [ ] TO_DO retains only unimplemented zoom/pan.
- [ ] The completed plan is archived and `[Unreleased]` records the feature.
