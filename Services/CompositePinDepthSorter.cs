using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using InteractiveWorldMap.Models;

namespace InteractiveWorldMap.Services;

public sealed class CompositePinDepthSorter
{
    public IReadOnlyList<CompositePinDepthItem> Sort(IEnumerable<CompositePinDepthItem> items)
    {
        if (items == null) throw new ArgumentNullException(nameof(items));

        var orderedInput = items.ToList();
        if (orderedInput.Count < 2)
            return orderedInput;

        var outgoing = orderedInput.ToDictionary(item => item.MarkerId, _ => new HashSet<string>(), StringComparer.Ordinal);
        var incomingCount = orderedInput.ToDictionary(item => item.MarkerId, _ => 0, StringComparer.Ordinal);
        var itemById = orderedInput.ToDictionary(item => item.MarkerId, StringComparer.Ordinal);

        for (var i = 0; i < orderedInput.Count; i++)
        {
            for (var j = 0; j < orderedInput.Count; j++)
            {
                if (i == j)
                    continue;

                var foreground = orderedInput[i];
                var background = orderedInput[j];
                if (!IsInterior(
                        foreground.TipScreen,
                        foreground.ShaftDirection,
                        background.TipScreen,
                        background.ShaftDirection))
                    continue;

                if (outgoing[background.MarkerId].Add(foreground.MarkerId))
                    incomingCount[foreground.MarkerId]++;
            }
        }

        var result = new List<CompositePinDepthItem>(orderedInput.Count);
        var ready = new Queue<CompositePinDepthItem>(
            orderedInput.Where(item => incomingCount[item.MarkerId] == 0));

        while (ready.Count > 0)
        {
            var current = ready.Dequeue();
            result.Add(current);

            foreach (var foregroundId in outgoing[current.MarkerId])
            {
                incomingCount[foregroundId]--;
                if (incomingCount[foregroundId] == 0)
                    ready.Enqueue(itemById[foregroundId]);
            }
        }

        if (result.Count == orderedInput.Count)
            return result;

        var emitted = new HashSet<string>(result.Select(item => item.MarkerId), StringComparer.Ordinal);
        result.AddRange(
            orderedInput
                .Where(item => !emitted.Contains(item.MarkerId))
                .OrderBy(item => item.TipScreen.Y)
                .ThenBy(item => item.MarkerId, StringComparer.Ordinal));

        return result;
    }

    public static bool IsInterior(Point tipA, Vector dirA, Point tipB, Vector dirB)
    {
        var normalizedA = NormalizeOrZero(dirA);
        var normalizedB = NormalizeOrZero(dirB);
        if (normalizedA.LengthSquared == 0 || normalizedB.LengthSquared == 0)
            return false;

        if (Vector.Multiply(normalizedA, normalizedB) <= 0)
            return false;

        var offset = tipA - tipB;
        return Vector.Multiply(offset, normalizedB) < 0;
    }

    private static Vector NormalizeOrZero(Vector vector)
    {
        if (vector.LengthSquared == 0)
            return new Vector();

        vector.Normalize();
        return vector;
    }
}
