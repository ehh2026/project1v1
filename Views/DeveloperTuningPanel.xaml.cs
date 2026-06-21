using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using InteractiveWorldMap.Models;

namespace InteractiveWorldMap.Views;

public partial class DeveloperTuningPanel : UserControl
{
    private bool _isLoading;

    public event EventHandler<TuningPanelEventArgs>? ApplyRequested;
    public event EventHandler? SaveRequested;
    public event EventHandler? ReloadRequested;

    public DeveloperTuningPanel()
    {
        InitializeComponent();
        ValidateInputs();
    }

    public void LoadValues(VisualConfig config)
    {
        if (config == null) throw new ArgumentNullException(nameof(config));

        _isLoading = true;
        try
        {
            ChkPinPartsEnabled.IsChecked = config.PinParts.Enabled;
            ChkComposite.IsChecked = config.PinParts.UseCompositeRendering;
            ChkPrerasterize.IsChecked = config.PinParts.UsePrerasterizedRendering;
            ChkDebugOverlay.IsChecked = config.Debug.ShowCompositePinDebugOverlay;
            ChkUseLitShafts.IsChecked = config.PinParts.UseLitShafts;
            TxtShaftVariant.Text = config.PinParts.ShaftAssetVariant;
            TxtHeadVariant.Text = config.PinParts.HeadAssetVariant;
            TxtClusterThreshold.Text = Format(config.ClusterDistanceThreshold);
            TxtStubLength.Text = Format(config.PinParts.DefaultStubLengthPixels);
            TxtTargetHeadRadius.Text = Format(config.PinParts.TargetHeadRadiusPx);
            TxtTargetShaftHalfWidth.Text = Format(config.PinParts.TargetShaftHalfWidthPx);
            TxtLocationMarkerSize.Text = Format(config.LocationMarkerSize);
            TxtClusterMarkerSize.Text = Format(config.ClusterMarkerSize);
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

    private void OnInputChanged(object sender, TextChangedEventArgs e)
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
            !TryReadPositive(TxtClusterMarkerSize.Text, "Cluster marker", out var clusterMarkerSize, out error))
        {
            return false;
        }

        args = new TuningPanelEventArgs
        {
            PinPartsEnabled = ChkPinPartsEnabled.IsChecked == true,
            UseComposite = ChkComposite.IsChecked == true,
            UsePrerasterize = ChkPrerasterize.IsChecked == true,
            ShowDebugOverlay = ChkDebugOverlay.IsChecked == true,
            UseLitShafts = ChkUseLitShafts.IsChecked == true,
            ShaftVariant = TxtShaftVariant.Text.Trim(),
            HeadVariant = TxtHeadVariant.Text.Trim(),
            ClusterThreshold = clusterThreshold,
            StubLength = stubLength,
            TargetHeadRadiusPx = targetHeadRadius,
            TargetShaftHalfWidthPx = targetShaftHalfWidth,
            LocationMarkerSize = locationMarkerSize,
            ClusterMarkerSize = clusterMarkerSize
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
}
