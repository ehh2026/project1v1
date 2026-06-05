using InteractiveWorldMap.Models;
using Xunit;

namespace InteractiveWorldMap.Tests.Architecture;

/// <summary>
/// Ensures content filename constants stay aligned across validators and loaders.
/// </summary>
public class ContentFileNamesTests
{
    [Fact]
    public void WorldMapFileName_IsCanonicalExtraLargeVariant()
    {
        Assert.Equal("World Map Extra Large.jpg", ContentFileNames.WorldMapFileName);
    }

    [Fact]
    public void ContentFolderName_MatchesProjectCopyRoot()
    {
        Assert.Equal("Images&Content", ContentFileNames.ContentFolderName);
    }
}
