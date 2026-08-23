using System.Collections.Generic;
using System.Linq;
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
        ExtensionLineLength = 50.0,
        MinimumLineLength = 13.0
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
        var vp = MakeZoomedViewport();
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
        var vp = MakeZoomedViewport();
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
        var vp = MakeZoomedViewport();
        var cfg = MakeConfig();

        var key = LayoutKeyGenerator.GenerateKey(locations, vp, cfg);
        var parts = key.Split('_');

        // hash _ z{zoom} _ c{cx}_{cy} _ m{min} _ p{prox} _ l{len} _ n{minlen}
        // No viewport size since Phase 6.9: it was in the key but never in the compatibility
        // check, so it split one cluster into a group per window size and found nothing extra.
        // Eight, not seven: the centre contributes two of them (c{cx} and {cy}). Asserting seven
        // while reading parts[7] below would throw an index error instead of naming the problem.
        Assert.True(parts.Length >= 8, $"Expected at least 8 underscore-separated parts, got: {key}");
        Assert.Matches(@"^[0-9a-f]{16}$", parts[0]);          // 16-char hex hash
        Assert.StartsWith("z", parts[1]);                     // zoom
        Assert.StartsWith("c", parts[2]);                     // center x
        Assert.Equal("m3", parts[4]);                     // MinLocationsForExtension
        Assert.Equal("p10.0", parts[5]);                     // ProximityThresholdPixels
        Assert.Equal("l50.0", parts[6]);                     // ExtensionLineLength
        Assert.Equal("n13.0", parts[7]);                     // MinimumLineLength
    }

    [Fact]
    public void GenerateKey_DifferentLocations_ProduceDifferentHashes()
    {
        var vp = MakeZoomedViewport();
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
        var vp = MakeZoomedViewport();
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
    public void AreKeysCompatible_LegacySizedKeySameHash_ReturnsTrue()
    {
        // Since Phase 6.9 new keys carry no s{w}x{h}, but keys written before it do, and they have
        // to keep resolving -- the migration only runs when the file is next read or written.
        var locations = new List<Location>
        {
            new() { Name = "Alpha", PixelX = 100, PixelY = 200 },
            new() { Name = "Beta",  PixelX = 110, PixelY = 210 }
        };
        var current = LayoutKeyGenerator.GenerateKey(locations, MakeZoomedViewport(), MakeConfig());

        var parts = current.Split('_').ToList();
        parts.Insert(4, "s161x101");
        var legacy = string.Join("_", parts);

        Assert.NotEqual(current, legacy);
        Assert.True(LayoutKeyGenerator.AreKeysCompatible(current, legacy));
    }

    [Fact]
    public void GenerateFullMapGroupKey_IsSizeIndependent()
    {
        // The full-map layout is keyed by identity alone; canvas size must not appear,
        // otherwise a resize would orphan the saved layout (Phase 5c).
        Assert.Equal("fullmap", LayoutKeyGenerator.GenerateFullMapGroupKey());
    }

    [Fact]
    public void AreKeysCompatible_FullMapDifferentSizes_ReturnsTrue()
    {
        // Any two full-map keys (including legacy sized forms) describe the same "whole map"
        // layout, so they are compatible — the saved layout survives a window resize.
        Assert.True(LayoutKeyGenerator.AreKeysCompatible(
            "fullmap_s1920x1080",
            "fullmap_s1440x900"));
        Assert.True(LayoutKeyGenerator.AreKeysCompatible(
            "fullmap",
            "fullmap_s1440x900"));
    }

    [Fact]
    public void AreKeysCompatible_FullMapVsCluster_ReturnsFalse()
    {
        var clusterKey = LayoutKeyGenerator.GenerateKey(
            new List<Location>
            {
                new() { Name = "Alpha", PixelX = 100, PixelY = 200 },
                new() { Name = "Beta",  PixelX = 110, PixelY = 210 }
            },
            MakeZoomedViewport(),
            MakeConfig());

        Assert.False(LayoutKeyGenerator.AreKeysCompatible("fullmap", clusterKey));
        Assert.False(LayoutKeyGenerator.AreKeysCompatible(clusterKey, "fullmap_s1920x1080"));
    }

    // -------------------------------------------------------------------------
    // DeriveEditSessionKey — the editor must key off the view actually on screen
    // -------------------------------------------------------------------------

    [Fact]
    public void DeriveEditSessionKey_NoZoomedCluster_ReturnsFullMapKey()
    {
        var key = LayoutKeyGenerator.DeriveEditSessionKey(null, MakeZoomedViewport(), MakeConfig());

        Assert.Equal(LayoutKeyGenerator.GenerateFullMapGroupKey(), key);
    }

    [Fact]
    public void DeriveEditSessionKey_EmptyClusterLocations_ReturnsFullMapKey()
    {
        var key = LayoutKeyGenerator.DeriveEditSessionKey(
            new List<Location>(), MakeZoomedViewport(), MakeConfig());

        Assert.Equal(LayoutKeyGenerator.GenerateFullMapGroupKey(), key);
    }

    [Fact]
    public void DeriveEditSessionKey_WithZoomedCluster_ReturnsClusterKeyNotFullMap()
    {
        var locations = new List<Location>
        {
            new() { Name = "Alpha", PixelX = 100, PixelY = 200 },
            new() { Name = "Beta",  PixelX = 110, PixelY = 210 }
        };
        var vp = MakeZoomedViewport();
        var cfg = MakeConfig();

        var key = LayoutKeyGenerator.DeriveEditSessionKey(locations, vp, cfg);

        Assert.NotEqual(LayoutKeyGenerator.GenerateFullMapGroupKey(), key);
        Assert.Equal(LayoutKeyGenerator.GenerateKey(locations, vp, cfg), key);
    }

    [Fact]
    public void DeriveEditSessionKey_DifferentClusters_ProduceDifferentKeys()
    {
        var vp = MakeZoomedViewport();
        var cfg = MakeConfig();

        var newYork = LayoutKeyGenerator.DeriveEditSessionKey(
            new List<Location> { new() { Name = "New York", PixelX = 100, PixelY = 200 } }, vp, cfg);
        var hongKong = LayoutKeyGenerator.DeriveEditSessionKey(
            new List<Location> { new() { Name = "Hong Kong", PixelX = 900, PixelY = 400 } }, vp, cfg);

        Assert.NotEqual(newYork, hongKong);
        Assert.False(LayoutKeyGenerator.AreKeysCompatible(newYork, hongKong));
    }
    // ─── Phase 6.6: the properties the scoping doc claims ────────────────────

    /// <summary>Two locations, enough to form a cluster.</summary>
    private static List<Location> ClusterLocations() => new()
    {
        new() { Name = "Alpha", PixelX = 100, PixelY = 200 },
        new() { Name = "Beta",  PixelX = 110, PixelY = 210 }
    };

    [Theory]
    [InlineData("MinLocationsForExtension")]
    [InlineData("ProximityThresholdPixels")]
    [InlineData("ExtensionLineLength")]
    [InlineData("MinimumLineLength")]
    public void ChangingARadialExtensionSetting_RekeysClusterLayoutsWithoutOrphaningThem(string setting)
    {
        // Each of the four is part of every cluster key, so changing one in visual-config.json
        // gives every cluster a different key. The plan and the scoping doc both said that orphaned
        // the saved layouts. It does not, and never did: AreKeysCompatible only ever compared the
        // location hash and the zoom, so the old group is still compatible with the new key and
        // still resolves. Measured through ManualLayoutManager, not inferred -- see
        // ConfigChangeDoesNotHideSavedLayoutsTests.
        //
        // What is left is real but milder, and is what configure.ps1 now says: the key moves, so
        // every save afterwards lands in a new group, and the pins are placed by settings the
        // layout was not drawn against.
        var locations = ClusterLocations();
        var viewport = MakeZoomedViewport();

        var before = MakeConfig();
        var after = MakeConfig();
        switch (setting)
        {
            case "MinLocationsForExtension": after.MinLocationsForExtension += 1; break;
            case "ProximityThresholdPixels": after.ProximityThresholdPixels += 1; break;
            case "ExtensionLineLength": after.ExtensionLineLength += 1; break;
            case "MinimumLineLength": after.MinimumLineLength += 1; break;
        }

        var keyBefore = LayoutKeyGenerator.GenerateKey(locations, viewport, before);
        var keyAfter = LayoutKeyGenerator.GenerateKey(locations, viewport, after);

        Assert.NotEqual(keyBefore, keyAfter);
        Assert.True(
            LayoutKeyGenerator.AreKeysCompatible(keyBefore, keyAfter),
            $"Changing {setting} made the keys incompatible. Saved cluster layouts would now be " +
            "unreachable, which is the failure the docs used to describe and the warning in " +
            "configure.ps1 is written on the assumption does not happen.");
    }

    [Fact]
    public void ChangingARadialExtensionSetting_LeavesTheFullMapLayoutAlone()
    {
        // The counterpart, and why the warning says "cluster layouts" rather than "layouts".
        var before = MakeConfig();
        var after = MakeConfig();
        after.ExtensionLineLength += 1;

        Assert.Equal(
            LayoutKeyGenerator.DeriveEditSessionKey(null, MakeZoomedViewport(), before),
            LayoutKeyGenerator.DeriveEditSessionKey(null, MakeZoomedViewport(), after));
    }

    [Fact]
    public void TwoDifferentClusters_AreNotJustDifferentKeysButIncompatibleOnes()
    {
        // Distinct strings are not enough: lookup falls back through AreKeysCompatible, so if two
        // clusters were compatible one would show the other's layout under its own name.
        var newYork = new List<Location>
        {
            new() { Name = "New York", PixelX = 100, PixelY = 200 },
            new() { Name = "Newark",   PixelX = 110, PixelY = 210 }
        };
        var hongKong = new List<Location>
        {
            new() { Name = "Hong Kong", PixelX = 100, PixelY = 200 },
            new() { Name = "Kowloon",   PixelX = 110, PixelY = 210 }
        };

        var a = LayoutKeyGenerator.GenerateKey(newYork, MakeZoomedViewport(), MakeConfig());
        var b = LayoutKeyGenerator.GenerateKey(hongKong, MakeZoomedViewport(), MakeConfig());

        Assert.NotEqual(a, b);
        Assert.False(LayoutKeyGenerator.AreKeysCompatible(a, b));
    }

    [Fact]
    public void AFullMapKeyAndAClusterKey_NeverCollideOrResolveToEachOther()
    {
        // The guarantee the scoping doc leads with. A cluster layout applied to the whole map, or
        // the reverse, would put every pin somewhere arbitrary.
        var cluster = LayoutKeyGenerator.DeriveEditSessionKey(
            ClusterLocations(), MakeZoomedViewport(), MakeConfig());
        var fullMap = LayoutKeyGenerator.DeriveEditSessionKey(
            null, MakeZoomedViewport(), MakeConfig());

        Assert.NotEqual(cluster, fullMap);
        Assert.False(LayoutKeyGenerator.AreKeysCompatible(cluster, fullMap));
        Assert.False(LayoutKeyGenerator.AreKeysCompatible(fullMap, cluster));
    }
}
