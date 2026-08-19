using System;
using InteractiveWorldMap.Services;

namespace InteractiveWorldMap
{
    /// <summary>
    /// Layout-key derivation for the manual layout editor.
    /// </summary>
    /// <remarks>
    /// <c>LayoutEditorController.CurrentLayoutKey</c> is written from several places — zoom
    /// animations, full-map probes, cluster navigation — so any operation that loads or writes a
    /// layout derives the key it expects from the view currently on screen rather than trusting
    /// whatever the field happens to hold. A stale key silently writes one scope's geometry into
    /// another scope's layout.
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

        private bool TrySetFullMapLayoutKey(bool editSession)
        {
            if (!IsFullMapRootView())
                return false;

            _isFullMapLayoutSession = editSession;
            _layoutEditor.SetLayoutKey(GenerateCurrentFullMapGroupKey());
            return true;
        }

        private void ClearFullMapLayoutSession()
        {
            _isFullMapLayoutSession = false;
        }

        /// <summary>
        /// Derives the layout key the view currently on screen should be using, independent of
        /// whatever <c>CurrentLayoutKey</c> holds. Returns null when the viewport is not ready.
        /// </summary>
        private string? DeriveCurrentViewLayoutKey()
        {
            var viewport = MapDisplay.CurrentViewport;
            if (viewport == null)
                return null;

            return LayoutKeyGenerator.DeriveEditSessionKey(
                _currentZoomedCluster?.Locations,
                viewport,
                _visualConfig.RadialExtension);
        }

        /// <summary>
        /// True when <c>CurrentLayoutKey</c> still matches the view on screen. Destructive
        /// operations verify this immediately before writing rather than trusting the field.
        /// </summary>
        private bool CurrentLayoutKeyMatchesView()
        {
            var expected = DeriveCurrentViewLayoutKey();
            if (expected == null)
                return false;

            return string.Equals(expected, _layoutEditor.CurrentLayoutKey, StringComparison.Ordinal);
        }
    }
}
