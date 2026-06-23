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
    public void ZoomIn_ReappliesFullMapManualLayoutAfterOffsetCaptureBaseline()
    {
        // Regression: the offset-capture block calls UpdateMarkerPositions(), which recomputes
        // default placements and reverts an active full-map manual layout to default stubs.
        // It must re-apply the layout before capturing offsets so the edited appearance is
        // preserved throughout the zoom-in animation (not just at settle).
        var source = File.ReadAllText(Path.Combine(RepoRoot, "MainWindow.Navigation.partial.cs"));
        var body = ExtractMethodBody(source, "private void AnimateZoomToCluster");

        var updateIdx = body.IndexOf("UpdateMarkerPositions();", StringComparison.Ordinal);
        var reapplyIdx = body.IndexOf("TryApplyFullMapManualLayout();", StringComparison.Ordinal);
        var captureIdx = body.IndexOf("_animationOffsets[marker]", StringComparison.Ordinal);

        Assert.True(updateIdx >= 0, "AnimateZoomToCluster must call UpdateMarkerPositions() to baseline placements.");
        Assert.True(reapplyIdx >= 0, "AnimateZoomToCluster must re-apply the full-map manual layout before capturing offsets.");
        Assert.True(captureIdx >= 0, "AnimateZoomToCluster must capture animation offsets.");
        Assert.True(updateIdx < reapplyIdx, "Manual layout re-apply must follow UpdateMarkerPositions().");
        Assert.True(reapplyIdx < captureIdx, "Manual layout must be re-applied before offsets are captured.");
        Assert.Contains("IsManualLayoutActive", body);
    }

    [Fact]
    public void ZoomIn_ReplaysFullMapManualLayoutDuringAnimationFrames()
    {
        // Drawn-pin manual layouts render the shaft as a separate extension line; the offset path
        // clears it, leaving only the head. Zoom-in must pass a per-frame replay callback (like
        // zoom-out) so the shaft tracks the map throughout the animation.
        var source = File.ReadAllText(Path.Combine(RepoRoot, "MainWindow.Navigation.partial.cs"));
        var body = ExtractMethodBody(source, "private void AnimateZoomToCluster");

        Assert.Contains("TryLoadFullMapManualLayoutForAnimation()", body);
        Assert.Contains("ApplyManualLayoutDuringAnimation(animationLayout)", body);
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
