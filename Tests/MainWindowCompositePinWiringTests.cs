using System;
using System.IO;
using Xunit;

namespace InteractiveWorldMap.Tests;

/// <summary>
/// Structural wiring checks that MainWindow wires composite-pin application, cache invalidation,
/// and manual-layout assignment enrichment, following the repo's source-grep wiring-test convention.
/// </summary>
public class MainWindowCompositePinWiringTests
{
    private static string RepoRoot =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));

    private static string MainWindowSource =>
        File.ReadAllText(Path.Combine(RepoRoot, "MainWindow.xaml.cs"));

    private static string LayoutEditorSource =>
        File.ReadAllText(Path.Combine(RepoRoot, "MainWindow.LayoutEditor.partial.cs"));

    [Fact]
    public void ApplyManualLayout_UsesCompositePinApplicationServiceBuildApplyInstructions()
    {
        // ApplyManualLayout must delegate plan building to CompositePinApplicationService
        // so the disk cache is checked before expensive recomputation.
        var source = LayoutEditorSource;

        Assert.Contains("_planApplicationService.BuildApplyInstructions(", source);
    }

    [Fact]
    public void SaveManualLayout_InvalidatesCompositePinApplicationCache()
    {
        // After saving a layout the composite render-plan cache must be invalidated so
        // the next render builds fresh plans reflecting the new positions.
        var source = LayoutEditorSource;

        Assert.Contains("_planApplicationService.InvalidateGroup(", source);
    }

    [Fact]
    public void ManualLayoutSave_UsesManualLayoutAssignmentEnricher()
    {
        // Both the regular save and the Save-As variant paths must capture shaft/head
        // assignments through ManualLayoutAssignmentEnricher so the persisted JSON
        // carries them across sessions.
        var layoutSource = LayoutEditorSource;
        var mainSource = MainWindowSource;

        Assert.Contains("_assignmentEnricher.GetAssignments(", layoutSource);
        Assert.Contains(
            "private readonly ManualLayoutAssignmentEnricher _assignmentEnricher",
            mainSource);
    }
}
