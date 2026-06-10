using InteractiveWorldMap.Models;
using InteractiveWorldMap.Services;
using Xunit;

namespace InteractiveWorldMap.Tests;

public class CompositePinLayoutContentHasherTests
{
    [Fact]
    public void ComputeConfigHash_Changes_WhenShaftAssetVariantChanges()
    {
        var baseConfig = new PinPartConfig
        {
            TargetHeadRadiusPx = 8.0,
            TargetShaftHalfWidthPx = 1.75,
            UseLitShafts = true,
            ShaftAssetVariant = string.Empty
        };
        var variantConfig = new PinPartConfig
        {
            TargetHeadRadiusPx = 8.0,
            TargetShaftHalfWidthPx = 1.75,
            UseLitShafts = true,
            ShaftAssetVariant = "outline_dark"
        };

        Assert.NotEqual(
            CompositePinLayoutContentHasher.ComputeConfigHash(baseConfig),
            CompositePinLayoutContentHasher.ComputeConfigHash(variantConfig));
    }
}
