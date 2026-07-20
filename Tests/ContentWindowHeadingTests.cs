using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Xml.Linq;
using InteractiveWorldMap.Views;
using Xunit;

namespace InteractiveWorldMap.Tests;

public class ContentWindowHeadingTests
{
    private static readonly XNamespace Xaml =
        "http://schemas.microsoft.com/winfx/2006/xaml";

    private static string RepoRoot =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));

    [Fact]
    public void DidacticWindow_DisplaysTrimmedLocationName()
    {
        RunOnStaThread(() =>
        {
            var window = new DidacticTextWindow();

            window.SetContent("Body", "  Dr. Test  ");

            var heading = Assert.IsType<TextBlock>(window.FindName("HeadingText"));
            Assert.Equal("Dr. Test", heading.Text);
            Assert.Equal(Visibility.Visible, heading.Visibility);
        });
    }

    [Fact]
    public void DidacticWindow_BlankLocationNameCollapsesHeading()
    {
        RunOnStaThread(() =>
        {
            var window = new DidacticTextWindow();

            window.SetContent("Body", "   ");

            var heading = Assert.IsType<TextBlock>(window.FindName("HeadingText"));
            Assert.Equal(string.Empty, heading.Text);
            Assert.Equal(Visibility.Collapsed, heading.Visibility);
        });
    }

    [Fact]
    public void MainContentWindow_HasNoVisibleHeadingOrHeaderRow()
    {
        var document = LoadView("ContentSubwindow.xaml");

        Assert.DoesNotContain(
            document.Descendants(),
            element => (string?)element.Attribute(Xaml + "Name") == "TitleText");
        Assert.Equal(3, RootGridRowCount(document));
        Assert.Contains(
            document.Descendants(),
            element => (string?)element.Attribute(Xaml + "Name") == "CaptionPane");
    }

    [Fact]
    public void MainContentWindow_CaptionPaneSpansWholeBottom()
    {
        var document = LoadView("ContentSubwindow.xaml");
        var rootGrid = RootGrid(document);
        var captionPane = document
            .Descendants()
            .Single(element => (string?)element.Attribute(Xaml + "Name") == "CaptionPane");

        Assert.Equal("2", (string?)captionPane.Attribute("Grid.Row"));
        Assert.Equal("0", (string?)captionPane.Attribute("Margin"));
        // Background/opacity are applied at runtime from ContentWindows config (see
        // ContentWindowThemeTests / VisualConfigServiceTests); XAML only supplies a fallback.
        Assert.NotNull((string?)captionPane.Attribute("Background"));

        var rootGridChildren = rootGrid.Elements().ToList();
        Assert.Contains(captionPane, rootGridChildren);
    }

    [Theory]
    [InlineData("ContentSubwindow.xaml")]
    [InlineData("DidacticTextWindow.xaml")]
    [InlineData("ThumbnailBrowserWindow.xaml")]
    public void PopupWindows_HaveNamedRootBorderWithFallbackBackground(string fileName)
    {
        var document = LoadView(fileName);
        var popupBorder = document
            .Root!
            .Elements()
            .First(element => element.Name.LocalName == "Border");

        // The border is named so ApplyStyle can theme it at runtime from ContentWindows config;
        // the XAML Background is only a fallback if styling is never applied.
        Assert.NotNull((string?)popupBorder.Attribute(Xaml + "Name"));
        Assert.NotNull((string?)popupBorder.Attribute("Background"));
    }

    [Fact]
    public void ThumbnailWindow_HasNoImagesHeadingOrHeaderRow()
    {
        var document = LoadView("ThumbnailBrowserWindow.xaml");

        Assert.DoesNotContain(
            document.Descendants(),
            element =>
                element.Name.LocalName == "TextBlock" &&
                (string?)element.Attribute("Text") == "Images");
        Assert.Equal(1, RootGridRowCount(document));
    }

    private static XDocument LoadView(string fileName) =>
        XDocument.Load(Path.Combine(RepoRoot, "Views", fileName));

    private static int RootGridRowCount(XDocument document)
    {
        var rootGrid = RootGrid(document);
        var rowDefinitions = rootGrid
            .Elements()
            .Single(element => element.Name.LocalName == "Grid.RowDefinitions");
        return rowDefinitions
            .Elements()
            .Count(element => element.Name.LocalName == "RowDefinition");
    }

    private static XElement RootGrid(XDocument document) =>
        document
            .Root!
            .Descendants()
            .First(element => element.Name.LocalName == "Grid");

    private static void RunOnStaThread(Action action)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                failure = ex;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (failure != null)
            throw failure;
    }
}
