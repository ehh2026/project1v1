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
}
