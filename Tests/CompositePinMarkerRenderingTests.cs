using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace InteractiveWorldMap.Tests;

public class CompositePinMarkerRenderingTests
{
    private static string RepoRoot =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));

    [Fact]
    public void CompositePinMarker_UsesAntialiasedRenderingHints()
    {
        var xamlPath = Path.Combine(RepoRoot, "Views", "CompositePinMarker.xaml");
        var document = XDocument.Load(xamlPath);
        var root = document.Root ?? throw new InvalidOperationException("CompositePinMarker.xaml has no root element.");
        var ns = root.Name.Namespace;

        Assert.Null(root.Attribute("SnapsToDevicePixels"));
        Assert.Null(root.Attribute("UseLayoutRounding"));

        var imageElements = root.Descendants(ns + "Image").ToList();
        Assert.Equal(4, imageElements.Count);

        foreach (var image in imageElements)
        {
            Assert.Equal("Fant", image.Attribute("RenderOptions.BitmapScalingMode")?.Value);
            Assert.Equal("Unspecified", image.Attribute("RenderOptions.EdgeMode")?.Value);
        }
    }
}
