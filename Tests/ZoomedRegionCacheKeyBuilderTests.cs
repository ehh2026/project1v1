using System.Windows;
using InteractiveWorldMap.Models;
using InteractiveWorldMap.Services;
using Xunit;

namespace InteractiveWorldMap.Tests;

public class ZoomedRegionCacheKeyBuilderTests
{
    [Fact]
    public void Build_ChangesForDpiModeSourceAndFineCenter()
    {
        var builder = new ZoomedRegionCacheKeyBuilder();
        var source = new ZoomedRegionSourceFingerprint("full-resolution", @"C:\map.jpg", 10, 20);
        var baseline = Request(100.01, 1, ZoomedMapResamplingMode.Fant);
        var key = builder.Build(baseline, source);

        Assert.NotEqual(key, builder.Build(Request(100.04, 1, ZoomedMapResamplingMode.Fant), source));
        Assert.NotEqual(key, builder.Build(Request(100.01, 1.5, ZoomedMapResamplingMode.Fant), source));
        Assert.NotEqual(key, builder.Build(Request(100.01, 1, ZoomedMapResamplingMode.Lanczos3), source));
        Assert.NotEqual(key, builder.Build(baseline, source with { Length = 11 }));
        Assert.Equal(key, builder.Build(baseline, source));
    }

    private static ZoomedRegionRenderRequest Request(double center, double dpi, ZoomedMapResamplingMode mode) =>
        new(center, 200, 55, 2560, 1440, dpi, dpi, mode, new Int32Rect(1, 2, 3, 4));
}
