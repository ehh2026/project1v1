# Glossary

Shared vocabulary for docs, plans, tests, and code comments.

## Pins And Locations

- **Location**: A named point from the coordinate source, with map pixel coordinates and associated content.
- **Dense-cluster location**: A location that belongs to a dense cluster. On the unzoomed full map it is represented by the cluster aggregate marker, so it does not render as its own pin until the user zooms into that cluster.
- **Standalone location**: A location that does not belong to a dense cluster. It renders as an individual pin on the unzoomed full map and keeps the same individual-pin behavior when zoomed.
- **Pin**: The visible marker for an individual location. Depending on config, this may be drawn directly or assembled from composite pin parts.
- **Auto stub pin**: A standalone location pin that has not been edited in Edit Layout mode. It uses the automatic short stub shape: a pin head plus a short, vertical shaft using the configured default stub length.
- **Manual-layout pin**: A standalone location pin whose endpoint was edited in Edit Layout mode. Its saved layout controls the pin's shaft angle and length instead of the automatic vertical stub.
- **Composite pin**: A pin assembled from image parts, currently a shaft and head asset.
- **Drawn pin**: A pin rendered by WPF drawing primitives instead of composite image parts.

## Manual Layouts

- **Manual layout**: Saved marker endpoint positions for a layout group. Manual layouts override automatic radial extension placement when loaded.
- **Layout group**: A set of layout variants keyed by the relevant cluster, viewport, zoom, and radial-extension settings. Stored under `ManualLayoutCollection.LayoutGroups`.
- **Layout variant**: One saved layout choice inside a layout group. Variants have a `VariantId`, `DisplayName`, `Origin`, and marker list.
- **Manual variant**: A user-authored layout variant with `Origin = Manual`. Manual variants must not be overwritten by seed regeneration.
- **Imported variant**: A layout variant brought in from outside the normal in-app save flow, with `Origin = Imported`.
- **Selected variant**: The explicit per-group user choice stored in `ManualLayoutCollection.SelectedVariants`. It takes precedence over origin-priority fallback.
- **Layout key**: The generated string used to identify a layout group. Cluster layout keys come from `LayoutKeyGenerator.GenerateKey`; full-map layouts use the constant `fullmap`.
- **Full-map layout**: A manual layout for the unzoomed whole-map view. Full-map layouts are size-independent and keyed as `fullmap`.

## Seeds

- **Seed**: Pre-generated manual-layout data used as a starting/default layout. In this repo, "seed" does not mean a random-number seed.
- **AutoSeed variant**: A generated seed layout variant with `Origin = AutoSeed`, usually `VariantId = seed-default`.
- **Seed generator**: The headless tool at `Tools/ManualLayoutSeedGenerator` that generates AutoSeed variants by reusing runtime placement code.
- **Seed regeneration**: Running `scripts/generate_manual_layout_seeds.ps1`, which delegates to the seed generator. Regeneration updates `seed-default` AutoSeed variants and must preserve Manual/Imported variants and `SelectedVariants`.
- **Seed verification**: Running `scripts/verify_manual_layout_seeds.ps1`, which generates seeds to `temp/` and verifies the output without modifying the real `Images&Content/Demo-Content/manual-layouts.json`.

## Content Sets

- **Content set**: A self-contained dataset under `Images&Content/` — typically `Demo-Content/` or `Production-Content/` — with its own coordinate source (`Coordinates for map.xlsx` and/or `locations.json`), location subfolders, and optional bundled `manual-layouts.json`. Static maps and pin art live separately under `Assets/`. See [CONTENT_SETS.md](../guides/CONTENT_SETS.md).
- **Active content set**: The content set selected at startup (Production if it has a coordinate source, else Demo, else legacy flat root). Location content and Excel/JSON loading use this folder for the session.
