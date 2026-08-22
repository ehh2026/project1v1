using Xunit;

using static InteractiveWorldMap.Tests.TestHelpers.SourceGuard;

namespace InteractiveWorldMap.Tests;

/// <summary>
/// "Auto Assign Pins" re-picks an image shaft and head for each pin. That only means something when
/// composite pin rendering is on; with drawn pins there is no pair to pick.
///
/// It was not merely useless in drawn mode. Nothing on the apply path checks the rendering mode, so
/// a click replaced drawn pins with composite ones for the pins it touched, leaving the map in a
/// state the config does not describe.
///
/// MainWindow cannot be instantiated under test (WPF), so the wiring is pinned against source.
/// </summary>
public class AutoAssignPinsAvailabilityTests
{
    [Fact]
    public void Handler_RefusesToRunWhenCompositesAreOff()
    {
        // The disabled button is the UI telling the truth; this is the part that makes it true.
        // Without it, any other caller -- or a stale button state -- silently changes how pins are
        // drawn.
        var body = MemberBody(
            Read("MainWindow.CompositePins.partial.cs"),
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
        var source = Read("MainWindow.CompositePins.partial.cs");

        Assert.Contains("private void UpdateAutoAssignPinsAvailability()", source);

        var body = MemberBody(source, "private void UpdateAutoAssignPinsAvailability()");
        Assert.Contains("CanUseCompositePins()", body);
        Assert.Contains("ReassignPinsButton.IsEnabled", body);
    }

    [Fact]
    public void ButtonState_IsRefreshedFromBothPlacesTheModeIsDecided()
    {
        // Entering edit mode is what makes this correct today: CanRunTuningAction refuses to apply
        // tuning while edit mode is active, so the mode cannot change while the button is visible.
        // The tuning-side call is deliberate redundancy -- the button should not be relying on that
        // guard staying in place, and if it is ever relaxed the enablement is already right.
        Assert.Contains(
            "UpdateAutoAssignPinsAvailability();",
            Read("MainWindow.LayoutEditor.partial.cs"));

        Assert.Contains(
            "UpdateAutoAssignPinsAvailability();",
            Read("MainWindow.DeveloperTuning.partial.cs"));
    }

    [Fact]
    public void DisabledButton_CanStillExplainItself()
    {
        // WPF swallows tooltips on disabled controls unless asked not to, which would leave the
        // button greyed out with no way to find out why.
        var xaml = Read("MainWindow.xaml");

        var buttonIndex = xaml.IndexOf("x:Name=\"ReassignPinsButton\"", StringComparison.Ordinal);
        Assert.True(buttonIndex >= 0, "ReassignPinsButton not found.");

        var declaration = xaml.Substring(buttonIndex, Math.Min(900, xaml.Length - buttonIndex));
        Assert.Contains("ToolTipService.ShowOnDisabled=\"True\"", declaration);
    }
}
