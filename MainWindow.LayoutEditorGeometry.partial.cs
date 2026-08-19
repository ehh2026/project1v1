using System.Windows;
using System.Windows.Controls;
using InteractiveWorldMap.Views;

namespace InteractiveWorldMap
{
    /// <summary>
    /// Marker-endpoint resolution for the manual layout editor.
    /// </summary>
    /// <remarks>
    /// Where a pin actually points is held by the extension-line renderer, not by the marker's own
    /// canvas position. When that record is unavailable the marker's anchor is indistinguishable
    /// from a pin legitimately sitting on its anchor, so resolution reports success separately from
    /// the point itself and callers refuse to persist unresolved geometry.
    /// </remarks>
    public partial class MainWindow
    {
        /// <summary>
        /// Phase 4: returns the endpoint of a marker for layout saving.
        /// Uses extension line endpoint first, then composite pin head center, then marker center fallback.
        /// </summary>
        private Point GetMarkerEndpoint(LocationMarker marker) =>
            TryGetMarkerEndpoint(marker, out var endpoint)
                ? endpoint
                : endpoint; // caller does not distinguish; see TryGetMarkerEndpoint for the guard

        /// <summary>
        /// Resolves a marker's endpoint for layout saving. Returns false when no authoritative
        /// source was available and the returned point is a last-resort guess at the marker's own
        /// anchor.
        /// </summary>
        /// <remarks>
        /// The distinction matters on save: a pin legitimately sitting on its anchor produces the
        /// same coordinates as a pin whose endpoint could not be determined. Persisting the latter
        /// silently flattens the layout, so saves refuse when any endpoint is unresolved rather
        /// than writing a guess.
        /// </remarks>
        private bool TryGetMarkerEndpoint(LocationMarker marker, out Point endpoint)
        {
            if (_extensionLineRenderer.TryGetLineEndpoint(marker, out var lineEnd))
            {
                endpoint = lineEnd;
                return true;
            }

            if (marker.Content is CompositePinMarker cmp && cmp.RenderPlan != null)
            {
                var plan = cmp.RenderPlan;
                endpoint = new Point(
                    Canvas.GetLeft(marker) + plan.HeadCenterLocal.X,
                    Canvas.GetTop(marker) + plan.HeadCenterLocal.Y);
                return true;
            }

            // Drawn roles expose their actual head connection point. Using the configured
            // LocationMarkerSize center would introduce a small saved-angle drift.
            if (marker.Content is ManualLayoutPinMarker manualPin)
            {
                var connection = manualPin.GetConnectionPoint();
                endpoint = new Point(
                    Canvas.GetLeft(marker) + connection.X,
                    Canvas.GetTop(marker) + connection.Y);
                return true;
            }

            if (marker.Content is AutoStubPinMarker autoStub)
            {
                var connection = autoStub.GetConnectionPoint();
                endpoint = new Point(
                    Canvas.GetLeft(marker) + connection.X,
                    Canvas.GetTop(marker) + connection.Y);
                return true;
            }

            // Last resort: the marker's own anchor. Indistinguishable from a pin that genuinely
            // sits on its anchor, so report it as unresolved rather than letting a save persist it.
            var markerSize = _visualConfig.LocationMarkerSize;
            endpoint = new Point(
                Canvas.GetLeft(marker) + markerSize / 2,
                Canvas.GetTop(marker) + markerSize / 2);
            return false;
        }
    }
}
