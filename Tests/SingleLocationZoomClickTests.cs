using System;
using System.IO;
using System.Linq;
using Xunit;

namespace InteractiveWorldMap.Tests;

public class SingleLocationZoomClickTests
{
    private static string RepoRoot =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));

    [Fact]
    public void MainWindow_DeclaresAutoOpenLocationField()
    {
        var source = File.ReadAllText(Path.Combine(RepoRoot, "MainWindow.xaml.cs"));
        Assert.Contains("private Location? _autoOpenLocation", source);
    }

    [Fact]
    public void AnimateZoomOut_ClosesActiveContentPopup()
    {
        // Backing out of a zoomed view must dismiss any open content popup — including the one
        // auto-opened from a single-location unzoomed click — so it does not linger over the map.
        var source = File.ReadAllText(Path.Combine(RepoRoot, "MainWindow.Navigation.partial.cs"));
        var body = ExtractMethodBody(source, "private void AnimateZoomOut()");

        Assert.Contains("CloseActiveSubwindow();", body);
    }

    [Fact]
    public void IndividualMarker_SetsAutoOpenLocation_And_GuardsAgainstDoubleClicks()
    {
        var source = File.ReadAllText(Path.Combine(RepoRoot, "MainWindow.xaml.cs"));
        var methodBody = ExtractMethodBody(source, "private LocationMarker AddIndividualMarker");

        // The handler is registered inline, so we verify its body logic
        Assert.Contains("_autoOpenLocation = location;", methodBody);
        
        // Ensure IsAnimating or InteractionMode.Animating guard exists
        Assert.True(methodBody.Contains("IsAnimating") || methodBody.Contains("InteractionMode.Animating"),
            "Individual marker click handler must guard against double clicks during animation.");
            
        // Verify order: edit mode check first, then animation check
        var editModeCheck = methodBody.IndexOf("MarkerMouseDownAction.AllowEditDrag");
        var animCheck = methodBody.IndexOf("InteractionMode.Animating");
        var clickLogic = methodBody.IndexOf("AnimateMarkerClick");
        
        Assert.True(editModeCheck < animCheck, "Edit mode check must happen before animation state guard.");
        Assert.True(animCheck < clickLogic, "Animation guard must return early before zoom logic runs.");
    }

    [Fact]
    public void IndividualMarker_GatesAutoOpenLocationBehindConfig()
    {
        var source = File.ReadAllText(Path.Combine(RepoRoot, "MainWindow.xaml.cs"));
        var methodBody = ExtractMethodBody(source, "private LocationMarker AddIndividualMarker");

        var gateIdx = methodBody.IndexOf("_visualConfig.AutoOpenSingleLocationContentAfterZoom", StringComparison.Ordinal);
        var setIdx = methodBody.IndexOf("_autoOpenLocation = location;", StringComparison.Ordinal);
        var clusterClickIdx = methodBody.IndexOf("OnClusterClicked(singleCluster);", StringComparison.Ordinal);

        Assert.True(gateIdx >= 0, "Individual full-map pin click must check AutoOpenSingleLocationContentAfterZoom.");
        Assert.True(setIdx > gateIdx, "_autoOpenLocation should only be set after the config gate.");
        Assert.True(clusterClickIdx > setIdx, "The auto-open decision must be made before starting the zoom.");
    }

    [Fact]
    public void ClusterMarker_DoesNotSetAutoOpenLocation()
    {
        var source = File.ReadAllText(Path.Combine(RepoRoot, "MainWindow.xaml.cs"));
        var methodBody = ExtractMethodBody(source, "private void AddClusterMarker");

        Assert.DoesNotContain("_autoOpenLocation", methodBody);
    }

    [Fact]
    public void AnimateZoomToCluster_ClearsAutoOpenLocationOnFailureAndCallback()
    {
        var source = File.ReadAllText(Path.Combine(RepoRoot, "MainWindow.Navigation.partial.cs"));
        var body = ExtractMethodBody(source, "private void AnimateZoomToCluster");

        // Should clear on early return (startViewport == null)
        Assert.Contains("_autoOpenLocation = null;", body);
        
        // Should clear in catch block
        var catchBlockStart = body.IndexOf("catch (Exception");
        Assert.True(catchBlockStart > 0, "Method must have a catch block");
        var catchBody = body.Substring(catchBlockStart);
        Assert.Contains("_autoOpenLocation = null;", catchBody);

        // Completion callback checks
        var transitionCall = body.IndexOf("AnimateViewportTransition(");
        var callbackBody = body.Substring(transitionCall);
        
        // Copy to local and clear synchronously
        Assert.Contains("var toOpen = _autoOpenLocation;", callbackBody);
        Assert.Contains("_autoOpenLocation = null;", callbackBody);
        
        // Order requirement: ShowContentForLocation after ShowZoomedView
        var showZoomedIdx = callbackBody.IndexOf("ShowZoomedView");
        var showContentIdx = callbackBody.IndexOf("ShowContentForLocation");
        
        Assert.True(showZoomedIdx < showContentIdx, 
            "ShowContentForLocation must happen after ShowZoomedView so viewport updates finish.");
    }

    [Fact]
    public void AnimateZoomOut_ClearsAutoOpenLocation()
    {
        var source = File.ReadAllText(Path.Combine(RepoRoot, "MainWindow.Navigation.partial.cs"));
        var body = ExtractMethodBody(source, "private void AnimateZoomOut");

        Assert.Contains("_autoOpenLocation = null;", body);
    }

    private static string ExtractMethodBody(string source, string signature)
    {
        var methodStart = source.IndexOf(signature, StringComparison.Ordinal);
        Assert.True(methodStart >= 0, $"Method not found: {signature}");

        var openBrace = source.IndexOf('{', methodStart);
        Assert.True(openBrace >= 0, $"Opening brace not found for: {signature}");

        var depth = 0;
        for (var i = openBrace; i < source.Length; i++)
        {
            if (source[i] == '{')
                depth++;
            else if (source[i] == '}')
            {
                depth--;
                if (depth == 0)
                    return source.Substring(openBrace, i - openBrace + 1);
            }
        }

        throw new InvalidOperationException($"Closing brace not found for: {signature}");
    }
}
