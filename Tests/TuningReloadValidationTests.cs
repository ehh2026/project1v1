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
        StubLength = 24,
        TargetHeadRadiusPx = 8,
        TargetShaftHalfWidthPx = 3,
        ShaftVariant = "outline_dark_7px",
        HeadVariant = string.Empty
    };
}
