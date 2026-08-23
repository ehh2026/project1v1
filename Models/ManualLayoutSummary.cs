using System;

namespace InteractiveWorldMap.Models
{
    public sealed record ManualLayoutSummary(
        string GroupKey,
        string VariantId,
        string DisplayName,
        ManualLayoutOrigin Origin,
        DateTime UpdatedUtc,
        bool IsDefault,
        bool IsSelected,
        int MarkerCount)
    {
        /// <summary>
        /// What the variant picker shows. Every generated seed on disk is named "Generated Seed",
        /// so the stored name alone tells the user nothing and every row looks like every other.
        /// Rebuilding the label on read rather than rewriting the files covers what is already
        /// there — the names in existing layouts cannot be assumed to have been chosen.
        ///
        /// The view being edited is named once above the picker, by the edit session, rather than
        /// repeated on every row: the panel is a fixed-width overlay and the rows all belong to
        /// that one view anyway. What rows need is what distinguishes them from each other.
        /// </summary>
        public string Label
        {
            get
            {
                var name = string.IsNullOrWhiteSpace(DisplayName) ? "(unnamed)" : DisplayName.Trim();
                var facts = $"{MarkerCount} pin{(MarkerCount == 1 ? "" : "s")}";
                if (IsDefault) facts += ", default";
                return $"{name} - {facts}";
            }
        }
    }
}
