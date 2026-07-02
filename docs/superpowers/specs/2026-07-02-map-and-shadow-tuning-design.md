# Map and Shadow Runtime Tuning Design

**Date:** July 2, 2026  
**Status:** Approved for implementation planning

## Goal

Complete the Runtime Tuning **Map** category and add coherent, independently
configurable shadows for location pins and cluster markers.

## Scope

This slice:

- exposes zoom scale, zoom animation duration, cluster badge size, and cluster
  count font size in the existing Map category;
- adds a dedicated **Shadows** tuning category;
- keeps the existing shared pin-shadow configuration compatible;
- makes pin shadow enablement and opacity apply consistently to drawn pin heads,
  drawn extended shafts, and composite pin heads;
- adds separate cluster-shadow enablement and opacity configuration;
- applies supported changes immediately and persists them through the existing
  Apply, Reload, and Save flow.

It does not add free zooming, intermediate zoom levels, new shadow geometry
controls, or controls for window/dialog shadows.

## Runtime Tuning Layout

The Tuning chooser gains a fifth category named **Shadows**.

The **Map** category contains:

- auto-open single-location content after zoom;
- settled zoomed-map resampling mode;
- cluster threshold;
- location-marker size;
- cluster-marker size;
- cluster badge size;
- cluster count font size;
- zoom scale;
- zoom animation duration in milliseconds.

The **Shadows** category contains two groups:

- **Pin shadows:** Enabled and Opacity.
- **Cluster shadows:** Enabled and Opacity.

Opacity is entered as a decimal from `0.0` through `1.0`. Disabled shadows are
removed rather than retained with zero opacity. Tooltips state which visuals
each group controls.

## Configuration

The existing pin settings remain the compatibility contract:

```csharp
VisualConfig.PinMarkers.ShowShadow
VisualConfig.PinMarkers.ShadowOpacity
```

Add a focused model for cluster shadows:

```csharp
public sealed class ClusterMarkerShadowConfig
{
    public bool Enabled { get; set; } = false;
    public double Opacity { get; set; } = 0.0;
}
```

`VisualConfig.ClusterMarkerShadow` owns this object. Defaults are shadow-off,
matching the preferred cluster presentation and preserving safe behavior when
the section is absent from older configuration files. The checked-in
`visual-config.json` explicitly records the disabled cluster-shadow choice.

Pin opacity is no longer floored or overridden. Older files that specify
`PinMarkers.ShowShadow` and `PinMarkers.ShadowOpacity` continue to load without
migration.

## Rendering Behavior

Pin shadow settings govern:

- the drawn pin head;
- the drawn extended-shaft line;
- the composite pin head.

They do not add a runtime effect to the ordinary drawn stub shaft or composite
shaft images. Composite shaft depth remains part of the asset.

Cluster shadow settings govern:

- the cluster stamp/fallback marker body;
- the count badge.

Both cluster elements use the configured opacity. Their existing blur radius
and shadow depth remain fixed. Pin elements likewise retain their existing
blur radius and depth; this slice tunes presence and strength only.

Views receive model configuration or explicit primitive shadow values through
their existing construction/update paths. Views do not read configuration
files or depend on Services.

## Live Apply Semantics

Map values follow these rules:

- cluster badge and font-size changes are recreate-class changes because
  existing `ClusterMarker` instances capture those values during construction;
- zoom scale is applied to the current zoomed cluster by rebuilding that
  settled zoomed view; on the full map it affects the next zoom;
- animation duration affects subsequent zoom and back transitions and does not
  restart an animation already in progress;
- the existing tuning guard continues to reject Apply while animation or Edit
  Layout is active.

Shadow changes refresh the currently visible marker visuals without reloading
content or discarding saved manual-layout positions. The refresh covers the
full-map root and an active zoomed cluster.

No tuning apply may partially mutate configuration after validation failure.

## Validation

The panel and reload path apply the same rules:

- cluster badge size and cluster count font size must be positive and finite;
- zoom scale must be positive and finite;
- animation duration must be a positive integer;
- pin and cluster opacity must be finite and within `0.0` through `1.0`,
  inclusive.

Existing validation rules remain unchanged for existing controls. Validation
errors use the shared inline error/status presentation.

## Ownership and Modularity

- `Models/ClusterMarkerShadowConfig.cs` owns persisted cluster-shadow values.
- `Models/VisualConfig.cs` exposes the new config object and retains existing
  map and pin-shadow properties.
- `Views/DeveloperTuningPanel.xaml(.cs)` owns category presentation, parsing,
  and validation.
- Marker views own applying or removing their local WPF `DropShadowEffect`.
- `MainWindow.DeveloperTuning.partial.cs` owns event-argument mapping,
  configuration mutation, change classification, and refresh orchestration.

The implementation should add focused application methods to marker views
rather than duplicating effect construction across `MainWindow`. No touched
file is expected to cross the repository's 800-line limit; if the tuning
partial approaches that boundary, extract pure change classification rather
than adding another orchestration responsibility.

## Testing

Automated coverage includes:

- the fifth Shadows category and one-category-at-a-time visibility;
- Map control load, parse, validation, event mapping, reload, apply, and save;
- default/load/save behavior for `ClusterMarkerShadowConfig`;
- opacity boundary acceptance at `0.0` and `1.0`, with rejection outside the
  range and for non-finite values;
- exact removal/application of drawn-head, drawn extended-shaft, composite-head,
  cluster-body, and cluster-badge shadows;
- removal of the drawn extended-shaft `0.45` opacity floor;
- absence of hardcoded pin and cluster opacity values in the governed render
  paths;
- live refresh behavior in full-map and zoomed states;
- architecture dependency rules.

Focused tests run first. The Windows `.\scripts\verify.ps1` gate must pass
before completion.

## Documentation and Completion

When implementation is verified:

- remove the completed high-priority zoom/cluster-tuning bullet from
  `docs/TO_DO.md`;
- remove the completed shadow-tuning bullet, or narrow it to any independently
  unfinished shadow-performance work still owned by the zoom-performance plan;
- update `docs/guides/VISUAL_CONFIG.md` for all new controls and semantics;
- add an `[Unreleased]` changelog entry;
- mark the implementation plan complete and archive it according to repository
  workflow.

## Out of Scope

- shadow color, blur radius, direction, or depth controls;
- independent opacity per pin sub-element or cluster sub-element;
- shadows for content, thumbnail, didactic, or other windows;
- changing baked lighting/shadows in composite pin assets;
- changes to zoom navigation structure or gesture support;
- resolving unrelated shadow-performance tasks beyond the governed marker
  effects.
