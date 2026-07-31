using System;
using InteractiveWorldMap.Models;

namespace InteractiveWorldMap.Services
{
    /// <summary>
    /// Calculates viewport states for zoom and pan operations.
    /// </summary>
    public class ViewportCalculator
    {
        /// <summary>
        /// Interpolates between two viewport states for smooth animation.
        /// Maintains aspect ratio and provides linear visual scaling.
        /// Pan speed is adjusted for zoom level to maintain constant visual velocity.
        /// </summary>
        /// <param name="start">Starting viewport state</param>
        /// <param name="end">Ending viewport state</param>
        /// <param name="progress">Animation progress (0.0 to 1.0)</param>
        /// <returns>Interpolated viewport state</returns>
        public ViewportState Interpolate(ViewportState start, ViewportState end, double progress)
        {
            // Clamp progress to [0, 1]
            progress = Math.Max(0.0, Math.Min(1.0, progress));

            // Interpolate zoom level linearly for controlled zoom feel
            var currentZoom = Lerp(start.ZoomLevel, end.ZoomLevel, progress);

            // Calculate viewport size based on interpolated zoom level
            // This ensures aspect ratio is maintained and scaling is visually linear
            var baseViewportWidth = start.ViewportWidth * start.ZoomLevel; // Full map viewport width
            var baseViewportHeight = start.ViewportHeight * start.ZoomLevel; // Full map viewport height

            var currentViewportWidth = baseViewportWidth / currentZoom;
            var currentViewportHeight = baseViewportHeight / currentZoom;

            // For pan interpolation, we want constant VISUAL velocity, not constant source velocity
            // To achieve this, we need to account for the zoom level when interpolating position

            // Calculate start and end centers
            var startCenterX = start.ViewportX + (start.ViewportWidth / 2.0);
            var startCenterY = start.ViewportY + (start.ViewportHeight / 2.0);
            var endCenterX = end.ViewportX + (end.ViewportWidth / 2.0);
            var endCenterY = end.ViewportY + (end.ViewportHeight / 2.0);

            // Calculate the total distance to travel in source coordinates
            var totalDeltaX = endCenterX - startCenterX;
            var totalDeltaY = endCenterY - startCenterY;

            // To maintain constant visual velocity, we need to adjust pan speed based on zoom
            // The idea: when more zoomed in, pan slower in source coordinates
            // We'll use a weighted interpolation where the weight accounts for zoom level

            // Calculate the "visual distance" traveled by integrating 1/zoom over the path
            // For linear zoom interpolation, this gives us a logarithmic position curve
            // But we'll use a simpler approximation: weight by average zoom factor

            // Simple approach: interpolate in "zoom-normalized" space
            // This makes pan speed inversely proportional to zoom level
            var startZoomWeight = 1.0 / start.ZoomLevel;
            var endZoomWeight = 1.0 / end.ZoomLevel;
            var totalZoomWeight = startZoomWeight + endZoomWeight;

            // Calculate weighted progress for position
            // This makes panning slower when zoomed in, maintaining visual velocity
            var currentZoomWeight = 1.0 / currentZoom;
            var normalizedProgress = (startZoomWeight - currentZoomWeight) / (startZoomWeight - endZoomWeight);
            normalizedProgress = Math.Max(0.0, Math.Min(1.0, normalizedProgress));

            // Interpolate center using zoom-adjusted progress
            var currentCenterX = Lerp(startCenterX, endCenterX, normalizedProgress);
            var currentCenterY = Lerp(startCenterY, endCenterY, normalizedProgress);

            // Calculate viewport position from center
            var currentViewportX = currentCenterX - (currentViewportWidth / 2.0);
            var currentViewportY = currentCenterY - (currentViewportHeight / 2.0);

            return new ViewportState
            {
                SourceImageWidth = start.SourceImageWidth,
                SourceImageHeight = start.SourceImageHeight,
                ViewportX = currentViewportX,
                ViewportY = currentViewportY,
                ViewportWidth = currentViewportWidth,
                ViewportHeight = currentViewportHeight,
                ZoomLevel = currentZoom
            };
        }

        /// <summary>
        /// Calculates a viewport centered on a specific point with the given zoom level.
        /// </summary>
        /// <param name="centerX">X coordinate in source image space to center on</param>
        /// <param name="centerY">Y coordinate in source image space to center on</param>
        /// <param name="zoomLevel">Desired zoom level</param>
        /// <param name="sourceWidth">Full source image width</param>
        /// <param name="sourceHeight">Full source image height</param>
        /// <param name="containerWidth">Display container width</param>
        /// <param name="containerHeight">Display container height</param>
        /// <returns>Viewport state centered on the specified point</returns>
        public ViewportState CalculateZoomToPoint(double centerX, double centerY, double zoomLevel,
            double sourceWidth, double sourceHeight, double containerWidth, double containerHeight)
        {
            return ViewportState.CreateZoomedView(centerX, centerY, zoomLevel,
                sourceWidth, sourceHeight, containerWidth, containerHeight);
        }

        /// <summary>
        /// Linear interpolation between two values.
        /// </summary>
        private double Lerp(double start, double end, double progress)
        {
            return start + (end - start) * progress;
        }
    }
}
