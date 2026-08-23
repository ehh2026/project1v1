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
                System.IO.Path.Combine(_contentLoader.ContentFolderPath, ContentFileNames.AssetsFolderName),
                _visualConfig.PinParts.PartsFolderPath,
                "shaft_variants",
                shaftToInclude);

            var heads = _variantCatalog.ListVariants(
                System.IO.Path.Combine(_contentLoader.ContentFolderPath, ContentFileNames.AssetsFolderName),
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
                HideTuningPanel();
                return;
            }

            DeveloperTuningPanel.ShowCategory(category);
            DeveloperTuningPanel.Visibility = Visibility.Visible;
        }

        private void HideTuningPanel()
        {
            DeveloperTuningPanel.Visibility = Visibility.Collapsed;
        }

        private bool IsTuningPanelVisible =>
            DeveloperTuningPanel.Visibility == Visibility.Visible;

        private void OnTuningPanelCloseRequested(object? sender, EventArgs e)
        {
            HideTuningPanel();
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

        private async void OnSaveTuningToDisk(object? sender, EventArgs e)
        {
            if (!CanRunTuningAction("save"))
                return;

            try
            {
                if (!DeveloperTuningPanel.TryGetCurrentValues(out var args))
                {
                    DeveloperTuningPanel.SetStatus("Save rejected: fix invalid tuning values first.");
                    return;
                }

                if (!await ApplyTuningAsync(args))
                    return;

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
                var fresh = _configService.Load(_configPath, _defaultConfigPath);
                var args = CreateTuningArgs(fresh);
                if (!DeveloperTuningPanel.TryValidate(args, out var error))
                {
                    _logger.LogWarning($"[Tuning] Reloaded config rejected: {error}");
                    DeveloperTuningPanel.SetStatus($"Reload rejected: {error}");
                    return;
                }

                RefreshTuningPanelVariantOptions(fresh.PinParts.ShaftAssetVariant, fresh.PinParts.HeadAssetVariant);
                if (!await ApplyTuningAsync(args))
                    return;

                DeveloperTuningPanel.SetStatus($"Reloaded tuning values from {_configPath}.");
            }
            catch (Exception ex)
            {
                _logger.LogError($"[Tuning] Failed to reload visual config: {ex.Message}");
                DeveloperTuningPanel.SetStatus("Reload failed; see log for details.");
            }
        }

        private async Task<bool> ApplyTuningAsync(TuningPanelEventArgs e)
        {
            if (!CanRunTuningAction("apply"))
                return false;

            _isTuningBusy = true;
            try
            {
                var changes = TuningChangeSet.Create(
                    _visualConfig,
                    e,
                    _individualMarkers.Count,
                    _baseMarkerVisuals.Count);
                var newShaftVariant = changes.NewShaftVariant;
                var newHeadVariant = changes.NewHeadVariant;
                var newCanUseComposite = changes.NewCanUseComposite;
                var turningCompositeOff = changes.TurningCompositeOff;
                var needsRecreate = changes.NeedsRecreate;

                // Recreate-class changes require a full-map cluster rebuild which is meaningless
                // while zoomed into a cluster. Reject and prompt the user to zoom out first.
                if (needsRecreate && _currentZoomedCluster != null)
                {
                    DeveloperTuningPanel.SetStatus("Zoom out to apply cluster/marker-size changes.");
                    return false;
                }

                ApplyTuningValues(e, newShaftVariant, newHeadVariant);
                ApplyOpenContentWindowStyle();

                if (changes.AssetVariantChanged)
                    _pinPartBitmapCache.Clear();

                if (changes.CompositePlanChanged)
                    _compositePinPlanCache.ClearAll();

                if (turningCompositeOff)
                {
                    // Composite head/shaft overrides are meaningless in drawn mode and would
                    // otherwise be replayed onto markers (see ReapplyPendingOverrides), forcing a
                    // composite pin to leak back into drawn rendering. Drop them when composite off.
                    _overrideStore.ClearAll();
                }

                if (changes.DrawnDimensionsChanged)
                    RefreshDrawnPinVisuals();

                if (changes.PinShadowChanged || changes.ClusterShadowChanged)
                    RefreshMarkerShadows();

                if (needsRecreate)
                {
                    await RecreateAllMarkersAsync();
                }
                else
                {
                    if (turningCompositeOff || ((changes.CompositePlanChanged || changes.RenderSettingsChanged) && newCanUseComposite))
                        RestoreBaseMarkerVisuals();

                    ReapplyViewAfterTuningChange();
                }

                if (changes.HitTargetsChanged)
                    RefreshMarkerHitTargets();

                DeveloperTuningPanel.LoadValues(_visualConfig);
                DeveloperTuningPanel.SetStatus("Applied tuning values.");
                _logger.LogInfo("[Tuning] Applied runtime tuning values.");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError($"[Tuning] Failed to apply runtime tuning values: {ex.Message}");
                DeveloperTuningPanel.SetStatus("Apply failed; see log for details.");
                return false;
            }
            finally
            {
                _isTuningBusy = false;
            }
        }

        private void ApplyTuningValues(
            TuningPanelEventArgs e,
            string newShaftVariant,
            string newHeadVariant)
        {
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
            _visualConfig.ClusterBadgeSize = e.ClusterBadgeSize;
            _visualConfig.ClusterCountFontSize = e.ClusterCountFontSize;
            _visualConfig.ZoomScale = e.ZoomScale;
            _visualConfig.AnimationDurationMs = e.AnimationDurationMs;
            _visualConfig.AutoOpenSingleLocationContentAfterZoom = e.AutoOpenSingleLocationContentAfterZoom;
            ApplyPinMarkerTuning(e);
            ApplyContentWindowTuning(e);

            // The composite toggle above decides whether Auto Assign Pins has anything to do.
            // CanRunTuningAction currently refuses to apply tuning during edit mode, so this cannot
            // fire while that button is on screen; it is here so the button's correctness does not
            // depend on that guard staying in place.
            UpdateAutoAssignPinsAvailability();
        }

        private void ApplyPinMarkerTuning(TuningPanelEventArgs e)
        {
            _visualConfig.PinMarkers.BallSize = e.DrawnHeadDiameterPx;
            _visualConfig.PinMarkers.ShaftWidth = e.DrawnShaftWidthPx;
            _visualConfig.PinMarkers.ShaftLength = e.DrawnShaftLengthPx;
            _visualConfig.PinMarkers.ShowShadow = e.PinShadowEnabled;
            _visualConfig.PinMarkers.ShadowOpacity = e.PinShadowOpacity;
            _visualConfig.ClusterMarkerShadow.Enabled = e.ClusterShadowEnabled;
            _visualConfig.ClusterMarkerShadow.Opacity = e.ClusterShadowOpacity;
            _visualConfig.MarkerHitTargets.PinDiameterPx = e.PinHitDiameterPx;
            _visualConfig.MarkerHitTargets.ClusterDiameterPx = e.ClusterHitDiameterPx;
            _visualConfig.ZoomedMapRendering.ResamplingMode = e.ZoomedMapResamplingMode;

            var cap = _visualConfig.PinMarkers.DrawnPinTipCap;
            cap.Style = e.TipCapStyle;
            cap.Alignment = e.TipCapAlignment;
            cap.WidthPx = e.TipCapWidthPx;
            cap.LineWeightPx = e.TipCapLineWeightPx;
            cap.ArcDepthPx = e.TipCapArcDepthPx;
        }

        private void ApplyContentWindowTuning(TuningPanelEventArgs e)
        {
            var cw = _visualConfig.ContentWindows;
            cw.FontFamily = e.ContentFontFamily;
            cw.Popup.BackgroundColor = e.PopupBackgroundColor;
            cw.Popup.BackgroundOpacity = e.PopupBackgroundOpacity;
            cw.Popup.BorderColor = e.PopupBorderColor;
            cw.Popup.BorderThickness = e.PopupBorderThickness;
            cw.Popup.CornerRadius = e.PopupCornerRadius;
            cw.Popup.TextColor = e.PopupTextColor;
            cw.Popup.HeadingFontSize = e.PopupHeadingFontSize;
            cw.Popup.BodyFontSize = e.PopupBodyFontSize;
            cw.Caption.BackgroundColor = e.CaptionBackgroundColor;
            cw.Caption.BackgroundOpacity = e.CaptionBackgroundOpacity;
            cw.Caption.TopBorderColor = e.CaptionTopBorderColor;
            cw.Caption.TextColor = e.CaptionTextColor;
            cw.Caption.FontSize = e.CaptionFontSize;
        }

        private void ApplyOpenContentWindowStyle()
        {
            var cw = _visualConfig.ContentWindows;
            _activeSubwindow?.ApplyStyle(cw);
            _activeDidacticWindow?.ApplyStyle(cw);
            _activeThumbnailBrowser?.ApplyStyle(cw);
        }

        private async Task RecreateAllMarkersAsync()
        {
            _extensionLineRenderer.Clear();
            _overrideStore.ClearAll();
            ClearAllMarkers();

            _contentLoader.ClusterDistanceThreshold = _visualConfig.ClusterDistanceThreshold;
            _contentLoader.MaxCachedLocations = _visualConfig.MaxCachedLocations;
            _clusters = await _contentLoader.LoadClustersAsync();
            AddClustersToMap(_clusters);
        }

        private void RefreshMarkerShadows()
        {
            RefreshDrawnPinVisuals();

            foreach (var marker in _individualMarkers)
            {
                if (marker.Content is CompositePinMarker composite)
                {
                    composite.ApplyHeadShadow(
                        _visualConfig.PinMarkers.ShowShadow,
                        _visualConfig.PinMarkers.ShadowOpacity);
                }
            }

            foreach (var clusterMarker in _clusterMarkers)
                clusterMarker.ApplyShadowConfig(_visualConfig.ClusterMarkerShadow);
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
            var content = config.ContentWindows ?? new ContentWindowConfig();
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
                ClusterBadgeSize = config.ClusterBadgeSize,
                ClusterCountFontSize = config.ClusterCountFontSize,
                ZoomScale = config.ZoomScale,
                AnimationDurationMs = config.AnimationDurationMs,
                PinShadowEnabled = pinConfig.ShowShadow,
                PinShadowOpacity = pinConfig.ShadowOpacity,
                ClusterShadowEnabled = config.ClusterMarkerShadow.Enabled,
                ClusterShadowOpacity = config.ClusterMarkerShadow.Opacity,
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
                TipCapArcDepthPx = cap.ArcDepthPx,
                ContentFontFamily = content.FontFamily,
                PopupBackgroundColor = content.Popup.BackgroundColor,
                PopupBackgroundOpacity = content.Popup.BackgroundOpacity,
                PopupBorderColor = content.Popup.BorderColor,
                PopupBorderThickness = content.Popup.BorderThickness,
                PopupCornerRadius = content.Popup.CornerRadius,
                PopupTextColor = content.Popup.TextColor,
                PopupHeadingFontSize = content.Popup.HeadingFontSize,
                PopupBodyFontSize = content.Popup.BodyFontSize,
                CaptionBackgroundColor = content.Caption.BackgroundColor,
                CaptionBackgroundOpacity = content.Caption.BackgroundOpacity,
                CaptionTopBorderColor = content.Caption.TopBorderColor,
                CaptionTextColor = content.Caption.TextColor,
                CaptionFontSize = content.Caption.FontSize
            };
        }

        private sealed class TuningChangeSet
        {
            private TuningChangeSet(
                string newShaftVariant,
                string newHeadVariant,
                bool newCanUseComposite,
                bool turningCompositeOff,
                bool needsRecreate,
                bool assetVariantChanged,
                bool compositePlanChanged,
                bool renderSettingsChanged,
                bool drawnDimensionsChanged,
                bool hitTargetsChanged,
                bool pinShadowChanged,
                bool clusterShadowChanged)
            {
                NewShaftVariant = newShaftVariant;
                NewHeadVariant = newHeadVariant;
                NewCanUseComposite = newCanUseComposite;
                TurningCompositeOff = turningCompositeOff;
                NeedsRecreate = needsRecreate;
                AssetVariantChanged = assetVariantChanged;
                CompositePlanChanged = compositePlanChanged;
                RenderSettingsChanged = renderSettingsChanged;
                DrawnDimensionsChanged = drawnDimensionsChanged;
                HitTargetsChanged = hitTargetsChanged;
                PinShadowChanged = pinShadowChanged;
                ClusterShadowChanged = clusterShadowChanged;
            }

            public string NewShaftVariant { get; }
            public string NewHeadVariant { get; }
            public bool NewCanUseComposite { get; }
            public bool TurningCompositeOff { get; }
            public bool NeedsRecreate { get; }
            public bool AssetVariantChanged { get; }
            public bool CompositePlanChanged { get; }
            public bool RenderSettingsChanged { get; }
            public bool DrawnDimensionsChanged { get; }
            public bool HitTargetsChanged { get; }
            public bool PinShadowChanged { get; }
            public bool ClusterShadowChanged { get; }

            public static TuningChangeSet Create(
                VisualConfig visualConfig,
                TuningPanelEventArgs e,
                int individualMarkerCount,
                int baseMarkerVisualCount)
            {
                var newShaftVariant = e.ShaftVariant.Trim();
                var newHeadVariant = e.HeadVariant.Trim();
                var oldCanUseComposite = visualConfig.UsePinMarkers &&
                                         visualConfig.PinParts.Enabled &&
                                         visualConfig.PinParts.UseCompositeRendering;
                var newCanUseComposite = visualConfig.UsePinMarkers && e.UseComposite;
                var turningCompositeOff = oldCanUseComposite && !newCanUseComposite;
                var assetVariantChanged = HasAssetVariantChanged(visualConfig, e, newShaftVariant, newHeadVariant);
                var hitTargetsChanged = HasHitTargetChange(visualConfig, e);

                return new TuningChangeSet(
                    newShaftVariant,
                    newHeadVariant,
                    newCanUseComposite,
                    turningCompositeOff,
                    HasRecreateChange(visualConfig, e, turningCompositeOff, individualMarkerCount, baseMarkerVisualCount),
                    assetVariantChanged,
                    HasCompositePlanChange(visualConfig, e, assetVariantChanged),
                    HasRenderSettingChange(visualConfig, e),
                    HasDrawnDimensionChange(visualConfig, e),
                    hitTargetsChanged,
                    visualConfig.PinMarkers.ShowShadow != e.PinShadowEnabled ||
                    !NearlyEqual(visualConfig.PinMarkers.ShadowOpacity, e.PinShadowOpacity),
                    visualConfig.ClusterMarkerShadow.Enabled != e.ClusterShadowEnabled ||
                    !NearlyEqual(visualConfig.ClusterMarkerShadow.Opacity, e.ClusterShadowOpacity));
            }

            private static bool HasAssetVariantChanged(
                VisualConfig visualConfig,
                TuningPanelEventArgs e,
                string newShaftVariant,
                string newHeadVariant) =>
                !string.Equals(visualConfig.PinParts.ShaftAssetVariant, newShaftVariant, StringComparison.Ordinal) ||
                !string.Equals(visualConfig.PinParts.HeadAssetVariant, newHeadVariant, StringComparison.Ordinal) ||
                visualConfig.PinParts.UseLitShafts != e.UseLitShafts;

            private static bool HasRecreateChange(
                VisualConfig visualConfig,
                TuningPanelEventArgs e,
                bool turningCompositeOff,
                int individualMarkerCount,
                int baseMarkerVisualCount) =>
                !NearlyEqual(visualConfig.ClusterDistanceThreshold, e.ClusterThreshold) ||
                !NearlyEqual(visualConfig.LocationMarkerSize, e.LocationMarkerSize) ||
                !NearlyEqual(visualConfig.ClusterMarkerSize, e.ClusterMarkerSize) ||
                !NearlyEqual(visualConfig.ClusterBadgeSize, e.ClusterBadgeSize) ||
                !NearlyEqual(visualConfig.ClusterCountFontSize, e.ClusterCountFontSize) ||
                (turningCompositeOff && individualMarkerCount > 0 && baseMarkerVisualCount == 0);

            private static bool HasCompositePlanChange(
                VisualConfig visualConfig,
                TuningPanelEventArgs e,
                bool assetVariantChanged) =>
                visualConfig.PinParts.UseCompositeRendering != e.UseComposite ||
                assetVariantChanged ||
                !NearlyEqual(visualConfig.PinParts.DefaultStubLengthPixels, e.StubLength) ||
                !NearlyEqual(visualConfig.PinParts.TargetHeadRadiusPx, e.TargetHeadRadiusPx) ||
                !NearlyEqual(visualConfig.PinParts.TargetShaftHalfWidthPx, e.TargetShaftHalfWidthPx);

            private static bool HasRenderSettingChange(VisualConfig visualConfig, TuningPanelEventArgs e) =>
                visualConfig.PinParts.UsePrerasterizedRendering != e.UsePrerasterize ||
                visualConfig.Debug.ShowCompositePinDebugOverlay != e.ShowDebugOverlay ||
                visualConfig.ZoomedMapRendering.ResamplingMode != e.ZoomedMapResamplingMode;

            private static bool HasDrawnDimensionChange(VisualConfig visualConfig, TuningPanelEventArgs e) =>
                !NearlyEqual(visualConfig.PinMarkers.BallSize, e.DrawnHeadDiameterPx) ||
                !NearlyEqual(visualConfig.PinMarkers.ShaftWidth, e.DrawnShaftWidthPx) ||
                !NearlyEqual(visualConfig.PinMarkers.ShaftLength, e.DrawnShaftLengthPx);

            private static bool HasHitTargetChange(VisualConfig visualConfig, TuningPanelEventArgs e) =>
                !NearlyEqual(visualConfig.MarkerHitTargets.PinDiameterPx, e.PinHitDiameterPx) ||
                !NearlyEqual(visualConfig.MarkerHitTargets.ClusterDiameterPx, e.ClusterHitDiameterPx);
        }

        private static bool NearlyEqual(double left, double right)
        {
            return Math.Abs(left - right) < 0.0001;
        }
    }
}
