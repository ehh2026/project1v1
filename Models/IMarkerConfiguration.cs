namespace InteractiveWorldMap.Models;

/// <summary>
/// Marker sizing values needed by marker views.
/// </summary>
public interface IMarkerConfiguration
{
    double LocationMarkerSize { get; }
    double ClusterMarkerSize { get; }
    double ClusterBadgeSize { get; }
    double ClusterCountFontSize { get; }
}
