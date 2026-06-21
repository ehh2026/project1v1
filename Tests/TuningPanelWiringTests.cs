using System;
using System.IO;
using Xunit;

namespace InteractiveWorldMap.Tests;

public class TuningPanelWiringTests
{
    private static string RepoRoot =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));

    [Fact]
    public void DeveloperTuningPanel_CodeBehind_DoesNotReferenceServicesOrUtilities()
    {
        var source = File.ReadAllText(Path.Combine(RepoRoot, "Views", "DeveloperTuningPanel.xaml.cs"));

        Assert.DoesNotContain("InteractiveWorldMap.Services", source);
        Assert.DoesNotContain("InteractiveWorldMap.Utilities", source);
    }

    [Fact]
    public void RecreateAllMarkers_UpdatesClusterThresholdBeforeLoadingClusters()
    {
        var source = File.ReadAllText(Path.Combine(RepoRoot, "MainWindow.DeveloperTuning.partial.cs"));
        var thresholdAssignment = source.IndexOf(
            "_contentLoader.ClusterDistanceThreshold = _visualConfig.ClusterDistanceThreshold",
            StringComparison.Ordinal);
        var loadClusters = source.IndexOf("LoadClustersAsync()", StringComparison.Ordinal);

        Assert.True(thresholdAssignment >= 0, "RecreateAllMarkersAsync must update ContentLoader.ClusterDistanceThreshold.");
        Assert.True(loadClusters >= 0, "RecreateAllMarkersAsync must reload clusters.");
        Assert.True(
            thresholdAssignment < loadClusters,
            "ContentLoader.ClusterDistanceThreshold must be updated before LoadClustersAsync().");
    }

    [Fact]
    public void DeveloperTuningPanel_UsesSingleCompositePinsToggle()
    {
        var xaml = File.ReadAllText(Path.Combine(RepoRoot, "Views", "DeveloperTuningPanel.xaml"));
        var source = File.ReadAllText(Path.Combine(RepoRoot, "Views", "DeveloperTuningPanel.xaml.cs"));

        Assert.DoesNotContain("ChkPinPartsEnabled", xaml);
        Assert.DoesNotContain("Pin parts", xaml);
        Assert.Contains("Content=\"Composite pins\"", xaml);
        Assert.Contains("PinPartsEnabled = ChkComposite.IsChecked == true", source);
        Assert.Contains("UseComposite = ChkComposite.IsChecked == true", source);
        Assert.Contains("config.PinParts.Enabled && config.PinParts.UseCompositeRendering", source);
    }

    [Fact]
    public void DeveloperTuningPanel_ProvidesTooltipsForTuningOptions()
    {
        var xaml = File.ReadAllText(Path.Combine(RepoRoot, "Views", "DeveloperTuningPanel.xaml"));

        foreach (var controlName in new[]
        {
            "ChkComposite",
            "ChkPrerasterize",
            "ChkDebugOverlay",
            "ChkUseLitShafts",
            "TxtShaftVariant",
            "TxtHeadVariant",
            "TxtClusterThreshold",
            "TxtStubLength",
            "TxtTargetHeadRadius",
            "TxtTargetShaftHalfWidth",
            "TxtLocationMarkerSize",
            "TxtClusterMarkerSize"
        })
        {
            var nameIndex = xaml.IndexOf($"x:Name=\"{controlName}\"", StringComparison.Ordinal);
            Assert.True(nameIndex >= 0, $"{controlName} not found.");
            var nextNameIndex = xaml.IndexOf("x:Name=\"", nameIndex + 1, StringComparison.Ordinal);
            var controlBlock = nextNameIndex >= 0
                ? xaml.Substring(nameIndex, nextNameIndex - nameIndex)
                : xaml.Substring(nameIndex);

            Assert.Contains("ToolTip=", controlBlock);
        }
    }

    [Fact]
    public void ApplyTuning_MapsSingleCompositeToggleToBothConfigGates()
    {
        var source = File.ReadAllText(Path.Combine(RepoRoot, "MainWindow.DeveloperTuning.partial.cs"));

        Assert.Contains("_visualConfig.PinParts.Enabled = e.UseComposite;", source);
        Assert.Contains("_visualConfig.PinParts.UseCompositeRendering = e.UseComposite;", source);
    }
}
