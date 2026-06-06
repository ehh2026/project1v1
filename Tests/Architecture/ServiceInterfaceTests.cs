using InteractiveWorldMap.Services;
using Xunit;

namespace InteractiveWorldMap.Tests.Architecture;

public class ServiceInterfaceTests
{
    [Fact]
    public void ContentLoader_ImplementsIContentLoader()
    {
        Assert.True(typeof(IContentLoader).IsAssignableFrom(typeof(ContentLoader)));
    }

    [Fact]
    public void ManualLayoutManager_ImplementsIManualLayoutManager()
    {
        Assert.True(typeof(IManualLayoutManager).IsAssignableFrom(typeof(ManualLayoutManager)));
    }
}
