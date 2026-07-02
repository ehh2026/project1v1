using System.IO;
using InteractiveWorldMap.Models;
using Xunit;

namespace InteractiveWorldMap.Tests;

/// <summary>
/// Tests for Task 4 of the tuning-and-pin-render-bugfixes plan (M2):
/// Reload-from-disk must validate config values before applying them.
/// </summary>
public class TuningReloadValidationTests
{
    private static string RepoRoot =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));

    // ── DeveloperTuningPanel.TryValidate — pure unit tests ──

    [Fact]
    public void TryValidate_ValidArgs_ReturnsTrue()
    {
        var ok = InteractiveWorldMap.Views.DeveloperTuningPanel.TryValidate(ValidArgs(), out var error);
        Assert.True(ok);
        Assert.Equal(string.Empty, error);
    }

    [Fact]
    public void TryValidate_ZeroClusterThreshold_ReturnsFalse()
    {
        var args = ValidArgs(); args.ClusterThreshold = 0;
        var ok = InteractiveWorldMap.Views.DeveloperTuningPanel.TryValidate(args, out var error);
        Assert.False(ok);
        Assert.Contains("Cluster threshold", error);
    }

    [Fact]
    public void TryValidate_NegativeClusterThreshold_ReturnsFalse()
    {
        var args = ValidArgs(); args.ClusterThreshold = -1;
        var ok = InteractiveWorldMap.Views.DeveloperTuningPanel.TryValidate(args, out var error);
        Assert.False(ok);
        Assert.Contains("Cluster threshold", error);
    }

    [Fact]
    public void TryValidate_NaNClusterThreshold_ReturnsFalse()
    {
        var args = ValidArgs(); args.ClusterThreshold = double.NaN;
        var ok = InteractiveWorldMap.Views.DeveloperTuningPanel.TryValidate(args, out var error);
        Assert.False(ok);
        Assert.Contains("Cluster threshold", error);
    }

    [Fact]
    public void TryValidate_InfinityLocationMarkerSize_ReturnsFalse()
    {
        var args = ValidArgs(); args.LocationMarkerSize = double.PositiveInfinity;
        var ok = InteractiveWorldMap.Views.DeveloperTuningPanel.TryValidate(args, out var error);
        Assert.False(ok);
        Assert.Contains("Location marker", error);
    }

    [Fact]
    public void TryValidate_ZeroClusterMarkerSize_ReturnsFalse()
    {
        var args = ValidArgs(); args.ClusterMarkerSize = 0;
        var ok = InteractiveWorldMap.Views.DeveloperTuningPanel.TryValidate(args, out var error);
        Assert.False(ok);
        Assert.Contains("Cluster marker", error);
    }

    [Fact]
    public void TryValidate_NegativeStubLength_ReturnsFalse()
    {
        var args = ValidArgs(); args.StubLength = -1;
        var ok = InteractiveWorldMap.Views.DeveloperTuningPanel.TryValidate(args, out var error);
        Assert.False(ok);
        Assert.Contains("Stub length", error);
    }

    [Fact]
    public void TryValidate_ZeroStubLength_ReturnsTrue()
    {
        // StubLength = 0 is the minimum (non-negative rule), so it should pass.
        var args = ValidArgs(); args.StubLength = 0;
        var ok = InteractiveWorldMap.Views.DeveloperTuningPanel.TryValidate(args, out var error);
        Assert.True(ok);
    }

    [Fact]
    public void TryValidate_NegativeHeadRadius_ReturnsFalse()
    {
        var args = ValidArgs(); args.TargetHeadRadiusPx = -0.1;
        var ok = InteractiveWorldMap.Views.DeveloperTuningPanel.TryValidate(args, out var error);
        Assert.False(ok);
        Assert.Contains("Head radius", error);
    }

    [Fact]
    public void TryValidate_NegativeShaftHalfWidth_ReturnsFalse()
    {
        var args = ValidArgs(); args.TargetShaftHalfWidthPx = -0.1;
        var ok = InteractiveWorldMap.Views.DeveloperTuningPanel.TryValidate(args, out var error);
        Assert.False(ok);
        Assert.Contains("Shaft half width", error);
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(-0.1)]
    public void TryValidate_NonPositiveTipCapWidth_ReturnsFalse(double value)
    {
        var args = ValidArgs(); args.TipCapWidthPx = value;
        var ok = InteractiveWorldMap.Views.DeveloperTuningPanel.TryValidate(args, out var error);
        Assert.False(ok);
        Assert.Contains("Cap width", error);
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(-0.1)]
    public void TryValidate_NonPositiveTipCapLineWeight_ReturnsFalse(double value)
    {
        var args = ValidArgs(); args.TipCapLineWeightPx = value;
        var ok = InteractiveWorldMap.Views.DeveloperTuningPanel.TryValidate(args, out var error);
        Assert.False(ok);
        Assert.Contains("Line weight", error);
    }

    [Fact]
    public void TryValidate_NegativeTipCapCurvature_ReturnsFalse()
    {
        var args = ValidArgs(); args.TipCapArcDepthPx = -0.1;
        var ok = InteractiveWorldMap.Views.DeveloperTuningPanel.TryValidate(args, out var error);
        Assert.False(ok);
        Assert.Contains("Curvature", error);
    }

    [Fact]
    public void TryValidate_ConcaveTipCapWithValidValues_ReturnsTrue()
    {
        var args = ValidArgs();
        args.TipCapStyle = DrawnPinTipCapStyle.Concave;
        args.TipCapWidthPx = 12.0;
        args.TipCapLineWeightPx = 3.0;
        args.TipCapArcDepthPx = 3.0;
        var ok = InteractiveWorldMap.Views.DeveloperTuningPanel.TryValidate(args, out var error);
        Assert.True(ok);
        Assert.Equal(string.Empty, error);
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(-1.0)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void TryValidate_InvalidDrawnHeadDiameter_ReturnsFalse(double value)
    {
        var args = ValidArgs(); args.DrawnHeadDiameterPx = value;
        Assert.False(InteractiveWorldMap.Views.DeveloperTuningPanel.TryValidate(args, out var error));
        Assert.Contains("Drawn head diameter", error);
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(-1.0)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void TryValidate_InvalidDrawnShaftWidth_ReturnsFalse(double value)
    {
        var args = ValidArgs(); args.DrawnShaftWidthPx = value;
        Assert.False(InteractiveWorldMap.Views.DeveloperTuningPanel.TryValidate(args, out var error));
        Assert.Contains("Drawn shaft width", error);
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(-1.0)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void TryValidate_InvalidDrawnShaftLength_ReturnsFalse(double value)
    {
        var args = ValidArgs(); args.DrawnShaftLengthPx = value;
        Assert.False(InteractiveWorldMap.Views.DeveloperTuningPanel.TryValidate(args, out var error));
        Assert.Contains("Drawn shaft length", error);
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(-1.0)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void TryValidate_InvalidPinHitbox_ReturnsFalse(double value)
    {
        var args = ValidArgs(); args.PinHitDiameterPx = value;
        Assert.False(InteractiveWorldMap.Views.DeveloperTuningPanel.TryValidate(args, out var error));
        Assert.Contains("Pin hitbox", error);
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(-1.0)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void TryValidate_InvalidClusterHitbox_ReturnsFalse(double value)
    {
        var args = ValidArgs(); args.ClusterHitDiameterPx = value;
        Assert.False(InteractiveWorldMap.Views.DeveloperTuningPanel.TryValidate(args, out var error));
        Assert.Contains("Cluster hitbox", error);
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(-1.0)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void TryValidate_InvalidZoomScale_ReturnsFalse(double value)
    {
        var args = ValidArgs();
        args.ZoomScale = value;
        Assert.False(InteractiveWorldMap.Views.DeveloperTuningPanel.TryValidate(args, out var error));
        Assert.Contains("Zoom scale", error);
    }

    [Theory]
    [InlineData(-0.01)]
    [InlineData(1.01)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void TryValidate_InvalidShadowOpacity_ReturnsFalse(double value)
    {
        var pinArgs = ValidArgs();
        pinArgs.PinShadowOpacity = value;
        Assert.False(InteractiveWorldMap.Views.DeveloperTuningPanel.TryValidate(pinArgs, out var pinError));
        Assert.Contains("Pin shadow opacity", pinError);

        var clusterArgs = ValidArgs();
        clusterArgs.ClusterShadowOpacity = value;
        Assert.False(InteractiveWorldMap.Views.DeveloperTuningPanel.TryValidate(clusterArgs, out var clusterError));
        Assert.Contains("Cluster shadow opacity", clusterError);
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(1.0)]
    public void TryValidate_ShadowOpacityBoundaries_ReturnTrue(double value)
    {
        var args = ValidArgs();
        args.PinShadowOpacity = value;
        args.ClusterShadowOpacity = value;
        Assert.True(InteractiveWorldMap.Views.DeveloperTuningPanel.TryValidate(args, out var error), error);
    }

    [Fact]
    public void TryValidate_NonPositiveAnimationDuration_ReturnsFalse()
    {
        var args = ValidArgs();
        args.AnimationDurationMs = 0;
        Assert.False(InteractiveWorldMap.Views.DeveloperTuningPanel.TryValidate(args, out var error));
        Assert.Contains("Animation duration", error);
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void TryValidate_InvalidClusterAppearanceSizes_ReturnFalse(double value)
    {
        var badgeArgs = ValidArgs();
        badgeArgs.ClusterBadgeSize = value;
        Assert.False(InteractiveWorldMap.Views.DeveloperTuningPanel.TryValidate(
            badgeArgs, out var badgeError));
        Assert.Contains("Cluster badge", badgeError);

        var fontArgs = ValidArgs();
        fontArgs.ClusterCountFontSize = value;
        Assert.False(InteractiveWorldMap.Views.DeveloperTuningPanel.TryValidate(
            fontArgs, out var fontError));
        Assert.Contains("Cluster count font", fontError);
    }

    // ── Source guard: reload path calls TryValidate before ApplyTuningAsync ──

    [Fact]
    public void OnReloadTuningFromDisk_CallsTryValidateBeforeApply()
    {
        var source = File.ReadAllText(
            Path.Combine(RepoRoot, "MainWindow.DeveloperTuning.partial.cs"));

        var validateIdx = source.IndexOf(
            "DeveloperTuningPanel.TryValidate(", System.StringComparison.Ordinal);
        var applyIdx = source.IndexOf(
            "await ApplyTuningAsync(args)", System.StringComparison.Ordinal);

        Assert.True(validateIdx >= 0, "TryValidate call not found in MainWindow.DeveloperTuning.partial.cs.");
        Assert.True(applyIdx > validateIdx, "TryValidate must precede await ApplyTuningAsync(args).");
    }

    private static TuningPanelEventArgs ValidArgs() => new TuningPanelEventArgs
    {
        ClusterThreshold = 50,
        LocationMarkerSize = 16,
        ClusterMarkerSize = 24,
        ClusterBadgeSize = 12,
        ClusterCountFontSize = 11,
        ZoomScale = 55,
        AnimationDurationMs = 390,
        PinShadowOpacity = 0.55,
        ClusterShadowOpacity = 0.0,
        StubLength = 24,
        TargetHeadRadiusPx = 8,
        TargetShaftHalfWidthPx = 3,
        DrawnHeadDiameterPx = 14,
        DrawnShaftWidthPx = 3,
        DrawnShaftLengthPx = 24,
        PinHitDiameterPx = 32,
        ClusterHitDiameterPx = 40,
        TipCapWidthPx = 12,
        TipCapLineWeightPx = 3,
        TipCapArcDepthPx = 3,
        AutoOpenSingleLocationContentAfterZoom = false,
        ShaftVariant = "outline_dark_7px",
        HeadVariant = string.Empty
    };
}
