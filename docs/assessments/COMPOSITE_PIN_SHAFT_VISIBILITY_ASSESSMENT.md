# Composite Pin Shaft Visibility Assessment

Date: 2026-06-10

## Context

The anti-aliasing, gated pre-rasterization, and depth sorting work improved pin rendering quality, but the screenshot review shows a separate visibility problem: some shafts and short stubs are still hard to read against the map.

This is most visible when thin gray shafts cross:

- dark city-label text
- blue/green map features
- textured beige background areas with similar luminance
- dense clusters where many shafts overlap in a small area

The colored heads remain legible. The weak element is the shaft body, especially the screen-up stub segments.

## Assessment

The current shaft style depends on a subtle mid-gray/light-gray rendered image. That style can look polished in isolation, but it has poor contrast against a historical map background with heavy labels and varied terrain colors. The issue is therefore not primarily pixelation anymore; it is contrast and figure-ground separation.

Pre-rasterization can reduce seam/edge artifacts, but it cannot make a low-contrast shaft stand out if the shaft color is too close to the map underneath it. The next plan should treat shaft visibility as a visual contrast problem.

## Proposed Ideas

### 1. Runtime shaft halo / outline

Render a dark or light halo behind the shaft before rendering the normal shaft. This could be done by adding an expanded silhouette layer, a duplicate shaft layer with darker tint and blur, or a WPF effect behind the prerasterized/live shaft.

Pros:
- works for all existing pin part assets
- configurable at runtime
- can be tuned without regenerating images
- likely best first MVP

Cons:
- may look too graphic if the halo is too thick
- needs care so dense clusters do not become muddy

Possible config:

- `PinParts.ShaftHaloEnabled`
- `PinParts.ShaftHaloColor`
- `PinParts.ShaftHaloThicknessPx`
- `PinParts.ShaftHaloOpacity`

### 2. Runtime shaft tint / contrast boost

Apply a tint or brightness/contrast adjustment to shaft layers before or during rendering. A darker charcoal shaft, brighter white shaft, or two-tone shaft may read better than the current gray.

Pros:
- simpler than per-asset editing
- can be controlled with config presets
- pairs well with halo/outline

Cons:
- global color choices may fail on some map regions
- WPF bitmap color processing may need a helper/cache to avoid per-frame cost

Possible modes:

- preserve current assets
- darken shaft
- brighten shaft
- force single high-contrast shaft color

### 3. New shaft asset variants

Generate or edit new `Pins_v2/parts` shaft images with stronger contrast, such as black/dark outlines, brighter center strokes, or cleaner two-tone shading.

Pros:
- visually clean if done well
- avoids runtime image-processing complexity
- keeps per-marker runtime cost close to the current bitmap rendering path
- pays most of the contrast/outline work once during asset creation and image decode
- can preserve a hand-designed look

Cons:
- requires asset generation and visual QA for every shaft
- harder to tune per map/background
- may need updated geometry metadata if visible bounds change

Good asset directions:

- dark 1-2 px outline around each shaft
- light inner highlight plus dark outer edge
- slightly wider shaft body for stubs
- stronger endpoint contrast near the head

### 4. Adaptive contrast from map sampling

Sample pixels along or near the shaft path and choose a dark or light shaft/halo style depending on the local map luminance.

Pros:
- strongest theoretical contrast
- can adapt to labels, rivers, and beige areas

Cons:
- more complex
- requires access to map pixels in the current viewport
- risk of flickering or inconsistent style during pan/zoom unless cached carefully

This is probably not the MVP.

### 5. Geometry and layout tuning

Increase minimum shaft/stub length or shaft half-width, especially for stubs. The current visibility problem is amplified by very short, thin marks.

Pros:
- easy to test with existing config (`DefaultStubLengthPixels`, `TargetShaftHalfWidthPx`)
- no new rendering architecture

Cons:
- wider/longer shafts can increase clutter
- does not fully solve contrast over dark text

## Recommended Direction

Use runtime treatment as a fast prototype path, but treat baked asset variants as the likely production path if the visual direction is clear. Asset variants have the important advantage of avoiding repeated runtime tinting, blur, dilation, or halo-generation work across many pins. They also keep rendering closer to the current WPF bitmap-transform path.

A practical sequence is:

1. Prototype shaft visibility styles quickly with config-gated runtime halo/tint controls.
2. Pick the best look from screenshot comparisons.
3. Bake that look into new outlined or higher-contrast shaft/stub asset variants.
4. Keep runtime config for selecting the shaft asset set and minor geometry/opacity tuning, not for expensive per-marker image processing.

If we already know the desired look, it may be worth skipping most runtime image processing and going directly to a small asset-variant spike.

Suggested future plan order:

1. Create a small set of candidate shaft/stub asset variants: darker outline, light inner highlight, and slightly bolder stub.
2. Add config selection for the shaft asset variant set.
3. Optionally add a cheap runtime halo/tint prototype only if asset-only candidates are inconclusive.
4. Capture before/after screenshots over text-heavy map areas.
5. Choose the default variant and document any remaining runtime controls.

## Open Questions For The Plan

- Should the halo be dark, light, or dual-tone by default?
- Should stubs use a stronger style than long radial-extension shafts?
- Should shaft visibility settings apply globally or only when `UsePrerasterizedRendering` is enabled?
- How much visual boldness is acceptable before the pins feel too modern for the map style?
