using InteractiveWorldMap.Views;
using Xunit;

namespace InteractiveWorldMap.Tests;

public class TuningPanelParsingTests
{
    [Fact]
    public void TryReadMapTuning_ValidText_ReturnsParsedValues()
    {
        var ok = DeveloperTuningPanel.TryReadMapTuning(
            "48.5",
            "55",
            "390",
            out var values,
            out var error);

        Assert.True(ok, error);
        Assert.Equal(48.5, values.ClusterThreshold);
        Assert.Equal(55, values.ZoomScale);
        Assert.Equal(390, values.AnimationDurationMs);
        Assert.Equal(string.Empty, error);
    }

    [Fact]
    public void TryReadMapTuning_InvalidAnimationDuration_ReturnsLabelledError()
    {
        var ok = DeveloperTuningPanel.TryReadMapTuning(
            "48.5",
            "55",
            "0",
            out _,
            out var error);

        Assert.False(ok);
        Assert.Contains("Animation duration", error);
    }

    [Fact]
    public void TryReadPinAppearance_InvalidMarkerSize_ReturnsLabelledError()
    {
        var ok = DeveloperTuningPanel.TryReadPinAppearance(
            locationMarkerSizeText: "0",
            clusterMarkerSizeText: "24",
            clusterBadgeSizeText: "12",
            clusterCountFontSizeText: "11",
            stubLengthText: "24",
            targetHeadRadiusText: "8",
            targetShaftHalfWidthText: "3",
            drawnHeadDiameterText: "14",
            drawnShaftWidthText: "3",
            drawnShaftLengthText: "24",
            tipCapWidthText: "12",
            tipCapLineWeightText: "3",
            tipCapArcDepthText: "3",
            out _,
            out var error);

        Assert.False(ok);
        Assert.Contains("Location marker", error);
    }

    [Fact]
    public void TryReadPinAppearance_InvalidLateCapValue_ReturnsLabelledError()
    {
        var ok = DeveloperTuningPanel.TryReadPinAppearance(
            locationMarkerSizeText: "18",
            clusterMarkerSizeText: "24",
            clusterBadgeSizeText: "12",
            clusterCountFontSizeText: "11",
            stubLengthText: "24",
            targetHeadRadiusText: "8",
            targetShaftHalfWidthText: "3",
            drawnHeadDiameterText: "14",
            drawnShaftWidthText: "3",
            drawnShaftLengthText: "24",
            tipCapWidthText: "12",
            tipCapLineWeightText: "3",
            tipCapArcDepthText: "-1",
            out _,
            out var error);

        Assert.False(ok);
        Assert.Contains("Curvature", error);
    }

    [Fact]
    public void TryReadHitboxes_ValidText_ReturnsParsedValues()
    {
        var ok = DeveloperTuningPanel.TryReadHitboxes(
            "32",
            "40",
            out var values,
            out var error);

        Assert.True(ok, error);
        Assert.Equal(32, values.PinHitDiameterPx);
        Assert.Equal(40, values.ClusterHitDiameterPx);
    }

    [Fact]
    public void TryReadHitboxes_InvalidClusterHitbox_ReturnsLabelledError()
    {
        var ok = DeveloperTuningPanel.TryReadHitboxes(
            "32",
            "Infinity",
            out _,
            out var error);

        Assert.False(ok);
        Assert.Contains("Cluster hitbox", error);
    }

    [Fact]
    public void TryReadShadowTuning_InvalidOpacity_ReturnsLabelledError()
    {
        var ok = DeveloperTuningPanel.TryReadShadowTuning(
            "1.01",
            "0.5",
            out _,
            out var error);

        Assert.False(ok);
        Assert.Contains("Pin shadow opacity", error);
    }

    [Fact]
    public void TryReadShadowTuning_InvalidClusterOpacity_ReturnsLabelledError()
    {
        var ok = DeveloperTuningPanel.TryReadShadowTuning(
            "0.5",
            "-0.01",
            out _,
            out var error);

        Assert.False(ok);
        Assert.Contains("Cluster shadow opacity", error);
    }

    [Fact]
    public void TryReadContentWindowTuning_ValidText_ReturnsParsedValues()
    {
        var ok = DeveloperTuningPanel.TryReadContentWindowTuning(
            "Segoe UI",
            "#1E1E1E",
            "0.70",
            "#FFFFFFFF",
            "2",
            "12",
            "#FFFFFFFF",
            "18",
            "14",
            "#000000",
            "0.85",
            "#66FFFFFF",
            "#FFFFFFFF",
            "13",
            out var values,
            out var error);

        Assert.True(ok, error);
        Assert.Equal("Segoe UI", values.ContentFontFamily);
        Assert.Equal("#1E1E1E", values.PopupBackgroundColor);
        Assert.Equal(0.70, values.PopupBackgroundOpacity);
        Assert.Equal(13, values.CaptionFontSize);
    }

    [Fact]
    public void TryReadContentWindowTuning_InvalidLateCaptionColor_ReturnsLabelledError()
    {
        var ok = DeveloperTuningPanel.TryReadContentWindowTuning(
            "Segoe UI",
            "#1E1E1E",
            "0.70",
            "#FFFFFFFF",
            "2",
            "12",
            "#FFFFFFFF",
            "18",
            "14",
            "#000000",
            "0.85",
            "#66FFFFFF",
            "not-a-color",
            "13",
            out _,
            out var error);

        Assert.False(ok);
        Assert.Contains("Caption text color", error);
    }
}
