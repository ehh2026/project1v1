using System;
using System.IO;
using InteractiveWorldMap.Models;
using InteractiveWorldMap.Services;
using Xunit;

namespace InteractiveWorldMap.Tests;

public class VisualConfigServiceTests
{
    [Fact]
    public void MarkerHitTargets_RoundTrip()
    {
        var path = Path.GetTempFileName();
        try
        {
            var service = new VisualConfigService();
            var config = new VisualConfig();
            config.MarkerHitTargets.PinDiameterPx = 36.0;
            config.MarkerHitTargets.ClusterDiameterPx = 48.0;

            service.Save(config, path);
            var reloaded = service.Load(path);

            Assert.Equal(36.0, reloaded.MarkerHitTargets.PinDiameterPx);
            Assert.Equal(48.0, reloaded.MarkerHitTargets.ClusterDiameterPx);
        }
        finally
        {
            File.Delete(path);
        }
    }

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
    public void Load_EnableDeveloperTools_Deserializes()
    {
        var tempDir = CreateTempDir();
        try
        {
            var path = Path.Combine(tempDir, "visual-config.json");
            File.WriteAllText(path, @"{ ""EnableDeveloperTools"": true }");
            var service = new VisualConfigService();

            var config = service.Load(path);

            Assert.True(config.EnableDeveloperTools);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void Load_EnableDeveloperTools_UsesDefaultFalseWhenOmitted()
    {
        var tempDir = CreateTempDir();
        try
        {
            var path = Path.Combine(tempDir, "visual-config.json");
            File.WriteAllText(path, @"{ ""Debug"": { ""EnableTuningPanel"": true } }");
            var service = new VisualConfigService();

            var config = service.Load(path);

            Assert.False(config.EnableDeveloperTools);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void Load_AutoOpenSingleLocationContentAfterZoom_Deserializes()
    {
        var tempDir = CreateTempDir();
        try
        {
            var path = Path.Combine(tempDir, "visual-config.json");
            File.WriteAllText(path, @"{ ""AutoOpenSingleLocationContentAfterZoom"": true }");
            var service = new VisualConfigService();

            var config = service.Load(path);

            Assert.True(config.AutoOpenSingleLocationContentAfterZoom);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void Load_AutoOpenSingleLocationContentAfterZoom_UsesDefaultFalseWhenOmitted()
    {
        var tempDir = CreateTempDir();
        try
        {
            var path = Path.Combine(tempDir, "visual-config.json");
            File.WriteAllText(path, @"{ ""LocationMarkerSize"": 18.5 }");
            var service = new VisualConfigService();

            var config = service.Load(path);

            Assert.False(config.AutoOpenSingleLocationContentAfterZoom);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void Load_MaximizedContentBackgroundOpacity_UsesOpaqueDefaultWhenOmitted()
    {
        var tempDir = CreateTempDir();
        try
        {
            var path = Path.Combine(tempDir, "visual-config.json");
            File.WriteAllText(path, @"{ ""LocationMarkerSize"": 18.5 }");

            var config = new VisualConfigService().Load(path);

            Assert.Equal(1.0, config.MaximizedContentBackgroundOpacity);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Theory]
    [InlineData(-0.25, 0.0)]
    [InlineData(0.4, 0.4)]
    [InlineData(1.25, 1.0)]
    public void MaximizedContentBackgroundOpacity_Clamps(
        double requested,
        double expected)
    {
        var config = new VisualConfig
        {
            MaximizedContentBackgroundOpacity = requested
        };

        Assert.Equal(expected, config.MaximizedContentBackgroundOpacity);
    }

    [Fact]
    public void Load_MaximizedContentBackgroundOpacity_Deserializes()
    {
        var tempDir = CreateTempDir();
        try
        {
            var path = Path.Combine(tempDir, "visual-config.json");
            File.WriteAllText(
                path,
                @"{ ""MaximizedContentBackgroundOpacity"": 0.65 }");

            var config = new VisualConfigService().Load(path);

            Assert.Equal(0.65, config.MaximizedContentBackgroundOpacity);
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
    public void Load_DrawnPinTipCap_DeserializesStrokeControls()
    {
        var tempDir = CreateTempDir();
        try
        {
            var path = Path.Combine(tempDir, "visual-config.json");
            File.WriteAllText(path, @"{ ""PinMarkers"": { ""DrawnPinTipCap"": { ""Style"": ""Concave"", ""WidthPx"": 14.0, ""LineWeightPx"": 3.5, ""ArcDepthPx"": 4.0, ""Color"": ""#FF111111"" } } }");
            var service = new VisualConfigService();

            var cap = service.Load(path).PinMarkers.DrawnPinTipCap;

            Assert.Equal(14.0, cap.WidthPx);
            Assert.Equal(3.5, cap.LineWeightPx);
            Assert.Equal("#FF111111", cap.Color);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void Load_DrawnPinTipCap_LegacyFieldsResolveWithoutShrinking()
    {
        var tempDir = CreateTempDir();
        try
        {
            var path = Path.Combine(tempDir, "visual-config.json");
            File.WriteAllText(path, @"{ ""PinMarkers"": { ""DrawnPinTipCap"": { ""Style"": ""Horizontal"", ""ExtendPx"": 2.0, ""HeightPx"": 6.0, ""UseOutlineRing"": true } } }");
            var service = new VisualConfigService();

            var cap = service.Load(path).PinMarkers.DrawnPinTipCap;

            Assert.Equal(10.0, cap.ResolveWidthPx(outlineWidthPx: 6.0));
            Assert.Equal(1.5, cap.ResolveLineWeightPx(shaftOutlineThicknessPx: 1.5));
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
            Assert.Null(config.PinMarkers.DrawnPinTipCap.WidthPx);
            Assert.Null(config.PinMarkers.DrawnPinTipCap.LineWeightPx);
            Assert.Equal(3.0, config.PinMarkers.DrawnPinTipCap.ResolveWidthPx(3.0));
            Assert.Equal(1.0, config.PinMarkers.DrawnPinTipCap.ResolveLineWeightPx(0.0));
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void Load_DrawnPinTipCap_DefaultsAlignmentToScreenHorizontal()
    {
        var tempDir = CreateTempDir();
        try
        {
            var path = Path.Combine(tempDir, "visual-config.json");
            File.WriteAllText(
                path,
                @"{ ""PinMarkers"": { ""DrawnPinTipCap"": { ""Style"": ""Concave"" } } }");

            var cap = new VisualConfigService().Load(path).PinMarkers.DrawnPinTipCap;

            Assert.Equal(DrawnPinTipCapAlignment.ScreenHorizontal, cap.Alignment);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Theory]
    [InlineData(DrawnPinTipCapAlignment.ScreenHorizontal)]
    [InlineData(DrawnPinTipCapAlignment.ShaftAligned)]
    public void SaveAndReload_DrawnPinTipCap_RoundTripsAlignmentAsString(
        DrawnPinTipCapAlignment alignment)
    {
        var tempDir = CreateTempDir();
        try
        {
            var path = Path.Combine(tempDir, "visual-config.json");
            var service = new VisualConfigService();
            var config = new VisualConfig();
            config.PinMarkers.DrawnPinTipCap.Alignment = alignment;

            service.Save(config, path);
            var json = File.ReadAllText(path);
            var reloaded = service.Load(path);

            Assert.Contains($"\"{alignment}\"", json);
            Assert.Equal(alignment, reloaded.PinMarkers.DrawnPinTipCap.Alignment);
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
