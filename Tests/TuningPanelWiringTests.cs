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
}
