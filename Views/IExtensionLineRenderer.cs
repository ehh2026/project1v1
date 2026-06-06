using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;
using InteractiveWorldMap.Models;

namespace InteractiveWorldMap.Views
{
    /// <summary>
    /// Manages radial extension lines on the map canvas.
    /// </summary>
    public interface IExtensionLineRenderer
    {
        int  LineCount          { get; }
        int  MarkerMappingCount { get; }
        bool HasLine(LocationMarker marker);

        /// <summary>Removes all tracked lines from the canvas and clears all state.</summary>
        void Clear();

        /// <summary>
        /// Renders extension lines for a dense marker group.
        /// For each extension, either applies a composite pin or draws a line.
        /// </summary>
        void Apply(
            DenseMarkerGroup              group,
            ViewportState                 viewport,
            double                        containerWidth,
            double                        containerHeight,
            IReadOnlyList<LocationMarker> markers,
            Func<LocationMarker, Point, Point, bool> tryCompositePinApplier);

        /// <summary>Creates and tracks a line for the given marker.</summary>
        void AddLine(LocationMarker marker, Point start, Point end);

        /// <summary>Replaces the tracked line for a marker with a new one ending at <paramref name="newEndpoint"/>.</summary>
        void MoveLineEndpoint(LocationMarker marker, Point newEndpoint);

        /// <summary>Sets the Z-index of the tracked line for a marker (no-op if no line).</summary>
        void SetLineZIndex(LocationMarker marker, int zIndex);

        // Hover event handlers — subscribe directly to marker.MouseEnter/Leave
        void OnMouseEnter(object sender, MouseEventArgs e);
        void OnMouseLeave(object sender, MouseEventArgs e);
    }
}
