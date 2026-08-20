using System;
using System.IO;
using Xunit;

namespace InteractiveWorldMap.Tests;

/// <summary>
/// Source guards for layout-key handling in the MainWindow partials. MainWindow cannot be
/// instantiated under test (WPF), so the behavior itself is covered by
/// <see cref="LayoutKeyGeneratorTests"/> and <see cref="LayoutEditorControllerTests"/>; these
/// tests only pin the wiring that connects them.
/// </summary>
public class LayoutEditorKeyDerivationTests
{
    private static readonly string RepoRoot =
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));

    private static string ReadSource(string fileName) =>
        File.ReadAllText(Path.Combine(RepoRoot, fileName));

    [Fact]
    public void OnEditLayoutButtonClick_DerivesClusterKey_RatherThanInheritingIt()
    {
        var source = ReadSource("MainWindow.LayoutEditor.partial.cs");

        var handlerIndex = source.IndexOf("private void OnEditLayoutButtonClick", StringComparison.Ordinal);
        Assert.True(handlerIndex >= 0, "OnEditLayoutButtonClick not found.");

        var deriveIndex = source.IndexOf(
            "LayoutKeyGenerator.DeriveEditSessionKey", handlerIndex, StringComparison.Ordinal);
        Assert.True(
            deriveIndex >= 0,
            "Entering edit mode while zoomed must re-derive the cluster layout key. Inheriting " +
            "CurrentLayoutKey lets a 'fullmap' key set by the zoom animation survive into a " +
            "cluster edit session, so the save overwrites the full-map layout.");
    }

    [Fact]
    public void EverySavePath_CapturesMarkersThroughTheGuardedRoute()
    {
        // Regression: the Save button's handler was guarded while "Save As" used a second,
        // unguarded copy of the same collection logic, so a corrupting save still got through.
        // Both must go through TryCollectCurrentExtensions, and nothing may collect markers
        // for saving on its own.
        var source = ReadSource("MainWindow.LayoutEditor.partial.cs");

        foreach (var handler in new[]
                 {
                     "private async void OnSaveLayoutButtonClick",
                     "private async void OnSaveAsConfirmButtonClick"
                 })
        {
            var handlerIndex = source.IndexOf(handler, StringComparison.Ordinal);
            Assert.True(handlerIndex >= 0, $"{handler} not found.");

            var guardIndex = source.IndexOf(
                "TryCollectCurrentExtensions(", handlerIndex, StringComparison.Ordinal);
            Assert.True(
                guardIndex >= 0,
                $"{handler} must capture markers through TryCollectCurrentExtensions, which " +
                "verifies both layout scope and marker geometry before anything is written.");
        }

        Assert.DoesNotContain("LayoutEditorController.BuildExtensions", source);
    }

    [Fact]
    public void GuardedCapture_TakesScopeFromTheSessionAndChecksGeometryBeforeBuilding()
    {
        var source = ReadSource("MainWindow.LayoutEditorGeometry.partial.cs");

        var methodIndex = source.IndexOf(
            "private ExtensionCollectionStatus TryCollectCurrentExtensions", StringComparison.Ordinal);
        Assert.True(methodIndex >= 0, "TryCollectCurrentExtensions not found.");

        // Scope is no longer re-derived and re-checked: it comes from the session captured on
        // entry, which navigation cannot write. There is no "wrong layout" case left to test.
        var sessionIndex = source.IndexOf("_layoutEditor.ActiveSession", methodIndex, StringComparison.Ordinal);
        var staleIndex = source.IndexOf("session.MatchesView(", methodIndex, StringComparison.Ordinal);
        var geometryIndex = source.IndexOf("FindNonFiniteMarkers", methodIndex, StringComparison.Ordinal);
        var buildIndex = source.IndexOf("BuildExtensions(", methodIndex, StringComparison.Ordinal);

        Assert.True(sessionIndex >= 0 && sessionIndex < buildIndex,
            "Capture must take its scope from the edit session, not ambient state.");
        Assert.True(staleIndex >= 0 && staleIndex < buildIndex,
            "Capture must reject geometry captured against a view the session no longer matches.");
        Assert.True(geometryIndex >= 0 && geometryIndex < buildIndex,
            "Geometry must be verified before building.");

        // The ambient key must not creep back into the capture path.
        var methodEnd = source.IndexOf("private ", buildIndex, StringComparison.Ordinal);
        var body = source.Substring(methodIndex, methodEnd - methodIndex);
        Assert.DoesNotContain("CurrentLayoutKey", body);
    }

    [Fact]
    public void ZoomAnimation_DoesNotClaimFullMapKeySpeculatively()
    {
        var source = ReadSource("MainWindow.Navigation.partial.cs");

        var methodIndex = source.IndexOf(
            "private ManualLayout? TryLoadFullMapManualLayoutForAnimation", StringComparison.Ordinal);
        Assert.True(methodIndex >= 0, "TryLoadFullMapManualLayoutForAnimation not found.");

        var methodEnd = source.IndexOf(
            "private void ApplyManualLayoutDuringAnimation", methodIndex, StringComparison.Ordinal);
        Assert.True(methodEnd > methodIndex, "Could not bound TryLoadFullMapManualLayoutForAnimation.");

        var body = source.Substring(methodIndex, methodEnd - methodIndex);

        // The animation load path must not claim edit scope by any means — neither the ambient
        // setter nor a session.
        Assert.DoesNotContain("SetLayoutKey", body);
        Assert.DoesNotContain("BeginEditSession", body);

        // Sentinel: a "does not contain" test passes for free once the thing it names is gone.
        // Assert at least one scope-claiming symbol still exists somewhere, so deleting
        // SetLayoutKey in Phase C makes this test fail and demand re-targeting rather than
        // quietly protecting nothing.
        var controller = ReadSource("Services/LayoutEditorController.cs");
        Assert.True(
            controller.Contains("SetLayoutKey", StringComparison.Ordinal) ||
            controller.Contains("BeginEditSession", StringComparison.Ordinal),
            "Neither scope-claiming API exists any more — re-target this test at whatever replaced them.");
    }

    [Fact]
    public void SingleLocationZoom_PrefersAHandMadeZoomedLayoutOverTheFullMapOne()
    {
        // Precedence is by origin, not scope: a Manual zoomed layout is a more specific deliberate
        // choice than a Manual full-map one. Without this, a zoomed layout could be saved and then
        // never displayed, because the full-map layout always won.
        var source = ReadSource("MainWindow.Navigation.partial.cs");

        var methodIndex = source.IndexOf(
            "private bool TryApplyFullMapLayoutForZoomedSingle", StringComparison.Ordinal);
        Assert.True(methodIndex >= 0, "TryApplyFullMapLayoutForZoomedSingle not found.");

        var guardIndex = source.IndexOf(
            "HasManualLayoutForZoomedView(cluster)", methodIndex, StringComparison.Ordinal);
        var applyIndex = source.IndexOf("ApplyManualLayout(layout)", methodIndex, StringComparison.Ordinal);

        Assert.True(guardIndex >= 0, "The full-map replay must yield to a hand-made zoomed layout.");
        Assert.True(guardIndex < applyIndex, "The precedence check must run before applying.");

        // The same rule must gate ShowZoomedView's preferFullMapLayout branch, or the two paths
        // would disagree about which layout is in effect.
        Assert.Contains(
            "cluster.IsSingleLocation && !HasManualLayoutForZoomedView(cluster)",
            source);
    }

    [Fact]
    public void OnSizeChanged_DoesNotRePlaceMarkersDuringEdit()
    {
        var source = ReadSource("MainWindow.xaml.cs");

        var handlerIndex = source.IndexOf("private void OnSizeChanged", StringComparison.Ordinal);
        Assert.True(handlerIndex >= 0, "OnSizeChanged not found.");

        var guardIndex = source.IndexOf("_layoutEditor.IsEditMode", handlerIndex, StringComparison.Ordinal);
        var updateIndex = source.IndexOf("UpdateMarkerPositions()", handlerIndex, StringComparison.Ordinal);

        Assert.True(guardIndex >= 0, "OnSizeChanged must not re-place markers while edit mode is active.");
        Assert.True(
            guardIndex < updateIndex,
            "The edit-mode guard must run before UpdateMarkerPositions, which clears the " +
            "marker-to-line map a save depends on.");

        // Skipping the update leaves endpoints in pre-resize screen space while the viewport moves
        // on. That no longer needs flagging here: the session captured the viewport it was derived
        // against, so the save path detects the mismatch itself. Behaviour is covered by
        // LayoutEditSessionTests.MatchesView_AfterAResize_IsFalse; this only pins that the handler
        // does not resurrect a flag to do the same job.
        Assert.DoesNotContain("MarkEditSessionGeometryStale", source);
    }

    [Fact]
    public void CollapseGuard_StandsDownOnlyWhenEveryJudgedMarkerWasMoved()
    {
        // Dragging every head onto its own anchor is a legitimate arrangement of zero-length
        // extensions, so the backstop must yield to it. But one drag — or a click with no motion —
        // must not vouch for the whole group, or incidental input would let genuinely collapsed
        // geometry through. Movement is therefore recorded per marker, and the guard stands down
        // only when every marker it would judge is covered.
        var source = ReadSource("MainWindow.LayoutEditorGeometry.partial.cs");

        Assert.Contains("expectedExtended.IsSubsetOf(_draggedLocationsThisEditSession)", source);

        // A press without motion is not a decision about where the head belongs.
        Assert.Contains("if (GeometryMath.ArePointsCoincident(from, to)) return;", source);

        var dragSource = ReadSource("MainWindow.LayoutEditorDrag.partial.cs");
        Assert.Contains("RecordDeliberateDrag(_draggedMarker,", dragSource);
    }

    [Fact]
    public void MarkerEndpoint_ReportsWhetherItCouldBeResolved()
    {
        var source = ReadSource("MainWindow.LayoutEditorGeometry.partial.cs");

        Assert.Contains("private bool TryGetMarkerEndpoint(", source);

        // The capture loop must branch on the resolution result rather than discarding it.
        // Matched loosely on purpose: this guards the behavior, not the local variable's name.
        var captureIndex = source.IndexOf(
            "private ExtensionCollectionStatus TryCollectCurrentExtensions", StringComparison.Ordinal);
        Assert.True(captureIndex >= 0, "TryCollectCurrentExtensions not found.");

        var branchIndex = source.IndexOf("if (!TryGetMarkerEndpoint(", captureIndex, StringComparison.Ordinal);
        Assert.True(
            branchIndex >= 0,
            "The capture loop must record markers whose endpoint could not be resolved.");
    }
}
