using System;
using System.IO;
using Xunit;

namespace InteractiveWorldMap.Tests;

public class CompositePinFallbackTests
{
    private static string RepoRoot =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));

    [Fact]
    public void MissingCompositeAssets_FallbackDoesNotUseLegacyImagePath()
    {
        var mainWindowPath = Path.Combine(RepoRoot, "MainWindow.CompositePins.partial.cs");
        var source = File.ReadAllText(mainWindowPath);

        Assert.Contains("Composite pin assets missing", source);
        Assert.DoesNotContain("falling back to legacy", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ImagePinMarker", source);
        Assert.DoesNotContain("PinImages", source);
        Assert.DoesNotContain("pins.jpg", source, StringComparison.OrdinalIgnoreCase);
    }
}
