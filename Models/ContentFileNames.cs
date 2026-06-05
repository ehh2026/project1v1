namespace InteractiveWorldMap.Models;

/// <summary>
/// Canonical filenames and folder names under the content root.
/// Single source of truth for StartupValidator, ContentLoader, and tests.
/// </summary>
public static class ContentFileNames
{
    /// <summary>Content root folder name copied next to the executable.</summary>
    public const string ContentFolderName = "Images&Content";

    /// <summary>Primary world map image loaded at runtime.</summary>
    public const string WorldMapFileName = "World Map Extra Large.jpg";

    /// <summary>Cluster marker stamp image.</summary>
    public const string ClusterStampFileName = "stamp_demo.png";

    /// <summary>Location coordinate JSON (optional if Excel is used).</summary>
    public const string LocationsJsonFileName = "locations.json";
}
