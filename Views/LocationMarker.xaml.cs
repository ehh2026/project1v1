using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;
using InteractiveWorldMap.Models;

namespace InteractiveWorldMap.Views;

/// <summary>
/// Visual representation of a clickable location on the map.
/// </summary>
public partial class LocationMarker : UserControl
{
    private bool _isHovered;

    /// <summary>
    /// Gets or sets the location associated with this marker.
    /// </summary>
    public Location Location { get; set; } = null!;

    /// <summary>
    /// Gets or sets the screen position of this marker.
    /// </summary>
    public Point ScreenPosition { get; set; }

    /// <summary>
    /// Gets or sets whether the marker is currently hovered.
    /// </summary>
    public bool IsHovered
    {
        get => _isHovered;
        set
        {
            if (_isHovered != value)
            {
                _isHovered = value;
                AnimateHover(value);
            }
        }
    }

    public LocationMarker()
    {
        InitializeComponent();
        
        // Wire up mouse events
        MouseEnter += (s, e) => IsHovered = true;
        MouseLeave += (s, e) => IsHovered = false;
    }

    /// <summary>
    /// Animates the marker on hover state change.
    /// </summary>
    /// <param name="isEntering">True if entering hover, false if leaving</param>
    public void AnimateHover(bool isEntering)
    {
        var targetScale = isEntering ? 1.2 : 1.0;
        var duration = TimeSpan.FromMilliseconds(200);

        var scaleXAnimation = new DoubleAnimation
        {
            To = targetScale,
            Duration = duration,
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
        };

        var scaleYAnimation = new DoubleAnimation
        {
            To = targetScale,
            Duration = duration,
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
        };

        MarkerScale.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleXProperty, scaleXAnimation);
        MarkerScale.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleYProperty, scaleYAnimation);
    }

    /// <summary>
    /// Animates the marker on click.
    /// </summary>
    public void AnimateClick()
    {
        var duration = TimeSpan.FromMilliseconds(100);

        // Pulse effect: scale up briefly then back to normal
        var storyboard = new Storyboard();

        var scaleUpX = new DoubleAnimation
        {
            To = 1.3,
            Duration = TimeSpan.FromMilliseconds(50),
            AutoReverse = true
        };

        var scaleUpY = new DoubleAnimation
        {
            To = 1.3,
            Duration = TimeSpan.FromMilliseconds(50),
            AutoReverse = true
        };

        Storyboard.SetTarget(scaleUpX, MarkerScale);
        Storyboard.SetTargetProperty(scaleUpX, new PropertyPath(System.Windows.Media.ScaleTransform.ScaleXProperty));
        
        Storyboard.SetTarget(scaleUpY, MarkerScale);
        Storyboard.SetTargetProperty(scaleUpY, new PropertyPath(System.Windows.Media.ScaleTransform.ScaleYProperty));

        storyboard.Children.Add(scaleUpX);
        storyboard.Children.Add(scaleUpY);
        storyboard.Begin();
    }

    /// <summary>
    /// Checks if a point is within the marker bounds.
    /// </summary>
    /// <param name="point">Point to check in marker's coordinate space</param>
    /// <returns>True if the point is within the marker</returns>
    public bool ContainsPoint(Point point)
    {
        // Check if point is within the circular marker
        var center = new Point(Width / 2, Height / 2);
        var radius = Width / 2;
        var distance = Math.Sqrt(Math.Pow(point.X - center.X, 2) + Math.Pow(point.Y - center.Y, 2));
        return distance <= radius;
    }
}
