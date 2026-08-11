using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using InteractiveWorldMap.Models;
using InteractiveWorldMap.Services;
using InteractiveWorldMap.Utilities;
using InteractiveWorldMap.Views;

namespace InteractiveWorldMap;

public partial class MainWindow
{
    private readonly Dictionary<LocationMarker, Ellipse> _pinHitTargets = new();
    private readonly Dictionary<ClusterMarker, Ellipse> _clusterHitTargets = new();

    private void HandleIndividualMarkerPrimaryAction(LocationMarker marker)
    {
        if (MarkerMouseDownPolicy.GetIndividualMarkerAction(_layoutEditor.IsEditMode) ==
            MarkerMouseDownAction.AllowEditDrag)
            return;

        if (_mode == InteractionMode.Animating)
            return;

        AnimateMarkerClick(marker);
        var location = marker.Location;
        var viewport = MapDisplay.CurrentViewport;
        if (viewport != null && viewport.ZoomLevel <= 1.0)
        {
            if (_visualConfig.AutoOpenSingleLocationContentAfterZoom)
                _autoOpenLocation = location;

            OnClusterClicked(new LocationCluster
            {
                Locations = new List<Location> { location },
                CenterPoint = new Point(location.PixelX, location.PixelY)
            });
            return;
        }

        ShowContentForLocation(location, suppressNextContentActivation: true);
    }

    private void HandleClusterMarkerPrimaryAction(ClusterMarker marker)
    {
        if (MarkerMouseDownPolicy.GetClusterMarkerAction(_layoutEditor.IsEditMode) ==
            MarkerMouseDownAction.BlockNavigation)
        {
            ShowEditModeNavigationBlockedStatus();
            return;
        }

        if (marker.Cluster == null)
            return;

        marker.AnimateClick();
        OnClusterClicked(marker.Cluster);
    }

    private void AnimateMarkerHover(LocationMarker marker, bool isHovered)
    {
        switch (marker.Content)
        {
            case AutoStubPinMarker autoStub:
                autoStub.AnimateHover(isHovered);
                break;
            case ManualLayoutPinMarker manual:
                manual.AnimateHover(isHovered);
                break;
            case CompositePinMarker composite:
                composite.AnimateHover(isHovered);
                break;
            default:
                marker.IsHovered = isHovered;
                break;
        }
    }

    private void RefreshMarkerHitTargets()
    {
        RemoveStaleTargets();

        foreach (var marker in _individualMarkers)
            SyncPinHitTarget(marker);

        foreach (var cluster in _clusterMarkers)
            SyncClusterHitTarget(cluster);
    }

    /// <summary>
    /// Authoritative hit-target sync after a manual layout has been fully applied (all instructions,
    /// depth sort, tip caps). This mirrors <c>UpdateMarkerPositions</c> — the Delete &amp; Recalculate
    /// path — which ends in a single refresh. Without it, saved layouts in dense clusters could leave
    /// pin hit targets drifted off their heads, because the per-marker refreshes inside
    /// <c>ApplyRenderPlanToMarker</c>/<c>RepositionCompositePinMarker</c> run before the post-passes
    /// that can still adjust final geometry. Skipped on the per-frame zoom-animation hot path; the
    /// non-animating settle frame performs the clean sync.
    /// </summary>
    private void RefreshHitTargetsAfterManualLayout()
    {
        if (!IsAnimating)
            RefreshMarkerHitTargets();
    }

    private void SyncPinHitTarget(LocationMarker marker)
    {
        if (!TryGetPinTargetGeometry(marker, out var center, out var visibleDiameter))
        {
            marker.IsHitTestVisible = true;
            RemovePinHitTarget(marker);
            return;
        }

        if (!_pinHitTargets.TryGetValue(marker, out var target))
        {
            target = CreateTarget();
            _pinHitTargets[marker] = target;
            MapDisplay.MarkerInteractions.Children.Add(target);

            target.MouseEnter += (_, _) => AnimateMarkerHover(marker, true);
            target.MouseLeave += (_, _) => AnimateMarkerHover(marker, false);
            target.MouseLeftButtonDown += (_, e) =>
            {
                if (_layoutEditor.IsEditMode)
                    OnMarkerDragStart(marker, e);
                else
                    HandleIndividualMarkerPrimaryAction(marker);
                e.Handled = true;
            };
            target.MouseMove += (_, e) => OnMarkerDragMove(marker, e);
            target.MouseLeftButtonUp += (_, e) => OnMarkerDragEnd(marker, e);
            target.MouseRightButtonUp += (_, e) =>
            {
                if (marker.Content is CompositePinMarker)
                {
                    OnShaftOverrideRequested(marker, marker.Location.Name);
                    e.Handled = true;
                }
            };
        }

        var diameter = MarkerHitTargetGeometry.EffectiveDiameter(
            _visualConfig.MarkerHitTargets.PinDiameterPx,
            visibleDiameter);
        PositionTarget(target, center, diameter, marker);
        marker.IsHitTestVisible = false;
    }

    private void SyncClusterHitTarget(ClusterMarker cluster)
    {
        var left = Canvas.GetLeft(cluster);
        var top = Canvas.GetTop(cluster);
        if (!double.IsFinite(left) || !double.IsFinite(top))
            return;

        var center = MarkerHitTargetGeometry.ToCanvasCenter(
            new Point(left, top),
            new Point(cluster.Width / 2.0, cluster.Height / 2.0));
        var visibleDiameter = System.Math.Max(cluster.Width, cluster.Height);

        if (!_clusterHitTargets.TryGetValue(cluster, out var target))
        {
            target = CreateTarget();
            _clusterHitTargets[cluster] = target;
            MapDisplay.MarkerInteractions.Children.Add(target);
            target.MouseEnter += (_, _) => cluster.AnimateHover(true);
            target.MouseLeave += (_, _) => cluster.AnimateHover(false);
            target.MouseLeftButtonDown += (_, e) =>
            {
                HandleClusterMarkerPrimaryAction(cluster);
                e.Handled = true;
            };
        }

        var diameter = MarkerHitTargetGeometry.EffectiveDiameter(
            _visualConfig.MarkerHitTargets.ClusterDiameterPx,
            visibleDiameter);
        PositionTarget(target, center, diameter, cluster);
        cluster.IsHitTestVisible = false;
    }

    private bool TryGetPinTargetGeometry(
        LocationMarker marker,
        out Point center,
        out double visibleDiameter)
    {
        center = default;
        visibleDiameter = 0;
        var left = Canvas.GetLeft(marker);
        var top = Canvas.GetTop(marker);
        if (!double.IsFinite(left) || !double.IsFinite(top))
            return false;

        Point localCenter;
        switch (marker.Content)
        {
            case AutoStubPinMarker autoStub:
                localCenter = autoStub.GetConnectionPoint();
                visibleDiameter = autoStub.GetHeadDiameter();
                break;
            case ManualLayoutPinMarker manual:
                localCenter = manual.GetConnectionPoint();
                visibleDiameter = manual.GetHeadDiameter();
                break;
            case CompositePinMarker composite when composite.RenderPlan != null:
                localCenter = composite.RenderPlan.HeadCenterLocal;
                visibleDiameter = composite.RenderPlan.HeadDiameterPx;
                break;
            default:
                return false;
        }

        center = MarkerHitTargetGeometry.ToCanvasCenter(
            new Point(left, top),
            localCenter);
        return true;
    }

    private static Ellipse CreateTarget() => new()
    {
        Fill = Brushes.Transparent,
        Cursor = Cursors.Hand
    };

    private static void PositionTarget(
        Ellipse target,
        Point center,
        double diameter,
        FrameworkElement marker)
    {
        target.Width = diameter;
        target.Height = diameter;
        target.Visibility = marker.Visibility;
        target.ToolTip = marker.ToolTip;
        Canvas.SetLeft(target, center.X - (diameter / 2.0));
        Canvas.SetTop(target, center.Y - (diameter / 2.0));
        Panel.SetZIndex(target, Panel.GetZIndex(marker));
    }

    private void RemoveStaleTargets()
    {
        foreach (var marker in _pinHitTargets.Keys
                     .Where(marker => !_individualMarkers.Contains(marker))
                     .ToList())
            RemovePinHitTarget(marker);

        foreach (var marker in _clusterHitTargets.Keys
                     .Where(marker => !_clusterMarkers.Contains(marker))
                     .ToList())
        {
            MapDisplay.MarkerInteractions.Children.Remove(_clusterHitTargets[marker]);
            _clusterHitTargets.Remove(marker);
        }
    }

    private void RemovePinHitTarget(LocationMarker marker)
    {
        if (!_pinHitTargets.Remove(marker, out var target))
            return;

        MapDisplay.MarkerInteractions.Children.Remove(target);
    }

    private void ClearMarkerHitTargets()
    {
        MapDisplay.MarkerInteractions.Children.Clear();
        _pinHitTargets.Clear();
        _clusterHitTargets.Clear();
    }
}
