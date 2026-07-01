using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using InteractiveWorldMap.Models;

namespace InteractiveWorldMap.Views;

/// <summary>
/// Displays location-specific content in a popup window overlay.
/// </summary>
public partial class ContentSubwindow : Window
{
    private const double WindowWidthPercent = 0.37125;  // 37.125% of screen width (increased by 10%)
    private const double WindowHeightPercent = 0.70872; // 70.872% of screen height (increased by 20%)
    private const double MinWindowWidth = 300;
    private const double MinWindowHeight = 200;
    private const double TitleBarHeight = 40;
    private const double ContentPadding = 20;

    /// <summary>
    /// Gets or sets the location associated with this subwindow.
    /// </summary>
    public Location? AssociatedLocation { get; set; }

    /// <summary>
    /// Gets the preferred size of the subwindow.
    /// </summary>
    public Size PreferredSize { get; private set; }

    private string? _currentTranslationText;
    private bool _isTranslationVisible;
    private Rect? _normalBounds;
    private Brush? _normalBackground;
    private Thickness _normalBorderThickness;
    private Thickness _normalPadding;
    private CornerRadius _normalCornerRadius;
    private Effect? _normalEffect;

    public event EventHandler? PresentationModeChanged;

    public bool IsPresentationMode { get; private set; }

    public double MaximizedBackgroundOpacity { get; set; } = 1.0;

    public ContentSubwindow()
    {
        InitializeComponent();
        
        // Start with opacity 0 for animation
        Opacity = 0;
        PreferredSize = new Size(400, 300);
    }

    /// <summary>
    /// Shows content at the specified anchor position.
    /// </summary>
    /// <param name="content">The content to display (ImageSource or string)</param>
    /// <param name="locationName">The associated location name (retained for caller compatibility)</param>
    /// <param name="anchorPosition">The position to anchor the window near</param>
    /// <param name="translationText">Optional translation text for the content</param>
    public void ShowContent(object content, string locationName, Point anchorPosition, string? translationText = null)
    {
        if (content == null)
            throw new ArgumentNullException(nameof(content));

        _currentTranslationText = translationText;
        _isTranslationVisible = false;
        TranslationOverlay.Visibility = Visibility.Collapsed;

        // Show/hide translate button based on whether translation is available
        if (!string.IsNullOrWhiteSpace(translationText))
        {
            TranslateButton.Visibility = Visibility.Visible;
            TranslateButton.Content = "Translate";
            TranslationText.Text = translationText;
        }
        else
        {
            TranslateButton.Visibility = Visibility.Collapsed;
        }

        // Render content based on type and calculate size
        if (content is ImageSource imageSource)
        {
            var image = new Image
            {
                Source = imageSource,
                Stretch = Stretch.Uniform,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            ContentArea.Content = image;

            CalculateContentSize();
        }
        else if (content is string text)
        {
            var textBlock = new TextBlock
            {
                Text = text,
                TextWrapping = TextWrapping.Wrap,
                FontSize = 14,
                LineHeight = 20
            };
            ContentArea.Content = textBlock;

            CalculateContentSize();
        }

        if (!IsPresentationMode)
        {
            Width = PreferredSize.Width;
            Height = PreferredSize.Height;
            PositionWindow(anchorPosition);
        }

        // Show and animate
        Show();
        AnimateOpen();
    }

    public bool TryTogglePresentationMode(Rect ownerBounds)
    {
        if (!IsPresentationMode &&
            (ownerBounds.IsEmpty ||
             ownerBounds.Width <= 0 ||
             ownerBounds.Height <= 0))
        {
            return false;
        }

        if (IsPresentationMode)
            ExitPresentationMode();
        else
            EnterPresentationMode(ownerBounds);

        PresentationModeChanged?.Invoke(this, EventArgs.Empty);
        return true;
    }

    private void EnterPresentationMode(Rect ownerBounds)
    {
        _normalBounds = new Rect(Left, Top, Width, Height);
        _normalBackground = ContentBorder.Background;
        _normalBorderThickness = ContentBorder.BorderThickness;
        _normalPadding = ContentBorder.Padding;
        _normalCornerRadius = ContentBorder.CornerRadius;
        _normalEffect = ContentBorder.Effect;

        var opacity = Math.Clamp(MaximizedBackgroundOpacity, 0.0, 1.0);
        var alpha = (byte)Math.Round(opacity * byte.MaxValue);
        ContentBorder.Background = new SolidColorBrush(
            Color.FromArgb(alpha, 0, 0, 0));
        ContentBorder.BorderThickness = new Thickness(0);
        ContentBorder.Padding = new Thickness(0);
        ContentBorder.CornerRadius = new CornerRadius(0);
        ContentBorder.Effect = null;

        Left = ownerBounds.Left;
        Top = ownerBounds.Top;
        Width = ownerBounds.Width;
        Height = ownerBounds.Height;
        IsPresentationMode = true;
    }

    private void ExitPresentationMode()
    {
        if (_normalBounds is not Rect bounds)
            return;

        Left = bounds.Left;
        Top = bounds.Top;
        Width = bounds.Width;
        Height = bounds.Height;
        ContentBorder.Background = _normalBackground;
        ContentBorder.BorderThickness = _normalBorderThickness;
        ContentBorder.Padding = _normalPadding;
        ContentBorder.CornerRadius = _normalCornerRadius;
        ContentBorder.Effect = _normalEffect;

        _normalBounds = null;
        IsPresentationMode = false;
    }

    private void ContentSurface_MouseLeftButtonUp(
        object sender,
        MouseButtonEventArgs e)
    {
        if (Owner is not Window owner)
            return;

        var ownerWidth = owner.ActualWidth > 0 ? owner.ActualWidth : owner.Width;
        var ownerHeight = owner.ActualHeight > 0 ? owner.ActualHeight : owner.Height;
        var ownerBounds = new Rect(
            owner.Left,
            owner.Top,
            ownerWidth,
            ownerHeight);

        if (TryTogglePresentationMode(ownerBounds))
            e.Handled = true;
    }

    private void TranslateButton_Click(object sender, RoutedEventArgs e)
    {
        _isTranslationVisible = !_isTranslationVisible;
        
        if (_isTranslationVisible)
        {
            TranslationOverlay.Visibility = Visibility.Visible;
            TranslateButton.Content = "Hide Translation";
        }
        else
        {
            TranslationOverlay.Visibility = Visibility.Collapsed;
            TranslateButton.Content = "Translate";
        }
    }

    private void CalculateContentSize()
    {
        var screenWidth = SystemParameters.PrimaryScreenWidth;
        var screenHeight = SystemParameters.PrimaryScreenHeight;
        
        // Calculate target window size based on screen percentages
        var targetWidth = screenWidth * WindowWidthPercent;
        var targetHeight = screenHeight * WindowHeightPercent;
        
        // Ensure minimum dimensions
        targetWidth = Math.Max(MinWindowWidth, targetWidth);
        targetHeight = Math.Max(MinWindowHeight, targetHeight);

        PreferredSize = new Size(targetWidth, targetHeight);
    }

    /// <summary>
    /// Positions the window near the anchor point, avoiding screen edges.
    /// </summary>
    private void PositionWindow(Point anchorPosition)
    {
        var screenWidth = SystemParameters.PrimaryScreenWidth;
        var screenHeight = SystemParameters.PrimaryScreenHeight;

        // Try to center on screen
        var left = (screenWidth - Width) / 2;
        var top = (screenHeight - Height) / 2;

        // Ensure window stays on screen
        left = Math.Max(0, Math.Min(left, screenWidth - Width));
        top = Math.Max(0, Math.Min(top, screenHeight - Height));

        Left = left;
        Top = top;
    }

    /// <summary>
    /// Animates the window opening with fade-in and scale effect.
    /// </summary>
    public void AnimateOpen()
    {
        var duration = TimeSpan.FromMilliseconds(150);

        // Fade in animation
        var fadeIn = new DoubleAnimation
        {
            From = 0,
            To = 1,
            Duration = duration,
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
        };

        // Scale animation
        var scaleTransform = new ScaleTransform(0.9, 0.9, Width / 2, Height / 2);
        ContentBorder.RenderTransform = scaleTransform;

        var scaleAnimation = new DoubleAnimation
        {
            From = 0.9,
            To = 1.0,
            Duration = duration,
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
        };

        BeginAnimation(OpacityProperty, fadeIn);
        scaleTransform.BeginAnimation(ScaleTransform.ScaleXProperty, scaleAnimation);
        scaleTransform.BeginAnimation(ScaleTransform.ScaleYProperty, scaleAnimation);
    }

    /// <summary>
    /// Animates the window closing with fade-out effect.
    /// </summary>
    /// <param name="onComplete">Action to execute when animation completes</param>
    public void AnimateClose(Action? onComplete = null)
    {
        var duration = TimeSpan.FromMilliseconds(100);

        var fadeOut = new DoubleAnimation
        {
            From = 1,
            To = 0,
            Duration = duration,
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn }
        };

        fadeOut.Completed += (s, e) =>
        {
            Close();
            onComplete?.Invoke();
        };

        BeginAnimation(OpacityProperty, fadeOut);
    }

    /// <summary>
    /// Checks if a screen point is within the window bounds.
    /// </summary>
    /// <param name="screenPoint">Screen point to check</param>
    /// <returns>True if the point is within the window</returns>
    public bool ContainsPoint(Point screenPoint)
    {
        var windowBounds = new Rect(Left, Top, Width, Height);
        return windowBounds.Contains(screenPoint);
    }
}
