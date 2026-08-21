using System;
using InteractiveWorldMap.Models;
using InteractiveWorldMap.Services;

namespace InteractiveWorldMap
{
    /// <summary>
    /// Layout-key derivation for the manual layout editor.
    /// </summary>
    /// <remarks>
    /// Layout keys are derived per use and never stored: the editor takes its key from the session
    /// captured on entry, and display and replay resolve theirs locally. There is deliberately no
    /// shared "current key" field — when there was, zoom animations and full-map probes could
    /// change what an in-progress edit would overwrite.
    /// </remarks>
    public partial class MainWindow
    {
        private bool IsFullMapRootView()
        {
            var viewport = MapDisplay.CurrentViewport;
            return _currentZoomedCluster == null &&
                   viewport != null &&
                   viewport.ZoomLevel <= 1.01;
        }

        private bool IsFullMapLayoutSessionActive()
        {
            return _isFullMapLayoutSession && _currentZoomedCluster == null;
        }

        private string GenerateCurrentFullMapGroupKey()
        {
            // Size-independent: marker positions re-project from source space, so the full-map
            // layout is keyed by identity alone and survives window resizes.
            return LayoutKeyGenerator.GenerateFullMapGroupKey();
        }

        /// <summary>
        /// Marks this as a full-map edit session. The key comes from the session built on editor
        /// entry, so nothing is stored here.
        /// </summary>
        private bool TrySetFullMapLayoutKey(bool editSession)
        {
            if (!IsFullMapRootView())
                return false;

            _isFullMapLayoutSession = editSession;
            return true;
        }

        private void ClearFullMapLayoutSession()
        {
            _isFullMapLayoutSession = false;
        }

        /// <summary>
        /// Builds the immutable scope for an edit session from the view currently on screen.
        /// Returns null when the viewport is not ready.
        /// </summary>
        /// <remarks>
        /// Uses the same derivation as <see cref="LayoutKeyGenerator.DeriveEditSessionKey"/>, so a
        /// session's key always matches what the current view would produce. The viewport and
        /// container size are captured too: they define the coordinate space the session's marker
        /// positions live in, which is what makes staleness detectable later.
        /// </remarks>
        private LayoutEditSession? TryBuildEditSession()
        {
            var viewport = MapDisplay.CurrentViewport;
            if (viewport == null)
                return null;

            var locations = _currentZoomedCluster?.Locations;
            var isCluster = locations != null && locations.Count > 0;

            return new LayoutEditSession(
                LayoutKey: LayoutKeyGenerator.DeriveEditSessionKey(
                    locations, viewport, _visualConfig.RadialExtension),
                Scope: isCluster ? LayoutScope.Cluster : LayoutScope.FullMap,
                ScopeLocations: isCluster ? locations! : Array.Empty<Location>(),
                Viewport: viewport,
                ContainerWidth: MapDisplay.ActualWidth,
                ContainerHeight: MapDisplay.ActualHeight);
        }

        /// <summary>
        /// True when the user has deliberately arranged the zoomed view for this cluster, i.e. a
        /// Manual layout exists under the key that view would edit.
        /// </summary>
        /// <remarks>
        /// Used to decide precedence on a single-location zoom. Seeds do not count — only a layout
        /// the user made by hand outranks their full-map arrangement. Side-effect free, so calling
        /// it during navigation cannot disturb editor state.
        /// </remarks>
        private bool HasManualLayoutForZoomedView(LocationCluster cluster)
        {
            var viewport = MapDisplay.CurrentViewport;
            if (viewport == null || cluster?.Locations == null || cluster.Locations.Count == 0)
                return false;

            var zoomedKey = LayoutKeyGenerator.DeriveEditSessionKey(
                cluster.Locations, viewport, _visualConfig.RadialExtension);

            return _layoutEditor.HasManualLayout(zoomedKey);
        }
    }
}
