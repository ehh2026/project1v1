---
status: active
owner: agent
started: 2026-06-25
requirements_ref: docs/TO_DO.md#developer-tooling
---

# Developer Tools Production Disable Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add one master config option that disables all in-app developer tools for gallery/guest display mode.

**Architecture:** Add a top-level `EnableDeveloperTools` boolean to `VisualConfig`, defaulting to `false` for safe gallery behavior. The checked-in `visual-config.json` can set it to `true` for this development checkout. MainWindow treats the master flag as an outer gate around Edit Layout, Runtime Tuning, F12 tuning toggle, debug overlays/logging, and debug-only windowed mode; existing detailed config fields remain as sub-settings that only matter when the master gate is enabled.

**Tech Stack:** WPF / .NET 6 / C#, xUnit source/behavior tests, existing `VisualConfigService`, `MainWindow.*.partial.cs`, `DeveloperTuningPanel`.

---

## Current State

- `Debug.EnableTuningPanel` controls the visible tuning toggle and the F12 panel toggle.
- `ManualLayoutEditor.Enabled` controls whether Edit Layout can appear.
- `Debug.ShowCompositePinDebugOverlay`, debug log flags, and `Debug.WindowedMode` can be enabled independently from the tuning panel.
- The checked-in `visual-config.json` currently enables the in-app tuning panel and windowed debug mode for development.
- The gallery requirement is stronger: guests should not be able to discover or activate editing, tuning, debug overlays, or debug-mode behavior from the UI.

## Desired Behavior

| Scenario | `EnableDeveloperTools` | Result |
|---|---:|---|
| Gallery / production config | `false` | No Edit Layout button, no tuning toggle, F12 ignored, tuning panel collapsed, debug overlay/log flags treated as off, debug windowed mode ignored. |
| Development config | `true` | Existing developer behaviors work, still respecting sub-settings such as `ManualLayoutEditor.Enabled` and current tuning defaults. |
| Missing property in config | omitted | Safe default: developer tools disabled. |
| Existing config has `Debug.ShowCompositePinDebugOverlay: true` but master disabled | `false` | Overlay does not render. |
| Existing config has `ManualLayoutEditor.Enabled: true` but master disabled | `false` | Edit Layout does not appear and click/key paths cannot enter edit mode. |

## File Map

| File | Role |
|---|---|
| `Models/VisualConfig.cs` | Add `EnableDeveloperTools` with safe model default `false`. |
| `visual-config.json` | Set `EnableDeveloperTools: true` for the current development checkout. |
| `MainWindow.xaml.cs` | Gate debug windowed mode, F12, and any direct keyboard path that reveals Edit Layout. |
| `MainWindow.DeveloperTuning.partial.cs` | Gate tuning panel setup/toggle/apply/save/reload entry points with the master flag. |
| `MainWindow.LayoutEditor.partial.cs` | Gate `UpdateEditLayoutButtonVisibility` and `OnEditLayoutButtonClick` with the master flag. |
| `MainWindow.CompositePins.partial.cs` | Gate `ShowCompositePinDebugOverlay` rendering with the master flag. |
| `Views/ExtensionLineRenderer.cs` | Treat extension-render debug logging flags as off when the master flag is disabled. |
| `Services/MarkerPlacementOrchestrator.cs` | Treat marker-placement debug logging flags as off when the master flag is disabled. |
| `Services/RadialExtensionAdjuster.cs` | Treat angle/overlap debug logging flags as off when the master flag is disabled. |
| `docs/guides/VISUAL_CONFIG.md` | Document gallery/development usage and the safe default. |
| `docs/TO_DO.md` | Remove or complete the developer-tooling backlog item after implementation passes. |
| `CHANGELOG.md` | Record the behavior change under `[Unreleased]`. |
| `Tests/VisualConfigServiceTests.cs` | Cover default false and explicit true deserialization. |
| `Tests/DeveloperToolsGateTests.cs` | Add source/behavior guards for MainWindow and renderer gating. |
| `Tests/TuningPanelWiringTests.cs` | Extend source guards for tuning panel master-gate wiring. |

## Task 1: Config Surface and Defaults

**Files:**
- Modify: `Models/VisualConfig.cs`
- Modify: `visual-config.json`
- Test: `Tests/VisualConfigServiceTests.cs`

- [ ] **Step 1: Write failing tests for the master flag**

Add these tests to `Tests/VisualConfigServiceTests.cs`:

```csharp
[Fact]
public void Load_EnableDeveloperTools_Deserializes()
{
    var tempDir = CreateTempDir();
    try
    {
        var path = Path.Combine(tempDir, "visual-config.json");
        File.WriteAllText(path, @"{ ""EnableDeveloperTools"": true }");
        var service = new VisualConfigService();

        var config = service.Load(path);

        Assert.True(config.EnableDeveloperTools);
    }
    finally
    {
        Directory.Delete(tempDir, recursive: true);
    }
}

[Fact]
public void Load_EnableDeveloperTools_UsesDefaultFalseWhenOmitted()
{
    var tempDir = CreateTempDir();
    try
    {
        var path = Path.Combine(tempDir, "visual-config.json");
        File.WriteAllText(path, @"{ ""Debug"": { ""EnableTuningPanel"": true } }");
        var service = new VisualConfigService();

        var config = service.Load(path);

        Assert.False(config.EnableDeveloperTools);
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
dotnet test Tests\InteractiveWorldMap.Tests.csproj --filter VisualConfigServiceTests
```

Expected: compile failure because `VisualConfig.EnableDeveloperTools` does not exist.

- [ ] **Step 3: Add the model property**

In `Models/VisualConfig.cs`, add a top-level property near the other root behavior switches:

```csharp
/// <summary>
/// Master gate for in-app developer tools such as Edit Layout, Runtime Tuning,
/// debug overlays/logging, and debug-only windowed mode.
/// Defaults off so gallery/guest display configs are safe unless explicitly enabled.
/// </summary>
public bool EnableDeveloperTools { get; set; } = false;
```

- [ ] **Step 4: Enable it in the checked-in development config**

In `visual-config.json`, add the root property near the top-level behavior flags:

```json
"EnableDeveloperTools": true,
```

Keep the existing detailed `ManualLayoutEditor` and `Debug` values unchanged for now. They become sub-settings honored only when the master flag is true.

- [ ] **Step 5: Run the focused tests and verify they pass**

Run:

```powershell
dotnet test Tests\InteractiveWorldMap.Tests.csproj --filter VisualConfigServiceTests
```

Expected: all `VisualConfigServiceTests` pass.

## Task 2: Centralize Gate Semantics in MainWindow

**Files:**
- Modify: `MainWindow.xaml.cs`
- Test: `Tests/DeveloperToolsGateTests.cs`

- [ ] **Step 1: Create source-guard tests for the central helper**

Create `Tests/DeveloperToolsGateTests.cs` with:

```csharp
using System;
using System.IO;
using Xunit;

namespace InteractiveWorldMap.Tests;

public class DeveloperToolsGateTests
{
    private static readonly string RepoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));

    [Fact]
    public void MainWindow_HasCentralDeveloperToolsGate()
    {
        var source = File.ReadAllText(Path.Combine(RepoRoot, "MainWindow.xaml.cs"));

        Assert.Contains("private bool AreDeveloperToolsEnabled", source);
        Assert.Contains("_visualConfig.EnableDeveloperTools", source);
    }

    [Fact]
    public void MainWindow_F12RequiresDeveloperToolsGate()
    {
        var source = File.ReadAllText(Path.Combine(RepoRoot, "MainWindow.xaml.cs"));

        var f12Index = source.IndexOf("e.Key == Key.F12", StringComparison.Ordinal);
        Assert.True(f12Index >= 0, "F12 handler not found.");

        var gateIndex = source.IndexOf("AreDeveloperToolsEnabled()", f12Index, StringComparison.Ordinal);
        Assert.True(gateIndex >= 0, "F12 tuning toggle must require AreDeveloperToolsEnabled().");
    }

    [Fact]
    public void MainWindow_DebugWindowedModeRequiresDeveloperToolsGate()
    {
        var source = File.ReadAllText(Path.Combine(RepoRoot, "MainWindow.xaml.cs"));

        Assert.Contains("AreDeveloperToolsEnabled() && _visualConfig.Debug.WindowedMode", source);
    }
}
```

- [ ] **Step 2: Run the focused tests and verify they fail**

Run:

```powershell
dotnet test Tests\InteractiveWorldMap.Tests.csproj --filter DeveloperToolsGateTests
```

Expected: compile or assertion failure because the helper and usages do not exist yet.

- [ ] **Step 3: Add a central helper**

In `MainWindow.xaml.cs`, add this private helper near other small state helpers:

```csharp
private bool AreDeveloperToolsEnabled() => _visualConfig.EnableDeveloperTools;
```

Keep this intentionally simple so tests and future readers see that the single config flag owns the master decision.

- [ ] **Step 4: Gate debug windowed mode**

Change the startup windowed-mode branch from:

```csharp
if (_visualConfig.Debug.WindowedMode)
```

to:

```csharp
if (AreDeveloperToolsEnabled() && _visualConfig.Debug.WindowedMode)
```

- [ ] **Step 5: Gate F12**

Change the F12 branch from:

```csharp
else if (e.Key == Key.F12 && _visualConfig.Debug.EnableTuningPanel)
```

to:

```csharp
else if (e.Key == Key.F12 && AreDeveloperToolsEnabled() && _visualConfig.Debug.EnableTuningPanel)
```

- [ ] **Step 6: Run the focused tests and verify they pass**

Run:

```powershell
dotnet test Tests\InteractiveWorldMap.Tests.csproj --filter DeveloperToolsGateTests
```

Expected: all `DeveloperToolsGateTests` pass.

## Task 3: Gate Runtime Tuning Panel

**Files:**
- Modify: `MainWindow.DeveloperTuning.partial.cs`
- Test: `Tests/TuningPanelWiringTests.cs`

- [ ] **Step 1: Add source-guard tests for tuning gate coverage**

Add to `Tests/TuningPanelWiringTests.cs`:

```csharp
[Fact]
public void MainWindow_TuningPanelVisibilityRequiresDeveloperToolsGate()
{
    var source = File.ReadAllText(Path.Combine(RepoRoot, "MainWindow.DeveloperTuning.partial.cs"));

    Assert.Contains("AreDeveloperToolsEnabled() && _visualConfig.Debug.EnableTuningPanel", source);
}

[Fact]
public void MainWindow_TuningActionsRejectWhenDeveloperToolsDisabled()
{
    var source = File.ReadAllText(Path.Combine(RepoRoot, "MainWindow.DeveloperTuning.partial.cs"));

    Assert.Contains("if (!AreDeveloperToolsEnabled())", source);
    Assert.Contains("Developer tools are disabled", source);
}
```

- [ ] **Step 2: Run the focused tests and verify they fail**

Run:

```powershell
dotnet test Tests\InteractiveWorldMap.Tests.csproj --filter "TuningPanelVisibilityRequiresDeveloperToolsGate|TuningActionsRejectWhenDeveloperToolsDisabled"
```

Expected: assertion failure because the tuning partial still only checks `Debug.EnableTuningPanel`.

- [ ] **Step 3: Gate setup and toggle**

In `MainWindow.DeveloperTuning.partial.cs`, change setup visibility to:

```csharp
TuningPanelToggleBtn.Visibility = AreDeveloperToolsEnabled() && _visualConfig.Debug.EnableTuningPanel
    ? Visibility.Visible
    : Visibility.Collapsed;
```

Change the toggle guard to:

```csharp
if (!AreDeveloperToolsEnabled() || !_visualConfig.Debug.EnableTuningPanel)
    return;
```

- [ ] **Step 4: Gate action entry points**

At the start of `CanRunTuningAction`, before busy/animation/edit-mode checks, add:

```csharp
if (!AreDeveloperToolsEnabled())
{
    DeveloperTuningPanel.SetStatus("Developer tools are disabled.");
    return false;
}
```

- [ ] **Step 5: Run the focused tests and verify they pass**

Run:

```powershell
dotnet test Tests\InteractiveWorldMap.Tests.csproj --filter "TuningPanelVisibilityRequiresDeveloperToolsGate|TuningActionsRejectWhenDeveloperToolsDisabled"
```

Expected: both tests pass.

## Task 4: Gate Edit Layout

**Files:**
- Modify: `MainWindow.LayoutEditor.partial.cs`
- Modify: `MainWindow.xaml.cs`
- Test: `Tests/DeveloperToolsGateTests.cs`

- [ ] **Step 1: Add source-guard tests for Edit Layout gate coverage**

Add to `Tests/DeveloperToolsGateTests.cs`:

```csharp
[Fact]
public void LayoutEditor_ButtonVisibilityRequiresDeveloperToolsGate()
{
    var source = File.ReadAllText(Path.Combine(RepoRoot, "MainWindow.LayoutEditor.partial.cs"));

    Assert.Contains("!AreDeveloperToolsEnabled() || !_visualConfig.ManualLayoutEditor.Enabled", source);
}

[Fact]
public void LayoutEditor_ClickRequiresDeveloperToolsGate()
{
    var source = File.ReadAllText(Path.Combine(RepoRoot, "MainWindow.LayoutEditor.partial.cs"));

    Assert.Contains("if (!AreDeveloperToolsEnabled())", source);
    Assert.Contains("Developer tools are disabled", source);
}

[Fact]
public void MainWindow_DebugKeyboardEditLayoutRequiresDeveloperToolsGate()
{
    var source = File.ReadAllText(Path.Combine(RepoRoot, "MainWindow.xaml.cs"));

    Assert.Contains("AreDeveloperToolsEnabled() && _visualConfig.ManualLayoutEditor.Enabled", source);
}
```

- [ ] **Step 2: Run the focused tests and verify they fail**

Run:

```powershell
dotnet test Tests\InteractiveWorldMap.Tests.csproj --filter "LayoutEditor_ButtonVisibilityRequiresDeveloperToolsGate|LayoutEditor_ClickRequiresDeveloperToolsGate|MainWindow_DebugKeyboardEditLayoutRequiresDeveloperToolsGate"
```

Expected: assertion failures because the master gate is not wired yet.

- [ ] **Step 3: Gate button visibility**

In `UpdateEditLayoutButtonVisibility`, change the initial guard to:

```csharp
if (!AreDeveloperToolsEnabled() || !_visualConfig.ManualLayoutEditor.Enabled || _layoutEditor.IsEditMode)
{
    EditLayoutButton.Visibility = Visibility.Collapsed;
    return;
}
```

- [ ] **Step 4: Gate the click handler**

At the top of `OnEditLayoutButtonClick`, before entering edit mode, add:

```csharp
if (!AreDeveloperToolsEnabled())
{
    _logger.LogInfo("[EditLayout] Ignored because developer tools are disabled.");
    EditLayoutButton.Visibility = Visibility.Collapsed;
    return;
}
```

Do not introduce a new view dependency or status surface for this guard. In gallery mode the desired behavior is that the button is not visible; the click guard is a defensive backstop.

- [ ] **Step 5: Gate keyboard reveal/debug path**

In `MainWindow.xaml.cs`, change any manual-layout keyboard branch that currently checks:

```csharp
if (_visualConfig.ManualLayoutEditor.Enabled)
```

to:

```csharp
if (AreDeveloperToolsEnabled() && _visualConfig.ManualLayoutEditor.Enabled)
```

- [ ] **Step 6: Run the focused tests and verify they pass**

Run:

```powershell
dotnet test Tests\InteractiveWorldMap.Tests.csproj --filter DeveloperToolsGateTests
```

Expected: all developer-tool gate tests pass.

## Task 5: Gate Debug Overlay and Debug Logging

**Files:**
- Modify: `MainWindow.CompositePins.partial.cs`
- Modify: `Views/ExtensionLineRenderer.cs`
- Modify: `Services/MarkerPlacementOrchestrator.cs`
- Modify: `Services/RadialExtensionAdjuster.cs`
- Test: `Tests/DeveloperToolsGateTests.cs`

- [ ] **Step 1: Add source-guard tests for debug feature gates**

Add to `Tests/DeveloperToolsGateTests.cs`:

```csharp
[Fact]
public void CompositePinDebugOverlayRequiresDeveloperToolsGate()
{
    var source = File.ReadAllText(Path.Combine(RepoRoot, "MainWindow.CompositePins.partial.cs"));

    Assert.Contains("AreDeveloperToolsEnabled() && _visualConfig.Debug.ShowCompositePinDebugOverlay", source);
}

[Fact]
public void ExtensionLineDebugLoggingRequiresDeveloperToolsGate()
{
    var source = File.ReadAllText(Path.Combine(RepoRoot, "Views", "ExtensionLineRenderer.cs"));

    Assert.Contains("_visualConfig.EnableDeveloperTools && _visualConfig.Debug.LogRadialExtensionCalculation", source);
}

[Fact]
public void MarkerPlacementDebugLoggingRequiresDeveloperToolsGate()
{
    var source = File.ReadAllText(Path.Combine(RepoRoot, "Services", "MarkerPlacementOrchestrator.cs"));

    Assert.Contains("var logRadialExtensionCalculation", source);
    Assert.Contains("_visualConfig.EnableDeveloperTools", source);
    Assert.Contains("_visualConfig.Debug.LogRadialExtensionCalculation", source);
    Assert.DoesNotContain("if (_visualConfig.Debug.LogRadialExtensionCalculation)", source);
}

[Fact]
public void RadialExtensionAdjusterDebugLoggingRequiresDeveloperToolsGate()
{
    var source = File.ReadAllText(Path.Combine(RepoRoot, "Services", "RadialExtensionAdjuster.cs"));

    Assert.Contains("_visualConfig.EnableDeveloperTools && _visualConfig.Debug.LogRadialExtensionAngles", source);
    Assert.Contains("_visualConfig.EnableDeveloperTools && _visualConfig.Debug.LogRadialExtensionOverlaps", source);
}
```

- [ ] **Step 2: Run the focused tests and verify they fail**

Run:

```powershell
dotnet test Tests\InteractiveWorldMap.Tests.csproj --filter "CompositePinDebugOverlayRequiresDeveloperToolsGate|ExtensionLineDebugLoggingRequiresDeveloperToolsGate|MarkerPlacementDebugLoggingRequiresDeveloperToolsGate|RadialExtensionAdjusterDebugLoggingRequiresDeveloperToolsGate"
```

Expected: assertion failures because debug overlay/logging still use debug sub-flags directly.

- [ ] **Step 3: Gate composite debug overlay rendering**

In `MainWindow.CompositePins.partial.cs`, change the `CompositePinMarker` constructor argument from:

```csharp
_visualConfig.Debug.ShowCompositePinDebugOverlay
```

to:

```csharp
AreDeveloperToolsEnabled() && _visualConfig.Debug.ShowCompositePinDebugOverlay
```

- [ ] **Step 4: Gate extension-line debug logging**

In `Views/ExtensionLineRenderer.cs`, change debug log booleans from:

```csharp
_visualConfig.Debug.LogRadialExtensionCalculation
```

to:

```csharp
_visualConfig.EnableDeveloperTools && _visualConfig.Debug.LogRadialExtensionCalculation
```

Apply this to both the local `log` variable near the top of `Apply` and the later conditional logging branch near the line factory/debug output.

- [ ] **Step 5: Gate marker-placement debug logging**

In `Services/MarkerPlacementOrchestrator.cs`, avoid repeated long expressions by adding one local boolean near the existing `shouldApplyExtensions` calculation:

```csharp
var logRadialExtensionCalculation =
    _visualConfig.EnableDeveloperTools &&
    _visualConfig.Debug.LogRadialExtensionCalculation;
```

Then replace every direct read of `_visualConfig.Debug.LogRadialExtensionCalculation` in this file with `logRadialExtensionCalculation`.

- [ ] **Step 6: Gate radial-adjuster debug logging**

In `Services/RadialExtensionAdjuster.cs`, change:

```csharp
bool logAngles  = _visualConfig.Debug.LogRadialExtensionAngles;
bool logOverlaps = _visualConfig.Debug.LogRadialExtensionOverlaps;
```

to:

```csharp
bool logAngles  = _visualConfig.EnableDeveloperTools && _visualConfig.Debug.LogRadialExtensionAngles;
bool logOverlaps = _visualConfig.EnableDeveloperTools && _visualConfig.Debug.LogRadialExtensionOverlaps;
```

- [ ] **Step 7: Run the focused tests and verify they pass**

Run:

```powershell
dotnet test Tests\InteractiveWorldMap.Tests.csproj --filter DeveloperToolsGateTests
```

Expected: all developer-tool gate tests pass.

## Task 6: Documentation and Backlog Cleanup

**Files:**
- Modify: `docs/guides/VISUAL_CONFIG.md`
- Modify: `docs/TO_DO.md`
- Modify: `CHANGELOG.md`

- [ ] **Step 1: Update visual config docs**

In `docs/guides/VISUAL_CONFIG.md`, add a top-level section:

```markdown
## Developer Tools Master Gate

`EnableDeveloperTools` is the single master switch for in-app developer controls.
It defaults to `false` in the model so gallery/guest display configs are safe by default.

When `EnableDeveloperTools` is `false`:

- Edit Layout is hidden and cannot be entered.
- Runtime Tuning and the F12 tuning toggle are disabled.
- Composite debug overlays and verbose debug logging are treated as off.
- Debug-only windowed mode is ignored.

The repository's development `visual-config.json` may set this to `true`; production/gallery deployments should set it to `false`.
```

Also add `EnableDeveloperTools` to the abbreviated config sample near the top.

- [ ] **Step 2: Update TO_DO**

In `docs/TO_DO.md`, remove the Developer tooling item:

```markdown
- [ ] Dev tools production gate — disable Edit Layout, Runtime Tuning/F12, and debug-only affordances for gallery guests via one config switch — dev-tools-production-disable-plan.md
```

Do this only after code and tests pass.

- [ ] **Step 3: Update CHANGELOG**

Under `[Unreleased]`, add:

```markdown
- **Developer tools production gate:** Added a single `EnableDeveloperTools` master config switch. When disabled, gallery/guest mode hides and blocks Edit Layout, Runtime Tuning/F12, debug overlays/logging, and debug-only windowed mode; the model default is safe-off while the development config can opt in.
```

- [ ] **Step 4: Run doc link check through full verification**

Run:

```powershell
.\scripts\verify.ps1
```

Expected: verification passes, including doc links and taste checks.

## Modularity / File Size Impact

- `MainWindow.xaml.cs`, `MainWindow.DeveloperTuning.partial.cs`, `MainWindow.LayoutEditor.partial.cs`, and `MainWindow.CompositePins.partial.cs` receive only small gate checks and one helper. No new workflow logic should be added inline.
- No new view control is needed. Existing buttons/panels are hidden or ignored based on the master gate.
- If the implementation needs more than a few lines of repeated gate logic, add a small helper in `MainWindow.xaml.cs` rather than duplicating expressions across partials.
- `Views/DeveloperTuningPanel` stays a pure view/control: it should not read config or know about production mode directly.
- `Views/ExtensionLineRenderer` may read `_visualConfig.EnableDeveloperTools` because it already owns `VisualConfig` for rendering/logging decisions.

## Acceptance Criteria

- `VisualConfig.EnableDeveloperTools` exists and defaults to `false` when omitted.
- Checked-in `visual-config.json` explicitly opts in with `"EnableDeveloperTools": true` for current development workflows.
- With `EnableDeveloperTools = false`, no visible UI path exposes Edit Layout or Runtime Tuning.
- With `EnableDeveloperTools = false`, F12 does nothing.
- With `EnableDeveloperTools = false`, debug overlay and verbose debug logging are treated as disabled even if their sub-flags are true.
- With `EnableDeveloperTools = false`, `Debug.WindowedMode` is ignored and the app uses normal gallery window mode.
- With `EnableDeveloperTools = true`, current developer workflows still work, gated by their existing sub-settings.
- `docs/guides/VISUAL_CONFIG.md`, `docs/TO_DO.md`, and `CHANGELOG.md` reflect the finished behavior.
- `.\scripts\verify.ps1` passes.
