# Runtime Tuning Panel Plan

## Objective
Add an in-app developer tuning panel (`Views/DeveloperTuningPanel.xaml`) to adjust visual and layout configuration parameters on the fly. This prevents the need to hand-edit `visual-config.json` and restart the application when tweaking aesthetics or debugging composite pin logic.

## Scope
1. A toggle button in `MainWindow.xaml` (e.g., bottom-right) gated by a new `DebugConfig.EnableTuningPanel` flag.
2. An encapsulated UserControl (`Views/DeveloperTuningPanel.xaml`) exposing tuning options via data binding or events.
3. A new EventArgs model `Models/TuningPanelEventArgs.cs` containing primitives only, to ensure the View layer does not reference the Services layer.
4. Logic in `MainWindow.DeveloperTuning.partial.cs` to apply changes robustly without restarting, preserving reference stability of the `_visualConfig` object.

## Modularity & File-Size Impact
- The core logic for UI interaction is placed in `Views/DeveloperTuningPanel.xaml` and `.cs`.
- The wiring logic goes into `MainWindow.DeveloperTuning.partial.cs`.
- This ensures `MainWindow.xaml.cs` (currently at 776 lines) doesn't exceed the 800-line project limit for a single `.cs` file.

## Runtime Constraints & Reference Stability
1. **Reference Stability**: `_visualConfig` is passed by reference to `MarkerPlacementOrchestrator` and other services upon construction. When loading configuration from disk (`OnReloadTuningFromDisk`), we **must not** reassign the `_visualConfig` field. We must copy the properties over to the existing instance so dependent services stay synchronized.
2. **Cluster Threshold & Size Parameters (`ClusterDistanceThreshold`, `LocationMarkerSize`)**: 
   - Modifying `ClusterDistanceThreshold` requires re-fetching data.
   - `LocationMarkerSize` and `ClusterMarkerSize` are baked into the `LocationMarker` and `ClusterMarker` UserControls at creation time.
   - **Solution**: Modifying these requires calling a full recreation flow (`RecreateAllMarkersAsync()`) which clears all markers, reclusters, and reinstantiates the WPF marker objects.
3. **Asset Variants**: Changing variants updates the strings and clears caches (`_pinPartBitmapCache.Clear()` and `_compositePinPlanCache.ClearAll()`). The composite cache actually keys on variant ID, so the clear is belt-and-suspenders.
4. **Composite Toggles**: Turning off composite rendering (`UseCompositeRendering = false`) leaves previously composited markers blank. We must explicitly call `RestoreBaseMarkerVisuals()` and note the caveat: this only works if drawn pins were enabled during startup, as `_baseMarkerVisuals` is only captured if `UsePinMarkers` is true.

## Proposed Implementation

### 1. Configuration & Models
Update `Models/DebugConfig.cs`:
```csharp
public bool EnableTuningPanel { get; set; } = false;
```

Create `Models/TuningPanelEventArgs.cs`:
```csharp
namespace InteractiveWorldMap.Models
{
    public class TuningPanelEventArgs : System.EventArgs
    {
        public bool UseComposite { get; set; }
        public bool UsePrerasterize { get; set; }
        public bool ShowDebugOverlay { get; set; }
        public string ShaftVariant { get; set; } = string.Empty;
        public string HeadVariant { get; set; } = string.Empty;
        public double ClusterThreshold { get; set; }
        public double StubLength { get; set; }
        public bool NeedsFullRecreation { get; set; } // Set if threshold or marker sizes change
    }
}
```

### 2. UserControl (`Views/DeveloperTuningPanel.xaml`)
Create the UserControl to handle the UI inputs. It will enforce numeric validations (e.g., `Math.Max(0, value)` and red-border error states). It communicates purely via an event `public event EventHandler<TuningPanelEventArgs>? ApplyRequested;` without referencing `InteractiveWorldMap.Services`.

### 3. Logic Wire-up (`MainWindow.DeveloperTuning.partial.cs`)
```csharp
using System;
using System.Threading.Tasks;
using System.Windows;
using InteractiveWorldMap.Models;

namespace InteractiveWorldMap
{
    public partial class MainWindow
    {
        // _configService and _configPath should be initialized directly in the MainWindow constructor.
        private Services.VisualConfigService _configService = new Services.VisualConfigService(); 
        private string _configPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "visual-config.json");
        private bool _isTuningBusy = false;

        private void SetupTuningPanel()
        {
            if (_visualConfig.Debug.EnableTuningPanel)
            {
                TuningPanelToggleBtn.Visibility = Visibility.Visible;
                DeveloperTuningPanel.LoadValues(_visualConfig);
            }
        }

        // Hooked from OnKeyDown in MainWindow.xaml.cs for F12
        private void HandleTuningPanelHotkey()
        {
            if (!_visualConfig.Debug.EnableTuningPanel) return;
            
            DeveloperTuningPanel.Visibility = DeveloperTuningPanel.Visibility == Visibility.Visible 
                ? Visibility.Collapsed 
                : Visibility.Visible;
                
            if (DeveloperTuningPanel.Visibility == Visibility.Visible)
                DeveloperTuningPanel.LoadValues(_visualConfig);
        }

        private async void OnApplyTuning(object sender, TuningPanelEventArgs e)
        {
            if (_isTuningBusy) return;
            _isTuningBusy = true;
            try
            {
                bool wasComposite = _visualConfig.PinParts.UseCompositeRendering;
                bool variantChanged = _visualConfig.PinParts.ShaftAssetVariant != e.ShaftVariant ||
                                      _visualConfig.PinParts.HeadAssetVariant != e.HeadVariant;

                // Mutate the reference-stable instance
                _visualConfig.PinParts.UseCompositeRendering = e.UseComposite;
                _visualConfig.PinParts.UsePrerasterizedRendering = e.UsePrerasterize;
                _visualConfig.Debug.ShowCompositePinDebugOverlay = e.ShowDebugOverlay;
                _visualConfig.PinParts.ShaftAssetVariant = e.ShaftVariant;
                _visualConfig.PinParts.HeadAssetVariant = e.HeadVariant;
                _visualConfig.PinParts.DefaultStubLengthPixels = e.StubLength;
                _visualConfig.ClusterDistanceThreshold = e.ClusterThreshold;

                if (e.NeedsFullRecreation)
                {
                    await RecreateAllMarkersAsync();
                }
                else
                {
                    if (variantChanged)
                    {
                        _pinPartBitmapCache.Clear();
                        _pinPartGeometryHash = null;
                        _compositePinPlanCache.ClearAll();
                    }
                    
                    if (wasComposite && !_visualConfig.PinParts.UseCompositeRendering)
                    {
                        RestoreBaseMarkerVisuals(); 
                    }
                    
                    UpdateMarkerPositions();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Tuning apply failed: {ex.Message}");
            }
            finally
            {
                _isTuningBusy = false;
            }
        }

        private async Task RecreateAllMarkersAsync()
        {
            ClearAllMarkers();
            _clusters = await _contentLoader.LoadClustersAsync();
            AddClustersToMap(_clusters); // Implicitly calls TryApplyFullMapManualLayout and UpdateMarkerPositions
        }
        
        private void OnSaveTuningToDisk(object sender, EventArgs e)
        {
            if (_isTuningBusy) return;
            try
            {
                _configService.Save(_visualConfig, _configPath);
                _logger.LogInfo("VisualConfig saved to disk via Tuning Panel.");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to save tuning config: {ex.Message}");
            }
        }
        
        private async void OnReloadTuningFromDisk(object sender, EventArgs e)
        {
            if (_isTuningBusy) return;
            _isTuningBusy = true;
            try
            {
                var diskConfig = _configService.Load(_configPath);
                
                // Copy properties over to preserve the reference!
                _visualConfig.PinParts.UseCompositeRendering = diskConfig.PinParts.UseCompositeRendering;
                _visualConfig.PinParts.UsePrerasterizedRendering = diskConfig.PinParts.UsePrerasterizedRendering;
                _visualConfig.Debug.ShowCompositePinDebugOverlay = diskConfig.Debug.ShowCompositePinDebugOverlay;
                _visualConfig.PinParts.ShaftAssetVariant = diskConfig.PinParts.ShaftAssetVariant;
                _visualConfig.PinParts.HeadAssetVariant = diskConfig.PinParts.HeadAssetVariant;
                _visualConfig.PinParts.DefaultStubLengthPixels = diskConfig.PinParts.DefaultStubLengthPixels;
                
                bool needsRecreate = _visualConfig.ClusterDistanceThreshold != diskConfig.ClusterDistanceThreshold || 
                                     _visualConfig.LocationMarkerSize != diskConfig.LocationMarkerSize;
                
                _visualConfig.ClusterDistanceThreshold = diskConfig.ClusterDistanceThreshold;
                _visualConfig.LocationMarkerSize = diskConfig.LocationMarkerSize;
                
                _pinPartBitmapCache.Clear();
                _compositePinPlanCache.ClearAll();

                if (needsRecreate)
                {
                    await RecreateAllMarkersAsync();
                }
                else
                {
                    RestoreBaseMarkerVisuals();
                    UpdateMarkerPositions();
                }
                
                DeveloperTuningPanel.LoadValues(_visualConfig);
                _logger.LogInfo("VisualConfig reloaded from disk.");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to reload tuning config: {ex.Message}");
            }
            finally
            {
                _isTuningBusy = false;
            }
        }
    }
}
```

## Failure Modes & Error Handling
1. **Invalid Input:** `Views/DeveloperTuningPanel.xaml` implements strict validation. Invalid numeric entries prevent applying changes and visually alert the user.
2. **Configuration I/O Exceptions:** Saving/loading the JSON gracefully logs errors and blocks UI corruption without crashing the application.
3. **Composite Off Fallback Limits:** Reverting from composite to drawn pins relies on `RestoreBaseMarkerVisuals()`. If the app booted with `UsePinMarkers = false`, this cache might be empty, resulting in blank markers unless an explicit full recreation is triggered.

## Tests
- **Unit Tests:** 
  - Add JSON roundtrip test in `Tests/VisualConfigServiceTests.cs` for `EnableTuningPanel`.
  - Add test in `Tests/ContentLoaderClusterTests.cs` explicitly comparing cluster counts across thresholds to ensure caching handles thresholds correctly.
- **Architecture Test:** Verifies that `Views/DeveloperTuningPanel.xaml.cs` does not depend on `InteractiveWorldMap.Services`.

## Documentation Updates
Add an entry in `CHANGELOG.md` under `[Unreleased]` for the Tuning Panel feature and F12 hotkey.
