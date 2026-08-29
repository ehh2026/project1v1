using InteractiveWorldMap;
using Xunit;

namespace InteractiveWorldMap.Tests;

public class AppUnattendedModeTests
{
    [Theory]
    [InlineData("--unattended")]
    [InlineData("--UNATTENDED")]
    public void IsUnattended_RecognisesTheLauncherFlag(string arg)
    {
        Assert.True(App.IsUnattended(new[] { arg }));
        Assert.True(App.IsUnattended(new[] { "--other", arg }));
    }

    [Fact]
    public void IsUnattended_IsFalseForAnInteractiveLaunch()
    {
        Assert.False(App.IsUnattended(new string[0]));
        Assert.False(App.IsUnattended(new[] { "--something-else" }));
    }

    [Fact]
    public void IsUnattended_ToleratesNoArguments()
    {
        // StartupEventArgs.Args is never null in practice, but a crash handler that throws
        // while handling a startup crash would replace the failure being reported.
        Assert.False(App.IsUnattended(null!));
    }
}
