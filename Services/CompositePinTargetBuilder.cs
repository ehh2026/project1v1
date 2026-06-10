using System;
using System.Windows;
using InteractiveWorldMap.Models;

namespace InteractiveWorldMap.Services;

public sealed class CompositePinTargetBuilder
{
    public PinPlacementTarget Build(
        Location location,
        ViewportState viewport,
        double containerWidth,
        double containerHeight,
        PinPartConfig config,
        RadialExtension? extension = null)
    {
        if (location == null) throw new ArgumentNullException(nameof(location));
        if (viewport == null) throw new ArgumentNullException(nameof(viewport));
        if (config == null) throw new ArgumentNullException(nameof(config));

        if (extension != null)
        {
            return new PinPlacementTarget
            {
                StartScreen = extension.OriginalPosition,
                EndScreen = extension.ExtendedPosition,
                LocationId = location.Name,
                GroupId = extension.GroupId
            };
        }

        var start = viewport.SourceToScreen(
            location.PixelX,
            location.PixelY,
            containerWidth,
            containerHeight);
        var stubLength = Math.Max(0, config.DefaultStubLengthPixels);

        return new PinPlacementTarget
        {
            StartScreen = start,
            EndScreen = new Point(start.X, start.Y - stubLength),
            LocationId = location.Name,
            GroupId = 0
        };
    }
}
