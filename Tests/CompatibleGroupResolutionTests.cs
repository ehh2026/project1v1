using System.Linq;
using InteractiveWorldMap.Models;
using InteractiveWorldMap.Services;
using Xunit;

using static InteractiveWorldMap.Tests.TestHelpers.LayoutEditorTestFixtures;

namespace InteractiveWorldMap.Tests;

/// <summary>
/// Trap 3, from docs/reference/manual-layout-scoping.md.
///
/// A cluster key carries the viewport size, and <c>AreKeysCompatible</c> deliberately ignores it —
/// so moving the window, or the app to another monitor, still finds your layout. Loading used that
/// fallback; listing, selecting and deleting matched the key exactly. At any window size outside the
/// seeded ones the map therefore showed a saved layout the dropdown reported as absent, and every
/// group-scoped action would have operated on a different group than the one on screen.
/// </summary>
public class CompatibleGroupResolutionTests
{
    // Generated rather than written out. Hand-written keys assume a format the generator owns, and
    // a 16-character hash beside an assignment is also indistinguishable from a leaked credential to
    // a secret scanner -- see .gitleaksignore for the previous round of that.
    private static readonly RadialExtensionConfig KeyConfig = new();

    private static string KeyFor(string[] locationNames, double containerWidth = 1920)
    {
        var locations = locationNames.Select(n => new Location { Id = n, Name = n }).ToList();
        var viewport = ViewportState.CreateZoomedView(4000, 3000, 55, 8198, 5542, containerWidth, 1080);
        return LayoutKeyGenerator.GenerateKey(locations, viewport, KeyConfig);
    }

    private static readonly string[] Cluster = { "New York", "Newark" };
    private static readonly string[] OtherCluster = { "Hong Kong", "Kowloon" };

    // Same locations and zoom, different window size: compatible by design.
    private static readonly string SeededKey = KeyFor(Cluster);
    private static readonly string OtherSizeKey = KeyFor(Cluster, containerWidth: 1600);

    // A different cluster entirely, and never compatible.
    private static readonly string OtherClusterKey = KeyFor(OtherCluster);

    [Fact]
    public void ListVariants_AtAnotherWindowSize_FindsTheLayoutThatIsActuallyApplied()
    {
        var (_, manager, _, _) = Make();
        manager.SaveVariant(SeededKey, "seed-default", "Generated Seed", ManualLayoutOrigin.AutoSeed,
            OneExtension(), null, setAsDefault: true, setAsSelected: false);

        // The map finds it...
        Assert.NotNull(manager.LoadLayout(OtherSizeKey));

        // ...so the dropdown has to as well. Reporting "none saved" over a layout the user can see
        // invites them to redo work that already exists.
        var variants = manager.ListVariants(OtherSizeKey);
        Assert.Single(variants);
        Assert.Equal(SeededKey, variants[0].GroupKey);
    }

    [Fact]
    public void LoadVariant_AtAnotherWindowSize_CanSelectWhatWasListed()
    {
        // Listing without this would trade one inconsistency for another: variants shown in the
        // picker that selecting them silently fails to load.
        var (_, manager, _, _) = Make();
        manager.SaveVariant(SeededKey, "manual-default", "Mine", ManualLayoutOrigin.Manual,
            OneExtension(), null, setAsDefault: true, setAsSelected: true);

        var listed = manager.ListVariants(OtherSizeKey).Single();
        var loaded = manager.LoadVariant(OtherSizeKey, listed.VariantId);

        Assert.NotNull(loaded);
        Assert.Equal("Mine", loaded!.DisplayName);
    }

    [Fact]
    public void DeleteVariant_AtAnotherWindowSize_RemovesTheOneThatWasListed()
    {
        // The delete confirmation names variants from the listing. If deletion matched the exact
        // key while listing fell back, it would report success having removed nothing.
        var (_, manager, _, _) = Make();
        manager.SaveVariant(SeededKey, "manual-default", "Keep", ManualLayoutOrigin.Manual,
            OneExtension(), null, setAsDefault: true, setAsSelected: false);
        manager.SaveVariant(SeededKey, "v2", "Doomed", ManualLayoutOrigin.Manual,
            OneExtension(), null, setAsDefault: false, setAsSelected: false);

        Assert.True(manager.DeleteVariant(OtherSizeKey, "v2"));

        var remaining = manager.ListVariants(SeededKey);
        Assert.Single(remaining);
        Assert.Equal("Keep", remaining[0].DisplayName);
    }

    [Fact]
    public void DeleteLayout_AtAnotherWindowSize_RemovesTheGroupThatWasListed()
    {
        var (_, manager, _, _) = Make();
        manager.SaveVariant(SeededKey, "manual-default", "Mine", ManualLayoutOrigin.Manual,
            OneExtension(), null, setAsDefault: true, setAsSelected: false);

        Assert.True(manager.DeleteLayout(OtherSizeKey));
        Assert.Empty(manager.ListVariants(SeededKey));
    }

    [Fact]
    public void SelectingAVariant_PersistsAgainstTheGroupItCameFrom()
    {
        // Every read of SelectedVariants goes through the resolved key. Writing the choice under
        // the session's own key would store it where nothing reads it back, so the selection would
        // appear to take and then be gone on the next load -- silently, since the write "succeeded".
        var (_, manager, _, _) = Make();
        manager.SaveVariant(SeededKey, "manual-default", "First", ManualLayoutOrigin.Manual,
            OneExtension(), null, setAsDefault: true, setAsSelected: true);
        manager.SaveVariant(SeededKey, "v2", "Second", ManualLayoutOrigin.Manual,
            OneExtension(), null, setAsDefault: false, setAsSelected: false);

        Assert.True(manager.SetSelectedVariantId(OtherSizeKey, "v2"));

        Assert.Equal("v2", manager.GetSelectedVariantId(OtherSizeKey));
        Assert.Equal("v2", manager.GetSelectedVariantId(SeededKey));
        Assert.Equal("Second", manager.LoadLayout(OtherSizeKey)!.DisplayName);
    }

    [Fact]
    public void SettingTheDefault_ReachesTheGroupThatWasListed()
    {
        var (_, manager, _, _) = Make();
        manager.SaveVariant(SeededKey, "manual-default", "First", ManualLayoutOrigin.Manual,
            OneExtension(), null, setAsDefault: true, setAsSelected: false);
        manager.SaveVariant(SeededKey, "v2", "Second", ManualLayoutOrigin.Manual,
            OneExtension(), null, setAsDefault: false, setAsSelected: false);

        Assert.True(manager.SetDefaultVariant(OtherSizeKey, "v2"));

        var listed = manager.ListVariants(SeededKey);
        Assert.True(listed.Single(v => v.VariantId == "v2").IsDefault);
    }

    [Fact]
    public void ADifferentCluster_IsStillADifferentLayout()
    {
        // The fallback must stay as narrow as AreKeysCompatible. Falling back across location sets
        // would show one cluster's layout under another's name — worse than the bug being fixed.
        var (_, manager, _, _) = Make();
        manager.SaveVariant(SeededKey, "manual-default", "Mine", ManualLayoutOrigin.Manual,
            OneExtension(), null, setAsDefault: true, setAsSelected: false);

        Assert.Empty(manager.ListVariants(OtherClusterKey));
        Assert.Null(manager.LoadVariant(OtherClusterKey, "manual-default"));
        Assert.False(manager.DeleteLayout(OtherClusterKey));
    }

    [Fact]
    public void AnExactMatch_IsStillPreferredOverACompatibleOne()
    {
        var (_, manager, _, _) = Make();
        manager.SaveVariant(SeededKey, "manual-default", "Seeded size", ManualLayoutOrigin.Manual,
            OneExtension(), null, setAsDefault: true, setAsSelected: false);
        manager.SaveVariant(OtherSizeKey, "manual-default", "This size", ManualLayoutOrigin.Manual,
            OneExtension(), null, setAsDefault: true, setAsSelected: false);

        var variants = manager.ListVariants(OtherSizeKey);
        Assert.Single(variants);
        Assert.Equal("This size", variants[0].DisplayName);
    }
}
