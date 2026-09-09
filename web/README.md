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
      "id": "loc_001",
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
- `tiles` is the whole-map tile pyramid. `levels` lists the generated URL zoom levels; level `baseZoom + n` is a whole-map image `2^n` × the base. The renderer creates a `L.tileLayer(url, { tileSize, zoomOffset: baseZoom, minZoom: min(levels)-baseZoom, maxZoom: max(levels)-baseZoom, noWrap: true, bounds })`. Tile layout is the Leaflet `CRS.Simple` projected grid: world origin `(lng 0, lat 0)` is the image's **bottom-left**, `lat` decreases below that, so URL `y` is **negative** above the map top (level 5 has rows `-43..0`? — exact rows are `-ceil(H_level/256) .. -1`). Each tile covers level-pixel `x ∈ [256x, 256x+256)`, y-down `∈ [H_level + 256y, H_level + 256y + 256)` where `H_level` is the level's pixel height. Rows above the map top are blank. Keep `baseZoom` and the formula in sync with `scripts/prepare_web_assets.py`.
- `crops[]` are full-resolution regional cuts from the 16397×11085 master. Their `nx0..ny1` are **[0,1] fractions of the whole map** (origin top-left), mapped to Leaflet bounds exactly like locations: a crop bounding `nx0..nx1 / ny0..ny1` is shown at `[[(1-ny1)*H, nx0*W], [(1-ny0)*H, nx1*W]]`. Show them only when zoomed in (viewport intersects the bounds). `overBudgetBytes` is `true` only when a crop had to be downscaled to fit the per-crop byte budget.
- `locations[].nx`, `ny` are **[0,1] fractions, origin top-left of the map image**. Renderers map them as
  `lat = (1 - ny) * height`, `lng = nx * width` against the `map.width`/`map.height` in the manifest.
- `images[].altText` is pre-baked (caption from the Excel captions sheet, else `"<Location> image <N>"`);
  renderers must use it directly — there is no renderer-time lookup of captions or sidecar files.
- All on-disk paths (map, crops, content images) are ASCII-safe and match the renderer's
  allow list `^images/(base|crops|content)/[\w\-. ]+` — the pre-bake sanitizes names accordingly.

## Run locally

```sh
py -3 -m http.server   # in web/, then open http://localhost:8000/
```
