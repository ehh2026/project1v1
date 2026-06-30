using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace InteractiveWorldMap.Tools.ThumbnailTouchSmoke;

public sealed class SyntheticTouchDevice : TouchDevice
{
    private const double ContactSize = 8;

    private Point _position;
    private TouchAction _action;

    public SyntheticTouchDevice(int id, PresentationSource source)
        : base(id)
    {
        SetActiveSource(source);
    }

    public async Task InjectAsync(
        IReadOnlyList<TouchContactFrame> frames,
        int frameDelayMilliseconds = 16)
    {
        if (frames.Count == 0)
        {
            throw new ArgumentException(
                "At least one touch frame is required.",
                nameof(frames));
        }

        foreach (var frame in frames)
        {
            _position = new Point(frame.X, frame.Y);
            _action = frame.Phase switch
            {
                TouchContactPhase.Down => TouchAction.Down,
                TouchContactPhase.Move => TouchAction.Move,
                TouchContactPhase.Up => TouchAction.Up,
                _ => throw new ArgumentOutOfRangeException(nameof(frame))
            };

            switch (frame.Phase)
            {
                case TouchContactPhase.Down:
                    Activate();
                    ReportDown();
                    break;
                case TouchContactPhase.Move:
                    ReportMove();
                    break;
                case TouchContactPhase.Up:
                    ReportUp();
                    Deactivate();
                    break;
            }

            if (frame.Phase != TouchContactPhase.Up)
            {
                await Task.Delay(frameDelayMilliseconds);
            }
        }
    }

    public override TouchPoint GetTouchPoint(IInputElement relativeTo)
    {
        var position = GetPosition(relativeTo);
        var bounds = new Rect(
            position.X - (ContactSize / 2),
            position.Y - (ContactSize / 2),
            ContactSize,
            ContactSize);
        return new TouchPoint(this, position, bounds, _action);
    }

    public override TouchPointCollection GetIntermediateTouchPoints(
        IInputElement relativeTo)
    {
        var points = new TouchPointCollection
        {
            GetTouchPoint(relativeTo)
        };
        return points;
    }

    private Point GetPosition(IInputElement relativeTo)
    {
        if (relativeTo is null)
        {
            return _position;
        }

        if (ActiveSource.RootVisual is not UIElement root)
        {
            throw new InvalidOperationException(
                "The active presentation source has no UIElement root.");
        }

        if (relativeTo is not UIElement target)
        {
            throw new InvalidOperationException(
                "Synthetic touch targets must be UIElement instances.");
        }

        return root.TranslatePoint(_position, target);
    }
}
