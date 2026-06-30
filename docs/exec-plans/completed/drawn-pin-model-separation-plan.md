---
status: completed
owner: agent
started: 2026-06-21
completed: 2026-06-28
requirements_ref: drawn-pin-model-separation
parent_program: composite-pins-program.md
---

# Drawn Pin Model Separation Implementation Plan

> Completed 2026-06-28. Full Windows verification passed with 481 tests; UI smoke confirmed tip-anchored auto stubs and head-only first-frame drag behavior.

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [x]`) syntax for tracking.

**Goal:** Split the drawn pin path into explicit head-only, auto-stub, and manual-layout visual roles, keep the shaft tip anchored at map locations, and prevent edited pins from dragging or retaining a hidden built-in shaft.

**Architecture:** Keep the draggable canvas item as `LocationMarker`; only the marker content changes. Extract the drawn pin head/shaft composition behind small WPF controls and a factory, then centralize role-specific anchoring so auto stubs map their shaft tip to the location and manual-layout heads map their connection point to the external-shaft endpoint. Classify extension-line use from the final projected endpoints, not stale saved screen coordinates. Composite pins remain out of scope except for preserving fallback behavior.

**Tech Stack:** WPF / .NET 6 / C#, existing `PinMarkerConfig`, `LocationMarker`, `ExtensionLineRenderer`, `LayoutEditorController`, xUnit source and behavior tests.

---

## Current Problem

`Views/PinMarker.xaml` is a single complete visual: pin head plus a short vertical shaft. That is correct for an auto stub pin, but it is the wrong primitive for a manual-layout pin. Manual-layout drawn pins render their custom shaft through `ExtensionLineRenderer`, so the built-in vertical shaft must be hidden with `PinMarker.SetShaftVisible(false)`.

That hidden-shaft workaround has already caused fragile transition behavior:

- during zoom-out animation, normal placement can restore the built-in shaft before manual layout replay
- `ExtensionLineRenderer.AnchorExtendedMarker` knows too much about `PinMarker` internals
- future drawn head-color selection has no clean head-only visual to target
- tests must guard against shaft visibility state instead of asserting explicit visual roles

The 2026-06-28 investigation also confirmed two concrete failure modes:

- The active per-user layout store contained 24 `AutoSeed` records with
  `OriginalPosition == ExtendedPosition` and `LineLength == 0`. These correctly mean
  "use an auto stub," but `ApplyManualLayout` centered the complete `PinMarker` bounding box
  on that point. The head therefore landed at the seed/location instead of the shaft tip.
- Dragging starts from that same complete `PinMarker`; the active path only hides its shaft
  later through mutable `ShaftHost.Visibility`. Because the base-visual cache stores the same
  control instance rather than a role, replay, animation, and drag can observe or restore the
  wrong shaft state.

These are not seed-file corruption problems. Zero-length seed entries are valid and must replay
as auto stubs with their shaft tips at the map coordinates.

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
| `Views/DrawnPinColorPalette.cs` | Shared random-color palette formerly owned by legacy `PinMarker` |
| `Views/PinMarker.xaml` / `Views/PinMarker.xaml.cs` | Delete after all active callers migrate |
| `Views/DrawnPinMarkerFactory.cs` | Creates drawn marker content by role while preserving the existing head color |
| `Utilities/ManualLayoutPlacementPolicy.cs` | Classifies extension-line use from final projected tip/head endpoints |
| `MainWindow.xaml.cs` | Own and initialize the drawn-pin factory |
| `MainWindow.DrawnPins.partial.cs` | Role switching, color preservation, and role-specific anchoring helpers |
| `MainWindow.CompositePins.partial.cs` | Create and auto-place explicit auto-stub drawn pins |
| `MainWindow.LayoutEditor.partial.cs` | Stop calling `SetShaftVisible`; switch marker content role instead |
| `MainWindow.LayoutEditorDrag.partial.cs` | Convert auto stubs to the manual-layout role at drag start before movement |
| `MainWindow.MarkerPlacement.partial.cs` / `MainWindow.Navigation.partial.cs` | Use explicit auto-stub tip anchors during normal placement and zoom |
| `MainWindow.TipCap.partial.cs` | Build built-in cap geometry from `AutoStubPinMarker`; extension caps remain line-based |
| `Views/ExtensionLineRenderer.cs` | Anchor manual-layout heads without hiding internal shafts |
| `Views/IExtensionLineRenderer.cs` | Update comments from hidden shaft to head-only manual-layout role |
| `Tests/PinMarkerRenderingTests.cs` | Update existing drawn pin rendering tests for split controls |
| `Tests/DrawnPinModelSeparationTests.cs` | New behavior/source guard tests for role separation |
| `Tests/ManualLayoutPlacementPolicyTests.cs` | Final-endpoint extension classification, including zero-length seeds |
| `Tests/CompositePinPlanCacheTests.cs` | Verify replay classification uses projected endpoints |
| `docs/guides/VISUAL_CONFIG.md` | Document drawn mode roles and `PinMarkers` fields |
| `docs/TO_DO.md`, `CHANGELOG.md` | Track progress and user-visible change |

## Design Rules

1. `LocationMarker` remains the WPF element on the canvas and keeps click/drag handlers.
2. Drawn manual-layout pins must not contain a built-in shaft in their visual tree.
3. Auto stub pins own their short shaft and do not require an external line.
4. Manual-layout pins use `ExtensionLineRenderer` for the external shaft and `ManualLayoutPinMarker` for the endpoint head.
5. Compatibility methods such as `SetShaftVisible` can exist only during migration and must be removed or made unused before completion.
6. `DrawnPinMarkerFactory` lives under `Views/`; `Services/` must not reference WPF controls or the `Views` layer.
7. Auto-stub placement always maps `GetShaftTipPoint()` to the map location. Never center an auto-stub bounding box on a location.
8. Manual-layout placement always maps `GetConnectionPoint()` to the external shaft's head endpoint.
9. `RequiresExtensionLine` is derived after source-space endpoints are projected for the current view. Saved screen-space distance is not authoritative.
10. Role changes preserve the current `PinColor`; creating a new control must not randomly recolor a pin.
11. Drawn drag converts the marker to the head-only role before the first draggable frame is rendered.

---

## Task 1: Add Head-Only Regression Coverage

**Files:**
- Create: `Tests/DrawnPinModelSeparationTests.cs`
- Modify: `Tests/PinMarkerRenderingTests.cs`

- [x] **Step 1: Write failing source guard tests**

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

- [x] **Step 2: Run tests to verify red**

Run:

```powershell
dotnet test Tests\InteractiveWorldMap.Tests.csproj --filter FullyQualifiedName~DrawnPinModelSeparationTests --no-restore
```

Expected: fail because `ManualLayoutPinMarker.xaml` and `AutoStubPinMarker.xaml` do not exist and `ExtensionLineRenderer` still hides `PinMarker` shafts.

- [x] **Step 3: Update existing rendering tests**

Modify `Tests/PinMarkerRenderingTests.cs` so old `PinMarker` assertions move to `AutoStubPinMarker` where appropriate:

```csharp
var xaml = File.ReadAllText(Path.Combine(RepoRoot, "Views", "AutoStubPinMarker.xaml"));
Assert.Contains("PinShaftOutline", xaml);
Assert.Contains("PinShaft", xaml);
```

- [x] **Step 4: Re-run red tests**

Run:

```powershell
dotnet test Tests\InteractiveWorldMap.Tests.csproj --filter "FullyQualifiedName~DrawnPinModelSeparationTests|FullyQualifiedName~PinMarkerRenderingTests" --no-restore
```

Expected: fail only on missing new controls / old implementation.

---

## Task 2: Classify Roles From Final Projected Endpoints

**Files:**
- Create: `Utilities/ManualLayoutPlacementPolicy.cs`
- Create: `Tests/ManualLayoutPlacementPolicyTests.cs`
- Modify: `Services/LayoutEditorController.cs`
- Modify: `Services/CompositePinApplicationService.cs`
- Modify: `Tests/CompositePinPlanCacheTests.cs`

- [x] **Step 1: Write failing policy tests**

Create `Tests/ManualLayoutPlacementPolicyTests.cs`:

```csharp
using System.Windows;
using InteractiveWorldMap.Utilities;
using Xunit;

namespace InteractiveWorldMap.Tests;

public class ManualLayoutPlacementPolicyTests
{
    [Fact]
    public void RequiresExtensionLine_ZeroLengthSeed_ReturnsFalse()
    {
        var location = new Point(640, 440);

        Assert.False(ManualLayoutPlacementPolicy.RequiresExtensionLine(location, location));
    }

    [Fact]
    public void RequiresExtensionLine_ProjectedHeadBeyondThreshold_ReturnsTrue()
    {
        Assert.True(ManualLayoutPlacementPolicy.RequiresExtensionLine(
            new Point(640, 440),
            new Point(640, 464)));
    }
}
```

- [x] **Step 2: Write failing final-projection regression test**

Add to `Tests/CompositePinPlanCacheTests.cs`:

```csharp
[Fact]
public void BuildApplyInstructions_ReclassifiesUsingFinalProjectedEndpoints()
{
    var cache = new CompositePinPlanCache(new MockLogger());
    var planning = new CompositePinPlanningService(
        new PinPartPlacementCalculator(),
        new CompositePinRenderPlanBuilder());
    var service = new CompositePinApplicationService(cache, planning);
    var layout = new ManualLayout { GroupKey = "g1", VariantId = "seed-default" };

    var applications = new List<LayoutEditorController.LayoutMarkerApplication>
    {
        // Saved screen points say "extended", but angle/length reconstruct a zero-length seed.
        new("LocA", new Point(100, 100), new Point(150, 100), true)
        {
            Angle = 0,
            LineLength = 0
        },
        // Saved screen points say "auto stub", but final replay produces an extension.
        new("LocB", new Point(200, 200), new Point(200, 200), false)
        {
            Angle = 90,
            LineLength = 24
        }
    };

    var result = service.BuildApplyInstructions(
        layout,
        applications,
        new Dictionary<string, (double PixelX, double PixelY)>(),
        null,
        0,
        0,
        new PinPartConfig(),
        "group-key",
        Path.Combine(Path.GetTempPath(), "missing_geometry.json"),
        false);

    Assert.Collection(
        result.Instructions,
        first =>
        {
            Assert.Equal("LocA", first.LocationName);
            Assert.False(first.RequiresExtensionLine);
        },
        second =>
        {
            Assert.Equal("LocB", second.LocationName);
            Assert.True(second.RequiresExtensionLine);
        });
}
```

- [x] **Step 3: Run tests to verify red**

Run:

```powershell
dotnet test Tests\InteractiveWorldMap.Tests.csproj --filter "FullyQualifiedName~ManualLayoutPlacementPolicyTests|FullyQualifiedName~BuildApplyInstructions_ReclassifiesUsingFinalProjectedEndpoints" --no-restore
```

Expected: fail because `ManualLayoutPlacementPolicy` does not exist and replay still copies
`application.RequiresExtensionLine`.

- [x] **Step 4: Add the shared policy**

Create `Utilities/ManualLayoutPlacementPolicy.cs`:

```csharp
using System;
using System.Windows;

namespace InteractiveWorldMap.Utilities;

public static class ManualLayoutPlacementPolicy
{
    public const double ExtensionLineThreshold = 5.0;

    public static bool RequiresExtensionLine(Point tip, Point head)
    {
        var dx = head.X - tip.X;
        var dy = head.Y - tip.Y;
        return Math.Sqrt((dx * dx) + (dy * dy)) > ExtensionLineThreshold;
    }
}
```

- [x] **Step 5: Use the policy before and after projection**

In `Services/LayoutEditorController.cs`, remove the private
`ExtensionLineThreshold` constant and replace the local distance calculation plus comparison with:

```csharp
bool requiresExtensionLine = ManualLayoutPlacementPolicy.RequiresExtensionLine(
    layoutMarker.OriginalPosition,
    layoutMarker.ExtendedPosition);
```

Pass `requiresExtensionLine` into `LayoutMarkerApplication`.

In `Services/CompositePinApplicationService.cs`, classify only after `originalPos` and
`extendedPos` are final:

```csharp
var requiresExtensionLine =
    ManualLayoutPlacementPolicy.RequiresExtensionLine(originalPos, extendedPos);

instructions.Add(new ManualLayoutApplyInstruction(
    application.LocationName,
    originalPos,
    extendedPos,
    requiresExtensionLine,
    application.PairId,
    application.HeadSourcePath,
    cachedPlan));
```

- [x] **Step 6: Run focused tests**

Run:

```powershell
dotnet test Tests\InteractiveWorldMap.Tests.csproj --filter "FullyQualifiedName~ManualLayoutPlacementPolicyTests|FullyQualifiedName~LayoutEditorControllerTests|FullyQualifiedName~CompositePinPlanCacheTests" --no-restore
```

Expected: all selected tests pass.

---

## Task 3: Extract Reusable Pin Head Control

**Files:**
- Create: `Views/PinHead.xaml`
- Create: `Views/PinHead.xaml.cs`
- Modify: `InteractiveWorldMap.csproj` only if SDK auto-glob does not include the new XAML
- Test: `Tests/DrawnPinModelSeparationTests.cs`

- [x] **Step 1: Create `PinHead.xaml`**

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
                 StrokeThickness="1.5">
            <Ellipse.Effect>
                <DropShadowEffect Color="Black"
                                  Direction="315"
                                  ShadowDepth="1.5"
                                  BlurRadius="2.5"
                                  Opacity="0.55"/>
            </Ellipse.Effect>
        </Ellipse>
    </Grid>
</UserControl>
```

- [x] **Step 2: Create `PinHead.xaml.cs`**

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

        public Point GetConnectionPoint() => new(Width / 2.0, Height / 2.0);

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

- [x] **Step 3: Run focused build**

Run:

```powershell
dotnet build InteractiveWorldMap.sln
```

Expected: build succeeds or reports missing XAML compile entries. If compile entries are needed, add them consistently with existing WPF files.

---

## Task 4: Add Explicit Auto Stub and Manual Layout Controls

**Files:**
- Create: `Views/AutoStubPinMarker.xaml`
- Create: `Views/AutoStubPinMarker.xaml.cs`
- Create: `Views/ManualLayoutPinMarker.xaml`
- Create: `Views/ManualLayoutPinMarker.xaml.cs`
- Create: `Views/DrawnPinColorPalette.cs`
- Test: `Tests/DrawnPinModelSeparationTests.cs`, `Tests/PinMarkerRenderingTests.cs`

- [x] **Step 1: Create `AutoStubPinMarker.xaml`**

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

- [x] **Step 2: Create `AutoStubPinMarker.xaml.cs`**

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
            PinHead.PinColor = DrawnPinColorPalette.GetRandom();
            ApplyConfig(visualConfig.PinMarkers);
        }

        public Color PinColor
        {
            get => PinHead.PinColor;
            set => PinHead.PinColor = value;
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

        public Point GetConnectionPoint() => new(Width / 2.0, PinHead.Height / 2.0);
        public Point GetShaftTipPoint() => new(Width / 2.0, Height);

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

- [x] **Step 3: Create `ManualLayoutPinMarker.xaml`**

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

- [x] **Step 4: Create `ManualLayoutPinMarker.xaml.cs`**

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
            PinHead.PinColor = DrawnPinColorPalette.GetRandom();
            PinHead.ApplyConfig(visualConfig.PinMarkers);
            Width = PinHead.Width;
            Height = PinHead.Height;
        }

        public Color PinColor
        {
            get => PinHead.PinColor;
            set => PinHead.PinColor = value;
        }

        public void SetPinColor(Color color)
        {
            PinColor = color;
        }

        public Point GetConnectionPoint() => new(Width / 2.0, Height / 2.0);
    }
}
```

- [x] **Step 5: Extract the shared color palette**

Create `Views/DrawnPinColorPalette.cs` by moving the existing saturated `PinColors` array and
locked/shared random selection out of `PinMarker`:

```csharp
using System;
using System.Windows.Media;

namespace InteractiveWorldMap.Views;

public static class DrawnPinColorPalette
{
    private static readonly Random Random = new();

    private static readonly Color[] Colors =
    {
        Color.FromRgb(229, 57, 53),
        Color.FromRgb(25, 118, 210),
        Color.FromRgb(46, 125, 50),
        Color.FromRgb(245, 124, 0),
        Color.FromRgb(123, 31, 162),
        Color.FromRgb(194, 24, 91),
        Color.FromRgb(0, 151, 167),
        Color.FromRgb(251, 192, 45),
        Color.FromRgb(109, 76, 65),
        Color.FromRgb(0, 105, 92)
    };

    public static Color GetRandom() => Colors[Random.Next(Colors.Length)];
}
```

Both explicit controls call `DrawnPinColorPalette.GetRandom()`. This removes their last
dependency on the legacy complete-pin control.

- [x] **Step 6: Preserve hover, click, and cap geometry APIs**

Both explicit controls must retain the current drawn-pin interaction behavior. Add these methods
to each control:

```csharp
public void AnimateHover(bool isHovered)
{
    var animation = new DoubleAnimation
    {
        To = isHovered ? 1.15 : 1.0,
        Duration = TimeSpan.FromMilliseconds(150),
        EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
    };
    PinTransform.BeginAnimation(ScaleTransform.ScaleXProperty, animation);
    PinTransform.BeginAnimation(ScaleTransform.ScaleYProperty, animation);
}

public void AnimateClick()
{
    var storyboard = new Storyboard();
    var scaleX = new DoubleAnimation
    {
        To = 1.3,
        Duration = TimeSpan.FromMilliseconds(50),
        AutoReverse = true
    };
    var scaleY = new DoubleAnimation
    {
        To = 1.3,
        Duration = TimeSpan.FromMilliseconds(50),
        AutoReverse = true
    };
    Storyboard.SetTarget(scaleX, PinTransform);
    Storyboard.SetTargetProperty(
        scaleX,
        new PropertyPath(ScaleTransform.ScaleXProperty));
    Storyboard.SetTarget(scaleY, PinTransform);
    Storyboard.SetTargetProperty(
        scaleY,
        new PropertyPath(ScaleTransform.ScaleYProperty));
    storyboard.Children.Add(scaleX);
    storyboard.Children.Add(scaleY);
    storyboard.Begin();
}
```

Add `using System.Windows.Media.Animation;`. `AutoStubPinMarker` must additionally expose:

```csharp
public Point GetScaledShaftTipPoint() => ApplyPinTransform(GetShaftTipPoint());
public Point GetScaledConnectionPoint() => ApplyPinTransform(GetConnectionPoint());
public double GetScaledShaftOutlineWidth() => PinShaftOutline.Width * PinTransform.ScaleX;
```

Use the same center-origin transform math as the existing control:

```csharp
private Point ApplyPinTransform(Point point)
{
    var center = new Point(Width / 2.0, Height / 2.0);
    return new Point(
        center.X + ((point.X - center.X) * PinTransform.ScaleX),
        center.Y + ((point.Y - center.Y) * PinTransform.ScaleY));
}
```

Wire `MouseEnter` / `MouseLeave` in both controls to `AnimateHover`. Put a named
`ScaleTransform x:Name="PinTransform"` on each control's root visual so the API and XAML agree.

- [x] **Step 7: Run focused tests**

Run:

```powershell
dotnet test Tests\InteractiveWorldMap.Tests.csproj --filter "FullyQualifiedName~PinMarkerRenderingTests|FullyQualifiedName~ManualLayoutPinMarker_HasNoShaftVisual|FullyQualifiedName~AutoStubPinMarker_OwnsShortShaftVisual" --no-restore
```

Expected: the new-control and rendering tests pass. The `ExtensionLineRenderer` migration guard
remains intentionally red until Task 6.

---

## Task 5: Route Drawn Marker Creation Through Explicit Roles

**Files:**
- Create: `Views/DrawnPinMarkerFactory.cs`
- Modify: `MainWindow.xaml.cs`
- Modify: `MainWindow.CompositePins.partial.cs`
- Test: `Tests/DrawnPinModelSeparationTests.cs`

- [x] **Step 1: Create factory**

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

        public UserControl Create(DrawnPinRole role, Color? pinColor = null)
        {
            var marker = role switch
            {
                DrawnPinRole.AutoStub => new AutoStubPinMarker(_visualConfig),
                DrawnPinRole.ManualLayout => new ManualLayoutPinMarker(_visualConfig),
                _ => new AutoStubPinMarker(_visualConfig)
            };

            if (pinColor.HasValue)
            {
                switch (marker)
                {
                    case AutoStubPinMarker autoStub:
                        autoStub.PinColor = pinColor.Value;
                        break;
                    case ManualLayoutPinMarker manual:
                        manual.PinColor = pinColor.Value;
                        break;
                }
            }

            return marker;
        }
    }
}
```

Add `using System.Windows.Media;` for `Color`.

- [x] **Step 2: Add factory field to `MainWindow`**

Modify `MainWindow.xaml.cs`:

```csharp
private DrawnPinMarkerFactory _drawnPinFactory = null!;
```

Initialize it after `_visualConfig` loads:

```csharp
_drawnPinFactory = new DrawnPinMarkerFactory(_visualConfig);
```

- [x] **Step 3: Use auto stub role in marker creation**

In `CreateDrawnPinMarker(Location location)` in `MainWindow.CompositePins.partial.cs`,
replace direct `new PinMarker(_visualConfig)` creation with:

```csharp
var autoStub = (AutoStubPinMarker)_drawnPinFactory.Create(DrawnPinRole.AutoStub);
if (!_visualConfig.PinMarkers.UseRandomColors &&
    ColorConverter.ConvertFromString(_visualConfig.PinMarkers.DefaultBallColor) is Color defaultColor)
{
    autoStub.PinColor = defaultColor;
}

var marker = new LocationMarker(_visualConfig)
{
    Location = location,
    Content = autoStub,
    Width = autoStub.Width,
    Height = autoStub.Height,
    Tag = autoStub
};
return marker;
```

Preserve existing `marker.Width`, `marker.Height`, `Location`, click handlers, base visual capture, and logging.

- [x] **Step 4: Run the build**

Run:

```powershell
dotnet build InteractiveWorldMap.sln
```

Expected: build succeeds. The renderer migration guard intentionally remains red until Task 6.

---

## Task 6: Apply Manual Layout With Head-Only Drawn Pins

**Files:**
- Create: `MainWindow.DrawnPins.partial.cs`
- Modify: `MainWindow.LayoutEditor.partial.cs`
- Modify: `MainWindow.LayoutEditorDrag.partial.cs`
- Modify: `Views/ExtensionLineRenderer.cs`
- Modify: `Views/IExtensionLineRenderer.cs`
- Test: `Tests/DrawnPinModelSeparationTests.cs`, `Tests/LayoutEditorControllerTests.cs`, `Tests/ManualLayoutZoomAnimationTests.cs`

- [x] **Step 1: Add a focused drawn-role orchestration partial**

Create `MainWindow.DrawnPins.partial.cs` and place `SetDrawnPinRole` there:

```csharp
using System.Windows.Media;
using InteractiveWorldMap.Views;

namespace InteractiveWorldMap
{
    public partial class MainWindow
    {
        private void SetDrawnPinRole(LocationMarker marker, DrawnPinRole role)
        {
            if (!_visualConfig.UsePinMarkers || marker.Content is CompositePinMarker)
                return;

            if ((role == DrawnPinRole.AutoStub && marker.Content is AutoStubPinMarker) ||
                (role == DrawnPinRole.ManualLayout && marker.Content is ManualLayoutPinMarker))
                return;

            if (marker.Content is not AutoStubPinMarker &&
                marker.Content is not ManualLayoutPinMarker)
                return;

            Color? color = marker.Content switch
            {
                AutoStubPinMarker autoStub => autoStub.PinColor,
                ManualLayoutPinMarker manual => manual.PinColor,
                _ => null
            };

            var content = _drawnPinFactory.Create(role, color);
            marker.Content = content;
            marker.Width = content.Width;
            marker.Height = content.Height;
        }
    }
}
```

Keep role creation and color transfer out of `MainWindow.LayoutEditor.partial.cs`, which is
already close to the repository's 800-line limit.

The guard is based on the marker's actual content, not `CanUseCompositePins()`. Composite mode
can be globally enabled while one marker uses the drawn fallback after an asset/render failure;
that drawn marker must still switch roles correctly during drag and replay.

- [x] **Step 2: Teach endpoint saving about both explicit roles**

In `GetMarkerEndpoint`, preserve the extension-line and composite branches, then handle both
drawn roles:

```csharp
if (marker.Content is ManualLayoutPinMarker manualPin)
{
    var connection = manualPin.GetConnectionPoint();
    return new Point(
        Canvas.GetLeft(marker) + connection.X,
        Canvas.GetTop(marker) + connection.Y);
}

if (marker.Content is AutoStubPinMarker autoStub)
{
    var connection = autoStub.GetConnectionPoint();
    return new Point(
        Canvas.GetLeft(marker) + connection.X,
        Canvas.GetTop(marker) + connection.Y);
}
```

- [x] **Step 3: Use manual role before anchoring manual-layout line**

In `ApplyManualLayout(ManualLayout layout)`, before adding an extension line for `instruction.RequiresExtensionLine`, call:

```csharp
SetDrawnPinRole(marker, DrawnPinRole.ManualLayout);
```

Then keep:

```csharp
_extensionLineRenderer.AddLine(marker, instruction.OriginalScreen, instruction.ExtendedScreen);
_extensionLineRenderer.AnchorExtendedMarker(marker, instruction.ExtendedScreen);
```

- [x] **Step 4: Anchor zero-length seeds by auto-stub tip**

In the `else` branch for `instruction.RequiresExtensionLine == false`, call:

```csharp
SetDrawnPinRole(marker, DrawnPinRole.AutoStub);

if (marker.Content is AutoStubPinMarker autoStub)
{
    var tip = autoStub.GetShaftTipPoint();
    Canvas.SetLeft(marker, instruction.OriginalScreen.X - tip.X);
    Canvas.SetTop(marker, instruction.OriginalScreen.Y - tip.Y);
}
```

Use `instruction.OriginalScreen`, not `ExtendedScreen`: the shaft tip represents the map
location, while the final-endpoint policy has already decided that the saved head displacement
does not warrant an external shaft. Remove the old `LocationMarkerSize / 2` bounding-box
centering and any `PinMarker.SetShaftVisible(true)` call.

- [x] **Step 5: Update `ExtensionLineRenderer.AnchorExtendedMarker`**

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

- [x] **Step 6: Convert drawn pins before the first drag frame**

In `MainWindow.LayoutEditorDrag.partial.cs`, after `_draggedMarker = marker` and before mouse
capture, convert an auto stub to a head-only marker while preserving the same apparent head
position:

```csharp
if (marker.Content is AutoStubPinMarker)
{
    var viewport = MapDisplay.CurrentViewport;
    if (viewport != null)
    {
        var tipScreen = viewport.SourceToScreen(
            marker.Location.PixelX,
            marker.Location.PixelY,
            MapDisplay.ActualWidth,
            MapDisplay.ActualHeight);
        var headScreen = GetMarkerEndpoint(marker);

        SetDrawnPinRole(marker, DrawnPinRole.ManualLayout);
        if (!_extensionLineRenderer.HasLine(marker))
            _extensionLineRenderer.AddLine(marker, tipScreen, headScreen);
        _extensionLineRenderer.AnchorExtendedMarker(marker, headScreen);
    }
}
```

This makes the external line replace the built-in stub before WPF can render a dragged frame.
The head stays in place because `headScreen` is captured before replacing the content. Existing
manual-layout pins and composite pins skip this branch.

- [x] **Step 7: Add drag-start and auto-stub anchor guards**

Add source/behavior guards to `Tests/DrawnPinModelSeparationTests.cs`:

```csharp
[Fact]
public void ApplyManualLayout_ZeroLengthRole_AnchorsAutoStubByShaftTip()
{
    var source = File.ReadAllText(
        Path.Combine(RepoRoot, "MainWindow.LayoutEditor.partial.cs"));

    Assert.Contains("autoStub.GetShaftTipPoint()", source);
    Assert.Contains("instruction.OriginalScreen.X - tip.X", source);
    Assert.DoesNotContain(
        "instruction.ExtendedScreen.X - (markerSize / 2)",
        source);
}

[Fact]
public void DrawnDragStart_SwitchesAutoStubToManualRoleBeforeCapture()
{
    var source = File.ReadAllText(
        Path.Combine(RepoRoot, "MainWindow.LayoutEditorDrag.partial.cs"));
    var roleIndex = source.IndexOf(
        "SetDrawnPinRole(marker, DrawnPinRole.ManualLayout)",
        StringComparison.Ordinal);
    var captureIndex = source.IndexOf("marker.CaptureMouse()", StringComparison.Ordinal);

    Assert.True(roleIndex >= 0);
    Assert.True(captureIndex > roleIndex);
}

[Fact]
public void RoleSwitch_PreservesColorAndSupportsDrawnCompositeFallback()
{
    var source = File.ReadAllText(
        Path.Combine(RepoRoot, "MainWindow.DrawnPins.partial.cs"));

    Assert.Contains("autoStub.PinColor", source);
    Assert.Contains("manual.PinColor", source);
    Assert.Contains("_drawnPinFactory.Create(role, color)", source);
    Assert.Contains("marker.Content is CompositePinMarker", source);
    Assert.DoesNotContain("CanUseCompositePins()", source);
}
```

- [x] **Step 8: Update interface comments**

Modify `Views/IExtensionLineRenderer.cs` so comments say manual-layout drawn pins use a head-only visual and the extension line is the shaft. Remove language about hiding a pin's own shaft.

- [x] **Step 9: Run focused tests**

Run:

```powershell
dotnet test Tests\InteractiveWorldMap.Tests.csproj --filter "FullyQualifiedName~DrawnPinModelSeparationTests|FullyQualifiedName~LayoutEditorControllerTests|FullyQualifiedName~ManualLayoutZoomAnimationTests" --no-restore
```

Expected: all pass.

---

## Task 7: Remove Hidden-Shaft Compatibility From Active Paths

**Files:**
- Delete: `Views/PinMarker.xaml`
- Delete: `Views/PinMarker.xaml.cs`
- Modify: `MainWindow.CompositePins.partial.cs`
- Modify: `MainWindow.MarkerPlacement.partial.cs`
- Modify: `MainWindow.Navigation.partial.cs`
- Modify: `MainWindow.TipCap.partial.cs`
- Modify: `Views/ExtensionLineRenderer.cs`
- Test: `Tests/DrawnPinModelSeparationTests.cs`, `Tests/PinMarkerRenderingTests.cs`

- [x] **Step 1: Grep current shaft visibility usage**

Run:

```powershell
rg -n "SetShaftVisible|ShaftHost.Visibility|marker.Content is PinMarker" MainWindow*.cs Views Tests
```

Expected before edits: active `PinMarker` hits in `MainWindow` and `ExtensionLineRenderer`.

- [x] **Step 2: Remove active calls**

Replace `TryPlaceDrawnPinAtMapPoint` in `MainWindow.CompositePins.partial.cs` with explicit
auto-stub placement:

```csharp
private bool TryPlaceDrawnPinAtMapPoint(
    LocationMarker marker,
    MarkerScreenPlacement placement)
{
    if (marker.Content is not AutoStubPinMarker autoStub)
        return false;

    var mapPoint = GetMarkerMapPoint(placement);
    var shaftTip = autoStub.GetShaftTipPoint();
    Canvas.SetLeft(marker, mapPoint.X - shaftTip.X);
    Canvas.SetTop(marker, mapPoint.Y - shaftTip.Y);
    return true;
}
```

Update `AnimateMarkerClick` to dispatch to `AutoStubPinMarker` and
`ManualLayoutPinMarker`, calling each control's `AnimateClick()`.

In `MainWindow.MarkerPlacement.partial.cs` and `MainWindow.Navigation.partial.cs`, replace
animation-anchor checks for `PinMarker` with:

```csharp
if (marker.Content is AutoStubPinMarker autoStub)
    anchor = autoStub.GetShaftTipPoint();
else if (marker.Content is ManualLayoutPinMarker manual)
    anchor = manual.GetConnectionPoint();
```

Retain the composite branch after these drawn branches.

- [x] **Step 3: Migrate divot-cap geometry**

In `MainWindow.TipCap.partial.cs`, accept both explicit roles. Extension-line caps only need a
drawn role plus a mapped line; built-in caps require `AutoStubPinMarker`:

```csharp
if (marker.Content is not AutoStubPinMarker &&
    marker.Content is not ManualLayoutPinMarker)
{
    continue;
}

Panel.SetZIndex(marker, DrawnPinHeadZIndex);

if (_extensionLineRenderer.HasLine(marker))
{
    if (TryBuildExtensionPlacement(marker, out var extPlacement))
        placements.Add(extPlacement);
}
else if (marker.Content is AutoStubPinMarker autoStub)
{
    if (TryBuildStubPlacement(marker, autoStub, out var stubPlacement))
        placements.Add(stubPlacement);
}
```

Change `TryBuildStubPlacement` to accept `AutoStubPinMarker`; keep its use of
`GetScaledShaftTipPoint`, `GetScaledConnectionPoint`, and
`GetScaledShaftOutlineWidth`.

- [x] **Step 4: Tighten pin-style base checks**

Update `IsPinStyleMarkerBase` and any equivalent type guards so an
`AutoStubPinMarker` is a valid drawn fallback base, while `ManualLayoutPinMarker` is an
active overlay role and is never captured as the canonical base visual:

```csharp
private static bool IsPinStyleMarkerBase(object? content) =>
    content is AutoStubPinMarker or CompositePinMarker;
```

- [x] **Step 5: Delete the legacy complete-pin control**

After the active type guards, animation handlers, placement paths, cap path, and color palette
have migrated, delete `Views/PinMarker.xaml` and `Views/PinMarker.xaml.cs`. Update
`Tests/PinMarkerRenderingTests.cs` to inspect `AutoStubPinMarker` and
`ManualLayoutPinMarker`. Add:

```csharp
[Fact]
public void LegacyPinMarkerControl_IsRemoved()
{
    Assert.False(File.Exists(Path.Combine(RepoRoot, "Views", "PinMarker.xaml")));
    Assert.False(File.Exists(Path.Combine(RepoRoot, "Views", "PinMarker.xaml.cs")));
}
```

- [x] **Step 6: Add migration source guards**

Add to `Tests/DrawnPinModelSeparationTests.cs`:

```csharp
[Theory]
[InlineData("MainWindow.MarkerPlacement.partial.cs")]
[InlineData("MainWindow.Navigation.partial.cs")]
[InlineData("MainWindow.TipCap.partial.cs")]
public void ActivePlacementAndCapPaths_UseExplicitDrawnRoles(string fileName)
{
    var source = File.ReadAllText(Path.Combine(RepoRoot, fileName));

    Assert.Contains("AutoStubPinMarker", source);
    Assert.DoesNotContain("SetShaftVisible(", source);
}
```

- [x] **Step 7: Run source guard tests**

Run:

```powershell
dotnet test Tests\InteractiveWorldMap.Tests.csproj --filter FullyQualifiedName~DrawnPinModelSeparationTests --no-restore
```

Expected: pass, proving manual-layout drawn pins no longer rely on hidden built-in shafts.

---

## Task 8: Documentation And Plan State

**Files:**
- Modify: `docs/guides/VISUAL_CONFIG.md`
- Modify: `docs/TO_DO.md`
- Modify: `CHANGELOG.md`
- Modify: `docs/exec-plans/active/README.md`
- Modify: `docs/exec-plans/active/composite-pins-program.md`
- Modify: `docs/exec-plans/active/drawn-pin-tip-cap-plan.md`
- Modify: `docs/exec-plans/completed/tuning-and-pin-render-bugfixes-plan.md`
- Modify: `docs/exec-plans/completed/remove-pins-jpg-legacy-path-plan.md`
- Move: `docs/exec-plans/active/drawn-pin-model-separation-plan.md` to `docs/exec-plans/completed/drawn-pin-model-separation-plan.md`

- [x] **Step 1: Update visual config guide**

Add this under `## PinMarkers (drawn fallback)`:

```markdown
Drawn mode has two visual roles:

- Auto stub pins use a drawn head plus the configured short vertical shaft.
- Manual-layout pins use the same drawn head without an internal shaft; the saved/manual extension line is the shaft.

This keeps edited drawn pins from drawing a duplicate vertical shaft under the head.
```

- [x] **Step 2: Update `docs/TO_DO.md`**

When implementation and verification are complete, remove:

```markdown
- [x] Separate drawn pin model into head-only, auto-stub, and manual-layout pin components ...
```

- [x] **Step 3: Update changelog**

Add under `[Unreleased]`:

```markdown
- **Drawn pin model separation:** Drawn manual-layout pins now use a head-only visual while auto stubs keep their built-in short shaft anchored by its tip. Extension roles are classified from final projected endpoints, and drag no longer carries a hidden or duplicate built-in stub.
```

---

## Task 9: Final Verification

**Files:**
- No new files unless verification exposes a focused fix.

- [x] **Step 1: Run focused test suite**

Run:

```powershell
dotnet test Tests\InteractiveWorldMap.Tests.csproj --filter "FullyQualifiedName~DrawnPinModelSeparationTests|FullyQualifiedName~ManualLayoutPlacementPolicyTests|FullyQualifiedName~PinMarkerRenderingTests|FullyQualifiedName~ManualLayoutZoomAnimationTests|FullyQualifiedName~LayoutEditorControllerTests|FullyQualifiedName~CompositePinPlanCacheTests" --no-restore
```

Expected: all selected tests pass.

- [x] **Step 2: Run full repo verification**

Run:

```powershell
.\scripts\verify.ps1
```

Expected: restore, vulnerability check, build, all tests, doc links, taste checks, and headless startup validation pass.

- [x] **Step 3: Manual smoke**

Run app on Windows:

```powershell
dotnet run --project InteractiveWorldMap.csproj
```

Smoke:

1. Set `PinParts.UseCompositeRendering = false`.
2. Start at full map.
3. Load a generated seed containing at least one zero-length/non-extended marker.
4. Confirm that marker's shaft tip, not its head or bounding-box center, sits at the map location.
5. Confirm standalone locations render auto stub pins with short vertical shafts and configured caps.
6. Enter full-map Edit Layout.
7. Drag one standalone pin to create a custom angle/length.
8. Confirm the first dragged frame shows a head plus one external shaft, never the complete
   vertical stub moving with the head.
9. Confirm the head color remains unchanged while switching roles.
10. Save and exit.
11. Confirm the edited pin shows one custom shaft, not a custom shaft plus a vertical stub.
12. Hover the auto stub and manual-layout pin; confirm scaling and cap placement remain aligned.
13. Zoom into and back out of that location.
14. Confirm the edited pin remains a manual-layout pin during and after zoom-out.

- [x] **Step 4: Archive the completed plan and repair registries**

Only after Steps 1-3 pass:

1. Move this file to `docs/exec-plans/completed/drawn-pin-model-separation-plan.md`.
2. Remove its row from `docs/exec-plans/active/README.md` and add it to Recently completed.
3. Change the Drawn pin model row in `composite-pins-program.md` to the completed path and
   status `Complete`.
4. Change `drawn-pin-tip-cap-plan.md`'s dependency link to
   `../completed/drawn-pin-model-separation-plan.md`.
5. Repair the two completed-plan cross-links and the existing `CHANGELOG.md` plan link to use
   the completed path.

- [x] **Step 5: Run final doc checks after the move**

Run:

```powershell
py -3 scripts\verify_doc_links.py
py -3 scripts\doc_gardening.py
```

Expected: both pass.

---

## Acceptance Criteria

- [x] Auto stub pins still show a short vertical shaft in drawn mode.
- [x] Zero-length seed records anchor the auto-stub shaft tip at the map location.
- [x] Manual-layout drawn pins show a head plus exactly one external shaft.
- [x] Extension-line classification uses final projected endpoints.
- [x] Drag switches to head-only before the first moved frame and preserves head color.
- [x] Drawn hover/click animation and divot caps work for both explicit roles.
- [x] No active placement path calls `SetShaftVisible(false)` to make manual-layout pins look correct.
- [x] Existing composite pin behavior is unchanged.
- [x] Full-map edit/save/exit and zoom-out replay still preserve manual-layout drawn pins.
- [x] `.\scripts\verify.ps1` passes.

## Modularity / File-Size Impact

Current relevant sizes at plan review:

| File | Current lines | Expected change |
|------|--------------:|----------------:|
| `MainWindow.LayoutEditor.partial.cs` | 752 | At most +25 lines for endpoint and replay branches; keep below 800 |
| `MainWindow.CompositePins.partial.cs` | 550 | Net-neutral to +20 lines by replacing legacy drawn creation/placement |
| `MainWindow.Navigation.partial.cs` | 607 | Net-neutral type-switch migration |
| `Views/ExtensionLineRenderer.cs` | 564 | Net-negative or neutral after removing hidden-shaft logic |
| `MainWindow.DrawnPins.partial.cs` | new | Approximately 50-90 lines; owns role switching and anchoring only |

Ownership boundaries:

- Controls own their visual geometry, color, and local animation.
- `DrawnPinMarkerFactory` owns role construction, not canvas placement.
- `MainWindow.DrawnPins.partial.cs` owns role transitions and preservation of per-marker visual state.
- `ManualLayoutPlacementPolicy` owns only the pure endpoint-distance decision.
- `ExtensionLineRenderer` owns external lines and head endpoint anchoring; it must not mutate an
  internal shaft.

If `MainWindow.LayoutEditor.partial.cs` reaches 800 lines during implementation, move additional
drawn-role helpers into `MainWindow.DrawnPins.partial.cs`; do not suppress the taste check.

## Risks

| Risk | Mitigation |
|------|------------|
| WPF XAML control split causes sizing drift | Keep `GetConnectionPoint()` and `PinMarkerConfig` math identical to current `PinMarker` |
| Manual-layout pin changes color on role switch | Require the factory and `SetDrawnPinRole` to carry the existing `PinColor`; guard in tests and smoke |
| Divot caps or zoom offsets regress after the type split | Preserve scaled tip/connection APIs and migrate `MainWindow.TipCap`, marker-placement, and navigation type checks together |
| A drawn fallback cannot switch roles while composite mode is globally enabled | Guard on actual `marker.Content`, not `CanUseCompositePins()`, and retain composite persistence coverage |
| MainWindow grows again | Keep role creation in `DrawnPinMarkerFactory`; do not inline XAML/control construction in placement loops |

