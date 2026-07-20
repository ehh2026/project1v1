using System;
using System.IO;
using InteractiveWorldMap.Models;

namespace InteractiveWorldMap.Utilities;

public enum ContentSetKind { Production, Demo, Legacy }

public static class ContentSetKindExtensions
{
    public static string ToSuffix(this ContentSetKind kind) => kind switch
    {
        ContentSetKind.Production => "production",
        ContentSetKind.Demo => "demo",
        ContentSetKind.Legacy => "legacy",
        _ => throw new ArgumentOutOfRangeException(nameof(kind))
    };
}

public record ContentSetResolution(string Path, ContentSetKind Kind);

public interface IContentSetResolver
{
    ContentSetResolution ResolveActiveContentSet(string contentRoot);
    bool HasCoordinateSource(string folder);
}

public class ContentSetResolver : IContentSetResolver
{
    public ContentSetResolution ResolveActiveContentSet(string contentRoot)
    {
        var production = Path.Combine(contentRoot, ContentFileNames.ProductionContentFolderName);
        if (HasCoordinateSource(production))
            return new ContentSetResolution(production, ContentSetKind.Production);

        var demo = Path.Combine(contentRoot, ContentFileNames.DemoContentFolderName);
        if (HasCoordinateSource(demo))
            return new ContentSetResolution(demo, ContentSetKind.Demo);

        return new ContentSetResolution(contentRoot, ContentSetKind.Legacy);
    }

    public bool HasCoordinateSource(string folder) =>
        Directory.Exists(folder) &&
        (File.Exists(Path.Combine(folder, ContentFileNames.ExcelCoordinateFileName)) ||
         File.Exists(Path.Combine(folder, ContentFileNames.LocationsJsonFileName)));
}
