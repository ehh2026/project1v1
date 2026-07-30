using System;
using System.Windows;
using System.Windows.Media;
using InteractiveWorldMap.Models;

namespace InteractiveWorldMap.Views;

/// <summary>
/// Displays didactic text information for a location.
/// </summary>
public partial class DidacticTextWindow : Window
{
    public DidacticTextWindow()
    {
        InitializeComponent();

        // Start with opacity 0 for animation
        Opacity = 0;
    }

    /// <summary>
    /// Applies configured popup colors, opacity, and fonts. Call before the window is shown.
    /// </summary>
    public void ApplyStyle(ContentWindowConfig style)
    {
        if (style == null) return;

        var fontFamily = new FontFamily(style.FontFamily);
        FontFamily = fontFamily;

        RootBorder.Background = ContentWindowTheme.ToBrush(
            style.Popup.BackgroundColor, style.Popup.BackgroundOpacity, Color.FromRgb(0x1E, 0x1E, 0x1E));
        RootBorder.BorderBrush = ContentWindowTheme.ToBrush(style.Popup.BorderColor, Colors.White);
        RootBorder.BorderThickness = new Thickness(style.Popup.BorderThickness);
        RootBorder.CornerRadius = new CornerRadius(style.Popup.CornerRadius);

        var textBrush = ContentWindowTheme.ToBrush(style.Popup.TextColor, Colors.White);
        HeadingText.Foreground = textBrush;
        HeadingText.FontSize = style.Popup.HeadingFontSize;
        DidacticTextBlock.Foreground = textBrush;
        DidacticTextBlock.FontSize = style.Popup.BodyFontSize;
    }

    /// <summary>
    /// Sets the didactic text and its Excel-derived location/person heading.
    /// </summary>
    public void SetContent(string text, string? locationName)
    {
        DidacticTextBlock.Text = text;
        var heading = locationName?.Trim();
        HeadingText.Text = heading ?? string.Empty;
        HeadingText.Visibility = string.IsNullOrEmpty(heading)
            ? Visibility.Collapsed
            : Visibility.Visible;
    }

    /// <summary>
    /// Positions the window to the left of the content window.
    /// </summary>
    public void PositionRelativeTo(Window contentWindow)
    {
        // Position to the left of the content window
        Left = contentWindow.Left - Width - 10;
        Top = contentWindow.Top;

        // Ensure it stays on screen
        if (Left < 0)
        {
            // If it doesn't fit on the left, put it on the right of the main window
            Left = contentWindow.Left + contentWindow.Width + 10;
        }

        // Ensure minimum left position
        Left = Math.Max(0, Left);

        // Ensure the window doesn't go off the bottom of the screen
        var screenHeight = SystemParameters.PrimaryScreenHeight;
        if (Top + Height > screenHeight)
        {
            Top = Math.Max(0, screenHeight - Height - 10);
        }
    }

    /// <summary>
    /// Animates the window opening.
    /// </summary>
    public void AnimateOpen()
    {
        var duration = TimeSpan.FromMilliseconds(150);
        var fadeIn = new System.Windows.Media.Animation.DoubleAnimation
        {
            From = 0,
            To = 1,
            Duration = duration
        };
        BeginAnimation(OpacityProperty, fadeIn);
    }

    /// <summary>
    /// Animates the window closing.
    /// </summary>
    public void AnimateClose(Action? onComplete = null)
    {
        var duration = TimeSpan.FromMilliseconds(100);
        var fadeOut = new System.Windows.Media.Animation.DoubleAnimation
        {
            From = 1,
            To = 0,
            Duration = duration
        };

        fadeOut.Completed += (s, e) =>
        {
            Close();
            onComplete?.Invoke();
        };

        BeginAnimation(OpacityProperty, fadeOut);
    }
}
