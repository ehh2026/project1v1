using System;
using System.IO;
using Xunit;

namespace InteractiveWorldMap.Tests;

public class TuningPanelWiringTests
{
    private static string RepoRoot =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));

    [Fact]
    public void DeveloperTuningPanel_CodeBehind_DoesNotReferenceServicesOrUtilities()
    {
        var source = File.ReadAllText(Path.Combine(RepoRoot, "Views", "DeveloperTuningPanel.xaml.cs"));

        Assert.DoesNotContain("InteractiveWorldMap.Services", source);
        Assert.DoesNotContain("InteractiveWorldMap.Utilities", source);
    }

    [Fact]
    public void RecreateAllMarkers_UpdatesClusterThresholdBeforeLoadingClusters()
    {
        var source = File.ReadAllText(Path.Combine(RepoRoot, "MainWindow.DeveloperTuning.partial.cs"));
        var thresholdAssignment = source.IndexOf(
            "_contentLoader.ClusterDistanceThreshold = _visualConfig.ClusterDistanceThreshold",
            StringComparison.Ordinal);
        var loadClusters = source.IndexOf("LoadClustersAsync()", StringComparison.Ordinal);

        Assert.True(thresholdAssignment >= 0, "RecreateAllMarkersAsync must update ContentLoader.ClusterDistanceThreshold.");
        Assert.True(loadClusters >= 0, "RecreateAllMarkersAsync must reload clusters.");
        Assert.True(
            thresholdAssignment < loadClusters,
            "ContentLoader.ClusterDistanceThreshold must be updated before LoadClustersAsync().");
    }

    [Fact]
    public void DeveloperTuningPanel_UsesSingleCompositePinsToggle()
    {
        var xaml = File.ReadAllText(Path.Combine(RepoRoot, "Views", "DeveloperTuningPanel.xaml"));
        var source = File.ReadAllText(Path.Combine(RepoRoot, "Views", "DeveloperTuningPanel.xaml.cs"));

        Assert.DoesNotContain("ChkPinPartsEnabled", xaml);
        Assert.DoesNotContain("Pin parts", xaml);
        Assert.Contains("Content=\"Composite pins\"", xaml);
        Assert.Contains("PinPartsEnabled = ChkComposite.IsChecked == true", source);
        Assert.Contains("UseComposite = ChkComposite.IsChecked == true", source);
        Assert.Contains("config.PinParts.Enabled && config.PinParts.UseCompositeRendering", source);
    }

    [Fact]
    public void DeveloperTuningPanel_HasAutoOpenSingleLocationContentToggle()
    {
        var xaml = File.ReadAllText(Path.Combine(RepoRoot, "Views", "DeveloperTuningPanel.xaml"));
        var source = File.ReadAllText(Path.Combine(RepoRoot, "Views", "DeveloperTuningPanel.xaml.cs"));

        Assert.Contains("x:Name=\"ChkAutoOpenSingleLocationContent\"", xaml);
        Assert.Contains("Content=\"Auto-open single content\"", xaml);
        Assert.Contains("ChkAutoOpenSingleLocationContent.IsChecked = config.AutoOpenSingleLocationContentAfterZoom;", source);
        Assert.Contains("AutoOpenSingleLocationContentAfterZoom = ChkAutoOpenSingleLocationContent.IsChecked == true", source);
    }

    [Fact]
    public void DeveloperTuningPanel_ProvidesTooltipsForTuningOptions()
    {
        var xaml = File.ReadAllText(Path.Combine(RepoRoot, "Views", "DeveloperTuningPanel.xaml"));

        foreach (var controlName in new[]
        {
            "ChkComposite",
            "ChkPrerasterize",
            "ChkDebugOverlay",
            "ChkUseLitShafts",
            "ChkAutoOpenSingleLocationContent",
            "CmbShaftVariant",
            "CmbHeadVariant",
            "TxtClusterThreshold",
            "TxtStubLength",
            "TxtTargetHeadRadius",
            "TxtTargetShaftHalfWidth",
            "TxtLocationMarkerSize",
            "TxtClusterMarkerSize",
            "CmbTipCapStyle",
            "TxtTipCapWidth",
            "TxtTipCapLineWeight",
            "TxtTipCapArcDepth"
        })
        {
            var nameIndex = xaml.IndexOf($"x:Name=\"{controlName}\"", StringComparison.Ordinal);
            Assert.True(nameIndex >= 0, $"{controlName} not found.");
            var nextNameIndex = xaml.IndexOf("x:Name=\"", nameIndex + 1, StringComparison.Ordinal);
            var controlBlock = nextNameIndex >= 0
                ? xaml.Substring(nameIndex, nextNameIndex - nameIndex)
                : xaml.Substring(nameIndex);

            Assert.Contains("ToolTip=", controlBlock);
        }
    }

    [Fact]
    public void ApplyTuning_MapsSingleCompositeToggleToBothConfigGates()
    {
        var source = File.ReadAllText(Path.Combine(RepoRoot, "MainWindow.DeveloperTuning.partial.cs"));

        Assert.Contains("_visualConfig.PinParts.Enabled = e.UseComposite;", source);
        Assert.Contains("_visualConfig.PinParts.UseCompositeRendering = e.UseComposite;", source);
    }

    [Fact]
    public void ApplyTuning_MapsAutoOpenSingleLocationContent()
    {
        var source = File.ReadAllText(Path.Combine(RepoRoot, "MainWindow.DeveloperTuning.partial.cs"));

        Assert.Contains("_visualConfig.AutoOpenSingleLocationContentAfterZoom = e.AutoOpenSingleLocationContentAfterZoom;", source);
        Assert.Contains("AutoOpenSingleLocationContentAfterZoom = config.AutoOpenSingleLocationContentAfterZoom", source);
    }

    [Fact]
    public void ApplyTuning_MapsTipCapFieldsToDrawnPinTipCapConfig()
    {
        var source = File.ReadAllText(Path.Combine(RepoRoot, "MainWindow.DeveloperTuning.partial.cs"));

        Assert.Contains("var cap = _visualConfig.PinMarkers.DrawnPinTipCap;", source);
        Assert.Contains("cap.Style = e.TipCapStyle;", source);
        Assert.Contains("cap.WidthPx = e.TipCapWidthPx;", source);
        Assert.Contains("cap.LineWeightPx = e.TipCapLineWeightPx;", source);
        Assert.Contains("cap.ArcDepthPx = e.TipCapArcDepthPx;", source);
    }

    [Fact]
    public void DeveloperTuningPanel_TipCapStyleCombo_HasAllStyles()
    {
        var xaml = File.ReadAllText(Path.Combine(RepoRoot, "Views", "DeveloperTuningPanel.xaml"));

        Assert.Contains("x:Name=\"CmbTipCapStyle\"", xaml);
        Assert.Contains("<ComboBoxItem Content=\"None\"/>", xaml);
        Assert.Contains("<ComboBoxItem Content=\"Horizontal\"/>", xaml);
        Assert.Contains("<ComboBoxItem Content=\"Concave\"/>", xaml);
        Assert.Contains("x:Name=\"TxtTipCapWidth\"", xaml);
        Assert.Contains("x:Name=\"TxtTipCapLineWeight\"", xaml);
        Assert.DoesNotContain("TxtTipCapHeight", xaml);
        Assert.DoesNotContain("TxtTipCapExtend", xaml);
    }

    [Fact]
    public void DeveloperTuningPanel_CombosHaveCorrectProperties()
    {
        var xaml = File.ReadAllText(Path.Combine(RepoRoot, "Views", "DeveloperTuningPanel.xaml"));

        Assert.Contains("x:Name=\"CmbShaftVariant\"", xaml);
        Assert.Contains("x:Name=\"CmbHeadVariant\"", xaml);

        // Shaft variant combo: IsEnabled binding and IsEditable="False"
        var shaftIndex = xaml.IndexOf("x:Name=\"CmbShaftVariant\"", StringComparison.Ordinal);
        var shaftEnd = xaml.IndexOf("/>", shaftIndex, StringComparison.Ordinal);
        var shaftBlock = xaml.Substring(shaftIndex, shaftEnd - shaftIndex);
        Assert.Contains("IsEnabled=\"{Binding IsChecked, ElementName=ChkComposite}\"", shaftBlock);
        Assert.Contains("IsEditable=\"False\"", shaftBlock);

        // Head variant combo: IsEnabled binding and IsEditable="False"
        var headIndex = xaml.IndexOf("x:Name=\"CmbHeadVariant\"", StringComparison.Ordinal);
        var headEnd = xaml.IndexOf("/>", headIndex, StringComparison.Ordinal);
        var headBlock = xaml.Substring(headIndex, headEnd - headIndex);
        Assert.Contains("IsEnabled=\"{Binding IsChecked, ElementName=ChkComposite}\"", headBlock);
        Assert.Contains("IsEditable=\"False\"", headBlock);
    }

    [Fact]
    public void DeveloperTuningPanel_CombosHaveItemContainerStyle()
    {
        var xaml = File.ReadAllText(Path.Combine(RepoRoot, "Views", "DeveloperTuningPanel.xaml"));

        var shaftIndex = xaml.IndexOf("x:Name=\"CmbShaftVariant\"", StringComparison.Ordinal);
        var shaftEnd = xaml.IndexOf("</ComboBox>", shaftIndex, StringComparison.Ordinal);
        if (shaftEnd < 0) shaftEnd = xaml.IndexOf("/>", shaftIndex, StringComparison.Ordinal);
        var shaftBlock = xaml.Substring(shaftIndex, shaftEnd - shaftIndex);
        Assert.Contains("ItemContainerStyle=", shaftBlock);

        var headIndex = xaml.IndexOf("x:Name=\"CmbHeadVariant\"", StringComparison.Ordinal);
        var headEnd = xaml.IndexOf("</ComboBox>", headIndex, StringComparison.Ordinal);
        if (headEnd < 0) headEnd = xaml.IndexOf("/>", headIndex, StringComparison.Ordinal);
        var headBlock = xaml.Substring(headIndex, headEnd - headIndex);
        Assert.Contains("ItemContainerStyle=", headBlock);

        // Also check that the resource defines the style
        Assert.Contains("<Style x:Key=\"DarkComboBoxItemStyle\" TargetType=\"ComboBoxItem\">", xaml);
    }

    [Fact]
    public void DeveloperTuningPanel_CodeBehind_HasVariantHelpersAndNoTextBoxes()
    {
        var source = File.ReadAllText(Path.Combine(RepoRoot, "Views", "DeveloperTuningPanel.xaml.cs"));

        Assert.Contains("SelectVariant", source);
        Assert.Contains("GetVariantFromCombo", source);
        Assert.DoesNotContain("TxtShaftVariant", source);
        Assert.DoesNotContain("TxtHeadVariant", source);
    }

    [Fact]
    public void DeveloperTuningPanel_HasLoadingGuardAndCheckboxClick()
    {
        var source = File.ReadAllText(Path.Combine(RepoRoot, "Views", "DeveloperTuningPanel.xaml.cs"));
        var xaml = File.ReadAllText(Path.Combine(RepoRoot, "Views", "DeveloperTuningPanel.xaml"));

        // SetVariantOptions has loading guard
        Assert.Contains("SetVariantOptions", source);
        var setVariantOptionsIndex = source.IndexOf("SetVariantOptions", StringComparison.Ordinal);
        var block = source.Substring(setVariantOptionsIndex, Math.Min(1000, source.Length - setVariantOptionsIndex));
        Assert.Contains("_isLoading = true;", block);
        Assert.Contains("finally", block);
        Assert.Contains("_isLoading = false;", block);

        // ChkComposite click wiring
        var chkIndex = xaml.IndexOf("x:Name=\"ChkComposite\"", StringComparison.Ordinal);
        var chkEnd = xaml.IndexOf("/>", chkIndex, StringComparison.Ordinal);
        var chkBlock = xaml.Substring(chkIndex, chkEnd - chkIndex);
        Assert.Contains("Click=\"OnPanelInputChanged\"", chkBlock);
    }

    [Fact]
    public void MainWindow_SetupTuningPanel_InitializesCatalog()
    {
        var source = File.ReadAllText(Path.Combine(RepoRoot, "MainWindow.DeveloperTuning.partial.cs"));

        Assert.Contains("_variantCatalog = new PinPartVariantCatalog(_logger);", source);
    }

    [Fact]
    public void MainWindow_TuningPanelVisibilityRequiresDeveloperToolsGate()
    {
        var source = File.ReadAllText(Path.Combine(RepoRoot, "MainWindow.DeveloperTuning.partial.cs"));

        Assert.Contains("AreDeveloperToolsEnabled() && _visualConfig.Debug.EnableTuningPanel", source);
    }

    [Fact]
    public void MainWindow_TuningActionsRejectWhenDeveloperToolsDisabled()
    {
        var source = File.ReadAllText(Path.Combine(RepoRoot, "MainWindow.DeveloperTuning.partial.cs"));

        Assert.Contains("if (!AreDeveloperToolsEnabled())", source);
        Assert.Contains("Developer tools are disabled", source);
    }

    [Fact]
    public void OnReloadTuningFromDisk_ValidatesBeforeRefreshingVariantOptions()
    {
        var source = File.ReadAllText(Path.Combine(RepoRoot, "MainWindow.DeveloperTuning.partial.cs"));
        var reloadStart = source.IndexOf("OnReloadTuningFromDisk", StringComparison.Ordinal);
        Assert.True(reloadStart >= 0);

        var reloadBlock = source.Substring(reloadStart, Math.Min(2500, source.Length - reloadStart));
        var validateIndex = reloadBlock.IndexOf("TryValidate(args", StringComparison.Ordinal);
        var refreshIndex = reloadBlock.IndexOf("RefreshTuningPanelVariantOptions", StringComparison.Ordinal);

        Assert.True(validateIndex >= 0, "Reload path must validate tuning args.");
        Assert.True(refreshIndex >= 0, "Reload path must refresh variant options.");
        Assert.True(
            validateIndex < refreshIndex,
            "Reload must validate disk values before refreshing variant combo lists.");
    }
}
