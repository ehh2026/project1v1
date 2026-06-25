# Manual Layout Editor - Design Document

## Overview

A manual layout editor feature that allows users to override the automatic radial extension calculations by manually positioning markers and saving those layouts for reuse. This provides a fallback when the automatic algorithm produces suboptimal results.

## Use Case

When the automatic radial extension algorithm produces intersecting lines, overlapping markers, or aesthetically unpleasing layouts, users can enter "Edit Mode" to manually adjust marker positions. These manual layouts are saved and automatically loaded when the same cluster configuration is encountered again.

## Key Requirements

### 1. Edit Mode Toggle
- Master developer-tools flag in `visual-config.json`: `"EnableDeveloperTools": true/false`
- Editor sub-setting in `visual-config.json`: `"ManualLayoutEditor": { "Enabled": true/false }`
- UI button: "Edit Layout" (only visible when feature is enabled)
- When clicked, enters edit mode for the current zoomed view

### 2. Edit Mode Functionality
- All radial extension markers become draggable
- Line endpoint moves with the marker (origin point stays fixed at actual coordinate)
- Visual feedback: highlight selected marker, show drag cursor
- Real-time line redrawing as marker is dragged
- Constraints:
  - Marker cannot be dragged outside canvas bounds
  - Line origin always remains at the actual location coordinate
  - Only the extended position (marker endpoint) is adjustable

### 3. Save Layout
- "Save Layout" button appears in edit mode
- Saves current marker positions to a JSON file
- Layout is keyed by a unique identifier (see Configuration Key below)
- Stored in: `Images&Content/manual-layouts.json` or similar

### 4. Delete and Recalculate
- "Delete & Recalculate" button in edit mode
- Removes saved layout for current configuration
- Re-runs automatic algorithm
- Useful when algorithm improves or configuration changes

### 5. Automatic Loading
- Before applying automatic radial extensions, check for saved layout
- If matching layout exists, load saved marker positions instead
- Visual indicator that manual layout is being used (e.g., small icon or status text)

### 6. Full-Map Composite Stub Layouts
- The editor is also available at the fully zoomed-out map when `EnableDeveloperTools` and `ManualLayoutEditor.Enabled` are both true.
- Full-map edit targets are only visible single-location individual markers. Multi-location cluster markers and hidden dense-cluster members are not editable until the user zooms into that cluster.
- Full-map layouts use `fullmap_s{W}x{H}` group keys based on rounded `MapDisplay.ActualWidth` and `MapDisplay.ActualHeight`, for example `fullmap_s1920x1080`.
- Full-map keys are exact-match only. A saved `fullmap_s1920x1080` layout must not be reused for `fullmap_s1440x900`; resizing creates a separate layout group.
- Saved full-map layouts replay after startup placement and after returning from a zoomed cluster. Visible locations missing from the saved variant keep their automatic stub placement.
- Zoom/navigation is blocked while any edit mode is active. Exit edit mode before zooming into a marker/cluster or using Back.

## Configuration Key Design

The layout must be uniquely identified by all factors that affect the radial extension calculation:

### Required Key Components:
1. **Location Set Hash**: Hash of all location names in the cluster (sorted)
   - Ensures same locations are present
   
2. **Zoom Level**: Current zoom scale
   - Different zoom levels may need different layouts
   
3. **View Center**: Centered coordinate (lat/lon or pixel position)
   - Different view centers affect marker positions
   
4. **Canvas Size**: Width and height of the displayed area
   - Window resizing changes available space
   
5. **Configuration Parameters**:
   - `MinLocationsForExtension`
   - `ProximityThresholdPixels`
   - `ExtensionLineLength`
   - `MinimumLineLength`
   - Any other parameters that affect calculation

### Key Generation Strategy:
```csharp
string GenerateLayoutKey(DenseMarkerGroup group, ViewportState viewport, VisualConfig config)
{
    var locationNames = group.Locations.Select(l => l.Name).OrderBy(n => n).ToList();
    var locationHash = ComputeHash(string.Join("|", locationNames));
    
    return $"{locationHash}_{viewport.ZoomLevel:F2}_{viewport.CenterX:F2}_{viewport.CenterY:F2}_{viewport.Width}x{viewport.Height}_{config.RadialExtension.MinLocationsForExtension}_{config.RadialExtension.ProximityThresholdPixels:F1}";
}
```

## Data Model

Terminology for seeds, layout variants, and layout groups lives in [GLOSSARY.md](../reference/GLOSSARY.md).

### JSON Schema (current — `LayoutGroups` with multiple variants)

```json
{
  "LayoutGroups": {
    "abc123_z55.00_c2458.10_2571.57_s179x101_m3_p10.0_l50.0_n13.0": {
      "GroupKey": "abc123_z55.00_...",
      "Variants": [
        {
          "VariantId": "seed-default",
          "DisplayName": "Generated Seed",
          "Origin": "AutoSeed",
          "IsDefault": true,
          "GeneratorVersion": "ManualLayoutSeedGenerator/1.0",
          "Markers": []
        },
        {
          "VariantId": "manual-default",
          "DisplayName": "Manual Layout",
          "Origin": "Manual",
          "IsDefault": true,
          "BasedOnVariantId": "seed-default",
          "Markers": []
        },
        {
          "VariantId": "wider-spread-a1b2c3d4",
          "DisplayName": "Wider spread",
          "Origin": "Manual",
          "IsDefault": false,
          "Markers": []
        }
      ]
    }
  },
  "SelectedVariants": {
    "abc123_z55.00_...": "wider-spread-a1b2c3d4"
  }
}
```

**Key rules:**
- `LayoutGroups` is the authoritative store; the legacy `Layouts` flat dict is rebuilt on load.
- `Origin` values: `AutoSeed` (generated by the seed generator), `Manual` (user-authored), `Imported`.
- Each group may have at most 10 Manual/Imported variants (cap enforced by service).
- `SelectedVariants` records the user's last-chosen variant per group key; takes precedence over priority-based selection.
- AutoSeed variants cannot be deleted via the UI; they are updated only by seed regeneration.
- Seed regeneration (`scripts/generate_manual_layout_seeds.ps1`, a thin wrapper over `Tools/ManualLayoutSeedGenerator`) updates the `seed-default` AutoSeed variant in each generated group but **never** touches Manual or Imported variants.

### Variant Model (C# types)

| Type | Location | Purpose |
|------|----------|---------|
| `ManualLayoutOrigin` enum | `Models/ManualLayout.cs` | `Manual`, `AutoSeed`, `Imported` |
| `ManualLayout` | `Models/ManualLayout.cs` | Single variant: markers, VariantId, DisplayName, Origin, IsDefault |
| `ManualLayoutGroup` | `Models/ManualLayout.cs` | One group key → list of `ManualLayout` variants |
| `ManualLayoutCollection` | `Models/ManualLayout.cs` | Top-level: LayoutGroups + SelectedVariants |
| `ManualLayoutSummary` record | `Models/ManualLayoutSummary.cs` | Lightweight view of a variant for UI listing |

## Variant UI Flows (edit mode)

When edit mode opens, the variant picker dropdown is populated via `LayoutEditorController.GetVariants()`.
Each item shows `DisplayName [Origin]` (e.g., "Wider spread [Manual]").

| Action | Trigger | Service call |
|--------|---------|-------------|
| Switch variant | ComboBox selection change | `LayoutEditorController.SwitchToVariant(variantId)` → `ApplyManualLayout` |
| Save (Manual variant) | "Save Layout" button | `LayoutEditorController.TrySave(extensions, assignments)` |
| Save (AutoSeed variant) | "Save Layout" button | Redirected to inline Save As row |
| Save As | "Save As..." button | Show inline text field → `LayoutEditorController.TrySaveAsVariant(name, ...)` |
| Delete variant | "Delete Variant" button | Confirm dialog → `LayoutEditorController.TryDeleteActiveVariant()` |
| Delete layout | "Delete and Recalculate" button | `LayoutEditorController.TryDelete()` — removes all Manual variants |

**Persistence:** The selected variant is stored in `SelectedVariants` and reloaded on next session.
Stale entries (pointing to deleted variants) are cleared on the next load and fall back to priority selection.

**Variant id generation:** Save As slugifies the display name and appends an 8-character GUID suffix
(e.g., "wider-spread-a1b2c3d4"). This prevents collisions while keeping ids human-readable.

## Implementation Approach

### Phase 1: Data Infrastructure
1. Create `Models/ManualLayout.cs` - data model for saved layouts
2. Create `Services/ManualLayoutManager.cs` - handles save/load/delete operations
3. Add configuration flag to `visual-config.json`
4. Implement layout key generation algorithm

### Phase 2: Edit Mode UI
1. Add "Edit Layout" button to MainWindow (conditionally visible)
2. Implement edit mode state management
3. Add "Save Layout" and "Delete & Recalculate" buttons (visible only in edit mode)
4. Add visual indicator for when manual layout is active

### Phase 3: Drag Functionality
1. Make markers draggable in edit mode:
   - Add MouseDown, MouseMove, MouseUp handlers to marker elements
   - Track which marker is being dragged
   - Update marker position and redraw line in real-time
2. Implement constraints (bounds checking, origin point fixed)
3. Add visual feedback (highlight, cursor changes)

### Phase 4: Integration
1. Modify `ShowZoomedView` to check for saved layout before calculating
2. If saved layout exists and matches, load it instead of calculating
3. Add visual indicator when using manual layout
4. Implement save functionality to persist layouts
5. Implement delete functionality to remove layouts

### Phase 5: Polish
1. Add confirmation dialogs for save/delete operations
2. Add undo/redo for drag operations (optional)
3. Add snap-to-grid or angle snapping (optional)
4. Add layout validation (check for intersections, warn user)
5. Add export/import functionality for sharing layouts

## Technical Considerations

### Performance
- Layout lookup should be O(1) using dictionary with generated key
- Drag operations should be smooth (60fps minimum)
- Consider caching layout keys to avoid recalculation

### Robustness
- Handle missing layouts gracefully (fall back to automatic)
- Validate loaded layouts (check all locations exist, positions are valid)
- Handle configuration changes (warn if saved layout may be invalid)
- Consider versioning for layout file format

### User Experience
- Clear visual distinction between edit mode and normal mode
- Obvious save/cancel options
- Confirmation before overwriting existing layout
- Status messages for save/load/delete operations
- Keyboard shortcuts (Esc to cancel, Ctrl+S to save)

### Edge Cases
- Window resize while in edit mode
- Zoom change while in edit mode
- Location data changes (locations added/removed)
- Multiple users editing same layout (file locking?)
- Very large number of saved layouts (performance, file size)

## File Structure

```
Models/
  ManualLayout.cs              - Data model for saved layout
  ManualLayoutMarker.cs        - Individual marker position data

Services/
  ManualLayoutManager.cs       - Save/load/delete operations
  LayoutKeyGenerator.cs        - Generate unique keys for layouts

Tools/
  ManualLayoutSeedGenerator/   - Headless seed generator using runtime placement code

scripts/
  generate_manual_layout_seeds.ps1 - Thin wrapper around the seed generator
  verify_manual_layout_seeds.ps1   - Non-destructive seed verification into temp/

Images&Content/
  manual-layouts.json          - Saved layouts file

visual-config.json             - Add EnableManualLayoutEditor flag
```

## Configuration Changes

Add to `visual-config.json`:
```json
{
  "ManualLayoutEditor": {
    "Enabled": false,
    "ShowEditButton": true,
    "LayoutStoragePath": "Images&Content/manual-layouts.json",
    "EnableSnapToGrid": false,
    "GridSize": 5.0,
    "ShowLayoutIndicator": true
  }
}
```

## Testing Strategy

1. **Unit Tests**:
   - Layout key generation (same inputs = same key)
   - Save/load operations
   - Layout validation

2. **Integration Tests**:
   - Save layout, reload application, verify layout loads
   - Change configuration, verify layout doesn't load
   - Delete layout, verify automatic calculation runs

3. **Manual Testing**:
   - Drag markers smoothly without lag
   - Save and load layouts across sessions
   - Edit mode doesn't interfere with normal operation
   - Layouts work correctly after window resize

## Future Enhancements

1. **Layout Templates**: Save layouts as templates for similar clusters
2. **Batch Edit**: Edit multiple markers simultaneously
3. **Layout Sharing**: Export/import layouts for collaboration
4. **Layout History**: Keep history of layout changes with rollback
5. **Smart Suggestions**: AI-assisted layout improvements
6. **Collision Detection**: Real-time warning when markers overlap
7. **Alignment Tools**: Align markers to grid, distribute evenly, etc.
8. **Layout Presets**: Common patterns (circular, linear, etc.)

## Risks and Mitigations

| Risk | Impact | Mitigation |
|------|--------|------------|
| Saved layouts become invalid after code changes | High | Version layouts, validate on load |
| File corruption | Medium | Backup before save, validate JSON |
| Performance with many layouts | Medium | Index by key, lazy loading |
| User confusion about edit mode | Medium | Clear UI, tooltips, help documentation |
| Layouts don't work after window resize | High | Include canvas size in key, or make layouts resolution-independent |

## Success Criteria

1. Users can enter edit mode and drag markers smoothly
2. Saved layouts persist across application restarts
3. Layouts automatically load when matching configuration is detected
4. No performance degradation in normal (non-edit) mode
5. Clear visual feedback for all operations
6. Robust error handling for edge cases

## Timeline Estimate

- Phase 1 (Data Infrastructure): 4-6 hours
- Phase 2 (Edit Mode UI): 3-4 hours
- Phase 3 (Drag Functionality): 6-8 hours
- Phase 4 (Integration): 4-6 hours
- Phase 5 (Polish): 4-6 hours
- Testing and Bug Fixes: 4-6 hours

**Total: 25-36 hours**

## Conclusion

The manual layout editor provides essential fallback functionality when automatic algorithms fail. By carefully keying layouts to their configuration context and providing intuitive drag-and-drop editing, users can achieve perfect layouts while still benefiting from automatic calculation in most cases.
