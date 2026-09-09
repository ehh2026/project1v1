# Web map bundle

Static site for the gallery map. Built by `scripts/prepare_web_assets.py` (repo venv);
generated `web/images/` and `web/data/` are **gitignored** — rerun the script to rebuild.

## Files

| Path | Origin |
|------|--------|
| `index.html` | committed — the site |
| `test-projection.html` | committed — coordinate fixture |
| `vendor/leaflet/` | committed — self-hosted Leaflet 1.9.4 |
| `images/map-base.jpg` | generated — ~4096 px progressive JPEG from the 16397×11085 master |
| `images/tiles/{z}/{x}/{y}.jpg` | generated — tile pyramid (level 5 = 8192 px; level 6 = native master, opt-in) |
| `images/content/<Location>__loc_NNN/` | generated — bounded popup derivatives (≤1600 px, q80) + text sidecars |
| `data/locations.json` | generated — see schema below |

## `data/locations.json` schema

```json
{
  "map": { "width": 4096, "height": 2769, "image": "images/map-base.jpg" },
  "tiles": {
    "tileSize": 256,
    "baseZoom": 4,
    "url": "images/tiles/{z}/{x}/{y}.jpg",
    "levels": [5, 6]
  },
  "provenance": { "contentSet": "Images&Content/Demo-Content", "source": "excel" },
  "crops": [
    {
      "file": "images/crops/crop_01.jpg",
      "nx0": 0.28, "ny0": 0.44, "nx1": 0.32, "ny1": 0.47,
      "members": [ "Kevin", "Test2", "Anonymous" ],
      "overBudgetBytes": false
    }
  ],
  "locations": [
    {
      "id": "kevin",
      "name": "…",
      "nx": 0.3001,
      "ny": 0.465,
      "address": "…",
      "bio": "…",
      "images": [ { "file": "images/content/<dir>/<file>", "altText": "caption or fallback" } ]
    }
  ]
}
```

- `map.image` is the intermediate base map; `width`/`height` are its pixel size. Overlay bounds are `[[0, 0], [H, W]]` under `CRS.Simple`.
- `tiles` is the whole-map tile pyramid. `levels` lists the generated URL zoom levels; level `baseZoom + n` is a whole-map image `2^n` × the base. The renderer creates a `L.tileLayer(url, { tileSize, zoomOffset: baseZoom, minZoom: min(levels)-baseZoom, maxZoom: 6, maxNativeZoom: max(levels)-baseZoom, noWrap: true, bounds })` — deep zooms above the native level request native tiles and scale them, never nonexistent levels. Tile layout is the Leaflet `CRS.Simple` projected grid: world origin `(lng 0, lat 0)` is the image's **bottom-left**, `lat` decreases below that, so URL `y` is **negative** above the map top (level 5 rows `-22..-1`, level 6 rows `-44..-1`, at 4096×2769). Each tile covers level-pixel `x ∈ [256x, 256x+256)`, y-down `∈ [H_level + 256y, H_level + 256y + 256)` where `H_level` is the level's pixel height. Rows above the map top are blank. Keep `baseZoom` and the formula in sync with `scripts/prepare_web_assets.py`.
- `crops[]` are full-resolution regional cuts from the 16397×11085 master. Their `nx0..ny1` are **[0,1] fractions of the whole map** (origin top-left), mapped to Leaflet bounds exactly like locations: a crop bounding `nx0..nx1 / ny0..ny1` is shown at `[[(1-ny1)*H, nx0*W], [(1-ny0)*H, nx1*W]]`. Show them only when zoomed in (viewport intersects the bounds). `overBudgetBytes` is `true` only when a crop had to be downscaled to fit the per-crop byte budget.
- `locations[].nx`, `ny` are **[0,1] fractions, origin top-left of the map image**. Renderers map them as
  `lat = (1 - ny) * height`, `lng = nx * width` against the `map.width`/`map.height` in the manifest.
- `locations[].id` is a **stable, deep-link-friendly slug derived from the location name** (ASCII-normalized,
  lowercased, `[a-z0-9-]`, `#location=<id>` opens it). It survives source-row reordering; duplicate names get
  deterministic `-1`/`-2`… suffixes ordered by content, never by row position. Renaming a cell changes the slug,
  and duplicate-name suffixes can shift if another location sharing the slug is added or removed.
- `images[].altText` is pre-baked (caption from the Excel captions sheet, else `"<Location> image <N>"`);
  renderers must use it directly — there is no renderer-time lookup of captions or sidecar files.
- All on-disk paths (map, crops, content images) are ASCII-safe and match the renderer's
  allow list `^images/(base|crops|content)/[\w\-. ]+` — the pre-bake sanitizes names accordingly.

## Accessibility notes

- Pins are `role="button"` (Leaflet sets it for `keyboard: true`) with an `aria-label`
  from the location name; Enter opens via Leaflet's keypress handler, Space toggles,
  and Leaflet's `autoPanOnFocus` pans a focused pin into view. Escape closes a popup
  (`closeOnEscapeKey`) and focus returns to the originating marker.
- Contrast (measured 2026-09-09, WCAG relative-luminance formula): pin fill `#e04a5e`
  vs page background `#0d1b2a` = **4.4:1** (border `#ffffff` vs background = 17.4:1);
  focus ring `#ffd166` on the dark background = 12.1:1. Pins may sit on mid-toned map
  areas, where the 2px white ring supplies the delineation.

## Run locally

```sh
py -3 -m http.server   # in web/, then open http://localhost:8000/
```

## Verify a generated bundle

After regenerating, run the coherence gate (stdlib, exits nonzero on any
contract violation: ids/coords, image/crop/tile files on disk, sample tile
grid, crop payload budget):

```sh
py -3 scripts/verify_web_bundle.py web
```
