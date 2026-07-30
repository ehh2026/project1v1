using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using InteractiveWorldMap.Models;
using InteractiveWorldMap.Services;
using InteractiveWorldMap.Tests.TestHelpers;
using Xunit;

namespace InteractiveWorldMap.Tests;

/// <summary>
/// Phase 1 tests for the variant CRUD API and selection persistence.
/// </summary>
public class ManualLayoutVariantTests
{
    private const string GroupKey = "clusterhash_z55.00_c2458.10_2571.57_s179x101_m3_p10.0_l50.0_n13.0";

    private static ManualLayoutManager CreateManager(string layoutPath) =>
        new ManualLayoutManager(layoutPath, new MockLogger());

    private static List<RadialExtension> OneExtension(string name, double ex, double ey) => new()
    {
        new RadialExtension
        {
            Location         = new Location { Id = "l1", Name = name },
            OriginalPosition = new Point(10, 10),
            ExtendedPosition = new Point(ex, ey),
            Angle            = 45.0,
            GroupId          = 0
        }
    };

    private static string JsonWithAutoSeedVariant() => @"
{
  ""LayoutGroups"": {
    """ + GroupKey + @""": {
      ""GroupKey"": """ + GroupKey + @""",
      ""Variants"": [
        {
          ""Key"": """ + GroupKey + @""",
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
              ""LocationName"": ""Alpha"",
              ""OriginalPosition"": { ""X"": 10.0, ""Y"": 10.0 },
              ""ExtendedPosition"": { ""X"": 20.0, ""Y"": 20.0 },
              ""Angle"": 15.0,
              ""LineLength"": 14.0
            }
          ]
        }
      ]
    }
  },
  ""SelectedVariants"": {}
}";

    // ─── 1. ListVariants ─────────────────────────────────────────────────────

    [Fact]
    public void ListVariants_ReturnsAllVariantsWithCorrectFields()
    {
        var dir = Path.Combine(Path.GetTempPath(), "iwm-v-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, "ml.json");
        try
        {
            File.WriteAllText(path, JsonWithAutoSeedVariant());
            var mgr = CreateManager(path);
            mgr.SaveLayout(GroupKey, OneExtension("Alpha", 40, 50));

            var list = mgr.ListVariants(GroupKey);

            Assert.Equal(2, list.Count);
            Assert.Contains(list, s => s.VariantId == "seed-default" && s.Origin == ManualLayoutOrigin.AutoSeed);
            Assert.Contains(list, s => s.VariantId == "manual-default" && s.Origin == ManualLayoutOrigin.Manual);
        }
        finally { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
    }

    // ─── 2. Save As (second manual variant) ──────────────────────────────────

    [Fact]
    public void SaveVariant_SaveAsNewName_CreatesDistinctVariant()
    {
        var dir = Path.Combine(Path.GetTempPath(), "iwm-v-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, "ml.json");
        try
        {
            var mgr = CreateManager(path);
            mgr.SaveLayout(GroupKey, OneExtension("Alpha", 30, 30));

            bool ok = mgr.SaveVariant(GroupKey, "wider-spread", "Wider Spread",
                ManualLayoutOrigin.Manual, OneExtension("Alpha", 60, 70),
                null, setAsDefault: false, setAsSelected: false);

            Assert.True(ok);
            var list = mgr.ListVariants(GroupKey);
            Assert.Equal(2, list.Count);
            Assert.Contains(list, s => s.VariantId == "manual-default");
            Assert.Contains(list, s => s.VariantId == "wider-spread" && s.DisplayName == "Wider Spread");
        }
        finally { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
    }

    // ─── 3. Select variant B ─────────────────────────────────────────────────

    [Fact]
    public void SetSelectedVariantId_SelectVariantB_LoadLayoutReturnsVariantB()
    {
        var dir = Path.Combine(Path.GetTempPath(), "iwm-v-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, "ml.json");
        try
        {
            var mgr = CreateManager(path);
            mgr.SaveLayout(GroupKey, OneExtension("Alpha", 30, 30));
            mgr.SaveVariant(GroupKey, "variant-b", "Variant B",
                ManualLayoutOrigin.Manual, OneExtension("Alpha", 80, 90),
                null, setAsDefault: false, setAsSelected: false);

            mgr.SetSelectedVariantId(GroupKey, "variant-b");
            var loaded = mgr.LoadLayout(GroupKey);

            Assert.NotNull(loaded);
            Assert.Equal("variant-b", loaded!.VariantId);
            Assert.Equal(80.0, loaded.Markers[0].ExtendedPosition.X);
        }
        finally { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
    }

    // ─── 4. Selected variant persists across restart ──────────────────────────

    [Fact]
    public void SelectedVariant_PersistsAcrossManagerInstances()
    {
        var dir = Path.Combine(Path.GetTempPath(), "iwm-v-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, "ml.json");
        try
        {
            var mgr1 = CreateManager(path);
            mgr1.SaveLayout(GroupKey, OneExtension("Alpha", 30, 30));
            mgr1.SaveVariant(GroupKey, "variant-b", "Variant B",
                ManualLayoutOrigin.Manual, OneExtension("Alpha", 80, 90),
                null, setAsDefault: false, setAsSelected: false);
            mgr1.SetSelectedVariantId(GroupKey, "variant-b");

            // New manager instance (simulates restart)
            var mgr2 = CreateManager(path);
            var loaded = mgr2.LoadLayout(GroupKey);

            Assert.NotNull(loaded);
            Assert.Equal("variant-b", loaded!.VariantId);
        }
        finally { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
    }

    // ─── 5. Stale selected-id fallback ────────────────────────────────────────

    [Fact]
    public void LoadLayout_StaleSelectedVariantId_FallsBackToPreferred()
    {
        var dir = Path.Combine(Path.GetTempPath(), "iwm-v-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, "ml.json");
        try
        {
            // Write a file with a SelectedVariants entry pointing to a non-existent variant.
            var json = @"
{
  ""LayoutGroups"": {
    """ + GroupKey + @""": {
      ""GroupKey"": """ + GroupKey + @""",
      ""Variants"": [
        {
          ""Key"": """ + GroupKey + @""",
          ""VariantId"": ""manual-default"",
          ""DisplayName"": ""Manual Layout"",
          ""Origin"": ""Manual"",
          ""IsDefault"": true,
          ""Timestamp"": ""2026-06-07T00:00:00Z"",
          ""CreatedUtc"": ""2026-06-07T00:00:00Z"",
          ""UpdatedUtc"": ""2026-06-07T00:00:00Z"",
          ""LocationCount"": 1,
          ""Markers"": [
            {
              ""LocationName"": ""Alpha"",
              ""OriginalPosition"": { ""X"": 10.0, ""Y"": 10.0 },
              ""ExtendedPosition"": { ""X"": 30.0, ""Y"": 35.0 }
            }
          ]
        }
      ]
    }
  },
  ""SelectedVariants"": {
    """ + GroupKey + @""": ""ghost-variant""
  }
}";
            File.WriteAllText(path, json);
            var mgr = CreateManager(path);

            var loaded = mgr.LoadLayout(GroupKey);

            Assert.NotNull(loaded);
            // Falls back to the only existing variant (manual-default).
            Assert.Equal("manual-default", loaded!.VariantId);
        }
        finally { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
    }

    // ─── 6. Delete selected variant ───────────────────────────────────────────

    [Fact]
    public void DeleteVariant_DeletesSelectedVariant_SelectionMovesToNextPreferred()
    {
        var dir = Path.Combine(Path.GetTempPath(), "iwm-v-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, "ml.json");
        try
        {
            // Set up: AutoSeed + manual-default + variant-b; select variant-b
            File.WriteAllText(path, JsonWithAutoSeedVariant());
            var mgr = CreateManager(path);
            mgr.SaveLayout(GroupKey, OneExtension("Alpha", 30, 30));
            mgr.SaveVariant(GroupKey, "variant-b", "Variant B",
                ManualLayoutOrigin.Manual, OneExtension("Alpha", 80, 90),
                null, setAsDefault: false, setAsSelected: true);

            bool deleted = mgr.DeleteVariant(GroupKey, "variant-b");
            Assert.True(deleted);

            // Selection should fall back to the next preferred Manual variant.
            var loaded = mgr.LoadLayout(GroupKey);
            Assert.NotNull(loaded);
            Assert.NotEqual("variant-b", loaded!.VariantId);
        }
        finally { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
    }

    // ─── 7. Reject last-variant delete ────────────────────────────────────────

    [Fact]
    public void DeleteVariant_LastVariantInGroup_ReturnsFalse()
    {
        var dir = Path.Combine(Path.GetTempPath(), "iwm-v-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, "ml.json");
        try
        {
            var mgr = CreateManager(path);
            mgr.SaveLayout(GroupKey, OneExtension("Alpha", 30, 30));

            // Group now has exactly one variant.
            bool deleted = mgr.DeleteVariant(GroupKey, "manual-default");
            Assert.False(deleted);

            // The variant is still present.
            Assert.Single(mgr.ListVariants(GroupKey));
        }
        finally { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
    }

    // ─── 8. AutoSeed regen preserves manual variants ─────────────────────────

    [Fact]
    public void SaveVariant_AutoSeedDoesNotOverwriteManualVariantWithSameId()
    {
        var dir = Path.Combine(Path.GetTempPath(), "iwm-v-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, "ml.json");
        try
        {
            var mgr = CreateManager(path);
            // Save a Manual variant called "seed-default" (unusual but valid).
            mgr.SaveVariant(GroupKey, "seed-default", "My Seed Override",
                ManualLayoutOrigin.Manual, OneExtension("Alpha", 55, 66),
                null, setAsDefault: true, setAsSelected: true);

            // Attempt to overwrite it with an AutoSeed variant of the same id.
            bool rejected = mgr.SaveVariant(GroupKey, "seed-default", "Generated Seed",
                ManualLayoutOrigin.AutoSeed, OneExtension("Alpha", 20, 20),
                null, setAsDefault: true, setAsSelected: false);

            Assert.False(rejected);

            // Manual variant unchanged.
            var loaded = mgr.LoadLayout(GroupKey);
            Assert.Equal(ManualLayoutOrigin.Manual, loaded!.Origin);
            Assert.Equal(55.0, loaded.Markers[0].ExtendedPosition.X);
        }
        finally { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
    }

    // ─── 9. Assignment fields round-trip across variant save/load ─────────────

    [Fact]
    public void SaveVariant_WithAssignments_AssignmentFieldsRoundTrip()
    {
        var dir = Path.Combine(Path.GetTempPath(), "iwm-v-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, "ml.json");
        try
        {
            var mgr = CreateManager(path);
            var assignments = new Dictionary<string, (string PairId, string HeadSourcePath)>
            {
                ["Alpha"] = ("pin_07", "Pins_v2/parts/pin_03_head.png")
            };
            mgr.SaveVariant(GroupKey, "v-assign", "Assign Variant",
                ManualLayoutOrigin.Manual, OneExtension("Alpha", 30, 40),
                assignments, setAsDefault: true, setAsSelected: true);

            var loaded = mgr.LoadVariant(GroupKey, "v-assign");
            Assert.NotNull(loaded);
            Assert.Equal("pin_07", loaded!.Markers[0].PairId);
            Assert.Equal("Pins_v2/parts/pin_03_head.png", loaded.Markers[0].HeadSourcePath);
        }
        finally { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
    }

    // ─── 10. Cap enforced at 10 Manual variants ────────────────────────────────

    [Fact]
    public void SaveVariant_ExceedsManualCap_ReturnsFalse()
    {
        var dir = Path.Combine(Path.GetTempPath(), "iwm-v-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, "ml.json");
        try
        {
            var mgr = CreateManager(path);
            // Save 10 distinct manual variants.
            for (int i = 1; i <= 10; i++)
                mgr.SaveVariant(GroupKey, $"variant-{i}", $"Variant {i}",
                    ManualLayoutOrigin.Manual, OneExtension("Alpha", 10 * i, 10 * i),
                    null, setAsDefault: i == 1, setAsSelected: i == 1);

            // The 11th should be rejected.
            bool ok = mgr.SaveVariant(GroupKey, "variant-11", "Variant 11",
                ManualLayoutOrigin.Manual, OneExtension("Alpha", 110, 110),
                null, setAsDefault: false, setAsSelected: false);

            Assert.False(ok);
            Assert.Equal(10, mgr.ListVariants(GroupKey).Count);
        }
        finally { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
    }
}
