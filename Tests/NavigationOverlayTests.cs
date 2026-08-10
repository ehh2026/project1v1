using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Xunit;

namespace InteractiveWorldMap.Tests;

public class NavigationOverlayTests
{
    private static readonly string RepoRoot =
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));

    [Fact]
    public void BackNavigation_IsBottomLeftWithManualLayoutIndicatorAboveIt()
    {
        var document = XDocument.Load(Path.Combine(RepoRoot, "MainWindow.xaml"));
        XNamespace presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";

        var backButton = document
            .Descendants(presentation + "Button")
            .Single(element => (string?)element.Attribute(x + "Name") == "BackButton");
        var overlay = backButton.Parent;

        Assert.NotNull(overlay);
        Assert.Equal("StackPanel", overlay!.Name.LocalName);
        Assert.Equal("Left", (string?)overlay.Attribute("HorizontalAlignment"));
        Assert.Equal("Bottom", (string?)overlay.Attribute("VerticalAlignment"));
        Assert.Equal("← Back to Full Map", (string?)backButton.Attribute("Content"));

        var namedChildren = overlay.Elements()
            .Select(element => (string?)element.Attribute(x + "Name"))
            .Where(name => name != null)
            .ToArray();

        Assert.Equal(
            new[] { "ManualLayoutIndicator", "ContentStatusBanner", "BackButton" },
            namedChildren);
    }

    [Fact]
    public void ManualLayoutIndicator_RequiresDeveloperToolsGate()
    {
        var source = File.ReadAllText(
            Path.Combine(RepoRoot, "MainWindow.LayoutEditor.partial.cs"));

        var eventIndex = source.IndexOf(
            "_layoutEditor.ManualLayoutActivityChanged += isActive =>",
            StringComparison.Ordinal);
        Assert.True(eventIndex >= 0, "Manual-layout activity handler not found.");

        var eventEnd = source.IndexOf("};", eventIndex, StringComparison.Ordinal);
        Assert.True(eventEnd > eventIndex, "Manual-layout activity handler end not found.");

        var handler = source.Substring(eventIndex, eventEnd - eventIndex);
        Assert.Contains("AreDeveloperToolsEnabled()", handler);
    }
}
