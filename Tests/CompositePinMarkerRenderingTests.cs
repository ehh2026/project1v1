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
        var xName = XName.Get("Name", "http://schemas.microsoft.com/winfx/2006/xaml");

        Assert.Null(root.Attribute("SnapsToDevicePixels"));
        Assert.Null(root.Attribute("UseLayoutRounding"));

        var layerImageNames = new[]
        {
            "ShaftTipCapImage",
            "ShaftBodyImage",
            "ShaftHeadCapImage",
            "HeadImage"
        };
        var imageElements = root
            .Descendants(ns + "Image")
            .Where(image => layerImageNames.Contains(image.Attribute(xName)?.Value))
            .ToList();
        Assert.Equal(layerImageNames.Length, imageElements.Count);

        foreach (var image in imageElements)
        {
            Assert.Equal("Fant", image.Attribute("RenderOptions.BitmapScalingMode")?.Value);
            Assert.Equal("Unspecified", image.Attribute("RenderOptions.EdgeMode")?.Value);
        }
    }

    [Fact]
    public void CompositePinMarker_HasPrerasterizedImageHostOutsideLayerCanvas()
    {
        var xamlPath = Path.Combine(RepoRoot, "Views", "CompositePinMarker.xaml");
        var document = XDocument.Load(xamlPath);
        var root = document.Root ?? throw new InvalidOperationException("CompositePinMarker.xaml has no root element.");
        var ns = root.Name.Namespace;
        var xName = XName.Get("Name", "http://schemas.microsoft.com/winfx/2006/xaml");

        var layerCanvas = Assert.Single(root
            .Descendants(ns + "Canvas")
            .Where(canvas => canvas.Attribute(xName)?.Value == "LayerCanvas"));

        var flattenedImage = Assert.Single(root
            .Descendants(ns + "Image")
            .Where(image => image.Attribute(xName)?.Value == "FlattenedImage"));
        Assert.Equal("Collapsed", flattenedImage.Attribute("Visibility")?.Value);
        Assert.DoesNotContain(flattenedImage, layerCanvas.Descendants(ns + "Image"));
    }
}
