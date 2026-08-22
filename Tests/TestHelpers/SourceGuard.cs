using System;
using System.IO;
using Xunit;

namespace InteractiveWorldMap.Tests.TestHelpers;

/// <summary>
/// Reading MainWindow's source as text. MainWindow cannot be instantiated under test (WPF), so
/// several behaviours are pinned by asserting on the source that produces them.
/// </summary>
internal static class SourceGuard
{
    private static readonly string RepoRoot =
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));

    internal static string Read(string fileName) =>
        File.ReadAllText(Path.Combine(RepoRoot, fileName));

    internal static string[] Files(string searchPattern) =>
        Directory.GetFiles(RepoRoot, searchPattern);

    /// <summary>
    /// The text of one member, bounded by matching braces.
    /// </summary>
    /// <remarks>
    /// Not by "the next member declaration": that ends the body early at any nested member and
    /// breaks on reindentation. A body cut short makes every <c>DoesNotContain</c> assertion pass
    /// for free — the direction a guard test must never fail in, since it reports success while
    /// checking nothing.
    /// </remarks>
    internal static string MemberBody(string source, string signature)
    {
        var start = source.IndexOf(signature, StringComparison.Ordinal);
        Assert.True(start >= 0, $"{signature} not found.");

        var open = source.IndexOf('{', start);
        Assert.True(open >= 0, $"No body found for {signature}.");

        var depth = 0;
        for (var i = open; i < source.Length; i++)
        {
            if (source[i] == '{') depth++;
            else if (source[i] == '}' && --depth == 0)
                return source.Substring(start, i - start + 1);
        }

        Assert.True(false, $"Unbalanced braces after {signature}.");
        return "";
    }
}
