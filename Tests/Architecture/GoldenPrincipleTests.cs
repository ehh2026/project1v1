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

    [Fact]
    public void Views_DoNotCastApplicationCurrentMainWindow()
    {
        var violations = new List<string>();

        foreach (var file in GetCsFilesUnder("Views"))
        {
            var content = File.ReadAllText(file);
            if (content.Contains("Application.Current.MainWindow", StringComparison.Ordinal))
            {
                var relative = Path.GetRelativePath(RepoRoot, file);
                violations.Add(
                    $"{relative}: Views must not cast Application.Current.MainWindow. " +
                    "REMEDIATION: Pass marker configuration into the View constructor.");
            }
        }

        Assert.True(violations.Count == 0, string.Join("\n", violations));
    }

    [Fact]
    public void Models_DoNotPerformFileIo()
    {
        var violations = new List<string>();
        var ioPattern = new Regex(@"\b(File|Directory)\.", RegexOptions.CultureInvariant);

        foreach (var file in GetCsFilesUnder("Models"))
        {
            var content = File.ReadAllText(file);
            if (content.Contains("using System.IO;", StringComparison.Ordinal) ||
                ioPattern.IsMatch(content))
            {
                var relative = Path.GetRelativePath(RepoRoot, file);
                violations.Add(
                    $"{relative}: Models must not perform file I/O. " +
                    "REMEDIATION: Move load/save/ensure behavior into a Service.");
            }
        }

        Assert.True(violations.Count == 0, string.Join("\n", violations));
    }

    [Fact]
    public void AppCode_UsesLoggerInsteadOfConsoleWriteLine()
    {
        var violations = new List<string>();

        foreach (var file in GetCsFilesUnder("."))
        {
            var relative = Path.GetRelativePath(RepoRoot, file);
            if (relative.StartsWith("Tests", StringComparison.OrdinalIgnoreCase) ||
                relative.StartsWith("backups", StringComparison.OrdinalIgnoreCase) ||
                relative.StartsWith("Tools", StringComparison.OrdinalIgnoreCase) ||
                relative.Equals(Path.Combine("Services", "FileLogger.cs"), StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var content = File.ReadAllText(file);
            if (content.Contains("Console.WriteLine", StringComparison.Ordinal))
            {
                violations.Add($"{relative}: use ILogger or remove diagnostic console output.");
            }
        }

        Assert.True(violations.Count == 0, string.Join("\n", violations));
    }

    [Fact]
    public void MainWindow_DoesNotUseTaskDelayContinueWith()
    {
        var path = Path.Combine(RepoRoot, "MainWindow.xaml.cs");
        var content = File.ReadAllText(path);

        Assert.DoesNotContain("ContinueWith", content);
    }

    [Fact]
    public void MainWindow_UsesInteractionModeInsteadOfAnimatingFlag()
    {
        var path = Path.Combine(RepoRoot, "MainWindow.xaml.cs");
        var content = File.ReadAllText(path);

        Assert.Contains("enum InteractionMode", content);
        Assert.DoesNotContain("_isAnimating", content);
    }

    [Fact]
    public void ContentSubwindow_UsesSingleContentSizingMethod()
    {
        var path = Path.Combine(RepoRoot, "Views", "ContentSubwindow.xaml.cs");
        var content = File.ReadAllText(path);

        Assert.Contains("CalculateContentSize", content);
        Assert.DoesNotContain("CalculateSizeForImage", content);
        Assert.DoesNotContain("CalculateSizeForText", content);
    }
}
