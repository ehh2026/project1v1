using System;
using System.IO;
using System.Text.RegularExpressions;
using Xunit;

namespace InteractiveWorldMap.Tests.Architecture;

/// <summary>
/// Protects the release handoff contract without requiring a full GitHub Actions run.
/// </summary>
public class PortableReleaseWorkflowTests
{
    private static string RepoRoot =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));

    private static string Workflow => Read(".github", "workflows", "publish-release.yml");

    [Fact]
    public void PackageScript_CopiesAndValidatorRequiresGalleryTools()
    {
        var packageScript = Read("scripts", "package_windows_release.ps1");
        var validator = Read("scripts", "verify_release_package.py");

        Assert.Matches(
            new Regex(@"Copy-Item\s+-LiteralPath\s+\(Join-Path\s+\$repoRoot\s+'release-tools'\)\s+-Destination\s+\(Join-Path\s+\$stagingRoot\s+'Tools'\)", RegexOptions.IgnoreCase),
            packageScript);

        Assert.Contains("tools/configure-interactiveworldmap.ps1", validator, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("tools/configure-interactiveworldmap.bat", validator, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("tools/run-unattended.bat", validator, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ManualArtifact_UploadsTheValidatedFolderRatherThanANestedZip()
    {
        var step = GetStep("Upload portable release folder");

        Assert.Contains("uses: actions/upload-artifact@v4", step, StringComparison.Ordinal);
        Assert.Contains("name: portable-release", step, StringComparison.Ordinal);
        Assert.Contains(
            "path: artifacts/release/InteractiveWorldMap-win-x64-${{ env.RELEASE_VERSION }}",
            step,
            StringComparison.Ordinal);
        Assert.DoesNotContain(".zip", step, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TaggedRelease_UploadsAndDownloadsTheValidatedArchive()
    {
        var uploadStep = GetStep("Upload portable release archive for tagged release");
        var downloadStep = GetStep("Download validated release archive");

        Assert.Contains("if: github.event_name == 'push' && startsWith(github.ref, 'refs/tags/v')", uploadStep, StringComparison.Ordinal);
        Assert.Contains("name: portable-release-archive", uploadStep, StringComparison.Ordinal);
        Assert.Contains(
            "path: artifacts/release/InteractiveWorldMap-win-x64-${{ env.RELEASE_VERSION }}.zip",
            uploadStep,
            StringComparison.Ordinal);

        Assert.Contains("uses: actions/download-artifact@v4", downloadStep, StringComparison.Ordinal);
        Assert.Contains("name: portable-release-archive", downloadStep, StringComparison.Ordinal);
    }

    private static string GetStep(string stepName)
    {
        var match = Regex.Match(
            Workflow,
            $@"^      - name: {Regex.Escape(stepName)}\r?\n(?<body>.*?)(?=^      - name:|\z)",
            RegexOptions.Multiline | RegexOptions.Singleline);

        Assert.True(match.Success, $"Expected workflow step '{stepName}' was not found.");
        return match.Value;
    }

    private static string Read(params string[] relativePath) =>
        File.ReadAllText(Path.Combine(RepoRoot, Path.Combine(relativePath)));
}
