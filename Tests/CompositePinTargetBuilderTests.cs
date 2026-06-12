using System.Windows;
using InteractiveWorldMap.Models;
using InteractiveWorldMap.Services;

namespace InteractiveWorldMap.Tests;

public class CompositePinTargetBuilderTests
{
    [Fact]
    public void Build_UsesRadialExtensionEndpoints_WhenExtensionIsPresent()
    {
        var location = Location("alpha", 1200, 900);
        var extension = new RadialExtension
        {
            Location = location,
            OriginalPosition = new Point(100, 150),
            ExtendedPosition = new Point(140, 80),
            GroupId = 3
        };

        var target = new CompositePinTargetBuilder().Build(
            location,
            TestViewport(),
            containerWidth: 1000,
            containerHeight: 800,
            new PinPartConfig { DefaultStubLengthPixels = 24 },
            extension);

        Assert.Equal(extension.OriginalPosition, target.StartScreen);
        Assert.Equal(extension.ExtendedPosition, target.EndScreen);
        Assert.Equal("alpha", target.LocationId);
        Assert.Equal(3, target.GroupId);
    }

    [Fact]
    public void Build_UsesScreenUpStub_WhenExtensionIsMissing()
    {
        var location = Location("beta", 1250, 950);

        var target = new CompositePinTargetBuilder().Build(
            location,
            TestViewport(),
            containerWidth: 1000,
            containerHeight: 800,
            new PinPartConfig { DefaultStubLengthPixels = 18 });

        var expectedStart = TestViewport().SourceToScreen(location.PixelX, location.PixelY, 1000, 800);
        Assert.Equal(expectedStart, target.StartScreen);
        Assert.Equal(new Point(expectedStart.X, expectedStart.Y - 18), target.EndScreen);
        Assert.Equal("beta", target.LocationId);
        Assert.Equal(0, target.GroupId);
    }

    [Fact]
    public void Build_StubTarget_IsViewportProjectedButScreenLengthInvariant()
    {
        var location = Location("gamma", 1250, 950);
        var config = new PinPartConfig { DefaultStubLengthPixels = 24 };
        var builder = new CompositePinTargetBuilder();

        var fullMapTarget = builder.Build(
            location,
            TestViewport(),
            containerWidth: 1000,
            containerHeight: 800,
            config);

        var zoomedTarget = builder.Build(
            location,
            new ViewportState
            {
                SourceImageWidth = 8198,
                SourceImageHeight = 5542,
                ViewportX = 1150,
                ViewportY = 900,
                ViewportWidth = 250,
                ViewportHeight = 200,
                ZoomLevel = 8
            },
            containerWidth: 1000,
            containerHeight: 800,
            config);

        Assert.NotEqual(fullMapTarget.StartScreen, zoomedTarget.StartScreen);
        Assert.Equal(0, fullMapTarget.EndScreen.X - fullMapTarget.StartScreen.X, 6);
        Assert.Equal(-24, fullMapTarget.EndScreen.Y - fullMapTarget.StartScreen.Y, 6);
        Assert.Equal(0, zoomedTarget.EndScreen.X - zoomedTarget.StartScreen.X, 6);
        Assert.Equal(-24, zoomedTarget.EndScreen.Y - zoomedTarget.StartScreen.Y, 6);
    }

    private static Location Location(string name, double pixelX, double pixelY) =>
        new() { Id = name, Name = name, PixelX = pixelX, PixelY = pixelY };

    private static ViewportState TestViewport() =>
        new()
        {
            SourceImageWidth = 8198,
            SourceImageHeight = 5542,
            ViewportX = 1000,
            ViewportY = 800,
            ViewportWidth = 500,
            ViewportHeight = 400,
            ZoomLevel = 4
        };
}
