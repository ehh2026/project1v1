using System;
using System.Collections.Generic;
using System.Linq;
using InteractiveWorldMap.Models;
using InteractiveWorldMap.Services;
using InteractiveWorldMap.Tests.TestHelpers;
using InteractiveWorldMap.Tools.ManualLayoutSeedGenerator;
using Xunit;

namespace InteractiveWorldMap.Tests;

public class ManualLayoutSeedGeneratorTests
{
    [Fact]
    public void Generate_ReplacesOnlyAutoSeedVariant_AndPreservesManualImportedAndSelection()
    {
        var locations = CreateDenseLocations(5);
        var config = CreateConfig();
        var existing = new ManualLayoutCollection();
        var preservedKey = "existing-group";
        existing.LayoutGroups[preservedKey] = new ManualLayoutGroup
        {
            GroupKey = preservedKey,
            Variants = new List<ManualLayout>
            {
                new ManualLayout
                {
                    GroupKey = preservedKey,
                    Key = preservedKey,
                    VariantId = "manual-default",
                    DisplayName = "Curated",
                    Origin = ManualLayoutOrigin.Manual,
                    IsDefault = true,
                    Markers = new List<ManualLayoutMarker>
                    {
                        new ManualLayoutMarker("Preserved", new System.Windows.Point(1, 1), new System.Windows.Point(2, 2), 0, 1)
                    }
                },
                new ManualLayout
                {
                    GroupKey = preservedKey,
                    Key = preservedKey,
                    VariantId = "seed-default",
                    DisplayName = "Old Seed",
                    Origin = ManualLayoutOrigin.AutoSeed,
                    IsDefault = true,
                    Markers = new List<ManualLayoutMarker>()
                },
                new ManualLayout
                {
                    GroupKey = preservedKey,
                    Key = preservedKey,
                    VariantId = "imported-a",
                    DisplayName = "Imported",
                    Origin = ManualLayoutOrigin.Imported,
                    IsDefault = false,
                    Markers = new List<ManualLayoutMarker>()
                }
            }
        };
        existing.SelectedVariants[preservedKey] = "manual-default";

        var generator = new ManualLayoutSeedGenerator(new MockLogger());
        var result = generator.Generate(new ManualLayoutSeedGeneratorOptions
        {
            Config = config,
            Locations = locations,
            MapImageWidth = 4000,
            MapImageHeight = 2000,
            ExistingCollection = existing,
            ViewportSizes = new[] { new SeedViewportSize(1920, 1080) }
        });

        Assert.True(result.LayoutGroups.ContainsKey(preservedKey));
        var preservedGroup = result.LayoutGroups[preservedKey];
        Assert.Contains(preservedGroup.Variants, v => v.VariantId == "manual-default" && v.Origin == ManualLayoutOrigin.Manual);
        Assert.Contains(preservedGroup.Variants, v => v.VariantId == "imported-a" && v.Origin == ManualLayoutOrigin.Imported);
        Assert.DoesNotContain(preservedGroup.Variants, v => v.VariantId == "seed-default" && v.Origin == ManualLayoutOrigin.AutoSeed);
        Assert.Equal("manual-default", result.SelectedVariants[preservedKey]);

        var generatedSeed = Assert.Single(result.LayoutGroups.Values
            .Where(group => group.GroupKey != preservedKey)
            .SelectMany(group => group.Variants)
            .Where(variant => variant.Origin == ManualLayoutOrigin.AutoSeed));
        Assert.Equal("seed-default", generatedSeed.VariantId);
        Assert.Equal("ManualLayoutSeedGenerator/1.0", generatedSeed.GeneratorVersion);
        Assert.Equal(locations.Count, generatedSeed.Markers.Count);
        Assert.All(generatedSeed.Markers, marker =>
        {
            Assert.True(marker.SourceExtendedX.HasValue);
            Assert.True(marker.SourceExtendedY.HasValue);
        });
    }

    [Fact]
    public void GeneratedSeeds_LoadWithRuntimeLayoutKey()
    {
        var locations = CreateDenseLocations(3);
        var config = CreateConfig();
        var centerX = locations.Average(l => l.PixelX);
        var centerY = locations.Average(l => l.PixelY);
        var viewport = ViewportState.CreateZoomedView(
            centerX,
            centerY,
            config.ZoomScale,
            4000,
            2000,
            1920,
            1080);
        var expectedKey = LayoutKeyGenerator.GenerateKey(locations, viewport, config.RadialExtension);

        var generator = new ManualLayoutSeedGenerator(new MockLogger());
        var collection = generator.Generate(new ManualLayoutSeedGeneratorOptions
        {
            Config = config,
            Locations = locations,
            MapImageWidth = 4000,
            MapImageHeight = 2000,
            ViewportSizes = new[] { new SeedViewportSize(1920, 1080) }
        });

        Assert.True(collection.LayoutGroups.TryGetValue(expectedKey, out var group));
        var seed = Assert.Single(group!.Variants.Where(v => v.Origin == ManualLayoutOrigin.AutoSeed));
        Assert.Equal(locations.Count, seed.Markers.Count);
    }

    [Fact]
    public void Generate_WhenGeneratedGroupAlreadyHasManualVariant_PreservesManualVariant()
    {
        var locations = CreateDenseLocations(3);
        var config = CreateConfig();
        var centerX = locations.Average(l => l.PixelX);
        var centerY = locations.Average(l => l.PixelY);
        var viewport = ViewportState.CreateZoomedView(centerX, centerY, config.ZoomScale, 4000, 2000, 1920, 1080);
        var key = LayoutKeyGenerator.GenerateKey(locations, viewport, config.RadialExtension);
        var existing = new ManualLayoutCollection();
        existing.LayoutGroups[key] = new ManualLayoutGroup
        {
            GroupKey = key,
            Variants = new List<ManualLayout>
            {
                new ManualLayout
                {
                    GroupKey = key,
                    Key = key,
                    VariantId = "manual-default",
                    DisplayName = "Hand placed",
                    Origin = ManualLayoutOrigin.Manual,
                    IsDefault = true,
                    Markers = new List<ManualLayoutMarker>
                    {
                        new ManualLayoutMarker("Location 0", new System.Windows.Point(1, 1), new System.Windows.Point(2, 2), 0, 1)
                    }
                },
                new ManualLayout
                {
                    GroupKey = key,
                    Key = key,
                    VariantId = "seed-default",
                    DisplayName = "Old Seed",
                    Origin = ManualLayoutOrigin.AutoSeed,
                    IsDefault = true,
                    Markers = new List<ManualLayoutMarker>()
                }
            }
        };
        existing.SelectedVariants[key] = "manual-default";

        var generator = new ManualLayoutSeedGenerator(new MockLogger());
        var result = generator.Generate(new ManualLayoutSeedGeneratorOptions
        {
            Config = config,
            Locations = locations,
            MapImageWidth = 4000,
            MapImageHeight = 2000,
            ExistingCollection = existing,
            ViewportSizes = new[] { new SeedViewportSize(1920, 1080) }
        });

        var variants = result.LayoutGroups[key].Variants;
        Assert.Contains(variants, v => v.VariantId == "manual-default" && v.Origin == ManualLayoutOrigin.Manual);
        Assert.Contains(variants, v => v.VariantId == "seed-default" && v.Origin == ManualLayoutOrigin.AutoSeed);
        Assert.Equal("manual-default", result.SelectedVariants[key]);
    }

    private static VisualConfig CreateConfig()
    {
        return new VisualConfig
        {
            ClusterDistanceThreshold = 200,
            ZoomScale = 55,
            RadialExtension = new RadialExtensionConfig
            {
                Enabled = true,
                MinLocationsForExtension = 3,
                ProximityThresholdPixels = 160,
                ExtensionLineLength = 120,
                AngleNudgeThreshold = 12,
                AngleNudgeAmount = 6,
                MinimumLineLength = 20,
                ZoomThresholdForExtensions = 2
            }
        };
    }

    private static List<Location> CreateDenseLocations(int count)
    {
        return Enumerable.Range(0, count)
            .Select(i => new Location
            {
                Id = $"loc-{i}",
                Name = $"Location {i}",
                PixelX = 1000 + (i % 3) * 30,
                PixelY = 1000 + (i / 3) * 30
            })
            .ToList();
    }
}
