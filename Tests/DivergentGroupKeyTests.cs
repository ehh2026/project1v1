using System.IO;
using InteractiveWorldMap.Models;
using InteractiveWorldMap.Services;
using Newtonsoft.Json.Linq;
using Xunit;

using static InteractiveWorldMap.Tests.TestHelpers.LayoutEditorTestFixtures;

namespace InteractiveWorldMap.Tests;

/// <summary>
/// A group is stored twice over: once as its key in the <c>layoutGroups</c> dictionary, and again
/// inside the group as its <c>groupKey</c> field. Nothing the app writes lets those disagree, but
/// manual-layouts.json is documented as hand-editable, and a hand edit that renames one and not the
/// other is an easy mistake — rekeying a group by hand means changing the same string in two places.
///
/// The dictionary key is the one that matters: it is what every lookup is done by. So the field is
/// the copy, and normalisation makes it follow. Before that, the field was only filled when blank,
/// so a stale one survived load and was believed by the resolver, which returned it as the group key
/// for callers that then looked it up in the dictionary and found nothing.
/// </summary>
public class DivergentGroupKeyTests
{
    /// <summary>
    /// Writes a layouts file by hand with the dictionary key and the inner groupKey disagreeing.
    /// Both keys describe the same cluster and zoom, so they are compatible — the resolver reaches
    /// this group by the compatible path, which is where it returned the inner field.
    /// </summary>
    private static (ManualLayoutManager Manager, string DictKey, string AskedAbout) WriteDivergentFile()
    {
        var (_, manager, _, tempDir) = Make();
        const string dictKey = "hash9_z55.00_c10.00_10.00_m3_p10.0_l50.0_n13.0";
        const string askedAbout = "hash9_z55.00_c900.00_900.00_m3_p10.0_l50.0_n13.0";

        // Save normally, then corrupt only the inner field, so everything else in the file is
        // exactly what the app writes.
        manager.SaveVariant(dictKey, "manual", "Mine", ManualLayoutOrigin.Manual,
            OneExtension(), null, setAsDefault: true, setAsSelected: true);

        // Edited through the object model rather than by string replacement: GroupKey appears both
        // inside the group and on the variant mirrored under Layouts, and only the group's copy is
        // the one the resolver returns.
        var path = Path.Combine(tempDir, "layouts.json");
        var json = JObject.Parse(File.ReadAllText(path));
        var group = json["LayoutGroups"]?[dictKey];
        Assert.NotNull(group);
        Assert.Equal(dictKey, (string?)group!["GroupKey"]);
        group["GroupKey"] = "stale-renamed-by-hand";
        File.WriteAllText(path, json.ToString());

        // A fresh manager so the file is re-read rather than answered from anything cached.
        return (new ManualLayoutManager(path, new TestHelpers.MockLogger()), dictKey, askedAbout);
    }

    [Fact]
    public void AStaleGroupKeyField_DoesNotHideTheLayoutFromTheProbe()
    {
        // The failure this guards is silent and user-visible: the probe reports no manual layout,
        // navigation precedence falls back to the full-map layout, and the cluster view shows
        // something other than what the loader was about to return. No error, no log entry.
        var (manager, _, askedAbout) = WriteDivergentFile();

        Assert.True(manager.HasManualVariant(askedAbout));
    }

    [Fact]
    public void AStaleGroupKeyField_DoesNotHideTheLayoutFromTheRestOfTheEditor()
    {
        // Not a probe-only concern. Every group-scoped path resolves through the same helper, so
        // listing, loading and deleting were reachable by the same stale field.
        var (manager, _, askedAbout) = WriteDivergentFile();

        Assert.Equal("Mine", manager.LoadLayout(askedAbout)?.DisplayName);
        Assert.Single(manager.ListVariants(askedAbout));
    }

    [Fact]
    public void TheDictionaryKeyWins_AndTheFieldIsRewrittenToMatch()
    {
        // Stating which of the two is authoritative, rather than only that they stop disagreeing:
        // the dictionary key is what lookups use, so the field follows it and not the reverse.
        var (manager, dictKey, _) = WriteDivergentFile();

        var listed = manager.ListVariants(dictKey);

        Assert.Single(listed);
        Assert.Equal(dictKey, listed[0].GroupKey);
    }
}
