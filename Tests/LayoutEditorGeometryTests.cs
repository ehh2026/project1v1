using System;
using System.Collections.Generic;
using System.Windows;
using InteractiveWorldMap.Models;
using InteractiveWorldMap.Services;
using InteractiveWorldMap.Tests.TestHelpers;
using Xunit;

using static InteractiveWorldMap.Tests.TestHelpers.LayoutEditorTestFixtures;

namespace InteractiveWorldMap.Tests;

/// <summary>
/// Geometry and validation tests for <see cref="LayoutEditorController"/>: building extensions from
/// captured marker positions, and the guards that decide whether captured geometry is safe to save.
/// </summary>
/// <remarks>
/// Split from <c>LayoutEditorControllerTests</c>, which covers edit-session state and persistence.
/// The seam is computation versus state: nothing here needs a saved layout on disk except where a
/// guard is explicitly about persistence.
/// </remarks>
public class LayoutEditorGeometryTests
{
    // ─── FindNonFiniteMarkers ─────────────────────────────────────────────────

    [Fact]
    public void FindNonFiniteMarkers_AllFinite_ReturnsEmpty()
    {
        var bad = LayoutEditorController.FindNonFiniteMarkers(new[]
        {
            (Loc("a"), new Point(50, 50), new Point(10, 10))
        });

        Assert.Empty(bad);
    }

    [Fact]
    public void FindNonFiniteMarkers_NaNCoordinate_IsReported()
    {
        // Canvas.GetLeft returns NaN when a position was never set, so an un-laid-out marker
        // reaches the save path with NaN coordinates.
        var bad = LayoutEditorController.FindNonFiniteMarkers(new[]
        {
            (Loc("good"), new Point(50, 50),           new Point(10, 10)),
            (Loc("bad"),  new Point(double.NaN, 50),   new Point(10, 10))
        });

        Assert.Equal(new[] { "bad" }, bad);
    }

    [Fact]
    public void FindNonFiniteMarkers_InfiniteCoordinate_IsReported()
    {
        var bad = LayoutEditorController.FindNonFiniteMarkers(new[]
        {
            (Loc("inf"), new Point(50, 50), new Point(double.PositiveInfinity, 10))
        });

        Assert.Equal(new[] { "inf" }, bad);
    }

    [Fact]
    public void FindNonFiniteMarkers_ZeroLengthExtension_IsNotReported()
    {
        // A pin with no radial extension sits exactly on its anchor. That is legitimate and must
        // not block a save — only unresolvable or non-finite geometry does.
        var bad = LayoutEditorController.FindNonFiniteMarkers(new[]
        {
            (Loc("onAnchor"), new Point(10, 10), new Point(10, 10))
        });

        Assert.Empty(bad);
    }

    [Fact]
    public void BuildExtensions_ZeroLengthDelta_ProducesVerticalStub()
    {
        // Documents the exact shape of the reported bug. Coincident points give dx = dy = 0, and
        // Math.Atan2(0, -0.0) is pi — not 0 — because negating zero yields negative zero. So a
        // marker whose endpoint could not be resolved is persisted as a zero-length extension at
        // 180 degrees: the "short vertical stub" users saw, all pins pointing the same way.
        // Reaching this state is now prevented by rejecting unresolved endpoints before saving.
        var result = LayoutEditorController.BuildExtensions(new[]
        {
            (Loc("x"), new Point(10, 10), new Point(10, 10))
        });

        Assert.Single(result);
        Assert.Equal(180.0, result[0].Angle, 3);
        Assert.Equal(result[0].OriginalPosition, result[0].ExtendedPosition);
    }

    // ─── IsCollapsedLayout ────────────────────────────────────────────────────

    [Fact]
    public void IsCollapsedLayout_DenseGroupAllOnAnchor_IsTrue()
    {
        // The reported failure: a dense cluster whose pins all snapped to short vertical stubs.
        var dense = new HashSet<string> { "a", "b", "c" };

        Assert.True(LayoutEditorController.IsCollapsedLayout(
            new[]
            {
                (Loc("a"), new Point(10, 10), new Point(10, 10)),
                (Loc("b"), new Point(20, 20), new Point(20, 20)),
                (Loc("c"), new Point(30, 30), new Point(30, 30))
            },
            dense));
    }

    [Fact]
    public void IsCollapsedLayout_SparseViewAllOnAnchor_IsFalse()
    {
        // Pins too far apart to form a dense group are drawn as default stubs by design. A zoomed
        // view of a few scattered pins is legitimately all stubs and must remain saveable.
        Assert.False(LayoutEditorController.IsCollapsedLayout(
            new[]
            {
                (Loc("far1"), new Point(10, 10),   new Point(10, 10)),
                (Loc("far2"), new Point(400, 400), new Point(400, 400))
            },
            new HashSet<string>()));
    }

    [Fact]
    public void IsCollapsedLayout_OneDenseMemberStillExtended_IsFalse()
    {
        var dense = new HashSet<string> { "a", "b" };

        Assert.False(LayoutEditorController.IsCollapsedLayout(
            new[]
            {
                (Loc("a"), new Point(10, 10), new Point(10, 10)),
                (Loc("b"), new Point(99, 60), new Point(20, 20))
            },
            dense));
    }

    [Fact]
    public void IsCollapsedLayout_IgnoresMarkersOutsideDenseGroups()
    {
        // A collapsed dense group is still caught even when sparse stubs sit alongside it.
        var dense = new HashSet<string> { "a", "b" };

        Assert.True(LayoutEditorController.IsCollapsedLayout(
            new[]
            {
                (Loc("a"),      new Point(10, 10),   new Point(10, 10)),
                (Loc("b"),      new Point(20, 20),   new Point(20, 20)),
                (Loc("sparse"), new Point(400, 400), new Point(400, 400))
            },
            dense));
    }

    [Fact]
    public void FindExpectedExtendedLocations_ClassifiesFromSourceCoordinates_NotScreen()
    {
        // DetectDenseGroups applies ProximityThresholdPixels to whatever positions it is handed,
        // and placement hands it Location.PixelX/PixelY. Classifying from projected screen
        // positions instead would scale the threshold with zoom, so the collapse guard would
        // disagree with the placement it mirrors. Source coords are close enough to group; the
        // screen coords supplied here are deliberately far apart.
        var (ctrl, _, _, _) = Make();

        var near1 = new Location { Id = "a", Name = "a", PixelX = 100, PixelY = 100 };
        var near2 = new Location { Id = "b", Name = "b", PixelX = 102, PixelY = 100 };
        var near3 = new Location { Id = "c", Name = "c", PixelX = 104, PixelY = 100 };

        var markerData = new List<(Location, Point, Point)>
        {
            (near1, new Point(0, 0),      new Point(0, 0)),
            (near2, new Point(900, 900),  new Point(900, 900)),
            (near3, new Point(1800, 100), new Point(1800, 100))
        };

        var expected = ctrl.FindExpectedExtendedLocations(markerData);

        Assert.Equal(3, expected.Count);
        Assert.Contains("a", expected);
        Assert.Contains("b", expected);
        Assert.Contains("c", expected);
    }

    [Fact]
    public void IsCollapsedLayout_EmptyInput_IsFalse()
    {
        Assert.False(LayoutEditorController.IsCollapsedLayout(
            Array.Empty<(Location, Point, Point)>(),
            new HashSet<string>()));
    }


    // ─── BuildExtensions (static) ─────────────────────────────────────────────

    [Fact]
    public void BuildExtensions_NullInput_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            LayoutEditorController.BuildExtensions(null!));
    }

    [Fact]
    public void BuildExtensions_EmptyInput_ReturnsEmpty()
    {
        var result = LayoutEditorController.BuildExtensions(
            Array.Empty<(Location, Point, Point)>());
        Assert.Empty(result);
    }

    [Fact]
    public void BuildExtensions_SingleMarker_ReturnsOneExtension()
    {
        var loc = Loc("a");
        var center = new Point(110, 200);
        var origin = new Point(100, 190);

        var result = LayoutEditorController.BuildExtensions(
            new[] { (loc, center, origin) });

        Assert.Single(result);
        Assert.Equal(loc, result[0].Location);
        Assert.Equal(origin, result[0].OriginalPosition);
        Assert.Equal(center, result[0].ExtendedPosition);
    }

    [Fact]
    public void BuildExtensions_AngleCalculatedFromDelta()
    {
        var loc = Loc("b");
        var origin = new Point(0, 0);
        var center = new Point(1, 0);   // dx=1, dy=0 → north-up: East = 90°

        var result = LayoutEditorController.BuildExtensions(
            new[] { (loc, center, origin) });

        // North-up convention (matches ApplyManualLayout replay: X + L*sin, Y - L*cos).
        Assert.Equal(90.0, result[0].Angle, 3);
    }

    [Fact]
    public void BuildExtensions_AngleNorthUp_RoundTrips()
    {
        // Verify BuildExtensions angle + length can reconstruct extendedPos via the
        // same sin/cos formula used in ApplyManualLayout.
        var loc = Loc("c");
        var origin = new Point(100, 100);
        var center = new Point(100, 50); // dx=0, dy=-50 → north-up: North = 0°

        var result = LayoutEditorController.BuildExtensions(
            new[] { (loc, center, origin) });

        var ext = result[0];
        var rad = ext.Angle * Math.PI / 180.0;
        var len = 50.0;
        var reconstructed = new Point(
            origin.X + len * Math.Sin(rad),
            origin.Y - len * Math.Cos(rad));

        Assert.Equal(center.X, reconstructed.X, 3);
        Assert.Equal(center.Y, reconstructed.Y, 3);
    }

    // ─── ValidateLayout ───────────────────────────────────────────────────────

    [Fact]
    public void ValidateLayout_NullInput_Throws()
    {
        var (ctrl, _, _, _) = Make();
        Assert.Throws<ArgumentNullException>(() => ctrl.ValidateLayout(null!));
    }

    [Fact]
    public void ValidateLayout_EmptyList_ReturnsNoIssues()
    {
        var (ctrl, _, _, _) = Make();
        var issues = ctrl.ValidateLayout(new List<RadialExtension>());
        Assert.Empty(issues);
    }

    [Fact]
    public void ValidateLayout_WellSeparatedMarkers_ReturnsNoIssues()
    {
        var (ctrl, _, _, _) = Make();
        var extensions = new List<RadialExtension>
        {
            new RadialExtension { Location = Loc("a"), OriginalPosition = new Point(0, 0),   ExtendedPosition = new Point(0,   100) },
            new RadialExtension { Location = Loc("b"), OriginalPosition = new Point(200, 0), ExtendedPosition = new Point(200, 100) }
        };
        var issues = ctrl.ValidateLayout(extensions);
        Assert.Empty(issues);
    }

    [Fact]
    public void ValidateLayout_OverlappingMarkers_ReportsIssue()
    {
        var (ctrl, _, _, _) = Make();
        var extensions = new List<RadialExtension>
        {
            new RadialExtension { Location = Loc("a"), OriginalPosition = new Point(0, 0), ExtendedPosition = new Point(100, 100) },
            new RadialExtension { Location = Loc("b"), OriginalPosition = new Point(5, 0), ExtendedPosition = new Point(102, 100) }
            // Extended positions ~2px apart → less than LocationMarkerSize (default 12)
        };
        var issues = ctrl.ValidateLayout(extensions);
        Assert.Contains(issues, s => s.Contains("overlap") || s.Contains("intersect") || s.Contains("close"));
    }


    [Fact]
    public void CreateLayoutApplications_VisibleMarkers_ReturnsPlacementData()
    {
        var (ctrl, _, _, _) = Make();
        var layout = new ManualLayout(
            "key",
            new List<ManualLayoutMarker>
            {
                new("visible", new Point(10, 10), new Point(40, 50), 45.0, 50.0)
            });

        var applications = ctrl.CreateLayoutApplications(layout, new[] { "visible" });

        var application = Assert.Single(applications);
        Assert.Equal("visible", application.LocationName);
        Assert.Equal(new Point(10, 10), application.OriginalPosition);
        Assert.Equal(new Point(40, 50), application.ExtendedPosition);
        Assert.True(application.RequiresExtensionLine);
    }

    [Fact]
    public void CreateLayoutApplications_MissingVisibleMarker_SkipsAndLogsInfo()
    {
        var (ctrl, _, logger, _) = Make();
        var layout = new ManualLayout(
            "key",
            new List<ManualLayoutMarker>
            {
                new("missing", new Point(10, 10), new Point(40, 50), 45.0, 50.0)
            });

        var applications = ctrl.CreateLayoutApplications(layout, new[] { "other" });

        Assert.Empty(applications);
        // Not-currently-visible layout markers are skipped by design — logged at info, not warn.
        Assert.DoesNotContain(logger.WarningMessages, message => message.Contains("missing"));
        Assert.Contains(logger.InfoMessages, message => message.Contains("missing"));
    }

    [Fact]
    public void CreateLayoutApplications_ShortOffset_DoesNotRequireExtensionLine()
    {
        var (ctrl, _, _, _) = Make();
        var layout = new ManualLayout(
            "key",
            new List<ManualLayoutMarker>
            {
                new("visible", new Point(10, 10), new Point(13, 14), 45.0, 5.0)
            });

        var application = Assert.Single(ctrl.CreateLayoutApplications(layout, new[] { "visible" }));

        Assert.False(application.RequiresExtensionLine);
    }
}
