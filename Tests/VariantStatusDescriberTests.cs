using InteractiveWorldMap.Models;
using InteractiveWorldMap.Utilities;
using Xunit;

namespace InteractiveWorldMap.Tests;

/// <summary>
/// An empty variant dropdown has two very different causes, and the panel used to render both as a
/// blank line. Naming one of them is an improvement only if the other is not named wrongly.
/// </summary>
public class VariantStatusDescriberTests
{
    private static ManualLayoutSummary Summary(string name) =>
        new("k", "v1", name, ManualLayoutOrigin.Manual, System.DateTime.UtcNow,
            IsDefault: false, IsSelected: true, MarkerCount: 3);

    [Fact]
    public void SelectedVariant_IsNamedWithItsOrigin()
    {
        Assert.Equal(
            "Loaded: My Layout (Manual)",
            VariantStatusDescriber.Describe(Summary("My Layout"), 1, "v1"));
    }

    [Fact]
    public void NothingListedAndNothingLoaded_SaysNothingIsSaved()
    {
        Assert.Equal(
            VariantStatusDescriber.NothingSaved,
            VariantStatusDescriber.Describe(null, 0, null));
    }

    [Fact]
    public void NothingListedButALayoutIsLoaded_DoesNotClaimNothingIsSaved()
    {
        // Trap 3: loading falls back to a compatible key, the picker matches exactly. At a window
        // size outside the seeded ones the map is showing a saved layout the dropdown cannot find.
        // "None saved for this view yet" there is not merely unhelpful -- it is a confident denial
        // of work the user is looking at, and the obvious response to it is to redo the layout.
        var text = VariantStatusDescriber.Describe(null, 0, "seed-default");

        Assert.Equal(VariantStatusDescriber.LoadedButUnlisted, text);
        Assert.NotEqual(VariantStatusDescriber.NothingSaved, text);
    }

    [Fact]
    public void EmptyActiveVariantId_CountsAsNothingLoaded()
    {
        Assert.Equal(
            VariantStatusDescriber.NothingSaved,
            VariantStatusDescriber.Describe(null, 0, ""));
    }

    [Fact]
    public void VariantsListedButNoneSelected_SaysNothing()
    {
        // Transient: the picker has items and is mid-repopulation. Any message here would flicker.
        Assert.Equal("", VariantStatusDescriber.Describe(null, 3, "v1"));
    }
}
