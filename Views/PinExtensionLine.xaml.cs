using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace InteractiveWorldMap.Views
{
    /// <summary>
    /// Visual representation of a pin extension line that looks like a metal pin shaft.
    /// </summary>
    public partial class PinExtensionLine : UserControl
    {
        public PinExtensionLine()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Sets the line endpoints and styling to look like a pin shaft.
        /// </summary>
        public void SetLine(Point start, Point end, Color shaftColor, double thickness)
        {
            // Main shaft line
            PinShaftLine.X1 = start.X;
            PinShaftLine.Y1 = start.Y;
            PinShaftLine.X2 = end.X;
            PinShaftLine.Y2 = end.Y;
            PinShaftLine.Stroke = new SolidColorBrush(shaftColor);
            PinShaftLine.StrokeThickness = thickness;

            // Highlight line for metallic effect (slightly offset)
            double offsetX = (end.Y - start.Y) * 0.1; // Perpendicular offset
            double offsetY = (start.X - end.X) * 0.1;
            
            HighlightLine.X1 = start.X + offsetX;
            HighlightLine.Y1 = start.Y + offsetY;
            HighlightLine.X2 = end.X + offsetX;
            HighlightLine.Y2 = end.Y + offsetY;
            HighlightLine.StrokeThickness = thickness * 0.3;
        }

        /// <summary>
        /// Gets the main shaft line element for animation.
        /// </summary>
        public Line ShaftLine => PinShaftLine;

        /// <summary>
        /// Gets the highlight line element.
        /// </summary>
        public Line Highlight => HighlightLine;
    }
}