using System.Windows;
using InteractiveWorldMap.Models;
using Xunit;

namespace InteractiveWorldMap.Tests;

/// <summary>
/// Regression tests for ViewportState coordinate mapping.
/// The primary regression is the "unzoomed marker offset" bug: when CreateFullMapView
/// builds a virtual letterbox viewport (ViewportX or ViewportY negative), SourceToScreen
/// must derive scale from the actual rendered crop, not the virtual viewport dimensions.
/// </summary>
public class ViewportStateTests
{
    // Source image dimensions matching the display map used in production.
    private const double SourceW = 8198;
    private const double SourceH = 5542;

    // -------------------------------------------------------------------------
    // Full-map: wide container (image aspect < container aspect → wide letterbox)
    // Regression case: container 16:9, image ≈ 1.48:1 → ViewportX negative.
    // -------------------------------------------------------------------------

    private static ViewportState FullMapWideContainer() =>
        ViewportState.CreateFullMapView(SourceW, SourceH, containerWidth: 1920, containerHeight: 1080);

    [Fact]
    public void SourceToScreen_FullMapWideContainer_TopLeftMapsToScreenOrigin()
    {
        var vp = FullMapWideContainer();
        var screen = vp.SourceToScreen(0, 0, 1920, 1080);
        Assert.Equal(0.0, screen.X, 1);
        Assert.Equal(0.0, screen.Y, 1);
    }

    [Fact]
    public void SourceToScreen_FullMapWideContainer_BottomRightMapsToScreenCorner()
    {
        var vp = FullMapWideContainer();
        var screen = vp.SourceToScreen(SourceW, SourceH, 1920, 1080);
        Assert.Equal(1920.0, screen.X, 1);
        Assert.Equal(1080.0, screen.Y, 1);
    }

    [Fact]
    public void SourceToScreen_FullMapWideContainer_CenterMapsToScreenCenter()
    {
        var vp = FullMapWideContainer();
        var screen = vp.SourceToScreen(SourceW / 2, SourceH / 2, 1920, 1080);
        Assert.Equal(960.0, screen.X, 1);
        Assert.Equal(540.0, screen.Y, 1);
    }

    [Fact]
    public void ScreenToSource_FullMapWideContainer_ScreenOriginMapsToImageTopLeft()
    {
        var vp = FullMapWideContainer();
        var source = vp.ScreenToSource(0, 0, 1920, 1080);
        Assert.Equal(0.0, source.X, 1);
        Assert.Equal(0.0, source.Y, 1);
    }

    [Fact]
    public void ScreenToSource_FullMapWideContainer_ScreenCornerMapsToImageBottomRight()
    {
        var vp = FullMapWideContainer();
        var source = vp.ScreenToSource(1920, 1080, 1920, 1080);
        // GetSourceRect uses floor/ceiling so allow a 1-pixel tolerance
        Assert.InRange(source.X, SourceW - 2, SourceW + 2);
        Assert.InRange(source.Y, SourceH - 2, SourceH + 2);
    }

    // -------------------------------------------------------------------------
    // Full-map: tall container (image aspect > container aspect → tall letterbox)
    // ViewportY negative here.
    // -------------------------------------------------------------------------

    private static ViewportState FullMapTallContainer() =>
        ViewportState.CreateFullMapView(SourceW, SourceH, containerWidth: 800, containerHeight: 600);

    [Fact]
    public void SourceToScreen_FullMapTallContainer_TopLeftMapsToScreenOrigin()
    {
        var vp = FullMapTallContainer();
        var screen = vp.SourceToScreen(0, 0, 800, 600);
        Assert.Equal(0.0, screen.X, 1);
        Assert.Equal(0.0, screen.Y, 1);
    }

    [Fact]
    public void SourceToScreen_FullMapTallContainer_BottomRightMapsToScreenCorner()
    {
        var vp = FullMapTallContainer();
        var screen = vp.SourceToScreen(SourceW, SourceH, 800, 600);
        Assert.Equal(800.0, screen.X, 1);
        Assert.Equal(600.0, screen.Y, 1);
    }

    [Fact]
    public void SourceToScreen_FullMapTallContainer_CenterMapsToScreenCenter()
    {
        var vp = FullMapTallContainer();
        var screen = vp.SourceToScreen(SourceW / 2, SourceH / 2, 800, 600);
        Assert.Equal(400.0, screen.X, 1);
        Assert.Equal(300.0, screen.Y, 1);
    }

    // -------------------------------------------------------------------------
    // Full-map: square container (may still produce slight letterbox)
    // -------------------------------------------------------------------------

    [Fact]
    public void SourceToScreen_FullMapSquareContainer_CornersMapToScreenCorners()
    {
        var vp = ViewportState.CreateFullMapView(SourceW, SourceH, 1000, 1000);
        var tl = vp.SourceToScreen(0, 0, 1000, 1000);
        var br = vp.SourceToScreen(SourceW, SourceH, 1000, 1000);
        Assert.Equal(0.0, tl.X, 1);
        Assert.Equal(0.0, tl.Y, 1);
        Assert.Equal(1000.0, br.X, 1);
        Assert.Equal(1000.0, br.Y, 1);
    }

    // -------------------------------------------------------------------------
    // Zoomed view: viewport is inside image bounds; SourceToScreen should be
    // unchanged from the old implementation (both formulas are equivalent when
    // ViewportX >= 0 and ViewportWidth <= SourceImageWidth).
    // -------------------------------------------------------------------------

    private static ViewportState ZoomedView() =>
        ViewportState.CreateZoomedView(
            centerX: 4000, centerY: 3000,
            zoomLevel: 55,
            sourceWidth: SourceW, sourceHeight: SourceH,
            containerWidth: 1920, containerHeight: 1080);

    [Fact]
    public void SourceToScreen_ZoomedView_CropStartMapsToScreenOrigin()
    {
        // CroppedBitmap uses the integer-floored GetSourceRect, so the crop's integer
        // top-left — not the fractional ViewportX/Y — is the actual screen origin.
        var vp = ZoomedView();
        var rect = vp.GetSourceRect();
        var screen = vp.SourceToScreen(rect.X, rect.Y, 1920, 1080);
        Assert.Equal(0.0, screen.X, 1);
        Assert.Equal(0.0, screen.Y, 1);
    }

    [Fact]
    public void SourceToScreen_ZoomedView_CropCenterMapsToScreenCenter()
    {
        // The center of the integer crop should map to screen center.
        var vp = ZoomedView();
        var rect = vp.GetSourceRect();
        var cx = rect.X + rect.Width / 2.0;
        var cy = rect.Y + rect.Height / 2.0;
        var screen = vp.SourceToScreen(cx, cy, 1920, 1080);
        Assert.Equal(960.0, screen.X, 1);
        Assert.Equal(540.0, screen.Y, 1);
    }

    // -------------------------------------------------------------------------
    // Round-trip: ScreenToSource(SourceToScreen(p)) == p
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData(0, 0)]
    [InlineData(4099, 2771)]
    [InlineData(8198, 5542)]
    [InlineData(1000, 500)]
    public void RoundTrip_FullMapWideContainer_SourceThenScreenThenSourceIsIdentity(double sx, double sy)
    {
        var vp = FullMapWideContainer();
        var screen = vp.SourceToScreen(sx, sy, 1920, 1080);
        var back   = vp.ScreenToSource(screen.X, screen.Y, 1920, 1080);
        Assert.Equal(sx, back.X, 0); // within 1 source pixel (int rounding in GetSourceRect)
        Assert.Equal(sy, back.Y, 0);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(960, 540)]
    [InlineData(1920, 1080)]
    public void RoundTrip_FullMapWideContainer_ScreenThenSourceThenScreenIsIdentity(double scrX, double scrY)
    {
        var vp = FullMapWideContainer();
        var source = vp.ScreenToSource(scrX, scrY, 1920, 1080);
        var back   = vp.SourceToScreen(source.X, source.Y, 1920, 1080);
        Assert.Equal(scrX, back.X, 0);
        Assert.Equal(scrY, back.Y, 0);
    }
}
