using System.IO;
using Xunit;

namespace InteractiveWorldMap.Tests;

public class PinMarkerRenderingTests
{
    private static string RepoRoot =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));

    [Fact]
    public void AutoStubPinMarker_UsesPinMarkerConfigForDimensions()
    {
        var autoStubSource =
            File.ReadAllText(Path.Combine(RepoRoot, "Views", "AutoStubPinMarker.xaml.cs"));
        var headSource =
            File.ReadAllText(Path.Combine(RepoRoot, "Views", "PinHead.xaml.cs"));

        Assert.Contains("ApplyConfig(PinMarkerConfig", autoStubSource);
        Assert.Contains("pinConfig.ShaftOutlineColor", autoStubSource);
        Assert.Contains("config.BallSize", headSource);
        Assert.Contains("config.BallOutlineColor", headSource);
        Assert.DoesNotContain("LocationMarkerSize / 16.0", autoStubSource);
    }

    [Fact]
    public void AutoStubPinMarker_UsesLayeredShaftOutlineInXaml()
    {
        var xaml = File.ReadAllText(Path.Combine(RepoRoot, "Views", "AutoStubPinMarker.xaml"));

        Assert.Contains("PinShaftOutline", xaml);
        Assert.Contains("PinShaft", xaml);
    }

    [Fact]
    public void AutoStubPinMarker_DisablesPixelSnappingForThinShaft()
    {
        var xaml = File.ReadAllText(Path.Combine(RepoRoot, "Views", "AutoStubPinMarker.xaml"));

        Assert.Contains("SnapsToDevicePixels=\"False\"", xaml);
        Assert.Contains("UseLayoutRounding=\"False\"", xaml);
    }

    [Fact]
    public void ExtensionLineRenderer_RestoresConfiguredLineStyleAfterHover()
    {
        var source = File.ReadAllText(Path.Combine(RepoRoot, "Views", "ExtensionLineRenderer.cs"));

        Assert.Contains("RememberLineStyle", source);
        Assert.Contains("GetLineStyle(line)", source);
        Assert.Contains("CreatePinLinePair", source);
        Assert.Contains("ShaftOutlineColor", source);
    }

    [Fact]
    public void DrawnPinsExposeShaftTipAnchorForMapPlacement()
    {
        var source = File.ReadAllText(Path.Combine(RepoRoot, "Views", "AutoStubPinMarker.xaml.cs"));

        Assert.Contains("GetShaftTipPoint()", source);
        Assert.Contains("new(Width / 2.0, Height)", source);
    }

    [Fact]
    public void MainWindowPlacesDrawnPinsByShaftTipOutsideManualLayout()
    {
        var source =
            File.ReadAllText(Path.Combine(RepoRoot, "MainWindow.xaml.cs")) +
            File.ReadAllText(Path.Combine(RepoRoot, "MainWindow.CompositePins.partial.cs"));

        Assert.Contains("autoStub.GetShaftTipPoint()", source);
        Assert.Contains("Canvas.SetLeft(marker, mapPoint.X - shaftTip.X);", source);
        Assert.Contains("Canvas.SetTop(marker, mapPoint.Y - shaftTip.Y);", source);
    }
}
