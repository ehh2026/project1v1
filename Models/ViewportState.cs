using System;
using System.Windows;

namespace InteractiveWorldMap.Models
{
    /// <summary>
    /// Represents the current viewport state for displaying a portion of the map image.
    /// Coordinates are in source image space (0 to SourceImageWidth/Height).
    /// </summary>
    public class ViewportState
    {
        /// <summary>
        /// Full source image width in pixels.
        /// </summary>
        public double SourceImageWidth { get; set; }

        /// <summary>
        /// Full source image height in pixels.
        /// </summary>
        public double SourceImageHeight { get; set; }

        /// <summary>
        /// Top-left X coordinate of viewport in source image space.
        /// </summary>
        public double ViewportX { get; set; }

        /// <summary>
        /// Top-left Y coordinate of viewport in source image space.
        /// </summary>
        public double ViewportY { get; set; }

        /// <summary>
        /// Width of viewport rectangle in source image space.
        /// </summary>
        public double ViewportWidth { get; set; }

        /// <summary>
        /// Height of viewport rectangle in source image space.
        /// </summary>
        public double ViewportHeight { get; set; }

        /// <summary>
        /// Current zoom level (1.0 = full map visible, 3.5 = zoomed in 3.5x).
        /// </summary>
        public double ZoomLevel { get; set; }

        /// <summary>
        /// Gets the viewport rectangle as an Int32Rect for use with CroppedBitmap.
        /// Clamps to valid integer bounds within the source image.
        /// </summary>
        public System.Windows.Int32Rect GetSourceRect()
        {
            var x = Math.Max(0, (int)Math.Floor(ViewportX));
            var y = Math.Max(0, (int)Math.Floor(ViewportY));
            var width = Math.Min((int)Math.Ceiling(ViewportWidth), (int)SourceImageWidth - x);
            var height = Math.Min((int)Math.Ceiling(ViewportHeight), (int)SourceImageHeight - y);

            // Ensure minimum size of 1x1
            width = Math.Max(1, width);
            height = Math.Max(1, height);

            return new System.Windows.Int32Rect(x, y, width, height);
        }

        /// <summary>
        /// Converts source image coordinates to screen coordinates within the viewport.
        /// </summary>
        /// <param name="sourceX">X coordinate in source image space</param>
        /// <param name="sourceY">Y coordinate in source image space</param>
        /// <param name="containerWidth">Width of the display container</param>
        /// <param name="containerHeight">Height of the display container</param>
        /// <returns>Screen coordinates as a Point</returns>
        public Point SourceToScreen(double sourceX, double sourceY, double containerWidth, double containerHeight)
        {
            // Calculate position relative to viewport
            var relativeX = sourceX - ViewportX;
            var relativeY = sourceY - ViewportY;

            // Scale to screen coordinates
            var scaleX = containerWidth / ViewportWidth;
            var scaleY = containerHeight / ViewportHeight;

            return new Point(relativeX * scaleX, relativeY * scaleY);
        }

        /// <summary>
        /// Converts screen coordinates to source image coordinates.
        /// </summary>
        /// <param name="screenX">X coordinate in screen space</param>
        /// <param name="screenY">Y coordinate in screen space</param>
        /// <param name="containerWidth">Width of the display container</param>
        /// <param name="containerHeight">Height of the display container</param>
        /// <returns>Source image coordinates as a Point</returns>
        public Point ScreenToSource(double screenX, double screenY, double containerWidth, double containerHeight)
        {
            // Scale from screen to viewport coordinates
            var scaleX = ViewportWidth / containerWidth;
            var scaleY = ViewportHeight / containerHeight;

            // Add viewport offset
            var sourceX = (screenX * scaleX) + ViewportX;
            var sourceY = (screenY * scaleY) + ViewportY;

            return new Point(sourceX, sourceY);
        }

        /// <summary>
        /// Creates a viewport state for the full map view (unzoomed).
        /// </summary>
        public static ViewportState CreateFullMapView(double sourceWidth, double sourceHeight, double containerWidth, double containerHeight)
        {
            // Calculate aspect ratios
            var sourceAspect = sourceWidth / sourceHeight;
            var containerAspect = containerWidth / containerHeight;

            double viewportWidth, viewportHeight;

            if (sourceAspect > containerAspect)
            {
                // Source is wider - fit to width
                viewportWidth = sourceWidth;
                viewportHeight = sourceWidth / containerAspect;
            }
            else
            {
                // Source is taller - fit to height
                viewportHeight = sourceHeight;
                viewportWidth = sourceHeight * containerAspect;
            }

            // Center the viewport
            var viewportX = (sourceWidth - viewportWidth) / 2.0;
            var viewportY = (sourceHeight - viewportHeight) / 2.0;

            return new ViewportState
            {
                SourceImageWidth = sourceWidth,
                SourceImageHeight = sourceHeight,
                ViewportX = viewportX,
                ViewportY = viewportY,
                ViewportWidth = viewportWidth,
                ViewportHeight = viewportHeight,
                ZoomLevel = 1.0
            };
        }

        /// <summary>
        /// Creates a viewport state zoomed to a specific point in the source image.
        /// </summary>
        public static ViewportState CreateZoomedView(double centerX, double centerY, double zoomLevel, 
            double sourceWidth, double sourceHeight, double containerWidth, double containerHeight)
        {
            // Calculate the viewport size at this zoom level
            var fullViewport = CreateFullMapView(sourceWidth, sourceHeight, containerWidth, containerHeight);
            var viewportWidth = fullViewport.ViewportWidth / zoomLevel;
            var viewportHeight = fullViewport.ViewportHeight / zoomLevel;

            // Center on the specified point
            var viewportX = centerX - (viewportWidth / 2.0);
            var viewportY = centerY - (viewportHeight / 2.0);

            var state = new ViewportState
            {
                SourceImageWidth = sourceWidth,
                SourceImageHeight = sourceHeight,
                ViewportX = viewportX,
                ViewportY = viewportY,
                ViewportWidth = viewportWidth,
                ViewportHeight = viewportHeight,
                ZoomLevel = zoomLevel
            };

            // Clamp to ensure viewport stays within bounds
            ClampViewport(state);

            return state;
        }

        /// <summary>
        /// Clamps the viewport to ensure it stays within the source image bounds.
        /// </summary>
        private static void ClampViewport(ViewportState state)
        {
            // Ensure viewport doesn't extend beyond image bounds
            if (state.ViewportX < 0)
                state.ViewportX = 0;
            if (state.ViewportY < 0)
                state.ViewportY = 0;

            if (state.ViewportX + state.ViewportWidth > state.SourceImageWidth)
                state.ViewportX = state.SourceImageWidth - state.ViewportWidth;
            if (state.ViewportY + state.ViewportHeight > state.SourceImageHeight)
                state.ViewportY = state.SourceImageHeight - state.ViewportHeight;

            // Final safety check
            if (state.ViewportX < 0)
            {
                state.ViewportWidth += state.ViewportX;
                state.ViewportX = 0;
            }
            if (state.ViewportY < 0)
            {
                state.ViewportHeight += state.ViewportY;
                state.ViewportY = 0;
            }
        }

        /// <summary>
        /// Creates a copy of this viewport state.
        /// </summary>
        public ViewportState Clone()
        {
            return new ViewportState
            {
                SourceImageWidth = SourceImageWidth,
                SourceImageHeight = SourceImageHeight,
                ViewportX = ViewportX,
                ViewportY = ViewportY,
                ViewportWidth = ViewportWidth,
                ViewportHeight = ViewportHeight,
                ZoomLevel = ZoomLevel
            };
        }
    }
}
