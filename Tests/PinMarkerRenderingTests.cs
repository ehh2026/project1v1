using System.IO;
using Xunit;

namespace InteractiveWorldMap.Tests;

public class PinMarkerRenderingTests
{
    private static string RepoRoot =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));

    [Fact]
    public void PinMarker_UsesPinMarkerConfigForDimensions()
    {
        var source = File.ReadAllText(Path.Combine(RepoRoot, "Views", "PinMarker.xaml.cs"));

        Assert.Contains("ApplyPinDimensions(PinMarkerConfig", source);
        Assert.Contains("pinConfig.BallSize", source);
        Assert.Contains("pinConfig.ShaftOutlineColor", source);
        Assert.Contains("pinConfig.BallOutlineColor", source);
        Assert.DoesNotContain("LocationMarkerSize / 16.0", source);
    }

    [Fact]
    public void PinMarker_UsesLayeredShaftOutlineInXaml()
    {
        var xaml = File.ReadAllText(Path.Combine(RepoRoot, "Views", "PinMarker.xaml"));

        Assert.Contains("PinShaftOutline", xaml);
        Assert.Contains("PinShaft", xaml);
    }

    [Fact]
    public void PinMarker_DisablesPixelSnappingForThinShaft()
    {
        var xaml = File.ReadAllText(Path.Combine(RepoRoot, "Views", "PinMarker.xaml"));

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
        var source = File.ReadAllText(Path.Combine(RepoRoot, "Views", "PinMarker.xaml.cs"));

        Assert.Contains("GetShaftTipPoint()", source);
        Assert.Contains("return new Point(Width / 2, Height);", source);
    }

    [Fact]
    public void MainWindowPlacesDrawnPinsByShaftTipOutsideManualLayout()
    {
        var source =
            File.ReadAllText(Path.Combine(RepoRoot, "MainWindow.xaml.cs")) +
            File.ReadAllText(Path.Combine(RepoRoot, "MainWindow.CompositePins.partial.cs"));

        Assert.Contains("drawnPin.GetShaftTipPoint()", source);
        Assert.Contains("Canvas.SetLeft(marker, mapPoint.X - shaftTip.X);", source);
        Assert.Contains("Canvas.SetTop(marker, mapPoint.Y - shaftTip.Y);", source);
    }
}
