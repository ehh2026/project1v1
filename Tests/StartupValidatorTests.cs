using System;
using System.IO;
using InteractiveWorldMap.Services;
using InteractiveWorldMap.Tests.TestHelpers;
using Xunit;

namespace InteractiveWorldMap.Tests;

/// <summary>
/// Unit tests for StartupValidator environment checks.
/// </summary>
public class StartupValidatorTests
{
    [Fact]
    public void ValidateEnvironment_MissingContentFolder_ReturnsError()
    {
        var logger = new MockLogger();
        var missingPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var validator = new StartupValidator(logger, missingPath);

        var result = validator.ValidateEnvironment();

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("Content folder not found"));
    }

    [Fact]
    public void ValidateEnvironment_ValidFolderWithLocationsJson_PassesOrWarnsOnly()
    {
        var tempDir = CreateTempContentFolder(includeMap: false, includeLocations: true);
        try
        {
            var logger = new MockLogger();
            var validator = new StartupValidator(logger, tempDir);

            var result = validator.ValidateEnvironment();

            // Missing map is an error; valid JSON alone should not add JSON format errors
            Assert.DoesNotContain(result.Errors, e => e.Contains("Invalid JSON"));
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void ValidateEnvironment_InvalidLocationsJson_ReturnsJsonError()
    {
        var tempDir = CreateTempContentFolder(includeMap: false, includeLocations: false);
        try
        {
            File.WriteAllText(Path.Combine(tempDir, "locations.json"), "{ not valid json");
            var logger = new MockLogger();
            var validator = new StartupValidator(logger, tempDir);

            var result = validator.ValidateEnvironment();

            Assert.Contains(result.Errors, e => e.Contains("Invalid JSON"));
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void ValidateEnvironment_EmptyLocationsJson_AddsWarning()
    {
        var tempDir = CreateTempContentFolder(includeMap: false, includeLocations: false);
        try
        {
            File.WriteAllText(Path.Combine(tempDir, "locations.json"), "[]");
            var logger = new MockLogger();
            var validator = new StartupValidator(logger, tempDir);

            var result = validator.ValidateEnvironment();

            Assert.Contains(result.Warnings, w => w.Contains("empty"));
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void Constructor_NullLogger_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new StartupValidator(null!, Path.GetTempPath()));
    }

    [Fact]
    public void Constructor_NullContentPath_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new StartupValidator(new MockLogger(), null!));
    }

    private static string CreateTempContentFolder(bool includeMap, bool includeLocations)
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "iwm-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        if (includeMap)
        {
            File.WriteAllText(Path.Combine(tempDir, "World Map 1976.jpg"), "fake");
        }

        if (includeLocations)
        {
            File.WriteAllText(Path.Combine(tempDir, "locations.json"),
                """[{"Id":"1","Name":"Test","PixelX":100,"PixelY":200}]""");
        }

        return tempDir;
    }
}
