using System.IO;
using Xunit;

namespace InteractiveWorldMap.Tests;

/// <summary>
/// Source-guard tests for Task 1 of the tuning-and-pin-render-bugfixes plan:
/// H12 (composite toggle desync) and H1 (recreate while zoomed).
/// All tests read file text — no WPF instantiation needed.
/// </summary>
public class TuningReapplyTests
{
    private static string RepoRoot =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));

    private static string TuningSource =>
        File.ReadAllText(Path.Combine(RepoRoot, "MainWindow.DeveloperTuning.partial.cs"));

    // ── H12: non-recreate branch uses the replay helper, not a bare placement call ──

    [Fact]
    public void ApplyTuningAsync_NonRecreateBranch_CallsReapplyHelper()
    {
        var source = TuningSource;

        // The helper must be called in the else (non-recreate) branch.
        Assert.Contains("ReapplyViewAfterTuningChange()", source);
    }

    [Fact]
    public void ApplyTuningAsync_NonRecreateBranch_DoesNotCallBareUpdateMarkerPositions()
    {
        var source = TuningSource;

        // There must be no bare UpdateMarkerPositions() in ApplyTuningAsync.
        // The helper itself calls UpdateMarkerPositions internally — that's fine.
        // We check that the call site (inside ApplyTuningAsync) went through the helper.
        // Strategy: verify ReapplyViewAfterTuningChange appears after "else" and that
        // UpdateMarkerPositions is NOT a direct statement inside the same else block.
        var elseIdx = source.IndexOf("else\n                {", System.StringComparison.Ordinal);
        if (elseIdx < 0)
            elseIdx = source.IndexOf("else\r\n                {", System.StringComparison.Ordinal);

        // The else block ends before the RecreateAllMarkersAsync method definition.
        var recreateMethodIdx = source.IndexOf("private async Task RecreateAllMarkersAsync()", System.StringComparison.Ordinal);
        Assert.True(elseIdx >= 0 && recreateMethodIdx > elseIdx, "Could not locate else block in ApplyTuningAsync.");

        var elseBlock = source.Substring(elseIdx, recreateMethodIdx - elseIdx);
        Assert.Contains("ReapplyViewAfterTuningChange()", elseBlock);
        // Bare direct call is absent; the helper encapsulates UpdateMarkerPositions.
        Assert.DoesNotContain("UpdateMarkerPositions();", elseBlock);
    }

    // ── H12: ReapplyViewAfterTuningChange runs base placement before the layout overlay ──

    [Fact]
    public void ReapplyViewAfterTuningChange_FullMap_CallsUpdateMarkerPositionsThenLayout()
    {
        var source = TuningSource;

        // Method must reference both in the correct order.
        var updateIdx = source.IndexOf("UpdateMarkerPositions();", System.StringComparison.Ordinal);
        var layoutIdx = source.IndexOf("TryApplyFullMapManualLayout();", System.StringComparison.Ordinal);

        Assert.True(updateIdx >= 0, "ReapplyViewAfterTuningChange must call UpdateMarkerPositions().");
        Assert.True(layoutIdx >= 0, "ReapplyViewAfterTuningChange must call TryApplyFullMapManualLayout().");
        Assert.True(updateIdx < layoutIdx, "UpdateMarkerPositions() must precede TryApplyFullMapManualLayout().");
    }

    [Fact]
    public void ReapplyViewAfterTuningChange_DoesNotEarlyReturnIntoApplyManualLayout()
    {
        // The old error was an early return into ApplyManualLayout() that skipped auto-placement
        // for non-layout pins. Confirm the helper does not contain that pattern.
        var methodStart = TuningSource.IndexOf("private void ReapplyViewAfterTuningChange()", System.StringComparison.Ordinal);
        Assert.True(methodStart >= 0, "ReapplyViewAfterTuningChange not found.");

        // Grab the method body (heuristic: up to the next private/protected/public member).
        var nextMember = TuningSource.IndexOf("\n        private ", methodStart + 1, System.StringComparison.Ordinal);
        var methodBody = nextMember > 0
            ? TuningSource.Substring(methodStart, nextMember - methodStart)
            : TuningSource.Substring(methodStart);

        // Must NOT directly call ApplyManualLayout() — the overlay is applied via TryApplyFullMapManualLayout.
        Assert.DoesNotContain("ApplyManualLayout(", methodBody);
    }

    [Fact]
    public void ReapplyViewAfterTuningChange_Zoomed_CallsShowZoomedView()
    {
        var source = TuningSource;
        Assert.Contains("ShowZoomedView(_currentZoomedCluster)", source);
    }

    // ── H1: recreate-class changes while zoomed are rejected before mutating _visualConfig ──

    [Fact]
    public void ApplyTuningAsync_RejectsRecreateWhileZoomed_BeforeConfigMutation()
    {
        var source = TuningSource;

        // Guard must appear before the first _visualConfig mutation.
        var guardIdx = source.IndexOf(
            "if (needsRecreate && _currentZoomedCluster != null)",
            System.StringComparison.Ordinal);
        var firstMutationIdx = source.IndexOf(
            "_visualConfig.PinParts.Enabled = e.UseComposite",
            System.StringComparison.Ordinal);

        Assert.True(guardIdx >= 0, "Zoom guard not found in ApplyTuningAsync.");
        Assert.True(firstMutationIdx > guardIdx, "Zoom guard must precede _visualConfig mutations.");
    }

    [Fact]
    public void ApplyTuningAsync_ZoomGuard_ReturnsCleanWithStatus()
    {
        var source = TuningSource;

        // The guard block must set a user-readable status and return.
        var guardIdx = source.IndexOf(
            "if (needsRecreate && _currentZoomedCluster != null)",
            System.StringComparison.Ordinal);
        Assert.True(guardIdx >= 0);

        // Find the closing brace of the guard block (heuristic: next statement line after the if).
        var guardBlock = source.Substring(guardIdx, 300);
        Assert.Contains("SetStatus(", guardBlock);
        Assert.Contains("return;", guardBlock);
    }
}
