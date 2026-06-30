using System.IO;
using System.Xml.Linq;
using Xunit;

namespace InteractiveWorldMap.Tests;

public class ThumbnailBrowserWindowTests
{
    private static string RepoRoot =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));

    private static XDocument LoadView() =>
        XDocument.Load(Path.Combine(RepoRoot, "Views", "ThumbnailBrowserWindow.xaml"));

    [Fact]
    public void ThumbnailViewport_UsesAutomaticVerticalTouchScrolling()
    {
        var scrollViewer = LoadView()
            .Descendants()
            .Single(element =>
                element.Name.LocalName == "ScrollViewer" &&
                (string?)element.Attribute(
                    XName.Get("Name", "http://schemas.microsoft.com/winfx/2006/xaml")) ==
                    "ThumbnailScrollViewer");

        Assert.Equal("Auto", (string?)scrollViewer.Attribute("VerticalScrollBarVisibility"));
        Assert.Equal("Disabled", (string?)scrollViewer.Attribute("HorizontalScrollBarVisibility"));
        Assert.Equal("VerticalFirst", (string?)scrollViewer.Attribute("PanningMode"));
        Assert.Equal("Transparent", (string?)scrollViewer.Attribute("Background"));
        Assert.Contains(
            scrollViewer.Descendants(),
            element =>
                element.Name.LocalName == "ItemsControl" &&
                (string?)element.Attribute(
                    XName.Get("Name", "http://schemas.microsoft.com/winfx/2006/xaml")) ==
                    "ThumbnailList");
    }

    [Fact]
    public void ThumbnailItems_UseCompletedClickInsteadOfPressTimeSelection()
    {
        var document = LoadView();
        var thumbnailButton = document
            .Descendants()
            .Single(element =>
                element.Name.LocalName == "Button" &&
                (string?)element.Attribute("Click") == "Thumbnail_Click");

        Assert.Equal("False", (string?)thumbnailButton.Attribute("Focusable"));
        Assert.Contains(
            thumbnailButton.Descendants(),
            element => element.Name.LocalName == "ControlTemplate");
        Assert.DoesNotContain(
            document.Descendants().Attributes(),
            attribute => attribute.Name.LocalName == "MouseLeftButtonDown");

        var source = File.ReadAllText(
            Path.Combine(RepoRoot, "Views", "ThumbnailBrowserWindow.xaml.cs"));

        Assert.Contains(
            "private void Thumbnail_Click(object sender, RoutedEventArgs e)",
            source);
        Assert.Contains(
            "sender is System.Windows.Controls.Button button",
            source);
        Assert.Contains(
            "button.DataContext is ThumbnailItem item",
            source);
    }
}
