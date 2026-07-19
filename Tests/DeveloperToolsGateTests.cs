using System;
using System.IO;
using Xunit;

namespace InteractiveWorldMap.Tests;

public class DeveloperToolsGateTests
{
    private static readonly string RepoRoot =
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));

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
}
