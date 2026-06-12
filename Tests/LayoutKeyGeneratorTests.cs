using System.Collections.Generic;
using System.Text.RegularExpressions;
using InteractiveWorldMap.Models;
using InteractiveWorldMap.Services;
using Xunit;

namespace InteractiveWorldMap.Tests;

/// <summary>
/// Tests for LayoutKeyGenerator — verifies that key generation is deterministic,
/// locale-independent, and structurally compatible with the PowerShell seed generator.
/// </summary>
public class LayoutKeyGeneratorTests
{
    private static ViewportState MakeZoomedViewport() =>
        ViewportState.CreateZoomedView(4000, 3000, 55, 8198, 5542, 1920, 1080);

    private static RadialExtensionConfig MakeConfig() => new()
    {
        MinLocationsForExtension = 3,
        ProximityThresholdPixels = 10.0,
        ExtensionLineLength      = 50.0,
        MinimumLineLength        = 13.0
    };

    // -------------------------------------------------------------------------
    // Determinism
    // -------------------------------------------------------------------------

    [Fact]
    public void GenerateKey_SameInputs_ProducesSameKey()
    {
        var locations = new List<Location>
        {
            new() { Name = "Alpha", PixelX = 100, PixelY = 200 },
            new() { Name = "Beta",  PixelX = 110, PixelY = 210 }
        };
        var vp = MakeZoomedViewport();
        var cfg = MakeConfig();

        var key1 = LayoutKeyGenerator.GenerateKey(locations, vp, cfg);
        var key2 = LayoutKeyGenerator.GenerateKey(locations, vp, cfg);

        Assert.Equal(key1, key2);
    }

    // -------------------------------------------------------------------------
    // Sort order is ordinal (locale-independent)
    // -------------------------------------------------------------------------

    [Fact]
    public void GenerateKey_LocationOrderIndependent_ProducesSameKey()
    {
        var vp  = MakeZoomedViewport();
        var cfg = MakeConfig();

        var locationsAB = new List<Location>
        {
            new() { Name = "Alpha", PixelX = 100, PixelY = 200 },
            new() { Name = "Beta",  PixelX = 110, PixelY = 210 }
        };
        var locationsBA = new List<Location>
        {
            new() { Name = "Beta",  PixelX = 110, PixelY = 210 },
            new() { Name = "Alpha", PixelX = 100, PixelY = 200 }
        };

        Assert.Equal(
            LayoutKeyGenerator.GenerateKey(locationsAB, vp, cfg),
            LayoutKeyGenerator.GenerateKey(locationsBA, vp, cfg));
    }

    [Fact]
    public void GenerateKey_UppercaseBeforeLowercase_OrdinalSortMatchesPowerShell()
    {
        // StringComparer.Ordinal: 'A'(65) < 'a'(97), so "Alpha" < "alpha".
        // Both C# and the PS1 seed generator must agree on this order.
        var vp  = MakeZoomedViewport();
        var cfg = MakeConfig();

        var upperFirst = new List<Location>
        {
            new() { Name = "Alpha", PixelX = 100, PixelY = 200 },
            new() { Name = "alpha", PixelX = 100, PixelY = 200 }
        };
        var lowerFirst = new List<Location>
        {
            new() { Name = "alpha", PixelX = 100, PixelY = 200 },
            new() { Name = "Alpha", PixelX = 100, PixelY = 200 }
        };

        // Input order should not matter — both must hash to the same key.
        Assert.Equal(
            LayoutKeyGenerator.GenerateKey(upperFirst, vp, cfg),
            LayoutKeyGenerator.GenerateKey(lowerFirst, vp, cfg));
    }

    // -------------------------------------------------------------------------
    // Key structure
    // -------------------------------------------------------------------------

    [Fact]
    public void GenerateKey_ContainsExpectedComponents()
    {
        var locations = new List<Location>
        {
            new() { Name = "A", PixelX = 0, PixelY = 0 },
            new() { Name = "B", PixelX = 1, PixelY = 1 }
        };
        var vp  = MakeZoomedViewport();
        var cfg = MakeConfig();

        var key = LayoutKeyGenerator.GenerateKey(locations, vp, cfg);
        var parts = key.Split('_');

        // hash _ z{zoom} _ c{cx}_{cy} _ s{w}x{h} _ m{min} _ p{prox} _ l{len} _ n{minlen}
        Assert.True(parts.Length >= 8, $"Expected at least 8 underscore-separated parts, got: {key}");
        Assert.Matches(@"^[0-9a-f]{16}$", parts[0]);          // 16-char hex hash
        Assert.StartsWith("z",  parts[1]);                     // zoom
        Assert.StartsWith("c",  parts[2]);                     // center x
        Assert.Contains  ("x",  parts[4]);                     // viewport size
        Assert.Equal("m3",      parts[5]);                     // MinLocationsForExtension
        Assert.Equal("p10.0",   parts[6]);                     // ProximityThresholdPixels
        Assert.Equal("l50.0",   parts[7]);                     // ExtensionLineLength
        Assert.Equal("n13.0",   parts[8]);                     // MinimumLineLength
    }

    [Fact]
    public void GenerateKey_DifferentLocations_ProduceDifferentHashes()
    {
        var vp  = MakeZoomedViewport();
        var cfg = MakeConfig();

        var locsAB = new List<Location>
        {
            new() { Name = "Alpha", PixelX = 100, PixelY = 200 },
            new() { Name = "Beta",  PixelX = 110, PixelY = 210 }
        };
        var locsAC = new List<Location>
        {
            new() { Name = "Alpha",  PixelX = 100, PixelY = 200 },
            new() { Name = "Gamma",  PixelX = 120, PixelY = 220 }
        };

        var keyAB = LayoutKeyGenerator.GenerateKey(locsAB, vp, cfg);
        var keyAC = LayoutKeyGenerator.GenerateKey(locsAC, vp, cfg);

        // Different location sets must produce different hash segments
        Assert.NotEqual(keyAB.Split('_')[0], keyAC.Split('_')[0]);
    }

    // -------------------------------------------------------------------------
    // AreKeysCompatible
    // -------------------------------------------------------------------------

    [Fact]
    public void AreKeysCompatible_SameKey_ReturnsTrue()
    {
        var locations = new List<Location>
        {
            new() { Name = "Alpha", PixelX = 100, PixelY = 200 },
            new() { Name = "Beta",  PixelX = 110, PixelY = 210 }
        };
        var key = LayoutKeyGenerator.GenerateKey(locations, MakeZoomedViewport(), MakeConfig());

        Assert.True(LayoutKeyGenerator.AreKeysCompatible(key, key));
    }

    [Fact]
    public void AreKeysCompatible_DifferentHash_ReturnsFalse()
    {
        var vp  = MakeZoomedViewport();
        var cfg = MakeConfig();

        var k1 = LayoutKeyGenerator.GenerateKey(
            new List<Location>
            {
                new() { Name = "Alpha", PixelX = 100, PixelY = 200 },
                new() { Name = "Beta",  PixelX = 110, PixelY = 210 }
            },
            vp,
            cfg);
        var k2 = LayoutKeyGenerator.GenerateKey(
            new List<Location>
            {
                new() { Name = "Alpha", PixelX = 100, PixelY = 200 },
                new() { Name = "Gamma", PixelX = 120, PixelY = 220 }
            },
            vp,
            cfg);

        Assert.False(LayoutKeyGenerator.AreKeysCompatible(k1, k2));
    }

    [Fact]
    public void AreKeysCompatible_DifferentViewportSizeSameHash_ReturnsTrue()
    {
        // Different s{w}x{h} components should still be compatible (only hash+zoom checked)
        var locations = new List<Location>
        {
            new() { Name = "Alpha", PixelX = 100, PixelY = 200 },
            new() { Name = "Beta",  PixelX = 110, PixelY = 210 }
        };
        var k1920 = LayoutKeyGenerator.GenerateKey(locations, MakeZoomedViewport(), MakeConfig());
        var k1440 = Regex.Replace(k1920, @"s\d+x\d+", "s161x101");

        Assert.NotEqual(k1920, k1440);
        Assert.True(LayoutKeyGenerator.AreKeysCompatible(k1920, k1440));
    }

    [Fact]
    public void GenerateFullMapGroupKey_RoundsCanvasSize()
    {
        var key = LayoutKeyGenerator.GenerateFullMapGroupKey(1919.6, 1080.4);

        Assert.Equal("fullmap_s1920x1080", key);
    }

    [Fact]
    public void AreKeysCompatible_FullMapDifferentSizes_ReturnsFalse()
    {
        Assert.False(LayoutKeyGenerator.AreKeysCompatible(
            "fullmap_s1920x1080",
            "fullmap_s1440x900"));
    }
}
