using System;
using System.IO;
using InteractiveWorldMap.Models;
using Xunit;

namespace InteractiveWorldMap.Tests;

public class ZoomedMapRenderingWiringTests
{
    private static string Root => Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));

    [Fact]
    public void RequestBoundary_UsesVisualDpiAndPhysicalPixels()
    {
        var source = File.ReadAllText(Path.Combine(Root, "MainWindow.MapRendering.partial.cs"));
        Assert.Contains("VisualTreeHelper.GetDpi(MapDisplay)", source);
        Assert.Contains("PhysicalPixelSizeCalculator.TryCalculate(", source);
        Assert.Contains("_visualConfig.ZoomedMapRendering.ResamplingMode", source);
    }

    [Fact]
    public void Navigation_UsesRequestBasedCacheApi()
    {
        var source = File.ReadAllText(Path.Combine(Root, "MainWindow.Navigation.partial.cs"));
        Assert.Contains("_zoomedRegionCache.TryLoadRegion(request)", source);
        Assert.Contains("_zoomedRegionCache.GenerateAndCacheRegion(sourceImage, request)", source);
    }

    [Fact]
    public void Tuning_MapsZoomedMode()
    {
        var source = File.ReadAllText(Path.Combine(Root, "MainWindow.DeveloperTuning.partial.cs"));
        Assert.Contains("_visualConfig.ZoomedMapRendering.ResamplingMode = e.ZoomedMapResamplingMode;", source);
        Assert.Contains("ZoomedMapResamplingMode = config.ZoomedMapRendering.ResamplingMode", source);
    }

    [Fact]
    public void TuningXaml_OffersEveryMode()
    {
        var xaml = File.ReadAllText(Path.Combine(Root, "Views", "DeveloperTuningPanel.xaml"));
        Assert.Contains("x:Name=\"CmbZoomedMapResampling\"", xaml);
        foreach (var mode in Enum.GetNames<ZoomedMapResamplingMode>())
            Assert.Contains($"<ComboBoxItem Content=\"{mode}\"/>", xaml);
    }
}
