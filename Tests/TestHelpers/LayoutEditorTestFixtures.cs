using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using InteractiveWorldMap.Models;
using InteractiveWorldMap.Services;

namespace InteractiveWorldMap.Tests.TestHelpers;

/// <summary>
/// Shared construction helpers for <see cref="LayoutEditorController"/> tests.
/// </summary>
/// <remarks>
/// The controller is exercised against a real file-backed <see cref="ManualLayoutManager"/> pointed
/// at a temp directory rather than a fake, so persistence behaviour (variant selection, origin
/// precedence, backups) is covered for real. There is no <c>IManualLayoutManager</c> double in this
/// repo, and adding one would hide exactly the behaviour these tests care about.
/// </remarks>
internal static class LayoutEditorTestFixtures
{
    internal static (LayoutEditorController Controller, ManualLayoutManager Manager, MockLogger Logger, string TempDir)
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

    internal static Location Loc(string id) => new Location { Id = id, Name = id };

    /// <summary>
    /// An edit session scoped to <paramref name="layoutKey"/>, for tests that only care which
    /// layout is being written to.
    /// </summary>
    /// <remarks>
    /// Replaces the old <c>SetLayoutKey("k")</c> setup. The viewport is arbitrary but consistent,
    /// so <see cref="LayoutEditSession.MatchesView"/> succeeds when tests pass the same one back.
    /// Scope reads as a cluster because that is the case with a non-trivial key.
    /// </remarks>
    internal static LayoutEditSession SessionFor(string layoutKey) =>
        new(layoutKey,
            LayoutScope.Cluster,
            new[] { Loc("session-scope") },
            SessionViewport(),
            SessionContainerWidth,
            SessionContainerHeight);

    internal const double SessionContainerWidth = 1920;
    internal const double SessionContainerHeight = 1080;

    internal static ViewportState SessionViewport() =>
        ViewportState.CreateZoomedView(4000, 3000, 55, 8198, 5542, SessionContainerWidth, SessionContainerHeight);

    /// <summary>A single well-formed extension, for tests that just need something saveable.</summary>
    internal static List<RadialExtension> OneExtension() => new()
    {
        new RadialExtension
        {
            Location         = Loc("x"),
            OriginalPosition = new Point(10, 10),
            ExtendedPosition = new Point(50, 50),
            Angle            = 45.0
        }
    };
}
