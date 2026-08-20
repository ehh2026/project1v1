using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using InteractiveWorldMap.Models;
using InteractiveWorldMap.Services;
using InteractiveWorldMap.Utilities;
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
        /// Why a layout could not be captured for saving.
        /// </summary>
        private enum ExtensionCollectionStatus
        {
            Ok,

            /// <summary>No edit session or no viewport yet.</summary>
            NotReady,

            /// <summary>One or more markers had unresolvable or non-finite geometry.</summary>
            UnusableGeometry,

            /// <summary>Every marker sat on its anchor — the layout had collapsed to stubs.</summary>
            CollapsedLayout,

            /// <summary>The view changed under the editor, so captured endpoints are stale.</summary>
            GeometryStale
        }

        /// <summary>
        /// Captures current marker positions as extensions, refusing when the result would corrupt
        /// a saved layout.
        /// </summary>
        /// <remarks>
        /// Every save path goes through here. An earlier version of this guard lived only in the
        /// Save button's handler while "Save As" used a second, unguarded copy of the same
        /// collection logic — which is exactly how a corrupting save still got through. Keep this
        /// the single route; do not inline marker collection into a handler again.
        /// </remarks>
        private ExtensionCollectionStatus TryCollectCurrentExtensions(
            out List<RadialExtension> extensions,
            out IReadOnlyList<string> blockedMarkers)
        {
            extensions = new List<RadialExtension>();
            blockedMarkers = Array.Empty<string>();

            // Scope comes from the session captured on entry, not from ambient state that
            // navigation also writes. There is no "wrong layout" case left to check: a session
            // cannot point somewhere other than where the edit began.
            var session = _layoutEditor.ActiveSession;
            if (session == null) return ExtensionCollectionStatus.NotReady;

            var viewport = MapDisplay.CurrentViewport;
            if (viewport == null) return ExtensionCollectionStatus.NotReady;

            // Staleness is derived, not flagged: if the live view no longer matches the one the
            // session captured, the markers on screen are in a different coordinate space and
            // saving would mix them with freshly projected anchors.
            if (!session.MatchesView(viewport, MapDisplay.ActualWidth, MapDisplay.ActualHeight))
                return ExtensionCollectionStatus.GeometryStale;

            // Explicit types, not var: MapDisplay is XAML-generated, so the formatting analyzer
            // cannot resolve it and infers these as unknown, which poisons the tuple element type.
            double cw = MapDisplay.ActualWidth;
            double ch = MapDisplay.ActualHeight;

            var unresolved = new List<string>();
            var markerData = new List<(Location Location, Point MarkerCenter, Point OriginalScreen)>();
            foreach (var marker in _individualMarkers.Where(m => m.Visibility == Visibility.Visible))
            {
                if (!TryGetMarkerEndpoint(marker, out Point center))
                    unresolved.Add(marker.Location?.Name ?? "(unnamed)");

                Point original = viewport.SourceToScreen(
                    marker.Location.PixelX, marker.Location.PixelY, cw, ch);
                markerData.Add((marker.Location, center, original));
            }

            var nonFinite = LayoutEditorController.FindNonFiniteMarkers(markerData);
            if (unresolved.Count > 0 || nonFinite.Count > 0)
            {
                blockedMarkers = unresolved.Concat(nonFinite).Distinct().ToList();
                return ExtensionCollectionStatus.UnusableGeometry;
            }

            // Backstop for an endpoint that resolved but resolved to the anchor. It judges only
            // markers the placement rules would extend, since a sparse view with no dense group is
            // legitimately all default stubs and must stay saveable (smoke S8).
            //
            // It used to stand down once the user had dragged every marker it would judge, on the
            // grounds that they might have meant it. That case — arranging a dense cluster by
            // putting every head back on its own location marker — was judged not a real use case
            // (smoke S10, dropped 2026-08-20), so the exception and its per-marker drag tracking
            // are gone. An all-anchor dense cluster is now always refused.
            //
            // Lost renderer state is caught by the unresolved-endpoint check above, which does not
            // depend on the final coordinates at all.
            if (LayoutEditorController.IsCollapsedLayout(
                    markerData, _layoutEditor.FindExpectedExtendedLocations(markerData)))
            {
                return ExtensionCollectionStatus.CollapsedLayout;
            }

            extensions = LayoutEditorController.BuildExtensions(markerData);

            // Persist the extended position in source-image space so the layout re-projects to the
            // correct map position at any window size (size-independent persistence; see Phase 5c).
            foreach (var ext in extensions)
            {
                var src = viewport.ScreenToSource(ext.ExtendedPosition.X, ext.ExtendedPosition.Y, cw, ch);
                ext.SourceExtendedX = src.X;
                ext.SourceExtendedY = src.Y;
            }

            return ExtensionCollectionStatus.Ok;
        }

        /// <summary>
        /// Reports a failed capture on the edit-mode status line and in the log. Returns false
        /// always, so callers can `if (!ReportCollectionFailure(...)) return;`.
        /// </summary>
        private bool ReportCollectionFailure(
            ExtensionCollectionStatus status, IReadOnlyList<string> blockedMarkers)
        {
            switch (status)
            {
                case ExtensionCollectionStatus.UnusableGeometry:
                    _logger.LogError(
                        $"Refusing to save: {blockedMarkers.Count} marker(s) have unusable geometry — " +
                        string.Join(", ", blockedMarkers.Take(10)));
                    EditModeStatusText.Text = "✗ SAVE ABORTED — GEOMETRY UNAVAILABLE, RETRY";
                    EditModeStatusText.Foreground = new SolidColorBrush(Colors.Red);
                    break;

                case ExtensionCollectionStatus.GeometryStale:
                    _logger.LogError(
                        "Refusing to save: the view changed since editing began, so marker " +
                        "positions no longer match it. Re-enter edit mode to re-place them.");
                    EditModeStatusText.Text = "✗ SAVE ABORTED — VIEW CHANGED, RE-ENTER EDIT MODE";
                    EditModeStatusText.Foreground = new SolidColorBrush(Colors.Red);
                    break;

                case ExtensionCollectionStatus.CollapsedLayout:
                    _logger.LogError(
                        "Refusing to save: every marker sits on its anchor, so the layout has " +
                        "collapsed to stubs. The saved layout on disk is left untouched.");
                    EditModeStatusText.Text = "✗ SAVE ABORTED — LAYOUT COLLAPSED, RETRY";
                    EditModeStatusText.Foreground = new SolidColorBrush(Colors.Red);
                    break;

                default:
                    _logger.LogWarning("Cannot save layout - no layout key, session, or viewport");
                    EditModeStatusText.Text = "✗ SAVE ABORTED — NOT READY";
                    EditModeStatusText.Foreground = new SolidColorBrush(Colors.Red);
                    break;
            }

            return false;
        }

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
