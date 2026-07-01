# Tuning Categories, Drawn-Pin Sizing, and Marker Hitboxes Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Organize Runtime Tuning into category choices, expose drawn-pin dimensions, and replace whole-glyph marker input with tunable circular targets centered on pin heads and cluster images.

**Architecture:** Keep persisted values in Models, pure diameter/center math in Utilities, category presentation in the existing tuning View, and input orchestration in a focused `MainWindow` partial backed by a separate overlay Canvas. Existing navigation and edit decisions remain in `MainWindow`; the overlay only supplies correctly positioned WPF input elements.

**Tech Stack:** C# 10, WPF, .NET 6, Newtonsoft.Json, xUnit

---

## File Structure

- Create `Models/MarkerHitTargetConfig.cs`: persisted pin and cluster target diameters.
- Create `Utilities/MarkerHitTargetGeometry.cs`: pure effective-diameter and canvas-center math.
- Create `MainWindow.MarkerInteraction.partial.cs`: target lifecycle, synchronization, and event routing.
- Create `Tests/MarkerHitTargetGeometryTests.cs`: behavior tests for target sizing and centering.
- Create `Tests/MarkerInteractionWiringTests.cs`: structural lifecycle/routing guards.
- Modify `Models/VisualConfig.cs`: own `MarkerHitTargets`.
- Modify `Models/CompositePinRenderPlan.cs`: expose rendered head diameter.
- Modify `Models/TuningPanelEventArgs.cs`: carry drawn dimensions and hit-target values.
- Modify `Services/CompositePinRenderPlanBuilder.cs`: populate rendered head diameter.
- Modify `Views/MapDisplayControl.xaml` and `.xaml.cs`: expose a non-background interaction Canvas.
- Modify `Views/DeveloperTuningPanel.xaml` and `.xaml.cs`: four category sections and new fields.
- Modify `Views/ManualLayoutPinMarker.xaml.cs`: apply updated drawn-pin config.
- Modify `MainWindow.xaml` and `MainWindow.DeveloperTuning.partial.cs`: category chooser and runtime apply.
- Modify marker placement/composite/layout partials: synchronize targets after visual geometry changes.
- Modify `Tests/VisualConfigServiceTests.cs`, `Tests/TuningPanelWiringTests.cs`,
  `Tests/TuningReloadValidationTests.cs`, and composite builder tests.
- Modify `visual-config.json`, `docs/TO_DO.md`, and `CHANGELOG.md`.

## Task 1: Persisted Configuration and Pure Geometry

**Files:**
- Create: `Models/MarkerHitTargetConfig.cs`
- Create: `Utilities/MarkerHitTargetGeometry.cs`
- Create: `Tests/MarkerHitTargetGeometryTests.cs`
- Modify: `Models/VisualConfig.cs`
- Modify: `Models/CompositePinRenderPlan.cs`
- Modify: `Services/CompositePinRenderPlanBuilder.cs`
- Modify: `Tests/VisualConfigServiceTests.cs`
- Modify: `Tests/CompositePinRenderPlanBuilderTests.cs`
- Modify: `visual-config.json`

- [x] **Step 1: Write failing configuration and geometry tests**

Add tests proving defaults, JSON round-trip, minimum sizing, and canvas-center
translation:

```csharp
[Fact]
public void MarkerHitTargetConfig_Defaults_AreTouchFriendly()
{
    var config = new MarkerHitTargetConfig();
    Assert.Equal(32.0, config.PinDiameterPx);
    Assert.Equal(40.0, config.ClusterDiameterPx);
}

[Theory]
[InlineData(32, 14, 32)]
[InlineData(10, 14, 14)]
public void EffectiveDiameter_NeverShrinksBelowVisual(
    double configured, double visible, double expected)
{
    Assert.Equal(expected,
        MarkerHitTargetGeometry.EffectiveDiameter(configured, visible));
}

[Fact]
public void ToCanvasCenter_OffsetsLocalHeadCenterByMarkerPosition()
{
    Assert.Equal(
        new Point(112, 64),
        MarkerHitTargetGeometry.ToCanvasCenter(
            new Point(100, 50), new Point(12, 14)));
}
```

Add a config-service round-trip test setting `PinDiameterPx = 36` and
`ClusterDiameterPx = 48`, and extend a real composite-plan builder test with:

```csharp
Assert.Equal(config.TargetHeadRadiusPx * 2.0, plan.HeadDiameterPx, 3);
```

- [x] **Step 2: Run focused tests and verify RED**

Run:

```powershell
dotnet test Tests\InteractiveWorldMap.Tests.csproj --filter "FullyQualifiedName~MarkerHitTargetGeometryTests|FullyQualifiedName~VisualConfigServiceTests|FullyQualifiedName~CompositePinRenderPlanBuilderTests" --no-restore
```

Expected: compilation failures for missing `MarkerHitTargetConfig`,
`MarkerHitTargetGeometry`, `VisualConfig.MarkerHitTargets`, and
`CompositePinRenderPlan.HeadDiameterPx`.

- [x] **Step 3: Implement the model and geometry API**

Create:

```csharp
namespace InteractiveWorldMap.Models;

public sealed class MarkerHitTargetConfig
{
    public double PinDiameterPx { get; set; } = 32.0;
    public double ClusterDiameterPx { get; set; } = 40.0;
}
```

Add to `VisualConfig`:

```csharp
public MarkerHitTargetConfig MarkerHitTargets { get; set; } =
    new MarkerHitTargetConfig();
```

Create the pure helper:

```csharp
using System;
using System.Windows;

namespace InteractiveWorldMap.Utilities;

public static class MarkerHitTargetGeometry
{
    public static double EffectiveDiameter(double configured, double visible) =>
        Math.Max(configured, visible);

    public static Point ToCanvasCenter(Point markerTopLeft, Point localCenter) =>
        new(markerTopLeft.X + localCenter.X, markerTopLeft.Y + localCenter.Y);
}
```

Add `HeadDiameterPx` to `CompositePinRenderPlan`. In
`CompositePinRenderPlanBuilder.AssembleResult`, set it from the actual rendered
radius:

```csharp
HeadDiameterPx = 2.0 * v.HeadEntry.Head.LocalRadius *
                 (config.TargetHeadRadiusPx > 0.0 &&
                  v.HeadEntry.Head.LocalRadius > 0.0
                     ? config.TargetHeadRadiusPx / v.HeadEntry.Head.LocalRadius
                     : geo.OverallScale),
```

Add the checked-in JSON:

```json
"MarkerHitTargets": {
  "PinDiameterPx": 32.0,
  "ClusterDiameterPx": 40.0
},
```

- [x] **Step 4: Run focused tests and verify GREEN**

Run the Step 2 command. Expected: all selected tests pass.

- [x] **Step 5: Commit configuration and geometry**

```powershell
git add Models\MarkerHitTargetConfig.cs Models\VisualConfig.cs Models\CompositePinRenderPlan.cs Utilities\MarkerHitTargetGeometry.cs Services\CompositePinRenderPlanBuilder.cs Tests\MarkerHitTargetGeometryTests.cs Tests\VisualConfigServiceTests.cs Tests\CompositePinRenderPlanBuilderTests.cs visual-config.json
git commit -m "feat: configure centered marker hit targets"
```

## Task 2: Category-Based Tuning UI and New Values

**Files:**
- Modify: `Models/TuningPanelEventArgs.cs`
- Modify: `Views/DeveloperTuningPanel.xaml`
- Modify: `Views/DeveloperTuningPanel.xaml.cs`
- Modify: `MainWindow.xaml`
- Modify: `MainWindow.DeveloperTuning.partial.cs`
- Modify: `Tests/TuningPanelWiringTests.cs`
- Modify: `Tests/TuningReloadValidationTests.cs`

- [ ] **Step 1: Write failing category and value-wiring tests**

Extend `TuningPanelWiringTests` to require named category sections and the new
controls:

```csharp
[Theory]
[InlineData("MapSection")]
[InlineData("CompositePinsSection")]
[InlineData("DrawnPinsSection")]
[InlineData("HitboxesSection")]
public void DeveloperTuningPanel_HasCategorySection(string name)
{
    var xaml = File.ReadAllText(
        Path.Combine(RepoRoot, "Views", "DeveloperTuningPanel.xaml"));
    Assert.Contains($"x:Name=\"{name}\"", xaml);
}

[Fact]
public void TuningButton_OffersFourCategoryChoices()
{
    var xaml = File.ReadAllText(Path.Combine(RepoRoot, "MainWindow.xaml"));
    foreach (var category in new[] { "Map", "Composite Pins", "Drawn Pins", "Hitboxes" })
        Assert.Contains($"Header=\"{category}\"", xaml);
}
```

Require `TxtDrawnHeadDiameter`, `TxtDrawnShaftWidth`,
`TxtDrawnShaftLength`, `TxtPinHitDiameter`, and
`TxtClusterHitDiameter`, each with a tooltip. Add validation theories that
zero, negative, NaN, and infinity are rejected for every new numeric value.

- [ ] **Step 2: Run tuning tests and verify RED**

```powershell
dotnet test Tests\InteractiveWorldMap.Tests.csproj --filter "FullyQualifiedName~TuningPanelWiringTests|FullyQualifiedName~TuningReloadValidationTests" --no-restore
```

Expected: failures because category sections, menu items, event fields, and
validation do not exist.

- [ ] **Step 3: Add category presentation**

Add a View-only enum in `DeveloperTuningPanel.xaml.cs`:

```csharp
public enum TuningCategory
{
    Map,
    CompositePins,
    DrawnPins,
    Hitboxes
}
```

Add:

```csharp
public TuningCategory? VisibleCategory { get; private set; }

public void ShowCategory(TuningCategory category)
{
    VisibleCategory = category;
    CategoryTitleText.Text = category switch
    {
        TuningCategory.CompositePins => "Composite Pins",
        TuningCategory.DrawnPins => "Drawn Pins",
        _ => category.ToString()
    };
    MapSection.Visibility = category == TuningCategory.Map
        ? Visibility.Visible : Visibility.Collapsed;
    CompositePinsSection.Visibility = category == TuningCategory.CompositePins
        ? Visibility.Visible : Visibility.Collapsed;
    DrawnPinsSection.Visibility = category == TuningCategory.DrawnPins
        ? Visibility.Visible : Visibility.Collapsed;
    HitboxesSection.Visibility = category == TuningCategory.Hitboxes
        ? Visibility.Visible : Visibility.Collapsed;
}
```

Reorganize the existing controls into the four named StackPanels from the
approved spec. Keep the shared footer outside all four sections.

Replace the button's direct Click handler with a `ContextMenu` containing four
`MenuItem`s whose `Tag` values match the enum names and whose shared handler is
`OnTuningCategoryClick`.

- [ ] **Step 4: Wire drawn dimensions and hit-target values through tuning**

Add to `TuningPanelEventArgs`:

```csharp
public double DrawnHeadDiameterPx { get; set; }
public double DrawnShaftWidthPx { get; set; }
public double DrawnShaftLengthPx { get; set; }
public double PinHitDiameterPx { get; set; }
public double ClusterHitDiameterPx { get; set; }
```

Load, parse, and emit these values in `DeveloperTuningPanel`. Validate each as
positive and finite with labels “Drawn head diameter”, “Drawn shaft width”,
“Drawn shaft length”, “Pin hitbox”, and “Cluster hitbox”.

Add `ShowTuningCategory(TuningCategory category)` in `MainWindow`:

```csharp
private void ShowTuningCategory(TuningCategory category)
{
    if (DeveloperTuningPanel.Visibility == Visibility.Visible &&
        DeveloperTuningPanel.VisibleCategory == category)
    {
        DeveloperTuningPanel.Visibility = Visibility.Collapsed;
        return;
    }

    DeveloperTuningPanel.ShowCategory(category);
    DeveloperTuningPanel.Visibility = Visibility.Visible;
}
```

The Tuning button click opens its context menu. A category menu item calls
`ShowTuningCategory`.

- [ ] **Step 5: Run tuning tests and verify GREEN**

Run the Step 2 command. Expected: all selected tests pass.

- [ ] **Step 6: Commit category UI and value wiring**

```powershell
git add Models\TuningPanelEventArgs.cs Views\DeveloperTuningPanel.xaml Views\DeveloperTuningPanel.xaml.cs MainWindow.xaml MainWindow.DeveloperTuning.partial.cs Tests\TuningPanelWiringTests.cs Tests\TuningReloadValidationTests.cs
git commit -m "feat: organize runtime tuning by category"
```

## Task 3: Marker Interaction Overlay

**Files:**
- Modify: `Views/MapDisplayControl.xaml`
- Modify: `Views/MapDisplayControl.xaml.cs`
- Create: `MainWindow.MarkerInteraction.partial.cs`
- Create: `Tests/MarkerInteractionWiringTests.cs`
- Modify: `MainWindow.xaml.cs`
- Modify: `MainWindow.CompositePins.partial.cs`
- Modify: `MainWindow.DrawnPins.partial.cs`
- Modify: `MainWindow.MarkerPlacement.partial.cs`
- Modify: `MainWindow.LayoutEditor.partial.cs`
- Modify: `MainWindow.LayoutEditorDrag.partial.cs`

- [ ] **Step 1: Write failing interaction-layer tests**

Add source/XAML guards that require:

```csharp
[Fact]
public void MapDisplay_HasSeparateInteractionCanvasWithoutBackground()
{
    var xaml = File.ReadAllText(
        Path.Combine(RepoRoot, "Views", "MapDisplayControl.xaml"));
    Assert.Contains("x:Name=\"MarkerInteractionCanvas\"", xaml);
    Assert.DoesNotContain("x:Name=\"MarkerInteractionCanvas\" Background=", xaml);
}

[Fact]
public void MarkerInteraction_UsesAuthoritativeCenters()
{
    var source = File.ReadAllText(
        Path.Combine(RepoRoot, "MainWindow.MarkerInteraction.partial.cs"));
    Assert.Contains("autoStub.GetConnectionPoint()", source);
    Assert.Contains("manual.GetConnectionPoint()", source);
    Assert.Contains("composite.RenderPlan.HeadCenterLocal", source);
    Assert.Contains("cluster.Width / 2.0", source);
    Assert.Contains("cluster.Height / 2.0", source);
}
```

Also require `RefreshMarkerHitTargets` after placement, composite replacement,
drawn-role replacement, drag movement, and `ClearMarkerHitTargets` from
`ClearAllMarkers`.

- [ ] **Step 2: Run interaction tests and verify RED**

```powershell
dotnet test Tests\InteractiveWorldMap.Tests.csproj --filter "FullyQualifiedName~MarkerInteractionWiringTests" --no-restore
```

Expected: failures for the missing Canvas and interaction partial.

- [ ] **Step 3: Add the overlay Canvas and lifecycle**

Add after `MarkerCanvas`:

```xml
<Canvas x:Name="MarkerInteractionCanvas"
        HorizontalAlignment="Stretch"
        VerticalAlignment="Stretch"/>
```

Expose it as:

```csharp
public Canvas MarkerInteractions => MarkerInteractionCanvas;
```

In `MainWindow.MarkerInteraction.partial.cs`, keep dictionaries from visual
markers to transparent Ellipses. `RefreshMarkerHitTargets` updates existing
elements rather than rebuilding them:

```csharp
private readonly Dictionary<LocationMarker, Ellipse> _pinHitTargets = new();
private readonly Dictionary<ClusterMarker, Ellipse> _clusterHitTargets = new();
```

Each ellipse uses `Fill = Brushes.Transparent`, `Cursor = Cursors.Hand`, a
diameter computed with `MarkerHitTargetGeometry.EffectiveDiameter`, and
`Canvas.Left/Top = center - diameter / 2`. Copy marker visibility and z-order
to its target. Remove targets whose marker is no longer in the corresponding
marker collection.

Set visual markers `IsHitTestVisible = false` only after their target has
authoritative center geometry.

- [ ] **Step 4: Route navigation, hover, right-click, and edit drag**

Extract the individual and cluster click lambda bodies from `MainWindow.xaml.cs`
into:

```csharp
private void HandleIndividualMarkerPrimaryAction(LocationMarker marker)
private void HandleClusterMarkerPrimaryAction(ClusterMarker marker)
```

Target events resolve their associated marker from the dictionary and call
these methods. Hover invokes the existing visual animation APIs. Composite
right-click calls `OnShaftOverrideRequested(marker, marker.Location.Name)`.

Adapt drag handlers to resolve a `LocationMarker` from either a marker or its
target. Capture/release the actual input element that raised the event while
keeping `_draggedMarker` as the visual marker. Register drag handlers on target
creation; they remain gated by `_layoutEditor.IsEditMode`, eliminating dynamic
marker subscriptions in `OnEditLayoutButtonClick` and `ExitEditMode`.

- [ ] **Step 5: Synchronize targets at every geometry boundary**

Call `RefreshMarkerHitTargets`:

- after individual and cluster placement batches;
- after `ApplyRenderPlanToMarker`;
- after `SetDrawnPinRole`;
- after every successful drag move;
- after manual-layout replay completes;
- after tuning reapplies the view.

Call `ClearMarkerHitTargets` before clearing marker collections.

- [ ] **Step 6: Run interaction and existing drag tests**

```powershell
dotnet test Tests\InteractiveWorldMap.Tests.csproj --filter "FullyQualifiedName~MarkerInteractionWiringTests|FullyQualifiedName~DrawnPinDragTests|FullyQualifiedName~CompositeDragStretchTests|FullyQualifiedName~CompositePinEditModeTests" --no-restore
```

Expected: all selected tests pass.

- [ ] **Step 7: Commit the interaction overlay**

```powershell
git add Views\MapDisplayControl.xaml Views\MapDisplayControl.xaml.cs MainWindow.MarkerInteraction.partial.cs MainWindow.xaml.cs MainWindow.CompositePins.partial.cs MainWindow.DrawnPins.partial.cs MainWindow.MarkerPlacement.partial.cs MainWindow.LayoutEditor.partial.cs MainWindow.LayoutEditorDrag.partial.cs Tests\MarkerInteractionWiringTests.cs
git commit -m "feat: center marker input on visible heads"
```

## Task 4: Apply Drawn Dimensions and Hitbox Tuning at Runtime

**Files:**
- Modify: `Views/ManualLayoutPinMarker.xaml.cs`
- Modify: `MainWindow.CompositePins.partial.cs`
- Modify: `MainWindow.DeveloperTuning.partial.cs`
- Modify: `Tests/TuningPanelWiringTests.cs`
- Modify: `Tests/MarkerInteractionWiringTests.cs`

- [ ] **Step 1: Write failing runtime-apply tests**

Require assignments:

```csharp
Assert.Contains("_visualConfig.PinMarkers.BallSize = e.DrawnHeadDiameterPx;", source);
Assert.Contains("_visualConfig.PinMarkers.ShaftWidth = e.DrawnShaftWidthPx;", source);
Assert.Contains("_visualConfig.PinMarkers.ShaftLength = e.DrawnShaftLengthPx;", source);
Assert.Contains("_visualConfig.MarkerHitTargets.PinDiameterPx = e.PinHitDiameterPx;", source);
Assert.Contains("_visualConfig.MarkerHitTargets.ClusterDiameterPx = e.ClusterHitDiameterPx;", source);
Assert.Contains("RefreshDrawnPinVisuals()", source);
Assert.Contains("RefreshMarkerHitTargets()", source);
```

Add a test requiring `ManualLayoutPinMarker.ApplyConfig(PinMarkerConfig)`.

- [ ] **Step 2: Run runtime tuning tests and verify RED**

```powershell
dotnet test Tests\InteractiveWorldMap.Tests.csproj --filter "FullyQualifiedName~TuningPanelWiringTests|FullyQualifiedName~MarkerInteractionWiringTests" --no-restore
```

Expected: failures for missing assignments and refresh methods.

- [ ] **Step 3: Implement visual refresh without losing layout state**

Add `ManualLayoutPinMarker.ApplyConfig`, mirroring construction:

```csharp
public void ApplyConfig(PinMarkerConfig pinConfig)
{
    PinHead.ApplyConfig(pinConfig);
    Width = PinHead.Width;
    Height = PinHead.Height;
}
```

Add `RefreshDrawnPinVisuals` that applies config to current drawn content and
to drawn content stored in `_baseMarkerVisuals`, then replaces each affected
`MarkerVisualState` with updated width/height. Preserve each pin color and do
not recreate location collections or saved layouts.

- [ ] **Step 4: Apply and classify tuning changes**

In `ApplyTuningAsync`, snapshot all five new values, assign them after
validation, and classify:

```csharp
var drawnDimensionsChanged =
    !NearlyEqual(oldDrawnHeadDiameter, e.DrawnHeadDiameterPx) ||
    !NearlyEqual(oldDrawnShaftWidth, e.DrawnShaftWidthPx) ||
    !NearlyEqual(oldDrawnShaftLength, e.DrawnShaftLengthPx);

var hitTargetsChanged =
    !NearlyEqual(oldPinHitDiameter, e.PinHitDiameterPx) ||
    !NearlyEqual(oldClusterHitDiameter, e.ClusterHitDiameterPx);
```

For drawn changes call `RefreshDrawnPinVisuals` before
`ReapplyViewAfterTuningChange`. For hit-target-only changes call
`RefreshMarkerHitTargets` directly. Include the five values in
`CreateTuningArgs` and `DeveloperTuningPanel.LoadValues`.

- [ ] **Step 5: Run focused tuning and interaction tests**

Run the Step 2 command. Expected: all selected tests pass.

- [ ] **Step 6: Commit runtime application**

```powershell
git add Views\ManualLayoutPinMarker.xaml.cs MainWindow.CompositePins.partial.cs MainWindow.DeveloperTuning.partial.cs Tests\TuningPanelWiringTests.cs Tests\MarkerInteractionWiringTests.cs
git commit -m "feat: tune drawn pin dimensions and hitboxes"
```

## Task 5: Documentation and Verification

**Files:**
- Modify: `docs/TO_DO.md`
- Modify: `CHANGELOG.md`
- Modify: `docs/superpowers/plans/2026-07-01-tuning-categories-pin-hitboxes.md`

- [ ] **Step 1: Run all focused feature tests**

```powershell
dotnet test Tests\InteractiveWorldMap.Tests.csproj --filter "FullyQualifiedName~MarkerHitTargetGeometryTests|FullyQualifiedName~MarkerInteractionWiringTests|FullyQualifiedName~TuningPanelWiringTests|FullyQualifiedName~TuningReloadValidationTests|FullyQualifiedName~VisualConfigServiceTests|FullyQualifiedName~CompositePinRenderPlanBuilderTests|FullyQualifiedName~DrawnPinDragTests|FullyQualifiedName~CompositeDragStretchTests" --no-restore
```

Expected: all selected tests pass with zero build warnings/errors.

- [ ] **Step 2: Update completion bookkeeping**

Remove these completed bullets from `docs/TO_DO.md`:

- organize Runtime Tuning into submenus;
- expose drawn-pin head/shaft dimensions;
- make pin/cluster hitboxes tunable and audit their centers.

Under `[Unreleased]`, add:

```markdown
- Runtime Tuning now opens category-specific Map, Composite Pins, Drawn Pins,
  and Hitboxes panels. Drawn head/shaft dimensions and shared pin/cluster
  pointer targets are configurable; pin targets are centered on visible heads,
  cluster targets are centered on marker images, and neither can shrink below
  its visible artwork.
```

- [ ] **Step 3: Run the full Windows verification gate**

```powershell
.\scripts\verify.ps1
```

Expected: restore, vulnerability scan, Release build, full tests, seed checks,
doc links, taste checks, and headless startup validation all pass.

- [ ] **Step 4: Review the final diff**

```powershell
git status --short
git diff --check
git diff --stat
```

Expected: only planned feature, test, config, plan, backlog, and changelog files
are present; `git diff --check` emits no errors.

- [ ] **Step 5: Mark this plan complete and commit bookkeeping**

Change every remaining checkbox in this plan to checked, then:

```powershell
git add docs\TO_DO.md CHANGELOG.md docs\superpowers\plans\2026-07-01-tuning-categories-pin-hitboxes.md
git commit -m "docs: complete tuning and hitbox rollout"
```
