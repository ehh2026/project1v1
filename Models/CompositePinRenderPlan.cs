using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;

namespace InteractiveWorldMap.Models
{
    public class CompositePinLayerPlan
    {
        public string SourcePath { get; set; } = string.Empty;
        public int SourceWidth { get; set; }
        public int SourceHeight { get; set; }
        public List<Point> ClipPolygon { get; set; } = new List<Point>();
        public Matrix Transform { get; set; } = Matrix.Identity;
    }

    public class CompositePinRenderPlan
    {
        public string PairId { get; set; } = string.Empty;
        public string ShaftSourcePath { get; set; } = string.Empty;
        public string HeadSourcePath { get; set; } = string.Empty;
        public double Width { get; set; }
        public double Height { get; set; }
        public double TargetAngleDeg { get; set; }
        public double TargetLengthPx { get; set; }
        public double HeadRotationDeg { get; set; }
        public double BodyStretchFactor { get; set; }
        public double StretchBodyLengthPx { get; set; }
        public Point TipAnchorLocal { get; set; }
        public Point JoinAnchorLocal { get; set; }
        public Point StretchStartLocal { get; set; }
        public Point StretchEndLocal { get; set; }
        public Point HeadAttachLocal { get; set; }
        public Point HeadCenterLocal { get; set; }
        public CompositePinLayerPlan ShaftTipCapLayer { get; set; } = new CompositePinLayerPlan();
        public CompositePinLayerPlan ShaftBodyLayer { get; set; } = new CompositePinLayerPlan();
        public CompositePinLayerPlan ShaftHeadCapLayer { get; set; } = new CompositePinLayerPlan();
        public CompositePinLayerPlan HeadLayer { get; set; } = new CompositePinLayerPlan();
    }
}
