# Content Folder (`Images&Content/`)

This folder contains the world map images, pin assets, and location-specific content for the Interactive World Map application.

## Folder Structure

- **`Assets/`**: Static app assets required by the application shell (e.g. world map images, pin-part graphics, cluster stamp).
- **`Demo-Content/`**: Default sample dataset containing locations, coordinates Excel, and associated image subfolders.
- **`Production-Content/`**: Production dataset directory. If present and populated with a coordinate source (`locations.json` or `Coordinates for map.xlsx`), the application will prioritize this set over `Demo-Content/`.
- **`Extras/`**: Archival/legacy files not used by the active application logic.

For details on how content sets are loaded and resolved, see [CONTENT_SETS.md](../docs/guides/CONTENT_SETS.md).