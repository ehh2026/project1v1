using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace InteractiveWorldMap.Tests.Architecture;

/// <summary>
/// Structural tests enforcing layer dependency rules from ARCHITECTURE.md.
/// </summary>
public class LayerDependencyTests
{
    private static readonly Dictionary<string, string[]> ForbiddenReferences = new()
    {
        ["Models"] = new[] { "InteractiveWorldMap.Services", "InteractiveWorldMap.Utilities", "InteractiveWorldMap.Views" },
        ["Utilities"] = new[] { "InteractiveWorldMap.Views" },
        ["Services"] = new[] { "InteractiveWorldMap.Views" },
        ["Views"] = new[] { "InteractiveWorldMap.Services", "InteractiveWorldMap.Utilities" },
    };

    private static string RepoRoot =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));

    private static IEnumerable<string> GetProjectCsFiles()
    {
        var exclude = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            Path.Combine(RepoRoot, "Tests"),
            Path.Combine(RepoRoot, "obj"),
            Path.Combine(RepoRoot, "bin"),
        };

        foreach (var file in Directory.EnumerateFiles(RepoRoot, "*.cs", SearchOption.AllDirectories))
        {
            if (exclude.Any(ex => file.StartsWith(ex, StringComparison.OrdinalIgnoreCase)))
                continue;
            if (file.Contains($"{Path.DirectorySeparatorChar}Tests{Path.DirectorySeparatorChar}"))
                continue;
            yield return file;
        }
    }

    private static string GetLayer(string filePath)
    {
        if (filePath.Contains($"{Path.DirectorySeparatorChar}Models{Path.DirectorySeparatorChar}"))
            return "Models";
        if (filePath.Contains($"{Path.DirectorySeparatorChar}Utilities{Path.DirectorySeparatorChar}"))
            return "Utilities";
        if (filePath.Contains($"{Path.DirectorySeparatorChar}Services{Path.DirectorySeparatorChar}"))
            return "Services";
        if (filePath.Contains($"{Path.DirectorySeparatorChar}Views{Path.DirectorySeparatorChar}"))
            return "Views";
        return "Other";
    }

    [Fact]
    public void Layers_DoNotViolateDependencyRules()
    {
        var violations = new List<string>();
        var usingPattern = new Regex(@"^\s*using\s+(InteractiveWorldMap\.\w+)\s*;", RegexOptions.Multiline);

        foreach (var file in GetProjectCsFiles())
        {
            var layer = GetLayer(file);
            if (!ForbiddenReferences.ContainsKey(layer))
                continue;

            var content = File.ReadAllText(file);
            var relative = Path.GetRelativePath(RepoRoot, file);

            foreach (var forbiddenNs in ForbiddenReferences[layer])
            {
                if (!content.Contains(forbiddenNs, StringComparison.Ordinal))
                    continue;

                var viaUsing = usingPattern.Matches(content).Select(m => m.Groups[1].Value).Distinct().Contains(forbiddenNs);
                var detail = viaUsing ? "using or type reference" : "type reference";
                violations.Add(
                    $"{relative}: {layer} must not reference {forbiddenNs} ({detail}). " +
                    "REMEDIATION: Move shared logic to Services/ or Models/, inject dependencies via constructor.");
            }
        }

        Assert.True(violations.Count == 0, "Layer violations:\n" + string.Join("\n", violations));
    }
}
