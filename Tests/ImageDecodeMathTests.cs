using InteractiveWorldMap.Utilities;
using Xunit;

namespace InteractiveWorldMap.Tests;

public class ImageDecodeMathTests
{
    [Fact]
    public void ComputeDecodePixelWidth_NoCap_ReturnsZero()
    {
        Assert.Equal(0, ImageDecodeMath.ComputeDecodePixelWidth(10000, 8000, 0, 0));
        Assert.Equal(0, ImageDecodeMath.ComputeDecodePixelWidth(10000, 8000, -1, -1));
    }

    [Fact]
    public void ComputeDecodePixelWidth_UnknownSource_ReturnsZero()
    {
        Assert.Equal(0, ImageDecodeMath.ComputeDecodePixelWidth(0, 100, 3840, 2160));
        Assert.Equal(0, ImageDecodeMath.ComputeDecodePixelWidth(100, 0, 3840, 2160));
    }

    [Fact]
    public void ComputeDecodePixelWidth_SourceWithinBox_ReturnsZeroToKeepNative()
    {
        // Never upscale: an image already inside the box is left at native resolution.
        Assert.Equal(0, ImageDecodeMath.ComputeDecodePixelWidth(1920, 1080, 3840, 2160));
        Assert.Equal(0, ImageDecodeMath.ComputeDecodePixelWidth(3840, 2160, 3840, 2160));
    }

    [Fact]
    public void ComputeDecodePixelWidth_WidthIsBindingConstraint_ScalesByWidth()
    {
        // 7680x2160 into a 3840x2160 box: width must halve (2:1), height already fits.
        Assert.Equal(3840, ImageDecodeMath.ComputeDecodePixelWidth(7680, 2160, 3840, 2160));
    }

    [Fact]
    public void ComputeDecodePixelWidth_HeightIsBindingConstraint_ScalesByHeight()
    {
        // 4000x8000 into a 3840x2160 box: height is the tighter limit (2160/8000 = 0.27),
        // so width scales by the same factor → 4000 * 0.27 = 1080.
        Assert.Equal(1080, ImageDecodeMath.ComputeDecodePixelWidth(4000, 8000, 3840, 2160));
    }

    [Fact]
    public void ComputeDecodePixelWidth_SquareSourceIntoLandscapeBox_BoundedByHeight()
    {
        // 6000x6000 into 3840x2160: the 2160 height binds, width scales to match → 2160.
        Assert.Equal(2160, ImageDecodeMath.ComputeDecodePixelWidth(6000, 6000, 3840, 2160));
    }

    [Fact]
    public void ComputeDecodePixelWidth_SingleDimensionUnbounded_ScalesByBoundedDimension()
    {
        // Width unbounded (0), height capped at 2160: 4000x8000 scales by height → 4000 * 0.27 = 1080.
        Assert.Equal(1080, ImageDecodeMath.ComputeDecodePixelWidth(4000, 8000, 0, 2160));
        // Height unbounded (negative), width capped at 1000: 4000x8000 scales by width → 1000.
        Assert.Equal(1000, ImageDecodeMath.ComputeDecodePixelWidth(4000, 8000, 1000, -1));
    }

    [Fact]
    public void ComputeDecodePixelWidth_TinyCap_RoundsUpToAtLeastOnePixel()
    {
        Assert.Equal(1, ImageDecodeMath.ComputeDecodePixelWidth(100000, 100000, 1, 1));
    }

    [Fact]
    public void ResolveDecodeCap_TakesSmallerPositiveValue()
    {
        // Operator's smaller cap wins over a larger display.
        Assert.Equal(1920, ImageDecodeMath.ResolveDecodeCap(1920, 3840, 3840));
        // Display smaller than config wins (never exceed the screen).
        Assert.Equal(1600, ImageDecodeMath.ResolveDecodeCap(3840, 1600, 3840));
    }

    [Fact]
    public void ResolveDecodeCap_SinglePositive_ThatValueWins()
    {
        Assert.Equal(3840, ImageDecodeMath.ResolveDecodeCap(0, 3840, 1234));   // display only
        Assert.Equal(1920, ImageDecodeMath.ResolveDecodeCap(1920, 0, 1234));   // config only
    }

    [Fact]
    public void ResolveDecodeCap_NeitherPositive_UsesFallback()
    {
        Assert.Equal(2160, ImageDecodeMath.ResolveDecodeCap(0, 0, 2160));
        Assert.Equal(2160, ImageDecodeMath.ResolveDecodeCap(-5, -1, 2160));
    }
}
