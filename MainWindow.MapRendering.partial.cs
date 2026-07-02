using System.Windows.Media;
using InteractiveWorldMap.Models;
using InteractiveWorldMap.Utilities;

namespace InteractiveWorldMap;

public partial class MainWindow
{
    private ZoomedRegionRenderRequest? TryCreateZoomedRegionRenderRequest(
        ViewportState viewport, double centerX, double centerY)
    {
        var dpi = VisualTreeHelper.GetDpi(MapDisplay);
        if (!PhysicalPixelSizeCalculator.TryCalculate(
                MapDisplay.ActualWidth, MapDisplay.ActualHeight,
                dpi.DpiScaleX, dpi.DpiScaleY, out var width, out var height))
        {
            _logger.LogWarning("Skipping settled zoomed-map render because display dimensions are invalid.");
            return null;
        }
        return new(centerX, centerY, ZoomScale, width, height,
            dpi.DpiScaleX, dpi.DpiScaleY,
            _visualConfig.ZoomedMapRendering.ResamplingMode, viewport.GetSourceRect());
    }
}
