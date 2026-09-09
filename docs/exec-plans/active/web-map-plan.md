---
status: active
owner: agent
started: 2026-09-05
---

# Web Map Plan — Gallery Website Version

**Status:** Active — Stage 1 + Stage 2 MVP built, browser/phone-smoked, and merged in PR #35. Stage 3A is the launch-parity work; Stage 3B is optional enrichment.
**Created:** September 5, 2026
**Assessment:** [docs/assessments/WEB_ADAPTATION_ASSESSMENT.md](../../assessments/WEB_ADAPTATION_ASSESSMENT.md) (read this first for the "why")
**Owner note:** This plan assumes a solo, non-professional developer, one gallery, no ongoing content updates expected. It is deliberately staged so that each stage produces something usable even if later stages never happen.

## Goal

A website version of the map that gives a web visitor the **same experience as a gallery visitor at the kiosk**: browse the world map on desktop or phone, click pins, view the images and text for each location. Read-only. No editing, no dev tools, no config tuning.

## Firm Decisions (do not re-litigate mid-plan)

| Decision | Choice |
|---|---|
| Technology | Plain static site: HTML/CSS/JS + **Leaflet** with `L.imageOverlay` + `CRS.Simple` |
| Backend | None. Static hosting (GitHub Pages, Netlify, or gallery's own host) |
| Map rendering | **Intermediate base first** (~4096 px progressive JPEG — required: iOS Safari refuses >~16.7 MP single images, and the 45 MP desktop base would render blank on iPhones). Stage 3A adds a bounded Leaflet tile pyramid for sharp roaming at mid/deep zoom. Regional master crops remain a lazy, pin-area optimization, but must never be eagerly downloaded or exceed the same per-image device budget |
| Markers | Simple drawn pins (CSS/SVG). **Composite-pin rendering is not ported.** |
| Cluster markers | Existing **stamp image + count badge** asset |
| Interaction model | **Free pan/zoom anywhere** (scroll/pinch) — unlike the kiosk's cluster-click-only zoom. Include a visible Reset view control; zoom-out room is calculated from the current viewport, not a fixed magic `minZoom` |
| Content pipeline | One-time pre-bake: Excel/`locations.json` + image folders → one web `locations.json` + optimized images. Each location gets a stable web ID that survives harmless source-row reordering |
| Pre-bake authority | Mirror desktop precedence exactly (`ContentLoader.LoadLocationsAsync`): **Excel first, `locations.json` as fallback**. Web `locations.json` schema documented in [web/README.md](../../../web/README.md) (done, committed) and validated against the desktop loader's output before Stage 2 runs |
| Content updates | Not expected. Pipeline exists but cadence is "rerun the script if content ever changes" |
| Desktop app | Untouched. Shares content, no shared code |

**A11y release criterion (single source of truth):** Stage 3 ships the *basics* (keyboard navigation, focus rings, ARIA labels, `alt` from captions, contrast check) as **launch scope**. A formal WCAG 2.1 AA audit with assistive technology is **optional post-launch**, not a gate — it is plain "later phases if ever" work, not built into any stage. The assessment and CHANGELOG say the same thing — keep them in sync if this decision changes.

**Effort convention:** all stage durations below are **solo-developer engineering days**; they exclude gallery feedback cycles, content rights review, and any waiting on third parties (aligned with the assessment's 2–3 wk prototype / 4–8 wk production framing, which *does* include those).

## Deferred (only revisit with new requirements)

WCAG 2.1 AA formal audit (optional post-launch; not part of any stage) · analytics/consent · PWA/offline install · GeoJSON export · OpenSeadragon tile pyramids · SEO prerendering · Blazor/ASP.NET options · streaming

## Stage 0 — Decisions & asset reconnaissance (~½ day)

Ask the gallery four questions (full list in the assessment's "Open Questions"):

- [ ] Which platform runs the gallery website, and can it embed an iframe?
- [ ] Are all images cleared for **web** publication? Any privacy-sensitive letters/documents?
- [ ] Any brand fonts/colors to apply?
- [ ] Where should it live: page on their site, or a standalone link (e.g. Netlify URL) they link to?

Meanwhile, gather facts locally:

- [x] Record exact pixel dimensions of every candidate base map image in `Images&Content/Assets/` (done 2026-09-05 — inventory in Stage 1 below; desktop uses 8198×5542 base + 16397×11085 full-res)
- [x] Confirm which `locations.json`/content set is the real production content (done: `Production-Content/` is empty in-repo — the gallery's real content is supplied locally and never committed; `Demo-Content/` is the repo-bundled demo set)

**Exit criteria:** gallery answers written down; map-image choice made with measured dimensions.

## Stage 1 — Asset audit & image prep (~1–2 days)

- [x] Record map-image inventory (done 2026-09-05):
  - Desktop base map: `Assets/World Map Extra Large.jpg` — 8198×5542, 11.8 MB (`ContentFileNames.WorldMapFileName`)
  - Desktop full-res zoom source: `Assets/World Map 1976.jpg` — 16397×11085, 54.5 MB, 181 MP (`ContentFileNames.FullResolutionWorldMapFileName`; triggers Pillow's decompression-bomb guard at default settings)
  - Unreferenced map variants: `World Map Extra Large copy.jpg` (14.6 MB), `World Map Large.jpg` (2.9 MB), `Large_World_Map_bright.jpg` (3.0 MB)
  - Production content is **not in the repo** (`Production-Content/` is a `.gitkeep` placeholder); demo set lives in `Demo-Content/`
  - Note: map aspect is 1.48:1, not classic 2:1 equirectangular — the overlay is placed in the map image's own pixel-normalized space (see the coordinate contract below), never assumed geographic `-90..90`
- [x] Write `scripts/audit_unused_assets.py` and run it (2026-09-05): **31.8 MB never-referenced in-repo** (70 files), CSV at `TestResults/unused-assets.csv`. Updated to path-aware matching after CodeRabbit review: a referenced path must match the relative path (or a unique basename), so same-named files in `Extras/` can no longer hide behind `Assets/` matches. Biggest items: three unused map variants (~20.5 MB) + Extras pin-extraction experiments (~10 MB, already excluded from the public package). The expected ~50+ MB saving on the **web bundle** is real once the web ships an optimized downscaled map instead of the 11.8/54.5 MB desktop sources.
  - **Audit contract (so results are trustworthy):** reference sources = all `*.json` and `*.xlsx` under `Images&Content/` + all repo code/config (`*.cs`, `*.xaml`, `*.json`). Composite-pin composition rules are covered because `Assets/Pins_v2/` is implicitly referenced (code-driven patterns) and shared pin asset names appear in config/code. Location folders are implicitly referenced (directory enumeration at runtime). Anything outside the audit's authority must be excluded by hand before deletion.
- [ ] Human-confirm the audit candidates; record decisions in this plan (desktop package pruning is a separate decision — only web-bundle exclusion is in scope here)
- [x] Write `scripts/prepare_web_assets.py` (done 2026-09-06 on branch `web-map-mvp`, run against Demo-Content: 38 locations, base map 2.0 MB, pre-crop total payload 3.38 MB). Later demo crops added 4.2 MB; before production deployment, regenerate and record the **current** total, initial-transfer total, crop count, largest crop dimensions, and largest crop bytes. **Note:** source data turned out to be **pixel coordinates** (see corrected coordinate contract in Stage 2) — the script normalizes to `[0,1]` fractions. Remaining production work: rerun on the machine with real content (`--content-set ...\Production-Content`).
- [ ] Phone-network sanity check before deployment: on a representative mid-range phone under cellular throttling, record first paint, time to usable map, and initial bytes/requests. Crops and tiles not in or near the viewport must not be part of the initial transfer; only if the measured result is unacceptable revisit dimensions/encoding — record the numbers in this plan.

**Exit criteria:** `web/data/` + `web/images/` built from a script, total payload reported, never-referenced report produced and reviewed.

## Stage 2 — Static MVP (~3–5 days)

New top-level `web/` folder (static; not referenced by the WPF build).

**Coordinate contract (decided — CodeRabbit finding, corrected 2026-09-06 after reading the real data):** the source data is **pixel coordinates, not lat/lon** — Excel columns E/F are on the 8198×5542 base-image frame (what the app uses), columns B/C and `locations.json` `PixelX/PixelY` are on the 16397×11085 master frame. There is no geographic data anywhere.

- `web/data/locations.json` carries **normalized `[0,1]` fractions (`nx`, `ny`), origin top-left** — never raw pixels, never lat/lon. The pre-bake normalizes from whichever frame the row used
- The site uses `CRS.Simple` with `L.imageOverlay(image, [[0, 0], [H, W]])` where W×H is the *base web image* size (from the pre-bake manifest); markers map as `lng = nx*W`, `lat = (1-ny)*H` (CRS.Simple y increases upward, the image convention is y-down — hence the `1-ny`)
- **Fixture before marker work ship:** `web/test-projection.html` machine-checks corner round-trips and plots every location over the base image for one eyeball pass in a browser (HTTP smoke of all bundle files done 2026-09-06; **the eyeball pass is still a pending human checkbox below**)

**Minimal working skeleton** (saves tutorial-hunting; adapted from the coordinate contract):

```html
<style>html, body { height: 100%; margin: 0; } #map { height: 100%; }</style>
<!-- Leaflet self-hosted in web/vendor/ (no CDN dependency on the gallery host) -->
<link rel="stylesheet" href="vendor/leaflet/leaflet.css">
<script src="vendor/leaflet/leaflet.js"></script>
<div id="map"></div>
<script>
  const data = await (await fetch('data/locations.json')).json();
  const { width: W, height: H, image } = data.map;
  const bounds = [[0, 0], [H, W]];                 // pixel space of the base image
  const map = L.map('map', { crs: L.CRS.Simple, minZoom: -2, maxZoom: 4 });
  L.imageOverlay(image, bounds).addTo(map);
  map.fitBounds(bounds);
  // markers: L.marker([(1 - loc.ny) * H, loc.nx * W])  — normalized fractions in, Leaflet lat/lng out
</script>
```

(Self-hosting `leaflet.css`/`leaflet.js` in `web/vendor/` is the established approach — no CDN trust blast radius, works if the gallery site ever goes offline-ish. If CDN becomes preferable later, use the official SRI hashes + `crossorigin`.)

Tasks:

- [x] `web/index.html`: full-viewport Leaflet map, `CRS.Simple`, `L.imageOverlay` per the coordinate contract above (done 2026-09-06, commit on web-map-mvp; base = 4096×2769 progressive, 2.0 MB actual)
- [x] **Projection fixture:** `web/test-projection.html` machine-checks corner round-trips; every location plotted for eyeballing (written 2026-09-06)
- [x] Load `web/data/locations.json`; one CSS-drawn pin per location (done)
- [x] Click → popup styled like a simplified kiosk content window: images (lazy-loaded) + pre-baked altText/captions + bio text (done)
- [x] Responsive: `viewport` meta, initial `fitBounds`, pinch zoom via Leaflet, and a current popup CSS cap of `min(680px, 94vw)` (desktop/phone smoke completed; Stage 3A still makes the Leaflet option, height, scrolling, and post-image-load layout responsive)
- [x] Local test: HTTP smoke over `py -3 -m http.server` — all bundle files 200 (2026-09-06)
- [x] **Human browser + phone pass (done 2026-09-06, owner):** desktop OK; phone (iPhone via Parallels-bridged LAN) — renders ✓ (16.7 MP limit cleared), pinch zoom ✓, pins open ✓, popups readable-but-narrow, map "blurry but not terrible" when zoomed, rotation fine. Actions taken: `maxZoom` 4→6 for more zoom headroom, popups widened to min(680px,94vw) with a mobile font bump; crop overlays are doing the heavy lifting until the Stage 3 tile pyramid lands (planned).
- [x] **Phone sharpness check (done 2026-09-06):** owner zoomed on iPhone — "blurry but not terrible". Crops confirmed working; whole-map sharpness awaits the planned Stage 3 tile pyramid.
- [x] **Regional crops (triggered 2026-09-06 — desktop zoom was too soft):** `prepare_web_assets.py` unions nearby locations (radius 500 master px, **min 1 pin** — owner decision, people zoom on single pins) and cuts master crops (`images/crops/crop_NN.jpg` + bounds). Demo: 7 crops / 4.2 MB. Current overlays are opacity-hidden rather than network-lazy; Stage 3A replaces that behavior before production testing.
- [x] **Intermediate whole-map layer (Stage 3A):** **implemented** — the site keeps the 4096 px base for the initial overview and adds a deterministic Leaflet tile pyramid (`{z}/{x}/{y}.jpg`, 256 px tiles, `zoomOffset: baseZoom(4)`, `noWrap`, `bounds`-clipped). Level 5 = 8192 px whole-map level always; the ~16384 px native-master level is opt-in via `--tiles-native`. Tile layout follows the Leaflet `CRS.Simple` projected grid (URL `y` negative above the map top; level 5 rows `-22..-1`, level 6 `-44..-1`). Sample-tile-vs-master alignment is machine-checked (normalized MSE ≈ 0.0016, i.e. JPEG noise — no seam drift). **Pending (human):** eyeball corner/location alignment, no visible seams, and tile fetching on the target iPhone.
- [x] **Follow-up polish:** keyboard Tab now centers the focused marker in view (Leaflet `autoPanOnFocus`, verified default true); teardrop CSS pins stay per the model decision.

**Exit criteria:** every location clickable, every popup shows its real content, on desktop and a phone.

## Stage 3A — Launch-parity pass (~5–8 days)

- [x] **Lazy, device-safe regional crops:** **implemented** — crops are created only in/near the viewport at the zoom threshold and removed when irrelevant (overlay add/remove, no opacity-zero trick). The generator enforces the per-crop decoded-pixel budget (`CROP_MAX_PIXELS`, 16 MP) by deterministically splitting oversized transitive groups; the per-crop byte budget (`CROP_MAX_BYTES`, 2.5 MB) is enforced by tightening the encode then downscaling a solo pin, and the 25 MB total-crop budget is a **warning-only** threshold (exceeding it recommends the tile stage). Demo run records 7 crops / 6.01 MB, largest 1.23 MB. Regression-tested.
- [x] **Whole-map tile pyramid:** **implemented** — see the Stage 2 tile bullet above. Demo: level 5 (8192 px) = 704 tiles / 8.13 MB; opt-in native level 6 sample recorded at 2816 tiles / 46.17 MB. **Pending (human):** venue phone seam/fetch check.
- [x] **Stable deep links:** **implemented** — manifest ids are name-derived slugs (`kevin`, `dr-henry-rosin`) that survive source-row reordering; duplicate names keep the bare slug on the first content-ordered occurrence and later duplicates plus collisions with an already-taken id (e.g. a real `kevin-1` slug) get a deterministic incremented suffix until free, so ids are always unique. `#location=<id>` pans to, opens and focuses the marker, invalid ids (including malformed percent-encoding) fail harmlessly, and opening a popup mirrors the id into the URL without adding history. Reorder+collision tests present. **Pending (human):** browser direct-load/back-forward pass.
- [x] **Accessibility basics:** **implemented** — map is a labelled `region` with visually-hidden instructions; pins are buttons with aria-labels, visible focus rings, Enter/Space activation, and pan-into-view on focus (Leaflet `autoPanOnFocus`); popup focus moves into the scroll region predictably and returns to the originating marker on Escape/close; pre-baked `altText` used directly; pin fill raised to `#e04a5e` (4.4:1 vs background, white ring 17.4:1) and focus ring 12.1:1, recorded in web/README. **Pending (human):** screen-reader/keyboard focus-return pass.
- [x] **Gallery resilience:** **implemented (engineering)** — loading overlay and data-load failure state (role=alert, Reset view hidden); missing or failed popup images show a caption-preserving placeholder (both the pre-baked `missing` flag and runtime `onerror`); lazy image loads call `popup.update()` so layout/auto-pan track final size. **Pending (human):** Stage 0 brand answers and a live failed-image check.
- [x] **Portrait-phone popup usability:** **implemented (engineering)** — `max-height: calc(100dvh - 140px)` internal scroll container (overscroll-contained), Leaflet `maxWidth` recomputed from the viewport and an open popup repositions on resize/rotation. **Pending (human):** portrait/landscape gesture pass with a long bio + several images.
- [x] **Viewport-derived zoom-out and reset:** **implemented** — min zoom = `floor(getBoundsZoom(bounds)) - 1` recomputed on layout/resize (floor −4), plus a visible Reset view control that closes popups and returns to the full map. No fixed magic value.
- [x] **Regression gates (automated half):** pre-bake tests cover stable/unique slug ids (reorder-invariance + duplicate suffixes), normalized bounds, crop pixel/byte budgets and splitting, deterministic tile manifest/geometry, and a sample tile-vs-master MSE alignment check; a stdlib bundle-coherence gate (`scripts/verify_web_bundle.py`) validates generated ids/coords/files/tile grid/payload after every rebuild. **Pending (human):** browser checks for projection, deep-link direct-load/back-forward, tile seams, lazy-crop network requests, failed-image state, keyboard/focus return, and portrait popup scroll.

**Exit criteria:** a gallery visitor and a phone user get shapes-and-content parity with the kiosk; a shareable link opens a specific location; initial mobile transfer excludes distant crops/tiles; and the Stage 3A keyboard, focus, popup, error, and tile checks pass.

## Stage 3B — Optional enrichment (post-launch or only if the density/content review justifies it)

- [ ] **Clustering:** first validate that production pin density makes clustering more legible than individual pins. If it does, self-host the chosen implementation and use the existing stamp image + count badge; cluster activation must zoom/reveal individual pins, retain keyboard support and an accessible count/name, and still allow a deep link to reveal/open its target marker.
- [ ] **Clustering test override:** use a documented query parameter such as `?clusters=0` / `?clusters=1` for testing rather than a customer-facing toggle or a source-only constant. The default is the selected gallery behavior.
- [ ] **Image lightbox with zoom:** only add this if standard popup images prove insufficient. Generate a separate, lazy-on-tap fullscreen derivative with an explicit resolution/byte budget; do not pretend the ≤1600 px popup derivative supports meaningful zoom. Use a self-hosted/lightweight implementation with a labelled modal, caption, close control, Escape, focus trap/return, and touch handling isolated from Leaflet. Test screen-reader and phone pinch/pan/close behavior.

## Stage 4 — Deploy & handoff (~1–2 days)

- [ ] Deploy `web/` to chosen host (gallery CMS page via iframe, or Netlify/GitHub Pages URL)
- [ ] Verify embed in the gallery's actual website page; fix width/height quirks and confirm relative asset paths, caching, tile/image requests, deep links, and mobile viewport behavior on the real host
- [ ] Write `web/README.md`: what it is, how to rebuild (`scripts/prepare_web_assets.py` + redeploy) *if* content ever changes — aimed at a non-developer
- [ ] Archive this plan; update TO_DO.md and CHANGELOG.md

**Exit criteria:** gallery confirms the public link works from their website on desktop and mobile.

## Optional Later Phases (park unless requested)

- A11y audit (NVDA/VoiceOver session, WCAG 2.1 AA fixes)
- Analytics events + consent banner
- PWA / offline caching
- Full-map OpenSeadragon deep-zoom tiling — superseded by the planned Leaflet intermediate/high-resolution pyramid in Stage 3A unless OpenSeadragon's deep-zoom UI is specifically wanted
- GeoJSON export from the pre-bake for future map platforms

## Risks & Notes

- **Content rights (Stage 0)** is the only true blocker found so far; everything else is engineering comfort.
- If the base map is >5–6 MB even optimized, prefer tiles over blurring/prerender tricks. A visible-but-zero-opacity crop is still a network request; do not include distant crops in initial mobile transfer.
- Treat the iPhone ~16.7 MP ceiling as a per-decoded-image crop limit as well as a whole-map limit. Transitive proximity groups can be much larger than their individual pins.
- Stage 3A is the launch-critical work. Clustering and image zoom are deliberately Stage 3B so a tile system, deep links, and accessible mobile popups are not delayed by optional interaction complexity.
- The pre-bake scripts are Windows-friendly (`py -3`) and use only Pillow + stdlib, matching repo tooling conventions ([scripts/README.md](../../../scripts/README.md)).
- Do not touch `Models/`, `Services/` or the WPF app; the desktop build and tests must stay green (`.\scripts\verify.ps1`) after any repo change in this plan.
