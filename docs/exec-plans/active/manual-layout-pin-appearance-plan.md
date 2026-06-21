---
status: active
owner: agent
started: 2026-06-21
requirements_ref: manual-layout-pin-appearance
parent_program: composite-pins-program.md
---

# Manual Layout Pin Appearance Pickers

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Implement UI for users to manually override pin head visuals (head asset for composite mode, base color for drawn mode) via the manual layout edit context menu, and persist those choices.

## Phase 1: Composite Pin Head Override UI
1. **Event Updates (`CompositePinMarker.xaml.cs`)**
   - Keep `ShaftOverrideRequested` but potentially rename it to `PartsOverrideRequested` (or leave it as is if it's treated as a general "override requested" entry point).
   - Alternatively, add a `HeadOverrideRequested` event. However, since the user clicks the whole composite pin, firing a single event on `OnMouseRightButtonUp` and building a unified context menu is simpler.
2. **Context Menu Update (`MainWindow.CompositePins.partial.cs`)**
   - Update `OnShaftOverrideRequested` to build a combined context menu containing both a "Change shaft" submenu (or section) and a "Change head" submenu (or section).
   - Use the loaded `_pinPartGeometry` (which is a `Dictionary<string, PinPartGeometryEntry>`) to identify available head source paths. Iterate over entries where the key represents a head or filter by parts that are heads.
3. **Menu Action**
   - On selecting a head, capture the current `PairId` from `RenderPlan.PairId` and the new `headSourcePath`.
   - Call `_overrideStore.SetOverride(locationName, capturedPairId, capturedHeadSourcePath)`.
   - Call `ApplyCompositePinToMarker(marker, originalPos, extendedPos, capturedPairId, capturedHeadSourcePath)`.
4. **Verification**
   - Verify infrastructure works by creating an override, saving the manual layout, reloading, and confirming the custom head persists (handled by existing `ManualLayoutMarker.HeadSourcePath` and `ManualLayoutAssignmentEnricher`).

## Phase 2: Drawn Pin Color Override Model & Store
1. **`ManualLayoutMarker` Update**
   - Add `public string? PinColor { get; set; }` to `Models/ManualLayoutMarker.cs`.
2. **Override Store Update**
   - Update `ManualLayoutOverrideStore.cs` to store drawn pin color overrides (e.g., `SetColorOverride`, `TryGetColorOverride`).
3. **Enricher / Save Update**
   - Update `ManualLayoutAssignmentEnricher.cs` or the layout save logic to read drawn pin colors from `PinMarker` or `OverrideStore` and save them into the `PinColor` property of the exported layout JSON.

## Phase 3: Drawn Pin Color Override UI
1. **Event Addition**
   - In `Views/PinMarker.xaml.cs`, override `OnMouseRightButtonUp` and fire a `PinColorOverrideRequested` event.
   - Expose the fixed palette (e.g. `public static IReadOnlyList<Color> Palette => PinColors;`).
2. **Context Menu in MainWindow**
   - Subscribe to `PinColorOverrideRequested`.
   - Show a `ContextMenu` listing the fixed color palette (potentially rendering colored rectangles/swatches in the menu items).
3. **Menu Action**
   - On color selection, call `pinMarker.SetPinColor(selectedColor)`.
   - Record the override in `_overrideStore` using the new color override tracking.
4. **Replay on Load**
   - When loading manual layouts, if `ManualLayoutMarker.PinColor` is set, parse the color and apply it to the `PinMarker` on creation.

## Phase 4: Integration and Testing
- Write/update tests for serialization of `PinColor` in `ManualLayoutManagerTests.cs`.
- Ensure tests pass via `scripts/verify.ps1`.
