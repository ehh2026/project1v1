using System.IO;
using InteractiveWorldMap.Models;
using InteractiveWorldMap.Services;
using InteractiveWorldMap.Tests.TestHelpers;
using Xunit;

namespace InteractiveWorldMap.Tests;

/// <summary>
/// Headless harness validation against the real repository layout.
/// Run via: dotnet test --filter "FullyQualifiedName~StartupValidationHarness"
/// </summary>
public class StartupValidationHarnessTests
{
    private static string RepoRoot =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));

    [Fact]
    public void Repo_ContentFolder_Exists()
    {
        var contentPath = Path.Combine(RepoRoot, "Images&Content");
        Assert.True(Directory.Exists(contentPath),
            $"REMEDIATION: Create Images&Content folder at {contentPath}");
    }

    [Fact]
    public void Repo_VisualConfig_Deserializes()
    {
        var configPath = Path.Combine(RepoRoot, "visual-config.json");
        Assert.True(File.Exists(configPath),
            $"REMEDIATION: Add visual-config.json at repo root");

        var config = new VisualConfigService().Load(configPath);
        Assert.NotNull(config);
        Assert.True(config.ClusterDistanceThreshold > 0,
            "REMEDIATION: Set valid ClusterDistanceThreshold in visual-config.json");
        Assert.False(config.Debug.ShowCompositePinDebugOverlay,
            "REMEDIATION: Keep composite pin debug overlay disabled by default in visual-config.json");
    }

    [Fact]
    public void Repo_StartupValidator_RunsWithoutCrash()
    {
        var contentPath = Path.Combine(RepoRoot, "Images&Content");
        if (!Directory.Exists(contentPath))
        {
            return; // Covered by other test
        }

        var validator = new StartupValidator(new MockLogger(), contentPath);
        var result = validator.ValidateEnvironment();

        // Harness reports status; map filename mismatch may cause errors (known debt TD-002)
        Assert.NotNull(result);
    }

    [Fact]
    public void Repo_ContentLoader_ValidatesWhenBuiltOutputPresent()
    {
        var outputContent = Path.Combine(RepoRoot, "bin", "Debug", "net6.0-windows", "Images&Content");
        if (!Directory.Exists(outputContent))
        {
            // Build output not present in CI until after build — skip gracefully
            return;
        }

        var loader = new ContentLoader(new MockLogger()) { ContentFolderPath = outputContent };
        loader.ValidateContentFolder();
    }
}
