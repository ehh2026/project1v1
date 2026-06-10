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

    private static string CreateTempDir()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "iwm-visual-config-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        return tempDir;
    }
}
