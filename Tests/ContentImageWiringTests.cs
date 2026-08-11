using System;
using System.IO;
using Xunit;

namespace InteractiveWorldMap.Tests;

/// <summary>
/// Structural wiring checks that MainWindow configures the content-image decode/diagnostics settings
/// on the ContentLoader from VisualConfig, following the repo's source-grep wiring-test convention.
/// </summary>
public class ContentImageWiringTests
{
    private static string RepoRoot =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));

    private static string MainWindowSource =>
        File.ReadAllText(Path.Combine(RepoRoot, "MainWindow.xaml.cs"));

    [Fact]
    public void MainWindow_WiresDecodeBoxAndWarnBytesFromConfig()
    {
        var source = MainWindowSource;

        Assert.Contains(
            "_contentLoader.MaxDecodePixelWidth = _visualConfig.ContentImages.MaxDecodePixelWidth",
            source);
        Assert.Contains(
            "_contentLoader.MaxDecodePixelHeight = _visualConfig.ContentImages.MaxDecodePixelHeight",
            source);
        Assert.Contains(
            "_contentLoader.LargeImageWarnBytes = _visualConfig.ContentImages.LargeImageWarnBytes",
            source);
    }

    [Fact]
    public void MainWindow_GatesImageDiagnosticsBehindDevToolsAndDebugFlag()
    {
        var source = MainWindowSource;

        Assert.Contains("_contentLoader.EnableImageDiagnostics", source);
        Assert.Contains("AreDeveloperToolsEnabled()", source);
        Assert.Contains("_visualConfig.Debug.LogContentImageDiagnostics", source);
    }

    [Fact]
    public void MainWindow_SubscribesLargeImageDetected()
    {
        Assert.Contains("_contentLoader.LargeImageDetected += OnLargeContentImageDetected", MainWindowSource);
    }

    [Fact]
    public void MainWindow_ResolvesDecodeBoxFromConfigAndDisplayWithFloor()
    {
        var source = MainWindowSource;

        // Effective box combines config + display via the tested helper and floors each dimension.
        Assert.Contains("ImageDecodeMath.ResolveDecodeCap(config.MaxDecodePixelWidth", source);
        Assert.Contains("ImageDecodeMath.ResolveDecodeCap(config.MaxDecodePixelHeight", source);
        Assert.Contains("FallbackDecodePixelWidth", source);
        Assert.Contains("FallbackDecodePixelHeight", source);
    }

    [Fact]
    public void MainWindow_GatesLoadingBannerBehindStandaloneConfigFlag()
    {
        // The "Loading content…" banner is a standalone guest-facing toggle (default off), independent
        // of developer diagnostics, so it must be gated by ContentImages.ShowLoadingStatus.
        var source = File.ReadAllText(Path.Combine(RepoRoot, "MainWindow.Content.partial.cs"));

        var gateIdx = source.IndexOf(
            "if (_visualConfig.ContentImages.ShowLoadingStatus)", StringComparison.Ordinal);
        var loadingIdx = source.IndexOf("ShowContentStatus(\"Loading content", StringComparison.Ordinal);

        Assert.True(gateIdx >= 0, "Loading banner must be gated by ContentImages.ShowLoadingStatus.");
        Assert.True(loadingIdx > gateIdx, "The gate must precede the loading banner show.");
    }
}
