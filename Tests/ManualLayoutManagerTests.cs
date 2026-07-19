using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Windows;
using InteractiveWorldMap.Models;
using InteractiveWorldMap.Services;
using InteractiveWorldMap.Tests.TestHelpers;
using Xunit;

namespace InteractiveWorldMap.Tests;

public class ManualLayoutManagerTests
{
    [Fact]
    public void LoadLayout_WhenFileIsCorrupt_DoesNotThrow_AndBacksUpBadFile()
    {
        // A corrupt or schema-incompatible layout file must never crash the app: load returns null
        // (no layout) instead of throwing, and the unreadable file is preserved as ".corrupt".
        var tempDir = Path.Combine(Path.GetTempPath(), "iwm-layout-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        var layoutPath = Path.Combine(tempDir, "manual-layouts.json");

        try
        {
            File.WriteAllText(layoutPath, "{ this is not valid json ]]]");

            var manager = new ManualLayoutManager(layoutPath, new MockLogger());

            var loaded = manager.LoadLayout("anykey_z1.00_c0_0_s100x100_m3_p10.0_l50.0_n13.0");

            Assert.Null(loaded);
            Assert.True(File.Exists(layoutPath + ".corrupt"), "Unreadable layout file should be backed up.");
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }

    [Fact]
    public void LoadLayout_WhenExactKeyMissingButCompatibleLayoutExists_ReturnsCompatibleLayout()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "iwm-layout-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        var layoutPath = Path.Combine(tempDir, "manual-layouts.json");

        try
        {
            var compatibleKey = "clusterhash_z55.00_c2458.10_2571.57_s179x101_m3_p10.0_l50.0_n13.0";
            var requestedKey = "clusterhash_z55.05_c2458.10_2571.57_s179x101_m3_p10.0_l50.0_n13.0";
            var savedLayout = new ManualLayout(
                compatibleKey,
                new List<ManualLayoutMarker>
                {
                    new("Dr. Test", new Point(10, 10), new Point(25, 30), 45.0, 25.0)
                });

            var payload = new ManualLayoutCollection
            {
                Layouts = new Dictionary<string, ManualLayout>
                {
                    [compatibleKey] = savedLayout
                }
            };

            File.WriteAllText(
                layoutPath,
                JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true }));

            var manager = new ManualLayoutManager(layoutPath, new MockLogger());

            var loadedLayout = manager.LoadLayout(requestedKey);

            Assert.NotNull(loadedLayout);
            Assert.Equal(compatibleKey, loadedLayout!.Key);
            Assert.Single(loadedLayout.Markers);
            Assert.Equal("Dr. Test", loadedLayout.Markers[0].LocationName);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }

    [Fact]
    public void LoadLayout_WhenManualAndAutoSeedVariantsExist_PrefersManualVariant()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "iwm-layout-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        var layoutPath = Path.Combine(tempDir, "manual-layouts.json");

        try
        {
            var groupKey = "clusterhash_z55.00_c2458.10_2571.57_s179x101_m3_p10.0_l50.0_n13.0";
            var json = @"
{
  ""LayoutGroups"": {
    ""clusterhash_z55.00_c2458.10_2571.57_s179x101_m3_p10.0_l50.0_n13.0"": {
      ""GroupKey"": ""clusterhash_z55.00_c2458.10_2571.57_s179x101_m3_p10.0_l50.0_n13.0"",
      ""Variants"": [
        {
          ""Key"": ""clusterhash_z55.00_c2458.10_2571.57_s179x101_m3_p10.0_l50.0_n13.0"",
          ""VariantId"": ""seed-default"",
          ""DisplayName"": ""Generated Seed"",
          ""Origin"": ""AutoSeed"",
          ""IsDefault"": true,
          ""Timestamp"": ""2026-06-05T00:00:00Z"",
          ""CreatedUtc"": ""2026-06-05T00:00:00Z"",
          ""UpdatedUtc"": ""2026-06-05T00:00:00Z"",
          ""LocationCount"": 1,
          ""Markers"": [
            {
              ""LocationName"": ""Dr. Test"",
              ""OriginalPosition"": { ""X"": 10.0, ""Y"": 10.0 },
              ""ExtendedPosition"": { ""X"": 20.0, ""Y"": 20.0 },
              ""Angle"": 15.0,
              ""LineLength"": 14.0
            }
          ]
        },
        {
          ""Key"": ""clusterhash_z55.00_c2458.10_2571.57_s179x101_m3_p10.0_l50.0_n13.0"",
          ""VariantId"": ""manual-default"",
          ""DisplayName"": ""Manual Default"",
          ""Origin"": ""Manual"",
          ""IsDefault"": true,
          ""Timestamp"": ""2026-06-06T00:00:00Z"",
          ""CreatedUtc"": ""2026-06-06T00:00:00Z"",
          ""UpdatedUtc"": ""2026-06-06T00:00:00Z"",
          ""LocationCount"": 1,
          ""Markers"": [
            {
              ""LocationName"": ""Dr. Test"",
              ""OriginalPosition"": { ""X"": 10.0, ""Y"": 10.0 },
              ""ExtendedPosition"": { ""X"": 30.0, ""Y"": 35.0 },
              ""Angle"": 45.0,
              ""LineLength"": 32.0
            }
          ]
        }
      ]
    }
  }
}";
            File.WriteAllText(layoutPath, json);

            var manager = new ManualLayoutManager(layoutPath, new MockLogger());

            var loadedLayout = manager.LoadLayout(groupKey);

            Assert.NotNull(loadedLayout);
            Assert.Equal(30.0, loadedLayout!.Markers[0].ExtendedPosition.X);
            Assert.Equal(35.0, loadedLayout.Markers[0].ExtendedPosition.Y);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }

    [Fact]
    public void SaveLayout_WhenAutoSeedVariantExists_PreservesAutoSeedAndAddsManualVariant()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "iwm-layout-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        var layoutPath = Path.Combine(tempDir, "manual-layouts.json");

        try
        {
            var groupKey = "clusterhash_z55.00_c2458.10_2571.57_s179x101_m3_p10.0_l50.0_n13.0";
            var json = @"
{
  ""LayoutGroups"": {
    ""clusterhash_z55.00_c2458.10_2571.57_s179x101_m3_p10.0_l50.0_n13.0"": {
      ""GroupKey"": ""clusterhash_z55.00_c2458.10_2571.57_s179x101_m3_p10.0_l50.0_n13.0"",
      ""Variants"": [
        {
          ""Key"": ""clusterhash_z55.00_c2458.10_2571.57_s179x101_m3_p10.0_l50.0_n13.0"",
          ""VariantId"": ""seed-default"",
          ""DisplayName"": ""Generated Seed"",
          ""Origin"": ""AutoSeed"",
          ""IsDefault"": true,
          ""Timestamp"": ""2026-06-05T00:00:00Z"",
          ""CreatedUtc"": ""2026-06-05T00:00:00Z"",
          ""UpdatedUtc"": ""2026-06-05T00:00:00Z"",
          ""LocationCount"": 1,
          ""Markers"": [
            {
              ""LocationName"": ""Dr. Seed"",
              ""OriginalPosition"": { ""X"": 10.0, ""Y"": 10.0 },
              ""ExtendedPosition"": { ""X"": 20.0, ""Y"": 20.0 },
              ""Angle"": 15.0,
              ""LineLength"": 14.0
            }
          ]
        }
      ]
    }
  }
}";
            File.WriteAllText(layoutPath, json);

            var manager = new ManualLayoutManager(layoutPath, new MockLogger());
            var extensions = new List<RadialExtension>
            {
                new()
                {
                    Location = new Location { Id = "loc-1", Name = "Dr. Seed", PixelX = 0, PixelY = 0 },
                    OriginalPosition = new Point(10, 10),
                    ExtendedPosition = new Point(40, 50),
                    Angle = 33.0,
                    GroupId = 0
                }
            };

            var saved = manager.SaveLayout(groupKey, extensions);

            Assert.True(saved);

            using var document = JsonDocument.Parse(File.ReadAllText(layoutPath));
            var variants = document.RootElement
                .GetProperty("LayoutGroups")
                .GetProperty(groupKey)
                .GetProperty("Variants");

            Assert.Equal(2, variants.GetArrayLength());

            var origins = new List<string>();
            foreach (var variant in variants.EnumerateArray())
            {
                origins.Add(variant.GetProperty("Origin").GetString()!);
            }

            Assert.Contains("AutoSeed", origins);
            Assert.Contains("Manual", origins);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }

    // ─── Phase 2: PairId / HeadSourcePath round-trip ─────────────────────────

    [Fact]
    public void SaveLayout_WithAssignments_PersistsPairIdAndHeadSourcePath()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "iwm-layout-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        var layoutPath = Path.Combine(tempDir, "manual-layouts.json");

        try
        {
            var manager = new ManualLayoutManager(layoutPath, new MockLogger());
            var extensions = new List<RadialExtension>
            {
                new RadialExtension
                {
                    Location         = new Location { Id = "a", Name = "Alpha" },
                    OriginalPosition = new Point(0, 0),
                    ExtendedPosition = new Point(30, 30),
                    Angle            = 45.0,
                    GroupId          = 0
                }
            };
            var assignments = new Dictionary<string, (string PairId, string HeadSourcePath)>
            {
                ["Alpha"] = ("pin_07", "Pins_v2/parts/pin_03_head.png")
            };

            manager.SaveLayout("key-assign", extensions, assignments);
            var loaded = manager.LoadLayout("key-assign");

            Assert.NotNull(loaded);
            var marker = Assert.Single(loaded!.Markers);
            Assert.Equal("pin_07",                     marker.PairId);
            Assert.Equal("Pins_v2/parts/pin_03_head.png", marker.HeadSourcePath);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void SaveLayout_WithoutAssignments_LeavesAssignmentFieldsNull()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "iwm-layout-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        var layoutPath = Path.Combine(tempDir, "manual-layouts.json");

        try
        {
            var manager = new ManualLayoutManager(layoutPath, new MockLogger());
            var extensions = new List<RadialExtension>
            {
                new RadialExtension
                {
                    Location         = new Location { Id = "b", Name = "Beta" },
                    OriginalPosition = new Point(0, 0),
                    ExtendedPosition = new Point(20, 20),
                    Angle            = 0.0,
                    GroupId          = 0
                }
            };

            manager.SaveLayout("key-no-assign", extensions);
            var loaded = manager.LoadLayout("key-no-assign");

            Assert.NotNull(loaded);
            var marker = Assert.Single(loaded!.Markers);
            Assert.Null(marker.PairId);
            Assert.Null(marker.HeadSourcePath);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void LoadLayout_FullMapDifferentSize_LoadsCompatibleLayout()
    {
        // Phase 5c: full-map layouts are size-independent. A layout saved under a legacy sized
        // key must resolve under the size-independent "fullmap" key (and any other size), so a
        // window resize no longer orphans it.
        var tempDir = Path.Combine(Path.GetTempPath(), "iwm-layout-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        var layoutPath = Path.Combine(tempDir, "manual-layouts.json");

        try
        {
            var manager = new ManualLayoutManager(layoutPath, new MockLogger());
            var extensions = new List<RadialExtension>
            {
                new RadialExtension
                {
                    Location         = new Location { Id = "a", Name = "Alpha" },
                    OriginalPosition = new Point(0, 0),
                    ExtendedPosition = new Point(30, 30),
                    Angle            = 45.0,
                    GroupId          = 0
                }
            };

            Assert.True(manager.SaveLayout("fullmap_s1920x1080", extensions));

            var loaded = manager.LoadLayout("fullmap");

            Assert.NotNull(loaded);
            Assert.Single(loaded!.Markers);
            Assert.Equal("Alpha", loaded.Markers[0].LocationName);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void SaveLayout_PersistsSourceExtendedCoords_ForSizeIndependentReprojection()
    {
        // Phase 5c: user saves now carry source-image coords so the extended position
        // re-projects at any window size. Verify they round-trip through save/load.
        var tempDir = Path.Combine(Path.GetTempPath(), "iwm-layout-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        var layoutPath = Path.Combine(tempDir, "manual-layouts.json");

        try
        {
            var manager = new ManualLayoutManager(layoutPath, new MockLogger());
            var extensions = new List<RadialExtension>
            {
                new RadialExtension
                {
                    Location         = new Location { Id = "a", Name = "Alpha" },
                    OriginalPosition = new Point(10, 10),
                    ExtendedPosition = new Point(40, 60),
                    SourceExtendedX  = 1234.5,
                    SourceExtendedY  = 678.9,
                    Angle            = 30.0,
                    GroupId          = 0
                }
            };

            Assert.True(manager.SaveLayout("fullmap", extensions));

            var loaded = manager.LoadLayout("fullmap");

            Assert.NotNull(loaded);
            var marker = Assert.Single(loaded!.Markers);
            Assert.Equal(1234.5, marker.SourceExtendedX);
            Assert.Equal(678.9, marker.SourceExtendedY);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, recursive: true);
        }
    }
}
