# Tuning Categories, Drawn-Pin Sizing, and Marker Hitboxes Design

**Date:** July 1, 2026  
**Status:** Approved for implementation planning

## Goal

Reduce Runtime Tuning clutter, expose the existing drawn-pin dimensions, and
give mouse and touch users predictable circular targets centered on pin heads
and cluster images.

## Scope

This slice:

- changes the Tuning button into a category chooser;
- shows one tuning category at a time in the existing panel shell;
- exposes drawn-pin head diameter, shaft width, and shaft length;
- adds one shared pin hit-target diameter and one cluster hit-target diameter;
- centers drawn- and composite-pin targets on the visible head;
- centers cluster targets on the cluster marker image;
- keeps each effective target at least as large as its visible head or marker;
- preserves marker hover, click, right-click, and layout-edit drag behavior.

Buttons, content-window controls, and thumbnail hit areas are unchanged. They
continue to use their existing WPF visual/control bounds.

## Runtime Tuning Navigation

Clicking **Tuning** opens four category choices:

1. **Map**
2. **Composite Pins**
3. **Drawn Pins**
4. **Hitboxes**

Selecting a category opens the existing `DeveloperTuningPanel` shell and shows
only that category. The panel header identifies the selected category. Apply,
Reload, Save, validation messages, and status messages stay in a shared footer
so state and persistence logic are not duplicated.

The categories contain:

- **Map:** cluster threshold, location-marker size, cluster-marker size, and
  auto-open-single-content behavior.
- **Composite Pins:** composite/prerasterized rendering, debug overlay, lit
  shafts, shaft/head variants, stub length, target head radius, and target
  shaft half-width.
- **Drawn Pins:** head diameter, shaft width, shaft length, and all drawn-pin
  tip-cap controls.
- **Hitboxes:** shared pin diameter and cluster diameter.

The category chooser replaces the current direct show/hide action. Choosing the
currently visible category toggles the panel closed; choosing another category
switches the visible section without opening a second panel.

## Drawn-Pin Dimensions

The new controls map directly to existing `VisualConfig.PinMarkers` values:

- **Head diameter** -> `BallSize`
- **Shaft width** -> `ShaftWidth`
- **Shaft length** -> `ShaftLength`

All three values must be positive and finite. Applying them recreates or
refreshes drawn pin visuals while preserving the current map/cluster view and
saved manual-layout positions. Composite rendering values are not changed by
these controls.

The labels use “head diameter” rather than the model's historical “ball size”
term because that describes the visible result more clearly.

## Hit-Target Configuration

Add a focused configuration object under `VisualConfig`:

```csharp
public sealed class MarkerHitTargetConfig
{
    public double PinDiameterPx { get; set; } = 32.0;
    public double ClusterDiameterPx { get; set; } = 40.0;
}
```

Both values must be positive and finite. One configured pin diameter applies
to drawn and composite pins and to both mouse and touch input. The cluster
diameter applies to cluster markers and both input types.

The effective diameters are:

```text
effective pin diameter = max(configured pin diameter, visible head diameter)
effective cluster diameter = max(configured cluster diameter,
                                 max(cluster image width, cluster image height))
```

This permits larger accessibility targets while preventing tuning from making
any visible head or cluster image partly non-interactive.

## Interaction Layer

Add a transparent marker-interaction layer above the map's visual marker layer.
It owns one circular target per visible location pin or cluster marker. Marker
visuals no longer own pointer input, which prevents the shaft or a composite
pin's full rectangular bounds from acting as the pin target.

Each target retains an association with its visual marker and routes existing
behavior to that marker:

- hover enter/leave animation;
- primary-button navigation/content opening;
- composite-pin right-click override;
- layout-editor drag start, move, and end.

The interaction layer must not alter marker z-order, extension-line rendering,
or map-coordinate placement.

### Target Centers

Target centers come from the same geometry used to render and place the
corresponding marker:

- **Auto-stub drawn pin:** the `AutoStubPinMarker` head connection point,
  transformed into map-canvas coordinates.
- **Manual-layout drawn pin:** the `ManualLayoutPinMarker` head center,
  transformed into map-canvas coordinates.
- **Composite pin:** `CompositePinRenderPlan.HeadCenterLocal`, offset by the
  composite marker's map-canvas position.
- **Cluster marker:** the center of the rendered cluster marker/image bounds.

Targets are refreshed whenever marker visuals are created, repositioned,
replaced between drawn/composite modes, dragged, or cleared. They are also
refreshed after Runtime Tuning applies relevant dimension or hit-target
changes.

## Ownership and Boundaries

- `Models/MarkerHitTargetConfig.cs` owns persisted hit-target values only.
- A small pure policy/helper owns effective-diameter and center calculations.
- A focused interaction-layer controller owns target creation, synchronization,
  removal, and marker association.
- `DeveloperTuningPanel` owns category presentation and input parsing.
- `MainWindow` remains the composition root that routes marker actions and
  coordinates refreshes.

The interaction controller must not load configuration files or duplicate map
navigation decisions. Views continue to depend only on Models.

## Error Handling

Invalid numeric tuning text blocks Apply with the existing inline error
presentation. Reload rejects non-positive or non-finite drawn-pin dimensions
and hit-target diameters without partially applying the file. Save persists
only validated in-memory values.

If a visual marker temporarily lacks render geometry during replacement, its
target is withheld until authoritative center data is available rather than
placed at a guessed location.

## Testing

Automated coverage will include:

- category chooser entries and one-category-at-a-time visibility;
- category switching and same-category toggle behavior;
- loading, parsing, validation, applying, reloading, and saving the three
  drawn-pin dimensions;
- loading, validation, applying, reloading, and saving the 32 px pin and 40 px
  cluster target defaults;
- minimum-size enforcement for drawn heads, composite heads, and cluster
  images;
- exact center calculations for auto-stub drawn pins, manual-layout drawn
  pins, rotated composite pins, and clusters;
- interaction-target lifecycle across creation, reposition, drawn/composite
  replacement, drag, tuning refresh, and clear;
- routing of hover, primary click, composite right-click, and edit drag;
- structural architecture checks for the new model/helper/controller.

Focused tests run first during implementation. The Windows
`.\scripts\verify.ps1` gate must pass before completion.

## Documentation and Completion

When implementation is verified:

- remove the three completed bullets from `docs/TO_DO.md`;
- add an `[Unreleased]` changelog entry describing categorized tuning,
  drawn-pin sizing, and centered marker hit targets;
- keep any incomplete manual-only verification explicitly narrowed rather than
  marking it complete.

## Out of Scope

- content-window appearance tuning;
- changing button, content-control, or thumbnail hit areas;
- separate mouse and touch diameters;
- separate drawn-pin and composite-pin target diameters;
- non-circular marker hit targets;
- visual debug outlines for production hit targets.
