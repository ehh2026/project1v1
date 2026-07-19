using System.Windows;
using InteractiveWorldMap.Utilities;
using Xunit;

namespace InteractiveWorldMap.Tests;

public class ManualLayoutPlacementPolicyTests
{
    [Fact]
    public void RequiresExtensionLine_ZeroLengthSeed_ReturnsFalse()
    {
        var location = new Point(640, 440);

        Assert.False(ManualLayoutPlacementPolicy.RequiresExtensionLine(location, location));
    }

    [Fact]
    public void RequiresExtensionLine_ProjectedHeadBeyondThreshold_ReturnsTrue()
    {
        Assert.True(ManualLayoutPlacementPolicy.RequiresExtensionLine(
            new Point(640, 440),
            new Point(640, 464)));
    }
}
