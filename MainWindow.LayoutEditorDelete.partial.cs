using System;
using System.Linq;
using System.Windows;
using InteractiveWorldMap.Models;

namespace InteractiveWorldMap
{
    public partial class MainWindow
    {
        // The three ways to stop a saved layout being used, kept together because the only thing
        // separating them is how much they destroy: nothing, one variant, or all of them. Reading
        // them side by side is the point -- the defect this file was split out of was the most
        // destructive one wearing the mildest label and asking nothing before it ran.

        /// <summary>
        /// Renders a variant name safely inside a confirmation dialog. Names reach the file through
        /// Save As, but manual-layouts.json is documented as hand-editable, so a name can contain
        /// newlines or control characters. In a delete prompt that is not cosmetic: a name carrying
        /// its own line breaks can push the real warning out of view, or fake one. Collapse the
        /// whitespace, drop the control characters, and bound the length.
        /// </summary>
        private static string FormatVariantNameForPrompt(string name)
        {
            var cleaned = new string(name.Select(c => char.IsControl(c) ? ' ' : c).ToArray());
            cleaned = System.Text.RegularExpressions.Regex.Replace(cleaned, @"\s+", " ").Trim();

            if (cleaned.Length == 0) return "(unnamed)";
            return cleaned.Length > 80 ? cleaned.Substring(0, 77) + "..." : cleaned;
        }

        private void OnDeleteVariantButtonClick(object sender, RoutedEventArgs e)
        {
            // Name the variant. "Delete this variant?" gave no way to tell, before committing to
            // it, whether the thing about to be destroyed was the one on screen.
            var name = _layoutEditor.ActiveVariantDisplayName ?? _layoutEditor.ActiveVariantId;
            if (name == null)
            {
                _logger.LogWarning("Cannot delete variant - none is active");
                return;
            }

            var confirmed = MessageBox.Show(
                $"Delete the saved layout \"{FormatVariantNameForPrompt(name)}\"?\n\n" +
                "Only this one is deleted. Any other saved layouts for this view are kept.\n" +
                "This cannot be undone.",
                "Delete Layout",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning,
                MessageBoxResult.No);
            if (confirmed != MessageBoxResult.Yes) return;

            bool ok = _layoutEditor.TryDeleteActiveVariant();
            if (!ok) return;
            var nextId = _layoutEditor.ActiveVariantId;
            if (nextId != null) SwitchToVariantInEditor(nextId);
            else
            {
                UpdateMarkerPositions();
                if (IsFullMapLayoutSessionActive())
                    TryApplyFullMapManualLayout();
            }
        }
        /// <summary>
        /// Handles the bulk delete button click - destroys every hand-made layout saved for this
        /// view, then recalculates. The single-variant counterpart is
        /// <see cref="OnDeleteVariantButtonClick"/>; the non-destructive one is
        /// <see cref="OnUnloadLayoutButtonClick"/>.
        /// </summary>
        private void OnDeleteLayoutButtonClick(object sender, RoutedEventArgs e)
        {
            if (_layoutEditor.ActiveSession == null ||
                (_currentZoomedCluster == null && !IsFullMapLayoutSessionActive()))
            {
                _logger.LogWarning("Cannot delete layout - no layout key or active layout session");
                return;
            }

            // This button was labelled "Delete and Recalculate" and destroyed every hand-made
            // layout for the view with no confirmation at all — the whole point of the phase.
            // The controller decides what counts as deletable, so the set named here and the set
            // TryDelete removes cannot drift apart.
            var doomed = _layoutEditor.GetDeletableVariants();

            if (doomed.Count == 0)
            {
                _logger.LogInfo("Bulk delete requested but no hand-made layouts exist for this view");
                MessageBox.Show(
                    "There are no hand-made layouts saved for this view.\n\n" +
                    "To go back to automatic placement without deleting anything, " +
                    "use \"Unload and Recalculate\".",
                    "Nothing to Delete",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            var names = string.Join(
                "\n",
                doomed.Select(v => "    " + FormatVariantNameForPrompt(v.DisplayName ?? v.VariantId)));

            var confirmed = MessageBox.Show(
                $"Delete ALL {doomed.Count} saved layout(s) for this view?\n\n" +
                names + "\n\n" +
                "Every one of them is destroyed, not just the one on screen. This cannot be undone.\n\n" +
                "To go back to automatic placement while keeping these, cancel and use " +
                "\"Unload and Recalculate\" instead.",
                $"Delete All {doomed.Count} Saved Layouts",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning,
                MessageBoxResult.No);
            if (confirmed != MessageBoxResult.Yes)
            {
                _logger.LogInfo($"Bulk delete of {doomed.Count} variant(s) cancelled at the confirmation");
                return;
            }

            try
            {
                var wasFullMapSession = IsFullMapLayoutSessionActive();

                // Delete saved layout (controller sets IsManualLayoutActive and logs)
                _layoutEditor.TryDelete();

                // Clear any pending overrides — layout is gone.
                _overrideStore.ClearAll();
                UpdateOverrideIndicator();

                // Exit edit mode
                ExitEditMode();

                // Recalculate positions
                if (wasFullMapSession)
                {
                    UpdateMarkerPositions();
                    TryApplyFullMapManualLayout();
                }
                else if (_currentZoomedCluster != null)
                {
                    ShowZoomedView(_currentZoomedCluster);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to delete layout: {ex.Message}");
            }
        }
        /// <summary>
        /// Handles Unload Layout button click - reverts to auto-placement for this session while
        /// leaving the saved layout file on disk untouched (non-destructive counterpart to
        /// Delete &amp; Recalculate). The layout returns on the next edit or app restart.
        /// </summary>
        private void OnUnloadLayoutButtonClick(object sender, RoutedEventArgs e)
        {
            if (_layoutEditor.ActiveSession == null ||
                (_currentZoomedCluster == null && !IsFullMapLayoutSessionActive()))
            {
                _logger.LogWarning("Cannot unload layout - no layout key or active layout session");
                return;
            }

            try
            {
                var wasFullMapSession = IsFullMapLayoutSessionActive();

                // Capture the scope before ExitEditMode ends the session: the completion log below
                // is the only record of which saved group was suppressed, and reading it afterwards
                // would always report null.
                var sessionKey = _layoutEditor.ActiveSession.LayoutKey;

                // Suppress for this session; the saved JSON stays on disk.
                _layoutEditor.UnloadManualLayout();

                // Drop pending in-session edits — nothing is lost, the file is untouched.
                _overrideStore.ClearAll();
                UpdateOverrideIndicator();

                ExitEditMode();

                // Revert to auto-placement. The auto-apply paths are now no-ops (suppressed), so
                // markers stay auto-placed instead of reloading the saved layout.
                if (wasFullMapSession)
                {
                    UpdateMarkerPositions();
                    TryApplyFullMapManualLayout();
                }
                else if (_currentZoomedCluster != null)
                {
                    ShowZoomedView(_currentZoomedCluster);
                }

                _logger.LogInfo($"Unloaded manual layout (kept on disk) for key={sessionKey}");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to unload layout: {ex.Message}");
            }
        }

        /// <summary>
        /// Handles Exit Edit Mode button click - exits edit mode without saving.
        /// </summary>
        private void OnExitEditModeButtonClick(object sender, RoutedEventArgs e)
        {
            ExitEditMode();
        }
    }
}
