using System;
using System.Collections.Generic;
using System.Windows;
using InteractiveWorldMap.Models;
using InteractiveWorldMap.Services;
using Xunit;

namespace InteractiveWorldMap.Tests;

public class ManualLayoutAssignmentEnricherTests
{
    [Fact]
    public void GetAssignments_WithNoPlans_ReturnsEmpty()
    {
        var provider = new FakePlanningResultProvider();
        var enricher = new ManualLayoutAssignmentEnricher();

        var result = enricher.GetAssignments(
            new[] { CreateExtension("LocA") },
            provider);

        Assert.Empty(result);
    }

    [Fact]
    public void GetAssignments_WithCachedPlan_ReturnsAssignment()
    {
        var provider = new FakePlanningResultProvider();
        provider.Add("LocA", CreateResult("pair-a", "head-a.png"));
        var enricher = new ManualLayoutAssignmentEnricher();

        var result = enricher.GetAssignments(
            new[] { CreateExtension("LocA") },
            provider);

        var assignment = Assert.Single(result);
        Assert.Equal("LocA", assignment.Key);
        Assert.Equal("pair-a", assignment.Value.PairId);
        Assert.Equal("head-a.png", assignment.Value.HeadSourcePath);
    }

    [Fact]
    public void GetAssignments_WithMultiplePlans_ReturnsAll()
    {
        var provider = new FakePlanningResultProvider();
        provider.Add("LocA", CreateResult("pair-a", "head-a.png"));
        provider.Add("LocB", CreateResult("pair-b", "head-b.png"));
        var enricher = new ManualLayoutAssignmentEnricher();

        var result = enricher.GetAssignments(
            new[] { CreateExtension("LocA"), CreateExtension("LocB") },
            provider);

        Assert.Equal(2, result.Count);
        Assert.Equal(("pair-a", "head-a.png"), result["LocA"]);
        Assert.Equal(("pair-b", "head-b.png"), result["LocB"]);
    }

    [Fact]
    public void GetAssignments_WithNoPlan_OmitsLocation()
    {
        var provider = new FakePlanningResultProvider();
        provider.Add("LocA", CreateResult("pair-a", "head-a.png"));
        var enricher = new ManualLayoutAssignmentEnricher();

        var result = enricher.GetAssignments(
            new[] { CreateExtension("LocA"), CreateExtension("LocB") },
            provider);

        Assert.Single(result);
        Assert.True(result.ContainsKey("LocA"));
        Assert.False(result.ContainsKey("LocB"));
    }

    [Fact]
    public void GetAssignments_WithNullPlan_OmitsLocation()
    {
        var provider = new FakePlanningResultProvider();
        provider.Add("LocA", null);
        var enricher = new ManualLayoutAssignmentEnricher();

        var result = enricher.GetAssignments(
            new[] { CreateExtension("LocA") },
            provider);

        Assert.Empty(result);
    }

    [Fact]
    public void GetAssignments_KeysByLocationName()
    {
        var provider = new FakePlanningResultProvider();
        provider.Add("Display Name", CreateResult("pair-a", "head-a.png"));
        provider.Add("id-only", CreateResult("pair-b", "head-b.png"));
        var enricher = new ManualLayoutAssignmentEnricher();

        var result = enricher.GetAssignments(
            new[] { CreateExtension("Display Name", id: "id-only") },
            provider);

        var assignment = Assert.Single(result);
        Assert.Equal("Display Name", assignment.Key);
        Assert.Equal("pair-a", assignment.Value.PairId);
    }

    private static RadialExtension CreateExtension(string name, string? id = null)
        => new()
        {
            Location = new Location
            {
                Id = id ?? name,
                Name = name
            },
            OriginalPosition = new Point(10, 20),
            ExtendedPosition = new Point(30, 40)
        };

    private static CompositePinPlanningResult CreateResult(string pairId, string headSourcePath)
        => new()
        {
            RenderPlan = new CompositePinRenderPlan
            {
                PairId = pairId,
                HeadSourcePath = headSourcePath
            }
        };

    private sealed class FakePlanningResultProvider : ICompositePinPlanningResultProvider
    {
        private readonly Dictionary<string, CompositePinPlanningResult?> _results =
            new(StringComparer.Ordinal);

        public void Add(string locationId, CompositePinPlanningResult? result)
        {
            _results[locationId] = result;
        }

        public bool TryGetLastResult(string locationId, out CompositePinPlanningResult? result)
        {
            return _results.TryGetValue(locationId, out result);
        }
    }
}
