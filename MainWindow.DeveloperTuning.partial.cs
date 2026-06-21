using System;
using System.Threading.Tasks;
using System.Windows;
using InteractiveWorldMap.Models;

namespace InteractiveWorldMap
{
    public partial class MainWindow
    {
        private bool _isTuningBusy;

        private void SetupTuningPanel()
        {
            TuningPanelToggleBtn.Visibility = _visualConfig.Debug.EnableTuningPanel
                ? Visibility.Visible
                : Visibility.Collapsed;

            DeveloperTuningPanel.Visibility = Visibility.Collapsed;
            DeveloperTuningPanel.LoadValues(_visualConfig);
        }

        private void OnTuningPanelToggleClick(object sender, RoutedEventArgs e)
        {
            if (!_visualConfig.Debug.EnableTuningPanel)
                return;

            DeveloperTuningPanel.Visibility = DeveloperTuningPanel.Visibility == Visibility.Visible
                ? Visibility.Collapsed
                : Visibility.Visible;
        }

        private async void OnApplyTuning(object? sender, TuningPanelEventArgs e)
        {
            await ApplyTuningAsync(e);
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
                await ApplyTuningAsync(CreateTuningArgs(fresh));
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

                var newShaftVariant = e.ShaftVariant.Trim();
                var newHeadVariant = e.HeadVariant.Trim();
                var oldCanUseComposite = _visualConfig.UsePinMarkers && oldPinPartsEnabled && oldUseComposite;
                var newCanUseComposite = _visualConfig.UsePinMarkers && e.PinPartsEnabled && e.UseComposite;
                var turningCompositeOff = oldCanUseComposite && !newCanUseComposite;

                var needsRecreate =
                    !NearlyEqual(oldClusterThreshold, e.ClusterThreshold) ||
                    !NearlyEqual(oldLocationMarkerSize, e.LocationMarkerSize) ||
                    !NearlyEqual(oldClusterMarkerSize, e.ClusterMarkerSize) ||
                    (turningCompositeOff && _individualMarkers.Count > 0 && _baseMarkerVisuals.Count == 0);

                var assetVariantChanged =
                    !string.Equals(oldShaftVariant, newShaftVariant, StringComparison.Ordinal) ||
                    !string.Equals(oldHeadVariant, newHeadVariant, StringComparison.Ordinal) ||
                    oldUseLitShafts != e.UseLitShafts;

                var compositePlanChanged =
                    oldPinPartsEnabled != e.PinPartsEnabled ||
                    oldUseComposite != e.UseComposite ||
                    oldUsePrerasterize != e.UsePrerasterize ||
                    oldShowDebugOverlay != e.ShowDebugOverlay ||
                    assetVariantChanged ||
                    !NearlyEqual(oldStubLength, e.StubLength) ||
                    !NearlyEqual(oldTargetHeadRadius, e.TargetHeadRadiusPx) ||
                    !NearlyEqual(oldTargetShaftHalfWidth, e.TargetShaftHalfWidthPx);

                _visualConfig.PinParts.Enabled = e.PinPartsEnabled;
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

                if (assetVariantChanged)
                    _pinPartBitmapCache.Clear();

                if (compositePlanChanged)
                    _compositePinPlanCache.ClearAll();

                if (needsRecreate)
                {
                    await RecreateAllMarkersAsync();
                }
                else
                {
                    if (turningCompositeOff || (compositePlanChanged && newCanUseComposite))
                        RestoreBaseMarkerVisuals();

                    UpdateMarkerPositions();
                }

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

        private static TuningPanelEventArgs CreateTuningArgs(VisualConfig config)
        {
            return new TuningPanelEventArgs
            {
                PinPartsEnabled = config.PinParts.Enabled,
                UseComposite = config.PinParts.UseCompositeRendering,
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
                ClusterMarkerSize = config.ClusterMarkerSize
            };
        }

        private static bool NearlyEqual(double left, double right)
        {
            return Math.Abs(left - right) < 0.0001;
        }
    }
}
