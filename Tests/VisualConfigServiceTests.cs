using System;
using System.IO;
using InteractiveWorldMap.Models;
using InteractiveWorldMap.Services;
using Xunit;

namespace InteractiveWorldMap.Tests;

public class VisualConfigServiceTests
{
    [Fact]
    public void Load_ValidJsonFile_ReturnsConfig()
    {
        var tempDir = CreateTempDir();
        try
        {
            var path = Path.Combine(tempDir, "visual-config.json");
            File.WriteAllText(path, @"{ ""LocationMarkerSize"": 18.5, ""ClusterMarkerSize"": 42.0 }");
            var service = new VisualConfigService();

            var config = service.Load(path);

            Assert.Equal(18.5, config.LocationMarkerSize);
            Assert.Equal(42.0, config.ClusterMarkerSize);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void Load_MissingFile_CreatesDefault()
    {
        var tempDir = CreateTempDir();
        try
        {
            var path = Path.Combine(tempDir, "visual-config.json");
            var service = new VisualConfigService();

            var config = service.Load(path);

            Assert.NotNull(config);
            Assert.True(File.Exists(path));
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void Save_WritesJsonToFile()
    {
        var tempDir = CreateTempDir();
        try
        {
            var path = Path.Combine(tempDir, "visual-config.json");
            var service = new VisualConfigService();

            service.Save(new VisualConfig { LocationMarkerSize = 21.0 }, path);

            var json = File.ReadAllText(path);
            Assert.Contains("LocationMarkerSize", json);
            Assert.Contains("21.0", json);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void Save_DefaultConfig_DoesNotWriteObsoletePinImagesSection()
    {
        var tempDir = CreateTempDir();
        try
        {
            var path = Path.Combine(tempDir, "visual-config.json");
            var service = new VisualConfigService();

            service.Save(new VisualConfig(), path);

            var json = File.ReadAllText(path);
            Assert.DoesNotContain("PinImages", json);
            Assert.DoesNotContain("pins.jpg", json);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void Load_DebugEnableTuningPanel_Deserializes()
    {
        var tempDir = CreateTempDir();
        try
        {
            var path = Path.Combine(tempDir, "visual-config.json");
            File.WriteAllText(path, @"{ ""Debug"": { ""EnableTuningPanel"": true } }");
            var service = new VisualConfigService();

            var config = service.Load(path);

            Assert.True(config.Debug.EnableTuningPanel);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void Load_DebugEnableTuningPanel_UsesDefaultWhenOmitted()
    {
        var tempDir = CreateTempDir();
        try
        {
            var path = Path.Combine(tempDir, "visual-config.json");
            File.WriteAllText(path, @"{ ""Debug"": { ""ShowCompositePinDebugOverlay"": true } }");
            var service = new VisualConfigService();

            var config = service.Load(path);

            Assert.False(config.Debug.EnableTuningPanel);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void EnsureConfigExists_CreatesFileIfMissing()
    {
        var tempDir = CreateTempDir();
        try
        {
            var path = Path.Combine(tempDir, "visual-config.json");
            var service = new VisualConfigService();

            service.EnsureConfigExists(path);

            Assert.True(File.Exists(path));
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void Load_PinPartsDefaultStubLengthPixels_Deserializes()
    {
        var tempDir = CreateTempDir();
        try
        {
            var path = Path.Combine(tempDir, "visual-config.json");
            File.WriteAllText(path, @"{ ""PinParts"": { ""DefaultStubLengthPixels"": 18.0 } }");
            var service = new VisualConfigService();

            var config = service.Load(path);

            Assert.Equal(18.0, config.PinParts.DefaultStubLengthPixels);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void Load_PinPartsDefaultStubLengthPixels_UsesDefaultWhenOmitted()
    {
        var tempDir = CreateTempDir();
        try
        {
            var path = Path.Combine(tempDir, "visual-config.json");
            File.WriteAllText(path, @"{ ""PinParts"": { ""Enabled"": true } }");
            var service = new VisualConfigService();

            var config = service.Load(path);

            Assert.Equal(24.0, config.PinParts.DefaultStubLengthPixels);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void Load_PinPartsUsePrerasterizedRendering_Deserializes()
    {
        var tempDir = CreateTempDir();
        try
        {
            var path = Path.Combine(tempDir, "visual-config.json");
            File.WriteAllText(path, @"{ ""PinParts"": { ""UsePrerasterizedRendering"": true } }");
            var service = new VisualConfigService();

            var config = service.Load(path);

            Assert.True(config.PinParts.UsePrerasterizedRendering);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void Load_PinPartsUsePrerasterizedRendering_UsesDefaultWhenOmitted()
    {
        var tempDir = CreateTempDir();
        try
        {
            var path = Path.Combine(tempDir, "visual-config.json");
            File.WriteAllText(path, @"{ ""PinParts"": { ""Enabled"": true } }");
            var service = new VisualConfigService();

            var config = service.Load(path);

            Assert.False(config.PinParts.UsePrerasterizedRendering);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void Load_PinPartsShaftAssetVariant_Deserializes()
    {
        var tempDir = CreateTempDir();
        try
        {
            var path = Path.Combine(tempDir, "visual-config.json");
            File.WriteAllText(path, @"{ ""PinParts"": { ""ShaftAssetVariant"": ""outline_dark"" } }");
            var service = new VisualConfigService();

            var config = service.Load(path);

            Assert.Equal("outline_dark", config.PinParts.ShaftAssetVariant);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void Load_PinPartsShaftAssetVariant_UsesDefaultWhenOmitted()
    {
        var tempDir = CreateTempDir();
        try
        {
            var path = Path.Combine(tempDir, "visual-config.json");
            File.WriteAllText(path, @"{ ""PinParts"": { ""Enabled"": true } }");
            var service = new VisualConfigService();

            var config = service.Load(path);

            Assert.Equal(string.Empty, config.PinParts.ShaftAssetVariant);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void Load_PinPartsHeadAssetVariant_Deserializes()
    {
        var tempDir = CreateTempDir();
        try
        {
            var path = Path.Combine(tempDir, "visual-config.json");
            File.WriteAllText(path, @"{ ""PinParts"": { ""HeadAssetVariant"": ""outline_black_6px"" } }");
            var service = new VisualConfigService();

            var config = service.Load(path);

            Assert.Equal("outline_black_6px", config.PinParts.HeadAssetVariant);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void Load_PinPartsHeadAssetVariant_UsesDefaultWhenOmitted()
    {
        var tempDir = CreateTempDir();
        try
        {
            var path = Path.Combine(tempDir, "visual-config.json");
            File.WriteAllText(path, @"{ ""PinParts"": { ""Enabled"": true } }");
            var service = new VisualConfigService();

            var config = service.Load(path);

            Assert.Equal(string.Empty, config.PinParts.HeadAssetVariant);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void Load_DrawnPinTipCap_DeserializesStyleAndKnobs()
    {
        var tempDir = CreateTempDir();
        try
        {
            var path = Path.Combine(tempDir, "visual-config.json");
            File.WriteAllText(path, @"{ ""PinMarkers"": { ""DrawnPinTipCap"": { ""Style"": ""Concave"", ""ArcDepthPx"": 4.5, ""HeightPx"": 7.0, ""ExtendPx"": 1.0, ""UseOutlineRing"": false } } }");
            var service = new VisualConfigService();

            var config = service.Load(path);
            var cap = config.PinMarkers.DrawnPinTipCap;

            Assert.Equal(DrawnPinTipCapStyle.Concave, cap.Style);
            Assert.Equal(4.5, cap.ArcDepthPx);
            Assert.Equal(7.0, cap.HeightPx);
            Assert.Equal(1.0, cap.ExtendPx);
            Assert.False(cap.UseOutlineRing);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void Load_DrawnPinTipCap_DefaultsToNoneWhenOmitted()
    {
        var tempDir = CreateTempDir();
        try
        {
            var path = Path.Combine(tempDir, "visual-config.json");
            File.WriteAllText(path, @"{ ""PinMarkers"": { ""ShaftWidth"": 3.0 } }");
            var service = new VisualConfigService();

            var config = service.Load(path);

            Assert.Equal(DrawnPinTipCapStyle.None, config.PinMarkers.DrawnPinTipCap.Style);
            Assert.True(config.PinMarkers.DrawnPinTipCap.UseOutlineRing);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void SaveAndReload_DrawnPinTipCap_RoundTripsStyleAsString()
    {
        var tempDir = CreateTempDir();
        try
        {
            var path = Path.Combine(tempDir, "visual-config.json");
            var service = new VisualConfigService();
            var config = new VisualConfig();
            config.PinMarkers.DrawnPinTipCap.Style = DrawnPinTipCapStyle.Horizontal;

            service.Save(config, path);
            var json = File.ReadAllText(path);
            var reloaded = service.Load(path);

            Assert.Contains("\"Horizontal\"", json); // enum persisted as string, not an int
            Assert.Equal(DrawnPinTipCapStyle.Horizontal, reloaded.PinMarkers.DrawnPinTipCap.Style);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    private static string CreateTempDir()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "iwm-visual-config-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        return tempDir;
    }
}
