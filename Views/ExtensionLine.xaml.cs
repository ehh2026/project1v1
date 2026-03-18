using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace InteractiveWorldMap.Views
{
    /// <summary>
    /// Visual representation of a radial extension line.
    /// </summary>
    public partial class ExtensionLine : UserControl
    {
        public ExtensionLine()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Sets the line endpoints and styling.
        /// </summary>
        public void SetLine(Point start, Point end, Color strokeColor, double thickness)
        {
            LineElement.X1 = start.X;
            LineElement.Y1 = start.Y;
            LineElement.X2 = end.X;
            LineElement.Y2 = end.Y;
            LineElement.Stroke = new SolidColorBrush(strokeColor);
            LineElement.StrokeThickness = thickness;
        }

        /// <summary>
        /// Gets the underlying Line element for animation.
        /// </summary>
        public Line Line => LineElement;
    }
}
