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
    public void NavigationAutoApplyPaths_HonorManualLayoutSuppression()
    {
        // A session "Unload Layout" sets IsManualLayoutSuppressed; every navigation path that
        // auto-applies a saved layout (zoom animation, single-location zoom, and the cluster-key
        // ShowZoomedView staging/apply) must skip while suppressed so pins stay auto-placed.
        var source = File.ReadAllText(Path.Combine(RepoRoot, "MainWindow.Navigation.partial.cs"));

        var animBody = ExtractMethodBody(source, "private ManualLayout? TryLoadFullMapManualLayoutForAnimation()");
        Assert.Contains("IsManualLayoutSuppressed", animBody);

        var singleBody = ExtractMethodBody(source, "private bool TryApplyFullMapLayoutForZoomedSingle(LocationCluster cluster)");
        Assert.Contains("IsManualLayoutSuppressed", singleBody);

        // Cluster-key path: both the staging guard and the apply guard reference the flag.
        Assert.Contains("savedLayout != null && !_layoutEditor.IsManualLayoutSuppressed", source);
        Assert.Contains("_savedLayoutToApply != null && !_layoutEditor.IsManualLayoutSuppressed", source);
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

    [Fact]
    public void AnimateViewportTransition_UsesStopwatchNotDateTimeNow()
    {
        // Phase 1.3: DateTime.Now (~15.6 ms resolution) quantized progress and caused stutter.
        var source = File.ReadAllText(Path.Combine(RepoRoot, "MainWindow.Navigation.partial.cs"));
        var body = ExtractMethodBody(source, "private void AnimateViewportTransition");

        Assert.Contains("Stopwatch.StartNew()", body);
        // No DateTime.Now used for timing (assignment pattern; the rationale comment may mention it).
        Assert.DoesNotContain("= DateTime.Now", body);
    }

    [Fact]
    public void ApplyManualLayout_RepositionsLinesInPlaceDuringAnimation_NotFullRebuild()
    {
        // Phase 2.1: during a zoom animation the layout is replayed every frame. It must reuse the
        // existing extension-line pairs (reposition in place) instead of clearing and re-creating
        // every Line/Brush/Effect, which churned the GC and dropped frames.
        var source = File.ReadAllText(Path.Combine(RepoRoot, "MainWindow.LayoutEditor.partial.cs"));
        var body = ExtractMethodBody(source, "private void ApplyManualLayout");

        // Clear() must be guarded so it does not run on every animation frame.
        var guardIdx = body.IndexOf("if (!IsAnimating)", StringComparison.Ordinal);
        var clearIdx = body.IndexOf("_extensionLineRenderer.Clear();", StringComparison.Ordinal);
        Assert.True(guardIdx >= 0 && clearIdx > guardIdx,
            "ApplyManualLayout must guard the extension-line Clear() behind !IsAnimating.");

        Assert.Contains("TryRepositionPinLine(marker", body);
    }

    [Fact]
    public void ApplyManualLayout_ResyncsHitTargetsAfterSettle_SkippingAnimationFrames()
    {
        // Saved layouts in dense clusters must re-sync pin hit targets once the layout has fully
        // settled (mirroring the Delete & Recalculate path), and skip that work on animation frames.
        var editorSource = File.ReadAllText(Path.Combine(RepoRoot, "MainWindow.LayoutEditor.partial.cs"));
        var applyBody = ExtractMethodBody(editorSource, "private void ApplyManualLayout");
        Assert.Contains("RefreshHitTargetsAfterManualLayout()", applyBody);

        var interactionSource = File.ReadAllText(Path.Combine(RepoRoot, "MainWindow.MarkerInteraction.partial.cs"));
        var resyncBody = ExtractMethodBody(interactionSource, "private void RefreshHitTargetsAfterManualLayout()");
        Assert.Contains("if (!IsAnimating)", resyncBody);
        Assert.Contains("RefreshMarkerHitTargets()", resyncBody);
    }

    [Fact]
    public void UpdateMarkerPositions_CachesVisibleProjectionsDuringAnimation()
    {
        // Phase 2.2: visibility/source coords are constant across a single animation, so the
        // visible-marker projections are cached for its duration instead of rebuilt each frame.
        var source = File.ReadAllText(Path.Combine(RepoRoot, "MainWindow.MarkerPlacement.partial.cs"));
        var body = ExtractMethodBody(source, "private void UpdateMarkerPositions()");

        Assert.Contains("_animVisibleIndividuals", body);
        Assert.Contains("IsAnimating && _animVisibleIndividuals != null", body);
    }

    [Fact]
    public void UpdateMarkerPositions_UsesNameIndexNotPerPlacementScan()
    {
        // Phase 2.3: per-placement marker lookups use an O(1) name index instead of O(n)
        // FirstOrDefault scans (O(n^2) per frame).
        var source = File.ReadAllText(Path.Combine(RepoRoot, "MainWindow.MarkerPlacement.partial.cs"));
        var body = ExtractMethodBody(source, "private void UpdateMarkerPositions()");

        Assert.Contains("BuildIndividualMarkerIndex()", body);
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
