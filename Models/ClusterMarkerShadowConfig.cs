namespace InteractiveWorldMap.Models;

/// <summary>
/// Runtime shadow settings for aggregate cluster marker bodies and badges.
/// </summary>
public sealed class ClusterMarkerShadowConfig
{
    public bool Enabled { get; set; } = false;
    public double Opacity { get; set; } = 0.0;
}
