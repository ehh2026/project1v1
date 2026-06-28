# Drawn Pin Divot Cap Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace filled drawn-pin tip caps with tunable near-black stroked divot lines whose concave bow always faces away from the pin head.

**Architecture:** Keep placement in `MainWindow.TipCap.partial.cs`, pure screen-space geometry in `Utilities/PinTipCapGeometry`, and stroke presentation in `Views/DrawnPinTipCapRenderer`. Add nullable `WidthPx` and `LineWeightPx` config values so legacy configs can fall back to their old effective width and the shaft outline thickness without introducing view-layer dependencies.

**Tech Stack:** C# 10, .NET 6, WPF geometry/path rendering, Newtonsoft.Json, xUnit

**Design:** `docs/superpowers/specs/2026-06-28-drawn-pin-divot-cap-design.md`

---

## File Map

- Modify `Models/DrawnPinTipCapConfig.cs`: active stroke settings and legacy fallback helpers.
- Modify `Models/TuningPanelEventArgs.cs`: width and line-weight event values.
- Modify `Utilities/PinTipCapGeometry.cs`: open horizontal and concave centerline geometry.
- Modify `Views/DrawnPinTipCapRenderer.cs`: one stroked path per cap, no fill/outline pair.
- Modify `MainWindow.TipCap.partial.cs`: resolve effective width/weight and build new geometry.
- Modify `Views/DeveloperTuningPanel.xaml`: Width, Line weight, and Curvature controls.
- Modify `Views/DeveloperTuningPanel.xaml.cs`: load, parse, and emit the new controls.
- Modify `MainWindow.DeveloperTuning.partial.cs`: apply/reload the new event fields.
- Modify `visual-config.json`: explicit new defaults while keeping `Style: "None"`.
- Modify focused tests under `Tests/`: geometry, config, renderer, validation, and wiring.
- Modify `docs/exec-plans/active/drawn-pin-tip-cap-plan.md`, `docs/TO_DO.md`, and `CHANGELOG.md`: corrected intent and completion state.

### Task 1: Add Stroke Config With Legacy Fallbacks

**Files:**
- Modify: `Models/DrawnPinTipCapConfig.cs`
- Modify: `Tests/VisualConfigServiceTests.cs`
- Modify: `visual-config.json`

- [ ] **Step 1: Write failing config tests**

Add tests proving explicit values round-trip and omitted values retain legacy
fallback behavior:

```csharp
[Fact]
public void Load_DrawnPinTipCap_DeserializesStrokeControls()
{
    var tempDir = CreateTempDir();
    try
    {
        var path = Path.Combine(tempDir, "visual-config.json");
        File.WriteAllText(path,
            @"{ ""PinMarkers"": { ""DrawnPinTipCap"": {
                ""Style"": ""Concave"", ""WidthPx"": 14.0,
                ""LineWeightPx"": 3.5, ""ArcDepthPx"": 4.0,
                ""Color"": ""#FF111111"" } } }");

        var cap = new VisualConfigService().Load(path).PinMarkers.DrawnPinTipCap;

        Assert.Equal(14.0, cap.WidthPx);
        Assert.Equal(3.5, cap.LineWeightPx);
        Assert.Equal("#FF111111", cap.Color);
    }
    finally
    {
        Directory.Delete(tempDir, recursive: true);
    }
}

[Fact]
public void Load_DrawnPinTipCap_LegacyFieldsResolveWithoutShrinking()
{
    var tempDir = CreateTempDir();
    try
    {
        var path = Path.Combine(tempDir, "visual-config.json");
        File.WriteAllText(path,
            @"{ ""PinMarkers"": { ""DrawnPinTipCap"": {
                ""Style"": ""Horizontal"", ""ExtendPx"": 2.0,
                ""HeightPx"": 6.0, ""UseOutlineRing"": true } } }");

        var cap = new VisualConfigService().Load(path).PinMarkers.DrawnPinTipCap;

        Assert.Equal(10.0, cap.ResolveWidthPx(outlineWidthPx: 6.0));
        Assert.Equal(1.5, cap.ResolveLineWeightPx(shaftOutlineThicknessPx: 1.5));
    }
    finally
    {
        Directory.Delete(tempDir, recursive: true);
    }
}
```

- [ ] **Step 2: Run the tests and verify RED**

Run:

```powershell
dotnet test Tests\InteractiveWorldMap.Tests.csproj --filter "FullyQualifiedName~VisualConfigServiceTests"
```

Expected: FAIL because `WidthPx`, `LineWeightPx`, and resolver methods do not
exist.

- [ ] **Step 3: Implement active fields and deterministic compatibility**

Update `DrawnPinTipCapConfig`:

```csharp
public double? WidthPx { get; set; }
public double? LineWeightPx { get; set; }
public double ArcDepthPx { get; set; } = 3.0;
public string? Color { get; set; } = "#FF111111";

public double ExtendPx { get; set; } = 0.0;

public double HeightPx { get; set; } = 6.0;

public bool UseOutlineRing { get; set; } = true;

public bool ShouldSerializeExtendPx() => false;
public bool ShouldSerializeHeightPx() => false;
public bool ShouldSerializeUseOutlineRing() => false;

public double ResolveWidthPx(double outlineWidthPx) =>
    Math.Max(WidthPx ?? (Math.Max(outlineWidthPx, 0.0) + (2.0 * Math.Max(ExtendPx, 0.0))), 0.0);

public double ResolveLineWeightPx(double shaftOutlineThicknessPx) =>
    Math.Max(LineWeightPx ?? Math.Max(shaftOutlineThicknessPx, 1.0), 0.0);
```

Retain the legacy public properties for deserialization only and mark them
`[Obsolete]` if doing so does not produce warnings in the checked-in config
tests. Update enum/config comments to say “away from the head.”

Change the checked-in block to:

```json
"DrawnPinTipCap": {
  "Style": "None",
  "WidthPx": 12.0,
  "LineWeightPx": 3.0,
  "ArcDepthPx": 3.0,
  "Color": "#FF111111"
}
```

- [ ] **Step 4: Run focused config tests and verify GREEN**

Run the Task 1 test command. Expected: all selected tests pass.

- [ ] **Step 5: Commit**

```powershell
git add Models/DrawnPinTipCapConfig.cs Tests/VisualConfigServiceTests.cs visual-config.json
git commit -m "refactor: define stroked pin tip cap config"
```

### Task 2: Replace Filled Geometry With Open Centerlines

**Files:**
- Modify: `Utilities/PinTipCapGeometry.cs`
- Modify: `Tests/PinTipCapGeometryTests.cs`

- [ ] **Step 1: Replace old tests with failing open-path tests**

Test the required geometry directly:

```csharp
[Fact]
public void BuildHorizontal_IsOpenLineCenteredOnTip()
{
    var tip = new Point(100, 200);
    var geometry = (LineGeometry)PinTipCapGeometry.BuildHorizontal(tip, widthPx: 12);

    Assert.Equal(new Point(94, 200), geometry.StartPoint);
    Assert.Equal(new Point(106, 200), geometry.EndPoint);
}

[Theory]
[InlineData(-1.0, 197.0)] // head above: endpoints above, center bows down
[InlineData( 1.0, 203.0)] // head below: endpoints below, center bows up
public void BuildConcave_FlipsEndpointsAwayFromHead(double shaftY, double endpointY)
{
    var tip = new Point(100, 200);
    var geometry = (PathGeometry)PinTipCapGeometry.BuildConcave(
        tip, new Vector(0, shaftY), widthPx: 12, arcDepthPx: 3);

    var figure = geometry.Figures.Single();
    var curve = Assert.IsType<QuadraticBezierSegment>(figure.Segments.Single());
    var midpoint = new Point(
        (0.25 * figure.StartPoint.X) + (0.5 * curve.Point1.X) + (0.25 * curve.Point2.X),
        (0.25 * figure.StartPoint.Y) + (0.5 * curve.Point1.Y) + (0.25 * curve.Point2.Y));
    Assert.False(figure.IsClosed);
    Assert.Equal(endpointY, figure.StartPoint.Y, 6);
    Assert.Equal(endpointY, curve.Point2.Y, 6);
    Assert.Equal(tip.X, midpoint.X, 6);
    Assert.Equal(tip.Y, midpoint.Y, 6);
}

[Theory]
[InlineData(0.8, -0.6, 197.0)]
[InlineData(-0.8, 0.6, 203.0)]
[InlineData(1.0, 0.00001, 197.0)]
[InlineData(0.0, 0.0, 197.0)]
public void BuildConcave_UsesVerticalHeadSideWithStableFallback(
    double shaftX, double shaftY, double endpointY)
{
    var geometry = (PathGeometry)PinTipCapGeometry.BuildConcave(
        new Point(100, 200), new Vector(shaftX, shaftY), 12, 3);

    Assert.Equal(endpointY, geometry.Figures[0].StartPoint.Y, 6);
}
```

- [ ] **Step 2: Run geometry tests and verify RED**

Run:

```powershell
dotnet test Tests\InteractiveWorldMap.Tests.csproj --filter "FullyQualifiedName~PinTipCapGeometryTests"
```

Expected: compile/test failures because the old methods return filled rectangle
and closed baseline geometry.

- [ ] **Step 3: Implement minimal open geometry**

Replace width/shape methods with:

```csharp
private const double VerticalDirectionEpsilon = 0.001;

public static Geometry BuildHorizontal(Point tip, double widthPx)
{
    double halfWidth = Math.Max(widthPx, 0.0) / 2.0;
    var geometry = new LineGeometry(
        new Point(tip.X - halfWidth, tip.Y),
        new Point(tip.X + halfWidth, tip.Y));
    geometry.Freeze();
    return geometry;
}

public static Geometry BuildConcave(
    Point tip,
    Vector shaftDir,
    double widthPx,
    double arcDepthPx)
{
    double halfWidth = Math.Max(widthPx, 0.0) / 2.0;
    double depth = Math.Max(arcDepthPx, 0.0);
    double headSide = shaftDir.Y > VerticalDirectionEpsilon ? 1.0 : -1.0;
    double endpointY = tip.Y + (headSide * depth);
    double controlY = tip.Y - (headSide * depth);

    var figure = new PathFigure
    {
        StartPoint = new Point(tip.X - halfWidth, endpointY),
        IsClosed = false
    };
    figure.Segments.Add(new QuadraticBezierSegment(
        new Point(tip.X, controlY),
        new Point(tip.X + halfWidth, endpointY),
        isStroked: true));

    var geometry = new PathGeometry();
    geometry.Figures.Add(figure);
    geometry.Freeze();
    return geometry;
}
```

Delete `HalfWidth`, `ConcaveControlPoint`, and `ConcaveMidpoint` after updating
all callers/tests; they encode the obsolete filled geometry.

- [ ] **Step 4: Run geometry tests and verify GREEN**

Run the Task 2 test command. Expected: all selected tests pass.

- [ ] **Step 5: Commit**

```powershell
git add Utilities/PinTipCapGeometry.cs Tests/PinTipCapGeometryTests.cs
git commit -m "fix: bow pin divot caps away from heads"
```

### Task 3: Render One Near-Black Stroke Per Cap

**Files:**
- Modify: `Views/DrawnPinTipCapRenderer.cs`
- Modify: `MainWindow.TipCap.partial.cs`
- Test: `Tests/DrawnPinTipCapRendererTests.cs`

- [ ] **Step 1: Write failing renderer tests**

Add or update STA WPF tests:

```csharp
[Fact]
public void Sync_RendersOpenGeometryAsSingleUnfilledStroke()
{
    var canvas = new Canvas();
    var renderer = new DrawnPinTipCapRenderer(canvas);
    var cap = new DrawnPinTipCapConfig
    {
        Style = DrawnPinTipCapStyle.Concave,
        Color = "#FF111111",
        LineWeightPx = 3.0
    };

    renderer.Sync(
        new[] { new LineGeometry(new Point(0, 0), new Point(10, 0)) },
        cap,
        new PinMarkerConfig());

    var path = Assert.IsType<Path>(Assert.Single(canvas.Children));
    Assert.Null(path.Fill);
    Assert.Equal(3.0, path.StrokeThickness);
    Assert.Equal(PenLineCap.Round, path.StrokeStartLineCap);
    Assert.Equal(PenLineCap.Round, path.StrokeEndLineCap);
}
```

The test project already targets `net6.0-windows` with `<UseWPF>true</UseWPF>`;
use the existing xUnit `[Fact]` convention.

- [ ] **Step 2: Run renderer tests and verify RED**

Run:

```powershell
dotnet test Tests\InteractiveWorldMap.Tests.csproj --filter "FullyQualifiedName~DrawnPinTipCapRendererTests"
```

Expected: FAIL because the renderer creates outline/core filled paths.

- [ ] **Step 3: Simplify the renderer to one pooled path**

Replace `CapVisual` with a pooled `List<Path>`. Configure each path as:

```csharp
path.Data = geometry;
path.Fill = null;
path.Stroke = _strokeBrush;
path.StrokeThickness = capConfig.ResolveLineWeightPx(
    pinConfig.ShaftOutlineThickness);
path.StrokeStartLineCap = PenLineCap.Round;
path.StrokeEndLineCap = PenLineCap.Round;
path.StrokeLineJoin = PenLineJoin.Round;
path.Visibility = Visibility.Visible;
Panel.SetZIndex(path, 1501);
```

Use `#FF111111` as the brush fallback. Remove outline-ring allocation, fill
assignment, and outline brush caching.

- [ ] **Step 4: Update orchestration to use total width**

Change `BuildCapGeometry`:

```csharp
double widthPx = config.ResolveWidthPx(placement.OutlineWidthPx);
if (widthPx <= 0.0)
    return null;

return config.Style switch
{
    DrawnPinTipCapStyle.Horizontal =>
        PinTipCapGeometry.BuildHorizontal(placement.TipScreen, widthPx),
    DrawnPinTipCapStyle.Concave =>
        PinTipCapGeometry.BuildConcave(
            placement.TipScreen,
            placement.ShaftDir,
            widthPx,
            config.ArcDepthPx),
    _ => null
};
```

- [ ] **Step 5: Run renderer, geometry, and architecture tests**

Run:

```powershell
dotnet test Tests\InteractiveWorldMap.Tests.csproj --filter "FullyQualifiedName~DrawnPinTipCapRendererTests|FullyQualifiedName~PinTipCapGeometryTests|FullyQualifiedName~LayerDependencyTests"
```

Expected: all selected tests pass.

- [ ] **Step 6: Commit**

```powershell
git add Views/DrawnPinTipCapRenderer.cs MainWindow.TipCap.partial.cs Tests/DrawnPinTipCapRendererTests.cs
git commit -m "refactor: render pin tip caps as dark strokes"
```

### Task 4: Wire Width And Line Weight Through Tuning

**Files:**
- Modify: `Models/TuningPanelEventArgs.cs`
- Modify: `Views/DeveloperTuningPanel.xaml`
- Modify: `Views/DeveloperTuningPanel.xaml.cs`
- Modify: `MainWindow.DeveloperTuning.partial.cs`
- Modify: `Tests/TuningPanelWiringTests.cs`
- Modify: `Tests/TuningReloadValidationTests.cs`

- [ ] **Step 1: Write failing wiring and validation tests**

Replace legacy field assertions with:

```csharp
Assert.Contains("x:Name=\"TxtTipCapWidth\"", xaml);
Assert.Contains("x:Name=\"TxtTipCapLineWeight\"", xaml);
Assert.DoesNotContain("TxtTipCapHeight", xaml);
Assert.DoesNotContain("TxtTipCapExtend", xaml);

Assert.Contains("cap.WidthPx = e.TipCapWidthPx;", source);
Assert.Contains("cap.LineWeightPx = e.TipCapLineWeightPx;", source);
Assert.Contains("cap.ArcDepthPx = e.TipCapArcDepthPx;", source);
```

Add validation cases requiring width and line weight to be strictly positive:

```csharp
[Theory]
[InlineData(0.0)]
[InlineData(-0.1)]
public void TryValidate_NonPositiveTipCapLineWeight_ReturnsFalse(double value)
{
    var args = ValidArgs();
    args.TipCapLineWeightPx = value;
    Assert.False(DeveloperTuningPanel.TryValidate(args, out var error));
    Assert.Contains("Line weight", error);
}
```

```csharp
[Theory]
[InlineData(0.0)]
[InlineData(-0.1)]
public void TryValidate_NonPositiveTipCapWidth_ReturnsFalse(double value)
{
    var args = ValidArgs();
    args.TipCapWidthPx = value;
    Assert.False(DeveloperTuningPanel.TryValidate(args, out var error));
    Assert.Contains("Cap width", error);
}
```

- [ ] **Step 2: Run Tuning tests and verify RED**

Run:

```powershell
dotnet test Tests\InteractiveWorldMap.Tests.csproj --filter "FullyQualifiedName~TuningPanelWiringTests|FullyQualifiedName~TuningReloadValidationTests"
```

Expected: FAIL because the new names and positive validation do not exist.

- [ ] **Step 3: Rename event fields and panel controls**

Use:

```csharp
public double TipCapWidthPx { get; set; }
public double TipCapLineWeightPx { get; set; }
public double TipCapArcDepthPx { get; set; }
```

Replace the two old XAML rows:

```xml
<TextBlock Grid.Row="0" Text="Width (px)" Foreground="#DDDDDD" Margin="0,3"/>
<TextBox x:Name="TxtTipCapWidth" Grid.Row="0" Grid.Column="1"
         TextChanged="OnPanelInputChanged"
         ToolTip="Total screen-space width of the divot line."/>

<TextBlock Grid.Row="1" Text="Line weight (px)" Foreground="#DDDDDD" Margin="0,3"/>
<TextBox x:Name="TxtTipCapLineWeight" Grid.Row="1" Grid.Column="1"
         TextChanged="OnPanelInputChanged"
         ToolTip="Thickness of the near-black divot line."/>
```

Update the curvature tooltip to “Endpoint rise toward the head side; the curve
center remains at the shaft tip.”

- [ ] **Step 4: Update load, apply, reload, and validation**

Load effective values using the configured shaft dimensions:

```csharp
double outlineWidth = Math.Max(config.PinMarkers.ShaftWidth, 2.5) +
    (2.0 * Math.Max(config.PinMarkers.ShaftOutlineThickness, 1.0));
TxtTipCapWidth.Text = Format(cap.ResolveWidthPx(outlineWidth));
TxtTipCapLineWeight.Text = Format(
    cap.ResolveLineWeightPx(config.PinMarkers.ShaftOutlineThickness));
```

Parse both with `TryReadPositive`, map to `TipCapWidthPx` and
`TipCapLineWeightPx`, update `TryValidate`, and apply:

```csharp
cap.Style = e.TipCapStyle;
cap.WidthPx = e.TipCapWidthPx;
cap.LineWeightPx = e.TipCapLineWeightPx;
cap.ArcDepthPx = e.TipCapArcDepthPx;
```

- [ ] **Step 5: Run Tuning tests and verify GREEN**

Run the Task 4 test command. Expected: all selected tests pass.

- [ ] **Step 6: Commit**

```powershell
git add Models/TuningPanelEventArgs.cs Views/DeveloperTuningPanel.xaml Views/DeveloperTuningPanel.xaml.cs MainWindow.DeveloperTuning.partial.cs Tests/TuningPanelWiringTests.cs Tests/TuningReloadValidationTests.cs
git commit -m "feat: tune pin divot width and line weight"
```

### Task 5: Documentation, Visual Check, And Completion Gate

**Files:**
- Modify: `docs/exec-plans/active/drawn-pin-tip-cap-plan.md`
- Modify: `docs/TO_DO.md`
- Modify: `CHANGELOG.md`

- [ ] **Step 1: Update the active exec plan and changelog**

Correct all “bows toward the shaft/head” language to “bows away from the pin
head.” Record the stroked-path implementation, perspective flip, new config
fields, and automated test evidence under the plan’s progress log and
`CHANGELOG.md` `[Unreleased]`.

- [ ] **Step 2: Narrow the backlog item before visual acceptance**

Keep only the remaining human gate:

```markdown
- Manual visual acceptance only: drawn-pin divot caps at normal and inverted
  head placement, including extension lines, hover, drag, and zoom.
```

Do not archive the active exec plan until that visual gate passes.

- [ ] **Step 3: Run focused tests**

Run:

```powershell
dotnet test Tests\InteractiveWorldMap.Tests.csproj --filter "FullyQualifiedName~PinTipCap|FullyQualifiedName~TuningPanel|FullyQualifiedName~TuningReload|FullyQualifiedName~VisualConfigService|FullyQualifiedName~LayerDependency"
```

Expected: all selected tests pass with zero failures.

- [ ] **Step 4: Run the repository verification gate**

Run:

```powershell
.\scripts\verify.ps1
```

Expected: build, tests, architecture, doc links, taste checks, and headless
startup all pass. Any pre-existing failure must be verified against `HEAD` and
documented; do not claim full completion while the gate is red.

- [ ] **Step 5: Perform visual smoke checks**

Temporarily use `Style: "Concave"` with the checked-in width/weight/depth,
launch the WPF app, and capture:

1. Normal stub with head above tip.
2. Manual-layout or dense-cluster extension with head below tip.
3. Zoomed and full-map views.

Verify the center meets the shaft tip, the cap is near-black and approximately
shaft-outline weight, and the bow flips away from the head. Restore the default
`Style: "None"` before committing.

- [ ] **Step 6: Complete bookkeeping based on evidence**

If visual acceptance passes, remove the completed `docs/TO_DO.md` bullet, mark
the exec-plan acceptance boxes complete, move the plan to
`docs/exec-plans/completed/`, and repair active registry links. If visual
acceptance remains human-only, leave the concise manual gate and active plan in
place.

- [ ] **Step 7: Commit final docs**

```powershell
git add CHANGELOG.md docs/TO_DO.md docs/exec-plans
git commit -m "docs: record drawn pin divot cap progress"
```

- [ ] **Step 8: Review final diff**

Run:

```powershell
git status --short
git diff HEAD~5 --stat
git diff --check
```

Confirm no unrelated user changes are staged or committed.
