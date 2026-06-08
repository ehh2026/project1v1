namespace InteractiveWorldMap.Models
{
    /// <summary>
    /// DTO for a single shaft candidate in the right-click shaft override context menu.
    /// </summary>
    public sealed record CompositePinShaftMenuItem(
        string PairId,
        string Label,
        double Score,
        bool IsSelected);
}
