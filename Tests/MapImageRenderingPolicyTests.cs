using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Xunit;

namespace InteractiveWorldMap.Tests;

public class MapImageRenderingPolicyTests
{
    private static string RepoRoot =>
        Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", ".."));

    [Fact]
    public void SettledMapImage_KeepsFillAndUsesFantWithoutAliasedEdges()
    {
        var document = XDocument.Load(
            Path.Combine(RepoRoot, "Views", "MapDisplayControl.xaml"));
        XNamespace presentation =
            "http://schemas.microsoft.com/winfx/2006/xaml/presentation";

        var image = document
            .Descendants(presentation + "Image")
            .Single(element =>
                (string?)element.Attribute(
                    XName.Get("Name",
                        "http://schemas.microsoft.com/winfx/2006/xaml")) ==
                "MapImage");

        Assert.Equal("Fill", image.Attribute("Stretch")?.Value);
        Assert.Equal("True", image.Attribute("SnapsToDevicePixels")?.Value);
        Assert.Equal(
            "Fant",
            image.Attribute("RenderOptions.BitmapScalingMode")?.Value);
        Assert.Null(image.Attribute("RenderOptions.EdgeMode"));
    }

    [Fact]
    public void UpdateViewport_DoesNotRestoreNearestNeighbor()
    {
        var source = File.ReadAllText(
            Path.Combine(RepoRoot, "Views", "MapDisplayControl.xaml.cs"));

        Assert.DoesNotContain(
            "BitmapScalingMode.NearestNeighbor",
            source,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "SetBitmapScalingMode(MapImage",
            source,
            StringComparison.Ordinal);
    }

    [Fact]
    public void AnimationFrames_SelectLinearBeforeMaterialization()
    {
        var source = File.ReadAllText(
            Path.Combine(RepoRoot, "MainWindow.Navigation.partial.cs"));

        var transform = source.IndexOf(
            "var scaledBitmap = new TransformedBitmap(",
            StringComparison.Ordinal);
        var linear = source.IndexOf(
            "BitmapScalingMode.Linear",
            transform,
            StringComparison.Ordinal);
        var materialize = source.IndexOf(
            "new WriteableBitmap(scaledBitmap)",
            transform,
            StringComparison.Ordinal);

        Assert.True(transform >= 0, "Keyframe transform not found.");
        Assert.True(
            linear > transform && linear < materialize,
            "Linear scaling must be selected before keyframe materialization.");
    }

    [Fact]
    public void AnimationFrameCache_VersionInvalidatesPriorPixelPolicy()
    {
        var source = File.ReadAllText(
            Path.Combine(RepoRoot, "Services", "AnimationFrameCache.cs"));

        Assert.Contains(
            "private const int CacheVersion = 16;",
            source,
            StringComparison.Ordinal);
    }

    [Fact]
    public void SettledZoomCrop_ContinuesToUseFant()
    {
        var source = File.ReadAllText(
            Path.Combine(RepoRoot, "Services", "ZoomedRegionCache.cs"));

        Assert.Contains(
            "BitmapScalingMode.Fant",
            source,
            StringComparison.Ordinal);
    }
}
