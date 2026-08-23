using System.Collections.Generic;
using InteractiveWorldMap.Models;
using InteractiveWorldMap.Services;
using Xunit;

using static InteractiveWorldMap.Tests.TestHelpers.LayoutEditorTestFixtures;

namespace InteractiveWorldMap.Tests;

/// <summary>
/// "Trap 1" said that editing a <see cref="RadialExtensionConfig"/> value in visual-config.json
/// orphaned every saved cluster layout — that they stayed on disk but stopped resolving, so the
/// symptom was layouts appearing to vanish.
///
/// It does not happen, and on inspection never did: those values are in the cluster key, but
/// <c>AreKeysCompatible</c> has only ever compared the location hash and the zoom, so a group saved
/// under the old settings is still compatible with the new key. These tests measure that through
/// <see cref="ManualLayoutManager"/> rather than reasoning about the key format, because reasoning
/// about the key format is exactly how the wrong claim survived three documents.
/// </summary>
public class ConfigChangeDoesNotHideSavedLayoutsTests
{
    private static List<Location> Cluster() => new()
    {
        new() { Name = "Alpha", PixelX = 100, PixelY = 200 },
        new() { Name = "Beta",  PixelX = 110, PixelY = 210 }
    };

    private static ViewportState Viewport() =>
        ViewportState.CreateZoomedView(4000, 3000, 55, 8198, 5542, 1920, 1080);

    private static (string OldKey, string NewKey) KeysAcrossAConfigChange()
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
    public void ALayoutSavedBeforeTheChange_StillLoadsAfterIt()
    {
        var (_, manager, _, _) = Make();
        var (oldKey, newKey) = KeysAcrossAConfigChange();

        manager.SaveVariant(oldKey, "manual-default", "Mine", ManualLayoutOrigin.Manual,
            OneExtension(), null, setAsDefault: true, setAsSelected: true);

        var loaded = manager.LoadLayout(newKey);

        Assert.NotNull(loaded);
        Assert.Equal("Mine", loaded!.DisplayName);
    }

    [Fact]
    public void ALayoutSavedBeforeTheChange_IsStillListedAfterIt()
    {
        // The half that used to be broken, for a different reason: ListVariants matched the key
        // exactly until Phase 6.8, so a config change did make the dropdown go empty while the map
        // went on showing the layout. That was Trap 3 wearing Trap 1's clothes.
        var (_, manager, _, _) = Make();
        var (oldKey, newKey) = KeysAcrossAConfigChange();

        manager.SaveVariant(oldKey, "manual-default", "Mine", ManualLayoutOrigin.Manual,
            OneExtension(), null, setAsDefault: true, setAsSelected: true);

        var listed = manager.ListVariants(newKey);

        Assert.Single(listed);
        Assert.Equal("Mine", listed[0].DisplayName);
    }

    [Fact]
    public void SavingAfterTheChange_LandsInANewGroup()
    {
        // The part that is real, and what configure.ps1 warns about instead. Saving keys exactly,
        // so a layout saved after the change is stored beside the old one rather than replacing it,
        // and the file accumulates a group per settings combination anyone has run at.
        var (_, manager, _, _) = Make();
        var (oldKey, newKey) = KeysAcrossAConfigChange();

        manager.SaveVariant(oldKey, "manual-default", "Before", ManualLayoutOrigin.Manual,
            OneExtension(), null, setAsDefault: true, setAsSelected: false);
        manager.SaveVariant(newKey, "manual-default", "After", ManualLayoutOrigin.Manual,
            OneExtension(), null, setAsDefault: true, setAsSelected: false);

        // Each key finds its own group exactly; the fallback only applies when there is no exact
        // match, so the two do not merge.
        Assert.Equal("Before", manager.LoadLayout(oldKey)!.DisplayName);
        Assert.Equal("After", manager.LoadLayout(newKey)!.DisplayName);
    }
}
