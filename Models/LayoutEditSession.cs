using System.Collections.Generic;

namespace InteractiveWorldMap.Models
{
    /// <summary>
    /// Which view a manual layout belongs to.
    /// </summary>
    public enum LayoutScope
    {
        /// <summary>The unzoomed whole-map view.</summary>
        FullMap,

        /// <summary>A zoomed cluster view, identified by its member locations.</summary>
        Cluster
    }

    /// <summary>
    /// The scope an Edit Layout session is working in, captured once when the editor is entered.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This exists to separate two things that were previously one mutable field. A layout key is
    /// used both as the <em>edit scope</em> — which layout a save writes to — and as the
    /// <em>display key</em> that navigation updates constantly while zooming and panning. Sharing
    /// one field let navigation silently change what an in-progress edit would overwrite, which is
    /// the root of the layout data-loss defects fixed in PR #13.
    /// </para>
    /// <para>
    /// A session is immutable: it records the scope, the key derived from it, and the viewport it
    /// was derived against. Saves read from the session rather than re-deriving or re-checking, so
    /// navigation cannot influence them. The captured viewport also makes staleness a comparison
    /// rather than a flag — if the live viewport no longer matches, the markers on screen are in a
    /// different coordinate space than the session assumed.
    /// </para>
    /// </remarks>
    /// <param name="LayoutKey">The layout group key this session reads and writes.</param>
    /// <param name="Scope">Whether this session edits the whole map or one cluster.</param>
    /// <param name="ScopeLocations">
    /// The cluster's locations; empty for <see cref="LayoutScope.FullMap"/>.
    /// </param>
    /// <param name="Viewport">The viewport the key was derived against, captured at entry.</param>
    /// <param name="ContainerWidth">Marker canvas width at entry.</param>
    /// <param name="ContainerHeight">Marker canvas height at entry.</param>
    public sealed record LayoutEditSession(
        string LayoutKey,
        LayoutScope Scope,
        IReadOnlyList<Location> ScopeLocations,
        ViewportState Viewport,
        double ContainerWidth,
        double ContainerHeight)
    {
        /// <summary>True when this session edits the unzoomed whole-map layout.</summary>
        public bool IsFullMap => Scope == LayoutScope.FullMap;

        /// <summary>
        /// True when the live view still matches the one this session was derived against. When
        /// false, marker positions on screen no longer share the session's coordinate space, so
        /// captured geometry must not be saved.
        /// </summary>
        public bool MatchesView(ViewportState? viewport, double containerWidth, double containerHeight)
        {
            if (viewport == null) return false;

            return ContainerWidth == containerWidth
                && ContainerHeight == containerHeight
                && Viewport.ZoomLevel == viewport.ZoomLevel
                && Viewport.ViewportX == viewport.ViewportX
                && Viewport.ViewportY == viewport.ViewportY
                && Viewport.ViewportWidth == viewport.ViewportWidth
                && Viewport.ViewportHeight == viewport.ViewportHeight;
        }
    }
}
