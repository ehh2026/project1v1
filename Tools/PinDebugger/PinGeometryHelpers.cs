// Shared JSON geometry helpers for PinDebugger modes.
using System.Drawing;
using System.Text.Json;

internal static class PinGeometryHelpers
{
    internal static PointF ParsePoint(JsonElement el)
        => new PointF(
            (float)el.GetProperty("x").GetDouble(),
            (float)el.GetProperty("y").GetDouble());
}
