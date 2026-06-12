using System;
using System.Windows;
using InteractiveWorldMap.Models;

namespace InteractiveWorldMap.Services
{
    public static class CompositePinPlacementPolicy
    {
        public static Point GetCompositeTopLeft(
            MarkerScreenPlacement placement,
            double locationMarkerSize,
            CompositePinRenderPlan plan)
        {
            if (plan == null)
                throw new ArgumentNullException(nameof(plan));

            var locationMarkerRadius = locationMarkerSize / 2.0;
            var tipScreen = new Point(
                placement.Left + locationMarkerRadius,
                placement.Top + locationMarkerRadius);

            return new Point(
                tipScreen.X - plan.TipAnchorLocal.X,
                tipScreen.Y - plan.TipAnchorLocal.Y);
        }
    }
}
