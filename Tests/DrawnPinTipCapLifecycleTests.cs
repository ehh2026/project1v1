using System;
using System.IO;
using System.Text.RegularExpressions;
using Xunit;

namespace InteractiveWorldMap.Tests;

public class DrawnPinTipCapLifecycleTests
{
    private static string RepoRoot =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));

    [Fact]
    public void UpdatePinTipCaps_RaisesEligibleHeadsAboveCapLayer()
    {
        var tipCapSource = ReadSource("MainWindow.TipCap.partial.cs");
        var rendererSource = ReadSource("Views", "DrawnPinTipCapRenderer.cs");
        var updateBody = ExtractMethodBody(tipCapSource, "private void UpdatePinTipCaps()");

        var headLayer = ReadIntegerConstant(tipCapSource, "DrawnPinHeadZIndex");
        var capLayer = ReadIntegerConstant(rendererSource, "CapZIndex");

        Assert.True(headLayer > capLayer, "Drawn-pin heads must render above tip-cap paths.");
        Assert.Contains("Panel.SetZIndex(marker, DrawnPinHeadZIndex);", updateBody);
    }

    [Fact]
    public void ApplyManualLayout_RefreshesCapsAfterFinalPlacement()
    {
        var source = ReadSource("MainWindow.LayoutEditor.partial.cs");
        var body = ExtractMethodBody(source, "private void ApplyManualLayout");

        var depthSortIndex = body.LastIndexOf("ApplyCompositePinDepthSort();", StringComparison.Ordinal);
        var capRefreshIndex = body.LastIndexOf("UpdatePinTipCaps();", StringComparison.Ordinal);

        Assert.True(depthSortIndex >= 0, "Manual layout must finish composite depth sorting.");
        Assert.True(
            capRefreshIndex > depthSortIndex,
            "Tip caps must refresh after all manual-layout placement and depth changes.");
    }

    [Fact]
    public void DragMove_RefreshesCapsForCompositeAndDrawnBranches()
    {
        var source = ReadSource("MainWindow.LayoutEditorDrag.partial.cs");
        var body = ExtractMethodBody(source, "private void OnMarkerDragMove");

        Assert.True(
            Regex.Matches(body, @"\bUpdatePinTipCaps\(\);").Count >= 2,
            "Both composite and drawn drag branches must refresh or remove stale cap paths.");
    }

    [Fact]
    public void DragEnd_RefreshesCapsAfterTemporaryLayerReset()
    {
        var source = ReadSource("MainWindow.LayoutEditorDrag.partial.cs");
        var body = ExtractMethodBody(source, "private void OnMarkerDragEnd");

        var resetIndex = body.IndexOf("Panel.SetZIndex(_draggedMarker, 0);", StringComparison.Ordinal);
        var capRefreshIndex = body.IndexOf("UpdatePinTipCaps();", StringComparison.Ordinal);

        Assert.True(resetIndex >= 0, "Drag end must restore the marker's temporary drag layer.");
        Assert.True(
            capRefreshIndex > resetIndex,
            "Tip-cap refresh must restore drawn-pin head layering after drag cleanup.");
    }

    private static int ReadIntegerConstant(string source, string name)
    {
        var match = Regex.Match(source, $@"const int {name}\s*=\s*(\d+)");
        Assert.True(match.Success, $"Constant not found: {name}");
        return int.Parse(match.Groups[1].Value);
    }

    private static string ReadSource(params string[] pathParts)
    {
        var parts = new string[pathParts.Length + 1];
        parts[0] = RepoRoot;
        Array.Copy(pathParts, 0, parts, 1, pathParts.Length);
        return File.ReadAllText(Path.Combine(parts));
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
