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
        /// </summary>
        /// <param name="start">Starting viewport state</param>
        /// <param name="end">Ending viewport state</param>
        /// <param name="progress">Animation progress (0.0 to 1.0)</param>
        /// <returns>Interpolated viewport state</returns>
        public ViewportState Interpolate(ViewportState start, ViewportState end, double progress)
        {
            // Clamp progress to [0, 1]
            progress = Math.Max(0.0, Math.Min(1.0, progress));

            // Linear interpolation for all viewport properties
            return new ViewportState
            {
                SourceImageWidth = start.SourceImageWidth,
                SourceImageHeight = start.SourceImageHeight,
                ViewportX = Lerp(start.ViewportX, end.ViewportX, progress),
                ViewportY = Lerp(start.ViewportY, end.ViewportY, progress),
                ViewportWidth = Lerp(start.ViewportWidth, end.ViewportWidth, progress),
                ViewportHeight = Lerp(start.ViewportHeight, end.ViewportHeight, progress),
                ZoomLevel = Lerp(start.ZoomLevel, end.ZoomLevel, progress)
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
