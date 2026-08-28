# Content Sets

How the app picks Demo vs Production content, how assets are resolved, and how to switch sets locally.

## Layout

```
Images&Content/
├── Assets/                 # static app assets (map images, pin parts, cluster stamp)
├── Demo-Content/           # sample dataset (Excel, locations.json, location folders, manual-layouts.json)
├── Production-Content/     # deployment dataset (optional; used when it has a coordinate source)
└── Extras/                 # unused / archival files (not loaded as a content set)
```

See also [Images&Content/README.md](../../Images&Content/README.md).

## How the active set is chosen

At startup, `ContentSetResolver` picks **one** active content set under `Images&Content/`:

1. **Production** — if `Production-Content/` exists **and** contains `Coordinates for map.xlsx` **or** `locations.json`
2. Else **Demo** — if `Demo-Content/` exists with the same kind of coordinate source
3. Else **Legacy** — the bare `Images&Content/` root (developer/test convenience only)

A set is valid only when an explicit coordinate source file is present. An empty `Production-Content/` folder does **not** count; the app falls through to Demo.

Static assets (world map, pin-part art, cluster stamp) always resolve from `Assets/` first, with a legacy-root fallback for older flat trees used in tests.

## Portable release content

The portable Windows download contains `Assets/` and `Demo-Content/`, but deliberately omits `Production-Content/` and `Extras/`. To use deployment data, add or replace `Images&Content/Production-Content` beside the extracted executable. It must contain `locations.json` or `Coordinates for map.xlsx`; on restart a valid Production set takes priority automatically. Keep `Assets/` intact. Rename Production to `Production-Content.disabled` and restart to fall back to Demo.

## Force Demo locally

There is no in-app “force Demo” toggle yet. To ignore Production on a machine that has both sets:

1. Rename `Images&Content/Production-Content` → `Images&Content/Production-Content.disabled`
2. Restart the app (it will select Demo)

Rename back to `Production-Content` when you want Production again.

## Manual layouts (per set)

Bundled seed layouts live next to the active set, for example:

- `Images&Content/Demo-Content/manual-layouts.json`

Writable user layouts are stored under `%AppData%\InteractiveWorldMap\` and are **namespaced by set** when `ManualLayoutEditor.SetAwareStorage` is `true` (default):

- `manual-layouts.demo.json`
- `manual-layouts.production.json`
- `manual-layouts.legacy.json`

On first run after upgrade, an un-namespaced `%AppData%\InteractiveWorldMap\manual-layouts.json` is copied once to `manual-layouts.demo.json` when that namespaced file does not yet exist.

Switching Demo ↔ Production therefore does **not** load the other set’s user layouts. Within one set, whether a saved layout applies still depends on **layout keys** (location **names**, viewport, radial config — not coordinates alone). Details: [MANUAL_LAYOUT_EDITOR.md — When a saved layout loads](MANUAL_LAYOUT_EDITOR.md#when-a-saved-layout-loads-and-when-it-does-not).

Cluster cache files follow the same suffix rule under `%AppData%\InteractiveWorldMap\clusters\<suffix>.json`.

Default config:

```json
"ManualLayoutEditor": {
  "LayoutStoragePath": "Images&Content/Demo-Content/manual-layouts.json",
  "SetAwareStorage": true
}
```

For a Production seed path (when you ship one), use:

```json
"LayoutStoragePath": "Images&Content/Production-Content/manual-layouts.json"
```

(Relative `LayoutStoragePath` values are treated as the bundled seed hint; the live seed file is taken from the **active** content set’s `manual-layouts.json`.)

### Custom `visual-config.json` after the reorg (D8)

If you have a custom `LayoutStoragePath` in your local `visual-config.json` that still points at the old flat path (`Images&Content/manual-layouts.json`), update it to the new Demo (or Production) path, **or delete the override** so the app picks up the default from `visual-config.default.json`.

## Legacy flat layout

Legacy mode (coordinate source and/or assets directly under `Images&Content/` with no Demo/Production set) remains supported for **developers and tests only**. Production deployments should use the structured layout wholesale.

## Related docs

- [SETUP_GUIDE.md](SETUP_GUIDE.md) — prerequisites and folder setup
- [UPDATING_COORDINATES.md](UPDATING_COORDINATES.md) — Excel / JSON coordinate loading
- [MANUAL_LAYOUT_EDITOR.md](MANUAL_LAYOUT_EDITOR.md) — editing and saving layouts
- [CONTENT_FEATURES.md](CONTENT_FEATURES.md) — location folder content (images, didactic text)
