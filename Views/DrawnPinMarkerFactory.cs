using System.Windows.Controls;
using System.Windows.Media;
using InteractiveWorldMap.Models;

namespace InteractiveWorldMap.Views
{
    public enum DrawnPinRole
    {
        AutoStub,
        ManualLayout
    }

    public sealed class DrawnPinMarkerFactory
    {
        private readonly VisualConfig _visualConfig;

        public DrawnPinMarkerFactory(VisualConfig visualConfig)
        {
            _visualConfig = visualConfig;
        }

        public UserControl Create(DrawnPinRole role, Color? pinColor = null)
        {
            UserControl marker = role switch
            {
                DrawnPinRole.AutoStub => new AutoStubPinMarker(_visualConfig),
                DrawnPinRole.ManualLayout => new ManualLayoutPinMarker(_visualConfig),
                _ => new AutoStubPinMarker(_visualConfig)
            };

            if (pinColor.HasValue)
            {
                switch (marker)
                {
                    case AutoStubPinMarker autoStub:
                        autoStub.PinColor = pinColor.Value;
                        break;
                    case ManualLayoutPinMarker manual:
                        manual.PinColor = pinColor.Value;
                        break;
                }
            }

            return marker;
        }
    }
}
