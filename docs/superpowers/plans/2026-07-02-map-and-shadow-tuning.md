# Map and Shadow Runtime Tuning Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Complete the Map tuning controls and add independently configurable, live-applied pin and cluster-marker shadows.

**Architecture:** Extend the existing `VisualConfig` → `TuningPanelEventArgs` → `DeveloperTuningPanel` → `MainWindow.DeveloperTuning` data path. Marker views own applying/removing their WPF effects, while `MainWindow` classifies recreate-class map changes and render-only shadow changes and refreshes the active view without losing manual layouts.

**Tech Stack:** .NET 6, C#, WPF/XAML, `System.Text.Json`, xUnit, existing runtime-tuning and marker-refresh infrastructure.

**Design:** `docs/superpowers/specs/2026-07-02-map-and-shadow-tuning-design.md`

---

## File Structure and Responsibilities

| File | Responsibility in this change |
|---|---|
| `Models/ClusterMarkerShadowConfig.cs` | New persisted cluster-shadow enabled/opacity values. |
| `Models/VisualConfig.cs` | Own the new cluster-shadow object alongside existing map values. |
| `Models/TuningPanelEventArgs.cs` | Carry all new Map and Shadows values through Apply/Reload. |
| `Views/DeveloperTuningPanel.xaml` | Add Map fields and the fifth Shadows section. |
| `Views/DeveloperTuningPanel.xaml.cs` | Load, parse, validate, and emit the new values. |
| `MainWindow.xaml` | Add Shadows to the Tuning category menu. |
| `Views/PinHead.xaml(.cs)` | Remove hardcoded effect and apply pin shadow config. |
| `Views/CompositePinMarker.xaml(.cs)` | Apply/remove the shared pin-head shadow at render time. |
| `Views/ClusterMarker.xaml(.cs)` | Apply/remove cluster body and badge shadows. |
| `Views/ExtensionLineRenderer.cs` | Honor exact pin opacity with no hidden floor. |
| `MainWindow.CompositePins.partial.cs` | Pass pin shadow values when constructing composite visuals. |
| `MainWindow.DeveloperTuning.partial.cs` | Map, classify, mutate, and live-refresh all new values. |
| `Tests/VisualConfigServiceTests.cs` | Prove cluster-shadow defaults and persistence. |
| `Tests/TuningPanelWiringTests.cs` | Prove category, controls, mapping, and refresh wiring. |
| `Tests/TuningReloadValidationTests.cs` | Prove numeric validation for reload and Apply. |
| `Tests/MarkerShadowRenderingTests.cs` | Prove effect application/removal and exact opacity. |
| `visual-config.json` | Record the preferred disabled cluster-shadow setting. |
| `docs/guides/VISUAL_CONFIG.md` | Document map and shadow fields and runtime behavior. |
| `docs/TO_DO.md`, `CHANGELOG.md` | Completion bookkeeping required by `AGENTS.md`. |

`MainWindow.DeveloperTuning.partial.cs` is currently about 389 lines and
`DeveloperTuningPanel.xaml.cs` about 412 lines. This plan adds focused mapping
and classification only; neither should approach the 800-line limit. Shadow
effect creation stays in marker views rather than enlarging the composition
root.

---

### Task 1: Add Persisted Cluster-Shadow Configuration

**Files:**
- Create: `Models/ClusterMarkerShadowConfig.cs`
- Modify: `Models/VisualConfig.cs`
- Modify: `visual-config.json`
- Test: `Tests/VisualConfigServiceTests.cs`

- [ ] **Step 1: Write failing default and round-trip tests**

Add to `Tests/VisualConfigServiceTests.cs`:

```csharp
[Fact]
public void Load_ClusterMarkerShadow_UsesDisabledDefaultsWhenOmitted()
{
    var tempDir = CreateTempDir();
    try
    {
        var path = Path.Combine(tempDir, "visual-config.json");
        File.WriteAllText(path, "{}");

        var shadow = new VisualConfigService().Load(path).ClusterMarkerShadow;

        Assert.False(shadow.Enabled);
        Assert.Equal(0.0, shadow.Opacity);
    }
    finally
    {
        Directory.Delete(tempDir, recursive: true);
    }
}

[Fact]
public void SaveAndReload_ClusterMarkerShadow_RoundTrips()
{
    var tempDir = CreateTempDir();
    try
    {
        var path = Path.Combine(tempDir, "visual-config.json");
        var service = new VisualConfigService();
        var config = new VisualConfig
        {
            ClusterMarkerShadow = new ClusterMarkerShadowConfig
            {
                Enabled = true,
                Opacity = 0.65
            }
        };

        service.Save(config, path);
        var reloaded = service.Load(path);

        Assert.True(reloaded.ClusterMarkerShadow.Enabled);
        Assert.Equal(0.65, reloaded.ClusterMarkerShadow.Opacity);
    }
    finally
    {
        Directory.Delete(tempDir, recursive: true);
    }
}
```

- [ ] **Step 2: Run the focused tests and verify they fail**

Run:

```powershell
dotnet test Tests\InteractiveWorldMap.Tests.csproj --filter "Load_ClusterMarkerShadow_UsesDisabledDefaultsWhenOmitted|SaveAndReload_ClusterMarkerShadow_RoundTrips"
```

Expected: FAIL because `ClusterMarkerShadowConfig` and
`VisualConfig.ClusterMarkerShadow` do not exist.

- [ ] **Step 3: Add the focused model and VisualConfig property**

Create `Models/ClusterMarkerShadowConfig.cs`:

```csharp
namespace InteractiveWorldMap.Models;

/// <summary>
/// Runtime shadow settings for aggregate cluster marker bodies and badges.
/// </summary>
public sealed class ClusterMarkerShadowConfig
{
    public bool Enabled { get; set; } = false;
    public double Opacity { get; set; } = 0.0;
}
```

Add to `VisualConfig` near the other cluster-marker properties:

```csharp
/// <summary>
/// Shadow settings shared by the cluster marker body and count badge.
/// </summary>
public ClusterMarkerShadowConfig ClusterMarkerShadow { get; set; } =
    new ClusterMarkerShadowConfig();
```

Do not clamp opacity in the model. The tuning Apply/Reload path must reject
invalid values rather than silently rewriting them.

- [ ] **Step 4: Record the preferred checked-in setting**

Add beside the existing cluster marker values in `visual-config.json`:

```json
"ClusterMarkerShadow": {
  "Enabled": false,
  "Opacity": 0.0
},
```

- [ ] **Step 5: Run model tests**

Run:

```powershell
dotnet test Tests\InteractiveWorldMap.Tests.csproj --filter "FullyQualifiedName~VisualConfigServiceTests"
```

Expected: PASS.

- [ ] **Step 6: Commit the model slice**

```powershell
git add Models/ClusterMarkerShadowConfig.cs Models/VisualConfig.cs visual-config.json Tests/VisualConfigServiceTests.cs
git commit -m "feat: configure cluster marker shadows"
```

---

### Task 2: Complete the Map Category and Add Shadows UI

**Files:**
- Modify: `Models/TuningPanelEventArgs.cs`
- Modify: `Views/DeveloperTuningPanel.xaml`
- Modify: `Views/DeveloperTuningPanel.xaml.cs`
- Modify: `MainWindow.xaml`
- Test: `Tests/TuningPanelWiringTests.cs`
- Test: `Tests/TuningReloadValidationTests.cs`

- [ ] **Step 1: Write failing category and control-presence tests**

Update the category expectations in `Tests/TuningPanelWiringTests.cs` to include
`ShadowsSection` and `"Shadows"`, then add:

```csharp
[Fact]
public void DeveloperTuningPanel_MapAndShadowControlsArePresent()
{
    var xaml = File.ReadAllText(
        Path.Combine(RepoRoot, "Views", "DeveloperTuningPanel.xaml"));

    foreach (var name in new[]
    {
        "TxtClusterBadgeSize",
        "TxtClusterCountFontSize",
        "TxtZoomScale",
        "TxtAnimationDurationMs",
        "ChkPinShadowEnabled",
        "TxtPinShadowOpacity",
        "ChkClusterShadowEnabled",
        "TxtClusterShadowOpacity",
        "ShadowsSection"
    })
    {
        Assert.Contains($"x:Name=\"{name}\"", xaml);
    }
}

[Fact]
public void MainWindow_TuningMenuIncludesShadows()
{
    var xaml = File.ReadAllText(Path.Combine(RepoRoot, "MainWindow.xaml"));
    Assert.Contains(
        "<MenuItem Header=\"Shadows\" Tag=\"Shadows\" Click=\"OnTuningCategoryClick\"/>",
        xaml);
}
```

- [ ] **Step 2: Write failing validation tests**

Extend `ValidArgs()` in `Tests/TuningReloadValidationTests.cs`:

```csharp
ClusterBadgeSize = 12,
ClusterCountFontSize = 11,
ZoomScale = 55,
AnimationDurationMs = 390,
PinShadowOpacity = 0.55,
ClusterShadowOpacity = 0.0,
```

Add:

```csharp
[Theory]
[InlineData(0.0)]
[InlineData(-1.0)]
[InlineData(double.NaN)]
[InlineData(double.PositiveInfinity)]
public void TryValidate_InvalidZoomScale_ReturnsFalse(double value)
{
    var args = ValidArgs();
    args.ZoomScale = value;
    Assert.False(DeveloperTuningPanel.TryValidate(args, out var error));
    Assert.Contains("Zoom scale", error);
}

[Theory]
[InlineData(-0.01)]
[InlineData(1.01)]
[InlineData(double.NaN)]
[InlineData(double.PositiveInfinity)]
public void TryValidate_InvalidShadowOpacity_ReturnsFalse(double value)
{
    var pinArgs = ValidArgs();
    pinArgs.PinShadowOpacity = value;
    Assert.False(DeveloperTuningPanel.TryValidate(pinArgs, out var pinError));
    Assert.Contains("Pin shadow opacity", pinError);

    var clusterArgs = ValidArgs();
    clusterArgs.ClusterShadowOpacity = value;
    Assert.False(DeveloperTuningPanel.TryValidate(clusterArgs, out var clusterError));
    Assert.Contains("Cluster shadow opacity", clusterError);
}

[Theory]
[InlineData(0.0)]
[InlineData(1.0)]
public void TryValidate_ShadowOpacityBoundaries_ReturnTrue(double value)
{
    var args = ValidArgs();
    args.PinShadowOpacity = value;
    args.ClusterShadowOpacity = value;
    Assert.True(DeveloperTuningPanel.TryValidate(args, out var error), error);
}

[Fact]
public void TryValidate_NonPositiveAnimationDuration_ReturnsFalse()
{
    var args = ValidArgs();
    args.AnimationDurationMs = 0;
    Assert.False(DeveloperTuningPanel.TryValidate(args, out var error));
    Assert.Contains("Animation duration", error);
}
```

Add equivalent non-positive tests for `ClusterBadgeSize` and
`ClusterCountFontSize`.

- [ ] **Step 3: Run the focused tests and verify they fail**

Run:

```powershell
dotnet test Tests\InteractiveWorldMap.Tests.csproj --filter "DeveloperTuningPanel_MapAndShadowControlsArePresent|MainWindow_TuningMenuIncludesShadows|TryValidate_InvalidZoomScale|TryValidate_InvalidShadowOpacity|TryValidate_ShadowOpacityBoundaries|TryValidate_NonPositiveAnimationDuration"
```

Expected: FAIL because the category, controls, event fields, and validation do
not exist.

- [ ] **Step 4: Extend the event contract**

Add these properties to `TuningPanelEventArgs`:

```csharp
public double ClusterBadgeSize { get; set; }
public double ClusterCountFontSize { get; set; }
public double ZoomScale { get; set; }
public int AnimationDurationMs { get; set; }
public bool PinShadowEnabled { get; set; }
public double PinShadowOpacity { get; set; }
public bool ClusterShadowEnabled { get; set; }
public double ClusterShadowOpacity { get; set; }
```

- [ ] **Step 5: Add the fifth category and XAML controls**

Add `Shadows` to `TuningCategory`. Extend `ShowCategory` with:

```csharp
ShadowsSection.Visibility = category == TuningCategory.Shadows
    ? Visibility.Visible : Visibility.Collapsed;
```

Add to `MainWindow.xaml` after Hitboxes:

```xml
<MenuItem Header="Shadows" Tag="Shadows" Click="OnTuningCategoryClick"/>
```

Expand the Map grid with four rows and controls:

```xml
<TextBlock Grid.Row="3" Text="Cluster badge" Style="{StaticResource TuningLabelStyle}"/>
<TextBox x:Name="TxtClusterBadgeSize" Grid.Row="3" Grid.Column="1"
         TextChanged="OnPanelInputChanged"
         ToolTip="Diameter of the count badge on aggregate cluster markers."/>
<TextBlock Grid.Row="4" Text="Cluster count font" Style="{StaticResource TuningLabelStyle}"/>
<TextBox x:Name="TxtClusterCountFontSize" Grid.Row="4" Grid.Column="1"
         TextChanged="OnPanelInputChanged"
         ToolTip="Font size of the location count inside the cluster badge."/>
<TextBlock Grid.Row="5" Text="Zoom scale" Style="{StaticResource TuningLabelStyle}"/>
<TextBox x:Name="TxtZoomScale" Grid.Row="5" Grid.Column="1"
         TextChanged="OnPanelInputChanged"
         ToolTip="Magnification used for the settled cluster zoom view."/>
<TextBlock Grid.Row="6" Text="Animation (ms)" Style="{StaticResource TuningLabelStyle}"/>
<TextBox x:Name="TxtAnimationDurationMs" Grid.Row="6" Grid.Column="1"
         TextChanged="OnPanelInputChanged"
         ToolTip="Duration in milliseconds for subsequent zoom and back animations."/>
```

Declare all seven `RowDefinition` entries. Add a `ShadowsSection` before the
shared footer:

```xml
<StackPanel x:Name="ShadowsSection" Visibility="Collapsed">
    <TextBlock Text="Pin shadows" Foreground="#CCCCCC"
               FontSize="11" FontWeight="SemiBold"/>
    <CheckBox x:Name="ChkPinShadowEnabled" Content="Enabled"
              Foreground="White" Click="OnPanelInputChanged"
              ToolTip="Controls drawn pin heads, drawn extended shafts, and composite pin heads."/>
    <TextBlock Text="Opacity" Style="{StaticResource TuningLabelStyle}"/>
    <TextBox x:Name="TxtPinShadowOpacity" TextChanged="OnPanelInputChanged"
             ToolTip="Shared pin-shadow opacity from 0.0 through 1.0."/>

    <TextBlock Text="Cluster shadows" Foreground="#CCCCCC"
               FontSize="11" FontWeight="SemiBold" Margin="0,10,0,0"/>
    <CheckBox x:Name="ChkClusterShadowEnabled" Content="Enabled"
              Foreground="White" Click="OnPanelInputChanged"
              ToolTip="Controls both the aggregate cluster body and its count badge."/>
    <TextBlock Text="Opacity" Style="{StaticResource TuningLabelStyle}"/>
    <TextBox x:Name="TxtClusterShadowOpacity" TextChanged="OnPanelInputChanged"
             ToolTip="Shared cluster body/badge opacity from 0.0 through 1.0."/>
</StackPanel>
```

- [ ] **Step 6: Load, parse, and emit the values**

In `LoadValues`, assign:

```csharp
TxtClusterBadgeSize.Text = Format(config.ClusterBadgeSize);
TxtClusterCountFontSize.Text = Format(config.ClusterCountFontSize);
TxtZoomScale.Text = Format(config.ZoomScale);
TxtAnimationDurationMs.Text =
    config.AnimationDurationMs.ToString(CultureInfo.InvariantCulture);
ChkPinShadowEnabled.IsChecked = pinConfig.ShowShadow;
TxtPinShadowOpacity.Text = Format(pinConfig.ShadowOpacity);
var clusterShadow = config.ClusterMarkerShadow ?? new ClusterMarkerShadowConfig();
ChkClusterShadowEnabled.IsChecked = clusterShadow.Enabled;
TxtClusterShadowOpacity.Text = Format(clusterShadow.Opacity);
```

Add parsing using `TryReadPositive` for the three double Map values,
`TryReadPositiveInt` for animation duration, and `TryReadOpacity` for both
opacities:

```csharp
private static bool TryReadPositiveInt(
    string text, string label, out int value, out string error)
{
    if (!int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value)
        || value <= 0)
    {
        error = $"{label} must be a positive integer.";
        return false;
    }

    error = string.Empty;
    return true;
}

private static bool TryReadOpacity(
    string text, string label, out double value, out string error)
{
    if (!TryReadDouble(text, label, out value, out error))
        return false;
    if (value < 0 || value > 1)
    {
        error = $"{label} must be between 0 and 1.";
        return false;
    }

    return true;
}
```

Populate every new `TuningPanelEventArgs` property from the parsed values and
checkboxes.

- [ ] **Step 7: Extend reload validation**

Add checks to `TryValidate`:

```csharp
if (args.ClusterBadgeSize <= 0 || !double.IsFinite(args.ClusterBadgeSize))
{ error = "Cluster badge size must be > 0 and finite."; return false; }
if (args.ClusterCountFontSize <= 0 || !double.IsFinite(args.ClusterCountFontSize))
{ error = "Cluster count font size must be > 0 and finite."; return false; }
if (args.ZoomScale <= 0 || !double.IsFinite(args.ZoomScale))
{ error = "Zoom scale must be > 0 and finite."; return false; }
if (args.AnimationDurationMs <= 0)
{ error = "Animation duration must be a positive integer."; return false; }
if (!IsValidOpacity(args.PinShadowOpacity))
{ error = "Pin shadow opacity must be between 0 and 1 and finite."; return false; }
if (!IsValidOpacity(args.ClusterShadowOpacity))
{ error = "Cluster shadow opacity must be between 0 and 1 and finite."; return false; }
```

Add:

```csharp
private static bool IsValidOpacity(double value) =>
    double.IsFinite(value) && value >= 0 && value <= 1;
```

- [ ] **Step 8: Run tuning-panel and validation tests**

Run:

```powershell
dotnet test Tests\InteractiveWorldMap.Tests.csproj --filter "FullyQualifiedName~TuningPanelWiringTests|FullyQualifiedName~TuningReloadValidationTests"
```

Expected: PASS.

- [ ] **Step 9: Commit the UI contract**

```powershell
git add Models/TuningPanelEventArgs.cs Views/DeveloperTuningPanel.xaml Views/DeveloperTuningPanel.xaml.cs MainWindow.xaml Tests/TuningPanelWiringTests.cs Tests/TuningReloadValidationTests.cs
git commit -m "feat: add map and shadow tuning controls"
```

---

### Task 3: Make Marker Views Own Configurable Shadows

**Files:**
- Modify: `Views/PinHead.xaml`
- Modify: `Views/PinHead.xaml.cs`
- Modify: `Views/CompositePinMarker.xaml`
- Modify: `Views/CompositePinMarker.xaml.cs`
- Modify: `Views/ClusterMarker.xaml`
- Modify: `Views/ClusterMarker.xaml.cs`
- Modify: `Views/ExtensionLineRenderer.cs`
- Create: `Tests/MarkerShadowRenderingTests.cs`

- [ ] **Step 1: Write failing view-level shadow tests**

Create `Tests/MarkerShadowRenderingTests.cs`. The repository does not carry an
STA xUnit extension, so keep these as structural wiring tests rather than
adding a test-only package:

```csharp
using System.IO;
using Xunit;

namespace InteractiveWorldMap.Tests;

public class MarkerShadowRenderingTests
{
    private static string RepoRoot =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));

    [Fact]
    public void PinHead_AppliesConfiguredOpacityAndRemovesDisabledShadow()
    {
        var source = File.ReadAllText(
            Path.Combine(RepoRoot, "Views", "PinHead.xaml.cs"));
        Assert.Contains("Opacity = config.ShadowOpacity", source);
        Assert.Contains(": null", source);
    }

    [Fact]
    public void ClusterMarker_AppliesOneConfigToBodyAndBadge()
    {
        var source = File.ReadAllText(
            Path.Combine(RepoRoot, "Views", "ClusterMarker.xaml.cs"));
        Assert.Contains("StampImage.Effect = config.Enabled", source);
        Assert.Contains("BadgeEllipse.Effect = config.Enabled", source);
        Assert.Equal(2, Count(source, "config.Opacity"));
    }

    [Fact]
    public void ExtensionLineRenderer_DoesNotFloorConfiguredShadowOpacity()
    {
        var source = File.ReadAllText(
            Path.Combine(RepoRoot, "Views", "ExtensionLineRenderer.cs"));
        Assert.Contains("Opacity = pinConfig.ShadowOpacity", source);
        Assert.DoesNotContain("Math.Max(pinConfig.ShadowOpacity, 0.45)", source);
    }

    private static int Count(string source, string value)
    {
        var count = 0;
        var index = 0;
        while ((index = source.IndexOf(value, index, System.StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += value.Length;
        }
        return count;
    }
}
```

- [ ] **Step 2: Run the new tests and verify they fail**

Run:

```powershell
dotnet test Tests\InteractiveWorldMap.Tests.csproj --filter "FullyQualifiedName~MarkerShadowRenderingTests"
```

Expected: FAIL because the public effect accessors/application methods do not
exist and the opacity floor remains.

- [ ] **Step 3: Make PinHead apply the shared pin-shadow config**

Remove `<Ellipse.Effect>` from `PinHead.xaml`. In `PinHead.xaml.cs`, expose a
read-only test seam and apply the effect inside `ApplyConfig`:

```csharp
using System.Windows.Media.Effects;

public Effect? PinBallEffect => PinBall.Effect;

private void ApplyShadow(PinMarkerConfig config)
{
    PinBall.Effect = config.ShowShadow
        ? new DropShadowEffect
        {
            Color = Colors.Black,
            Direction = 315,
            ShadowDepth = 1.5,
            BlurRadius = 2.5,
            Opacity = config.ShadowOpacity
        }
        : null;
}
```

Call `ApplyShadow(config)` from `ApplyConfig` after size/stroke application.

- [ ] **Step 4: Make CompositePinMarker apply the same pin-shadow values**

Remove the hardcoded `HeadImage.Effect` block from
`CompositePinMarker.xaml`. Add:

```csharp
using System.Windows.Media.Effects;

public Effect? HeadShadowEffect => HeadImage.Effect;

public void ApplyHeadShadow(bool enabled, double opacity)
{
    HeadImage.Effect = enabled
        ? new DropShadowEffect
        {
            Color = Colors.Black,
            Direction = 315,
            ShadowDepth = 2,
            BlurRadius = 3,
            Opacity = opacity
        }
        : null;
}
```

Keep shadow application separate from `SetCompositeImages` so live tuning can
refresh it without rebuilding the render plan.

- [ ] **Step 5: Make ClusterMarker apply its dedicated config**

Remove both hardcoded effects from `ClusterMarker.xaml`. In code-behind add:

```csharp
using System.Windows.Media.Effects;

public Effect? StampShadowEffect => StampImage.Effect;
public Effect? BadgeShadowEffect => BadgeEllipse.Effect;

public void ApplyShadowConfig(ClusterMarkerShadowConfig config)
{
    if (config == null) throw new ArgumentNullException(nameof(config));

    StampImage.Effect = config.Enabled
        ? CreateShadow(depth: 2, blur: 4, config.Opacity)
        : null;
    BadgeEllipse.Effect = config.Enabled
        ? CreateShadow(depth: 1, blur: 2, config.Opacity)
        : null;
}

private static DropShadowEffect CreateShadow(
    double depth, double blur, double opacity) => new()
{
    Color = Colors.Black,
    Direction = 315,
    ShadowDepth = depth,
    BlurRadius = blur,
    Opacity = opacity
};
```

Call `ApplyShadowConfig` from the constructor using
`markerConfiguration.ClusterMarkerShadow`. Add this property to
`IMarkerConfiguration`:

```csharp
ClusterMarkerShadowConfig ClusterMarkerShadow { get; }
```

Update test fakes implementing `IMarkerConfiguration` to return
`new ClusterMarkerShadowConfig()`.

- [ ] **Step 6: Remove the extended-shaft opacity floor**

In `ExtensionLineRenderer`, replace:

```csharp
Opacity = Math.Max(pinConfig.ShadowOpacity, 0.45)
```

with:

```csharp
Opacity = pinConfig.ShadowOpacity
```

The existing `if (pinConfig.ShowShadow)` remains the enablement gate.

- [ ] **Step 7: Complete view tests**

Add a `CompositePinMarker_ApplyHeadShadow_UsesExactOpacityAndSupportsRemoval`
source-wiring test matching the PinHead test. Also assert that the governed
XAML files no longer contain hardcoded `DropShadowEffect` blocks.

Run:

```powershell
dotnet test Tests\InteractiveWorldMap.Tests.csproj --filter "FullyQualifiedName~MarkerShadowRenderingTests|FullyQualifiedName~Architecture"
```

Expected: PASS.

- [ ] **Step 8: Commit the rendering slice**

```powershell
git add Models/IMarkerConfiguration.cs Views/PinHead.xaml Views/PinHead.xaml.cs Views/CompositePinMarker.xaml Views/CompositePinMarker.xaml.cs Views/ClusterMarker.xaml Views/ClusterMarker.xaml.cs Views/ExtensionLineRenderer.cs Tests/MarkerShadowRenderingTests.cs Tests/ZoomOutTrackingTests.cs Tests/MarkerPlacementOrchestratorTests.cs
git commit -m "feat: apply configurable marker shadows"
```

---

### Task 4: Wire Runtime Apply, Reload, and Live Refresh

**Files:**
- Modify: `MainWindow.CompositePins.partial.cs`
- Modify: `MainWindow.DeveloperTuning.partial.cs`
- Test: `Tests/TuningPanelWiringTests.cs`
- Test: `Tests/TuningReapplyTests.cs`

- [ ] **Step 1: Write failing end-to-end wiring tests**

Add to `Tests/TuningPanelWiringTests.cs`:

```csharp
[Fact]
public void ApplyTuning_MapsMapAndShadowValues()
{
    var source = File.ReadAllText(
        Path.Combine(RepoRoot, "MainWindow.DeveloperTuning.partial.cs"));

    Assert.Contains("_visualConfig.ClusterBadgeSize = e.ClusterBadgeSize;", source);
    Assert.Contains("_visualConfig.ClusterCountFontSize = e.ClusterCountFontSize;", source);
    Assert.Contains("_visualConfig.ZoomScale = e.ZoomScale;", source);
    Assert.Contains("_visualConfig.AnimationDurationMs = e.AnimationDurationMs;", source);
    Assert.Contains("_visualConfig.PinMarkers.ShowShadow = e.PinShadowEnabled;", source);
    Assert.Contains("_visualConfig.PinMarkers.ShadowOpacity = e.PinShadowOpacity;", source);
    Assert.Contains("_visualConfig.ClusterMarkerShadow.Enabled = e.ClusterShadowEnabled;", source);
    Assert.Contains("_visualConfig.ClusterMarkerShadow.Opacity = e.ClusterShadowOpacity;", source);
}

[Fact]
public void ApplyTuning_RefreshesShadowVisualsWithoutContentReload()
{
    var source = File.ReadAllText(
        Path.Combine(RepoRoot, "MainWindow.DeveloperTuning.partial.cs"));

    Assert.Contains("RefreshMarkerShadows()", source);
    Assert.Contains("foreach (var clusterMarker in _clusterMarkers)", source);
    Assert.Contains("clusterMarker.ApplyShadowConfig(_visualConfig.ClusterMarkerShadow)", source);
}
```

Add a source test proving the composite creation path calls:

```csharp
compositeMarker.ApplyHeadShadow(
    _visualConfig.PinMarkers.ShowShadow,
    _visualConfig.PinMarkers.ShadowOpacity);
```

- [ ] **Step 2: Run the focused tests and verify they fail**

Run:

```powershell
dotnet test Tests\InteractiveWorldMap.Tests.csproj --filter "ApplyTuning_MapsMapAndShadowValues|ApplyTuning_RefreshesShadowVisualsWithoutContentReload"
```

Expected: FAIL because mapping and refresh orchestration are absent.

- [ ] **Step 3: Map disk/UI values into CreateTuningArgs**

Add:

```csharp
ClusterBadgeSize = config.ClusterBadgeSize,
ClusterCountFontSize = config.ClusterCountFontSize,
ZoomScale = config.ZoomScale,
AnimationDurationMs = config.AnimationDurationMs,
PinShadowEnabled = pinConfig.ShowShadow,
PinShadowOpacity = pinConfig.ShadowOpacity,
ClusterShadowEnabled = config.ClusterMarkerShadow.Enabled,
ClusterShadowOpacity = config.ClusterMarkerShadow.Opacity,
```

This keeps Reload using the same validated event contract as Apply.

- [ ] **Step 4: Classify map and shadow changes before mutation**

Capture old values and define:

```csharp
var clusterAppearanceChanged =
    !NearlyEqual(oldClusterBadgeSize, e.ClusterBadgeSize) ||
    !NearlyEqual(oldClusterCountFontSize, e.ClusterCountFontSize);

var zoomScaleChanged = !NearlyEqual(oldZoomScale, e.ZoomScale);

var pinShadowChanged =
    oldPinShadowEnabled != e.PinShadowEnabled ||
    !NearlyEqual(oldPinShadowOpacity, e.PinShadowOpacity);

var clusterShadowChanged =
    oldClusterShadowEnabled != e.ClusterShadowEnabled ||
    !NearlyEqual(oldClusterShadowOpacity, e.ClusterShadowOpacity);
```

Include `clusterAppearanceChanged` in `needsRecreate`, preserving the existing
zoomed-state rejection for cluster reconstruction. Do not include shadow
changes or animation duration in `needsRecreate`.

- [ ] **Step 5: Mutate every new value only after the zoomed-state guard**

Add:

```csharp
_visualConfig.ClusterBadgeSize = e.ClusterBadgeSize;
_visualConfig.ClusterCountFontSize = e.ClusterCountFontSize;
_visualConfig.ZoomScale = e.ZoomScale;
_visualConfig.AnimationDurationMs = e.AnimationDurationMs;
_visualConfig.PinMarkers.ShowShadow = e.PinShadowEnabled;
_visualConfig.PinMarkers.ShadowOpacity = e.PinShadowOpacity;
_visualConfig.ClusterMarkerShadow.Enabled = e.ClusterShadowEnabled;
_visualConfig.ClusterMarkerShadow.Opacity = e.ClusterShadowOpacity;
```

Keeping these assignments after the recreate rejection prevents partial
mutation when a zoomed user attempts a badge/font change.

- [ ] **Step 6: Add focused live shadow refresh**

Add to `MainWindow.DeveloperTuning.partial.cs`:

```csharp
private void RefreshMarkerShadows()
{
    RefreshDrawnPinVisuals();

    foreach (var marker in _individualMarkers)
    {
        if (marker.Content is CompositePinMarker composite)
        {
            composite.ApplyHeadShadow(
                _visualConfig.PinMarkers.ShowShadow,
                _visualConfig.PinMarkers.ShadowOpacity);
        }
    }

    foreach (var clusterMarker in _clusterMarkers)
        clusterMarker.ApplyShadowConfig(_visualConfig.ClusterMarkerShadow);
}
```

Call it when `pinShadowChanged || clusterShadowChanged`. If
`RefreshDrawnPinVisuals()` also replaces composite markers, narrow that helper
to drawn content or call the composite loop after replacement; the final
observable state must retain composite rendering.

- [ ] **Step 7: Pass shadow config during composite construction**

Immediately after constructing/configuring a `CompositePinMarker` in
`MainWindow.CompositePins.partial.cs`, call:

```csharp
compositeMarker.ApplyHeadShadow(
    _visualConfig.PinMarkers.ShowShadow,
    _visualConfig.PinMarkers.ShadowOpacity);
```

This covers first render, recreate, zoom transitions, and cache misses without
depending on a later tuning refresh.

- [ ] **Step 8: Apply zoom scale to an active zoomed view**

In the non-recreate branch, ensure `zoomScaleChanged` reaches
`ReapplyViewAfterTuningChange()`. That helper already calls
`ShowZoomedView(_currentZoomedCluster)` while zoomed, which rebuilds settled
zoom output at the new scale. On the full map no forced zoom occurs; the new
scale is used on the next click.

Animation duration requires no extra refresh: existing transition creation
reads `_visualConfig.AnimationDurationMs` for subsequent transitions. Add a
source assertion to `TuningPanelWiringTests` for those existing call sites if
coverage is absent.

- [ ] **Step 9: Run tuning and reapply tests**

Run:

```powershell
dotnet test Tests\InteractiveWorldMap.Tests.csproj --filter "FullyQualifiedName~TuningPanelWiringTests|FullyQualifiedName~TuningReapplyTests|FullyQualifiedName~MarkerShadowRenderingTests"
```

Expected: PASS.

- [ ] **Step 10: Commit orchestration**

```powershell
git add MainWindow.CompositePins.partial.cs MainWindow.DeveloperTuning.partial.cs Tests/TuningPanelWiringTests.cs Tests/TuningReapplyTests.cs
git commit -m "feat: apply map and shadow tuning live"
```

---

### Task 5: Integration Tests and Documentation

**Files:**
- Modify: `Tests/TuningPanelWiringTests.cs`
- Modify: `Tests/TuningReloadValidationTests.cs`
- Modify: `docs/guides/VISUAL_CONFIG.md`
- Modify: `docs/TO_DO.md`
- Modify: `CHANGELOG.md`
- Modify: `docs/superpowers/plans/2026-07-02-map-and-shadow-tuning.md`

- [ ] **Step 1: Add a complete event-contract coverage test**

Add one test that reads `Models/TuningPanelEventArgs.cs`,
`Views/DeveloperTuningPanel.xaml.cs`, and
`MainWindow.DeveloperTuning.partial.cs`, then verifies every new property name
appears in all three. Use this explicit field list:

```csharp
var fields = new[]
{
    "ClusterBadgeSize",
    "ClusterCountFontSize",
    "ZoomScale",
    "AnimationDurationMs",
    "PinShadowEnabled",
    "PinShadowOpacity",
    "ClusterShadowEnabled",
    "ClusterShadowOpacity"
};
```

This guards Load → Apply → Reload symmetry without repeating behavioral tests.

- [ ] **Step 2: Run the full focused feature set**

Run:

```powershell
dotnet test Tests\InteractiveWorldMap.Tests.csproj --filter "FullyQualifiedName~VisualConfigServiceTests|FullyQualifiedName~TuningPanelWiringTests|FullyQualifiedName~TuningReloadValidationTests|FullyQualifiedName~TuningReapplyTests|FullyQualifiedName~MarkerShadowRenderingTests"
```

Expected: PASS with zero failed tests.

- [ ] **Step 3: Update the visual-config guide**

In `docs/guides/VISUAL_CONFIG.md`:

- state that badge size, count font size, zoom scale, and animation duration are
  available in Runtime Tuning → Map;
- add `PinMarkers.ShowShadow` and `PinMarkers.ShadowOpacity` to the PinMarkers
  table;
- document that those settings govern drawn heads, drawn extended shafts, and
  composite heads, but not baked composite shaft shading;
- add `ClusterMarkerShadow.Enabled` and `.Opacity`, defaulting to disabled/0.0;
- state that cluster body and badge share the cluster opacity;
- document the inclusive `0.0–1.0` range and live Apply behavior.

- [ ] **Step 4: Perform finish bookkeeping**

In `docs/TO_DO.md`:

- remove `Expose zoom level and cluster marker config options...` because all
  listed controls are now exposed;
- narrow the shadow bullet to only any remaining shadow-performance work from
  `zoom-performance-appearance-plan.md`, or remove it if Tasks 2.4/2.9 are
  fully satisfied by the implementation and existing animation behavior.

Under `[Unreleased]` in `CHANGELOG.md`, add:

```markdown
- **Map and shadow Runtime Tuning:** Completed the Map category with zoom scale,
  animation duration, cluster badge size, and cluster count font size. Added a
  Shadows category with shared pin and independent cluster-marker controls;
  pin opacity is now honored consistently without a hidden floor, while the
  checked-in cluster-shadow default is off.
```

Mark Tasks 1–5 complete in this plan only after their verification commands
pass.

- [ ] **Step 5: Run documentation and diff checks**

Run:

```powershell
git diff --check
.\scripts\verify.ps1
```

Expected: `git diff --check` produces no output and the repository verification
script exits 0.

- [ ] **Step 6: Self-review the implementation**

Review:

```powershell
git status --short
git diff --stat HEAD~4
git diff HEAD~4 -- Models Views MainWindow.xaml MainWindow.CompositePins.partial.cs MainWindow.DeveloperTuning.partial.cs Tests visual-config.json docs CHANGELOG.md
```

Confirm:

- no hardcoded opacity remains on governed pin/cluster visuals;
- no window/dialog shadow was changed;
- zoomed recreate rejection happens before any new config mutation;
- shadow Apply does not clear manual layouts or reload content;
- checked-in cluster shadows are disabled;
- all completed backlog text is removed or narrowed.

- [ ] **Step 7: Commit documentation and verified completion state**

```powershell
git add Tests/TuningPanelWiringTests.cs Tests/TuningReloadValidationTests.cs docs/guides/VISUAL_CONFIG.md docs/TO_DO.md CHANGELOG.md docs/superpowers/plans/2026-07-02-map-and-shadow-tuning.md
git commit -m "docs: complete map and shadow tuning"
```

- [ ] **Step 8: Archive the completed plan**

After every checkbox is complete and `.\scripts\verify.ps1` passes:

```powershell
Move-Item docs/superpowers/plans/2026-07-02-map-and-shadow-tuning.md docs/exec-plans/completed/map-and-shadow-tuning-plan.md
git add docs/superpowers/plans/2026-07-02-map-and-shadow-tuning.md docs/exec-plans/completed/map-and-shadow-tuning-plan.md
git commit -m "docs: archive map and shadow tuning plan"
```

Update any active registry link if one is added during implementation. Do not
archive while manual or automated acceptance criteria remain incomplete;
instead leave the plan active and narrow its remaining checklist.

---

## Final Acceptance Criteria

- Runtime Tuning → Map exposes all four newly requested map values.
- Runtime Tuning → Shadows exposes independent enabled/opacity controls for
  pins and cluster markers.
- Existing pin config remains backward-compatible.
- Cluster shadows default off in both the model and checked-in config.
- Pin opacity controls drawn heads, extended drawn shafts, and composite heads
  exactly, including values below `0.45`.
- Cluster opacity controls both body and badge exactly.
- Apply/Reload reject invalid values before mutation.
- Live shadow changes preserve current view and manual layout state.
- Zoom scale rebuilds an active settled zoom; animation duration governs later
  transitions.
- Focused tests and `.\scripts\verify.ps1` pass.
- `docs/TO_DO.md`, `docs/guides/VISUAL_CONFIG.md`, `CHANGELOG.md`, and plan
  archival state satisfy `AGENTS.md`.
