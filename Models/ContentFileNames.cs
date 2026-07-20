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

    /// <summary>Full-resolution world map image used for high-quality zoomed regions.</summary>
    public const string FullResolutionWorldMapFileName = "World Map 1976.jpg";

    /// <summary>Cluster marker stamp image.</summary>
    public const string ClusterStampFileName = "stamp_demo.png";

    /// <summary>Location coordinate JSON (optional if Excel is used).</summary>
    public const string LocationsJsonFileName = "locations.json";

    public const string AssetsFolderName = "Assets";
    public const string DemoContentFolderName = "Demo-Content";
    public const string ProductionContentFolderName = "Production-Content";
    public const string ExtrasFolderName = "Extras";
    public const string ExcelCoordinateFileName = "Coordinates for map.xlsx";
}
