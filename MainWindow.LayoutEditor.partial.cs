using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Controls.Primitives;
using System.Windows.Shapes;
using InteractiveWorldMap.Models;
using InteractiveWorldMap.Services;
using InteractiveWorldMap.Utilities;
using InteractiveWorldMap.Views;
using IOPath = System.IO.Path;

namespace InteractiveWorldMap
{
    public partial class MainWindow
    {
        // Layout-key derivation lives in MainWindow.LayoutEditorKeys.partial.cs.

        private void UpdateEditLayoutButtonVisibility()
        {
            if (!AreDeveloperToolsEnabled() || !_visualConfig.ManualLayoutEditor.Enabled || _layoutEditor.IsEditMode)
            {
                EditLayoutButton.Visibility = Visibility.Collapsed;
                return;
            }

            EditLayoutButton.Visibility =
                IsFullMapRootView() || _currentZoomedCluster != null
                    ? Visibility.Visible
                    : Visibility.Collapsed;
        }

        private void ShowEditModeNavigationBlockedStatus()
        {
            _logger.LogInfo("Navigation blocked while edit mode is active");
            if (EditModePanel.Visibility == Visibility.Visible)
            {
                EditModeStatusText.Text = "Exit edit mode to zoom";
                EditModeStatusText.Foreground = new SolidColorBrush(Color.FromRgb(255, 215, 0));
            }
        }

        private bool TryApplyFullMapManualLayout()
        {
            if (_layoutEditor.IsEditMode || !IsFullMapRootView())
                return false;

            // User unloaded the layout this session: keep markers auto-placed and do not re-apply.
            if (_layoutEditor.IsManualLayoutSuppressed)
            {
                _layoutEditor.SetManualLayoutActive(false);
                UpdateEditLayoutButtonVisibility();
                return false;
            }

            var key = GenerateCurrentFullMapGroupKey();
            _layoutEditor.SetLayoutKey(key);

            var layout = _layoutEditor.TryLoad(key);
            if (layout == null)
            {
                _layoutEditor.SetManualLayoutActive(false);
                UpdateEditLayoutButtonVisibility();
                return false;
            }

            _logger.LogInfo($"[TryApplyFullMapManualLayout] Replaying full-map layout for key={key}");
            ApplyManualLayout(layout);
            _layoutEditor.SetManualLayoutActive(true);
            UpdateEditLayoutButtonVisibility();
            return true;
        }

        private void WireLayoutEditorEvents()
        {
            _layoutEditor.EditModeEntered += () =>
            {
                _mode = InteractionMode.Editing;
                EditLayoutButton.Visibility = Visibility.Collapsed;
                EditModePanel.Visibility = Visibility.Visible;
                UpdateOverrideIndicator(); // hide indicator while in edit mode
            };

            _layoutEditor.EditModeExited += () =>
            {
                if (_mode == InteractionMode.Editing)
                {
                    _mode = InteractionMode.Normal;
                }

                EditModePanel.Visibility = Visibility.Collapsed;
                UpdateEditLayoutButtonVisibility();
                UpdateOverrideIndicator(); // re-evaluate indicator now that edit mode is off
            };

            _layoutEditor.ManualLayoutActivityChanged += isActive =>
            {
                ManualLayoutIndicator.Visibility =
                    isActive &&
                    AreDeveloperToolsEnabled() &&
                    _visualConfig.ManualLayoutEditor.ShowLayoutIndicator
                        ? Visibility.Visible
                        : Visibility.Collapsed;
            };

            _layoutEditor.VariantsChanged += variants => PopulateVariantPicker(variants);
            VariantPickerComboBox.SelectionChanged += OnVariantPickerSelectionChanged;
        }

        // ─── Variant picker helpers ───────────────────────────────────────────

        private void PopulateVariantPicker(IReadOnlyList<ManualLayoutSummary> variants)
        {
            VariantPickerComboBox.SelectionChanged -= OnVariantPickerSelectionChanged;
            VariantPickerComboBox.Items.Clear();
            foreach (var s in variants) VariantPickerComboBox.Items.Add(s);
            VariantPickerComboBox.SelectedItem = variants.FirstOrDefault(s => s.VariantId == _layoutEditor.ActiveVariantId);
            VariantPickerComboBox.SelectionChanged += OnVariantPickerSelectionChanged;
            UpdateVariantUI();
        }

        private void UpdateVariantUI()
        {
            var active = VariantPickerComboBox.SelectedItem as ManualLayoutSummary;
            VariantStatusText.Text = active != null ? $"Loaded: {active.DisplayName} ({active.Origin})" : "";
            DeleteVariantButton.IsEnabled = active?.Origin == ManualLayoutOrigin.Manual;
        }

        private void OnVariantPickerSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (VariantPickerComboBox.SelectedItem is ManualLayoutSummary s && s.VariantId != _layoutEditor.ActiveVariantId)
                SwitchToVariantInEditor(s.VariantId);
        }

        private void SwitchToVariantInEditor(string variantId)
        {
            var layout = _layoutEditor.SwitchToVariant(variantId);
            if (layout == null) return;
            if (!CanUseCompositePins())
                RestoreBaseMarkerVisuals();
            _extensionLineRenderer.Clear();
            ApplyManualLayout(layout);
            UpdateVariantUI();
        }

        private void OnSaveAsVariantButtonClick(object sender, RoutedEventArgs e)
        {
            SaveAsNameTextBox.Text = "";
            SaveAsInputRow.Visibility = Visibility.Visible;
            SaveAsNameTextBox.Focus();
        }

        private void OnSaveAsCancelButtonClick(object sender, RoutedEventArgs e) =>
            SaveAsInputRow.Visibility = Visibility.Collapsed;

        private void OnSaveAsNameKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter) OnSaveAsConfirmButtonClick(sender, new RoutedEventArgs());
            else if (e.Key == Key.Escape) SaveAsInputRow.Visibility = Visibility.Collapsed;
        }

        private async void OnSaveAsConfirmButtonClick(object sender, RoutedEventArgs e)
        {
            SaveAsInputRow.Visibility = Visibility.Collapsed;
            var name = SaveAsNameTextBox.Text.Trim();
            if (string.IsNullOrEmpty(name)) return;
            var status = TryCollectCurrentExtensions(out var extensions, out var blocked);
            if (status != ExtensionCollectionStatus.Ok)
            {
                ReportCollectionFailure(status, blocked);
                await ResetEditModeStatusAfterDelayAsync(3000);
                return;
            }

            var assignments = _assignmentEnricher.GetAssignments(extensions, _compositePinPlanningService);
            bool ok = _layoutEditor.TrySaveAsVariant(name, extensions, assignments);
            EditModeStatusText.Text = ok ? "✓ VARIANT SAVED" : "✗ SAVE FAILED";
            EditModeStatusText.Foreground = ok ? new SolidColorBrush(Color.FromRgb(50, 205, 50)) : new SolidColorBrush(Colors.Red);
            await ResetEditModeStatusAfterDelayAsync(2000);
        }

        private void OnDeleteVariantButtonClick(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show("Delete this variant?", "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
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

        // Marker capture for saving lives in MainWindow.LayoutEditorGeometry.partial.cs
        // (TryCollectCurrentExtensions). Every save path must go through it — see the note there.

        #region Manual Layout Editor Methods

        /// <summary>
        /// Handles Edit Layout button click - enters edit mode.
        /// </summary>
        private void OnEditLayoutButtonClick(object sender, RoutedEventArgs e)
        {
            if (!AreDeveloperToolsEnabled())
            {
                _logger.LogInfo("[EditLayout] Developer tools are disabled; ignored.");
                EditLayoutButton.Visibility = Visibility.Collapsed;
                return;
            }

            if (_currentZoomedCluster == null)
            {
                if (!TrySetFullMapLayoutKey(editSession: true))
                {
                    _logger.LogWarning("Cannot enter full-map layout edit - full-map viewport is not ready");
                    return;
                }
            }
            else
            {
                ClearFullMapLayoutSession();

                // Re-derive the cluster key from the view on screen. Inheriting whatever
                // CurrentLayoutKey held would let a "fullmap" key set by the zoom animation
                // survive into a cluster edit session, and the save would then overwrite the
                // full-map layout with this cluster's handful of markers.
                var editViewport = MapDisplay.CurrentViewport;
                if (editViewport == null)
                {
                    _logger.LogWarning("Cannot enter cluster layout edit - viewport is not ready");
                    return;
                }

                _layoutEditor.SetLayoutKey(LayoutKeyGenerator.DeriveEditSessionKey(
                    _currentZoomedCluster.Locations,
                    editViewport,
                    _visualConfig.RadialExtension));
                _logger.LogInfo($"[OnEditLayoutButtonClick] Derived cluster layout key={_layoutEditor.CurrentLayoutKey}");
            }

            // Capture the scope once, immutably. Saves will read from this instead of re-deriving
            // and re-checking the ambient key; during the migration both exist and must agree.
            var session = TryBuildEditSession();
            if (session == null)
            {
                _logger.LogWarning("Cannot enter layout edit - viewport is not ready for a session");
                return;
            }
            _layoutEditor.BeginEditSession(session);

            // Entering the editor re-places markers against the current viewport, so any staleness
            // from a previous session is resolved here.
            ResetEditSessionGeometryState();

            _layoutEditor.EnterEditMode();

            // If a manual layout is saved, restore those positions for draggable editing.
            // Phase 4: when composite rendering is active, skip RestoreBaseMarkerVisuals so
            // composite pins remain composite during editing.
            // Load the saved layout when one is active OR was unloaded this session (so opening the
            // editor reloads a layout the user previously unloaded). SetManualLayoutActive(true)
            // below clears the suppression flag.
            bool loadedSaved = false;
            if ((_layoutEditor.IsManualLayoutActive || _layoutEditor.IsManualLayoutSuppressed) &&
                _layoutEditor.CurrentLayoutKey != null)
            {
                var layout = _layoutEditor.TryLoad(_layoutEditor.CurrentLayoutKey);
                if (layout != null)
                {
                    if (!CanUseCompositePins())
                        RestoreBaseMarkerVisuals();
                    _extensionLineRenderer.Clear();
                    ApplyManualLayout(layout);
                    _layoutEditor.SetManualLayoutActive(true);
                    _logger.LogInfo($"[OnEditLayoutButtonClick] Restored saved layout for key={_layoutEditor.CurrentLayoutKey}");
                    loadedSaved = true;
                }
            }
            if (!loadedSaved)
                UpdateMarkerPositions();

            var visibleMarkers = _individualMarkers.Count(m => m.Visibility == Visibility.Visible);
            _logger.LogInfo($"[OnEditLayoutButtonClick] Entering edit mode");
            _logger.LogInfo($"  Visible markers: {visibleMarkers}");
            _logger.LogInfo($"  Extension lines: {_extensionLineRenderer.LineCount}");
            _logger.LogInfo($"  Marker-to-line mappings: {_extensionLineRenderer.MarkerMappingCount}");

            // Enable dragging on all visible markers
            foreach (var marker in _individualMarkers.Where(m => m.Visibility == Visibility.Visible))
            {
                marker.Cursor = Cursors.Hand;
                marker.MouseLeftButtonDown += OnMarkerDragStart;
                marker.MouseMove += OnMarkerDragMove;
                marker.MouseLeftButtonUp += OnMarkerDragEnd;

                _logger.LogInfo(_extensionLineRenderer.HasLine(marker)
                    ? $"    Marker '{marker.Location.Name}' has line"
                    : $"    Marker '{marker.Location.Name}' has NO line");
            }

            _logger.LogInfo("Edit mode activated");

            // Populate variant picker with the loaded group's variants.
            PopulateVariantPicker(_layoutEditor.GetVariants());
        }

        /// <summary>
        /// Handles Save Layout button click - saves current marker positions.
        /// If the active variant is AutoSeed, redirects to the inline Save As prompt.
        /// </summary>
        private async void OnSaveLayoutButtonClick(object sender, RoutedEventArgs e)
        {
            if (_layoutEditor.CurrentLayoutKey == null ||
                (_currentZoomedCluster == null && !IsFullMapLayoutSessionActive()))
            {
                _logger.LogWarning("Cannot save layout - no layout key or active layout session");
                return;
            }

            // Scope and geometry are verified inside TryCollectCurrentExtensions below, which both
            // this handler and Save As share. Do not re-add a separate check here: two copies of
            // this guard is how the Save As path ended up unprotected.

            // If editing an AutoSeed layout, redirect to the Save As prompt.
            if (_layoutEditor.ActiveVariantOrigin == ManualLayoutOrigin.AutoSeed)
            {
                OnSaveAsVariantButtonClick(sender, e);
                return;
            }

            try
            {
                var viewport = MapDisplay.CurrentViewport;
                if (viewport == null)
                {
                    _logger.LogWarning("Cannot save layout - viewport is null");
                    return;
                }

                // Single guarded capture path — shared with Save As. See
                // TryCollectCurrentExtensions in MainWindow.LayoutEditorGeometry.partial.cs.
                var status = TryCollectCurrentExtensions(out var extensions, out var blocked);
                if (status != ExtensionCollectionStatus.Ok)
                {
                    ReportCollectionFailure(status, blocked);
                    await ResetEditModeStatusAfterDelayAsync(3000);
                    return;
                }

                // Validate layout before saving
                var validationIssues = _layoutEditor.ValidateLayout(extensions);
                if (validationIssues.Count > 0)
                {
                    _logger.LogWarning($"Layout validation found {validationIssues.Count} issues:");
                    foreach (var issue in validationIssues)
                        _logger.LogWarning($"  - {issue}");

                    // Show warning but allow save
                    EditModeStatusText.Text = $"⚠ {validationIssues.Count} Issues Found";
                    EditModeStatusText.Foreground = new SolidColorBrush(Color.FromRgb(255, 165, 0));

                }

                // Capture shaft/head assignments from the session plan cache before saving.
                var assignments = _assignmentEnricher.GetAssignments(extensions, _compositePinPlanningService);

                // Save (controller sets IsManualLayoutActive and logs)
                _layoutEditor.TrySave(extensions, assignments);

                // Phase 4: invalidate cached plans so next render builds fresh ones.
                if (_layoutEditor.CurrentLayoutKey != null)
                    _planApplicationService.InvalidateGroup(_layoutEditor.CurrentLayoutKey);

                // Pending overrides are now persisted — clear them and hide the indicator.
                _overrideStore.ClearOverrides();
                UpdateOverrideIndicator();

                // Show confirmation (unless we just showed a warning)
                if (validationIssues.Count == 0)
                {
                    EditModeStatusText.Text = "✓ LAYOUT SAVED";
                    EditModeStatusText.Foreground = new SolidColorBrush(Color.FromRgb(50, 205, 50));

                }

                await ResetEditModeStatusAfterDelayAsync(validationIssues.Count > 0 ? 3000 : 2000);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to save layout: {ex.Message}");
                EditModeStatusText.Text = "✗ SAVE FAILED";
                EditModeStatusText.Foreground = new SolidColorBrush(Colors.Red);
            }
        }

        /// <summary>
        /// Handles Delete & Recalculate button click - removes saved layout and recalculates.
        /// </summary>
        private void OnDeleteLayoutButtonClick(object sender, RoutedEventArgs e)
        {
            if (_layoutEditor.CurrentLayoutKey == null ||
                (_currentZoomedCluster == null && !IsFullMapLayoutSessionActive()))
            {
                _logger.LogWarning("Cannot delete layout - no layout key or active layout session");
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
            if (_layoutEditor.CurrentLayoutKey == null ||
                (_currentZoomedCluster == null && !IsFullMapLayoutSessionActive()))
            {
                _logger.LogWarning("Cannot unload layout - no layout key or active layout session");
                return;
            }

            try
            {
                var wasFullMapSession = IsFullMapLayoutSessionActive();

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

                _logger.LogInfo($"Unloaded manual layout (kept on disk) for key={_layoutEditor.CurrentLayoutKey}");
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

        /// <summary>
        /// Exits edit mode and restores normal interaction.
        /// </summary>
        private void ExitEditMode()
        {
            var wasFullMapSession = IsFullMapLayoutSessionActive();
            _layoutEditor.ExitEditMode();
            _layoutEditor.EndEditSession();

            // Disable dragging on all markers
            foreach (var marker in _individualMarkers)
            {
                marker.Cursor = Cursors.Arrow;
                marker.MouseLeftButtonDown -= OnMarkerDragStart;
                marker.MouseMove -= OnMarkerDragMove;
                marker.MouseLeftButtonUp -= OnMarkerDragEnd;
            }

            _draggedMarker = null;

            _logger.LogInfo("Edit mode deactivated");

            // If a manual layout is active, replay it so composite pins appear at the saved positions.
            if (_layoutEditor.IsManualLayoutActive && _layoutEditor.CurrentLayoutKey != null)
            {
                var layout = _layoutEditor.TryLoad(_layoutEditor.CurrentLayoutKey);
                if (layout != null)
                {
                    _logger.LogInfo($"[ExitEditMode] Replaying manual layout for key={_layoutEditor.CurrentLayoutKey}");
                    ApplyManualLayout(layout);
                    if (wasFullMapSession)
                        ClearFullMapLayoutSession();
                    return;
                }
            }

            // Auto path: no manual layout saved yet (or load failed).
            UpdateMarkerPositions();
            if (wasFullMapSession)
                ClearFullMapLayoutSession();
        }

        /// <summary>
        /// Applies a saved manual layout to the current view.
        /// Phase 4: checks the composite render-plan disk cache before building plans;
        /// saves plans to cache on a miss.
        /// </summary>
        private void ApplyManualLayout(ManualLayout layout)
        {
            _logger.LogInfo($"[ApplyManualLayout] Applying layout with {layout.Markers.Count} markers");

            var groupKey = _layoutEditor.CurrentLayoutKey ?? layout.GroupKey;

            // 2.1: on the zoom-animation hot path, keep the existing extension-line pairs and
            // reposition them in place each frame (see the RequiresExtensionLine branch below)
            // instead of clearing and re-creating every Line/Brush/Effect. The settle frame runs
            // with IsAnimating == false, so it still does a clean rebuild.
            if (!IsAnimating)
                _extensionLineRenderer.Clear();

            var visibleMarkers = _individualMarkers
                .Where(m => m.Visibility == Visibility.Visible)
                .GroupBy(m => m.Location.Name)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);

            var viewport = MapDisplay.CurrentViewport;
            var cw = MapDisplay.ActualWidth;
            var ch = MapDisplay.ActualHeight;

            var sourceCoords = visibleMarkers.ToDictionary(
                kvp => kvp.Key,
                kvp => (kvp.Value.Location.PixelX, kvp.Value.Location.PixelY),
                StringComparer.Ordinal);

            var applications = _layoutEditor.CreateLayoutApplications(layout, visibleMarkers.Keys);
            var geometryPath = _contentLoader.ResolvePinPartPath(_visualConfig.PinParts.GeometryMetadataPath);

            // Full-map reference viewport: source-space pin heads are projected at this fixed fit
            // scale so the shaft keeps a constant screen length at any zoom (still resize-aware,
            // since the fit scale tracks the window). Without it, a zoomed-in single-location pin's
            // shaft stretched with the zoom factor.
            var fullMapViewport = (cw > 0 && ch > 0)
                ? ViewportState.CreateFullMapView(ImageWidth, ImageHeight, cw, ch)
                : null;

            var applyPlan = _planApplicationService.BuildApplyInstructions(
                layout,
                applications,
                sourceCoords,
                viewport,
                cw,
                ch,
                _visualConfig.PinParts,
                groupKey ?? string.Empty,
                geometryPath,
                CanUseCompositePins() && _pinPartGeometryHash != null,
                fullMapViewport);

            foreach (var instruction in applyPlan.Instructions)
            {
                ApplyManualLayoutInstruction(instruction, visibleMarkers);
            }
            // TryRepositionPinLine(marker...) is used inside ApplyManualLayoutInstruction during animation.
            if (applyPlan.ShouldSaveToCache && !string.IsNullOrEmpty(groupKey))
            {
                _planApplicationService.SaveIfMissed(
                    applyPlan.CacheKey,
                    groupKey,
                    layout.VariantId,
                    layout.Markers.Select(m => m.LocationName));
            }

            if (_overrideStore.HasPendingOverrides && !_layoutEditor.IsEditMode)
                ReapplyPendingOverrides();

            ApplyCompositePinDepthSort();
            UpdatePinTipCaps();
            RefreshHitTargetsAfterManualLayout();
        }

        private void ApplyManualLayoutInstruction(
            ManualLayoutApplyInstruction instruction,
            IReadOnlyDictionary<string, LocationMarker> visibleMarkers)
        {
            if (!visibleMarkers.TryGetValue(instruction.LocationName, out var marker))
                return;

            if (TryApplyCachedManualLayoutInstruction(marker, instruction))
                return;

            if (TryApplyCompositeManualLayoutInstruction(marker, instruction))
                return;

            ApplyDrawnManualLayoutInstruction(marker, instruction);
        }

        private bool TryApplyCachedManualLayoutInstruction(
            LocationMarker marker,
            ManualLayoutApplyInstruction instruction)
        {
            if (instruction.CachedPlan == null ||
                !_baseMarkerVisuals.TryGetValue(marker, out var baseState) ||
                !IsPinStyleMarkerBase(baseState.Content))
            {
                return false;
            }

            var shaftImage = LoadPinPartBitmap(instruction.CachedPlan.ShaftSourcePath);
            var headImage = LoadPinPartBitmap(instruction.CachedPlan.HeadSourcePath);
            if (shaftImage == null || headImage == null)
                return false;

            ApplyRenderPlanToMarker(
                marker,
                instruction.OriginalScreen,
                instruction.ExtendedScreen,
                instruction.CachedPlan,
                shaftImage,
                headImage);
            AddEditModeGuideLine(marker, instruction);
            return true;
        }

        private bool TryApplyCompositeManualLayoutInstruction(
            LocationMarker marker,
            ManualLayoutApplyInstruction instruction)
        {
            if (!TryApplyCompositePinMarker(
                    marker,
                    instruction.OriginalScreen,
                    instruction.ExtendedScreen,
                    instruction.PairId,
                    instruction.HeadSourcePath))
            {
                return false;
            }

            AddEditModeGuideLine(marker, instruction);
            return true;
        }

        private void ApplyDrawnManualLayoutInstruction(
            LocationMarker marker,
            ManualLayoutApplyInstruction instruction)
        {
            if (instruction.RequiresExtensionLine)
            {
                ApplyDrawnManualExtensionInstruction(marker, instruction);
            }
            else
            {
                ApplyAutoStubInstruction(marker, instruction);
            }
        }

        private void ApplyDrawnManualExtensionInstruction(
            LocationMarker marker,
            ManualLayoutApplyInstruction instruction)
        {
            SetDrawnPinRole(marker, DrawnPinRole.ManualLayout);
            if (!IsAnimating ||
                !_extensionLineRenderer.TryRepositionPinLine(marker, instruction.OriginalScreen, instruction.ExtendedScreen))
            {
                _extensionLineRenderer.AddLine(marker, instruction.OriginalScreen, instruction.ExtendedScreen);
            }
            _extensionLineRenderer.AnchorExtendedMarker(marker, instruction.ExtendedScreen);
        }

        private void ApplyAutoStubInstruction(LocationMarker marker, ManualLayoutApplyInstruction instruction)
        {
            SetDrawnPinRole(marker, DrawnPinRole.AutoStub);
            if (marker.Content is AutoStubPinMarker autoStub)
            {
                var tip = autoStub.GetShaftTipPoint();
                Canvas.SetLeft(marker, instruction.OriginalScreen.X - tip.X);
                Canvas.SetTop(marker, instruction.OriginalScreen.Y - tip.Y);
            }
        }

        private void AddEditModeGuideLine(LocationMarker marker, ManualLayoutApplyInstruction instruction)
        {
            if (_layoutEditor.IsEditMode)
                _extensionLineRenderer.AddLine(marker, instruction.OriginalScreen, instruction.ExtendedScreen);
        }
        private async Task ResetEditModeStatusAfterDelayAsync(int delayMs)
        {
            await Task.Delay(delayMs);
            EditModeStatusText.Text = "EDIT MODE ACTIVE";
            EditModeStatusText.Foreground = new SolidColorBrush(Color.FromRgb(255, 215, 0));
        }

        #endregion

    }
}
