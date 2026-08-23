using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using InteractiveWorldMap.Models;
using InteractiveWorldMap.Services;
using InteractiveWorldMap.Utilities;

namespace InteractiveWorldMap.Tools.ManualLayoutSeedGenerator;

public sealed record SeedViewportSize(double Width, double Height);

public sealed class ManualLayoutSeedGeneratorOptions
{
    public VisualConfig Config { get; set; } = new VisualConfig();
    public IReadOnlyList<Location> Locations { get; set; } = Array.Empty<Location>();
    public double MapImageWidth { get; set; }
    public double MapImageHeight { get; set; }
    public ManualLayoutCollection? ExistingCollection { get; set; }
    public IReadOnlyList<SeedViewportSize> ViewportSizes { get; set; } = ManualLayoutSeedGenerator.DefaultViewportSizes;
}

public sealed class ManualLayoutSeedGenerator
{
    public const string GeneratorVersion = "ManualLayoutSeedGenerator/1.0";

    public static readonly IReadOnlyList<SeedViewportSize> DefaultViewportSizes = new[]
    {
        new SeedViewportSize(1920, 1080),
        new SeedViewportSize(1440, 900),
        new SeedViewportSize(1600, 1200),
        new SeedViewportSize(3440, 1440)
    };

    private readonly ILogger _logger;

    public ManualLayoutSeedGenerator(ILogger logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public ManualLayoutCollection Generate(ManualLayoutSeedGeneratorOptions options)
    {
        if (options == null) throw new ArgumentNullException(nameof(options));
        if (options.MapImageWidth <= 0) throw new ArgumentOutOfRangeException(nameof(options.MapImageWidth));
        if (options.MapImageHeight <= 0) throw new ArgumentOutOfRangeException(nameof(options.MapImageHeight));

        var output = CloneWithoutAutoSeeds(options.ExistingCollection ?? new ManualLayoutCollection());
        var locations = options.Locations?.ToList() ?? new List<Location>();
        if (locations.Count == 0)
        {
            UpdateLegacyLayoutIndex(output);
            return output;
        }

        var config = options.Config ?? new VisualConfig();
        var clusterer = new LocationClusterer { DistanceThreshold = config.ClusterDistanceThreshold };
        var clusters = clusterer.ClusterLocations(locations);
        var extensionCalculator = new RadialExtensionCalculator(config.RadialExtension);

        foreach (var viewportSize in options.ViewportSizes.Count == 0 ? DefaultViewportSizes : options.ViewportSizes)
        {
            foreach (var cluster in clusters.Where(c => !c.IsSingleLocation))
            {
                var viewport = ViewportState.CreateZoomedView(
                    cluster.CenterPoint.X,
                    cluster.CenterPoint.Y,
                    config.ZoomScale,
                    options.MapImageWidth,
                    options.MapImageHeight,
                    viewportSize.Width,
                    viewportSize.Height);

                var markerSourcePositions = cluster.Locations.ToDictionary(
                    location => location,
                    location => new Point(location.PixelX, location.PixelY));
                var denseGroups = extensionCalculator.DetectDenseGroups(markerSourcePositions);
                var allExtensions = new List<RadialExtension>();
                var markerScreenPositions = cluster.Locations.ToDictionary(
                    location => location,
                    location => viewport.SourceToScreen(location.PixelX, location.PixelY, viewportSize.Width, viewportSize.Height));

                var groupId = 0;
                foreach (var denseGroup in denseGroups)
                {
                    var extensions = extensionCalculator.CalculateRadialExtensions(
                        denseGroup,
                        markerScreenPositions,
                        viewportSize.Width,
                        viewportSize.Height);

                    foreach (var extension in extensions)
                    {
                        extension.GroupId = groupId;
                        var sourcePoint = viewport.ScreenToSource(
                            extension.ExtendedPosition.X,
                            extension.ExtendedPosition.Y,
                            viewportSize.Width,
                            viewportSize.Height);
                        extension.SourceExtendedX = sourcePoint.X;
                        extension.SourceExtendedY = sourcePoint.Y;
                    }

                    if (extensions.Count > 0 && extensionCalculator.ValidateNoCrossings(extensions))
                        allExtensions.AddRange(extensions);

                    groupId++;
                }

                if (allExtensions.Count == 0)
                    continue;

                var key = LayoutKeyGenerator.GenerateKey(cluster.Locations, viewport, config.RadialExtension);
                if (!output.LayoutGroups.TryGetValue(key, out var group))
                {
                    group = new ManualLayoutGroup { GroupKey = key };
                    output.LayoutGroups[key] = group;
                }

                group.GroupKey = key;
                group.Variants.RemoveAll(v => v.Origin == ManualLayoutOrigin.AutoSeed);
                group.Variants.Add(new ManualLayout(key, allExtensions.Select(ManualLayoutMarker.FromRadialExtension).ToList())
                {
                    GroupKey = key,
                    Key = key,
                    VariantId = "seed-default",
                    // Named after the cluster rather than "Generated Seed". This file gets read by
                    // hand often enough -- not least by anyone wondering where layouts they never
                    // made came from -- that sixteen identical names is a poor answer.
                    DisplayName = DescribeSeed(cluster.Locations),
                    Origin = ManualLayoutOrigin.AutoSeed,
                    IsDefault = true,
                    GeneratorVersion = GeneratorVersion,
                    LocationCount = allExtensions.Count
                });

                _logger.LogInfo($"[ManualLayoutSeedGenerator] Generated {allExtensions.Count} seed markers for {key}");
            }
        }

        UpdateLegacyLayoutIndex(output);
        return output;
    }

    private static ManualLayoutCollection CloneWithoutAutoSeeds(ManualLayoutCollection source)
    {
        var clone = new ManualLayoutCollection
        {
            SelectedVariants = new Dictionary<string, string>(
                source.SelectedVariants ?? new Dictionary<string, string>(),
                StringComparer.Ordinal)
        };

        foreach (var entry in source.LayoutGroups ?? new Dictionary<string, ManualLayoutGroup>())
        {
            var variants = (entry.Value.Variants ?? new List<ManualLayout>())
                .Where(v => v.Origin != ManualLayoutOrigin.AutoSeed)
                .Select(CloneLayout)
                .ToList();

            if (variants.Count > 0)
            {
                clone.LayoutGroups[entry.Key] = new ManualLayoutGroup
                {
                    GroupKey = string.IsNullOrWhiteSpace(entry.Value.GroupKey) ? entry.Key : entry.Value.GroupKey,
                    Variants = variants
                };
            }
            else
            {
                clone.SelectedVariants.Remove(entry.Key);
            }
        }

        return clone;
    }

    private static ManualLayout CloneLayout(ManualLayout source)
    {
        return new ManualLayout
        {
            Key = source.Key,
            GroupKey = source.GroupKey,
            VariantId = source.VariantId,
            DisplayName = source.DisplayName,
            Origin = source.Origin,
            IsDefault = source.IsDefault,
            Timestamp = source.Timestamp,
            CreatedUtc = source.CreatedUtc,
            UpdatedUtc = source.UpdatedUtc,
            BasedOnKey = source.BasedOnKey,
            BasedOnVariantId = source.BasedOnVariantId,
            GeneratorVersion = source.GeneratorVersion,
            LocationCount = source.LocationCount,
            Markers = (source.Markers ?? new List<ManualLayoutMarker>()).Select(CloneMarker).ToList()
        };
    }

    private static ManualLayoutMarker CloneMarker(ManualLayoutMarker source)
    {
        return new ManualLayoutMarker
        {
            LocationName = source.LocationName,
            OriginalPosition = source.OriginalPosition,
            ExtendedPosition = source.ExtendedPosition,
            Angle = source.Angle,
            LineLength = source.LineLength,
            SourceExtendedX = source.SourceExtendedX,
            SourceExtendedY = source.SourceExtendedY,
            PairId = source.PairId,
            HeadSourcePath = source.HeadSourcePath
        };
    }

    private static void UpdateLegacyLayoutIndex(ManualLayoutCollection collection)
    {
        collection.Layouts = collection.LayoutGroups
            .Where(group => group.Value.Variants.Count > 0)
            .ToDictionary(
                group => group.Key,
                group => group.Value.Variants
                    .OrderByDescending(GetVariantPriority)
                    .ThenByDescending(v => v.UpdatedUtc)
                    .ThenByDescending(v => v.Timestamp)
                    .First(),
                StringComparer.Ordinal);
    }

    /// <summary>
    /// A seed name that says which cluster it belongs to. Three locations then a count, for the
    /// same reason the edit panel truncates: this is read in a narrow dropdown, not a report.
    /// </summary>
    private static string DescribeSeed(List<Location> locations)
    {
        if (locations == null || locations.Count == 0) return "Generated Seed";

        var named = locations
            .Select(l => string.IsNullOrWhiteSpace(l?.Name) ? "(unnamed)" : l!.Name.Trim())
            .OrderBy(n => n, StringComparer.Ordinal)
            .Take(3)
            .ToList();

        var remaining = locations.Count - named.Count;
        var suffix = remaining > 0 ? $", +{remaining} more" : "";
        return "Seed: " + string.Join(", ", named) + suffix;
    }

    private static int GetVariantPriority(ManualLayout variant)
    {
        if (variant.Origin == ManualLayoutOrigin.Manual && variant.IsDefault) return 6;
        if (variant.Origin == ManualLayoutOrigin.AutoSeed && variant.IsDefault) return 5;
        if (variant.Origin == ManualLayoutOrigin.Manual) return 4;
        if (variant.Origin == ManualLayoutOrigin.AutoSeed) return 3;
        if (variant.IsDefault) return 2;
        return 1;

    }
}
