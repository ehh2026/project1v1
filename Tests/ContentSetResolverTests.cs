using System;
using System.IO;
using InteractiveWorldMap.Models;
using InteractiveWorldMap.Utilities;
using Xunit;

namespace InteractiveWorldMap.Tests;

public class ContentSetResolverTests
{
    [Fact]
    public void ResolveActiveContentSet_ProductionWithExcel_ReturnsProduction()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var prodDir = Path.Combine(tempDir, ContentFileNames.ProductionContentFolderName);
        Directory.CreateDirectory(prodDir);
        File.WriteAllText(Path.Combine(prodDir, ContentFileNames.ExcelCoordinateFileName), "fake excel");

        var resolver = new ContentSetResolver();
        var resolution = resolver.ResolveActiveContentSet(tempDir);

        Assert.Equal(ContentSetKind.Production, resolution.Kind);
        Assert.Equal(prodDir, resolution.Path);

        Directory.Delete(tempDir, recursive: true);
    }

    [Fact]
    public void ResolveActiveContentSet_ProductionWithJsonOnly_ReturnsProduction()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var prodDir = Path.Combine(tempDir, ContentFileNames.ProductionContentFolderName);
        Directory.CreateDirectory(prodDir);
        File.WriteAllText(Path.Combine(prodDir, ContentFileNames.LocationsJsonFileName), "fake json");

        var resolver = new ContentSetResolver();
        var resolution = resolver.ResolveActiveContentSet(tempDir);

        Assert.Equal(ContentSetKind.Production, resolution.Kind);
        Assert.Equal(prodDir, resolution.Path);

        Directory.Delete(tempDir, recursive: true);
    }

    [Fact]
    public void ResolveActiveContentSet_ProductionPresentButNoSource_ReturnsDemo()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var prodDir = Path.Combine(tempDir, ContentFileNames.ProductionContentFolderName);
        Directory.CreateDirectory(prodDir); // Empty, no coordinates source
        var demoDir = Path.Combine(tempDir, ContentFileNames.DemoContentFolderName);
        Directory.CreateDirectory(demoDir);
        File.WriteAllText(Path.Combine(demoDir, ContentFileNames.LocationsJsonFileName), "fake json");

        var resolver = new ContentSetResolver();
        var resolution = resolver.ResolveActiveContentSet(tempDir);

        Assert.Equal(ContentSetKind.Demo, resolution.Kind);
        Assert.Equal(demoDir, resolution.Path);

        Directory.Delete(tempDir, recursive: true);
    }

    [Fact]
    public void ResolveActiveContentSet_OnlyDemoWithSource_ReturnsDemo()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var demoDir = Path.Combine(tempDir, ContentFileNames.DemoContentFolderName);
        Directory.CreateDirectory(demoDir);
        File.WriteAllText(Path.Combine(demoDir, ContentFileNames.ExcelCoordinateFileName), "fake excel");

        var resolver = new ContentSetResolver();
        var resolution = resolver.ResolveActiveContentSet(tempDir);

        Assert.Equal(ContentSetKind.Demo, resolution.Kind);
        Assert.Equal(demoDir, resolution.Path);

        Directory.Delete(tempDir, recursive: true);
    }

    [Fact]
    public void ResolveActiveContentSet_NeitherSetHasSource_ReturnsLegacyRoot()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        var resolver = new ContentSetResolver();
        var resolution = resolver.ResolveActiveContentSet(tempDir);

        Assert.Equal(ContentSetKind.Legacy, resolution.Kind);
        Assert.Equal(tempDir, resolution.Path);

        Directory.Delete(tempDir, recursive: true);
    }

    [Fact]
    public void ResolveActiveContentSet_RandomSubfolderAloneDoesNotCountAsSet()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var prodDir = Path.Combine(tempDir, ContentFileNames.ProductionContentFolderName);
        Directory.CreateDirectory(prodDir);
        Directory.CreateDirectory(Path.Combine(prodDir, "__pycache__")); // no coordinate source, only python cache
        var demoDir = Path.Combine(tempDir, ContentFileNames.DemoContentFolderName);
        Directory.CreateDirectory(demoDir);
        Directory.CreateDirectory(Path.Combine(demoDir, ".git")); // no coordinate source, only git folder

        var resolver = new ContentSetResolver();
        var resolution = resolver.ResolveActiveContentSet(tempDir);

        Assert.Equal(ContentSetKind.Legacy, resolution.Kind);
        Assert.Equal(tempDir, resolution.Path);

        Directory.Delete(tempDir, recursive: true);
    }

    [Fact]
    public void ResolveActiveContentSet_ReturnsCorrectKind_ForEachBranch()
    {
        var resolver = new ContentSetResolver();

        // 1. Production
        var tempDir1 = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var prodDir = Path.Combine(tempDir1, ContentFileNames.ProductionContentFolderName);
        Directory.CreateDirectory(prodDir);
        File.WriteAllText(Path.Combine(prodDir, ContentFileNames.LocationsJsonFileName), "[]");
        var resolution1 = resolver.ResolveActiveContentSet(tempDir1);
        Assert.Equal(ContentSetKind.Production, resolution1.Kind);
        Directory.Delete(tempDir1, recursive: true);

        // 2. Demo
        var tempDir2 = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var demoDir = Path.Combine(tempDir2, ContentFileNames.DemoContentFolderName);
        Directory.CreateDirectory(demoDir);
        File.WriteAllText(Path.Combine(demoDir, ContentFileNames.LocationsJsonFileName), "[]");
        var resolution2 = resolver.ResolveActiveContentSet(tempDir2);
        Assert.Equal(ContentSetKind.Demo, resolution2.Kind);
        Directory.Delete(tempDir2, recursive: true);

        // 3. Legacy
        var tempDir3 = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir3);
        var resolution3 = resolver.ResolveActiveContentSet(tempDir3);
        Assert.Equal(ContentSetKind.Legacy, resolution3.Kind);
        Directory.Delete(tempDir3, recursive: true);
    }
}
