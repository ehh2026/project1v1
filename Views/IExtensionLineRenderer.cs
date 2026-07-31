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
        int LineCount { get; }
        int MarkerMappingCount { get; }
        bool HasLine(LocationMarker marker);

        /// <summary>Removes all tracked lines from the canvas and clears all state.</summary>
        void Clear();

        /// <summary>
        /// Renders extension lines for a dense marker group.
        /// For each extension, either applies a composite pin or draws a line.
        /// </summary>
        void Apply(
            DenseMarkerGroup group,
            ViewportState viewport,
            double containerWidth,
            double containerHeight,
            IReadOnlyList<LocationMarker> markers,
            Func<LocationMarker, Point, Point, bool> tryCompositePinApplier);

        /// <summary>Creates and tracks a line for the given marker.</summary>
        void AddLine(LocationMarker marker, Point start, Point end);

        /// <summary>
        /// Moves an existing pin extension-line pair in place (updates endpoints only, reusing the
        /// Line/Brush/Effect objects). Returns false if no pin-line pair is tracked for the marker.
        /// Used on the zoom-animation hot path to avoid clearing and re-creating lines every frame.
        /// </summary>
        bool TryRepositionPinLine(LocationMarker marker, Point start, Point end);

        /// <summary>
        /// Positions an extended marker so its head sits on the extension endpoint.
        /// Manual-layout drawn pins use a head-only visual because the extension line is
        /// their shaft.
        /// </summary>
        void AnchorExtendedMarker(LocationMarker marker, Point extendedScreenPos);

        /// <summary>Replaces the tracked line for a marker with a new one ending at <paramref name="newEndpoint"/>.</summary>
        void MoveLineEndpoint(LocationMarker marker, Point newEndpoint);

        /// <summary>Sets the Z-index of the tracked line for a marker (no-op if no line).</summary>
        void SetLineZIndex(LocationMarker marker, int zIndex);

        /// <summary>Gets the current endpoint of the tracked line for a marker. Returns false if no line.</summary>
        bool TryGetLineEndpoint(LocationMarker marker, out Point endpoint);

        /// <summary>
        /// Gets the current start (map anchor) of the tracked line for a marker. This is the
        /// pin tip end of the extension line. Returns false if no line is tracked.
        /// </summary>
        bool TryGetLineStart(LocationMarker marker, out Point start);

        // Hover event handlers — subscribe directly to marker.MouseEnter/Leave
        void OnMouseEnter(object sender, MouseEventArgs e);
        void OnMouseLeave(object sender, MouseEventArgs e);
    }
}
