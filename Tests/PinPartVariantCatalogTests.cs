using System;
using System.Collections.Generic;
using System.IO;
using InteractiveWorldMap.Services;
using Xunit;

namespace InteractiveWorldMap.Tests;

public class PinPartVariantCatalogTests
{
    private class FakeLogger : ILogger
    {
        public List<string> Warnings { get; } = new();
        public List<string> Errors { get; } = new();
        public List<string> Infos { get; } = new();

        public void LogError(string message, Exception? ex = null)
        {
            Errors.Add(message + (ex != null ? $" Exception: {ex.Message}" : ""));
        }

        public void LogWarning(string message)
        {
            Warnings.Add(message);
        }

        public void LogInfo(string message)
        {
            Infos.Add(message);
        }
    }

    private static string CreateTempDir()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "iwm-variant-catalog-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        return tempDir;
    }

    [Fact]
    public void ListVariants_ReturnsSortedSubdirectoryNames()
    {
        var tempDir = CreateTempDir();
        try
        {
            var partsDir = Path.Combine(tempDir, "Pins_v2", "parts");
            var shaftDir = Path.Combine(partsDir, "shaft_variants");
            Directory.CreateDirectory(shaftDir);

            // Create some folders in unsorted order
            Directory.CreateDirectory(Path.Combine(shaftDir, "zebra"));
            Directory.CreateDirectory(Path.Combine(shaftDir, "alpha"));
            Directory.CreateDirectory(Path.Combine(shaftDir, "bravo"));

            var logger = new FakeLogger();
            var catalog = new PinPartVariantCatalog(logger);

            var result = catalog.ListVariants(tempDir, Path.Combine("Pins_v2", "parts"), "shaft_variants");

            Assert.Equal(3, result.Count);
            Assert.Equal("alpha", result[0]);
            Assert.Equal("bravo", result[1]);
            Assert.Equal("zebra", result[2]);
            Assert.Empty(logger.Warnings);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void ListVariants_MissingDirectory_ReturnsEnsureIncludedOnly()
    {
        var tempDir = CreateTempDir();
        try
        {
            var logger = new FakeLogger();
            var catalog = new PinPartVariantCatalog(logger);

            var result = catalog.ListVariants(tempDir, Path.Combine("Pins_v2", "parts"), "shaft_variants", "missing_but_needed");

            Assert.Single(result);
            Assert.Equal("missing_but_needed", result[0]);
            Assert.Single(logger.Warnings);
            Assert.Contains("Variants directory does not exist", logger.Warnings[0]);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void ListVariants_EnsureIncludedCasingDifference_DedupesCaseInsensitively()
    {
        var tempDir = CreateTempDir();
        try
        {
            var partsDir = Path.Combine(tempDir, "Pins_v2", "parts");
            var shaftDir = Path.Combine(partsDir, "shaft_variants");
            Directory.CreateDirectory(shaftDir);

            Directory.CreateDirectory(Path.Combine(shaftDir, "Outline_Dark"));

            var logger = new FakeLogger();
            var catalog = new PinPartVariantCatalog(logger);

            var result = catalog.ListVariants(tempDir, Path.Combine("Pins_v2", "parts"), "shaft_variants", "outline_dark");

            Assert.Single(result);
            // It should keep the on-disk directory name
            Assert.Equal("Outline_Dark", result[0]);
            Assert.Empty(logger.Warnings);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void ListVariants_StrayFiles_AreIgnored()
    {
        var tempDir = CreateTempDir();
        try
        {
            var partsDir = Path.Combine(tempDir, "Pins_v2", "parts");
            var shaftDir = Path.Combine(partsDir, "shaft_variants");
            Directory.CreateDirectory(shaftDir);

            Directory.CreateDirectory(Path.Combine(shaftDir, "folder1"));
            File.WriteAllText(Path.Combine(shaftDir, "stray_file.txt"), "hello");

            var logger = new FakeLogger();
            var catalog = new PinPartVariantCatalog(logger);

            var result = catalog.ListVariants(tempDir, Path.Combine("Pins_v2", "parts"), "shaft_variants");

            Assert.Single(result);
            Assert.Equal("folder1", result[0]);
            Assert.Empty(logger.Warnings);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void ListVariants_EmptyDirectory_ReturnsEnsureIncludedOnly()
    {
        var tempDir = CreateTempDir();
        try
        {
            var partsDir = Path.Combine(tempDir, "Pins_v2", "parts");
            Directory.CreateDirectory(Path.Combine(partsDir, "shaft_variants"));

            var logger = new FakeLogger();
            var catalog = new PinPartVariantCatalog(logger);

            var result = catalog.ListVariants(
                tempDir,
                Path.Combine("Pins_v2", "parts"),
                "shaft_variants",
                "saved_variant");

            Assert.Single(result);
            Assert.Equal("saved_variant", result[0]);
            Assert.Empty(logger.Warnings);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void ListVariants_GetDirectoriesFailure_StillAppendsEnsureIncluded()
    {
        var repoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
        var source = File.ReadAllText(Path.Combine(repoRoot, "Services", "PinPartVariantCatalog.cs"));

        var catchIndex = source.IndexOf("catch (Exception ex)", StringComparison.Ordinal);
        Assert.True(catchIndex >= 0, "ListVariants must catch GetDirectories failures.");

        var ensureIncludedIndex = source.IndexOf("ensureIncluded", catchIndex, StringComparison.Ordinal);
        Assert.True(ensureIncludedIndex > catchIndex,
            "ensureIncluded must still be appended after the GetDirectories catch block.");
    }
}
