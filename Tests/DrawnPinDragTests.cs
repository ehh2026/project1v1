using System.IO;
using Xunit;

namespace InteractiveWorldMap.Tests;

/// <summary>
/// Source-guard tests for Task 2 of the tuning-and-pin-render-bugfixes plan (H14):
/// Drawn-pin drag must keep the tip fixed at its Excel location and show a connecting shaft.
/// </summary>
public class DrawnPinDragTests
{
    private static string RepoRoot =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));

    private static string LayoutEditorSource =>
        File.ReadAllText(Path.Combine(RepoRoot, "MainWindow.LayoutEditorDrag.partial.cs"));

    [Fact]
    public void DrawnDragBranch_ComputesTipFromSourceToScreen()
    {
        // The legacy (drawn) drag branch must derive the fixed tip from the Excel pixel coordinates
        // via SourceToScreen, not from the marker's canvas position.
        var source = LayoutEditorSource;

        // The drawn branch comes after the composite 'return;'.
        // Check that SourceToScreen is called with PixelX/PixelY in the legacy section.
        Assert.Contains("viewport.SourceToScreen(", source);
        Assert.Contains("_draggedMarker.Location.PixelX", source);
        Assert.Contains("_draggedMarker.Location.PixelY", source);
    }

    [Fact]
    public void DrawnDragBranch_CallsAnchorExtendedMarker()
    {
        // AnchorExtendedMarker sets z-index 2000 and anchors the head-only role by its
        // connection point. The drawn branch must call it.
        Assert.Contains("AnchorExtendedMarker(_draggedMarker, headScreen)", LayoutEditorSource);
    }

    [Fact]
    public void DrawnDragBranch_CreatesLineIfMissing()
    {
        // The branch must create an extension line on the first drag tick when none exists yet.
        var source = LayoutEditorSource;
        Assert.Contains("!_extensionLineRenderer.HasLine(_draggedMarker)", source);
        Assert.Contains("_extensionLineRenderer.AddLine(_draggedMarker, tipScreen, headScreen)", source);
    }

    [Fact]
    public void DrawnDragBranch_MovesLineIfExists()
    {
        // On subsequent ticks (line already exists), only move the endpoint.
        Assert.Contains("_extensionLineRenderer.MoveLineEndpoint(_draggedMarker, headScreen)", LayoutEditorSource);
    }

    [Fact]
    public void DrawnDragBranch_RecordsEndpointsWithTipAndHead()
    {
        // The saved endpoint must use tipScreen (from SourceToScreen) and headScreen,
        // not the old LocationMarkerSize-based centering.
        var source = LayoutEditorSource;
        Assert.Contains("_overrideStore.RecordEndpoints(_draggedMarker.Location.Name, tipScreen, headScreen)", source);
    }

    [Fact]
    public void DrawnDragBranch_DoesNotCenterOnLocationMarkerSize()
    {
        // The old bug: marker was centered with LocationMarkerSize / 2 (wrong glyph size).
        // The fixed code must not compute a new canvas position using that approach.
        var source = LayoutEditorSource;

        // Locate the drawn drag block (after the composite branch's 'return').
        var compositeReturnIdx = source.IndexOf("// Drawn pin drag:", System.StringComparison.Ordinal);
        Assert.True(compositeReturnIdx >= 0, "Drawn drag comment not found.");

        var drawnBlock = source.Substring(compositeReturnIdx);

        // LocationMarkerSize / 2 centering must be gone from this section.
        Assert.DoesNotContain("LocationMarkerSize / 2", drawnBlock.Substring(0, System.Math.Min(drawnBlock.Length, 1500)));
    }
}
