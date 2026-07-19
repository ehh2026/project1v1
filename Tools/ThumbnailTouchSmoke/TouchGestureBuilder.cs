namespace InteractiveWorldMap.Tools.ThumbnailTouchSmoke;

public enum TouchContactPhase
{
    Down,
    Move,
    Up
}

public readonly record struct TouchContactFrame(
    int X,
    int Y,
    TouchContactPhase Phase);

public static class TouchGestureBuilder
{
    public static IReadOnlyList<TouchContactFrame> BuildTap(int x, int y) =>
        new[]
        {
            Frame(x, y, TouchContactPhase.Down),
            Frame(x, y, TouchContactPhase.Up)
        };

    public static IReadOnlyList<TouchContactFrame> BuildSwipe(
        int startX,
        int startY,
        int endX,
        int endY,
        int moveCount)
    {
        if (moveCount < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(moveCount));
        }

        var frames = new List<TouchContactFrame>(moveCount + 2)
        {
            Frame(startX, startY, TouchContactPhase.Down)
        };

        for (var index = 1; index <= moveCount; index++)
        {
            var progress = index / (double)moveCount;
            frames.Add(Frame(
                (int)Math.Round(startX + ((endX - startX) * progress)),
                (int)Math.Round(startY + ((endY - startY) * progress)),
                TouchContactPhase.Move));
        }

        frames.Add(Frame(endX, endY, TouchContactPhase.Up));
        return frames;
    }

    private static TouchContactFrame Frame(
        int x,
        int y,
        TouchContactPhase phase) =>
        new(x, y, phase);
}
