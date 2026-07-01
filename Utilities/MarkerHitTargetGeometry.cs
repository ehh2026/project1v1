using System;
using System.Windows;

namespace InteractiveWorldMap.Utilities;

/// <summary>
/// Pure geometry helpers for marker interaction targets.
/// </summary>
public static class MarkerHitTargetGeometry
{
    public static double EffectiveDiameter(double configured, double visible) =>
        Math.Max(configured, visible);

    public static Point ToCanvasCenter(Point markerTopLeft, Point localCenter) =>
        new(markerTopLeft.X + localCenter.X, markerTopLeft.Y + localCenter.Y);
}
