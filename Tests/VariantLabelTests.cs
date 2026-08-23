using System;
using InteractiveWorldMap.Models;
using Xunit;

using static InteractiveWorldMap.Tests.TestHelpers.SourceGuard;

namespace InteractiveWorldMap.Tests;

/// <summary>
/// Every generated seed on disk is stored as "Generated Seed", so the picker showed one identical
/// row per view and looked like the same layout everywhere. The layouts were always distinct; only
/// the labels were not.
/// </summary>
public class VariantLabelTests
{
    private static ManualLayoutSummary Summary(
        string name, int markers, bool isDefault = false, ManualLayoutOrigin origin = ManualLayoutOrigin.AutoSeed) =>
        new("k", "seed-default", name, origin, DateTime.UtcNow, isDefault, IsSelected: false, markers);

    [Fact]
    public void Label_AddsWhatDistinguishesOneVariantFromAnother()
    {
        Assert.Equal("Generated Seed - 12 pins", Summary("Generated Seed", 12).Label);
    }

    [Fact]
    public void Label_MarksTheDefault()
    {
        Assert.Equal("Mine - 3 pins, default", Summary("Mine", 3, isDefault: true).Label);
    }

    [Fact]
    public void Label_SingularForOnePin()
    {
        Assert.Equal("Mine - 1 pin", Summary("Mine", 1).Label);
    }

    [Fact]
    public void Label_NeverComesOutBlank()
    {
        // manual-layouts.json is hand-editable, so a name can be missing or whitespace. A blank row
        // in the picker cannot be selected with any confidence about what it is.
        Assert.Equal("(unnamed) - 4 pins", Summary("   ", 4).Label);
    }

    [Fact]
    public void ThePicker_ShowsTheLabelRatherThanTheRawName()
    {
        Assert.Contains("{Binding Label}", Read("MainWindow.xaml"));
        Assert.DoesNotContain("<TextBlock Text=\"{Binding DisplayName}\"/>", Read("MainWindow.xaml"));
    }

    [Fact]
    public void NewSeeds_AreNamedAfterTheirCluster()
    {
        // Covers the file going forward. Existing files keep their stored names and are handled by
        // the label above, since what is already on disk cannot be assumed to have been chosen.
        var source = Read("Tools/ManualLayoutSeedGenerator/ManualLayoutSeedGenerator.cs");

        Assert.Contains("DisplayName = DescribeSeed(cluster.Locations)", source);
        Assert.DoesNotContain("DisplayName = \"Generated Seed\"", source);
    }
}
