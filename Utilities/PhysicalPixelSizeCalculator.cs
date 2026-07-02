using System;

namespace InteractiveWorldMap.Utilities;

public static class PhysicalPixelSizeCalculator
{
    public static bool TryCalculate(
        double dipWidth, double dipHeight, double dpiScaleX, double dpiScaleY,
        out int pixelWidth, out int pixelHeight)
    {
        pixelWidth = 0;
        pixelHeight = 0;
        if (!double.IsFinite(dipWidth) || !double.IsFinite(dipHeight) ||
            !double.IsFinite(dpiScaleX) || !double.IsFinite(dpiScaleY) ||
            dipWidth <= 0 || dipHeight <= 0 || dpiScaleX <= 0 || dpiScaleY <= 0)
            return false;

        var width = Math.Round(dipWidth * dpiScaleX, MidpointRounding.AwayFromZero);
        var height = Math.Round(dipHeight * dpiScaleY, MidpointRounding.AwayFromZero);
        if (width < 1 || width > int.MaxValue || height < 1 || height > int.MaxValue)
            return false;

        pixelWidth = (int)width;
        pixelHeight = (int)height;
        return true;
    }
}
