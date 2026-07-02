using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using InteractiveWorldMap.Models;
using InteractiveWorldMap.Services;
using InteractiveWorldMap.Views;

namespace InteractiveWorldMap
{
    public partial class MainWindow
    {
        private bool _isTuningBusy;
        private PinPartVariantCatalog _variantCatalog = null!;

        private void SetupTuningPanel()
        {
            TuningPanelToggleBtn.Visibility = AreDeveloperToolsEnabled() && _visualConfig.Debug.EnableTuningPanel
                ? Visibility.Visible
                : Visibility.Collapsed;

            DeveloperTuningPanel.Visibility = Visibility.Collapsed;
            _variantCatalog = new PinPartVariantCatalog(_logger);
            RefreshTuningPanelVariantOptions(_visualConfig.PinParts.ShaftAssetVariant, _visualConfig.PinParts.HeadAssetVariant);
            DeveloperTuningPanel.LoadValues(_visualConfig);
        }

        private void RefreshTuningPanelVariantOptions(string? shaftToInclude = null, string? headToInclude = null)
        {
            var shafts = _variantCatalog.ListVariants(
                _contentLoader.ContentFolderPath,
                _visualConfig.PinParts.PartsFolderPath,
                "shaft_variants",
                shaftToInclude);

            var heads = _variantCatalog.ListVariants(
                _contentLoader.ContentFolderPath,
                _visualConfig.PinParts.PartsFolderPath,
                "head_variants",
                headToInclude);

            DeveloperTuningPanel.SetVariantOptions(shafts, heads);
        }

        private void OnTuningPanelToggleClick(object sender, RoutedEventArgs e)
        {
            if (!AreDeveloperToolsEnabled() || !_visualConfig.Debug.EnableTuningPanel)
                return;

            TuningPanelToggleBtn.ContextMenu.PlacementTarget = TuningPanelToggleBtn;
            TuningPanelToggleBtn.ContextMenu.IsOpen = true;
        }

        private void OnTuningCategoryClick(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem item &&
                item.Tag is string value &&
                Enum.TryParse<TuningCategory>(value, out var category))
            {
                ShowTuningCategory(category);
            }
        }

        private void ShowTuningCategory(TuningCategory category)
        {
            if (DeveloperTuningPanel.Visibility == Visibility.Visible &&
                DeveloperTuningPanel.VisibleCategory == category)
            {
                DeveloperTuningPanel.Visibility = Visibility.Collapsed;
                return;
            }

            DeveloperTuningPanel.ShowCategory(category);
            DeveloperTuningPanel.Visibility = Visibility.Visible;
        }

        private async void OnApplyTuning(object? sender, TuningPanelEventArgs e)
        {
            try
            {
                await ApplyTuningAsync(e);
            }
            catch (Exception ex)
            {
                _logger.LogError($"[Tuning] Unhandled apply error: {ex.Message}");
                DeveloperTuningPanel.SetStatus("Apply failed; see log for details.");
            }
        }

        private void OnSaveTuningToDisk(object? sender, EventArgs e)
        {
            if (!CanRunTuningAction("save"))
                return;

            try
            {
                _configService.Save(_visualConfig, _configPath);
                DeveloperTuningPanel.SetStatus($"Saved tuning values to {_configPath}.");
                _logger.LogInfo($"[Tuning] Saved visual config to {_configPath}");
            }
            catch (Exception ex)
            {
                _logger.LogError($"[Tuning] Failed to save visual config: {ex.Message}");
                DeveloperTuningPanel.SetStatus("Save failed; see log for details.");
            }
        }

        private async void OnReloadTuningFromDisk(object? sender, EventArgs e)
        {
            if (!CanRunTuningAction("reload"))
                return;

            try
            {
                var fresh = _configService.Load(_configPath);
                var args = CreateTuningArgs(fresh);
                if (!DeveloperTuningPanel.TryValidate(args, out var error))
                {
                    _logger.LogWarning($"[Tuning] Reloaded config rejected: {error}");
                    DeveloperTuningPanel.SetStatus($"Reload rejected: {error}");
                    return;
                }

                RefreshTuningPanelVariantOptions(fresh.PinParts.ShaftAssetVariant, fresh.PinParts.HeadAssetVariant);
                await ApplyTuningAsync(args);
                DeveloperTuningPanel.SetStatus($"Reloaded tuning values from {_configPath}.");
            }
            catch (Exception ex)
            {
                _logger.LogError($"[Tuning] Failed to reload visual config: {ex.Message}");
                DeveloperTuningPanel.SetStatus("Reload failed; see log for details.");
            }
        }

        private async Task ApplyTuningAsync(TuningPanelEventArgs e)
        {
            if (!CanRunTuningAction("apply"))
                return;

            _isTuningBusy = true;
            try
            {
                var oldPinPartsEnabled = _visualConfig.PinParts.Enabled;
                var oldUseComposite = _visualConfig.PinParts.UseCompositeRendering;
                var oldUsePrerasterize = _visualConfig.PinParts.UsePrerasterizedRendering;
                var oldShowDebugOverlay = _visualConfig.Debug.ShowCompositePinDebugOverlay;
                var oldUseLitShafts = _visualConfig.PinParts.UseLitShafts;
                var oldShaftVariant = _visualConfig.PinParts.ShaftAssetVariant;
                var oldHeadVariant = _visualConfig.PinParts.HeadAssetVariant;
                var oldClusterThreshold = _visualConfig.ClusterDistanceThreshold;
                var oldStubLength = _visualConfig.PinParts.DefaultStubLengthPixels;
                var oldTargetHeadRadius = _visualConfig.PinParts.TargetHeadRadiusPx;
                var oldTargetShaftHalfWidth = _visualConfig.PinParts.TargetShaftHalfWidthPx;
                var oldLocationMarkerSize = _visualConfig.LocationMarkerSize;
                var oldClusterMarkerSize = _visualConfig.ClusterMarkerSize;
                var oldDrawnHeadDiameter = _visualConfig.PinMarkers.BallSize;
                var oldDrawnShaftWidth = _visualConfig.PinMarkers.ShaftWidth;
                var oldDrawnShaftLength = _visualConfig.PinMarkers.ShaftLength;
                var oldPinHitDiameter = _visualConfig.MarkerHitTargets.PinDiameterPx;
                var oldClusterHitDiameter = _visualConfig.MarkerHitTargets.ClusterDiameterPx;
                var oldZoomedMapMode = _visualConfig.ZoomedMapRendering.ResamplingMode;

                var newShaftVariant = e.ShaftVariant.Trim();
                var newHeadVariant = e.HeadVariant.Trim();
                var oldCanUseComposite = _visualConfig.UsePinMarkers && oldPinPartsEnabled && oldUseComposite;
                var newCanUseComposite = _visualConfig.UsePinMarkers && e.UseComposite;
                var turningCompositeOff = oldCanUseComposite && !newCanUseComposite;

                var needsRecreate =
                    !NearlyEqual(oldClusterThreshold, e.ClusterThreshold) ||
                    !NearlyEqual(oldLocationMarkerSize, e.LocationMarkerSize) ||
                    !NearlyEqual(oldClusterMarkerSize, e.ClusterMarkerSize) ||
                    (turningCompositeOff && _individualMarkers.Count > 0 && _baseMarkerVisuals.Count == 0);

                // Recreate-class changes require a full-map cluster rebuild which is meaningless
                // while zoomed into a cluster. Reject and prompt the user to zoom out first.
                if (needsRecreate && _currentZoomedCluster != null)
                {
                    DeveloperTuningPanel.SetStatus("Zoom out to apply cluster/marker-size changes.");
                    return;
                }

                var assetVariantChanged =
                    !string.Equals(oldShaftVariant, newShaftVariant, StringComparison.Ordinal) ||
                    !string.Equals(oldHeadVariant, newHeadVariant, StringComparison.Ordinal) ||
                    oldUseLitShafts != e.UseLitShafts;

                // Plan-affecting changes: trigger cache invalidation and composite rebuild.
                var compositePlanChanged =
                    oldUseComposite != e.UseComposite ||
                    assetVariantChanged ||
                    !NearlyEqual(oldStubLength, e.StubLength) ||
                    !NearlyEqual(oldTargetHeadRadius, e.TargetHeadRadiusPx) ||
                    !NearlyEqual(oldTargetShaftHalfWidth, e.TargetShaftHalfWidthPx);

                // Render-only changes: need a visual refresh but do not invalidate cached plans.
                var renderSettingsChanged =
                    oldUsePrerasterize != e.UsePrerasterize ||
                    oldShowDebugOverlay != e.ShowDebugOverlay ||
                    oldZoomedMapMode != e.ZoomedMapResamplingMode;

                var drawnDimensionsChanged =
                    !NearlyEqual(oldDrawnHeadDiameter, e.DrawnHeadDiameterPx) ||
                    !NearlyEqual(oldDrawnShaftWidth, e.DrawnShaftWidthPx) ||
                    !NearlyEqual(oldDrawnShaftLength, e.DrawnShaftLengthPx);

                var hitTargetsChanged =
                    !NearlyEqual(oldPinHitDiameter, e.PinHitDiameterPx) ||
                    !NearlyEqual(oldClusterHitDiameter, e.ClusterHitDiameterPx);

                _visualConfig.PinParts.Enabled = e.UseComposite;
                _visualConfig.PinParts.UseCompositeRendering = e.UseComposite;
                _visualConfig.PinParts.UsePrerasterizedRendering = e.UsePrerasterize;
                _visualConfig.Debug.ShowCompositePinDebugOverlay = e.ShowDebugOverlay;
                _visualConfig.PinParts.UseLitShafts = e.UseLitShafts;
                _visualConfig.PinParts.ShaftAssetVariant = newShaftVariant;
                _visualConfig.PinParts.HeadAssetVariant = newHeadVariant;
                _visualConfig.ClusterDistanceThreshold = e.ClusterThreshold;
                _visualConfig.PinParts.DefaultStubLengthPixels = e.StubLength;
                _visualConfig.PinParts.TargetHeadRadiusPx = e.TargetHeadRadiusPx;
                _visualConfig.PinParts.TargetShaftHalfWidthPx = e.TargetShaftHalfWidthPx;
                _visualConfig.LocationMarkerSize = e.LocationMarkerSize;
                _visualConfig.ClusterMarkerSize = e.ClusterMarkerSize;
                _visualConfig.AutoOpenSingleLocationContentAfterZoom = e.AutoOpenSingleLocationContentAfterZoom;
                _visualConfig.PinMarkers.BallSize = e.DrawnHeadDiameterPx;
                _visualConfig.PinMarkers.ShaftWidth = e.DrawnShaftWidthPx;
                _visualConfig.PinMarkers.ShaftLength = e.DrawnShaftLengthPx;
                _visualConfig.MarkerHitTargets.PinDiameterPx = e.PinHitDiameterPx;
                _visualConfig.MarkerHitTargets.ClusterDiameterPx = e.ClusterHitDiameterPx;
                _visualConfig.ZoomedMapRendering.ResamplingMode = e.ZoomedMapResamplingMode;

                // Tip cap is a render-only drawn-pin setting; the ReapplyViewAfterTuningChange call
                // below re-runs UpdatePinTipCaps, which reads these fresh values.
                var cap = _visualConfig.PinMarkers.DrawnPinTipCap;
                cap.Style = e.TipCapStyle;
                cap.Alignment = e.TipCapAlignment;
                cap.WidthPx = e.TipCapWidthPx;
                cap.LineWeightPx = e.TipCapLineWeightPx;
                cap.ArcDepthPx = e.TipCapArcDepthPx;

                if (assetVariantChanged)
                    _pinPartBitmapCache.Clear();

                if (compositePlanChanged)
                    _compositePinPlanCache.ClearAll();

                if (turningCompositeOff)
                {
                    // Composite head/shaft overrides are meaningless in drawn mode and would
                    // otherwise be replayed onto markers (see ReapplyPendingOverrides), forcing a
                    // composite pin to leak back into drawn rendering. Drop them when composite off.
                    _overrideStore.ClearAll();
                }

                if (drawnDimensionsChanged)
                    RefreshDrawnPinVisuals();

                if (needsRecreate)
                {
                    await RecreateAllMarkersAsync();
                }
                else
                {
                    if (turningCompositeOff || ((compositePlanChanged || renderSettingsChanged) && newCanUseComposite))
                        RestoreBaseMarkerVisuals();

                    ReapplyViewAfterTuningChange();
                }

                if (hitTargetsChanged)
                    RefreshMarkerHitTargets();

                DeveloperTuningPanel.LoadValues(_visualConfig);
                DeveloperTuningPanel.SetStatus("Applied tuning values.");
                _logger.LogInfo("[Tuning] Applied runtime tuning values.");
            }
            catch (Exception ex)
            {
                _logger.LogError($"[Tuning] Failed to apply runtime tuning values: {ex.Message}");
                DeveloperTuningPanel.SetStatus("Apply failed; see log for details.");
            }
            finally
            {
                _isTuningBusy = false;
            }
        }

        private async Task RecreateAllMarkersAsync()
        {
            _extensionLineRenderer.Clear();
            _overrideStore.ClearAll();
            ClearAllMarkers();

            _contentLoader.ClusterDistanceThreshold = _visualConfig.ClusterDistanceThreshold;
            _clusters = await _contentLoader.LoadClustersAsync();
            AddClustersToMap(_clusters);
        }

        private bool CanRunTuningAction(string action)
        {
            if (!AreDeveloperToolsEnabled())
            {
                DeveloperTuningPanel.SetStatus("Developer tools are disabled.");
                return false;
            }

            if (_isTuningBusy)
            {
                DeveloperTuningPanel.SetStatus($"Cannot {action} while tuning is busy.");
                return false;
            }

            if (IsAnimating)
            {
                DeveloperTuningPanel.SetStatus($"Cannot {action} while animation is active.");
                return false;
            }

            if (_layoutEditor.IsEditMode)
            {
                DeveloperTuningPanel.SetStatus($"Cannot {action} while Edit Layout mode is active.");
                return false;
            }

            return true;
        }

        /// <summary>
        /// Restores the view to its correct state after a tuning change. Recomputes base
        /// auto-placement for ALL pins, then overlays the saved manual layout (which only
        /// covers the edited subset). Covers full-map root view AND a currently zoomed cluster.
        /// </summary>
        private void ReapplyViewAfterTuningChange()
        {
            if (_currentZoomedCluster != null)
            {
                // ShowZoomedView internally replays auto-placement and the manual layout overlay
                // for the cluster, so the zoomed layout is preserved. (precedent: OnDeleteLayoutButtonClick)
                ShowZoomedView(_currentZoomedCluster);
                return;
            }

            UpdateMarkerPositions();          // base auto-placement for every visible pin
            TryApplyFullMapManualLayout();    // overlay saved full-map layout (no-op if none / not full-map root)
        }

        private static TuningPanelEventArgs CreateTuningArgs(VisualConfig config)
        {
            var pinConfig = config.PinMarkers ?? new PinMarkerConfig();
            var cap = pinConfig.DrawnPinTipCap ?? new DrawnPinTipCapConfig();
            double outlineWidth = Math.Max(pinConfig.ShaftWidth, 2.5) +
                                  (2.0 * Math.Max(pinConfig.ShaftOutlineThickness, 1.0));
            return new TuningPanelEventArgs
            {
                PinPartsEnabled = config.PinParts.Enabled && config.PinParts.UseCompositeRendering,
                UseComposite = config.PinParts.Enabled && config.PinParts.UseCompositeRendering,
                UsePrerasterize = config.PinParts.UsePrerasterizedRendering,
                ShowDebugOverlay = config.Debug.ShowCompositePinDebugOverlay,
                UseLitShafts = config.PinParts.UseLitShafts,
                ShaftVariant = config.PinParts.ShaftAssetVariant,
                HeadVariant = config.PinParts.HeadAssetVariant,
                ClusterThreshold = config.ClusterDistanceThreshold,
                StubLength = config.PinParts.DefaultStubLengthPixels,
                TargetHeadRadiusPx = config.PinParts.TargetHeadRadiusPx,
                TargetShaftHalfWidthPx = config.PinParts.TargetShaftHalfWidthPx,
                LocationMarkerSize = config.LocationMarkerSize,
                ClusterMarkerSize = config.ClusterMarkerSize,
                DrawnHeadDiameterPx = pinConfig.BallSize,
                DrawnShaftWidthPx = pinConfig.ShaftWidth,
                DrawnShaftLengthPx = pinConfig.ShaftLength,
                PinHitDiameterPx = config.MarkerHitTargets.PinDiameterPx,
                ClusterHitDiameterPx = config.MarkerHitTargets.ClusterDiameterPx,
                AutoOpenSingleLocationContentAfterZoom = config.AutoOpenSingleLocationContentAfterZoom,
                ZoomedMapResamplingMode = config.ZoomedMapRendering.ResamplingMode,
                TipCapStyle = cap.Style,
                TipCapAlignment = cap.Alignment,
                TipCapWidthPx = cap.ResolveWidthPx(outlineWidth),
                TipCapLineWeightPx = cap.ResolveLineWeightPx(
                    pinConfig.ShaftOutlineThickness),
                TipCapArcDepthPx = cap.ArcDepthPx
            };
        }

        private static bool NearlyEqual(double left, double right)
        {
            return Math.Abs(left - right) < 0.0001;
        }
    }
}
