using System;
using System.Windows;
using InteractiveWorldMap.Models;

namespace InteractiveWorldMap.Services
{
    /// <summary>
    /// Pure placement policy for composite pins: tip-anchor positioning and reposition-only decisions.
    /// </summary>
    public static class CompositePinPlacementPolicy
    {
        public static Point GetCompositeTopLeft(Point tipScreen, CompositePinRenderPlan plan)
        {
            if (plan == null)
                throw new ArgumentNullException(nameof(plan));

            return new Point(
                tipScreen.X - plan.TipAnchorLocal.X,
                tipScreen.Y - plan.TipAnchorLocal.Y);
        }

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

            return GetCompositeTopLeft(tipScreen, plan);
        }

        /// <summary>
        /// Returns true when the existing render plan can stay on screen with only a tip reposition
        /// (no <c>BuildPlan</c> / new <c>CompositePinMarker</c>).
        /// </summary>
        public static bool ShouldRepositionOnly(
            CompositePinRenderPlan? existingPlan,
            PinPlacementTarget newTarget,
            string? preferredPairId = null,
            string? preferredHeadSourcePath = null,
            double toleranceDeg = 0.5,
            double tolerancePx = 0.5)
        {
            if (existingPlan == null || newTarget == null)
                return false;

            if (preferredPairId != null &&
                !string.Equals(preferredPairId, existingPlan.PairId, StringComparison.Ordinal))
            {
                return false;
            }

            if (preferredHeadSourcePath != null &&
                !string.Equals(preferredHeadSourcePath, existingPlan.HeadSourcePath, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var (angleDeg, lengthPx) = GetSegmentAngleAndLength(newTarget);
            if (AngleDifferenceDeg(angleDeg, existingPlan.TargetAngleDeg) > toleranceDeg)
                return false;

            if (Math.Abs(lengthPx - existingPlan.TargetLengthPx) > tolerancePx)
                return false;

            return true;
        }

        internal static (double AngleDeg, double LengthPx) GetSegmentAngleAndLength(PinPlacementTarget target)
        {
            var dx = target.EndScreen.X - target.StartScreen.X;
            var dy = target.EndScreen.Y - target.StartScreen.Y;
            var length = Math.Sqrt((dx * dx) + (dy * dy));
            var angle = Math.Atan2(dx, -dy) * (180.0 / Math.PI);
            if (angle < 0.0)
                angle += 360.0;

            return (angle, length);
        }

        internal static double AngleDifferenceDeg(double a, double b)
        {
            var diff = Math.Abs(a - b) % 360.0;
            if (diff > 180.0)
                diff = 360.0 - diff;

            return diff;
        }
    }
}
