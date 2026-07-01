namespace InteractiveWorldMap.Models;

/// <summary>
/// Configures circular pointer/touch targets for map markers.
/// </summary>
public sealed class MarkerHitTargetConfig
{
    public double PinDiameterPx { get; set; } = 32.0;

    public double ClusterDiameterPx { get; set; } = 40.0;
}
