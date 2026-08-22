using InteractiveWorldMap.Models;

namespace InteractiveWorldMap.Utilities;

/// <summary>
/// Chooses the one-line status shown under the variant picker.
///
/// The three cases are not interchangeable, and the difference matters: "nothing is saved here" and
/// "something is saved here but this list cannot find it" look identical in an empty dropdown, and
/// only one of them means the user has lost sight of real work.
/// </summary>
public static class VariantStatusDescriber
{
    /// <summary>
    /// Said when a layout is loaded and applied but the picker lists nothing. See
    /// <c>docs/reference/manual-layout-scoping.md</c>, Trap 3: layout lookup falls back to a
    /// compatible key, the variant list does an exact match, so at a window size outside the seeded
    /// ones the map shows a saved layout the dropdown cannot see.
    /// </summary>
    public const string LoadedButUnlisted = "Layout loaded, but not listed at this window size";

    /// <summary>Said only when nothing is saved for the view and nothing is loaded.</summary>
    public const string NothingSaved = "None saved for this view yet";

    /// <param name="active">The variant selected in the picker, if any.</param>
    /// <param name="listedCount">How many variants the picker is showing.</param>
    /// <param name="activeVariantId">
    /// The editor's active variant identity. Non-null means a layout is loaded, whether or not the
    /// picker managed to list it.
    /// </param>
    public static string Describe(ManualLayoutSummary? active, int listedCount, string? activeVariantId)
    {
        if (active != null)
            return $"Loaded: {active.DisplayName} ({active.Origin})";

        if (listedCount > 0)
            return "";

        // An empty list is a fact about the list, not about the file. Only claim nothing is saved
        // when nothing is loaded either -- otherwise the panel contradicts the map.
        return string.IsNullOrEmpty(activeVariantId) ? NothingSaved : LoadedButUnlisted;
    }
}
