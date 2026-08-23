using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Xunit;

namespace InteractiveWorldMap.Tests;

/// <summary>
/// WPF property precedence puts a value set directly on an element — a *local value* — above any
/// setter in a style trigger. So an element that declares <c>Background="..."</c> and then a trigger
/// changing <c>Background</c> has a trigger that never fires anything. There is no error and no
/// warning: the markup is valid and the control simply never changes.
///
/// Every hover effect on the edit panel was written that way and silently did nothing for as long
/// as it existed. Nobody catches this by reading — it looks right, and the only symptom is an
/// absence — so it is checked here instead.
/// </summary>
public class XamlLocalValueTests
{
    /// <summary>Appearance properties a trigger is likely to want to change.</summary>
    private static readonly string[] Triggerable =
        { "Background", "Foreground", "BorderBrush", "Cursor", "Opacity" };

    private static string RepoRoot() =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));

    public static IEnumerable<object[]> XamlFiles() =>
        Directory.GetFiles(RepoRoot(), "*.xaml", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")
                     && !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                     && !f.Contains($"{Path.DirectorySeparatorChar}backups{Path.DirectorySeparatorChar}"))
            .Select(f => new object[] { Path.GetFileName(f), f });

    [Theory]
    [MemberData(nameof(XamlFiles))]
    public void NoElementSetsLocallyWhatItsOwnTriggersChange(string fileName, string fullPath)
    {
        // XAML is XML, so read it as XML. Bounding elements by regex works only until markup is
        // nested or reordered, and getting it subtly wrong here means the guard reports success
        // while checking something other than what it claims.
        var root = XDocument.Load(fullPath).Root;
        Assert.NotNull(root);

        var offenders = new List<string>();

        foreach (var element in root!.DescendantsAndSelf())
        {
            // Property-element syntax, e.g. <Button.Style>. Not a control.
            if (element.Name.LocalName.Contains('.')) continue;

            var triggered = TriggeredProperties(element);
            if (triggered.Count == 0) continue;

            foreach (var property in Triggerable)
            {
                // A binding or resource reference is a local value too: it wins over the trigger
                // exactly as a literal does, and does so just as silently.
                if (element.Attribute(property) == null || !triggered.Contains(property)) continue;

                var name = element.Attribute(XName.Get("Name", "http://schemas.microsoft.com/winfx/2006/xaml"))?.Value
                           ?? element.Attribute("Name")?.Value
                           ?? $"<{element.Name.LocalName}>";
                offenders.Add($"{name}.{property}");
            }
        }

        Assert.True(
            offenders.Count == 0,
            $"{fileName}: these set a property on the element that one of their own triggers also " +
            $"sets, so the trigger is silently ignored. Move the property into the Style as a " +
            $"Setter: {string.Join(", ", offenders.Distinct())}");
    }

    /// <summary>
    /// Properties changed by triggers in this element's own inline style — <c>&lt;Foo.Style&gt;</c>
    /// directly beneath it. A style further down the tree belongs to a different element and its
    /// triggers say nothing about this one.
    /// </summary>
    private static HashSet<string> TriggeredProperties(XElement element)
    {
        var styleHolder = element
            .Elements()
            .FirstOrDefault(e => e.Name.LocalName == $"{element.Name.LocalName}.Style");

        var properties = new HashSet<string>(StringComparer.Ordinal);
        if (styleHolder == null) return properties;

        foreach (var setter in styleHolder.Descendants()
                     .Where(e => e.Name.LocalName is "Trigger" or "MultiTrigger" or "DataTrigger")
                     .SelectMany(t => t.Elements().Where(e => e.Name.LocalName == "Setter")))
        {
            var property = setter.Attribute("Property")?.Value;
            if (property != null) properties.Add(property);
        }

        return properties;
    }
}
