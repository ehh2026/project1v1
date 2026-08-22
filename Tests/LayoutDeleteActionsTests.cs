using System;
using System.IO;
using System.Linq;
using InteractiveWorldMap.Models;
using Xunit;

using static InteractiveWorldMap.Tests.TestHelpers.LayoutEditorTestFixtures;

namespace InteractiveWorldMap.Tests;

/// <summary>
/// The editor offers three ways to stop a saved layout being used, and they differ in how much
/// they destroy: unload (nothing), delete this one (one variant), delete all (every hand-made
/// variant for the view). The button that destroyed the most used to be the one labelled most
/// mildly — "Delete and Recalculate" — and it asked nothing before running.
///
/// MainWindow cannot be instantiated under test (WPF), so the wiring is pinned against source and
/// the deletion behaviour against the controller.
/// </summary>
public class LayoutDeleteActionsTests
{
    private static readonly string RepoRoot =
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));

    private static string ReadSource(string fileName) =>
        File.ReadAllText(Path.Combine(RepoRoot, fileName));

    private static string HandlerBody(string source, string handler)
    {
        var start = source.IndexOf(handler, StringComparison.Ordinal);
        Assert.True(start >= 0, $"{handler} not found.");

        // Bounded by the next member declaration, which is enough to keep one handler's
        // assertions from being satisfied by a neighbour's code.
        var end = source.IndexOf("        private ", start + handler.Length, StringComparison.Ordinal);
        if (end < 0) end = source.Length;
        return source.Substring(start, end - start);
    }

    [Fact]
    public void BulkDelete_CountsAndConfirmsBeforeDestroyingAnything()
    {
        var source = ReadSource("MainWindow.LayoutEditorDelete.partial.cs");
        var body = HandlerBody(source, "private void OnDeleteLayoutButtonClick");

        var countIndex = body.IndexOf("GetDeletableVariants()", StringComparison.Ordinal);
        var deleteIndex = body.IndexOf("_layoutEditor.TryDelete()", StringComparison.Ordinal);

        // Anchor on the *decision* prompt, not on "MessageBox.Show(". The handler shows two
        // dialogs -- an informational one for the nothing-to-delete case, then the confirmation --
        // and the informational one comes first in the source. Matching the first Show() would let
        // the confirmation move below the deletion while this test still passed.
        var confirmIndex = body.IndexOf("MessageBoxButton.YesNo", StringComparison.Ordinal);

        Assert.True(deleteIndex >= 0, "The bulk handler must still be the one calling TryDelete.");
        Assert.True(confirmIndex >= 0, "The bulk delete must ask a yes/no question.");
        Assert.True(
            countIndex >= 0 && countIndex < confirmIndex,
            "The variants must be counted before the prompt, so the prompt can state how many " +
            "are about to be destroyed.");
        Assert.True(
            confirmIndex < deleteIndex,
            "Nothing may be deleted before the user has confirmed.");

        // The answer must also be acted on. Reaching the prompt is not the same as honouring it.
        var bailIndex = body.IndexOf("!= MessageBoxResult.Yes", StringComparison.Ordinal);
        Assert.True(
            bailIndex >= 0 && bailIndex < deleteIndex,
            "Anything other than Yes must return before the deletion.");

        // A prompt that is not read is not a confirmation: defaulting to No means a stray Enter
        // cancels rather than destroys.
        Assert.Contains("MessageBoxResult.No", body);

        // The count has to reach the text, not just the log.
        Assert.Contains("doomed.Count", body);
    }

    [Fact]
    public void BulkDelete_PromptDoesNotClaimToDestroyTheSeeds()
    {
        // TryDelete leaves AutoSeed and Imported variants alone, so a prompt saying it removes
        // "ALL saved layouts" would promise destruction that does not happen. Wrong in the safe
        // direction, but it is the same defect as the one this phase fixed -- a button whose words
        // and behaviour disagree -- and it would push someone toward cancelling to save work that
        // was never at risk.
        var source = ReadSource("MainWindow.LayoutEditorDelete.partial.cs");
        var body = HandlerBody(source, "private void OnDeleteLayoutButtonClick");

        var confirmIndex = body.IndexOf("MessageBoxButton.YesNo", StringComparison.Ordinal);
        Assert.True(confirmIndex >= 0);
        var prompt = body.Substring(0, confirmIndex);

        Assert.Contains("hand-made layout(s) saved for this view", prompt);
        Assert.DoesNotContain("saved layout(s) for this view?", prompt);
    }

    [Fact]
    public void BulkDelete_PointsAtTheNonDestructiveActionInstead()
    {
        // The reported confusion was reaching for this button wanting automatic placement back,
        // not wanting saved work destroyed. Both the "nothing to delete" notice and the
        // confirmation name the action that actually does that.
        var source = ReadSource("MainWindow.LayoutEditorDelete.partial.cs");
        var body = HandlerBody(source, "private void OnDeleteLayoutButtonClick");

        var mentions = body.Split(new[] { "Unload and Recalculate" }, StringSplitOptions.None).Length - 1;
        Assert.True(
            mentions >= 2,
            "Both the empty case and the confirmation should offer the non-destructive alternative.");
    }

    [Fact]
    public void SingleVariantDelete_NamesTheVariantAndDeletesOnlyIt()
    {
        var source = ReadSource("MainWindow.LayoutEditorDelete.partial.cs");
        var body = HandlerBody(source, "private void OnDeleteVariantButtonClick");

        Assert.Contains("ActiveVariantDisplayName", body);
        Assert.Contains("TryDeleteActiveVariant()", body);

        // TryDelete is the bulk path. If it ever appears here, this button silently grew from
        // deleting one layout to deleting all of them, which is the original defect.
        Assert.DoesNotContain("_layoutEditor.TryDelete()", body);
    }

    [Fact]
    public void OnlyTheBulkHandlerCanReachTryDelete()
    {
        // Every editor partial, not just the one that holds it today: the point is that no second
        // caller appears anywhere, including in a file that does not exist yet.
        var occurrences = Directory
            .GetFiles(RepoRoot, "MainWindow*.cs")
            .Sum(f => File.ReadAllText(f)
                .Split(new[] { "_layoutEditor.TryDelete()" }, StringSplitOptions.None).Length - 1);

        Assert.Equal(1, occurrences);
    }

    [Fact]
    public void ButtonLabels_SayWhatEachActionDestroys()
    {
        var xaml = ReadSource("MainWindow.xaml");

        // "Delete and Recalculate" read as a benign recalculation while being the most
        // destructive action in the panel.
        Assert.DoesNotContain("Content=\"Delete and Recalculate\"", xaml);

        Assert.Contains("Content=\"Delete ALL Saved Layouts\"", xaml);
        Assert.Contains("Content=\"Delete This Layout\"", xaml);
        Assert.Contains("Content=\"Unload and Recalculate\"", xaml);
    }

    [Fact]
    public void DeletingOneVariant_LeavesTheOthersAndTheGroup()
    {
        var (ctrl, manager, _, _) = Make();
        ctrl.BeginEditSession(SessionFor("key-many"));

        Assert.True(ctrl.TrySave(OneExtension()));                       // manual-default
        Assert.True(ctrl.TrySaveAsVariant("Keep Me", OneExtension()));
        Assert.True(ctrl.TrySaveAsVariant("Delete Me", OneExtension())); // now active

        var doomedId = ctrl.ActiveVariantId;
        Assert.Equal(3, manager.ListVariants("key-many").Count);

        Assert.True(ctrl.TryDeleteActiveVariant());

        var remaining = manager.ListVariants("key-many");
        Assert.Equal(2, remaining.Count);
        Assert.DoesNotContain(remaining, v => v.VariantId == doomedId);
        Assert.Contains(remaining, v => v.DisplayName == "Keep Me");
        Assert.Contains(remaining, v => v.VariantId == "manual-default");
    }

    [Fact]
    public void PromptsNameVariantsThroughTheSanitiser()
    {
        // manual-layouts.json is documented as hand-editable, so a DisplayName can carry newlines
        // or control characters. Interpolated raw into a delete confirmation, such a name can push
        // the real warning out of view or fake one -- in the dialog that decides whether saved work
        // is destroyed.
        var source = ReadSource("MainWindow.LayoutEditorDelete.partial.cs");

        Assert.Contains("private static string FormatVariantNameForPrompt(", source);

        foreach (var handler in new[]
                 {
                     "private void OnDeleteVariantButtonClick",
                     "private void OnDeleteLayoutButtonClick"
                 })
        {
            var body = HandlerBody(source, handler);
            Assert.Contains("FormatVariantNameForPrompt(", body);
        }
    }

    [Fact]
    public void WhatBulkDeleteDestroys_IsDecidedByTheController()
    {
        // The set the prompt names and the set TryDelete removes have to be the same set. Deciding
        // it in the click handler means two definitions of "deletable" that can drift, and the one
        // the user reads is the one that is not authoritative.
        var source = ReadSource("MainWindow.LayoutEditorDelete.partial.cs");
        var body = HandlerBody(source, "private void OnDeleteLayoutButtonClick");

        Assert.Contains("GetDeletableVariants()", body);
        Assert.DoesNotContain("ManualLayoutOrigin.Manual", body);
    }

    [Fact]
    public void BulkDelete_TakesEveryManualVariantButLeavesSeeds()
    {
        // What the confirmation has to be honest about: this is not "the layout", it is all of
        // them. Generated seeds are not the user's work and survive, which is why the count the
        // prompt shows is of Manual variants only.
        var (ctrl, manager, _, _) = Make();
        manager.SaveVariant(
            "key-bulk", "seed-default", "Generated Seed", ManualLayoutOrigin.AutoSeed,
            OneExtension(), null, setAsDefault: false, setAsSelected: false);

        ctrl.BeginEditSession(SessionFor("key-bulk"));
        Assert.True(ctrl.TrySave(OneExtension()));
        Assert.True(ctrl.TrySaveAsVariant("Second", OneExtension()));

        var manualCount = ctrl.GetVariants().Count(v => v.Origin == ManualLayoutOrigin.Manual);
        Assert.Equal(2, manualCount);

        Assert.True(ctrl.TryDelete());

        var remaining = manager.ListVariants("key-bulk");
        Assert.DoesNotContain(remaining, v => v.Origin == ManualLayoutOrigin.Manual);
        Assert.Contains(remaining, v => v.Origin == ManualLayoutOrigin.AutoSeed);
    }
}
