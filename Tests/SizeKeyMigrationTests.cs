using System.Linq;
using InteractiveWorldMap.Models;
using InteractiveWorldMap.Services;
using Xunit;

using static InteractiveWorldMap.Tests.TestHelpers.LayoutEditorTestFixtures;

namespace InteractiveWorldMap.Tests;

/// <summary>
/// Phase 6.9. The viewport size was baked into cluster keys but never consulted by
/// <c>AreKeysCompatible</c>, so it fragmented each cluster into one group per window size while
/// changing nothing about which layout was found — the shipped file holds the same four clusters
/// four times over, one per size someone happened to run at.
///
/// Dropping it from new keys is easy. These cover the part that is not: what happens to the groups
/// already on disk.
/// </summary>
public class SizeKeyMigrationTests
{
    private static readonly RadialExtensionConfig KeyConfig = new();

    private static string CurrentKey(params string[] names)
    {
        var locations = names.Select(n => new Location { Id = n, Name = n }).ToList();
        var viewport = ViewportState.CreateZoomedView(4000, 3000, 55, 8198, 5542, 1920, 1080);
        return LayoutKeyGenerator.GenerateKey(locations, viewport, KeyConfig);
    }

    /// <summary>A key in the old shape: the current one with an s{W}x{H} put back into it.</summary>
    private static string LegacySizedKey(string current, string size)
    {
        var parts = current.Split('_').ToList();
        parts.Insert(4, size);   // after the hash, zoom, and the two centre halves
        return string.Join("_", parts);
    }

    [Fact]
    public void NewKeys_DoNotCarryTheViewportSize()
    {
        var key = CurrentKey("New York", "Newark");

        Assert.DoesNotContain("_s1920x", key);
        Assert.Contains("_z", key);
        Assert.Contains("_m3", key);
    }

    [Fact]
    public void TheSameClusterAtTwoWindowSizes_IsNowOneKey()
    {
        var locations = new[] { new Location { Id = "a", Name = "A" }, new Location { Id = "b", Name = "B" } }.ToList();

        var wide = LayoutKeyGenerator.GenerateKey(
            locations, ViewportState.CreateZoomedView(4000, 3000, 55, 8198, 5542, 1920, 1080), KeyConfig);
        var narrow = LayoutKeyGenerator.GenerateKey(
            locations, ViewportState.CreateZoomedView(4000, 3000, 55, 8198, 5542, 1280, 1080), KeyConfig);

        Assert.Equal(wide, narrow);
    }

    [Fact]
    public void ALegacySizedGroup_IsReadUnderTheNewKey()
    {
        var (_, manager, _, _) = Make();
        var current = CurrentKey("New York", "Newark");
        manager.SaveVariant(LegacySizedKey(current, "s161x101"), "manual-default", "Mine",
            ManualLayoutOrigin.Manual, OneExtension(), null, setAsDefault: true, setAsSelected: true);

        var variants = manager.ListVariants(current);

        Assert.Single(variants);
        Assert.Equal("Mine", variants[0].DisplayName);
        Assert.Equal(current, variants[0].GroupKey);
    }

    [Fact]
    public void SeveralSizedGroups_CollapseIntoOne()
    {
        // The shape of the shipped file: one cluster, one seed, four window sizes.
        var (_, manager, _, _) = Make();
        var current = CurrentKey("New York", "Newark");

        foreach (var size in new[] { "s149x112", "s161x101", "s179x101", "s241x101" })
        {
            manager.SaveVariant(LegacySizedKey(current, size), "seed-default", "Generated Seed",
                ManualLayoutOrigin.AutoSeed, OneExtension(), null, setAsDefault: true, setAsSelected: false);
        }

        var variants = manager.ListVariants(current);

        Assert.Single(variants);
        Assert.Equal(ManualLayoutOrigin.AutoSeed, variants[0].Origin);
    }

    [Fact]
    public void CollidingHandMadeVariants_AreKeptRatherThanDropped()
    {
        // Each sized group has its own "manual-default". Losing one to a merge would destroy work
        // the user never asked to delete, so the loser survives under a suffixed id — an awkward
        // name is recoverable, a missing layout is not.
        var (_, manager, _, _) = Make();
        var current = CurrentKey("New York", "Newark");

        manager.SaveVariant(LegacySizedKey(current, "s161x101"), "manual-default", "On the laptop",
            ManualLayoutOrigin.Manual, OneExtension(), null, setAsDefault: true, setAsSelected: false);
        manager.SaveVariant(LegacySizedKey(current, "s241x101"), "manual-default", "On the big screen",
            ManualLayoutOrigin.Manual, OneExtension(), null, setAsDefault: true, setAsSelected: false);

        var variants = manager.ListVariants(current);

        Assert.Equal(2, variants.Count);
        Assert.Contains(variants, v => v.DisplayName == "On the laptop");
        Assert.Contains(variants, v => v.DisplayName.StartsWith("On the big screen (from "));
        Assert.Single(variants.Where(v => v.IsDefault));
    }

    [Fact]
    public void ASeedNeverEvictsAHandMadeVariantSharingItsId()
    {
        // The seed generator writes "seed-default", but nothing stops a hand-made variant holding
        // that id -- an imported file, or an older save. Replacing on recency alone would let a
        // regenerable seed delete the user's layout during a migration they never asked for, which
        // is the worst place for it: no prompt, no undo, and it looks like the file was corrupt.
        var (_, manager, _, _) = Make();
        var current = CurrentKey("New York", "Newark");

        manager.SaveVariant(LegacySizedKey(current, "s161x101"), "seed-default", "Actually mine",
            ManualLayoutOrigin.Manual, OneExtension(), null, setAsDefault: true, setAsSelected: false);
        manager.SaveVariant(LegacySizedKey(current, "s241x101"), "seed-default", "Generated Seed",
            ManualLayoutOrigin.AutoSeed, OneExtension(), null, setAsDefault: true, setAsSelected: false);

        var variants = manager.ListVariants(current);

        Assert.Single(variants);
        Assert.Equal("Actually mine", variants[0].DisplayName);
        Assert.Equal(ManualLayoutOrigin.Manual, variants[0].Origin);
    }

    [Fact]
    public void ASelectionFollowsItsVariantThroughARename()
    {
        // The selected variant is renamed by the merge, because the group it lands in already has a
        // "manual-default". Carrying the old id across unchanged would leave the selection naming
        // the *other* group's layout: a different layout, silently selected, with nothing on screen
        // to say the choice was reassigned.
        var (_, manager, _, _) = Make();
        var current = CurrentKey("New York", "Newark");

        manager.SaveVariant(LegacySizedKey(current, "s161x101"), "manual-default", "First saved",
            ManualLayoutOrigin.Manual, OneExtension(), null, setAsDefault: true, setAsSelected: false);
        manager.SaveVariant(LegacySizedKey(current, "s241x101"), "manual-default", "The one I picked",
            ManualLayoutOrigin.Manual, OneExtension(), null, setAsDefault: true, setAsSelected: true);

        var selectedId = manager.GetSelectedVariantId(current);
        var variants = manager.ListVariants(current);

        Assert.Equal(2, variants.Count);
        var picked = variants.Single(v => v.DisplayName.StartsWith("The one I picked"));
        Assert.Equal(picked.VariantId, selectedId);
        Assert.True(picked.IsSelected);
        // Loading honours the selection, and the layout it returns is the renamed one -- the
        // suffix records which window size it came from, so the choice stays recognisable.
        Assert.StartsWith("The one I picked (from ", manager.LoadLayout(current)!.DisplayName);
    }

    [Fact]
    public void ASelectionSurvivingUnrenamed_IsStillCarriedOver()
    {
        // The plain case: nothing collides, so the id is unchanged and the selection just moves.
        var (_, manager, _, _) = Make();
        var current = CurrentKey("New York", "Newark");

        manager.SaveVariant(LegacySizedKey(current, "s161x101"), "manual-default", "First saved",
            ManualLayoutOrigin.Manual, OneExtension(), null, setAsDefault: true, setAsSelected: false);
        manager.SaveVariant(LegacySizedKey(current, "s241x101"), "v2", "The one I picked",
            ManualLayoutOrigin.Manual, OneExtension(), null, setAsDefault: false, setAsSelected: true);

        Assert.Equal("v2", manager.GetSelectedVariantId(current));
        Assert.Equal("The one I picked", manager.LoadLayout(current)!.DisplayName);
    }

    [Fact]
    public void TwoGroupsWithTheirOwnDefault_LeaveOneDefaultBehind()
    {
        // Different ids, so nothing collides and both variants survive -- but each sized group had
        // its own default, and "the default" belongs to the group rather than to a variant. Two of
        // them in one origin makes which layout loads depend on enumeration order.
        var (_, manager, _, _) = Make();
        var current = CurrentKey("New York", "Newark");

        manager.SaveVariant(LegacySizedKey(current, "s161x101"), "laptop", "On the laptop",
            ManualLayoutOrigin.Manual, OneExtension(), null, setAsDefault: true, setAsSelected: false);
        manager.SaveVariant(LegacySizedKey(current, "s241x101"), "desk", "On the big screen",
            ManualLayoutOrigin.Manual, OneExtension(), null, setAsDefault: true, setAsSelected: false);

        var variants = manager.ListVariants(current);

        Assert.Equal(2, variants.Count);
        Assert.Single(variants.Where(v => v.IsDefault));
    }

    [Fact]
    public void ASeedDefaultAndAHandMadeDefault_BothSurvive()
    {
        // The counterpart: one default per origin is the existing rule, not one per group, so
        // clearing across origins would break the fallback the loader depends on.
        var (_, manager, _, _) = Make();
        var current = CurrentKey("New York", "Newark");

        manager.SaveVariant(LegacySizedKey(current, "s161x101"), "seed-default", "Generated Seed",
            ManualLayoutOrigin.AutoSeed, OneExtension(), null, setAsDefault: true, setAsSelected: false);
        manager.SaveVariant(LegacySizedKey(current, "s241x101"), "mine", "Mine",
            ManualLayoutOrigin.Manual, OneExtension(), null, setAsDefault: true, setAsSelected: false);

        var variants = manager.ListVariants(current);

        Assert.Equal(2, variants.Count);
        Assert.True(variants.Single(v => v.Origin == ManualLayoutOrigin.AutoSeed).IsDefault);
        Assert.True(variants.Single(v => v.Origin == ManualLayoutOrigin.Manual).IsDefault);
    }

    [Fact]
    public void ASelectedSeedLosingToAHandMadeVariant_LeavesTheGroupUnselected()
    {
        // The seed the user selected is gone. The variant that survives holds the same id, so
        // recording it as the selection would resolve -- and would claim they chose a hand-made
        // layout they have never seen. Unselected hands it to origin priority, which is honest
        // about the choice no longer existing.
        var (_, manager, _, _) = Make();
        var current = CurrentKey("New York", "Newark");

        manager.SaveVariant(LegacySizedKey(current, "s161x101"), "seed-default", "Actually mine",
            ManualLayoutOrigin.Manual, OneExtension(), null, setAsDefault: true, setAsSelected: false);
        manager.SaveVariant(LegacySizedKey(current, "s241x101"), "seed-default", "Generated Seed",
            ManualLayoutOrigin.AutoSeed, OneExtension(), null, setAsDefault: true, setAsSelected: true);

        Assert.Null(manager.GetSelectedVariantId(current));
        Assert.Equal("Actually mine", manager.ListVariants(current).Single().DisplayName);
    }

    [Fact]
    public void ASelectedSeedLosingToANewerSeed_StillPointsAtASeed()
    {
        // Unlike the case above, the survivor is the same generated layout for the same cluster
        // under the same id, so the selection still names what was chosen.
        var (_, manager, _, _) = Make();
        var current = CurrentKey("New York", "Newark");

        manager.SaveVariant(LegacySizedKey(current, "s161x101"), "seed-default", "Generated Seed",
            ManualLayoutOrigin.AutoSeed, OneExtension(), null, setAsDefault: true, setAsSelected: false);
        manager.SaveVariant(LegacySizedKey(current, "s241x101"), "seed-default", "Generated Seed",
            ManualLayoutOrigin.AutoSeed, OneExtension(), null, setAsDefault: true, setAsSelected: true);

        Assert.Equal("seed-default", manager.GetSelectedVariantId(current));
        Assert.Equal(ManualLayoutOrigin.AutoSeed, manager.ListVariants(current).Single().Origin);
    }

    [Fact]
    public void TheMigration_IsIdempotent()
    {
        var (_, manager, _, _) = Make();
        var current = CurrentKey("New York", "Newark");
        manager.SaveVariant(LegacySizedKey(current, "s161x101"), "manual-default", "Mine",
            ManualLayoutOrigin.Manual, OneExtension(), null, setAsDefault: true, setAsSelected: false);

        manager.ListVariants(current);
        manager.ListVariants(current);
        var variants = manager.ListVariants(current);

        Assert.Single(variants);
        Assert.Equal("Mine", variants[0].DisplayName);
    }

    [Fact]
    public void TheCentreComponent_IsNotMistakenForASize()
    {
        // The centre is numeric too. A loose match on "starts with s" or "contains x" would strip
        // coordinates out of the key and merge clusters that are genuinely at different places.
        var (_, manager, _, _) = Make();
        var here = CurrentKey("New York", "Newark");

        manager.SaveVariant(here, "manual-default", "Mine", ManualLayoutOrigin.Manual,
            OneExtension(), null, setAsDefault: true, setAsSelected: false);

        Assert.Single(manager.ListVariants(here));
        Assert.Contains("_c", here);
    }
}
