using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace InteractiveWorldMap.Tests.Architecture;

/// <summary>
/// Structural tests for golden principles documented in docs/design-docs/golden-principles.md.
/// </summary>
public class GoldenPrincipleTests
{
    private static string RepoRoot =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));

    private static IEnumerable<string> GetCsFilesUnder(string relativeFolder)
    {
        var root = Path.Combine(RepoRoot, relativeFolder);
        if (!Directory.Exists(root))
            yield break;

        foreach (var file in Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories))
        {
            if (file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}") ||
                file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
                continue;
            yield return file;
        }
    }

    [Fact]
    public void Views_DoNotReferenceContentFolderPaths()
    {
        var violations = new List<string>();
        var pattern = new Regex(@"Images&Content", RegexOptions.IgnoreCase);

        foreach (var file in GetCsFilesUnder("Views"))
        {
            var content = File.ReadAllText(file);
            if (pattern.IsMatch(content))
            {
                var relative = Path.GetRelativePath(RepoRoot, file);
                violations.Add(
                    $"{relative}: Views must not construct Images&Content paths. " +
                    "REMEDIATION: Load assets in MainWindow via ContentLoader and pass ImageSource to the View.");
            }
        }

        Assert.True(violations.Count == 0, string.Join("\n", violations));
    }

    [Fact]
    public void Views_DoNotUseJObject()
    {
        var violations = new List<string>();

        foreach (var file in GetCsFilesUnder("Views"))
        {
            var content = File.ReadAllText(file);
            if (content.Contains("JObject", StringComparison.Ordinal))
            {
                var relative = Path.GetRelativePath(RepoRoot, file);
                violations.Add(
                    $"{relative}: Views must not use JObject. " +
                    "REMEDIATION: Deserialize JSON into Models/ types at the service boundary.");
            }
        }

        Assert.True(violations.Count == 0, string.Join("\n", violations));
    }
}
