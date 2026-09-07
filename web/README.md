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
| `images/content/<Location>__loc_NNN/` | generated — bounded popup derivatives (≤1600 px, q80) + text sidecars |
| `data/locations.json` | generated — see schema below |

## `data/locations.json` schema

```json
{
  "map": { "width": 4096, "height": 2769, "image": "images/map-base.jpg" },
  "provenance": { "contentSet": "Images&Content/Demo-Content", "source": "excel" },
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

- `nx`, `ny` are **[0,1] fractions, origin top-left of the map image**. Renderers map them as
  `lat = (1 - ny) * height`, `lng = nx * width` against the `map.width`/`map.height` in the manifest.
- `images[].altText` is pre-baked (caption from the Excel captions sheet, else `"<Location> image <N>"`);
  renderers must use it directly — there is no renderer-time lookup of captions or sidecar files.

## Run locally

```sh
py -3 -m http.server   # in web/, then open http://localhost:8000/
```
