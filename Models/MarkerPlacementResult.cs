using System.Collections.Generic;

namespace InteractiveWorldMap.Models
{
    /// <summary>How marker positions were computed for the current viewport update.</summary>
    public enum MarkerPlacementMode
    {
        /// <summary>Zoom animation in progress — normal positions only, no extensions.</summary>
        AnimatingFallback,

        /// <summary>No radial extensions applied (threshold/disabled/no dense groups).</summary>
        NormalOnly,

        /// <summary>Dense groups detected; extensions calculated and adjusted.</summary>
        WithExtensions
    }

    /// <summary>Canvas SetLeft/SetTop for one individual marker (top-left corner).</summary>
    public sealed record MarkerScreenPlacement(string LocationName, double Left, double Top);

    /// <summary>Canvas SetLeft/SetTop for one cluster marker (top-left corner).</summary>
    public sealed record ClusterScreenPlacement(double Left, double Top, double MarkerSize);

    /// <summary>
    /// Pure placement plan produced by <see cref="Services.MarkerPlacementOrchestrator"/>.
    /// MainWindow applies these values to WPF markers and extension lines.
    /// </summary>
    public sealed class MarkerPlacementResult
    {
        public MarkerPlacementMode Mode { get; init; }

        /// <summary>Individual markers positioned normally (animating, no-extension, or outside dense groups).</summary>
        public IReadOnlyList<MarkerScreenPlacement> IndividualPlacements { get; init; }
            = System.Array.Empty<MarkerScreenPlacement>();

        /// <summary>Cluster aggregate markers.</summary>
        public IReadOnlyList<ClusterScreenPlacement> ClusterPlacements { get; init; }
            = System.Array.Empty<ClusterScreenPlacement>();

        /// <summary>Groups with extensions ready for <see cref="Views.IExtensionLineRenderer.Apply"/>.</summary>
        public IReadOnlyList<DenseMarkerGroup> ExtensionGroups { get; init; }
            = System.Array.Empty<DenseMarkerGroup>();

        /// <summary>Whether radial extension threshold and config allowed extension logic.</summary>
        public bool ShouldApplyExtensions { get; init; }

        public MarkerPlacementResult() { }

        public MarkerPlacementResult(
            MarkerPlacementMode mode,
            IReadOnlyList<MarkerScreenPlacement> individualPlacements,
            IReadOnlyList<ClusterScreenPlacement> clusterPlacements,
            IReadOnlyList<DenseMarkerGroup> extensionGroups,
            bool shouldApplyExtensions)
        {
            Mode = mode;
            IndividualPlacements = individualPlacements;
            ClusterPlacements = clusterPlacements;
            ExtensionGroups = extensionGroups;
            ShouldApplyExtensions = shouldApplyExtensions;
        }
    }
}
