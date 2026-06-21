---
status: active
owner: agent
started: 2026-06-21
requirements_ref: drawn-pin-model-separation
parent_program: composite-pins-program.md
---

# Drawn Pin Model Separation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Split the drawn pin path into explicit head-only, auto-stub, and manual-layout visual roles so edited drawn pins no longer depend on hiding a built-in vertical shaft.

**Architecture:** Keep the draggable canvas item as `LocationMarker`; only the marker content changes. Extract the drawn pin head/shaft composition behind small WPF controls and a factory so `MainWindow` applies placement semantics without knowing whether the visual has an internal shaft. Composite pins remain out of scope except for preserving fallback behavior.

**Tech Stack:** WPF / .NET 6 / C#, existing `PinMarkerConfig`, `LocationMarker`, `ExtensionLineRenderer`, `LayoutEditorController`, xUnit source and behavior tests.

---

## Current Problem

`Views/PinMarker.xaml` is a single complete visual: pin head plus a short vertical shaft. That is correct for an auto stub pin, but it is the wrong primitive for a manual-layout pin. Manual-layout drawn pins render their custom shaft through `ExtensionLineRenderer`, so the built-in vertical shaft must be hidden with `PinMarker.SetShaftVisible(false)`.

That hidden-shaft workaround has already caused fragile transition behavior:

- during zoom-out animation, normal placement can restore the built-in shaft before manual layout replay
- `ExtensionLineRenderer.AnchorExtendedMarker` knows too much about `PinMarker` internals
- future drawn head-color selection has no clean head-only visual to target
- tests must guard against shaft visibility state instead of asserting explicit visual roles

## Target Vocabulary

Use the terms from `AGENTS.md`:

| Term | Visual behavior |
|------|-----------------|
| Auto stub pin | Drawn head plus built-in short vertical shaft |
| Manual-layout pin | Drawn head only, anchored at the saved endpoint; external line is the shaft |
| Pin head | Circle/head visual that can be reused by auto stub and manual-layout pins |

## Non-goals

- Do not change composite pin behavior or `CompositePinMarker`.
- Do not add new product UI for pin color selection in this plan.
- Do not change manual-layout JSON schema unless a tiny optional head-color field is explicitly needed by tests; the drawn head should keep today's color behavior.
- Do not rework zoom animation beyond preserving existing manual-layout replay behavior.

## File Structure

| File | Responsibility |
|------|----------------|
| `Views/PinHead.xaml` / `Views/PinHead.xaml.cs` | New reusable drawn head control: color, rim, hover/click scale target, connection point |
| `Views/AutoStubPinMarker.xaml` / `Views/AutoStubPinMarker.xaml.cs` | New head-plus-stub control for auto stub pins |
| `Views/ManualLayoutPinMarker.xaml` / `Views/ManualLayoutPinMarker.xaml.cs` | New head-only control for manual-layout pins |
| `Views/PinMarker.xaml` / `Views/PinMarker.xaml.cs` | Temporary compatibility wrapper or deletion target after callers migrate |
| `Services/DrawnPinMarkerFactory.cs` | Creates drawn marker content by role without referencing `MainWindow` |
| `MainWindow.xaml.cs` | Use the factory when creating drawn pins and when applying/reverting manual layout |
| `MainWindow.LayoutEditor.partial.cs` | Stop calling `SetShaftVisible`; switch marker content role instead |
| `Views/ExtensionLineRenderer.cs` | Anchor manual-layout heads without hiding internal shafts |
| `Views/IExtensionLineRenderer.cs` | Update comments from hidden shaft to head-only manual-layout role |
| `Tests/PinMarkerRenderingTests.cs` | Update existing drawn pin rendering tests for split controls |
| `Tests/DrawnPinModelSeparationTests.cs` | New behavior/source guard tests for role separation |
| `docs/guides/VISUAL_CONFIG.md` | Document drawn mode roles and `PinMarkers` fields |
| `docs/TO_DO.md`, `CHANGELOG.md` | Track progress and user-visible change |

## Design Rules

1. `LocationMarker` remains the WPF element on the canvas and keeps click/drag handlers.
2. Drawn manual-layout pins must not contain a built-in shaft in their visual tree.
3. Auto stub pins own their short shaft and do not require an external line.
4. Manual-layout pins use `ExtensionLineRenderer` for the external shaft and `ManualLayoutPinMarker` for the endpoint head.
5. Compatibility methods such as `SetShaftVisible` can exist only during migration and must be removed or made unused before completion.
6. `Services/` may create WPF controls only if the repo accepts view factories there; otherwise put `DrawnPinMarkerFactory` under `Views/` and keep service layers clean. Check `Tests/Architecture/LayerDependencyTests.cs` before choosing.

---

## Task 1: Add Head-Only Regression Coverage

**Files:**
- Create: `Tests/DrawnPinModelSeparationTests.cs`
- Modify: `Tests/PinMarkerRenderingTests.cs`

- [ ] **Step 1: Write failing source guard tests**

Create `Tests/DrawnPinModelSeparationTests.cs`:

```csharp
using System;
using System.IO;
using Xunit;

namespace InteractiveWorldMap.Tests;

public class DrawnPinModelSeparationTests
{
    private static string RepoRoot =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));

    [Fact]
    public void ManualLayoutPinMarker_HasNoShaftVisual()
    {
        var xaml = File.ReadAllText(Path.Combine(RepoRoot, "Views", "ManualLayoutPinMarker.xaml"));

        Assert.DoesNotContain("PinShaft", xaml);
        Assert.DoesNotContain("ShaftHost", xaml);
        Assert.Contains("PinHead", xaml);
    }

    [Fact]
    public void AutoStubPinMarker_OwnsShortShaftVisual()
    {
        var xaml = File.ReadAllText(Path.Combine(RepoRoot, "Views", "AutoStubPinMarker.xaml"));

        Assert.Contains("PinShaft", xaml);
        Assert.Contains("ShaftHost", xaml);
        Assert.Contains("PinHead", xaml);
    }

    [Fact]
    public void ExtensionLineRenderer_DoesNotHideBuiltInPinShaft()
    {
        var source = File.ReadAllText(Path.Combine(RepoRoot, "Views", "ExtensionLineRenderer.cs"));

        Assert.DoesNotContain("SetShaftVisible(false)", source);
        Assert.Contains("ManualLayoutPinMarker", source);
    }
}
```

- [ ] **Step 2: Run tests to verify red**

Run:

```powershell
dotnet test Tests\InteractiveWorldMap.Tests.csproj --filter FullyQualifiedName~DrawnPinModelSeparationTests --no-restore
```

Expected: fail because `ManualLayoutPinMarker.xaml` and `AutoStubPinMarker.xaml` do not exist and `ExtensionLineRenderer` still hides `PinMarker` shafts.

- [ ] **Step 3: Update existing rendering tests**

Modify `Tests/PinMarkerRenderingTests.cs` so old `PinMarker` assertions move to `AutoStubPinMarker` where appropriate:

```csharp
var xaml = File.ReadAllText(Path.Combine(RepoRoot, "Views", "AutoStubPinMarker.xaml"));
Assert.Contains("PinShaftOutline", xaml);
Assert.Contains("PinShaft", xaml);
```

- [ ] **Step 4: Re-run red tests**

Run:

```powershell
dotnet test Tests\InteractiveWorldMap.Tests.csproj --filter "FullyQualifiedName~DrawnPinModelSeparationTests|FullyQualifiedName~PinMarkerRenderingTests" --no-restore
```

Expected: fail only on missing new controls / old implementation.

---

## Task 2: Extract Reusable Pin Head Control

**Files:**
- Create: `Views/PinHead.xaml`
- Create: `Views/PinHead.xaml.cs`
- Modify: `InteractiveWorldMap.csproj` only if SDK auto-glob does not include the new XAML
- Test: `Tests/DrawnPinModelSeparationTests.cs`

- [ ] **Step 1: Create `PinHead.xaml`**

```xml
<UserControl x:Class="InteractiveWorldMap.Views.PinHead"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             SnapsToDevicePixels="False">
    <Grid x:Name="Root">
        <Ellipse x:Name="PinBall"
                 Width="14"
                 Height="14"
                 Stroke="Black"
                 StrokeThickness="1.5"/>
    </Grid>
</UserControl>
```

- [ ] **Step 2: Create `PinHead.xaml.cs`**

```csharp
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using InteractiveWorldMap.Models;

namespace InteractiveWorldMap.Views
{
    public partial class PinHead : UserControl
    {
        public static readonly DependencyProperty PinColorProperty =
            DependencyProperty.Register(nameof(PinColor), typeof(Color), typeof(PinHead),
                new PropertyMetadata(Colors.Red, OnPinColorChanged));

        public Color PinColor
        {
            get => (Color)GetValue(PinColorProperty);
            set => SetValue(PinColorProperty, value);
        }

        public PinHead()
            : this(new VisualConfig())
        {
        }

        public PinHead(VisualConfig visualConfig)
        {
            InitializeComponent();
            ApplyConfig(visualConfig.PinMarkers);
        }

        public void ApplyConfig(PinMarkerConfig config)
        {
            var ballSize = Math.Max(config.BallSize, 6.0);
            var ballOutline = Math.Max(config.BallOutlineThickness, 0.0);

            PinBall.Width = ballSize;
            PinBall.Height = ballSize;
            PinBall.StrokeThickness = ballOutline;

            if (TryParseColor(config.BallOutlineColor, out var outline))
                PinBall.Stroke = new SolidColorBrush(outline);

            Width = ballSize + (2 * ballOutline);
            Height = ballSize + (2 * ballOutline);
            ApplyBallFill(PinColor);
        }

        public Point GetConnectionPoint() => new(Width / 2.0, PinBall.Height / 2.0);

        private static void OnPinColorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is PinHead head && e.NewValue is Color color)
                head.ApplyBallFill(color);
        }

        private void ApplyBallFill(Color color)
        {
            PinBall.Fill = new RadialGradientBrush
            {
                GradientOrigin = new Point(0.35, 0.35),
                Center = new Point(0.35, 0.35),
                RadiusX = 0.85,
                RadiusY = 0.85,
                GradientStops = new GradientStopCollection
                {
                    new(Colors.White, 0.0),
                    new(Lighten(color, 1.15), 0.35),
                    new(color, 1.0)
                }
            };
        }

        private static bool TryParseColor(string? value, out Color color)
        {
            color = default;
            return !string.IsNullOrWhiteSpace(value) &&
                   ColorConverter.ConvertFromString(value) is Color parsed &&
                   (color = parsed).A > 0;
        }

        private static Color Lighten(Color color, double factor)
        {
            factor = Math.Max(factor, 1.0);
            return Color.FromRgb(
                (byte)Math.Min(255, color.R * factor),
                (byte)Math.Min(255, color.G * factor),
                (byte)Math.Min(255, color.B * factor));
        }
    }
}
```

- [ ] **Step 3: Run focused build**

Run:

```powershell
dotnet build InteractiveWorldMap.sln
```

Expected: build succeeds or reports missing XAML compile entries. If compile entries are needed, add them consistently with existing WPF files.

---

## Task 3: Add Explicit Auto Stub and Manual Layout Controls

**Files:**
- Create: `Views/AutoStubPinMarker.xaml`
- Create: `Views/AutoStubPinMarker.xaml.cs`
- Create: `Views/ManualLayoutPinMarker.xaml`
- Create: `Views/ManualLayoutPinMarker.xaml.cs`
- Modify: `Views/PinMarker.xaml.cs`
- Test: `Tests/DrawnPinModelSeparationTests.cs`, `Tests/PinMarkerRenderingTests.cs`

- [ ] **Step 1: Create `AutoStubPinMarker.xaml`**

```xml
<UserControl x:Class="InteractiveWorldMap.Views.AutoStubPinMarker"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:views="clr-namespace:InteractiveWorldMap.Views"
             SnapsToDevicePixels="False"
             UseLayoutRounding="False">
    <Grid x:Name="Root">
        <Grid x:Name="ShaftHost"
              HorizontalAlignment="Center"
              VerticalAlignment="Top">
            <Rectangle x:Name="PinShaftOutline"
                       RadiusX="2"
                       RadiusY="2"/>
            <Rectangle x:Name="PinShaft"
                       RadiusX="1.5"
                       RadiusY="1.5"
                       HorizontalAlignment="Center"/>
        </Grid>
        <views:PinHead x:Name="PinHead"
                       HorizontalAlignment="Center"
                       VerticalAlignment="Top"/>
    </Grid>
</UserControl>
```

- [ ] **Step 2: Create `AutoStubPinMarker.xaml.cs`**

```csharp
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using InteractiveWorldMap.Models;

namespace InteractiveWorldMap.Views
{
    public partial class AutoStubPinMarker : UserControl
    {
        public AutoStubPinMarker()
            : this(new VisualConfig())
        {
        }

        public AutoStubPinMarker(VisualConfig visualConfig)
        {
            InitializeComponent();
            PinHead.PinColor = PinMarker.GetRandomPinColor();
            ApplyConfig(visualConfig.PinMarkers);
        }

        public void ApplyConfig(PinMarkerConfig config)
        {
            PinHead.ApplyConfig(config);

            var shaftWidth = Math.Max(config.ShaftWidth, 2.0);
            var shaftLength = Math.Max(config.ShaftLength, 12.0);
            var shaftOutline = Math.Max(config.ShaftOutlineThickness, 0.0);

            PinShaft.Width = shaftWidth;
            PinShaft.Height = shaftLength;
            PinShaftOutline.Width = shaftWidth + (2 * shaftOutline);
            PinShaftOutline.Height = shaftLength;
            ShaftHost.Margin = new Thickness(0, PinHead.GetConnectionPoint().Y, 0, 0);

            if (TryParseColor(config.ShaftColor, out var shaftColor))
                PinShaft.Fill = new SolidColorBrush(shaftColor);

            if (TryParseColor(config.ShaftOutlineColor, out var outlineColor))
                PinShaftOutline.Fill = new SolidColorBrush(outlineColor);

            Width = Math.Max(PinHead.Width, PinShaftOutline.Width);
            Height = PinHead.GetConnectionPoint().Y + shaftLength;
        }

        public Point GetConnectionPoint() => PinHead.GetConnectionPoint();

        private static bool TryParseColor(string? value, out Color color)
        {
            color = default;
            return !string.IsNullOrWhiteSpace(value) &&
                   ColorConverter.ConvertFromString(value) is Color parsed &&
                   (color = parsed).A > 0;
        }
    }
}
```

- [ ] **Step 3: Create `ManualLayoutPinMarker.xaml`**

```xml
<UserControl x:Class="InteractiveWorldMap.Views.ManualLayoutPinMarker"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:views="clr-namespace:InteractiveWorldMap.Views"
             SnapsToDevicePixels="False"
             UseLayoutRounding="False">
    <views:PinHead x:Name="PinHead"/>
</UserControl>
```

- [ ] **Step 4: Create `ManualLayoutPinMarker.xaml.cs`**

```csharp
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using InteractiveWorldMap.Models;

namespace InteractiveWorldMap.Views
{
    public partial class ManualLayoutPinMarker : UserControl
    {
        public ManualLayoutPinMarker()
            : this(new VisualConfig())
        {
        }

        public ManualLayoutPinMarker(VisualConfig visualConfig)
        {
            InitializeComponent();
            PinHead.PinColor = PinMarker.GetRandomPinColor();
            PinHead.ApplyConfig(visualConfig.PinMarkers);
            Width = PinHead.Width;
            Height = PinHead.Height;
        }

        public void SetPinColor(Color color)
        {
            PinHead.PinColor = color;
        }

        public Point GetConnectionPoint() => PinHead.GetConnectionPoint();
    }
}
```

- [ ] **Step 5: Keep `PinMarker` as compatibility wrapper**

Modify `Views/PinMarker.xaml.cs` only enough to preserve public methods used by callers. During this task, it may wrap `AutoStubPinMarker` internally or remain as-is. The completion condition is that new code paths use `AutoStubPinMarker` / `ManualLayoutPinMarker`, not that `PinMarker` is deleted immediately.

- [ ] **Step 6: Run focused tests**

Run:

```powershell
dotnet test Tests\InteractiveWorldMap.Tests.csproj --filter "FullyQualifiedName~DrawnPinModelSeparationTests|FullyQualifiedName~PinMarkerRenderingTests" --no-restore
```

Expected: tests pass after the controls exist and source guards are satisfied.

---

## Task 4: Route Drawn Marker Creation Through Explicit Roles

**Files:**
- Create: `Views/DrawnPinMarkerFactory.cs`
- Modify: `MainWindow.xaml.cs`
- Modify: `MainWindow.LayoutEditor.partial.cs`
- Test: `Tests/DrawnPinModelSeparationTests.cs`

- [ ] **Step 1: Create factory**

Create `Views/DrawnPinMarkerFactory.cs`:

```csharp
using System.Windows.Controls;
using InteractiveWorldMap.Models;

namespace InteractiveWorldMap.Views
{
    public enum DrawnPinRole
    {
        AutoStub,
        ManualLayout
    }

    public sealed class DrawnPinMarkerFactory
    {
        private readonly VisualConfig _visualConfig;

        public DrawnPinMarkerFactory(VisualConfig visualConfig)
        {
            _visualConfig = visualConfig;
        }

        public UserControl Create(DrawnPinRole role)
        {
            return role switch
            {
                DrawnPinRole.AutoStub => new AutoStubPinMarker(_visualConfig),
                DrawnPinRole.ManualLayout => new ManualLayoutPinMarker(_visualConfig),
                _ => new AutoStubPinMarker(_visualConfig)
            };
        }
    }
}
```

- [ ] **Step 2: Add factory field to `MainWindow`**

Modify `MainWindow.xaml.cs`:

```csharp
private DrawnPinMarkerFactory _drawnPinFactory = null!;
```

Initialize it after `_visualConfig` loads:

```csharp
_drawnPinFactory = new DrawnPinMarkerFactory(_visualConfig);
```

- [ ] **Step 3: Use auto stub role in marker creation**

In `CreatePinMarker(Location location)`, replace direct `new PinMarker(_visualConfig)` creation with:

```csharp
marker = new LocationMarker(_visualConfig)
{
    Location = location,
    Content = _drawnPinFactory.Create(DrawnPinRole.AutoStub)
};
```

Preserve existing `marker.Width`, `marker.Height`, `Location`, click handlers, base visual capture, and logging.

- [ ] **Step 4: Run build**

Run:

```powershell
dotnet build InteractiveWorldMap.sln
```

Expected: build succeeds.

---

## Task 5: Apply Manual Layout With Head-Only Drawn Pins

**Files:**
- Modify: `MainWindow.LayoutEditor.partial.cs`
- Modify: `Views/ExtensionLineRenderer.cs`
- Modify: `Views/IExtensionLineRenderer.cs`
- Test: `Tests/DrawnPinModelSeparationTests.cs`, `Tests/LayoutEditorControllerTests.cs`, `Tests/ManualLayoutZoomAnimationTests.cs`

- [ ] **Step 1: Add helper to switch drawn visual role**

In `MainWindow.LayoutEditor.partial.cs`, add:

```csharp
private void SetDrawnPinRole(LocationMarker marker, DrawnPinRole role)
{
    if (!_visualConfig.UsePinMarkers || CanUseCompositePins())
        return;

    marker.Content = _drawnPinFactory.Create(role);
}
```

- [ ] **Step 2: Use manual role before anchoring manual-layout line**

In `ApplyManualLayout(ManualLayout layout)`, before adding an extension line for `instruction.RequiresExtensionLine`, call:

```csharp
SetDrawnPinRole(marker, DrawnPinRole.ManualLayout);
```

Then keep:

```csharp
_extensionLineRenderer.AddLine(marker, instruction.OriginalScreen, instruction.ExtendedScreen);
_extensionLineRenderer.AnchorExtendedMarker(marker, instruction.ExtendedScreen);
```

- [ ] **Step 3: Use auto-stub role for non-extended drawn fallback**

In the `else` branch for `instruction.RequiresExtensionLine == false`, call:

```csharp
SetDrawnPinRole(marker, DrawnPinRole.AutoStub);
```

Remove any `PinMarker.SetShaftVisible(true)` call from that branch once auto-stub role is active.

- [ ] **Step 4: Update `ExtensionLineRenderer.AnchorExtendedMarker`**

Replace the `PinMarker` shaft-hiding branch with a head-only branch:

```csharp
if (marker.Content is ManualLayoutPinMarker manualPin)
{
    var connection = manualPin.GetConnectionPoint();
    Canvas.SetLeft(marker, extendedScreenPos.X - connection.X);
    Canvas.SetTop(marker, extendedScreenPos.Y - connection.Y);
    return;
}
```

Keep the generic center-anchored fallback for other marker types.

- [ ] **Step 5: Update interface comments**

Modify `Views/IExtensionLineRenderer.cs` so comments say manual-layout drawn pins use a head-only visual and the extension line is the shaft. Remove language about hiding a pin's own shaft.

- [ ] **Step 6: Run focused tests**

Run:

```powershell
dotnet test Tests\InteractiveWorldMap.Tests.csproj --filter "FullyQualifiedName~DrawnPinModelSeparationTests|FullyQualifiedName~LayoutEditorControllerTests|FullyQualifiedName~ManualLayoutZoomAnimationTests" --no-restore
```

Expected: all pass.

---

## Task 6: Remove Hidden-Shaft Compatibility From Active Paths

**Files:**
- Modify: `Views/PinMarker.xaml.cs`
- Modify: `MainWindow.xaml.cs`
- Modify: `Views/ExtensionLineRenderer.cs`
- Test: `Tests/DrawnPinModelSeparationTests.cs`, `Tests/PinMarkerRenderingTests.cs`

- [ ] **Step 1: Grep current shaft visibility usage**

Run:

```powershell
rg -n "SetShaftVisible|ShaftHost.Visibility|marker.Content is PinMarker" MainWindow*.cs Views Tests
```

Expected before edits: hits in `PinMarker`, `MainWindow`, and/or `ExtensionLineRenderer`.

- [ ] **Step 2: Remove active calls**

Remove calls that switch shaft visibility as part of placement:

```csharp
drawnPin.SetShaftVisible(true);
pin.SetShaftVisible(false);
```

Replace them with explicit role changes through `SetDrawnPinRole(...)` or with no-op behavior when the marker is composite.

- [ ] **Step 3: Decide whether `PinMarker` remains**

If no active code creates `PinMarker`, either:

1. Delete `Views/PinMarker.xaml` and `.xaml.cs`, or
2. Keep it as a compatibility alias with an `[Obsolete]` comment and no active callers.

Recommended: keep for one iteration if deletion creates broad churn; add a TODO in the plan checklist to delete it after manual smoke.

- [ ] **Step 4: Run source guard tests**

Run:

```powershell
dotnet test Tests\InteractiveWorldMap.Tests.csproj --filter FullyQualifiedName~DrawnPinModelSeparationTests --no-restore
```

Expected: pass, proving manual-layout drawn pins no longer rely on hidden built-in shafts.

---

## Task 7: Documentation And Plan State

**Files:**
- Modify: `docs/guides/VISUAL_CONFIG.md`
- Modify: `docs/TO_DO.md`
- Modify: `CHANGELOG.md`
- Modify: `AGENTS.md` if terminology needs tightening

- [ ] **Step 1: Update visual config guide**

Add this under `## PinMarkers (drawn fallback)`:

```markdown
Drawn mode has two visual roles:

- Auto stub pins use a drawn head plus the configured short vertical shaft.
- Manual-layout pins use the same drawn head without an internal shaft; the saved/manual extension line is the shaft.

This keeps edited drawn pins from drawing a duplicate vertical shaft under the head.
```

- [ ] **Step 2: Update `docs/TO_DO.md`**

When implementation and verification are complete, change:

```markdown
- [ ] Separate drawn pin model into head-only, auto-stub, and manual-layout pin components ...
```

to:

```markdown
- [x] Separate drawn pin model into head-only, auto-stub, and manual-layout pin components ...
```

- [ ] **Step 3: Update changelog**

Add under `[Unreleased]`:

```markdown
- **Drawn pin model separation:** Drawn manual-layout pins now use a head-only visual while auto stubs keep their built-in short shaft, removing placement-time shaft hiding from the active drawn path.
```

- [ ] **Step 4: Run doc checks**

Run:

```powershell
py -3 scripts\verify_doc_links.py
py -3 scripts\doc_gardening.py
```

Expected: both pass.

---

## Task 8: Final Verification

**Files:**
- No new files unless verification exposes a focused fix.

- [ ] **Step 1: Run focused test suite**

Run:

```powershell
dotnet test Tests\InteractiveWorldMap.Tests.csproj --filter "FullyQualifiedName~DrawnPinModelSeparationTests|FullyQualifiedName~PinMarkerRenderingTests|FullyQualifiedName~ManualLayoutZoomAnimationTests|FullyQualifiedName~LayoutEditorControllerTests" --no-restore
```

Expected: all selected tests pass.

- [ ] **Step 2: Run full repo verification**

Run:

```powershell
.\scripts\verify.ps1
```

Expected: restore, vulnerability check, build, all tests, doc links, taste checks, and headless startup validation pass.

- [ ] **Step 3: Manual smoke**

Run app on Windows:

```powershell
dotnet run --project InteractiveWorldMap.csproj
```

Smoke:

1. Set `PinParts.UseCompositeRendering = false`.
2. Start at full map.
3. Confirm standalone locations render auto stub pins with short vertical shafts.
4. Enter full-map Edit Layout.
5. Drag one standalone pin to create a custom angle/length.
6. Save and exit.
7. Confirm the edited pin shows one custom shaft, not a custom shaft plus a vertical stub.
8. Zoom into and back out of that location.
9. Confirm the edited pin remains a manual-layout pin during and after zoom-out.

---

## Acceptance Criteria

- [ ] Auto stub pins still show a short vertical shaft in drawn mode.
- [ ] Manual-layout drawn pins show a head plus exactly one external shaft.
- [ ] No active placement path calls `SetShaftVisible(false)` to make manual-layout pins look correct.
- [ ] Existing composite pin behavior is unchanged.
- [ ] Full-map edit/save/exit and zoom-out replay still preserve manual-layout drawn pins.
- [ ] `.\scripts\verify.ps1` passes.

## Risks

| Risk | Mitigation |
|------|------------|
| WPF XAML control split causes sizing drift | Keep `GetConnectionPoint()` and `PinMarkerConfig` math identical to current `PinMarker` |
| Manual-layout pin loses random color on role switch | Carry color from old content where possible, or accept current random behavior only if visual smoke confirms no jarring change |
| Composite fallback path regresses | Guard `SetDrawnPinRole` with `!CanUseCompositePins()` and run composite persistence tests |
| MainWindow grows again | Keep role creation in `DrawnPinMarkerFactory`; do not inline XAML/control construction in placement loops |
