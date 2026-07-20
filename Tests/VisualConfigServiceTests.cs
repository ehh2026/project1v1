using System;
using System.Collections.Generic;
using System.IO;
using InteractiveWorldMap.Models;
using InteractiveWorldMap.Services;
using Xunit;

namespace InteractiveWorldMap.Tests;

public class VisualConfigServiceTests
{
    private static string RepoRoot =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));

    [Fact]
    public void ZoomedMapRendering_RoundTripsStringEnum()
    {
        var path = Path.GetTempFileName();
        try
        {
            var service = new VisualConfigService();
            var config = new VisualConfig();
            config.ZoomedMapRendering.ResamplingMode = ZoomedMapResamplingMode.MitchellNetravali;

            service.Save(config, path);
            var json = File.ReadAllText(path);
            var reloaded = service.Load(path);

            Assert.Contains("\"ResamplingMode\": \"MitchellNetravali\"", json);
            Assert.Equal(ZoomedMapResamplingMode.MitchellNetravali, reloaded.ZoomedMapRendering.ResamplingMode);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Load_UnknownZoomedMapMode_DefaultsOnlyModeAndWarns()
    {
        var path = Path.GetTempFileName();
        var warnings = new List<string>();
        try
        {
            File.WriteAllText(path,
                "{ \"LocationMarkerSize\": 19.5, \"ZoomedMapRendering\": { \"ResamplingMode\": \"FutureFilter\" } }");
            var config = new VisualConfigService(warnings.Add).Load(path);

            Assert.Equal(19.5, config.LocationMarkerSize);
            Assert.Equal(ZoomedMapResamplingMode.Fant, config.ZoomedMapRendering.ResamplingMode);
            Assert.Single(warnings);
            Assert.Contains("FutureFilter", warnings[0]);
        }
        finally { File.Delete(path); }
    }

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
    public void DebugConfig_WindowedMode_DefaultsToFullscreen()
    {
        var config = new VisualConfig();

        Assert.False(config.Debug.WindowedMode);
    }

    [Fact]
    public void CheckedInVisualConfig_RunsFullscreenByDefault()
    {
        var config = new VisualConfigService().Load(
            Path.Combine(RepoRoot, "visual-config.default.json"));

        Assert.False(config.Debug.WindowedMode);
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

    [Fact]
    public void Load_ClusterMarkerShadow_UsesDisabledDefaultsWhenOmitted()
    {
        var tempDir = CreateTempDir();
        try
        {
            var path = Path.Combine(tempDir, "visual-config.json");
            File.WriteAllText(path, "{}");

            var shadow = new VisualConfigService().Load(path).ClusterMarkerShadow;

            Assert.False(shadow.Enabled);
            Assert.Equal(0.0, shadow.Opacity);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void SaveAndReload_ClusterMarkerShadow_RoundTrips()
    {
        var tempDir = CreateTempDir();
        try
        {
            var path = Path.Combine(tempDir, "visual-config.json");
            var service = new VisualConfigService();
            var config = new VisualConfig
            {
                ClusterMarkerShadow = new ClusterMarkerShadowConfig
                {
                    Enabled = true,
                    Opacity = 0.65
                }
            };

            service.Save(config, path);
            var reloaded = service.Load(path);

            Assert.True(reloaded.ClusterMarkerShadow.Enabled);
            Assert.Equal(0.65, reloaded.ClusterMarkerShadow.Opacity);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void Overlay_MissingUserFile_SeededFromDefault()
    {
        var tempDir = CreateTempDir();
        try
        {
            var userPath = Path.Combine(tempDir, "visual-config.json");
            var defaultPath = Path.Combine(tempDir, "visual-config.default.json");
            File.WriteAllText(defaultPath, "{ \"LocationMarkerSize\": 21.0 }");

            var config = new VisualConfigService().Load(userPath, defaultPath);

            Assert.True(File.Exists(userPath), "User file should be seeded from the default file.");
            Assert.Equal(21.0, config.LocationMarkerSize);
        }
        finally { Directory.Delete(tempDir, true); }
    }

    [Fact]
    public void Overlay_UserValue_WinsOverDefault()
    {
        var tempDir = CreateTempDir();
        try
        {
            var userPath = Path.Combine(tempDir, "visual-config.json");
            var defaultPath = Path.Combine(tempDir, "visual-config.default.json");
            File.WriteAllText(defaultPath, "{ \"LocationMarkerSize\": 12.0, \"ZoomScale\": 55.0 }");
            File.WriteAllText(userPath, "{ \"LocationMarkerSize\": 30.0 }");

            var config = new VisualConfigService().Load(userPath, defaultPath);

            Assert.Equal(30.0, config.LocationMarkerSize); // user override wins
            Assert.Equal(55.0, config.ZoomScale);          // untouched default flows through
        }
        finally { Directory.Delete(tempDir, true); }
    }

    [Fact]
    public void Overlay_NewDefaultKey_ReachesExistingUser()
    {
        var tempDir = CreateTempDir();
        try
        {
            var userPath = Path.Combine(tempDir, "visual-config.json");
            var defaultPath = Path.Combine(tempDir, "visual-config.default.json");
            // Existing user saved before a new key/section was added to the shipped default.
            File.WriteAllText(userPath, "{ \"LocationMarkerSize\": 30.0 }");
            File.WriteAllText(defaultPath,
                "{ \"LocationMarkerSize\": 12.0, \"ContentWindows\": { \"FontFamily\": \"Georgia\" } }");

            var config = new VisualConfigService().Load(userPath, defaultPath);

            Assert.Equal(30.0, config.LocationMarkerSize);          // user override preserved
            Assert.Equal("Georgia", config.ContentWindows.FontFamily); // new default key surfaced
        }
        finally { Directory.Delete(tempDir, true); }
    }

    [Fact]
    public void ContentWindows_DefaultsMatchShippedAppearance()
    {
        var cw = new VisualConfig().ContentWindows;

        Assert.Equal("Segoe UI", cw.FontFamily);
        Assert.Equal(0.70, cw.Popup.BackgroundOpacity);
        Assert.Equal(0.85, cw.Caption.BackgroundOpacity);
        Assert.Equal(18.0, cw.Popup.HeadingFontSize);
        Assert.Equal(14.0, cw.Popup.BodyFontSize);
        Assert.Equal(13.0, cw.Caption.FontSize);
    }

    [Fact]
    public void ContentWindows_RoundTrip()
    {
        var path = Path.GetTempFileName();
        try
        {
            var service = new VisualConfigService();
            var config = new VisualConfig();
            config.ContentWindows.FontFamily = "Georgia";
            config.ContentWindows.Popup.BackgroundOpacity = 0.42;
            config.ContentWindows.Caption.FontSize = 17.0;

            service.Save(config, path);
            var reloaded = service.Load(path);

            Assert.Equal("Georgia", reloaded.ContentWindows.FontFamily);
            Assert.Equal(0.42, reloaded.ContentWindows.Popup.BackgroundOpacity);
            Assert.Equal(17.0, reloaded.ContentWindows.Caption.FontSize);
        }
        finally { File.Delete(path); }
    }

    private static string CreateTempDir()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "iwm-visual-config-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        return tempDir;
    }
}
