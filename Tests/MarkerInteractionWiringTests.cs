using System.IO;
using Xunit;

namespace InteractiveWorldMap.Tests;

public class MarkerInteractionWiringTests
{
    private static string RepoRoot =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));

    [Fact]
    public void MapDisplay_HasSeparateInteractionCanvasWithoutBackground()
    {
        var xaml = File.ReadAllText(
            Path.Combine(RepoRoot, "Views", "MapDisplayControl.xaml"));

        Assert.Contains("x:Name=\"MarkerInteractionCanvas\"", xaml);
        Assert.DoesNotContain(
            "x:Name=\"MarkerInteractionCanvas\" Background=",
            xaml);
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

    [Fact]
    public void MarkerInteraction_TargetsAreTransparentCircles()
    {
        var source = File.ReadAllText(
            Path.Combine(RepoRoot, "MainWindow.MarkerInteraction.partial.cs"));

        Assert.Contains("Fill = Brushes.Transparent", source);
        Assert.Contains("MarkerHitTargetGeometry.EffectiveDiameter", source);
        Assert.Contains("Canvas.SetLeft(target, center.X - (diameter / 2.0))", source);
        Assert.Contains("Canvas.SetTop(target, center.Y - (diameter / 2.0))", source);
    }

    [Theory]
    [InlineData("MainWindow.MarkerPlacement.partial.cs", "RefreshMarkerHitTargets()")]
    [InlineData("MainWindow.CompositePins.partial.cs", "RefreshMarkerHitTargets()")]
    [InlineData("MainWindow.DrawnPins.partial.cs", "RefreshMarkerHitTargets()")]
    [InlineData("MainWindow.LayoutEditorDrag.partial.cs", "RefreshMarkerHitTargets()")]
    public void GeometryBoundary_RefreshesTargets(string fileName, string expected)
    {
        var source = File.ReadAllText(Path.Combine(RepoRoot, fileName));
        Assert.Contains(expected, source);
    }

    [Fact]
    public void ClearAllMarkers_ClearsInteractionTargets()
    {
        var source = File.ReadAllText(
            Path.Combine(RepoRoot, "MainWindow.MarkerPlacement.partial.cs"));

        Assert.Contains("ClearMarkerHitTargets();", source);
    }

    [Fact]
    public void MarkerInteraction_RoutesAllExistingBehaviors()
    {
        var source = File.ReadAllText(
            Path.Combine(RepoRoot, "MainWindow.MarkerInteraction.partial.cs"));

        Assert.Contains("HandleIndividualMarkerPrimaryAction", source);
        Assert.Contains("HandleClusterMarkerPrimaryAction", source);
        Assert.Contains("AnimateMarkerHover", source);
        Assert.Contains("OnShaftOverrideRequested", source);
        Assert.Contains("OnMarkerDragStart", source);
        Assert.Contains("OnMarkerDragMove", source);
        Assert.Contains("OnMarkerDragEnd", source);
    }

    [Fact]
    public void TuningRefreshesHitTargetsAfterApplyingNewDiameters()
    {
        var source = File.ReadAllText(
            Path.Combine(RepoRoot, "MainWindow.DeveloperTuning.partial.cs"));

        Assert.Contains("var hitTargetsChanged =", source);
        Assert.Contains("RefreshMarkerHitTargets();", source);
    }
}
