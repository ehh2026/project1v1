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
    public void OnEditLayoutButtonClick_BeginsASessionScopedToTheViewOnScreen()
    {
        var source = ReadSource("MainWindow.LayoutEditor.partial.cs");

        var handlerIndex = source.IndexOf("private void OnEditLayoutButtonClick", StringComparison.Ordinal);
        Assert.True(handlerIndex >= 0, "OnEditLayoutButtonClick not found.");

        var buildIndex = source.IndexOf("TryBuildEditSession()", handlerIndex, StringComparison.Ordinal);
        var beginIndex = source.IndexOf("BeginEditSession(", handlerIndex, StringComparison.Ordinal);

        Assert.True(
            buildIndex >= 0 && beginIndex > buildIndex,
            "Entering edit mode must build a session from the view on screen and begin it. That " +
            "session is the only record of what the edit will write to.");

        // There is no ambient key to inherit any more; this pins that none reappears.
        Assert.DoesNotContain("CurrentLayoutKey", source);

        // Entry must also adopt the session layout's variant identity. Identity used to arrive as
        // a side effect of navigation's probe loads; now that loading is side-effect free, only
        // this call establishes it, and without it a save silently targets "manual-default"
        // instead of the variant the user is editing. The controller-level test for that property
        // cannot see this wiring, so it is pinned here.
        var adoptIndex = source.IndexOf("LoadForEditSession()", handlerIndex, StringComparison.Ordinal);
        Assert.True(
            adoptIndex >= 0,
            "Entering edit mode must load the session's layout so its variant identity is adopted.");
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
        var applyIndex = source.IndexOf("ApplyManualLayout(layout, key)", methodIndex, StringComparison.Ordinal);

        Assert.True(guardIndex >= 0, "The full-map replay must yield to a hand-made zoomed layout.");
        Assert.True(guardIndex < applyIndex, "The precedence check must run before applying.");

        // The same rule must gate ShowZoomedView's preferFullMapLayout branch, or the two paths
        // would disagree about which layout is in effect.
        Assert.Contains(
            "cluster.IsSingleLocation && !HasManualLayoutForZoomedView(cluster)",
            source);
    }

    [Fact]
    public void StagedClusterLayout_CarriesTheKeyItWasResolvedFor()
    {
        // ShowZoomedView loads a cluster layout under one key but applies it later, after the
        // high-res region lands. LoadLayout can resolve a *compatible* group rather than an exact
        // one (a legacy sized key, a near-zoom match), so the returned layout's own GroupKey is not
        // always the key the view selected. Applying without the key lets ApplyManualLayout fall
        // back to layout.GroupKey, which puts the composite plan cache in a group the
        // post-save invalidation never reaches.
        var source = ReadSource("MainWindow.Navigation.partial.cs");

        // The layout and its key live in one field so they cannot be set or cleared separately and
        // drift apart — a tuple assignment is the only way to stage either of them.
        Assert.Contains("_stagedClusterLayout = (savedLayout, clusterKey)", source);
        Assert.Contains(
            "ApplyManualLayout(staged.Value.Layout, staged.Value.GroupKey)",
            source);

        // Staging must not outlive the call that did it. Cleared on entry, so no exit path — an
        // early return, the single-location full-map branch, or the catch — can leave a layout
        // staged for a cluster the user has already navigated away from.
        var methodIndex = source.IndexOf("private void ShowZoomedView", StringComparison.Ordinal);
        Assert.True(methodIndex >= 0, "ShowZoomedView not found.");

        var clearIndex = source.IndexOf("_stagedClusterLayout = null", methodIndex, StringComparison.Ordinal);
        var stageIndex = source.IndexOf("_stagedClusterLayout = (", methodIndex, StringComparison.Ordinal);
        Assert.True(
            clearIndex >= 0 && clearIndex < stageIndex,
            "ShowZoomedView must clear any previously staged layout before staging a new one.");
    }

    [Fact]
    public void UnloadLogsTheSessionKeyItCaptured_NotOneReadAfterTheSessionEnds()
    {
        // ExitEditMode ends the session, so anything read from ActiveSession afterwards is null.
        // The unload log is the only record of which saved group was suppressed; logging null
        // there is silent, which is why this is pinned rather than left to review.
        // Lives with the other two "stop using this layout" actions since the Phase 2 split.
        var source = ReadSource("MainWindow.LayoutEditorDelete.partial.cs");

        var handlerIndex = source.IndexOf(
            "private void OnUnloadLayoutButtonClick", StringComparison.Ordinal);
        Assert.True(handlerIndex >= 0, "OnUnloadLayoutButtonClick not found.");

        // Last member in its file, so the class close bounds it.
        var handlerEnd = source.LastIndexOf("    }", StringComparison.Ordinal);
        Assert.True(handlerEnd > handlerIndex, "Could not bound OnUnloadLayoutButtonClick.");

        var body = source.Substring(handlerIndex, handlerEnd - handlerIndex);

        var captureIndex = body.IndexOf(
            "var sessionKey = _layoutEditor.ActiveSession.LayoutKey", StringComparison.Ordinal);
        var exitIndex = body.IndexOf("ExitEditMode()", StringComparison.Ordinal);

        Assert.True(captureIndex >= 0, "Unload must capture the session key before ending the session.");
        Assert.True(captureIndex < exitIndex, "The capture must happen before ExitEditMode().");
        Assert.DoesNotContain("ActiveSession?.LayoutKey", body);
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
    public void CollapseGuard_HasNoDragBasedBypass()
    {
        // The guard once stood down when the user had dragged every marker it would judge, to allow
        // deliberately putting every head back on its own location marker. That was judged not a
        // real use case (smoke S10, dropped 2026-08-20), so the exception and its per-marker drag
        // tracking are gone and an all-anchor dense cluster is always refused.
        //
        // Pinned because the bypass was itself added in response to a review finding: without a
        // test, it is the kind of thing that gets reintroduced the next time someone reasons about
        // intent from coordinates.
        var source = ReadSource("MainWindow.LayoutEditorGeometry.partial.cs");

        Assert.DoesNotContain("_draggedLocationsThisEditSession", source);
        Assert.DoesNotContain("RecordDeliberateDrag", source);
        Assert.DoesNotContain("RecordDeliberateDrag", ReadSource("MainWindow.LayoutEditorDrag.partial.cs"));

        // The guard itself must remain, judging only markers placement would extend (smoke S8).
        Assert.Contains("IsCollapsedLayout(", source);
        Assert.Contains("FindExpectedExtendedLocations(", source);
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
