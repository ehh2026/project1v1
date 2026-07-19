using System;
using System.Windows;

namespace InteractiveWorldMap.Utilities;

public static class ManualLayoutPlacementPolicy
{
    public const double ExtensionLineThreshold = 5.0;

    public static bool RequiresExtensionLine(Point tip, Point head)
    {
        var dx = head.X - tip.X;
        var dy = head.Y - tip.Y;
        return Math.Sqrt((dx * dx) + (dy * dy)) > ExtensionLineThreshold;
    }
}
