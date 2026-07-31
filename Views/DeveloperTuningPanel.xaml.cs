using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using InteractiveWorldMap.Models;

namespace InteractiveWorldMap.Views;

public enum TuningCategory
{
    Map,
    CompositePins,
    DrawnPins,
    Hitboxes,
    Shadows,
    ContentWindows
}

public partial class DeveloperTuningPanel : UserControl
{
    public const string BaseVariantLabel = "(base)";
    private bool _isLoading;

    public event EventHandler<TuningPanelEventArgs>? ApplyRequested;
    public event EventHandler? SaveRequested;
    public event EventHandler? ReloadRequested;
    public event EventHandler? CloseRequested;

    public TuningCategory? VisibleCategory { get; private set; }

    public DeveloperTuningPanel()
    {
        InitializeComponent();
        ValidateInputs();
    }

    public void ShowCategory(TuningCategory category)
    {
        VisibleCategory = category;
        CategoryTitleText.Text = category switch
        {
            TuningCategory.CompositePins => "Composite Pins",
            TuningCategory.DrawnPins => "Drawn Pins",
            TuningCategory.ContentWindows => "Content Windows",
            _ => category.ToString()
        };
        MapSection.Visibility = category == TuningCategory.Map
            ? Visibility.Visible : Visibility.Collapsed;
        CompositePinsSection.Visibility = category == TuningCategory.CompositePins
            ? Visibility.Visible : Visibility.Collapsed;
        DrawnPinsSection.Visibility = category == TuningCategory.DrawnPins
            ? Visibility.Visible : Visibility.Collapsed;
        HitboxesSection.Visibility = category == TuningCategory.Hitboxes
            ? Visibility.Visible : Visibility.Collapsed;
        ShadowsSection.Visibility = category == TuningCategory.Shadows
            ? Visibility.Visible : Visibility.Collapsed;
        ContentWindowsSection.Visibility = category == TuningCategory.ContentWindows
            ? Visibility.Visible : Visibility.Collapsed;
    }

    public void SetVariantOptions(System.Collections.Generic.IEnumerable<string> shaftVariants, System.Collections.Generic.IEnumerable<string> headVariants)
    {
        _isLoading = true;
        try
        {
            var shafts = new System.Collections.Generic.List<string> { BaseVariantLabel };
            shafts.AddRange(shaftVariants);
            CmbShaftVariant.ItemsSource = shafts;

            var heads = new System.Collections.Generic.List<string> { BaseVariantLabel };
            heads.AddRange(headVariants);
            CmbHeadVariant.ItemsSource = heads;
        }
        finally
        {
            _isLoading = false;
        }
    }

    private void SelectVariant(ComboBox combo, string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            combo.SelectedItem = BaseVariantLabel;
            return;
        }

        foreach (var item in combo.Items)
        {
            if (item is string s && string.Equals(s, value, StringComparison.OrdinalIgnoreCase))
            {
                combo.SelectedItem = s;
                return;
            }
        }

        combo.SelectedItem = BaseVariantLabel;
    }

    private string GetVariantFromCombo(ComboBox cmb)
    {
        if (cmb.SelectedItem is string selected && !string.Equals(selected, BaseVariantLabel, StringComparison.Ordinal))
        {
            return selected;
        }
        return string.Empty;
    }

    private void SetTipCapStyle(DrawnPinTipCapStyle style)
    {
        var name = style.ToString();
        foreach (var item in CmbTipCapStyle.Items)
        {
            if (item is ComboBoxItem ci && ci.Content is string s &&
                string.Equals(s, name, StringComparison.Ordinal))
            {
                CmbTipCapStyle.SelectedItem = ci;
                return;
            }
        }
        CmbTipCapStyle.SelectedIndex = 0; // None
    }

    private DrawnPinTipCapStyle GetTipCapStyle()
    {
        if (CmbTipCapStyle.SelectedItem is ComboBoxItem item &&
            item.Content is string s &&
            Enum.TryParse<DrawnPinTipCapStyle>(s, out var style))
        {
            return style;
        }
        return DrawnPinTipCapStyle.None;
    }

    private void SetTipCapAlignment(DrawnPinTipCapAlignment alignment)
    {
        var name = alignment.ToString();
        foreach (var item in CmbTipCapAlignment.Items)
        {
            if (item is ComboBoxItem ci && ci.Content is string s &&
                string.Equals(s, name, StringComparison.Ordinal))
            {
                CmbTipCapAlignment.SelectedItem = ci;
                return;
            }
        }
        CmbTipCapAlignment.SelectedIndex = 0;
    }

    private DrawnPinTipCapAlignment GetTipCapAlignment()
    {
        if (CmbTipCapAlignment.SelectedItem is ComboBoxItem item &&
            item.Content is string s &&
            Enum.TryParse<DrawnPinTipCapAlignment>(s, out var alignment))
        {
            return alignment;
        }
        return DrawnPinTipCapAlignment.ScreenHorizontal;
    }

    private void SetZoomedMapResamplingMode(ZoomedMapResamplingMode mode)
    {
        foreach (var item in CmbZoomedMapResampling.Items)
        {
            if (item is ComboBoxItem option && option.Content?.ToString() == mode.ToString())
            {
                CmbZoomedMapResampling.SelectedItem = option;
                return;
            }
        }
        CmbZoomedMapResampling.SelectedIndex = 0;
    }

    private ZoomedMapResamplingMode GetZoomedMapResamplingMode()
    {
        var text = (CmbZoomedMapResampling.SelectedItem as ComboBoxItem)?.Content?.ToString();
        return Enum.TryParse<ZoomedMapResamplingMode>(text, out var mode)
            ? mode : ZoomedMapResamplingMode.Fant;
    }

    public void LoadValues(VisualConfig config)
    {
        if (config == null) throw new ArgumentNullException(nameof(config));

        _isLoading = true;
        try
        {
            ChkComposite.IsChecked = config.PinParts.Enabled && config.PinParts.UseCompositeRendering;
            ChkPrerasterize.IsChecked = config.PinParts.UsePrerasterizedRendering;
            ChkDebugOverlay.IsChecked = config.Debug.ShowCompositePinDebugOverlay;
            ChkUseLitShafts.IsChecked = config.PinParts.UseLitShafts;
            ChkAutoOpenSingleLocationContent.IsChecked = config.AutoOpenSingleLocationContentAfterZoom;
            SetZoomedMapResamplingMode(config.ZoomedMapRendering.ResamplingMode);
            SelectVariant(CmbShaftVariant, config.PinParts.ShaftAssetVariant);
            SelectVariant(CmbHeadVariant, config.PinParts.HeadAssetVariant);
            TxtClusterThreshold.Text = Format(config.ClusterDistanceThreshold);
            TxtStubLength.Text = Format(config.PinParts.DefaultStubLengthPixels);
            TxtTargetHeadRadius.Text = Format(config.PinParts.TargetHeadRadiusPx);
            TxtTargetShaftHalfWidth.Text = Format(config.PinParts.TargetShaftHalfWidthPx);
            TxtLocationMarkerSize.Text = Format(config.LocationMarkerSize);
            TxtClusterMarkerSize.Text = Format(config.ClusterMarkerSize);

            var pinConfig = config.PinMarkers ?? new PinMarkerConfig();
            TxtClusterBadgeSize.Text = Format(config.ClusterBadgeSize);
            TxtClusterCountFontSize.Text = Format(config.ClusterCountFontSize);
            TxtZoomScale.Text = Format(config.ZoomScale);
            TxtAnimationDurationMs.Text =
                config.AnimationDurationMs.ToString(CultureInfo.InvariantCulture);
            ChkPinShadowEnabled.IsChecked = pinConfig.ShowShadow;
            TxtPinShadowOpacity.Text = Format(pinConfig.ShadowOpacity);
            var clusterShadow = config.ClusterMarkerShadow ?? new ClusterMarkerShadowConfig();
            ChkClusterShadowEnabled.IsChecked = clusterShadow.Enabled;
            TxtClusterShadowOpacity.Text = Format(clusterShadow.Opacity);
            TxtDrawnHeadDiameter.Text = Format(pinConfig.BallSize);
            TxtDrawnShaftWidth.Text = Format(pinConfig.ShaftWidth);
            TxtDrawnShaftLength.Text = Format(pinConfig.ShaftLength);
            var hitTargets = config.MarkerHitTargets ?? new MarkerHitTargetConfig();
            TxtPinHitDiameter.Text = Format(hitTargets.PinDiameterPx);
            TxtClusterHitDiameter.Text = Format(hitTargets.ClusterDiameterPx);
            var cap = pinConfig.DrawnPinTipCap ?? new DrawnPinTipCapConfig();
            double outlineWidth = Math.Max(pinConfig.ShaftWidth, 2.5) +
                                  (2.0 * Math.Max(pinConfig.ShaftOutlineThickness, 1.0));
            SetTipCapStyle(cap.Style);
            SetTipCapAlignment(cap.Alignment);
            TxtTipCapWidth.Text = Format(cap.ResolveWidthPx(outlineWidth));
            TxtTipCapLineWeight.Text = Format(
                cap.ResolveLineWeightPx(pinConfig.ShaftOutlineThickness));
            TxtTipCapArcDepth.Text = Format(cap.ArcDepthPx);

            var content = config.ContentWindows ?? new ContentWindowConfig();
            TxtContentFontFamily.Text = content.FontFamily;
            TxtPopupBackgroundColor.Text = content.Popup.BackgroundColor;
            TxtPopupBackgroundOpacity.Text = Format(content.Popup.BackgroundOpacity);
            TxtPopupBorderColor.Text = content.Popup.BorderColor;
            TxtPopupBorderThickness.Text = Format(content.Popup.BorderThickness);
            TxtPopupCornerRadius.Text = Format(content.Popup.CornerRadius);
            TxtPopupTextColor.Text = content.Popup.TextColor;
            TxtPopupHeadingFontSize.Text = Format(content.Popup.HeadingFontSize);
            TxtPopupBodyFontSize.Text = Format(content.Popup.BodyFontSize);
            TxtCaptionBackgroundColor.Text = content.Caption.BackgroundColor;
            TxtCaptionBackgroundOpacity.Text = Format(content.Caption.BackgroundOpacity);
            TxtCaptionTopBorderColor.Text = content.Caption.TopBorderColor;
            TxtCaptionTextColor.Text = content.Caption.TextColor;
            TxtCaptionFontSize.Text = Format(content.Caption.FontSize);
            UpdateColorSwatches();

            SetStatus("Loaded current values.");
        }
        finally
        {
            _isLoading = false;
            ValidateInputs();
        }
    }

    public void SetStatus(string message)
    {
        StatusText.Text = message;
    }

    public bool TryGetCurrentValues(out TuningPanelEventArgs args)
    {
        return TryBuildEventArgs(out args);
    }

    private void OnPanelInputChanged(object sender, RoutedEventArgs e)
    {
        if (!_isLoading)
        {
            UpdateColorSwatches();
            ValidateInputs();
        }
    }

    private static readonly Brush InvalidSwatchBrush = CreateInvalidSwatchBrush();

    private static Brush CreateInvalidSwatchBrush()
    {
        var brush = new SolidColorBrush(Color.FromRgb(0x55, 0x55, 0x55));
        brush.Freeze();
        return brush;
    }

    private void UpdateColorSwatches()
    {
        SetSwatch(SwPopupBackgroundColor, TxtPopupBackgroundColor.Text);
        SetSwatch(SwPopupBorderColor, TxtPopupBorderColor.Text);
        SetSwatch(SwPopupTextColor, TxtPopupTextColor.Text);
        SetSwatch(SwCaptionBackgroundColor, TxtCaptionBackgroundColor.Text);
        SetSwatch(SwCaptionTopBorderColor, TxtCaptionTopBorderColor.Text);
        SetSwatch(SwCaptionTextColor, TxtCaptionTextColor.Text);
    }

    private static void SetSwatch(Border swatch, string text)
    {
        if (ContentWindowTheme.TryParseColor(text, out var color))
        {
            var brush = new SolidColorBrush(color);
            brush.Freeze();
            swatch.Background = brush;
        }
        else
        {
            swatch.Background = InvalidSwatchBrush;
        }
    }

    private void OnVariantSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_isLoading)
            ValidateInputs();
    }

    private void OnApplyClick(object sender, RoutedEventArgs e)
    {
        if (!TryBuildEventArgs(out var args))
            return;

        ApplyRequested?.Invoke(this, args);
    }

    private void OnSaveClick(object sender, RoutedEventArgs e)
    {
        SaveRequested?.Invoke(this, EventArgs.Empty);
    }

    private void OnReloadClick(object sender, RoutedEventArgs e)
    {
        ReloadRequested?.Invoke(this, EventArgs.Empty);
    }

    private void OnCloseClick(object sender, RoutedEventArgs e)
    {
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }

    private void ValidateInputs()
    {
        ApplyButton.IsEnabled = TryBuildEventArgs(out _, out var error);
        ErrorText.Text = error;
    }

    private bool TryBuildEventArgs(out TuningPanelEventArgs args)
    {
        var ok = TryBuildEventArgs(out args, out var error);
        ErrorText.Text = error;
        return ok;
    }

    private bool TryBuildEventArgs(out TuningPanelEventArgs args, out string error)
    {
        args = new TuningPanelEventArgs();

        if (!TryReadMapTuning(
                TxtClusterThreshold.Text,
                TxtZoomScale.Text,
                TxtAnimationDurationMs.Text,
                out var map,
                out error) ||
            !TryReadPinAppearance(
                TxtLocationMarkerSize.Text,
                TxtClusterMarkerSize.Text,
                TxtClusterBadgeSize.Text,
                TxtClusterCountFontSize.Text,
                TxtStubLength.Text,
                TxtTargetHeadRadius.Text,
                TxtTargetShaftHalfWidth.Text,
                TxtDrawnHeadDiameter.Text,
                TxtDrawnShaftWidth.Text,
                TxtDrawnShaftLength.Text,
                TxtTipCapWidth.Text,
                TxtTipCapLineWeight.Text,
                TxtTipCapArcDepth.Text,
                out var appearance,
                out error) ||
            !TryReadHitboxes(
                TxtPinHitDiameter.Text,
                TxtClusterHitDiameter.Text,
                out var hitboxes,
                out error) ||
            !TryReadShadowTuning(
                TxtPinShadowOpacity.Text,
                TxtClusterShadowOpacity.Text,
                out var shadows,
                out error) ||
            !TryReadContentWindowTuning(
                TxtContentFontFamily.Text,
                TxtPopupBackgroundColor.Text,
                TxtPopupBackgroundOpacity.Text,
                TxtPopupBorderColor.Text,
                TxtPopupBorderThickness.Text,
                TxtPopupCornerRadius.Text,
                TxtPopupTextColor.Text,
                TxtPopupHeadingFontSize.Text,
                TxtPopupBodyFontSize.Text,
                TxtCaptionBackgroundColor.Text,
                TxtCaptionBackgroundOpacity.Text,
                TxtCaptionTopBorderColor.Text,
                TxtCaptionTextColor.Text,
                TxtCaptionFontSize.Text,
                out var content,
                out error))
        {
            return false;
        }

        args = new TuningPanelEventArgs
        {
            PinPartsEnabled = ChkComposite.IsChecked == true,
            UseComposite = ChkComposite.IsChecked == true,
            UsePrerasterize = ChkPrerasterize.IsChecked == true,
            ShowDebugOverlay = ChkDebugOverlay.IsChecked == true,
            UseLitShafts = ChkUseLitShafts.IsChecked == true,
            AutoOpenSingleLocationContentAfterZoom = ChkAutoOpenSingleLocationContent.IsChecked == true,
            ZoomedMapResamplingMode = GetZoomedMapResamplingMode(),
            ShaftVariant = GetVariantFromCombo(CmbShaftVariant).Trim(),
            HeadVariant = GetVariantFromCombo(CmbHeadVariant).Trim(),
            ClusterThreshold = map.ClusterThreshold,
            StubLength = appearance.StubLength,
            TargetHeadRadiusPx = appearance.TargetHeadRadiusPx,
            TargetShaftHalfWidthPx = appearance.TargetShaftHalfWidthPx,
            LocationMarkerSize = appearance.LocationMarkerSize,
            ClusterMarkerSize = appearance.ClusterMarkerSize,
            ClusterBadgeSize = appearance.ClusterBadgeSize,
            ClusterCountFontSize = appearance.ClusterCountFontSize,
            ZoomScale = map.ZoomScale,
            AnimationDurationMs = map.AnimationDurationMs,
            PinShadowEnabled = ChkPinShadowEnabled.IsChecked == true,
            PinShadowOpacity = shadows.PinShadowOpacity,
            ClusterShadowEnabled = ChkClusterShadowEnabled.IsChecked == true,
            ClusterShadowOpacity = shadows.ClusterShadowOpacity,
            DrawnHeadDiameterPx = appearance.DrawnHeadDiameterPx,
            DrawnShaftWidthPx = appearance.DrawnShaftWidthPx,
            DrawnShaftLengthPx = appearance.DrawnShaftLengthPx,
            PinHitDiameterPx = hitboxes.PinHitDiameterPx,
            ClusterHitDiameterPx = hitboxes.ClusterHitDiameterPx,
            TipCapStyle = GetTipCapStyle(),
            TipCapAlignment = GetTipCapAlignment(),
            TipCapWidthPx = appearance.TipCapWidthPx,
            TipCapLineWeightPx = appearance.TipCapLineWeightPx,
            TipCapArcDepthPx = appearance.TipCapArcDepthPx,
            ContentFontFamily = content.ContentFontFamily,
            PopupBackgroundColor = content.PopupBackgroundColor,
            PopupBackgroundOpacity = content.PopupBackgroundOpacity,
            PopupBorderColor = content.PopupBorderColor,
            PopupBorderThickness = content.PopupBorderThickness,
            PopupCornerRadius = content.PopupCornerRadius,
            PopupTextColor = content.PopupTextColor,
            PopupHeadingFontSize = content.PopupHeadingFontSize,
            PopupBodyFontSize = content.PopupBodyFontSize,
            CaptionBackgroundColor = content.CaptionBackgroundColor,
            CaptionBackgroundOpacity = content.CaptionBackgroundOpacity,
            CaptionTopBorderColor = content.CaptionTopBorderColor,
            CaptionTextColor = content.CaptionTextColor,
            CaptionFontSize = content.CaptionFontSize
        };
        error = string.Empty;
        return true;
    }

}
