using System.Windows;
using System.Windows.Media;

namespace InteractiveWorldMap.Models;

public sealed class CompositePinDepthItem
{
    public CompositePinDepthItem(string markerId, Point tipScreen, Vector shaftDirection)
    {
        MarkerId = markerId;
        TipScreen = tipScreen;
        ShaftDirection = shaftDirection;
    }

    public string MarkerId { get; }
    public Point TipScreen { get; }
    public Vector ShaftDirection { get; }
}
