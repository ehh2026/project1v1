using System;
using System.IO;
using InteractiveWorldMap.Models;
using InteractiveWorldMap.Services;
using InteractiveWorldMap.Utilities;
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
        var validator = new StartupValidator(logger, missingPath, new ContentSetResolver());

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
            var validator = new StartupValidator(logger, tempDir, new ContentSetResolver());

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
            var validator = new StartupValidator(logger, tempDir, new ContentSetResolver());

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
            var validator = new StartupValidator(logger, tempDir, new ContentSetResolver());

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
            new StartupValidator(null!, Path.GetTempPath(), new ContentSetResolver()));
    }

    [Fact]
    public void Constructor_NullContentPath_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new StartupValidator(new MockLogger(), null!, new ContentSetResolver()));
    }

    [Fact]
    public void Constructor_NullResolver_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new StartupValidator(new MockLogger(), Path.GetTempPath(), null!));
    }

    [Fact]
    public void ValidateEnvironment_WhenWorldMapMissing_AddsError()
    {
        var tempDir = CreateTempContentFolder(includeMap: false, includeLocations: true);
        try
        {
            var validator = new StartupValidator(new MockLogger(), tempDir, new ContentSetResolver());

            var result = validator.ValidateEnvironment();

            Assert.Contains(result.Errors, e => e.Contains("World map image not found"));
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void ValidateEnvironment_WhenCoordinatesMissing_AddsLegacyError()
    {
        var tempDir = CreateTempContentFolder(includeMap: true, includeLocations: false);
        try
        {
            var validator = new StartupValidator(new MockLogger(), tempDir, new ContentSetResolver());

            var result = validator.ValidateEnvironment();

            Assert.Contains(result.Errors, e => e.Contains("Legacy content root is missing a coordinate source"));
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void ValidateEnvironment_WithProductionContentSet_ValidatesProductionPaths()
    {
        var tempDir = CreateTempContentFolder(includeMap: true, includeLocations: false);
        try
        {
            var production = Path.Combine(tempDir, ContentFileNames.ProductionContentFolderName);
            Directory.CreateDirectory(production);
            File.WriteAllText(
                Path.Combine(production, ContentFileNames.LocationsJsonFileName),
                "[{\"Id\":\"1\",\"Name\":\"Prod\",\"PixelX\":100,\"PixelY\":200}]");
            var logger = new MockLogger();
            var validator = new StartupValidator(logger, tempDir, new ContentSetResolver());

            var result = validator.ValidateEnvironment();

            Assert.True(result.IsValid);
            Assert.Contains(logger.InfoMessages, message => message.Contains("Production"));
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void ValidateEnvironment_WithExcelOnlyCoordinateSource_DoesNotAddCoordinateSourceError()
    {
        var tempDir = CreateTempContentFolder(includeMap: true, includeLocations: false);
        try
        {
            File.WriteAllText(Path.Combine(tempDir, ContentFileNames.ExcelCoordinateFileName), "placeholder");
            var validator = new StartupValidator(new MockLogger(), tempDir, new ContentSetResolver());

            var result = validator.ValidateEnvironment();

            Assert.DoesNotContain(result.Errors, e => e.Contains("missing a coordinate source"));
            Assert.True(result.IsValid);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void ValidateLocationsJson_WithMissingName_AddsWarning()
    {
        var tempDir = CreateTempContentFolder(includeMap: true, includeLocations: false);
        try
        {
            File.WriteAllText(
                Path.Combine(tempDir, ContentFileNames.LocationsJsonFileName),
                "[{\"Id\":\"1\",\"PixelX\":100,\"PixelY\":200}]");
            var validator = new StartupValidator(new MockLogger(), tempDir, new ContentSetResolver());

            var result = validator.ValidateEnvironment();

            Assert.Contains(result.Warnings, w => w.Contains("missing 'Name' field"));
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void ValidateLocationsJson_WithMissingPixelX_AddsWarning()
    {
        var tempDir = CreateTempContentFolder(includeMap: true, includeLocations: false);
        try
        {
            File.WriteAllText(
                Path.Combine(tempDir, ContentFileNames.LocationsJsonFileName),
                "[{\"Id\":\"1\",\"Name\":\"Test\",\"PixelY\":200}]");
            var validator = new StartupValidator(new MockLogger(), tempDir, new ContentSetResolver());

            var result = validator.ValidateEnvironment();

            Assert.Contains(result.Warnings, w => w.Contains("missing 'PixelX' field"));
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void ValidateLocationsJson_WithNonObjectLocation_AddsWarning()
    {
        var tempDir = CreateTempContentFolder(includeMap: true, includeLocations: false);
        try
        {
            File.WriteAllText(Path.Combine(tempDir, ContentFileNames.LocationsJsonFileName), "[42]");
            var validator = new StartupValidator(new MockLogger(), tempDir, new ContentSetResolver());

            var result = validator.ValidateEnvironment();

            Assert.Contains(result.Warnings, w => w.Contains("not a valid object"));
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void ValidateLocationsJson_WithOutOfRangeCoordinate_AddsWarning()
    {
        var tempDir = CreateTempContentFolder(includeMap: true, includeLocations: false);
        try
        {
            File.WriteAllText(
                Path.Combine(tempDir, ContentFileNames.LocationsJsonFileName),
                "[{\"Id\":\"1\",\"Name\":\"Test\",\"PixelX\":9999,\"PixelY\":-1}]");
            var validator = new StartupValidator(
                new MockLogger(),
                tempDir,
                new ContentSetResolver(),
                new MapMetadata(displayWidth: 100, displayHeight: 50, fullResWidth: 200, fullResHeight: 100));

            var result = validator.ValidateEnvironment();

            Assert.Contains(result.Warnings, w => w.Contains("invalid PixelX"));
            Assert.Contains(result.Warnings, w => w.Contains("invalid PixelY"));
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void ValidateLocationsJson_WithValidCoordinate_DoesNotAddCoordinateWarning()
    {
        var tempDir = CreateTempContentFolder(includeMap: true, includeLocations: true);
        try
        {
            var validator = new StartupValidator(
                new MockLogger(),
                tempDir,
                new ContentSetResolver(),
                new MapMetadata(displayWidth: 200, displayHeight: 250, fullResWidth: 400, fullResHeight: 500));

            var result = validator.ValidateEnvironment();

            Assert.DoesNotContain(result.Warnings, w => w.Contains("invalid Pixel"));
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    private static string CreateTempContentFolder(bool includeMap, bool includeLocations)
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "iwm-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        if (includeMap)
        {
            File.WriteAllText(Path.Combine(tempDir, ContentFileNames.WorldMapFileName), "fake");
        }

        if (includeLocations)
        {
            File.WriteAllText(
                Path.Combine(tempDir, "locations.json"),
                "[{\"Id\":\"1\",\"Name\":\"Test\",\"PixelX\":100,\"PixelY\":200}]");
        }

        return tempDir;
    }
}
