using System.Collections.Generic;
using System.Windows;
using InteractiveWorldMap.Models;
using InteractiveWorldMap.Services;
using InteractiveWorldMap.Utilities;
using Xunit;

using static InteractiveWorldMap.Tests.TestHelpers.LayoutEditorTestFixtures;

namespace InteractiveWorldMap.Tests;

/// <summary>
/// "Trap 1": editing a <see cref="RadialExtensionConfig"/> value in visual-config.json and losing
/// sight of saved cluster layouts. All four values are in the cluster key, but they do not all have
/// the same effect, and the difference is the whole answer.
///
/// <list type="bullet">
/// <item><c>ExtensionLineLength</c> and <c>MinimumLineLength</c> only change how a line is drawn.
/// The key moves, but the location hash and the zoom do not, and those are the only things
/// <c>AreKeysCompatible</c> compares — so the layout still resolves.</item>
/// <item><c>ProximityThresholdPixels</c> and <c>MinLocationsForExtension</c> decide which locations
/// form a cluster at all. Change one <i>far enough to move a location in or out</i> and the cluster
/// is a different set, which hashes differently, which is exactly what compatibility does compare —
/// the layout is orphaned. Change one without crossing that line and the membership, the hash and
/// therefore the layout are all untouched.</item>
/// </list>
///
/// So the grouping pair is conditional, not automatic: the cost is not in editing the number, it is
/// in the number landing on the other side of a gap between locations. That is invisible from the
/// config file, which is why the warning treats the whole pair as dangerous.
///
/// Both halves are measured end to end through <see cref="ManualLayoutManager"/>, and the second is
/// driven through <see cref="RadialExtensionCalculator.DetectDenseGroups"/> rather than a
/// hand-written location list — holding the locations fixed is precisely what hides it.
/// </summary>
public class RadialConfigChangeAndSavedLayoutsTests
{
    private static ViewportState Viewport() =>
        ViewportState.CreateZoomedView(4000, 3000, 55, 8198, 5542, 1920, 1080);

    private static List<Location> Cluster() => new()
    {
        new() { Name = "Alpha", PixelX = 100, PixelY = 200 },
        new() { Name = "Beta",  PixelX = 110, PixelY = 210 }
    };

    // ─── Line geometry: the layout survives ─────────────────────────────────

    private static (string OldKey, string NewKey) KeysAcrossALineLengthChange()
    {
        var before = new RadialExtensionConfig();
        var after = new RadialExtensionConfig
        {
            ExtensionLineLength = before.ExtensionLineLength + 25
        };

        return (LayoutKeyGenerator.GenerateKey(Cluster(), Viewport(), before),
                LayoutKeyGenerator.GenerateKey(Cluster(), Viewport(), after));
    }

    [Fact]
    public void ChangingHowLinesAreDrawn_LeavesTheLayoutLoadable()
    {
        var (_, manager, _, _) = Make();
        var (oldKey, newKey) = KeysAcrossALineLengthChange();

        manager.SaveVariant(oldKey, "manual-default", "Mine", ManualLayoutOrigin.Manual,
            OneExtension(), null, setAsDefault: true, setAsSelected: true);

        Assert.Equal("Mine", manager.LoadLayout(newKey)?.DisplayName);
    }

    [Fact]
    public void ChangingHowLinesAreDrawn_LeavesTheLayoutListed()
    {
        // Until Phase 6.8 this half failed while the one above passed: ListVariants matched the key
        // exactly, so the dropdown emptied while the map went on showing the layout. That is what
        // the "my layouts vanished" reports were, for these two settings.
        var (_, manager, _, _) = Make();
        var (oldKey, newKey) = KeysAcrossALineLengthChange();

        manager.SaveVariant(oldKey, "manual-default", "Mine", ManualLayoutOrigin.Manual,
            OneExtension(), null, setAsDefault: true, setAsSelected: true);

        var listed = manager.ListVariants(newKey);

        Assert.Single(listed);
        Assert.Equal("Mine", listed[0].DisplayName);
    }

    [Fact]
    public void SavingAfterALineLengthChange_LandsInANewGroup()
    {
        // Saving keys exactly, so the file accumulates a group per settings combination even where
        // the layouts remain reachable.
        var (_, manager, _, _) = Make();
        var (oldKey, newKey) = KeysAcrossALineLengthChange();

        manager.SaveVariant(oldKey, "manual-default", "Before", ManualLayoutOrigin.Manual,
            OneExtension(), null, setAsDefault: true, setAsSelected: false);
        manager.SaveVariant(newKey, "manual-default", "After", ManualLayoutOrigin.Manual,
            OneExtension(), null, setAsDefault: true, setAsSelected: false);

        Assert.Equal("Before", manager.LoadLayout(oldKey)?.DisplayName);
        Assert.Equal("After", manager.LoadLayout(newKey)?.DisplayName);
    }

    // ─── Clustering: the layout really is orphaned ──────────────────────────

    /// <summary>
    /// Three locations: two close together, one further out. A proximity threshold below the gap
    /// makes a cluster of two; above it, a cluster of three.
    /// </summary>
    private static Dictionary<Location, Point> SpreadOutLocations()
    {
        var a = new Location { Id = "a", Name = "Alpha", PixelX = 100, PixelY = 100 };
        var b = new Location { Id = "b", Name = "Beta", PixelX = 110, PixelY = 100 };
        var c = new Location { Id = "c", Name = "Gamma", PixelX = 145, PixelY = 100 };

        return new Dictionary<Location, Point>
        {
            [a] = new Point(100, 100),
            [b] = new Point(110, 100),
            [c] = new Point(145, 100)
        };
    }

    private static string KeyForFirstCluster(RadialExtensionConfig config)
    {
        var groups = new RadialExtensionCalculator(config).DetectDenseGroups(SpreadOutLocations());
        Assert.NotEmpty(groups);
        return LayoutKeyGenerator.GenerateKey(groups[0].Locations, Viewport(), config);
    }

    [Fact]
    public void ChangingTheProximityThreshold_OrphansTheSavedLayout()
    {
        // The cluster is a different set of locations afterwards, so it hashes differently, and the
        // hash is the one thing compatibility does compare. Nothing is deleted -- the layout is
        // still in the file, under a key nothing asks for any more.
        var tight = new RadialExtensionConfig { ProximityThresholdPixels = 20, MinLocationsForExtension = 2 };
        var loose = new RadialExtensionConfig { ProximityThresholdPixels = 60, MinLocationsForExtension = 2 };

        var tightKey = KeyForFirstCluster(tight);
        var looseKey = KeyForFirstCluster(loose);

        Assert.False(LayoutKeyGenerator.AreKeysCompatible(tightKey, looseKey));

        var (_, manager, _, _) = Make();
        manager.SaveVariant(tightKey, "manual-default", "Mine", ManualLayoutOrigin.Manual,
            OneExtension(), null, setAsDefault: true, setAsSelected: true);

        Assert.Null(manager.LoadLayout(looseKey));
        Assert.Empty(manager.ListVariants(looseKey));

        // And it comes back when the setting goes back.
        Assert.Equal("Mine", manager.LoadLayout(tightKey)?.DisplayName);
    }

    [Fact]
    public void ChangingTheProximityThreshold_WithoutMovingAnyone_LeavesTheLayoutLoadable()
    {
        // The counterpart to the test above, and the reason the grouping pair is described as
        // conditional rather than automatic. 20 and 25 both sit in the gap between Beta (110) and
        // Gamma (145), so the cluster is the same two locations either side of the change. The key
        // string moves -- p is in it -- but the hash does not, and the hash is what compatibility
        // reads. Without this, the docs would be telling people a threshold edit always costs them
        // their layouts, which would push them away from a setting that is usually free to tune.
        var before = new RadialExtensionConfig { ProximityThresholdPixels = 20, MinLocationsForExtension = 2 };
        var after = new RadialExtensionConfig { ProximityThresholdPixels = 25, MinLocationsForExtension = 2 };

        var beforeMembers = new RadialExtensionCalculator(before).DetectDenseGroups(SpreadOutLocations())[0].Locations;
        var afterMembers = new RadialExtensionCalculator(after).DetectDenseGroups(SpreadOutLocations())[0].Locations;

        // Guard the premise: if a future default moves these apart the test must fail loudly rather
        // than quietly stop testing what it claims to.
        Assert.Equal(beforeMembers.Count, afterMembers.Count);

        var oldKey = KeyForFirstCluster(before);
        var newKey = KeyForFirstCluster(after);

        Assert.NotEqual(oldKey, newKey);
        Assert.True(LayoutKeyGenerator.AreKeysCompatible(oldKey, newKey));

        var (_, manager, _, _) = Make();
        manager.SaveVariant(oldKey, "manual-default", "Mine", ManualLayoutOrigin.Manual,
            OneExtension(), null, setAsDefault: true, setAsSelected: true);

        Assert.Equal("Mine", manager.LoadLayout(newKey)?.DisplayName);
        Assert.Single(manager.ListVariants(newKey));
    }

    [Fact]
    public void RaisingMinLocationsForExtension_CanRemoveTheClusterEntirely()
    {
        // The other grouping setting, and a blunter version of the same thing: with the minimum
        // above the cluster size there is no cluster, so there is no key to look a layout up by.
        var pairs = new RadialExtensionConfig { ProximityThresholdPixels = 20, MinLocationsForExtension = 2 };
        var triplesOnly = new RadialExtensionConfig { ProximityThresholdPixels = 20, MinLocationsForExtension = 3 };

        Assert.NotEmpty(new RadialExtensionCalculator(pairs).DetectDenseGroups(SpreadOutLocations()));
        Assert.Empty(new RadialExtensionCalculator(triplesOnly).DetectDenseGroups(SpreadOutLocations()));
    }
}
