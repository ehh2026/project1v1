using System;
using System.Collections.Generic;
using InteractiveWorldMap.Models;
using InteractiveWorldMap.Services;
using InteractiveWorldMap.Tests.TestHelpers;
using Xunit;

using static InteractiveWorldMap.Tests.TestHelpers.LayoutEditorTestFixtures;

namespace InteractiveWorldMap.Tests;

/// <summary>
/// Tests for <see cref="LayoutEditSession"/> and its lifetime on
/// <see cref="LayoutEditorController"/>.
/// </summary>
/// <remarks>
/// The session replaces ambient <c>CurrentLayoutKey</c> state as the record of what an edit is
/// scoped to. These tests pin the two properties that make it safer than the field it replaces:
/// its key matches what the current view would derive, and it can tell when the view has moved out
/// from under it.
/// </remarks>
public class LayoutEditSessionTests
{
    private static ViewportState ZoomedViewport() =>
        ViewportState.CreateZoomedView(4000, 3000, 55, 8198, 5542, 1920, 1080);

    private static RadialExtensionConfig Config() => new()
    {
        MinLocationsForExtension = 3,
        ProximityThresholdPixels = 10.0,
        ExtensionLineLength = 50.0,
        MinimumLineLength = 13.0
    };

    private static List<Location> TwoLocations() => new()
    {
        new Location { Id = "a", Name = "Alpha", PixelX = 100, PixelY = 200 },
        new Location { Id = "b", Name = "Beta",  PixelX = 110, PixelY = 210 }
    };

    private static LayoutEditSession ClusterSession(
        ViewportState? viewport = null, double width = 1920, double height = 1080)
    {
        var vp = viewport ?? ZoomedViewport();
        var locations = TwoLocations();
        return new LayoutEditSession(
            LayoutKeyGenerator.DeriveEditSessionKey(locations, vp, Config()),
            LayoutScope.Cluster,
            locations,
            vp,
            width,
            height);
    }

    // ─── Scope ────────────────────────────────────────────────────────────────

    [Fact]
    public void ClusterSession_KeyMatchesWhatTheCurrentViewWouldDerive()
    {
        // The session must not be able to hold a key the view would not produce; that divergence
        // is what let a save land in the wrong scope.
        var vp = ZoomedViewport();
        var locations = TwoLocations();

        var session = ClusterSession(vp);

        Assert.Equal(LayoutKeyGenerator.DeriveEditSessionKey(locations, vp, Config()), session.LayoutKey);
        Assert.False(session.IsFullMap);
    }

    [Fact]
    public void FullMapSession_UsesTheFullMapKeyAndHasNoScopeLocations()
    {
        var vp = ZoomedViewport();

        var session = new LayoutEditSession(
            LayoutKeyGenerator.DeriveEditSessionKey(null, vp, Config()),
            LayoutScope.FullMap,
            Array.Empty<Location>(),
            vp,
            1920,
            1080);

        Assert.Equal(LayoutKeyGenerator.GenerateFullMapGroupKey(), session.LayoutKey);
        Assert.True(session.IsFullMap);
        Assert.Empty(session.ScopeLocations);
    }

    [Fact]
    public void ClusterAndFullMapSessions_NeverShareAKey()
    {
        var vp = ZoomedViewport();
        var cluster = ClusterSession(vp);

        Assert.NotEqual(LayoutKeyGenerator.GenerateFullMapGroupKey(), cluster.LayoutKey);
    }

    // ─── Staleness, which replaces the mutable flag ──────────────────────────

    [Fact]
    public void MatchesView_WithTheViewportItWasBuiltFrom_IsTrue()
    {
        var vp = ZoomedViewport();
        var session = ClusterSession(vp);

        Assert.True(session.MatchesView(vp, 1920, 1080));
    }

    [Fact]
    public void MatchesView_AfterAResize_IsFalse()
    {
        // A resize leaves markers in the previous coordinate space, so captured geometry must not
        // be saved. Derived from the captured viewport rather than tracked by a flag someone has
        // to remember to set.
        var session = ClusterSession();

        Assert.False(session.MatchesView(ZoomedViewport(), 1600, 900));
    }

    [Fact]
    public void MatchesView_AfterTheViewportMoves_IsFalse()
    {
        var session = ClusterSession();
        var moved = ViewportState.CreateZoomedView(5000, 3000, 55, 8198, 5542, 1920, 1080);

        Assert.False(session.MatchesView(moved, 1920, 1080));
    }

    [Fact]
    public void MatchesView_WithNoViewport_IsFalse()
    {
        var session = ClusterSession();

        Assert.False(session.MatchesView(null, 1920, 1080));
    }

    // ─── Lifetime on the controller ──────────────────────────────────────────

    [Fact]
    public void ActiveSession_DefaultsToNull()
    {
        var (ctrl, _, _, _) = Make();

        Assert.Null(ctrl.ActiveSession);
    }

    [Fact]
    public void BeginEditSession_ExposesTheSession()
    {
        var (ctrl, _, _, _) = Make();
        var session = ClusterSession();

        ctrl.BeginEditSession(session);

        Assert.Same(session, ctrl.ActiveSession);
    }

    [Fact]
    public void BeginEditSession_Null_Throws()
    {
        var (ctrl, _, _, _) = Make();

        Assert.Throws<ArgumentNullException>(() => ctrl.BeginEditSession(null!));
    }

    [Fact]
    public void EndEditSession_ClearsTheSession()
    {
        var (ctrl, _, _, _) = Make();
        ctrl.BeginEditSession(ClusterSession());

        ctrl.EndEditSession();

        Assert.Null(ctrl.ActiveSession);
    }

    [Fact]
    public void BeginEditSession_ReplacesAnySessionInProgress()
    {
        var (ctrl, _, _, _) = Make();
        ctrl.BeginEditSession(ClusterSession());
        var second = ClusterSession(ViewportState.CreateZoomedView(6000, 3000, 55, 8198, 5542, 1920, 1080));

        ctrl.BeginEditSession(second);

        Assert.Same(second, ctrl.ActiveSession);
    }

    // ─── Phase 6.1: naming the scope being edited ───────────────────────────

    private static LayoutEditSession ClusterOf(params string[] names) =>
        new("k",
            LayoutScope.Cluster,
            System.Array.ConvertAll(names, n => new Location { Id = n, Name = n }),
            SessionViewport(),
            SessionContainerWidth,
            SessionContainerHeight);

    [Fact]
    public void ScopeDescription_FullMap_SaysWholeMap()
    {
        var session = new LayoutEditSession(
            "fullmap", LayoutScope.FullMap, new List<Location>(),
            SessionViewport(), SessionContainerWidth, SessionContainerHeight);

        Assert.Equal("Whole map", session.ScopeDescription);
    }

    [Fact]
    public void ScopeDescription_Cluster_NamesItsLocations()
    {
        // The hash in the layout key distinguishes clusters perfectly well and tells the user
        // nothing. Names are the only part of a cluster identity anyone can recognise on sight.
        Assert.Equal("Cluster: New York, Newark", ClusterOf("New York", "Newark").ScopeDescription);
    }

    [Fact]
    public void ScopeDescription_LargeCluster_NamesAFewAndCountsTheRest()
    {
        // The panel is a fixed-width overlay, not a list view. A dozen names would push the buttons
        // off it, so past a few the count carries the rest.
        var description = ClusterOf("A", "B", "C", "D", "E").ScopeDescription;

        Assert.Equal("Cluster: A, B, C, +2 more", description);
    }

    [Fact]
    public void ScopeDescription_ClusterWithNoLocations_StillSaysItIsACluster()
    {
        // Degenerate, but the label must never come out empty: a blank scope line reads as
        // "no scope" when the session very much has one and a save will write to it.
        var session = new LayoutEditSession(
            "k", LayoutScope.Cluster, new List<Location>(),
            SessionViewport(), SessionContainerWidth, SessionContainerHeight);

        Assert.Equal("Cluster", session.ScopeDescription);
    }

    [Fact]
    public void ScopeDescription_UnnamedLocation_DoesNotProduceAGap()
    {
        Assert.Equal("Cluster: (unnamed), B", ClusterOf("  ", "B").ScopeDescription);
    }
}
