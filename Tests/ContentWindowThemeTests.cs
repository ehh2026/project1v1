using System.Windows.Media;
using InteractiveWorldMap.Views;
using Xunit;

namespace InteractiveWorldMap.Tests;

public class ContentWindowThemeTests
{
    [Fact]
    public void ToBrush_RgbPlusOpacity_ComposesArgb()
    {
        var brush = ContentWindowTheme.ToBrush("#1E1E1E", 1.0, Colors.Magenta);

        Assert.Equal(Color.FromArgb(0xFF, 0x1E, 0x1E, 0x1E), brush.Color);
    }

    [Fact]
    public void ToBrush_Opacity_OverridesAlphaInColorString()
    {
        var brush = ContentWindowTheme.ToBrush("#FF112233", 0.0, Colors.Magenta);

        Assert.Equal(0x00, brush.Color.A);
        Assert.Equal(0x11, brush.Color.R);
        Assert.Equal(0x22, brush.Color.G);
        Assert.Equal(0x33, brush.Color.B);
    }

    [Fact]
    public void ToBrush_ArgbHex_KeepsAlphaChannel()
    {
        var brush = ContentWindowTheme.ToBrush("#66FFFFFF", Colors.Black);

        Assert.Equal(0x66, brush.Color.A);
    }

    [Fact]
    public void ToBrush_InvalidColor_UsesFallback()
    {
        var brush = ContentWindowTheme.ToBrush("not-a-color", 1.0, Colors.Red);

        Assert.Equal(Colors.Red, brush.Color);
    }
}
