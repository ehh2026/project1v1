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

    [Fact]
    public void LegacyPinMarkerControl_IsRemoved()
    {
        Assert.False(File.Exists(Path.Combine(RepoRoot, "Views", "PinMarker.xaml")));
        Assert.False(File.Exists(Path.Combine(RepoRoot, "Views", "PinMarker.xaml.cs")));
    }
}
