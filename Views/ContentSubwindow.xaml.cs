using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using InteractiveWorldMap.Models;

namespace InteractiveWorldMap.Views;

/// <summary>
/// Displays location-specific content in a popup window overlay.
/// </summary>
public partial class ContentSubwindow : Window
{
    private const double MaxWindowWidth = 800;
    private const double MaxWindowHeight = 600;
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
    /// <param name="locationName">The name of the location</param>
    /// <param name="anchorPosition">The position to anchor the window near</param>
    public void ShowContent(object content, string locationName, Point anchorPosition)
    {
        if (content == null)
            throw new ArgumentNullException(nameof(content));

        TitleText.Text = locationName ?? "Location";

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

            // Calculate window size based on image dimensions
            CalculateSizeForImage(imageSource);
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

            // Calculate window size based on text length
            CalculateSizeForText(text);
        }

        // Apply the calculated size
        Width = PreferredSize.Width;
        Height = PreferredSize.Height;

        // Position the window
        PositionWindow(anchorPosition);

        // Show and animate
        Show();
        AnimateOpen();
    }

    /// <summary>
    /// Calculates the optimal window size for an image.
    /// </summary>
    private void CalculateSizeForImage(ImageSource imageSource)
    {
        var imageWidth = imageSource.Width;
        var imageHeight = imageSource.Height;

        // Calculate aspect ratio
        var aspectRatio = imageWidth / imageHeight;

        // Start with image dimensions plus padding and title
        var targetWidth = imageWidth + ContentPadding;
        var targetHeight = imageHeight + TitleBarHeight + ContentPadding;

        // Constrain to max dimensions while preserving aspect ratio
        if (targetWidth > MaxWindowWidth)
        {
            targetWidth = MaxWindowWidth;
            targetHeight = (targetWidth - ContentPadding) / aspectRatio + TitleBarHeight + ContentPadding;
        }

        if (targetHeight > MaxWindowHeight)
        {
            targetHeight = MaxWindowHeight;
            targetWidth = (targetHeight - TitleBarHeight - ContentPadding) * aspectRatio + ContentPadding;
        }

        // Ensure minimum dimensions
        targetWidth = Math.Max(MinWindowWidth, targetWidth);
        targetHeight = Math.Max(MinWindowHeight, targetHeight);

        PreferredSize = new Size(targetWidth, targetHeight);
    }

    /// <summary>
    /// Calculates the optimal window size for text content.
    /// </summary>
    private void CalculateSizeForText(string text)
    {
        // Estimate size based on text length
        var charCount = text.Length;
        
        // Rough estimate: 10 chars per line, 20px per line
        var estimatedLines = Math.Max(5, Math.Min(20, charCount / 50));
        var estimatedHeight = estimatedLines * 20 + TitleBarHeight + ContentPadding;
        
        var targetWidth = 400;
        var targetHeight = Math.Min(MaxWindowHeight, Math.Max(MinWindowHeight, estimatedHeight));

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
