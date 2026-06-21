using System;
using System.IO;
using Xunit;

namespace InteractiveWorldMap.Tests;

public class CompositePinZoomPersistenceTests
{
    private static string RepoRoot =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));

    [Fact]
    public void UpdateMarkerPositions_DoesNotUnconditionallyRestoreBaseVisuals()
    {
        var source = File.ReadAllText(Path.Combine(RepoRoot, "MainWindow.xaml.cs"));
        var body = ExtractMethodBody(source, "private void UpdateMarkerPositions()");

        Assert.DoesNotContain("RestoreBaseMarkerVisuals();", body);
        Assert.Contains("PrepareMarkerVisualsForPlacementUpdate();", body);
    }

    [Fact]
    public void CompositeApply_HasExplicitDrawnFallbackRestorePath()
    {
        var source = File.ReadAllText(Path.Combine(RepoRoot, "MainWindow.CompositePins.partial.cs"));
        var body = ExtractMethodBody(source, "private bool ApplyCompositePinTargetToMarker");

        Assert.Contains("RestoreDrawnFallbackForCompositeFailure(marker)", body);
    }

    [Fact]
    public void NormalCompositeReapply_ReCentersDrawnFallback_WhenCompositeFails()
    {
        var source = File.ReadAllText(Path.Combine(RepoRoot, "MainWindow.CompositePins.partial.cs"));
        var body = ExtractMethodBody(source, "private void ApplyCompositePinsToNormalPlacements");

        Assert.Contains("RestoreDrawnFallbackForCompositeFailure(marker, placement)", body);
    }

    [Fact]
    public void NormalCompositeApply_UsesRepositionOnlyPolicy_WhenSegmentUnchanged()
    {
        var source = File.ReadAllText(Path.Combine(RepoRoot, "MainWindow.CompositePins.partial.cs"));
        var body = ExtractMethodBody(source, "private void ApplyCompositePinsToNormalPlacements");

        Assert.Contains("TryApplyCompositePinAtTarget(marker, target)", body);
    }

    [Fact]
    public void TryApplyCompositePinMarker_UsesRepositionOnlyPath()
    {
        var source = File.ReadAllText(Path.Combine(RepoRoot, "MainWindow.CompositePins.partial.cs"));
        var body = ExtractMethodBody(source, "private bool TryApplyCompositePinMarker");

        Assert.Contains("TryApplyCompositePinAtTarget(marker, target", body);
    }

    [Fact]
    public void RepositionOnlyPath_UsesPlacementPolicyNotViewTypesInServices()
    {
        var source = File.ReadAllText(Path.Combine(RepoRoot, "Services", "CompositePinPlacementPolicy.cs"));

        Assert.Contains("ShouldRepositionOnly", source);
        Assert.DoesNotContain("using InteractiveWorldMap.Views", source);
    }

    [Fact]
    public void CompositeAtTarget_ReferencesShouldRepositionOnlyWithRenderPlan()
    {
        var source = File.ReadAllText(Path.Combine(RepoRoot, "MainWindow.CompositePins.partial.cs"));
        var body = ExtractMethodBody(source, "private bool TryApplyCompositePinAtTarget");

        Assert.Contains("ShouldRepositionOnly", body);
        Assert.Contains("compositeMarker.RenderPlan", body);
        Assert.Contains("RepositionCompositePinMarker", body);
    }

    [Fact]
    public void ShowZoomedView_PrefersFullMapLayoutForSingleLocationZoom()
    {
        var source = File.ReadAllText(Path.Combine(RepoRoot, "MainWindow.Navigation.partial.cs"));

        Assert.Contains("TryApplyFullMapLayoutForZoomedSingle(cluster)", source);
        Assert.Contains("full-map manual layout takes precedence over cluster layout", source);
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
