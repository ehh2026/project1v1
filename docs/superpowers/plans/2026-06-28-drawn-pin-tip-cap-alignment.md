# Drawn Pin Tip Cap Alignment Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a config- and Tuning-selectable shaft-aligned mode in which drawn-pin divot caps rotate with the visible shaft while retaining the current screen-horizontal mode.

**Architecture:** Keep compatibility and selection in `DrawnPinTipCapConfig`, pure shaft-relative math in `PinTipCapGeometry`, and orchestration in `MainWindow.TipCap.partial.cs`. Extend the existing Tuning event flow with one enum value; do not change renderer, placement, extension-line, or manual-layout ownership.

**Tech Stack:** C# 10, .NET 6, WPF geometry, Newtonsoft.Json string enums, xUnit

**Design:** `docs/superpowers/specs/2026-06-28-drawn-pin-tip-cap-alignment-design.md`

**Progress:** Implementation and bookkeeping are complete. On 2026-06-28,
`.\scripts\verify.ps1` passed with 466 tests, zero build warnings/errors, seed
verification, doc links, taste checks, and headless startup. Intermediate and
final commit steps were not run because this shared worktree already contained
related uncommitted cap lifecycle changes and the user did not request a
commit.

---

## File Map

- Modify `Models/DrawnPinTipCapConfig.cs`: alignment enum and compatibility default.
- Modify `Utilities/PinTipCapGeometry.cs`: shaft-relative line and quadratic builders.
- Modify `MainWindow.TipCap.partial.cs`: select geometry by style and alignment.
- Modify `Models/TuningPanelEventArgs.cs`: carry alignment through the Tuning event.
- Modify `Views/DeveloperTuningPanel.xaml`: add the alignment picker.
- Modify `Views/DeveloperTuningPanel.xaml.cs`: load and emit alignment.
- Modify `MainWindow.DeveloperTuning.partial.cs`: apply and reload alignment.
- Modify `visual-config.json`: select shaft-aligned mode for visual evaluation.
- Modify `Tests/VisualConfigServiceTests.cs`: config defaults and string round-trip.
- Modify `Tests/PinTipCapGeometryTests.cs`: shaft-relative geometry and fallbacks.
- Modify `Tests/TuningPanelWiringTests.cs`: UI and event-flow source guards.
- Modify `docs/TO_DO.md`: defer the unreproduced cap-inside-head incident.
- Modify `docs/exec-plans/active/drawn-pin-tip-cap-plan.md`: record alignment implementation and remaining visual gate.
- Modify `CHANGELOG.md`: describe the new opt-in alignment mode.

No new production file is required. All touched production files remain below the
repository's 800-line limit, and responsibility boundaries remain unchanged.

### Task 1: Configuration Contract

**Files:**
- Modify: `Models/DrawnPinTipCapConfig.cs`
- Test: `Tests/VisualConfigServiceTests.cs`

- [ ] **Step 1: Write failing config tests**

Add tests that prove an omitted value remains compatible and both enum values
serialize as strings:

```csharp
[Fact]
public void Load_DrawnPinTipCap_DefaultsAlignmentToScreenHorizontal()
{
    var tempDir = CreateTempDir();
    try
    {
        var path = Path.Combine(tempDir, "visual-config.json");
        File.WriteAllText(path,
            @"{ ""PinMarkers"": { ""DrawnPinTipCap"": { ""Style"": ""Concave"" } } }");

        var cap = new VisualConfigService().Load(path).PinMarkers.DrawnPinTipCap;

        Assert.Equal(DrawnPinTipCapAlignment.ScreenHorizontal, cap.Alignment);
    }
    finally
    {
        Directory.Delete(tempDir, recursive: true);
    }
}

[Theory]
[InlineData(DrawnPinTipCapAlignment.ScreenHorizontal)]
[InlineData(DrawnPinTipCapAlignment.ShaftAligned)]
public void SaveAndReload_DrawnPinTipCap_RoundTripsAlignmentAsString(
    DrawnPinTipCapAlignment alignment)
{
    var tempDir = CreateTempDir();
    try
    {
        var path = Path.Combine(tempDir, "visual-config.json");
        var service = new VisualConfigService();
        var config = new VisualConfig();
        config.PinMarkers.DrawnPinTipCap.Alignment = alignment;

        service.Save(config, path);
        var json = File.ReadAllText(path);
        var reloaded = service.Load(path);

        Assert.Contains($"\"{alignment}\"", json);
        Assert.Equal(alignment, reloaded.PinMarkers.DrawnPinTipCap.Alignment);
    }
    finally
    {
        Directory.Delete(tempDir, recursive: true);
    }
}
```

- [ ] **Step 2: Run tests and verify RED**

Run:

```powershell
dotnet test Tests\InteractiveWorldMap.Tests.csproj --filter "FullyQualifiedName~VisualConfigServiceTests" --no-restore
```

Expected: compile failure because `DrawnPinTipCapAlignment` and `Alignment` do
not exist.

- [ ] **Step 3: Add the enum and property**

In `Models/DrawnPinTipCapConfig.cs`, add:

```csharp
[JsonConverter(typeof(StringEnumConverter))]
public enum DrawnPinTipCapAlignment
{
    ScreenHorizontal,
    ShaftAligned
}
```

Then add to `DrawnPinTipCapConfig`:

```csharp
/// <summary>How the cap width axis is oriented in screen space.</summary>
public DrawnPinTipCapAlignment Alignment { get; set; } =
    DrawnPinTipCapAlignment.ScreenHorizontal;
```

- [ ] **Step 4: Run tests and verify GREEN**

Run the command from Step 2.

Expected: all `VisualConfigServiceTests` pass.

- [ ] **Step 5: Commit the config contract**

```powershell
git add Models\DrawnPinTipCapConfig.cs Tests\VisualConfigServiceTests.cs
git commit -m "feat: configure drawn pin tip cap alignment"
```

### Task 2: Shaft-Relative Geometry

**Files:**
- Modify: `Utilities/PinTipCapGeometry.cs`
- Modify: `MainWindow.TipCap.partial.cs`
- Test: `Tests/PinTipCapGeometryTests.cs`

- [ ] **Step 1: Write failing geometry tests**

Add helpers that inspect line endpoints and quadratic points, then cover diagonal
line orientation, diagonal concavity, horizontal shafts, inverted shafts, and
invalid vectors:

```csharp
[Fact]
public void BuildShaftAlignedLine_IsPerpendicularToShaft()
{
    var shaftDir = new Vector(3, 4);
    var geometry = Assert.IsType<LineGeometry>(
        PinTipCapGeometry.BuildShaftAlignedLine(
            new Point(100, 200), shaftDir, widthPx: 10));
    var capDir = geometry.EndPoint - geometry.StartPoint;

    Assert.Equal(0.0, Vector.Multiply(shaftDir, capDir), 6);
    Assert.Equal(10.0, capDir.Length, 6);
}

[Theory]
[InlineData(3.0, 4.0)]
[InlineData(-3.0, -4.0)]
[InlineData(1.0, 0.0)]
[InlineData(0.0, -1.0)]
public void BuildShaftAlignedConcave_BowsAwayFromHeadAndKeepsTipAtMidpoint(
    double shaftX,
    double shaftY)
{
    var tip = new Point(100, 200);
    var shaftDir = new Vector(shaftX, shaftY);
    shaftDir.Normalize();

    var geometry = Assert.IsType<PathGeometry>(
        PinTipCapGeometry.BuildShaftAlignedConcave(
            tip, new Vector(shaftX, shaftY), widthPx: 12, arcDepthPx: 3));
    var figure = Assert.Single(geometry.Figures);
    var curve = Assert.IsType<QuadraticBezierSegment>(
        Assert.Single(figure.Segments));
    var midpoint = QuadraticPoint(
        figure.StartPoint, curve.Point1, curve.Point2, 0.5);
    var endpointOffset = figure.StartPoint - tip;

    Assert.True(Vector.Multiply(endpointOffset, shaftDir) > 0);
    Assert.Equal(tip.X, midpoint.X, 6);
    Assert.Equal(tip.Y, midpoint.Y, 6);
}

[Theory]
[InlineData(0.0, 0.0)]
[InlineData(double.NaN, 1.0)]
[InlineData(double.PositiveInfinity, 1.0)]
public void BuildShaftAlignedConcave_InvalidDirectionUsesUpwardFallback(
    double shaftX,
    double shaftY)
{
    var geometry = Assert.IsType<PathGeometry>(
        PinTipCapGeometry.BuildShaftAlignedConcave(
            new Point(100, 200),
            new Vector(shaftX, shaftY),
            widthPx: 12,
            arcDepthPx: 3));

    Assert.Equal(197.0, geometry.Figures[0].StartPoint.Y, 6);
}
```

- [ ] **Step 2: Run tests and verify RED**

Run:

```powershell
dotnet test Tests\InteractiveWorldMap.Tests.csproj --filter "FullyQualifiedName~PinTipCapGeometryTests" --no-restore
```

Expected: compile failure because the two shaft-aligned builders do not exist.

- [ ] **Step 3: Implement normalized shaft-relative basis math**

In `Utilities/PinTipCapGeometry.cs`, add a private helper:

```csharp
private const double DirectionLengthEpsilon = 0.000001;

private static Vector ResolveHeadDirection(Vector shaftDir)
{
    if (!double.IsFinite(shaftDir.X) ||
        !double.IsFinite(shaftDir.Y) ||
        shaftDir.LengthSquared < DirectionLengthEpsilon * DirectionLengthEpsilon)
    {
        return new Vector(0.0, -1.0);
    }

    shaftDir.Normalize();
    return shaftDir;
}
```

Add the straight builder:

```csharp
public static Geometry BuildShaftAlignedLine(
    Point tip,
    Vector shaftDir,
    double widthPx)
{
    Vector headDir = ResolveHeadDirection(shaftDir);
    var widthDir = new Vector(-headDir.Y, headDir.X);
    double halfWidth = Math.Max(widthPx, 0.0) / 2.0;
    var geometry = new LineGeometry(
        tip - (widthDir * halfWidth),
        tip + (widthDir * halfWidth));
    geometry.Freeze();
    return geometry;
}
```

Add the curve builder:

```csharp
public static Geometry BuildShaftAlignedConcave(
    Point tip,
    Vector shaftDir,
    double widthPx,
    double arcDepthPx)
{
    Vector headDir = ResolveHeadDirection(shaftDir);
    var widthDir = new Vector(-headDir.Y, headDir.X);
    double halfWidth = Math.Max(widthPx, 0.0) / 2.0;
    double depth = Math.Max(arcDepthPx, 0.0);
    Point endpointCenter = tip + (headDir * depth);

    var figure = new PathFigure
    {
        StartPoint = endpointCenter - (widthDir * halfWidth),
        IsClosed = false
    };
    figure.Segments.Add(new QuadraticBezierSegment(
        tip - (headDir * depth),
        endpointCenter + (widthDir * halfWidth),
        isStroked: true));

    var geometry = new PathGeometry();
    geometry.Figures.Add(figure);
    geometry.Freeze();
    return geometry;
}
```

- [ ] **Step 4: Select geometry in the orchestrator**

Update `BuildCapGeometry` in `MainWindow.TipCap.partial.cs`:

```csharp
if (config.Alignment == DrawnPinTipCapAlignment.ShaftAligned)
{
    return config.Style switch
    {
        DrawnPinTipCapStyle.Horizontal =>
            PinTipCapGeometry.BuildShaftAlignedLine(
                placement.TipScreen,
                placement.ShaftDir,
                widthPx),
        DrawnPinTipCapStyle.Concave =>
            PinTipCapGeometry.BuildShaftAlignedConcave(
                placement.TipScreen,
                placement.ShaftDir,
                widthPx,
                config.ArcDepthPx),
        _ => null
    };
}
```

Leave the existing screen-horizontal switch unchanged after this branch.

- [ ] **Step 5: Run geometry and cap tests and verify GREEN**

Run:

```powershell
dotnet test Tests\InteractiveWorldMap.Tests.csproj --filter "FullyQualifiedName~PinTipCap" --no-restore
```

Expected: all pin-tip-cap geometry, renderer, placement, and lifecycle tests pass.

- [ ] **Step 6: Commit geometry**

```powershell
git add Utilities\PinTipCapGeometry.cs MainWindow.TipCap.partial.cs Tests\PinTipCapGeometryTests.cs
git commit -m "feat: align drawn pin tip caps with shafts"
```

### Task 3: Runtime Tuning Wiring

**Files:**
- Modify: `Models/TuningPanelEventArgs.cs`
- Modify: `Views/DeveloperTuningPanel.xaml`
- Modify: `Views/DeveloperTuningPanel.xaml.cs`
- Modify: `MainWindow.DeveloperTuning.partial.cs`
- Modify: `visual-config.json`
- Test: `Tests/TuningPanelWiringTests.cs`

- [ ] **Step 1: Write failing Tuning wiring tests**

Extend the tooltip control list with `CmbTipCapAlignment`, then add:

```csharp
[Fact]
public void DeveloperTuningPanel_TipCapAlignmentCombo_HasBothModes()
{
    var xaml = File.ReadAllText(
        Path.Combine(RepoRoot, "Views", "DeveloperTuningPanel.xaml"));

    Assert.Contains("x:Name=\"CmbTipCapAlignment\"", xaml);
    Assert.Contains("<ComboBoxItem Content=\"ScreenHorizontal\"/>", xaml);
    Assert.Contains("<ComboBoxItem Content=\"ShaftAligned\"/>", xaml);
}

[Fact]
public void ApplyTuning_MapsTipCapAlignment()
{
    var source = File.ReadAllText(
        Path.Combine(RepoRoot, "MainWindow.DeveloperTuning.partial.cs"));

    Assert.Contains("cap.Alignment = e.TipCapAlignment;", source);
    Assert.Contains("TipCapAlignment = cap.Alignment", source);
}
```

Also extend `ApplyTuning_MapsTipCapFieldsToDrawnPinTipCapConfig` to require the
alignment assignment.

- [ ] **Step 2: Run tests and verify RED**

Run:

```powershell
dotnet test Tests\InteractiveWorldMap.Tests.csproj --filter "FullyQualifiedName~TuningPanelWiringTests" --no-restore
```

Expected: failures for the missing combo and missing alignment mappings.

- [ ] **Step 3: Carry alignment in the event model**

Add to `Models/TuningPanelEventArgs.cs`:

```csharp
public DrawnPinTipCapAlignment TipCapAlignment { get; set; } =
    DrawnPinTipCapAlignment.ScreenHorizontal;
```

- [ ] **Step 4: Add the alignment picker**

In `Views/DeveloperTuningPanel.xaml`, add beneath the Style combo:

```xml
<TextBlock Text="Alignment"
           Foreground="#AAAAAA"
           FontSize="10"/>
<ComboBox x:Name="CmbTipCapAlignment"
          Margin="0,1,0,5"
          IsEditable="False"
          Background="#222222"
          Foreground="White"
          SelectionChanged="OnVariantSelectionChanged"
          ItemContainerStyle="{StaticResource DarkComboBoxItemStyle}"
          ToolTip="Keep the cap horizontal on screen or rotate it perpendicular to the visible shaft.">
    <ComboBoxItem Content="ScreenHorizontal"/>
    <ComboBoxItem Content="ShaftAligned"/>
</ComboBox>
```

- [ ] **Step 5: Load and emit the selected enum**

Generalize the existing tip-cap style combo helpers or add parallel
`SetTipCapAlignment` and `GetTipCapAlignment` helpers using the same
`Enum.TryParse` pattern.

In `LoadValues`:

```csharp
SetTipCapAlignment(cap.Alignment);
```

In `TryBuildEventArgs`:

```csharp
TipCapAlignment = GetTipCapAlignment(),
```

- [ ] **Step 6: Apply and reload alignment**

In `ApplyTuningAsync`:

```csharp
cap.Alignment = e.TipCapAlignment;
```

In `CreateTuningArgs`:

```csharp
TipCapAlignment = cap.Alignment,
```

- [ ] **Step 7: Select the experimental checked-in mode**

Add to `visual-config.json` under `DrawnPinTipCap`:

```json
"Alignment": "ShaftAligned",
```

Do not change `"Style": "None"`; cap rendering remains default-off.

- [ ] **Step 8: Run focused Tuning and config tests and verify GREEN**

Run:

```powershell
dotnet test Tests\InteractiveWorldMap.Tests.csproj --filter "FullyQualifiedName~TuningPanelWiringTests|FullyQualifiedName~TuningReloadValidationTests|FullyQualifiedName~VisualConfigServiceTests" --no-restore
```

Expected: all selected tests pass.

- [ ] **Step 9: Commit Tuning wiring**

```powershell
git add Models\TuningPanelEventArgs.cs Views\DeveloperTuningPanel.xaml Views\DeveloperTuningPanel.xaml.cs MainWindow.DeveloperTuning.partial.cs visual-config.json Tests\TuningPanelWiringTests.cs
git commit -m "feat: tune drawn pin tip cap alignment"
```

### Task 4: Completion Bookkeeping And Verification

**Files:**
- Modify: `docs/TO_DO.md`
- Modify: `docs/exec-plans/active/drawn-pin-tip-cap-plan.md`
- Modify: `CHANGELOG.md`
- Modify: `docs/superpowers/plans/2026-06-28-drawn-pin-tip-cap-alignment.md`

- [ ] **Step 1: Defer the unreproduced incident**

Move the Japan/China cap-inside-head bullet to the existing Deferred/Inactive
section. Keep it concise:

```markdown
- Intermittent divot cap inside a stub-looking pin head near Japan/China:
  currently not reproducible after stale-cap refresh and head-layer safeguards;
  revisit if observed again.
```

Keep the broader visual acceptance bullet active and narrow it to the remaining
screen-horizontal versus shaft-aligned visual comparison and interaction smoke.

- [ ] **Step 2: Update plan and changelog**

In `docs/exec-plans/active/drawn-pin-tip-cap-plan.md`, record:

- alignment enum and compatibility default;
- shaft-relative geometry and fallback;
- Tuning/config support;
- focused automated coverage;
- remaining manual visual comparison.

Keep the plan active because visual acceptance remains open.

Under `[Unreleased]` in `CHANGELOG.md`, add a concise user-visible entry for the
selectable shaft-aligned mode.

- [ ] **Step 3: Mark this implementation plan complete**

Check every completed step in this file. Do not archive the broader
`drawn-pin-tip-cap-plan.md` while its visual gates remain open.

- [ ] **Step 4: Run full repository verification**

Run:

```powershell
.\scripts\verify.ps1
```

Expected:

- restore and vulnerability scan pass;
- build succeeds with zero errors;
- all xUnit tests pass;
- manual-layout seed verification passes;
- doc-link and taste checks pass;
- headless startup validation passes.

- [ ] **Step 5: Review the final diff**

Run:

```powershell
git diff --check
git status --short
```

Confirm no unrelated files were reverted and no generated artifacts were added.

- [ ] **Step 6: Commit bookkeeping**

```powershell
git add docs\TO_DO.md docs\exec-plans\active\drawn-pin-tip-cap-plan.md docs\superpowers\plans\2026-06-28-drawn-pin-tip-cap-alignment.md CHANGELOG.md
git commit -m "docs: record shaft-aligned pin tip caps"
```
