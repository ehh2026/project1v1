using System;
using System.IO;
using Xunit;

namespace InteractiveWorldMap.Tests;

/// <summary>
/// Source guards for layout-key handling in the MainWindow partials. MainWindow cannot be
/// instantiated under test (WPF), so the behavior itself is covered by
/// <see cref="LayoutKeyGeneratorTests"/> and <see cref="LayoutEditorControllerTests"/>; these
/// tests only pin the wiring that connects them.
/// </summary>
public class LayoutEditorKeyDerivationTests
{
    private static readonly string RepoRoot =
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));

    private static string ReadSource(string fileName) =>
        File.ReadAllText(Path.Combine(RepoRoot, fileName));

    [Fact]
    public void OnEditLayoutButtonClick_DerivesClusterKey_RatherThanInheritingIt()
    {
        var source = ReadSource("MainWindow.LayoutEditor.partial.cs");

        var handlerIndex = source.IndexOf("private void OnEditLayoutButtonClick", StringComparison.Ordinal);
        Assert.True(handlerIndex >= 0, "OnEditLayoutButtonClick not found.");

        var deriveIndex = source.IndexOf(
            "LayoutKeyGenerator.DeriveEditSessionKey", handlerIndex, StringComparison.Ordinal);
        Assert.True(
            deriveIndex >= 0,
            "Entering edit mode while zoomed must re-derive the cluster layout key. Inheriting " +
            "CurrentLayoutKey lets a 'fullmap' key set by the zoom animation survive into a " +
            "cluster edit session, so the save overwrites the full-map layout.");
    }

    [Fact]
    public void OnSaveLayoutButtonClick_VerifiesKeyMatchesViewBeforeWriting()
    {
        var source = ReadSource("MainWindow.LayoutEditor.partial.cs");

        var handlerIndex = source.IndexOf(
            "private async void OnSaveLayoutButtonClick", StringComparison.Ordinal);
        Assert.True(handlerIndex >= 0, "OnSaveLayoutButtonClick not found.");

        var guardIndex = source.IndexOf(
            "CurrentLayoutKeyMatchesView()", handlerIndex, StringComparison.Ordinal);
        Assert.True(
            guardIndex >= 0,
            "The save path must verify the layout key still matches the view on screen before " +
            "writing, so a stale key cannot overwrite another scope's layout.");

        var trySaveIndex = source.IndexOf("_layoutEditor.TrySave(", handlerIndex, StringComparison.Ordinal);
        Assert.True(trySaveIndex >= 0, "TrySave call not found in the save handler.");
        Assert.True(guardIndex < trySaveIndex, "The scope guard must run before TrySave.");
    }

    [Fact]
    public void ZoomAnimation_DoesNotClaimFullMapKeySpeculatively()
    {
        var source = ReadSource("MainWindow.Navigation.partial.cs");

        var methodIndex = source.IndexOf(
            "private ManualLayout? TryLoadFullMapManualLayoutForAnimation", StringComparison.Ordinal);
        Assert.True(methodIndex >= 0, "TryLoadFullMapManualLayoutForAnimation not found.");

        var methodEnd = source.IndexOf(
            "private void ApplyManualLayoutDuringAnimation", methodIndex, StringComparison.Ordinal);
        Assert.True(methodEnd > methodIndex, "Could not bound TryLoadFullMapManualLayoutForAnimation.");

        var body = source.Substring(methodIndex, methodEnd - methodIndex);
        Assert.DoesNotContain("SetLayoutKey", body);
    }
}
