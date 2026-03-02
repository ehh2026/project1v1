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
    private readonly CoordinateMapper _coordinateMapper;

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
        _coordinateMapper = new CoordinateMapper();

        // Wire up mouse events
        MouseLeftButtonDown += OnMouseLeftButtonDown;
        MouseMove += OnMouseMove;
    }

    /// <summary>
    /// Adds a marker for the specified location.
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

        // Position the marker
        var position = _coordinateMapper.LatLongToScreen(location.Latitude, location.Longitude);
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
    /// Updates the positions of all markers based on current coordinate mapping.
    /// </summary>
    public void UpdateMarkerPositions()
    {
        foreach (var marker in Markers)
        {
            var position = _coordinateMapper.LatLongToScreen(
                marker.Location.Latitude, 
                marker.Location.Longitude);
            
            marker.ScreenPosition = position;
            SetLeft(marker, position.X - marker.Width / 2);
            SetTop(marker, position.Y - marker.Height / 2);
        }
    }

    /// <summary>
    /// Updates the coordinate mapper with new map bounds.
    /// </summary>
    /// <param name="mapBounds">The new map bounds</param>
    public void UpdateMapBounds(Rect mapBounds)
    {
        _coordinateMapper.MapBounds = mapBounds;
        _coordinateMapper.ScreenSize = new Size(ActualWidth, ActualHeight);
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
