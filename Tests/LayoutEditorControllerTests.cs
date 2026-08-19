using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using InteractiveWorldMap.Models;
using InteractiveWorldMap.Services;
using InteractiveWorldMap.Tests.TestHelpers;
using Xunit;

namespace InteractiveWorldMap.Tests;

/// <summary>
/// Unit tests for <see cref="LayoutEditorController"/>.
/// </summary>
public class LayoutEditorControllerTests
{
    // ─── Helpers ─────────────────────────────────────────────────────────────

    private static (LayoutEditorController Controller, ManualLayoutManager Manager, MockLogger Logger, string TempDir)
        Make()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "iwm-lec-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        var layoutPath = Path.Combine(tempDir, "layouts.json");
        var logger = new MockLogger();
        var manager = new ManualLayoutManager(layoutPath, logger);
        var config = new VisualConfig();
        var controller = new LayoutEditorController(manager, config, logger);
        return (controller, manager, logger, tempDir);
    }

    private static Location Loc(string id) => new Location { Id = id, Name = id };

    // ─── Constructor guards ───────────────────────────────────────────────────

    [Fact]
    public void Constructor_NullLayoutManager_Throws()
    {
        var log = new MockLogger();
        var config = new VisualConfig();
        Assert.Throws<ArgumentNullException>(() =>
            new LayoutEditorController(null!, config, log));
    }

    [Fact]
    public void Constructor_NullVisualConfig_Throws()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "iwm-lec-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        var manager = new ManualLayoutManager(Path.Combine(tempDir, "l.json"), new MockLogger());
        Assert.Throws<ArgumentNullException>(() =>
            new LayoutEditorController(manager, null!, new MockLogger()));
    }

    [Fact]
    public void Constructor_NullLogger_Throws()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "iwm-lec-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        var manager = new ManualLayoutManager(Path.Combine(tempDir, "l.json"), new MockLogger());
        Assert.Throws<ArgumentNullException>(() =>
            new LayoutEditorController(manager, new VisualConfig(), null!));
    }

    // ─── State transitions ────────────────────────────────────────────────────

    [Fact]
    public void IsEditMode_DefaultsFalse()
    {
        var (ctrl, _, _, _) = Make();
        Assert.False(ctrl.IsEditMode);
    }

    [Fact]
    public void EnterEditMode_SetsIsEditModeTrue()
    {
        var (ctrl, _, _, _) = Make();
        ctrl.EnterEditMode();
        Assert.True(ctrl.IsEditMode);
    }

    [Fact]
    public void EnterEditMode_RaisesEditModeEntered()
    {
        var (ctrl, _, _, _) = Make();
        var raised = false;
        ctrl.EditModeEntered += () => raised = true;

        ctrl.EnterEditMode();

        Assert.True(raised);
    }

    [Fact]
    public void ExitEditMode_SetsIsEditModeFalse()
    {
        var (ctrl, _, _, _) = Make();
        ctrl.EnterEditMode();
        ctrl.ExitEditMode();
        Assert.False(ctrl.IsEditMode);
    }

    [Fact]
    public void ExitEditMode_RaisesEditModeExited()
    {
        var (ctrl, _, _, _) = Make();
        var raised = false;
        ctrl.EditModeExited += () => raised = true;
        ctrl.EnterEditMode();

        ctrl.ExitEditMode();

        Assert.True(raised);
    }

    [Fact]
    public void SetLayoutKey_UpdatesCurrentLayoutKey()
    {
        var (ctrl, _, _, _) = Make();
        ctrl.SetLayoutKey("key-abc");
        Assert.Equal("key-abc", ctrl.CurrentLayoutKey);
    }

    [Fact]
    public void SetLayoutKey_Null_ClearsKey()
    {
        var (ctrl, _, _, _) = Make();
        ctrl.SetLayoutKey("key-abc");
        ctrl.SetLayoutKey(null);
        Assert.Null(ctrl.CurrentLayoutKey);
    }

    [Fact]
    public void SetLayoutKey_ChangingKey_ClearsActiveVariantIdentity()
    {
        // Variant ids are only unique within a group. Carrying one across a scope change makes
        // TrySave target a variant belonging to the previous layout.
        var (ctrl, _, _, _) = Make();
        ctrl.SetLayoutKey("key-first");
        ctrl.TrySave(OneExtension());
        Assert.Equal("manual-default", ctrl.ActiveVariantId);

        ctrl.SetLayoutKey("key-second");

        Assert.Null(ctrl.ActiveVariantId);
        Assert.Null(ctrl.ActiveVariantOrigin);
        Assert.Null(ctrl.ActiveVariantDisplayName);
    }

    [Fact]
    public void SetLayoutKey_SameKey_PreservesActiveVariantIdentity()
    {
        var (ctrl, _, _, _) = Make();
        ctrl.SetLayoutKey("key-same");
        ctrl.TrySave(OneExtension());

        ctrl.SetLayoutKey("key-same");

        Assert.Equal("manual-default", ctrl.ActiveVariantId);
        Assert.Equal(ManualLayoutOrigin.Manual, ctrl.ActiveVariantOrigin);
    }

    [Fact]
    public void TrySave_AfterKeyChange_DoesNotWriteIntoPreviousKeysVariant()
    {
        // Regression: a stale active variant plus a changed key wrote the new scope's geometry
        // into the previous scope's named variant.
        var (ctrl, manager, _, _) = Make();
        ctrl.SetLayoutKey("key-origin");
        ctrl.TrySaveAsVariant("Layout One", OneExtension());
        var originVariants = manager.ListVariants("key-origin");
        Assert.Single(originVariants);

        ctrl.SetLayoutKey("key-elsewhere");
        ctrl.TrySave(OneExtension());

        // The original group is untouched; the new group got its own default variant.
        Assert.Single(manager.ListVariants("key-origin"));
        Assert.Equal(originVariants[0].VariantId, manager.ListVariants("key-origin")[0].VariantId);
        Assert.Contains(manager.ListVariants("key-elsewhere"), v => v.VariantId == "manual-default");
    }

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
    public void IsCollapsedLayout_EmptyInput_IsFalse()
    {
        Assert.False(LayoutEditorController.IsCollapsedLayout(
            Array.Empty<(Location, Point, Point)>(),
            new HashSet<string>()));
    }

    [Fact]
    public void SaveLayout_KeepsBackupOfPreviousFile()
    {
        var (ctrl, _, _, tempDir) = Make();
        var layoutPath = Path.Combine(tempDir, "layouts.json");
        ctrl.SetLayoutKey("key-backup");

        ctrl.TrySave(OneExtension());
        Assert.True(File.Exists(layoutPath));
        Assert.False(File.Exists(layoutPath + ".bak"), "No backup expected before a second save.");

        ctrl.TrySave(OneExtension());

        Assert.True(File.Exists(layoutPath + ".bak"),
            "The previous layout file must be recoverable after an overwriting save.");
    }

    private static List<RadialExtension> OneExtension() => new()
    {
        new RadialExtension
        {
            Location         = Loc("x"),
            OriginalPosition = new Point(10, 10),
            ExtendedPosition = new Point(50, 50),
            Angle            = 45.0
        }
    };

    [Fact]
    public void SetManualLayoutActive_True_SetsFlag()
    {
        var (ctrl, _, _, _) = Make();
        ctrl.SetManualLayoutActive(true);
        Assert.True(ctrl.IsManualLayoutActive);
    }

    [Fact]
    public void SetManualLayoutActive_False_ClearsFlag()
    {
        var (ctrl, _, _, _) = Make();
        ctrl.SetManualLayoutActive(true);
        ctrl.SetManualLayoutActive(false);
        Assert.False(ctrl.IsManualLayoutActive);
    }

    [Fact]
    public void IsManualLayoutSuppressed_DefaultsFalse()
    {
        var (ctrl, _, _, _) = Make();
        Assert.False(ctrl.IsManualLayoutSuppressed);
    }

    [Fact]
    public void UnloadManualLayout_SuppressesAndDeactivates()
    {
        var (ctrl, _, _, _) = Make();
        ctrl.SetManualLayoutActive(true);

        ctrl.UnloadManualLayout();

        Assert.True(ctrl.IsManualLayoutSuppressed);
        Assert.False(ctrl.IsManualLayoutActive);
    }

    [Fact]
    public void SetManualLayoutActive_True_ClearsSuppression()
    {
        var (ctrl, _, _, _) = Make();
        ctrl.UnloadManualLayout();
        Assert.True(ctrl.IsManualLayoutSuppressed);

        ctrl.SetManualLayoutActive(true);

        Assert.False(ctrl.IsManualLayoutSuppressed);
        Assert.True(ctrl.IsManualLayoutActive);
    }

    [Fact]
    public void SetManualLayoutActive_False_LeavesSuppressionUntouched()
    {
        var (ctrl, _, _, _) = Make();
        ctrl.UnloadManualLayout();

        // Auto-apply paths call SetManualLayoutActive(false) when no layout is found; that must not
        // clear a session unload.
        ctrl.SetManualLayoutActive(false);

        Assert.True(ctrl.IsManualLayoutSuppressed);
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

    // ─── TrySave ─────────────────────────────────────────────────────────────

    [Fact]
    public void TrySave_NullExtensions_Throws()
    {
        var (ctrl, _, _, _) = Make();
        ctrl.SetLayoutKey("key1");
        Assert.Throws<ArgumentNullException>(() => ctrl.TrySave(null!));
    }

    [Fact]
    public void TrySave_NullKey_ReturnsFalseAndLogs()
    {
        var (ctrl, _, logger, _) = Make();
        // CurrentLayoutKey is null (default)
        var result = ctrl.TrySave(new List<RadialExtension>());
        Assert.False(result);
        Assert.NotEmpty(logger.WarningMessages);
    }

    [Fact]
    public void TrySave_ValidKey_ReturnsTrueAndSetsActive()
    {
        var (ctrl, _, _, tempDir) = Make();
        ctrl.SetLayoutKey("key-save");
        var ext = new List<RadialExtension>
        {
            new RadialExtension
            {
                Location         = Loc("x"),
                OriginalPosition = new Point(10, 10),
                ExtendedPosition = new Point(50, 50),
                Angle            = 45.0
            }
        };
        bool saved = ctrl.TrySave(ext);
        Assert.True(saved);
        Assert.True(ctrl.IsManualLayoutActive);
    }

    [Fact]
    public void TrySave_ValidKey_RaisesManualLayoutActivityChanged()
    {
        var (ctrl, _, _, _) = Make();
        ctrl.SetLayoutKey("key-save-event");
        bool? activeState = null;
        ctrl.ManualLayoutActivityChanged += active => activeState = active;

        bool saved = ctrl.TrySave(new List<RadialExtension>
        {
            new RadialExtension
            {
                Location = Loc("event"),
                OriginalPosition = new Point(0, 0),
                ExtendedPosition = new Point(20, 20),
                Angle = 45.0
            }
        });

        Assert.True(saved);
        Assert.True(activeState);
    }

    // ─── TryDelete ────────────────────────────────────────────────────────────

    [Fact]
    public void TryDelete_NullKey_ReturnsFalseAndLogs()
    {
        var (ctrl, _, logger, _) = Make();
        var result = ctrl.TryDelete();
        Assert.False(result);
        Assert.NotEmpty(logger.WarningMessages);
    }

    [Fact]
    public void TryDelete_AfterSave_ReturnsTrueAndClearsActive()
    {
        var (ctrl, _, _, _) = Make();
        ctrl.SetLayoutKey("key-del");
        var ext = new List<RadialExtension>
        {
            new RadialExtension
            {
                Location         = Loc("y"),
                OriginalPosition = new Point(0, 0),
                ExtendedPosition = new Point(30, 30),
                Angle            = 0.0
            }
        };
        ctrl.TrySave(ext);
        Assert.True(ctrl.IsManualLayoutActive);

        bool deleted = ctrl.TryDelete();
        Assert.True(deleted);
        Assert.False(ctrl.IsManualLayoutActive);
    }

    [Fact]
    public void TryDelete_AfterSave_RaisesManualLayoutActivityChanged()
    {
        var (ctrl, _, _, _) = Make();
        ctrl.SetLayoutKey("key-del-event");
        ctrl.TrySave(new List<RadialExtension>
        {
            new RadialExtension
            {
                Location = Loc("event"),
                OriginalPosition = new Point(0, 0),
                ExtendedPosition = new Point(20, 20),
                Angle = 45.0
            }
        });
        bool? activeState = null;
        ctrl.ManualLayoutActivityChanged += active => activeState = active;

        bool deleted = ctrl.TryDelete();

        Assert.True(deleted);
        Assert.False(activeState);
    }

    // ─── TryLoad ─────────────────────────────────────────────────────────────

    [Fact]
    public void TryLoad_MissingKey_ReturnsNull()
    {
        var (ctrl, _, _, _) = Make();
        var layout = ctrl.TryLoad("nonexistent");
        Assert.Null(layout);
    }

    [Fact]
    public void TryLoad_AfterSave_ReturnsLayout()
    {
        var (ctrl, _, _, _) = Make();
        ctrl.SetLayoutKey("key-load");
        var ext = new List<RadialExtension>
        {
            new RadialExtension
            {
                Location         = Loc("z"),
                OriginalPosition = new Point(5, 5),
                ExtendedPosition = new Point(55, 55),
                Angle            = 90.0
            }
        };
        ctrl.TrySave(ext);

        var loaded = ctrl.TryLoad("key-load");
        Assert.NotNull(loaded);
    }

    // ─── ExitEditMode manual-layout replay prerequisites ─────────────────────
    // These tests verify the controller-level invariants that MainWindow.ExitEditMode
    // relies on when deciding to replay the manual layout (the Phase 1 fix).

    [Fact]
    public void ExitEditMode_AfterTrySave_IsManualLayoutActiveRemainsTrue()
    {
        var (ctrl, _, _, _) = Make();
        ctrl.SetLayoutKey("key-exit-roundtrip");
        ctrl.TrySave(new List<RadialExtension>
        {
            new RadialExtension
            {
                Location         = Loc("a"),
                OriginalPosition = new Point(0, 0),
                ExtendedPosition = new Point(30, 30),
                Angle            = 45.0
            }
        });
        Assert.True(ctrl.IsManualLayoutActive);

        ctrl.ExitEditMode();

        // IsManualLayoutActive must survive ExitEditMode so MainWindow's branch fires.
        Assert.True(ctrl.IsManualLayoutActive);
    }

    [Fact]
    public void TryLoad_AfterSaveAndExitEditMode_ReturnsLayout()
    {
        var (ctrl, _, _, _) = Make();
        ctrl.SetLayoutKey("key-load-after-exit");
        ctrl.TrySave(new List<RadialExtension>
        {
            new RadialExtension
            {
                Location         = Loc("b"),
                OriginalPosition = new Point(5, 5),
                ExtendedPosition = new Point(50, 50),
                Angle            = 90.0
            }
        });
        ctrl.ExitEditMode();

        // TryLoad must return the saved layout so MainWindow's ExitEditMode branch can replay it.
        var layout = ctrl.TryLoad("key-load-after-exit");
        Assert.NotNull(layout);
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

    [Fact]
    public void GetVariants_WithNoCurrentLayoutKey_ReturnsEmpty()
    {
        var (ctrl, _, _, _) = Make();

        Assert.Empty(ctrl.GetVariants());
    }

    [Fact]
    public void GetVariants_WithCurrentLayoutKey_ReturnsSavedVariants()
    {
        var (ctrl, _, _, _) = Make();
        ctrl.SetLayoutKey("key-variants");
        Assert.True(ctrl.TrySave(new List<RadialExtension>
        {
            new()
            {
                Location = Loc("alpha"),
                OriginalPosition = new Point(0, 0),
                ExtendedPosition = new Point(30, 30),
                Angle = 45
            }
        }));

        var variants = ctrl.GetVariants();

        var variant = Assert.Single(variants);
        Assert.Equal("manual-default", variant.VariantId);
        Assert.True(variant.IsSelected);
    }

    [Fact]
    public void SwitchToVariant_WithNoCurrentLayoutKey_ReturnsNull()
    {
        var (ctrl, _, _, _) = Make();

        Assert.Null(ctrl.SwitchToVariant("manual-default"));
    }

    [Fact]
    public void SwitchToVariant_WithMissingVariant_ReturnsNull()
    {
        var (ctrl, _, _, _) = Make();
        ctrl.SetLayoutKey("key-switch");
        Assert.True(ctrl.TrySave(new List<RadialExtension>
        {
            new()
            {
                Location = Loc("alpha"),
                OriginalPosition = new Point(0, 0),
                ExtendedPosition = new Point(30, 30),
                Angle = 45
            }
        }));

        Assert.Null(ctrl.SwitchToVariant("missing"));
    }

    [Fact]
    public void SwitchToVariant_WithValidVariant_UpdatesActiveVariant()
    {
        var (ctrl, _, _, _) = Make();
        ctrl.SetLayoutKey("key-switch-valid");
        var extensions = new List<RadialExtension>
        {
            new()
            {
                Location = Loc("alpha"),
                OriginalPosition = new Point(0, 0),
                ExtendedPosition = new Point(30, 30),
                Angle = 45
            }
        };
        Assert.True(ctrl.TrySave(extensions));
        Assert.True(ctrl.TrySaveAsVariant("Wide Variant", extensions));
        var variantId = ctrl.ActiveVariantId!;
        Assert.NotEqual("manual-default", variantId);
        Assert.NotNull(ctrl.SwitchToVariant("manual-default"));

        var loaded = ctrl.SwitchToVariant(variantId);

        Assert.NotNull(loaded);
        Assert.Equal(variantId, ctrl.ActiveVariantId);
        Assert.Equal(ManualLayoutOrigin.Manual, ctrl.ActiveVariantOrigin);
        Assert.Equal("Wide Variant", ctrl.ActiveVariantDisplayName);
    }

    [Fact]
    public void TrySaveAsVariant_WithNullExtensions_Throws()
    {
        var (ctrl, _, _, _) = Make();
        ctrl.SetLayoutKey("key-save-as-null");

        Assert.Throws<ArgumentNullException>(() => ctrl.TrySaveAsVariant("Variant", null!));
    }

    [Fact]
    public void TrySaveAsVariant_WithNoCurrentLayoutKey_ReturnsFalse()
    {
        var (ctrl, _, logger, _) = Make();

        var saved = ctrl.TrySaveAsVariant("Variant", new List<RadialExtension>());

        Assert.False(saved);
        Assert.Contains(logger.WarningMessages, message => message.Contains("CurrentLayoutKey"));
    }

    [Fact]
    public void TrySaveAsVariant_WithBlankName_ReturnsFalse()
    {
        var (ctrl, _, _, _) = Make();
        ctrl.SetLayoutKey("key-save-as-blank");

        var saved = ctrl.TrySaveAsVariant("  ", new List<RadialExtension>());

        Assert.False(saved);
    }

    [Fact]
    public void TrySaveAsVariant_WithName_SlugifiesVariantIdAndRaisesEvent()
    {
        var (ctrl, _, _, _) = Make();
        ctrl.SetLayoutKey("key-save-as");
        var events = 0;
        ctrl.VariantsChanged += _ => events++;
        var extensions = new List<RadialExtension>
        {
            new()
            {
                Location = Loc("alpha"),
                OriginalPosition = new Point(0, 0),
                ExtendedPosition = new Point(30, 30),
                Angle = 45
            }
        };

        var saved = ctrl.TrySaveAsVariant("Wide Variant!! Name With Extra Text", extensions);

        Assert.True(saved);
        Assert.StartsWith("wide-variant-name-w", ctrl.ActiveVariantId);
        Assert.Equal("Wide Variant!! Name With Extra Text", ctrl.ActiveVariantDisplayName);
        Assert.True(ctrl.IsManualLayoutActive);
        Assert.Equal(1, events);
    }

    [Fact]
    public void TryDeleteActiveVariant_WithNoActiveVariant_ReturnsFalse()
    {
        var (ctrl, _, logger, _) = Make();

        Assert.False(ctrl.TryDeleteActiveVariant());
        Assert.Contains(logger.WarningMessages, message => message.Contains("no active variant"));
    }

    [Fact]
    public void TryDeleteActiveVariant_WithSavedManualVariant_DeletesAndRaisesEvent()
    {
        var (ctrl, _, _, _) = Make();
        ctrl.SetLayoutKey("key-delete-active");
        var events = 0;
        ctrl.VariantsChanged += _ => events++;
        var extensions = new List<RadialExtension>
        {
            new()
            {
                Location = Loc("alpha"),
                OriginalPosition = new Point(0, 0),
                ExtendedPosition = new Point(30, 30),
                Angle = 45
            }
        };
        Assert.True(ctrl.TrySave(extensions));
        Assert.True(ctrl.TrySaveAsVariant("Variant B", extensions));

        var deleted = ctrl.TryDeleteActiveVariant();

        Assert.True(deleted);
        Assert.Equal("manual-default", ctrl.ActiveVariantId);
        Assert.True(ctrl.IsManualLayoutActive);
        Assert.Equal(3, events);
    }
}
