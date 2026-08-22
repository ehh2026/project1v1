using System;
using System.IO;
using Xunit;

namespace InteractiveWorldMap.Tests;

/// <summary>
/// "Auto Assign Pins" re-picks an image shaft/head pair for each marker. That only means something
/// when composite pin rendering is on; with drawn pins there is no pair to pick.
///
/// It was not merely useless in drawn mode. Nothing on the apply path checks the rendering mode, so
/// a click replaced drawn pins with composite ones for the markers it touched, leaving the map in a
/// state the config does not describe.
///
/// MainWindow cannot be instantiated under test (WPF), so the wiring is pinned against source.
/// </summary>
public class AutoAssignPinsAvailabilityTests
{
    private static readonly string RepoRoot =
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));

    private static string ReadSource(string fileName) =>
        File.ReadAllText(Path.Combine(RepoRoot, fileName));

    private static string HandlerBody(string source, string handler)
    {
        var start = source.IndexOf(handler, StringComparison.Ordinal);
        Assert.True(start >= 0, $"{handler} not found.");

        var end = source.IndexOf("        private ", start + handler.Length, StringComparison.Ordinal);
        if (end < 0) end = source.Length;
        return source.Substring(start, end - start);
    }

    [Fact]
    public void Handler_RefusesToRunWhenCompositesAreOff()
    {
        // The disabled button is the UI telling the truth; this is the part that makes it true.
        // Without it, any other caller -- or a stale button state -- silently changes how pins are
        // drawn.
        var body = HandlerBody(
            ReadSource("MainWindow.CompositePins.partial.cs"),
            "private void OnReassignPinsButtonClick");

        var guardIndex = body.IndexOf("!CanUseCompositePins()", StringComparison.Ordinal);
        var applyIndex = body.IndexOf("ApplyCompositePinToMarker(", StringComparison.Ordinal);

        Assert.True(guardIndex >= 0, "The handler must check the rendering mode.");
        Assert.True(applyIndex >= 0, "The handler should still be the one applying composite pins.");
        Assert.True(
            guardIndex < applyIndex,
            "The mode check has to come before anything is applied.");
    }

    [Fact]
    public void ButtonState_FollowsTheRenderingMode()
    {
        var source = ReadSource("MainWindow.CompositePins.partial.cs");

        Assert.Contains("private void UpdateAutoAssignPinsAvailability()", source);

        var body = HandlerBody(source, "private void UpdateAutoAssignPinsAvailability()");
        Assert.Contains("CanUseCompositePins()", body);
        Assert.Contains("ReassignPinsButton.IsEnabled", body);
    }

    [Fact]
    public void ButtonState_IsRefreshedWhenTheModeCanChange()
    {
        // Composite rendering is toggled from the tuning panel, which does not require leaving edit
        // mode. Deciding availability once when the panel opens would leave the button lying for as
        // long as the session lasts.
        Assert.Contains(
            "UpdateAutoAssignPinsAvailability();",
            ReadSource("MainWindow.LayoutEditor.partial.cs"));

        Assert.Contains(
            "UpdateAutoAssignPinsAvailability();",
            ReadSource("MainWindow.DeveloperTuning.partial.cs"));
    }

    [Fact]
    public void DisabledButton_CanStillExplainItself()
    {
        // WPF swallows tooltips on disabled controls unless asked not to, which would leave the
        // button greyed out with no way to find out why.
        var xaml = ReadSource("MainWindow.xaml");

        var buttonIndex = xaml.IndexOf("x:Name=\"ReassignPinsButton\"", StringComparison.Ordinal);
        Assert.True(buttonIndex >= 0, "ReassignPinsButton not found.");

        var declaration = xaml.Substring(buttonIndex, Math.Min(900, xaml.Length - buttonIndex));
        Assert.Contains("ToolTipService.ShowOnDisabled=\"True\"", declaration);
    }
}
