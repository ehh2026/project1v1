using InteractiveWorldMap.Utilities;
using Xunit;

namespace InteractiveWorldMap.Tests;

public class PhysicalPixelSizeCalculatorTests
{
    [Theory]
    [InlineData(1920, 1080, 1.0, 1.0, 1920, 1080)]
    [InlineData(1706.6667, 960, 1.5, 1.5, 2560, 1440)]
    [InlineData(1536, 864, 1.25, 1.25, 1920, 1080)]
    public void TryCalculate_ValidInput_ReturnsPhysicalPixels(
        double width, double height, double dpiX, double dpiY, int expectedWidth, int expectedHeight)
    {
        Assert.True(PhysicalPixelSizeCalculator.TryCalculate(
            width, height, dpiX, dpiY, out var pixelWidth, out var pixelHeight));
        Assert.Equal(expectedWidth, pixelWidth);
        Assert.Equal(expectedHeight, pixelHeight);
    }

    [Theory]
    [InlineData(0, 100, 1, 1)]
    [InlineData(100, double.NaN, 1, 1)]
    [InlineData(100, 100, 0, 1)]
    [InlineData(100, 100, 1, double.PositiveInfinity)]
    public void TryCalculate_InvalidInput_ReturnsFalse(
        double width, double height, double dpiX, double dpiY)
    {
        Assert.False(PhysicalPixelSizeCalculator.TryCalculate(
            width, height, dpiX, dpiY, out _, out _));
    }
}
