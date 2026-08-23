using System;
using Xunit;

using static InteractiveWorldMap.Tests.TestHelpers.SourceGuard;

namespace InteractiveWorldMap.Tests;

/// <summary>
/// "Unload and Recalculate" reverts a view to automatic placement without touching the saved file.
/// Entering the editor used to count as the layout becoming active again and brought it straight
/// back, so the unload could not survive the next click — the button appeared to work and then
/// undid itself the moment you looked at the view again.
///
/// MainWindow cannot be instantiated under test (WPF), so the wiring is pinned against source.
/// </summary>
public class UnloadPersistenceTests
{
    [Fact]
    public void EnteringTheEditor_DoesNotReapplyAnUnloadedLayout()
    {
        var body = MemberBody(
            Read("MainWindow.LayoutEditor.partial.cs"),
            "private void OnEditLayoutButtonClick");

        var applyIndex = body.IndexOf("ApplyManualLayout(sessionLayout)", StringComparison.Ordinal);
        Assert.True(applyIndex >= 0, "Edit entry should still apply an active layout.");

        // The decision to apply must not treat suppression as a reason to apply.
        var gate = body.Substring(0, applyIndex);
        Assert.DoesNotContain("IsManualLayoutSuppressed", gate);
        Assert.Contains("_layoutEditor.IsManualLayoutActive && sessionLayout != null", gate);
    }

    [Fact]
    public void EnteringTheEditor_StillAdoptsTheVariantIdentity()
    {
        // Not applying is not the same as not loading. The editor has to know which variant this
        // view holds even while suppressed, or saving over an unloaded layout would write a second
        // variant beside it instead of replacing it.
        var body = MemberBody(
            Read("MainWindow.LayoutEditor.partial.cs"),
            "private void OnEditLayoutButtonClick");

        var loadIndex = body.IndexOf("_layoutEditor.LoadForEditSession()", StringComparison.Ordinal);
        var gateIndex = body.IndexOf("_layoutEditor.IsManualLayoutActive &&", StringComparison.Ordinal);

        Assert.True(loadIndex >= 0, "The session's layout must still be loaded.");
        Assert.True(
            loadIndex < gateIndex,
            "Identity is adopted first, unconditionally; only applying it is conditional.");
    }
}
