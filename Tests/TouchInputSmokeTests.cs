using InteractiveWorldMap.Tools.ThumbnailTouchSmoke;
using System.Windows.Input;
using Xunit;

namespace InteractiveWorldMap.Tests;

public class TouchInputSmokeTests
{
    [Fact]
    public void SyntheticTouchDevice_UsesWpfTouchInputPipeline()
    {
        Assert.True(typeof(SyntheticTouchDevice).IsSubclassOf(typeof(TouchDevice)));
    }

    [Fact]
    public void BuildSwipe_ProducesDownMovesAndUpInScreenPixels()
    {
        var frames = TouchGestureBuilder.BuildSwipe(
            startX: 240,
            startY: 420,
            endX: 240,
            endY: 220,
            moveCount: 4);

        Assert.Equal(6, frames.Count);
        Assert.Equal(TouchContactPhase.Down, frames[0].Phase);
        Assert.All(frames.Skip(1).Take(4), frame =>
            Assert.Equal(TouchContactPhase.Move, frame.Phase));
        Assert.Equal(TouchContactPhase.Up, frames[^1].Phase);
        Assert.Equal(240, frames[0].X);
        Assert.Equal(420, frames[0].Y);
        Assert.Equal(240, frames[^1].X);
        Assert.Equal(220, frames[^1].Y);
        Assert.Equal(frames[^2].X, frames[^1].X);
        Assert.Equal(frames[^2].Y, frames[^1].Y);
    }

    [Fact]
    public void BuildTap_ProducesStationaryDownAndUp()
    {
        var frames = TouchGestureBuilder.BuildTap(125, 310);

        Assert.Collection(
            frames,
            down =>
            {
                Assert.Equal(TouchContactPhase.Down, down.Phase);
                Assert.Equal((125, 310), (down.X, down.Y));
            },
            up =>
            {
                Assert.Equal(TouchContactPhase.Up, up.Phase);
                Assert.Equal((125, 310), (up.X, up.Y));
            });
    }
}
