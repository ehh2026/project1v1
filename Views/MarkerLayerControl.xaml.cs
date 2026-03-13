using System;
using System.Collections.Generic;
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
    /// Gets the collection of individual location markers displayed on the map.
    /// </summary>
    public ObservableCollection<LocationMarker> Markers { get; }

    /// <summary>
    /// Gets the collection of cluster markers displayed on the map.
    /// </summary>
    public ObservableCollection<ClusterMarker> ClusterMarkers { get; }

    /// <summary>
    /// Occurs when an individual marker is clicked.
    /// </summary>
    public event EventHandler<LocationClickedEventArgs>? MarkerClicked;

    /// <summary>
    /// Occurs when a cluster marker is clicked.
    /// </summary>
    public event EventHandler<ClusterClickedEventArgs>? ClusterClicked;

    /// <summary>
    /// Occurs when a marker is hovered.
    /// </summary>
    public event EventHandler<MarkerHoverEventArgs>? MarkerHovered;

    public MarkerLayerControl()
    {
        InitializeComponent();
        
        Markers = new ObservableCollection<LocationMarker>();
        ClusterMarkers = new ObservableCollection<ClusterMarker>();

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
    /// Adds a cluster marker to the layer.
    /// </summary>
    /// <param name="cluster">The cluster to add a marker for</param>
    public void AddClusterMarker(LocationCluster cluster)
    {
        if (cluster == null)
            throw new ArgumentNullException(nameof(cluster));

        var marker = new ClusterMarker
        {
            Cluster = cluster
        };

        // Position the marker based on cluster center point
        var mapBounds = _mapBounds.IsEmpty ? new Rect(0, 0, ActualWidth, ActualHeight) : _mapBounds;
        var normalizedX = cluster.CenterPoint.X / _imageWidth;
        var normalizedY = cluster.CenterPoint.Y / _imageHeight;
        
        var x = mapBounds.Left + (normalizedX * mapBounds.Width);
        var y = mapBounds.Top + (normalizedY * mapBounds.Height);
        var position = new Point(x, y);

        marker.ScreenPosition = position;
        SetLeft(marker, position.X - marker.Width / 2);
        SetTop(marker, position.Y - marker.Height / 2);

        marker.UpdateDisplay();

        ClusterMarkers.Add(marker);
        Children.Add(marker);
    }

    /// <summary>
    /// Removes a cluster marker from the layer.
    /// </summary>
    /// <param name="marker">The cluster marker to remove</param>
    public void RemoveClusterMarker(ClusterMarker marker)
    {
        if (marker == null)
            throw new ArgumentNullException(nameof(marker));

        ClusterMarkers.Remove(marker);
        Children.Remove(marker);
    }

    /// <summary>
    /// Clears all cluster markers from the layer.
    /// </summary>
    public void ClearClusterMarkers()
    {
        foreach (var marker in ClusterMarkers.ToList())
        {
            Children.Remove(marker);
        }
        ClusterMarkers.Clear();
    }

    /// <summary>
    /// Clears all individual markers from the layer.
    /// </summary>
    public void ClearMarkers()
    {
        foreach (var marker in Markers.ToList())
        {
            Children.Remove(marker);
        }
        Markers.Clear();
    }

    /// <summary>
    /// Adds markers for a cluster - either a cluster marker or individual markers.
    /// </summary>
    /// <param name="cluster">The cluster to display</param>
    public void AddCluster(LocationCluster cluster)
    {
        if (cluster == null)
            throw new ArgumentNullException(nameof(cluster));

        if (cluster.IsSingleLocation)
        {
            // Show individual marker for single-location clusters
            AddMarker(cluster.Locations[0]);
        }
        else
        {
            // Show cluster marker for multi-location clusters
            AddClusterMarker(cluster);
        }
    }

    /// <summary>
    /// Adds markers for multiple clusters.
    /// </summary>
    /// <param name="clusters">The clusters to display</param>
    public void AddClusters(IEnumerable<LocationCluster> clusters)
    {
        if (clusters == null)
            throw new ArgumentNullException(nameof(clusters));

        foreach (var cluster in clusters)
        {
            AddCluster(cluster);
        }
    }

    /// <summary>
    /// Performs hit testing to find a marker or cluster marker at the specified position.
    /// </summary>
    /// <param name="position">The position to test</param>
    /// <returns>The marker at the position, or null if none found</returns>
    public object? HitTest(Point position)
    {
        var hitResult = VisualTreeHelper.HitTest(this, position);
        
        if (hitResult?.VisualHit == null)
            return null;

        // Walk up the visual tree to find a marker
        DependencyObject current = hitResult.VisualHit;
        while (current != null && current != this)
        {
            if (current is LocationMarker locationMarker)
                return locationMarker;
            
            if (current is ClusterMarker clusterMarker)
                return clusterMarker;
            
            current = VisualTreeHelper.GetParent(current);
        }

        return null;
    }

    /// <summary>
    /// Updates the positions of all markers based on current map bounds and transforms.
    /// </summary>
    public void UpdateMarkerPositions()
    {
        var mapBounds = _mapBounds.IsEmpty ? new Rect(0, 0, ActualWidth, ActualHeight) : _mapBounds;
        
        // Get the parent window to access map transforms
        var window = Window.GetWindow(this);
        if (window is MainWindow mainWindow)
        {
            var scaleTransform = mainWindow.MapDisplay.ScaleTransform;
            var translateTransform = mainWindow.MapDisplay.TranslateTransform;
            
            // Update individual markers
            foreach (var marker in Markers)
            {
                var normalizedX = marker.Location.PixelX / _imageWidth;
                var normalizedY = marker.Location.PixelY / _imageHeight;
                
                var x = mapBounds.Left + (normalizedX * mapBounds.Width);
                var y = mapBounds.Top + (normalizedY * mapBounds.Height);
                
                // Apply transforms
                var transformedX = (x * scaleTransform.ScaleX) + translateTransform.X;
                var transformedY = (y * scaleTransform.ScaleY) + translateTransform.Y;
                
                var position = new Point(transformedX, transformedY);

                marker.ScreenPosition = position;
                SetLeft(marker, position.X - marker.Width / 2);
                SetTop(marker, position.Y - marker.Height / 2);
            }
            
            // Update cluster markers
            foreach (var marker in ClusterMarkers)
            {
                if (marker.Cluster == null)
                    continue;
                    
                var normalizedX = marker.Cluster.CenterPoint.X / _imageWidth;
                var normalizedY = marker.Cluster.CenterPoint.Y / _imageHeight;
                
                var x = mapBounds.Left + (normalizedX * mapBounds.Width);
                var y = mapBounds.Top + (normalizedY * mapBounds.Height);
                
                // Apply transforms
                var transformedX = (x * scaleTransform.ScaleX) + translateTransform.X;
                var transformedY = (y * scaleTransform.ScaleY) + translateTransform.Y;
                
                var position = new Point(transformedX, transformedY);

                marker.ScreenPosition = position;
                SetLeft(marker, position.X - marker.Width / 2);
                SetTop(marker, position.Y - marker.Height / 2);
            }
        }
        else
        {
            // Fallback: no transforms applied
            UpdateMarkerPositionsWithoutTransform();
        }
    }

    /// <summary>
    /// Updates marker positions without applying transforms (fallback method).
    /// </summary>
    private void UpdateMarkerPositionsWithoutTransform()
    {
        var mapBounds = _mapBounds.IsEmpty ? new Rect(0, 0, ActualWidth, ActualHeight) : _mapBounds;
        
        // Update individual markers
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
        
        // Update cluster markers
        foreach (var marker in ClusterMarkers)
        {
            if (marker.Cluster == null)
                continue;
                
            var normalizedX = marker.Cluster.CenterPoint.X / _imageWidth;
            var normalizedY = marker.Cluster.CenterPoint.Y / _imageHeight;
            
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
        var hitObject = HitTest(position);

        if (hitObject is LocationMarker locationMarker)
        {
            locationMarker.AnimateClick();
            MarkerClicked?.Invoke(this, new LocationClickedEventArgs(locationMarker.Location, position));
            e.Handled = true;
        }
        else if (hitObject is ClusterMarker clusterMarker)
        {
            clusterMarker.AnimateClick();
            ClusterClicked?.Invoke(this, new ClusterClickedEventArgs(clusterMarker.Cluster!, position));
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
