using System;
using System.Windows.Media;

namespace InteractiveWorldMap.Views;

public static class DrawnPinColorPalette
{
    private static readonly Random Random = new();

    private static readonly Color[] Colors =
    {
        Color.FromRgb(229, 57, 53),
        Color.FromRgb(25, 118, 210),
        Color.FromRgb(46, 125, 50),
        Color.FromRgb(245, 124, 0),
        Color.FromRgb(123, 31, 162),
        Color.FromRgb(194, 24, 91),
        Color.FromRgb(0, 151, 167),
        Color.FromRgb(251, 192, 45),
        Color.FromRgb(109, 76, 65),
        Color.FromRgb(0, 105, 92)
    };

    public static Color GetRandom() => Colors[Random.Next(Colors.Length)];
}
