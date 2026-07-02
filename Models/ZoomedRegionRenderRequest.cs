using System.Windows;

namespace InteractiveWorldMap.Models;

public sealed record ZoomedRegionRenderRequest(
    double CenterX, double CenterY, double ZoomLevel,
    int PixelWidth, int PixelHeight,
    double DpiScaleX, double DpiScaleY,
    ZoomedMapResamplingMode ResamplingMode,
    Int32Rect HalfResSourceRect);
