using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Xml.Linq;
using InteractiveWorldMap.Views;
using Xunit;

namespace InteractiveWorldMap.Tests;

public class ContentPresentationModeTests
{
    private static string RepoRoot =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));

    [Fact]
    public void TogglePresentationMode_AppliesOwnerBoundsAndBorderlessBlackStyling()
    {
        RunOnStaThread(() =>
        {
            var window = CreateNormalWindow();
            window.MaximizedBackgroundOpacity = 0.5;
            var border = Assert.IsType<Border>(window.FindName("ContentBorder"));
            var translate = Assert.IsType<Button>(window.FindName("TranslateButton"));
            translate.Visibility = Visibility.Visible;
            var changed = 0;
            window.PresentationModeChanged += (_, _) => changed++;

            Assert.True(window.TryTogglePresentationMode(new Rect(10, 20, 1000, 700)));

            Assert.True(window.IsPresentationMode);
            Assert.Equal(new Rect(10, 20, 1000, 700), CurrentBounds(window));
            Assert.Equal(new Thickness(0), border.BorderThickness);
            Assert.Equal(new Thickness(0), border.Padding);
            Assert.Equal(new CornerRadius(0), border.CornerRadius);
            Assert.Null(border.Effect);
            Assert.Equal(Visibility.Visible, translate.Visibility);
            Assert.Equal(
                Color.FromArgb(128, 0, 0, 0),
                Assert.IsType<SolidColorBrush>(border.Background).Color);
            Assert.Equal(1, changed);
        });
    }

    [Fact]
    public void TogglePresentationMode_RestoresExactBoundsAndPopupStyling()
    {
        RunOnStaThread(() =>
        {
            var window = CreateNormalWindow();
            var border = Assert.IsType<Border>(window.FindName("ContentBorder"));
            var normalBackground = border.Background;
            var normalBorderThickness = border.BorderThickness;
            var normalPadding = border.Padding;
            var normalCornerRadius = border.CornerRadius;
            var normalEffect = border.Effect;

            Assert.True(window.TryTogglePresentationMode(new Rect(10, 20, 1000, 700)));
            Assert.True(window.TryTogglePresentationMode(new Rect(10, 20, 1000, 700)));

            Assert.False(window.IsPresentationMode);
            Assert.Equal(new Rect(100, 120, 400, 300), CurrentBounds(window));
            Assert.Same(normalBackground, border.Background);
            Assert.Equal(normalBorderThickness, border.BorderThickness);
            Assert.Equal(normalPadding, border.Padding);
            Assert.Equal(normalCornerRadius, border.CornerRadius);
            Assert.Same(normalEffect, border.Effect);
        });
    }

    [Fact]
    public void TogglePresentationMode_InvalidBoundsLeavesNormalModeUnchanged()
    {
        RunOnStaThread(() =>
        {
            var window = CreateNormalWindow();

            Assert.False(window.TryTogglePresentationMode(new Rect(0, 0, 0, 700)));

            Assert.False(window.IsPresentationMode);
            Assert.Equal(new Rect(100, 120, 400, 300), CurrentBounds(window));
        });
    }

    [Fact]
    public void ContentSurface_UsesCompletedMouseClickAndExcludesTranslateButton()
    {
        var document = XDocument.Load(
            Path.Combine(RepoRoot, "Views", "ContentSubwindow.xaml"));
        var xamlName = XName.Get(
            "Name",
            "http://schemas.microsoft.com/winfx/2006/xaml");
        var surface = document
            .Descendants()
            .Single(element =>
                (string?)element.Attribute(xamlName) == "ContentInteractionSurface");
        var translate = document
            .Descendants()
            .Single(element =>
                (string?)element.Attribute(xamlName) == "TranslateButton");

        Assert.Equal(
            "ContentSurface_MouseLeftButtonUp",
            (string?)surface.Attribute("MouseLeftButtonUp"));
        Assert.DoesNotContain(translate, surface.Descendants());
        Assert.DoesNotContain(
            document.Descendants().Attributes(),
            attribute => attribute.Name.LocalName == "MouseLeftButtonDown");
    }

    [Fact]
    public void MainWindow_CoordinatesConfiguredContentAndCompanionVisibility()
    {
        var source = File.ReadAllText(
            Path.Combine(RepoRoot, "MainWindow.Content.partial.cs"));

        Assert.Contains(
            "private ContentSubwindow CreateContentSubwindow(Location location)",
            source);
        Assert.Contains(
            "MaximizedBackgroundOpacity =",
            source);
        Assert.Contains(
            "_visualConfig.MaximizedContentBackgroundOpacity",
            source);
        Assert.Contains(
            "window.PresentationModeChanged += OnContentPresentationModeChanged;",
            source);
        Assert.Contains("_activeThumbnailBrowser!.Hide();", source);
        Assert.Contains("_activeDidacticWindow!.Hide();", source);
        Assert.Contains("_activeThumbnailBrowser.Show();", source);
        Assert.Contains("_activeDidacticWindow.Show();", source);
        Assert.Contains("ResetCompanionPresentationState();", source);
    }

    private static ContentSubwindow CreateNormalWindow() =>
        new()
        {
            Left = 100,
            Top = 120,
            Width = 400,
            Height = 300
        };

    private static Rect CurrentBounds(Window window) =>
        new(window.Left, window.Top, window.Width, window.Height);

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
