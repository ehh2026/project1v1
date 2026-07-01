using System.Windows.Media;
using InteractiveWorldMap.Views;

namespace InteractiveWorldMap
{
    public partial class MainWindow
    {
        private void SetDrawnPinRole(LocationMarker marker, DrawnPinRole role)
        {
            if (!_visualConfig.UsePinMarkers || marker.Content is CompositePinMarker)
                return;

            if ((role == DrawnPinRole.AutoStub && marker.Content is AutoStubPinMarker) ||
                (role == DrawnPinRole.ManualLayout && marker.Content is ManualLayoutPinMarker))
                return;

            if (marker.Content is not AutoStubPinMarker &&
                marker.Content is not ManualLayoutPinMarker)
                return;

            Color? color = marker.Content switch
            {
                AutoStubPinMarker autoStub => autoStub.PinColor,
                ManualLayoutPinMarker manual => manual.PinColor,
                _ => null
            };

            var content = _drawnPinFactory.Create(role, color);
            marker.Content = content;
            marker.Width = content.Width;
            marker.Height = content.Height;
            marker.Tag = content;
            RefreshMarkerHitTargets();
        }

        private bool TryApplyCompositeOrPrepareDrawnManual(
            LocationMarker marker,
            System.Windows.Point originalScreen,
            System.Windows.Point extendedScreen)
        {
            if (TryApplyCompositePinMarker(marker, originalScreen, extendedScreen))
                return true;

            SetDrawnPinRole(marker, DrawnPinRole.ManualLayout);
            return false;
        }
    }
}
