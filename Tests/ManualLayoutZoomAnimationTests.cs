using System;
using System.IO;
using System.Linq;
using Xunit;

namespace InteractiveWorldMap.Tests;

public class ManualLayoutZoomAnimationTests
{
    private static string RepoRoot =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));

    [Fact]
    public void ZoomOut_ReplaysFullMapManualLayoutDuringAnimationFrames()
    {
        var source = File.ReadAllText(Path.Combine(RepoRoot, "MainWindow.Navigation.partial.cs"));
        var body = ExtractMethodBody(source, "private void AnimateZoomOut()");

        Assert.Contains("TryLoadFullMapManualLayoutForAnimation()", body);
        Assert.Contains("ApplyManualLayoutDuringAnimation(animationLayout)", body);
        Assert.Contains("CanUseCompositePins()", source);
    }

    [Fact]
    public void AnimateViewportTransition_InvokesFrameCallbackAfterMarkerPlacement()
    {
        var source = File.ReadAllText(Path.Combine(RepoRoot, "MainWindow.Navigation.partial.cs"));
        var body = ExtractMethodBody(source, "private void AnimateViewportTransition");

        Assert.Contains("Action? onFrameUpdated = null", source);
        Assert.True(
            body.Split("UpdateMarkerPositions();")
                .Skip(1)
                .Any(segment => segment.Contains("onFrameUpdated?.Invoke();")),
            "Animation frames should replay manual layout after marker positions update.");
    }

    private static string ExtractMethodBody(string source, string signature)
    {
        var methodStart = source.IndexOf(signature, StringComparison.Ordinal);
        Assert.True(methodStart >= 0, $"Method not found: {signature}");

        var openBrace = source.IndexOf('{', methodStart);
        Assert.True(openBrace >= 0, $"Opening brace not found for: {signature}");

        var depth = 0;
        for (var i = openBrace; i < source.Length; i++)
        {
            if (source[i] == '{')
                depth++;
            else if (source[i] == '}')
            {
                depth--;
                if (depth == 0)
                    return source.Substring(openBrace, i - openBrace + 1);
            }
        }

        throw new InvalidOperationException($"Closing brace not found for: {signature}");
    }
}
