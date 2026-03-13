using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using InteractiveWorldMap.Models;
using InteractiveWorldMap.Utilities;

namespace InteractiveWorldMap.Views;

/// <summary>
/// Renders location markers and handles marker interactions.
/// </summary>
public partial class MarkerLayerControl : Canvas
{
    private double _imageWidth = 16397;
    private double _imageHeight = 11085;
    private Rect _mapBounds = Rect.Empty;

    /// <summary>
    /// Gets the collection of markers displayed on the map.
    /// </summary>
    public ObservableCollection<LocationMarker> Markers { get; }

    /// <summary>
    /// Occurs when a marker is clicked.
    /// </summary>
    public event EventHandler<LocationClickedEventArgs>? MarkerClicked;

    /// <summary>
    /// Occurs when a marker is hovered.
    /// </summary>
    public event EventHandler<MarkerHoverEventArgs>? MarkerHovered;

    public MarkerLayerControl()
    {
        InitializeComponent();
        
        Markers = new ObservableCollection<LocationMarker>();

        // Wire up mouse events
        MouseLeftButtonDown += OnMouseLeftButtonDown;
        MouseMove += OnMouseMove;
    }

    /// <summary>
    /// Adds a marker for the specified location using pixel coordinates.
    /// </summary>
    /// <param name="location">The location to add a marker for</param>
    public void AddMarker(Location location)
    {
        if (location == null)
            throw new ArgumentNullException(nameof(location));

        var marker = new LocationMarker
        {
            Location = location
        };

        // Position the marker based on pixel coordinates
        var mapBounds = _mapBounds.IsEmpty ? new Rect(0, 0, ActualWidth, ActualHeight) : _mapBounds;
        var normalizedX = location.PixelX / _imageWidth;
        var normalizedY = location.PixelY / _imageHeight;
        
        var x = mapBounds.Left + (normalizedX * mapBounds.Width);
        var y = mapBounds.Top + (normalizedY * mapBounds.Height);
        var position = new Point(x, y);

        marker.ScreenPosition = position;
        SetLeft(marker, position.X - marker.Width / 2);
        SetTop(marker, position.Y - marker.Height / 2);

        Markers.Add(marker);
        Children.Add(marker);
    }

    /// <summary>
    /// Removes a marker from the layer.
    /// </summary>
    /// <param name="marker">The marker to remove</param>
    public void RemoveMarker(LocationMarker marker)
    {
        if (marker == null)
            throw new ArgumentNullException(nameof(marker));

        Markers.Remove(marker);
        Children.Remove(marker);
    }

    /// <summary>
    /// Performs hit testing to find a marker at the specified position.
    /// </summary>
    /// <param name="position">The position to test</param>
    /// <returns>The marker at the position, or null if none found</returns>
    public LocationMarker? HitTest(Point position)
    {
        var hitResult = VisualTreeHelper.HitTest(this, position);
        
        if (hitResult?.VisualHit == null)
            return null;

        // Walk up the visual tree to find a LocationMarker
        DependencyObject current = hitResult.VisualHit;
        while (current != null && current != this)
        {
            if (current is LocationMarker marker)
                return marker;
            
            current = VisualTreeHelper.GetParent(current);
        }

        return null;
    }

    /// <summary>
    /// Updates the positions of all markers based on current map bounds.
    /// </summary>
    public void UpdateMarkerPositions()
    {
        var mapBounds = _mapBounds.IsEmpty ? new Rect(0, 0, ActualWidth, ActualHeight) : _mapBounds;
        
        foreach (var marker in Markers)
        {
            var normalizedX = marker.Location.PixelX / _imageWidth;
            var normalizedY = marker.Location.PixelY / _imageHeight;
            
            var x = mapBounds.Left + (normalizedX * mapBounds.Width);
            var y = mapBounds.Top + (normalizedY * mapBounds.Height);
            var position = new Point(x, y);

            marker.ScreenPosition = position;
            SetLeft(marker, position.X - marker.Width / 2);
            SetTop(marker, position.Y - marker.Height / 2);
        }
    }

    /// <summary>
    /// Updates the map bounds for marker positioning.
    /// </summary>
    /// <param name="mapBounds">The new map bounds</param>
    public void UpdateMapBounds(Rect mapBounds)
    {
        _mapBounds = mapBounds;
        UpdateMarkerPositions();
    }

    private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        var position = e.GetPosition(this);
        var marker = HitTest(position);

        if (marker != null)
        {
            marker.AnimateClick();
            MarkerClicked?.Invoke(this, new LocationClickedEventArgs(marker.Location, position));
            e.Handled = true;
        }
    }

    private void OnMouseMove(object sender, MouseEventArgs e)
    {
        var position = e.GetPosition(this);
        var marker = HitTest(position);

        if (marker != null)
        {
            MarkerHovered?.Invoke(this, new MarkerHoverEventArgs(marker, true));
        }
    }
}
