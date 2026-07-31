using System.IO;
using Xunit;

namespace InteractiveWorldMap.Tests;

public class MarkerShadowRenderingTests
{
    private static string RepoRoot =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));

    [Fact]
    public void PinHead_AppliesConfiguredOpacityAndRemovesDisabledShadow()
    {
        var source = Read("Views", "PinHead.xaml.cs");
        Assert.Contains("Opacity = config.ShadowOpacity", source);
        Assert.Contains("PinBall.Effect = config.ShowShadow", source);
        Assert.Contains(": null", source);
    }

    [Fact]
    public void CompositePinMarker_RefreshesPrerasterizedHeadShadow()
    {
        var source = Read("Views", "CompositePinMarker.xaml.cs");
        Assert.Contains("Opacity = opacity", source);
        Assert.Contains("var wasPrerasterized =", source);
        Assert.Contains("TryApplyPrerasterizedRendering()", source);
    }

    [Fact]
    public void ClusterMarker_AppliesOneConfigToBodyAndBadge()
    {
        var source = Read("Views", "ClusterMarker.xaml.cs");
        Assert.Contains("MarkerBodyHost.Effect = config.Enabled", source);
        Assert.Contains("BadgeEllipse.Effect = config.Enabled", source);
        Assert.Equal(2, Count(source, "config.Opacity"));
    }

    [Fact]
    public void ClusterMarker_FallbackRemainsInsideShadowedBodyHost()
    {
        var xaml = Read("Views", "ClusterMarker.xaml");
        var source = Read("Views", "ClusterMarker.xaml.cs");
        Assert.Contains("x:Name=\"MarkerBodyHost\"", xaml);
        Assert.Contains("MarkerBodyHost.Children", source);
    }

    [Fact]
    public void ExtensionLineRenderer_DoesNotFloorConfiguredShadowOpacity()
    {
        var source = Read("Views", "ExtensionLineRenderer.cs");
        Assert.Contains("Opacity = pinConfig.ShadowOpacity", source);
        Assert.DoesNotContain("Math.Max(pinConfig.ShadowOpacity, 0.45)", source);
    }

    [Fact]
    public void GovernedXamlDoesNotHardcodeDropShadowEffects()
    {
        Assert.DoesNotContain("DropShadowEffect", Read("Views", "PinHead.xaml"));
        Assert.DoesNotContain("DropShadowEffect", Read("Views", "CompositePinMarker.xaml"));
        Assert.DoesNotContain("DropShadowEffect", Read("Views", "ClusterMarker.xaml"));
    }

    private static string Read(params string[] parts) =>
        File.ReadAllText(Path.Combine(new[] { RepoRoot }.Concat(parts).ToArray()));

    private static int Count(string source, string value)
    {
        var count = 0;
        var index = 0;
        while ((index = source.IndexOf(value, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += value.Length;
        }
        return count;
    }
}
