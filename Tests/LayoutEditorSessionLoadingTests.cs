using InteractiveWorldMap.Models;
using Xunit;

using static InteractiveWorldMap.Tests.TestHelpers.LayoutEditorTestFixtures;

namespace InteractiveWorldMap.Tests;

/// <summary>
/// Loading a layout and adopting its variant identity are separate decisions. They used to be one:
/// <c>TryLoad</c> adopted the identity of whatever key it was handed, so navigation's probe loads
/// silently rewrote what the editor was pointed at. These pin both halves of the split, and the
/// save behaviour that depends on it.
/// </summary>
public class LayoutEditorSessionLoadingTests
{
    [Fact]
    public void TryLoad_DoesNotAdoptVariantIdentity()
    {
        var (ctrl, manager, _, _) = Make();
        manager.SaveVariant(
            "key-elsewhere", "other-variant", "Other", ManualLayoutOrigin.Manual,
            OneExtension(), null, setAsDefault: true, setAsSelected: true);

        ctrl.BeginEditSession(SessionFor("key-here"));
        ctrl.TrySave(OneExtension());
        var identityBefore = ctrl.ActiveVariantId;

        var loaded = ctrl.TryLoad("key-elsewhere");

        Assert.NotNull(loaded);
        Assert.Equal(identityBefore, ctrl.ActiveVariantId);
        Assert.NotEqual("other-variant", ctrl.ActiveVariantId);
    }

    [Fact]
    public void LoadForEditSession_AdoptsTheLoadedVariantIdentity()
    {
        var (ctrl, manager, _, _) = Make();
        manager.SaveVariant(
            "key-session", "named-variant", "Named", ManualLayoutOrigin.Manual,
            OneExtension(), null, setAsDefault: true, setAsSelected: true);

        ctrl.BeginEditSession(SessionFor("key-session"));
        var loaded = ctrl.LoadForEditSession();

        Assert.NotNull(loaded);
        Assert.Equal("named-variant", ctrl.ActiveVariantId);
        Assert.Equal("Named", ctrl.ActiveVariantDisplayName);
        Assert.Equal(ManualLayoutOrigin.Manual, ctrl.ActiveVariantOrigin);
    }

    [Fact]
    public void SaveAfterLoadForEditSession_UpdatesTheLoadedVariantNotTheDefault()
    {
        // The reason editor entry loads unconditionally. Identity used to arrive as a side effect
        // of navigation; without establishing it here, a save would create or overwrite
        // "manual-default" and leave the variant the user was actually editing untouched.
        var (ctrl, manager, _, _) = Make();
        manager.SaveVariant(
            "key-session", "named-variant", "Named", ManualLayoutOrigin.Manual,
            OneExtension(), null, setAsDefault: true, setAsSelected: true);

        ctrl.BeginEditSession(SessionFor("key-session"));
        ctrl.LoadForEditSession();
        ctrl.TrySave(OneExtension());

        var variants = manager.ListVariants("key-session");
        Assert.Single(variants);
        Assert.Equal("named-variant", variants[0].VariantId);
        Assert.DoesNotContain(variants, v => v.VariantId == "manual-default");
    }

    [Fact]
    public void LoadForEditSession_WithNoSession_ReturnsNullAndAdoptsNothing()
    {
        var (ctrl, manager, _, _) = Make();
        manager.SaveVariant(
            "key-orphan", "v", "V", ManualLayoutOrigin.Manual,
            OneExtension(), null, setAsDefault: true, setAsSelected: true);

        var loaded = ctrl.LoadForEditSession();

        Assert.Null(loaded);
        Assert.Null(ctrl.ActiveVariantId);
    }

    [Fact]
    public void AdoptVariantIdentity_Null_LeavesIdentityUnchanged()
    {
        var (ctrl, _, _, _) = Make();
        ctrl.BeginEditSession(SessionFor("key-keep"));
        ctrl.TrySave(OneExtension());
        var before = ctrl.ActiveVariantId;

        ctrl.AdoptVariantIdentity(null);

        Assert.Equal(before, ctrl.ActiveVariantId);
    }
}
