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
