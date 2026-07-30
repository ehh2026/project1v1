using System;
using System.Globalization;
using InteractiveWorldMap.Models;

namespace InteractiveWorldMap.Views;

public partial class DeveloperTuningPanel
{
    internal readonly record struct MapTuningValues(
        double ClusterThreshold,
        double ZoomScale,
        int AnimationDurationMs);

    internal readonly record struct PinAppearanceValues(
        double LocationMarkerSize,
        double ClusterMarkerSize,
        double ClusterBadgeSize,
        double ClusterCountFontSize,
        double StubLength,
        double TargetHeadRadiusPx,
        double TargetShaftHalfWidthPx,
        double DrawnHeadDiameterPx,
        double DrawnShaftWidthPx,
        double DrawnShaftLengthPx,
        double TipCapWidthPx,
        double TipCapLineWeightPx,
        double TipCapArcDepthPx);

    internal readonly record struct HitboxValues(
        double PinHitDiameterPx,
        double ClusterHitDiameterPx);

    internal readonly record struct ShadowTuningValues(
        double PinShadowOpacity,
        double ClusterShadowOpacity);

    internal readonly record struct ContentWindowTuningValues(
        string ContentFontFamily,
        string PopupBackgroundColor,
        double PopupBackgroundOpacity,
        string PopupBorderColor,
        double PopupBorderThickness,
        double PopupCornerRadius,
        string PopupTextColor,
        double PopupHeadingFontSize,
        double PopupBodyFontSize,
        string CaptionBackgroundColor,
        double CaptionBackgroundOpacity,
        string CaptionTopBorderColor,
        string CaptionTextColor,
        double CaptionFontSize);

    internal static bool TryReadMapTuning(
        string clusterThresholdText,
        string zoomScaleText,
        string animationDurationText,
        out MapTuningValues values,
        out string error)
    {
        values = default;
        if (!TryReadPositive(clusterThresholdText, "Cluster threshold", out var clusterThreshold, out error) ||
            !TryReadPositive(zoomScaleText, "Zoom scale", out var zoomScale, out error) ||
            !TryReadPositiveInt(animationDurationText, "Animation duration", out var animationDurationMs, out error))
        {
            return false;
        }

        values = new MapTuningValues(clusterThreshold, zoomScale, animationDurationMs);
        return true;
    }

    internal static bool TryReadPinAppearance(
        string locationMarkerSizeText,
        string clusterMarkerSizeText,
        string clusterBadgeSizeText,
        string clusterCountFontSizeText,
        string stubLengthText,
        string targetHeadRadiusText,
        string targetShaftHalfWidthText,
        string drawnHeadDiameterText,
        string drawnShaftWidthText,
        string drawnShaftLengthText,
        string tipCapWidthText,
        string tipCapLineWeightText,
        string tipCapArcDepthText,
        out PinAppearanceValues values,
        out string error)
    {
        values = default;
        if (!TryReadPositive(locationMarkerSizeText, "Location marker", out var locationMarkerSize, out error) ||
            !TryReadPositive(clusterMarkerSizeText, "Cluster marker", out var clusterMarkerSize, out error) ||
            !TryReadPositive(clusterBadgeSizeText, "Cluster badge", out var clusterBadgeSize, out error) ||
            !TryReadPositive(clusterCountFontSizeText, "Cluster count font", out var clusterCountFontSize, out error) ||
            !TryReadNonNegative(stubLengthText, "Stub length", out var stubLength, out error) ||
            !TryReadNonNegative(targetHeadRadiusText, "Head radius", out var targetHeadRadius, out error) ||
            !TryReadNonNegative(targetShaftHalfWidthText, "Shaft half width", out var targetShaftHalfWidth, out error) ||
            !TryReadPositive(drawnHeadDiameterText, "Drawn head diameter", out var drawnHeadDiameter, out error) ||
            !TryReadPositive(drawnShaftWidthText, "Drawn shaft width", out var drawnShaftWidth, out error) ||
            !TryReadPositive(drawnShaftLengthText, "Drawn shaft length", out var drawnShaftLength, out error) ||
            !TryReadPositive(tipCapWidthText, "Cap width", out var tipCapWidth, out error) ||
            !TryReadPositive(tipCapLineWeightText, "Line weight", out var tipCapLineWeight, out error) ||
            !TryReadNonNegative(tipCapArcDepthText, "Curvature", out var tipCapArcDepth, out error))
        {
            return false;
        }

        values = new PinAppearanceValues(
            locationMarkerSize,
            clusterMarkerSize,
            clusterBadgeSize,
            clusterCountFontSize,
            stubLength,
            targetHeadRadius,
            targetShaftHalfWidth,
            drawnHeadDiameter,
            drawnShaftWidth,
            drawnShaftLength,
            tipCapWidth,
            tipCapLineWeight,
            tipCapArcDepth);
        return true;
    }

    internal static bool TryReadHitboxes(
        string pinHitDiameterText,
        string clusterHitDiameterText,
        out HitboxValues values,
        out string error)
    {
        values = default;
        if (!TryReadPositive(pinHitDiameterText, "Pin hitbox", out var pinHitDiameter, out error) ||
            !TryReadPositive(clusterHitDiameterText, "Cluster hitbox", out var clusterHitDiameter, out error))
        {
            return false;
        }

        values = new HitboxValues(pinHitDiameter, clusterHitDiameter);
        return true;
    }

    internal static bool TryReadShadowTuning(
        string pinShadowOpacityText,
        string clusterShadowOpacityText,
        out ShadowTuningValues values,
        out string error)
    {
        values = default;
        if (!TryReadOpacity(pinShadowOpacityText, "Pin shadow opacity", out var pinShadowOpacity, out error) ||
            !TryReadOpacity(clusterShadowOpacityText, "Cluster shadow opacity", out var clusterShadowOpacity, out error))
        {
            return false;
        }

        values = new ShadowTuningValues(pinShadowOpacity, clusterShadowOpacity);
        return true;
    }

    internal static bool TryReadContentWindowTuning(
        string contentFontFamilyText,
        string popupBackgroundColorText,
        string popupBackgroundOpacityText,
        string popupBorderColorText,
        string popupBorderThicknessText,
        string popupCornerRadiusText,
        string popupTextColorText,
        string popupHeadingFontSizeText,
        string popupBodyFontSizeText,
        string captionBackgroundColorText,
        string captionBackgroundOpacityText,
        string captionTopBorderColorText,
        string captionTextColorText,
        string captionFontSizeText,
        out ContentWindowTuningValues values,
        out string error)
    {
        values = default;
        if (!TryReadNonEmpty(contentFontFamilyText, "Font family", out var contentFontFamily, out error) ||
            !TryReadColor(popupBackgroundColorText, "Popup background color", out var popupBg, out error) ||
            !TryReadOpacity(popupBackgroundOpacityText, "Popup background opacity", out var popupBgOpacity, out error) ||
            !TryReadColor(popupBorderColorText, "Popup border color", out var popupBorderColor, out error) ||
            !TryReadNonNegative(popupBorderThicknessText, "Popup border thickness", out var popupBorderThickness, out error) ||
            !TryReadNonNegative(popupCornerRadiusText, "Popup corner radius", out var popupCornerRadius, out error) ||
            !TryReadColor(popupTextColorText, "Popup text color", out var popupTextColor, out error) ||
            !TryReadPositive(popupHeadingFontSizeText, "Popup heading size", out var popupHeadingSize, out error) ||
            !TryReadPositive(popupBodyFontSizeText, "Popup body size", out var popupBodySize, out error) ||
            !TryReadColor(captionBackgroundColorText, "Caption background color", out var captionBg, out error) ||
            !TryReadOpacity(captionBackgroundOpacityText, "Caption background opacity", out var captionBgOpacity, out error) ||
            !TryReadColor(captionTopBorderColorText, "Caption top border color", out var captionTopBorder, out error) ||
            !TryReadColor(captionTextColorText, "Caption text color", out var captionTextColor, out error) ||
            !TryReadPositive(captionFontSizeText, "Caption text size", out var captionSize, out error))
        {
            return false;
        }

        values = new ContentWindowTuningValues(
            contentFontFamily,
            popupBg,
            popupBgOpacity,
            popupBorderColor,
            popupBorderThickness,
            popupCornerRadius,
            popupTextColor,
            popupHeadingSize,
            popupBodySize,
            captionBg,
            captionBgOpacity,
            captionTopBorder,
            captionTextColor,
            captionSize);
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

    private static bool TryReadPositiveInt(
        string text, string label, out int value, out string error)
    {
        if (!int.TryParse(
                text,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out value) ||
            value <= 0)
        {
            error = $"{label} must be a positive integer.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    private static bool TryReadOpacity(
        string text, string label, out double value, out string error)
    {
        if (!TryReadDouble(text, label, out value, out error))
            return false;

        if (value < 0 || value > 1)
        {
            error = $"{label} must be between 0 and 1.";
            return false;
        }

        return true;
    }

    private static bool TryReadColor(string text, string label, out string value, out string error)
    {
        value = (text ?? string.Empty).Trim();
        if (!ContentWindowTheme.TryParseColor(value, out _))
        {
            error = $"{label} must be a valid hex color (e.g. #1E1E1E or #FF1E1E1E).";
            return false;
        }

        error = string.Empty;
        return true;
    }

    private static bool TryReadNonEmpty(string text, string label, out string value, out string error)
    {
        value = (text ?? string.Empty).Trim();
        if (value.Length == 0)
        {
            error = $"{label} must not be empty.";
            return false;
        }

        error = string.Empty;
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

        foreach (var rule in NumericValidationRules)
        {
            if (rule.IsInvalid(args))
            {
                error = rule.Error;
                return false;
            }
        }

        foreach (var rule in TextValidationRules)
        {
            if (rule.IsInvalid(args))
            {
                error = rule.Error;
                return false;
            }
        }

        error = string.Empty;
        return true;
    }

    private sealed record ValidationRule(
        Func<TuningPanelEventArgs, bool> IsInvalid,
        string Error);

    private static readonly ValidationRule[] NumericValidationRules =
    {
        PositiveFiniteRule(a => a.ClusterThreshold, "Cluster threshold"),
        PositiveFiniteRule(a => a.LocationMarkerSize, "Location marker size"),
        PositiveFiniteRule(a => a.ClusterMarkerSize, "Cluster marker size"),
        PositiveFiniteRule(a => a.ClusterBadgeSize, "Cluster badge size"),
        PositiveFiniteRule(a => a.ClusterCountFontSize, "Cluster count font size"),
        PositiveFiniteRule(a => a.ZoomScale, "Zoom scale"),
        new(a => a.AnimationDurationMs <= 0, "Animation duration must be a positive integer."),
        OpacityRule(a => a.PinShadowOpacity, "Pin shadow opacity"),
        OpacityRule(a => a.ClusterShadowOpacity, "Cluster shadow opacity"),
        NonNegativeFiniteRule(a => a.StubLength, "Stub length"),
        NonNegativeFiniteRule(a => a.TargetHeadRadiusPx, "Head radius"),
        NonNegativeFiniteRule(a => a.TargetShaftHalfWidthPx, "Shaft half width"),
        PositiveFiniteRule(a => a.DrawnHeadDiameterPx, "Drawn head diameter"),
        PositiveFiniteRule(a => a.DrawnShaftWidthPx, "Drawn shaft width"),
        PositiveFiniteRule(a => a.DrawnShaftLengthPx, "Drawn shaft length"),
        PositiveFiniteRule(a => a.PinHitDiameterPx, "Pin hitbox"),
        PositiveFiniteRule(a => a.ClusterHitDiameterPx, "Cluster hitbox"),
        PositiveFiniteRule(a => a.TipCapWidthPx, "Cap width"),
        PositiveFiniteRule(a => a.TipCapLineWeightPx, "Line weight"),
        NonNegativeFiniteRule(a => a.TipCapArcDepthPx, "Curvature"),
        OpacityRule(a => a.PopupBackgroundOpacity, "Popup background opacity"),
        NonNegativeFiniteRule(a => a.PopupBorderThickness, "Popup border thickness"),
        NonNegativeFiniteRule(a => a.PopupCornerRadius, "Popup corner radius"),
        PositiveFiniteRule(a => a.PopupHeadingFontSize, "Popup heading size"),
        PositiveFiniteRule(a => a.PopupBodyFontSize, "Popup body size"),
        OpacityRule(a => a.CaptionBackgroundOpacity, "Caption background opacity"),
        PositiveFiniteRule(a => a.CaptionFontSize, "Caption text size")
    };

    private static readonly ValidationRule[] TextValidationRules =
    {
        new(a => string.IsNullOrWhiteSpace(a.ContentFontFamily), "Font family must not be empty."),
        ColorRule(a => a.PopupBackgroundColor, "Popup background color"),
        ColorRule(a => a.PopupBorderColor, "Popup border color"),
        ColorRule(a => a.PopupTextColor, "Popup text color"),
        ColorRule(a => a.CaptionBackgroundColor, "Caption background color"),
        ColorRule(a => a.CaptionTopBorderColor, "Caption top border color"),
        ColorRule(a => a.CaptionTextColor, "Caption text color")
    };

    private static ValidationRule PositiveFiniteRule(
        Func<TuningPanelEventArgs, double> selector,
        string label) =>
        new(args => selector(args) <= 0 || !double.IsFinite(selector(args)),
            $"{label} must be > 0 and finite.");

    private static ValidationRule NonNegativeFiniteRule(
        Func<TuningPanelEventArgs, double> selector,
        string label) =>
        new(args => selector(args) < 0 || !double.IsFinite(selector(args)),
            $"{label} must be >= 0 and finite.");

    private static ValidationRule OpacityRule(
        Func<TuningPanelEventArgs, double> selector,
        string label) =>
        new(args => !IsValidOpacity(selector(args)),
            $"{label} must be between 0 and 1 and finite.");

    private static ValidationRule ColorRule(
        Func<TuningPanelEventArgs, string> selector,
        string label) =>
        new(args => !ContentWindowTheme.TryParseColor(selector(args), out _),
            $"{label} must be a valid hex color.");

    private static bool IsValidOpacity(double value) =>
        double.IsFinite(value) && value >= 0 && value <= 1;
}
