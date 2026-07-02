using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using InteractiveWorldMap.Models;

namespace InteractiveWorldMap.Views;

public enum TuningCategory
{
    Map,
    CompositePins,
    DrawnPins,
    Hitboxes
}

public partial class DeveloperTuningPanel : UserControl
{
    public const string BaseVariantLabel = "(base)";
    private bool _isLoading;

    public event EventHandler<TuningPanelEventArgs>? ApplyRequested;
    public event EventHandler? SaveRequested;
    public event EventHandler? ReloadRequested;

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

    private void OnPanelInputChanged(object sender, RoutedEventArgs e)
    {
        if (!_isLoading)
            ValidateInputs();
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

        if (!TryReadPositive(TxtClusterThreshold.Text, "Cluster threshold", out var clusterThreshold, out error) ||
            !TryReadNonNegative(TxtStubLength.Text, "Stub length", out var stubLength, out error) ||
            !TryReadNonNegative(TxtTargetHeadRadius.Text, "Head radius", out var targetHeadRadius, out error) ||
            !TryReadNonNegative(TxtTargetShaftHalfWidth.Text, "Shaft half width", out var targetShaftHalfWidth, out error) ||
            !TryReadPositive(TxtLocationMarkerSize.Text, "Location marker", out var locationMarkerSize, out error) ||
            !TryReadPositive(TxtClusterMarkerSize.Text, "Cluster marker", out var clusterMarkerSize, out error) ||
            !TryReadPositive(TxtDrawnHeadDiameter.Text, "Drawn head diameter", out var drawnHeadDiameter, out error) ||
            !TryReadPositive(TxtDrawnShaftWidth.Text, "Drawn shaft width", out var drawnShaftWidth, out error) ||
            !TryReadPositive(TxtDrawnShaftLength.Text, "Drawn shaft length", out var drawnShaftLength, out error) ||
            !TryReadPositive(TxtPinHitDiameter.Text, "Pin hitbox", out var pinHitDiameter, out error) ||
            !TryReadPositive(TxtClusterHitDiameter.Text, "Cluster hitbox", out var clusterHitDiameter, out error) ||
            !TryReadPositive(TxtTipCapWidth.Text, "Cap width", out var tipCapWidth, out error) ||
            !TryReadPositive(TxtTipCapLineWeight.Text, "Line weight", out var tipCapLineWeight, out error) ||
            !TryReadNonNegative(TxtTipCapArcDepth.Text, "Curvature", out var tipCapArcDepth, out error))
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
            ClusterThreshold = clusterThreshold,
            StubLength = stubLength,
            TargetHeadRadiusPx = targetHeadRadius,
            TargetShaftHalfWidthPx = targetShaftHalfWidth,
            LocationMarkerSize = locationMarkerSize,
            ClusterMarkerSize = clusterMarkerSize,
            DrawnHeadDiameterPx = drawnHeadDiameter,
            DrawnShaftWidthPx = drawnShaftWidth,
            DrawnShaftLengthPx = drawnShaftLength,
            PinHitDiameterPx = pinHitDiameter,
            ClusterHitDiameterPx = clusterHitDiameter,
            TipCapStyle = GetTipCapStyle(),
            TipCapAlignment = GetTipCapAlignment(),
            TipCapWidthPx = tipCapWidth,
            TipCapLineWeightPx = tipCapLineWeight,
            TipCapArcDepthPx = tipCapArcDepth
        };
        error = string.Empty;
        return true;
    }

    private static bool TryReadPositive(string text, string label, out double value, out string error)
    {
        if (!TryReadDouble(text, label, out value, out error))
            return false;

        if (value <= 0)
        {
            error = $"{label} must be greater than 0.";
            return false;
        }

        return true;
    }

    private static bool TryReadNonNegative(string text, string label, out double value, out string error)
    {
        if (!TryReadDouble(text, label, out value, out error))
            return false;

        if (value < 0)
        {
            error = $"{label} must be 0 or greater.";
            return false;
        }

        return true;
    }

    private static bool TryReadDouble(string text, string label, out double value, out string error)
    {
        if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
        {
            error = $"{label} must be a number.";
            return false;
        }

        if (double.IsNaN(value) || double.IsInfinity(value))
        {
            error = $"{label} must be finite.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    private static string Format(double value) => value.ToString(CultureInfo.InvariantCulture);

    /// <summary>
    /// Validates already-parsed numeric tuning values (e.g. from a reloaded config file).
    /// Applies the same positivity/finite rules as the UI path without re-parsing text fields.
    /// Called from MainWindow before applying a reloaded config so invalid disk values are rejected.
    /// </summary>
    public static bool TryValidate(TuningPanelEventArgs args, out string error)
    {
        if (args == null) throw new ArgumentNullException(nameof(args));

        if (args.ClusterThreshold <= 0 || !double.IsFinite(args.ClusterThreshold))
        { error = "Cluster threshold must be > 0 and finite."; return false; }
        if (args.LocationMarkerSize <= 0 || !double.IsFinite(args.LocationMarkerSize))
        { error = "Location marker size must be > 0 and finite."; return false; }
        if (args.ClusterMarkerSize <= 0 || !double.IsFinite(args.ClusterMarkerSize))
        { error = "Cluster marker size must be > 0 and finite."; return false; }
        if (args.StubLength < 0 || !double.IsFinite(args.StubLength))
        { error = "Stub length must be >= 0 and finite."; return false; }
        if (args.TargetHeadRadiusPx < 0 || !double.IsFinite(args.TargetHeadRadiusPx))
        { error = "Head radius must be >= 0 and finite."; return false; }
        if (args.TargetShaftHalfWidthPx < 0 || !double.IsFinite(args.TargetShaftHalfWidthPx))
        { error = "Shaft half width must be >= 0 and finite."; return false; }
        if (args.DrawnHeadDiameterPx <= 0 || !double.IsFinite(args.DrawnHeadDiameterPx))
        { error = "Drawn head diameter must be > 0 and finite."; return false; }
        if (args.DrawnShaftWidthPx <= 0 || !double.IsFinite(args.DrawnShaftWidthPx))
        { error = "Drawn shaft width must be > 0 and finite."; return false; }
        if (args.DrawnShaftLengthPx <= 0 || !double.IsFinite(args.DrawnShaftLengthPx))
        { error = "Drawn shaft length must be > 0 and finite."; return false; }
        if (args.PinHitDiameterPx <= 0 || !double.IsFinite(args.PinHitDiameterPx))
        { error = "Pin hitbox must be > 0 and finite."; return false; }
        if (args.ClusterHitDiameterPx <= 0 || !double.IsFinite(args.ClusterHitDiameterPx))
        { error = "Cluster hitbox must be > 0 and finite."; return false; }
        if (args.TipCapWidthPx <= 0 || !double.IsFinite(args.TipCapWidthPx))
        { error = "Cap width must be > 0 and finite."; return false; }
        if (args.TipCapLineWeightPx <= 0 || !double.IsFinite(args.TipCapLineWeightPx))
        { error = "Line weight must be > 0 and finite."; return false; }
        if (args.TipCapArcDepthPx < 0 || !double.IsFinite(args.TipCapArcDepthPx))
        { error = "Curvature must be >= 0 and finite."; return false; }

        error = string.Empty;
        return true;
    }
}
