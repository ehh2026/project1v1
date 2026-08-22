using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

using static InteractiveWorldMap.Tests.TestHelpers.SourceGuard;

namespace InteractiveWorldMap.Tests;

/// <summary>
/// WPF property precedence puts a value set directly on an element — a *local value* — above any
/// setter in a style trigger. So a button that declares <c>Background="..."</c> on the element and
/// then a trigger changing <c>Background</c> has a trigger that never fires anything. There is no
/// error and no warning: the button simply never changes.
///
/// Every hover effect and the disabled state of the edit panel was written that way and silently
/// did nothing. The fix is to declare such properties as Setters in the Style; this test is here so
/// the next one written the old way fails loudly instead of quietly not working.
/// </summary>
public class XamlLocalValueTests
{
    /// <summary>Appearance properties a trigger is likely to want to change.</summary>
    private static readonly string[] Triggerable =
        { "Background", "Foreground", "BorderBrush", "Cursor", "Opacity" };

    public static IEnumerable<object[]> XamlFiles() =>
        Directory.GetFiles(RepoRootForTests(), "*.xaml", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")
                     && !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                     && !f.Contains($"{Path.DirectorySeparatorChar}backups{Path.DirectorySeparatorChar}"))
            .Select(f => new object[] { Path.GetFileName(f), f });

    private static string RepoRootForTests() =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));

    [Theory]
    [MemberData(nameof(XamlFiles))]
    public void NoElementSetsLocallyWhatItsOwnTriggersChange(string fileName, string fullPath)
    {
        var xaml = File.ReadAllText(fullPath);
        var offenders = new List<string>();

        // One chunk per Button. Only Buttons with their own inline <Button.Style> matter: the
        // triggers have to belong to the same element as the attributes, or this compares a
        // template's inner Border against the button's triggers and invents offenders.
        foreach (var chunk in Regex.Split(xaml, @"(?=<Button )"))
        {
            var tagEnd = chunk.IndexOf('>');
            if (tagEnd < 0) continue;

            var attributes = chunk.Substring(0, tagEnd);
            var ownStyle = chunk.IndexOf("<Button.Style>", StringComparison.Ordinal);
            if (ownStyle < 0) continue;

            var triggerSection = chunk.IndexOf("Style.Triggers", ownStyle, StringComparison.Ordinal);
            if (triggerSection < 0) continue;

            // Bound the triggers at the end of this button's style, so a later sibling's triggers
            // are not read as this one's.
            var styleEnd = chunk.IndexOf("</Button.Style>", ownStyle, StringComparison.Ordinal);
            if (styleEnd < 0) styleEnd = chunk.Length;
            var triggers = chunk.Substring(triggerSection, styleEnd - triggerSection);

            var name = Regex.Match(attributes, @"x:Name=""(\w+)""").Groups[1].Value;

            foreach (var property in Triggerable)
            {
                // A binding is not a static local value and is normally the intended mechanism.
                var local = Regex.Match(attributes, $@"\s{property}=""(?<value>[^""]*)""");
                var setLocally = local.Success && !local.Groups["value"].Value.StartsWith("{");
                var setByTrigger = triggers.Contains($@"<Setter Property=""{property}""", StringComparison.Ordinal);

                if (setLocally && setByTrigger)
                    offenders.Add($"{(name.Length > 0 ? name : "(unnamed)")}.{property}");
            }
        }

        Assert.True(
            offenders.Count == 0,
            $"{fileName}: these set a property on the element that one of their own triggers also " +
            $"sets, so the trigger is silently ignored — move the property into the Style as a " +
            $"Setter: {string.Join(", ", offenders)}");
    }
}
