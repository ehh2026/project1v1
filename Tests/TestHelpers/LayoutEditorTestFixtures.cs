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
        SweepStaleTempDirectories();

        var tempDir = Path.Combine(Path.GetTempPath(), TempDirectoryPrefix + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        var layoutPath = Path.Combine(tempDir, "layouts.json");
        var logger = new MockLogger();
        var manager = new ManualLayoutManager(layoutPath, logger);
        var config = new VisualConfig();
        var controller = new LayoutEditorController(manager, config, logger);
        return (controller, manager, logger, tempDir);
    }

    private const string TempDirectoryPrefix = "iwm-lec-";

    private static bool _sweptThisRun;

    /// <summary>
    /// Deletes layout-test temp directories left by earlier runs.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Every call to <see cref="Make"/> creates one and nothing deletes it, so they accumulate for
    /// as long as anyone keeps running the suite. Sweeping on the way in rather than disposing on
    /// the way out is deliberate: the fixture is used from eight test classes through a plain tuple
    /// return, and threading an <c>IDisposable</c> through all of them to fix a housekeeping
    /// problem would be a lot of churn for the size of the complaint.
    /// </para>
    /// <para>
    /// An age cut-off rather than a blanket delete, so a run in another process does not have its
    /// working directories removed underneath it. Best effort throughout: a directory that cannot
    /// be deleted is one another run is probably holding, and failing a test over it would be worse
    /// than leaving it.
    /// </para>
    /// </remarks>
    private static void SweepStaleTempDirectories()
    {
        if (_sweptThisRun) return;
        _sweptThisRun = true;

        try
        {
            var cutoff = DateTime.UtcNow.AddHours(-1);

            foreach (var dir in Directory.GetDirectories(Path.GetTempPath(), TempDirectoryPrefix + "*"))
            {
                try
                {
                    if (Directory.GetCreationTimeUtc(dir) < cutoff)
                        Directory.Delete(dir, recursive: true);
                }
                catch
                {
                    // In use, or gone already. Either way not this run's problem.
                }
            }
        }
        catch
        {
            // No temp directory to enumerate is not a test failure.
        }
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
