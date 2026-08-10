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
}
