using System.Linq;
using System.Windows;
using System.Windows.Media;
using InteractiveWorldMap.Models;
using InteractiveWorldMap.Services;

namespace InteractiveWorldMap.Tests;

public class CompositePinDepthSorterTests
{
    [Fact]
    public void IsInterior_ReturnsTrue_WhenTipIsBehindAlongSharedShaftDirection()
    {
        var lowerTip = new Point(100, 140);
        var upperTip = new Point(100, 100);
        var upward = new Vector(0, -1);

        var isInterior = CompositePinDepthSorter.IsInterior(lowerTip, upward, upperTip, upward);

        Assert.True(isInterior);
    }

    [Fact]
    public void IsInterior_ReturnsFalse_WhenShaftsPointInOppositeDirections()
    {
        var tipA = new Point(100, 140);
        var tipB = new Point(100, 100);

        var isInterior = CompositePinDepthSorter.IsInterior(tipA, new Vector(0, -1), tipB, new Vector(0, 1));

        Assert.False(isInterior);
    }

    [Fact]
    public void Sort_ReturnsBackgroundToForegroundOrder_ForInteriorPins()
    {
        var upper = Item("upper", 100, 100, 0, -1);
        var lower = Item("lower", 100, 140, 0, -1);

        var sorted = new CompositePinDepthSorter().Sort(new[] { lower, upper });

        Assert.Equal(new[] { "upper", "lower" }, sorted.Select(item => item.MarkerId));
    }

    [Fact]
    public void Sort_PreservesInputOrder_WhenPinsHaveNoInteriorRelationship()
    {
        var left = Item("left", 70, 100, -1, 0);
        var right = Item("right", 130, 100, 1, 0);

        var sorted = new CompositePinDepthSorter().Sort(new[] { left, right });

        Assert.Equal(new[] { "left", "right" }, sorted.Select(item => item.MarkerId));
    }

    private static CompositePinDepthItem Item(string markerId, double tipX, double tipY, double dirX, double dirY)
    {
        return new CompositePinDepthItem(markerId, new Point(tipX, tipY), new Vector(dirX, dirY));
    }
}
