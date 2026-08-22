using System;
using System.IO;
using Xunit;

namespace InteractiveWorldMap.Tests;

/// <summary>
/// The edit panel used to look identical whichever view you were editing. Layouts are per view and
/// always have been, so "I saved this and it vanished" was usually a layout sitting safely under a
/// different scope, invisible because nothing named the scope.
///
/// MainWindow cannot be instantiated under test (WPF), so the wiring is pinned against source.
/// </summary>
public class LayoutEditorScopeUiTests
{
    private static readonly string RepoRoot =
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));

    private static string ReadSource(string fileName) =>
        File.ReadAllText(Path.Combine(RepoRoot, fileName));

    [Fact]
    public void Panel_NamesTheScopeBeingEdited()
    {
        Assert.Contains("x:Name=\"EditScopeText\"", ReadSource("MainWindow.xaml"));
    }

    [Fact]
    public void ScopeLabel_ComesFromTheSessionThatPerformsTheSave()
    {
        // Not rebuilt in the panel from the viewport or the key. The session is the object a save
        // writes through, so deriving the label from anything else lets the two disagree -- the
        // panel naming one view while the save lands in another, which is the failure this label
        // exists to make visible.
        var source = ReadSource("MainWindow.LayoutEditor.partial.cs");

        Assert.Contains("session.ScopeDescription", source);
    }

    [Fact]
    public void ScopeLabel_IsClearedWhenTheSessionEnds()
    {
        // A scope left on screen after the session ends names something no save can reach.
        var source = ReadSource("MainWindow.LayoutEditor.partial.cs");

        var endIndex = source.IndexOf("_layoutEditor.EndEditSession();", StringComparison.Ordinal);
        Assert.True(endIndex >= 0, "EndEditSession call not found.");

        var afterEnd = source.Substring(endIndex);
        var clearIndex = afterEnd.IndexOf("EditScopeText.Text = \"\";", StringComparison.Ordinal);
        Assert.True(clearIndex >= 0, "The scope label must be cleared once the session has ended.");
    }

    [Fact]
    public void VariantList_IsLabelledAsBelongingToThisView()
    {
        // "Variants:" over an empty list reads as missing work. "for this view" makes an empty list
        // a statement about where you are standing.
        var xaml = ReadSource("MainWindow.xaml");

        Assert.DoesNotContain("Text=\"Variants:\"", xaml);
        Assert.Contains("Saved layouts for this view:", xaml);
    }

    [Fact]
    public void EmptyVariantList_ExplainsItselfThroughTheDescriber()
    {
        // The panel must not decide this itself. An empty list has two causes -- nothing saved, or
        // saved but unlistable at this window size -- and only one of them is safe to state as
        // fact. VariantStatusDescriber owns that distinction and is tested directly.
        var source = ReadSource("MainWindow.LayoutEditor.partial.cs");

        Assert.Contains("VariantStatusDescriber.Describe(", source);
        Assert.DoesNotContain("None saved for this view yet", source);
    }

    [Fact]
    public void Actions_AreGroupedByWhetherTheyAddOrRemove()
    {
        // Five buttons in one undifferentiated column read as five similar options. The two that
        // write a layout come first, then a separator, then the three that stop one being used --
        // the non-destructive one at the head of that group, since it is what reaching for the red
        // button usually meant.
        var xaml = ReadSource("MainWindow.xaml");

        int At(string name)
        {
            var index = xaml.IndexOf($"x:Name=\"{name}\"", StringComparison.Ordinal);
            Assert.True(index >= 0, $"{name} not found in MainWindow.xaml.");
            return index;
        }

        var save = At("SaveLayoutButton");
        var saveAs = At("SaveAsVariantButton");
        var separator = xaml.IndexOf("Stop using this layout", StringComparison.Ordinal);
        var unload = At("UnloadLayoutButton");
        var deleteOne = At("DeleteVariantButton");
        var deleteAll = At("DeleteLayoutButton");

        Assert.True(separator >= 0, "The two groups should be separated.");
        Assert.True(save < saveAs, "Save should lead the group that writes a layout.");
        Assert.True(saveAs < separator, "Both writing actions belong above the separator.");
        Assert.True(separator < unload, "The separator introduces the actions that stop a layout being used.");
        Assert.True(unload < deleteOne, "The non-destructive action should lead its group.");
        Assert.True(deleteOne < deleteAll, "Deleting one should come before deleting all of them.");
    }
}
