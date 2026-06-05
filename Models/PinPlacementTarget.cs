using System.Windows;

namespace InteractiveWorldMap.Models
{
    public class PinPlacementTarget
    {
        public Point StartScreen { get; set; }
        public Point EndScreen { get; set; }
        public string LocationId { get; set; } = string.Empty;
        public int GroupId { get; set; }
    }

    public class PinPartPlacementResult
    {
        public string PairId { get; set; } = string.Empty;
        public PinPartGeometryEntry PairGeometry { get; set; } = new PinPartGeometryEntry();
        public double TargetAngleDeg { get; set; }
        public double TargetLengthPx { get; set; }
        public double NativeAngleDeg { get; set; }
        public double NativeLengthPx { get; set; }
        public double RequestedRotationDeg { get; set; }
        public double AppliedRotationDeg { get; set; }
        public double RequestedStretchFactor { get; set; }
        public double AppliedStretchFactor { get; set; }
        public bool IsRotationClamped { get; set; }
        public bool IsStretchClamped { get; set; }
        public double Score { get; set; }
    }
}
