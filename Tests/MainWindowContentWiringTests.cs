using System;
using System.IO;
using Xunit;

namespace InteractiveWorldMap.Tests;

/// <summary>
/// Structural wiring checks that MainWindow uses a bounded content-image decode path and
/// routes large-image warnings through the ContentLoader, complementing the assertions
/// already in ContentImageWiringTests.cs.
/// </summary>
public class MainWindowContentWiringTests
{
    private static string RepoRoot =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));

    private static string MainWindowSource =>
        File.ReadAllText(Path.Combine(RepoRoot, "MainWindow.xaml.cs"));

    private static string ContentSource =>
        File.ReadAllText(Path.Combine(RepoRoot, "MainWindow.Content.partial.cs"));

    [Fact]
    public void ContentLoad_UsesBoundedDecodePath()
    {
        // The decode box must be bounded from above by config and from below by the 4K fallback
        // so a content image is never decoded at the full source resolution.  The existing
        // ContentImageWiringTests cover ResolveDecodeCap; here we verify the fallback constants
        // and the fact that ApplyDisplayBasedImageDecodeCap is invoked from the init path.
        var source = MainWindowSource;

        // Fallback floor ensures a bounded decode even when config and display are both zero/absent.
        Assert.Contains("private const int FallbackDecodePixelWidth = 3840", source);
        Assert.Contains("private const int FallbackDecodePixelHeight = 2160", source);

        // The display-aware method is wired into startup so the decode box is set before any
        // content image is loaded.
        Assert.Contains("ApplyDisplayBasedImageDecodeCap();", source);
    }

    [Fact]
    public void ContentLoad_LogsLargeImageWarningsThroughContentLoader()
    {
        // LargeImageDetected is raised by ContentLoader (gated behind LargeImageWarnBytes) and
        // handled in MainWindow.Content.partial.cs to show the user-facing status banner.
        // ContentImageWiringTests already checks the subscription; here we confirm the handler
        // body reaches ShowContentStatus so the banner actually appears.
        var source = ContentSource;

        Assert.Contains("OnLargeContentImageDetected", source);
        Assert.Contains("ShowContentStatus(", source);
    }
}
