---
status: active
owner: agent
started: 2026-09-05
---

# Web Map Plan — Gallery Website Version

**Status:** Active — next up
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
| Map rendering | **Intermediate base first** (~4096 px progressive JPEG — required: iOS Safari refuses >~16.7 MP single images, and the 45 MP desktop base would render blank on iPhones). Free overzoom beyond native resolution is accepted at launch. **Regional high-res crops** from the 16397-px master are a conditional Stage 3 add: only if phone testing shows dense clusters (NYC etc.) look too soft when zoomed. Sparse regions staying soft at deep zoom is an accepted trade-off |
| Markers | Simple drawn pins (CSS/SVG). **Composite-pin rendering is not ported.** |
| Cluster markers | Existing **stamp image + count badge** asset |
| Interaction model | **Free pan/zoom anywhere** (scroll/pinch) — unlike the kiosk's cluster-click-only zoom. Web visitors expect standard map behavior; the cluster crops keep dense regions sharp regardless of zoom path |
| Content pipeline | One-time pre-bake: Excel/`locations.json` + image folders → one web `locations.json` + optimized images |
| Pre-bake authority | Mirror desktop precedence exactly (`ContentLoader.LoadLocationsAsync`): **Excel first, `locations.json` as fallback**. Web `locations.json` schema documented in `web/data/README` (produced Stage 1) and validated against the desktop loader's output before Stage 2 runs |
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
  - Note: map aspect is 1.48:1, not classic 2:1 equirectangular — Leaflet `imageOverlay` bounds must come from the app's geographic bounds, not assumed `-90..90`
- [x] Write `scripts/audit_unused_assets.py` and run it (2026-09-05): **31.8 MB never-referenced in-repo** (70 files), CSV at `TestResults/unused-assets.csv`. Updated to path-aware matching after CodeRabbit review: a referenced path must match the relative path (or a unique basename), so same-named files in `Extras/` can no longer hide behind `Assets/` matches. Biggest items: three unused map variants (~20.5 MB) + Extras pin-extraction experiments (~10 MB, already excluded from the public package). The expected ~50+ MB saving on the **web bundle** is real once the web ships an optimized downscaled map instead of the 11.8/54.5 MB desktop sources.
  - **Audit contract (so results are trustworthy):** reference sources = all `*.json` and `*.xlsx` under `Images&Content/` + all repo code/config (`*.cs`, `*.xaml`, `*.json`). Composite-pin composition rules are covered because `Assets/Pins_v2/` is implicitly referenced (code-driven patterns) and shared pin asset names appear in config/code. Location folders are implicitly referenced (directory enumeration at runtime). Anything outside the audit's authority must be excluded by hand before deletion.
- [ ] Human-confirm the audit candidates; record decisions in this plan (desktop package pruning is a separate decision — only web-bundle exclusion is in scope here)
- [x] Write `scripts/prepare_web_assets.py` (done 2026-09-06 on branch `web-map-mvp`, run against Demo-Content: 38 locations, base map 2.0 MB, total payload 3.38 MB). **Note:** source data turned out to be **pixel coordinates** (see corrected coordinate contract in Stage 2) — the script normalizes to `[0,1]` fractions. Remaining production work: rerun on the machine with real content (`--content-set ...\Production-Content`).
- [ ] Phone-network sanity check once the MVP loads: time the first paint on a mid-range phone over cellular throttling; only if unacceptable, revisit re-encoding quality (still same dimensions) or progressive JPEG — record the measured numbers in this plan
- [ ] Phone-network sanity check once the MVP loads: time the first paint on a mid-range phone over cellular throttling; only if unacceptable, revisit re-encoding quality (still same dimensions) or progressive JPEG — record the measured numbers in this plan

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
- [x] Responsive: `viewport` meta, popup capped at min(360px, 82vw), initial `fitBounds`, pinch zoom via Leaflet (done; needs human browser confirmation)
- [x] Local test: HTTP smoke over `py -3 -m http.server` — all bundle files 200 (2026-09-06)
- [ ] **Human browser pass:** open `web/index.html` and `web/test-projection.html` locally; confirm pins sit where the desktop app puts them (NYC cluster on the map), popups show images/captions, and then repeat on a phone over LAN
- [ ] **Phone sharpness check:** zoom into the densest cluster (NYC) on a phone. If pins/city labels are unacceptably soft, promote the regional-crop work into Stage 3; if fine, crops stay deferred

**Exit criteria:** every location clickable, every popup shows its real content, on desktop and a phone.

## Stage 3 — Experience parity pass (~3–5 days)

- [ ] **Conditional — regional crops, only if Stage 2's phone sharpness check failed:** extend `prepare_web_assets.py` to compute dense-cluster bounding boxes from `locations.json` and cut full-res crops from the 16397-px master → `web/images/crops/` + `web/data/crops.json`; in the site, add each crop as a second `L.imageOverlay` toggled on `zoomend` (zoom ≥ threshold and view intersects bounds; keep overlay count small, single-digit regions)
- [ ] Clustering: group nearby pins (leaflet.markercluster or the existing `LocationClusterer` logic ported); cluster marker = **stamp image + count badge**
- [ ] Deep links: `#location=<id>` opens that location's popup (shareable links)
- [ ] Accessibility basics: keyboard tab-through pins (focus ring, Enter opens), `aria-label` = location name, `alt` from the pre-baked `altText` field (captions are pre-bake inputs only — no renderer-time lookup), check pin/badge contrast against the map
- [ ] Gallery polish: brand fonts/colors per Stage 0 answers; loading state; error state if a popup image is missing
- [ ] Portrait-phone pass: initial zoom/center sane, popup fits small screens, no gesture conflicts

**Exit criteria:** a gallery visitor and a phone user both get shapes-and-content parity with the kiosk; a shareable link opens a specific location.

## Stage 4 — Deploy & handoff (~1–2 days)

- [ ] Deploy `web/` to chosen host (gallery CMS page via iframe, or Netlify/GitHub Pages URL)
- [ ] Verify embed in the gallery's actual website page; fix width/height quirks
- [ ] Write `web/README.md`: what it is, how to rebuild (`scripts/prepare_web_assets.py` + redeploy) *if* content ever changes — aimed at a non-developer
- [ ] Archive this plan; update TO_DO.md and CHANGELOG.md

**Exit criteria:** gallery confirms the public link works from their website on desktop and mobile.

## Optional Later Phases (park unless requested)

- A11y audit (NVDA/VoiceOver session, WCAG 2.1 AA fixes)
- Analytics events + consent banner
- PWA / offline caching
- Full-map OpenSeadragon tiling of the entire 16397-px master — unnecessary once regional crops cover the dense areas; revisit only if web users routinely zoom into non-cluster regions and complain
- GeoJSON export from the pre-bake for future map platforms

## Risks & Notes

- **Content rights (Stage 0)** is the only true blocker found so far; everything else is engineering comfort.
- If the base map is >5–6 MB even optimized, prefer tiles over blurring/prerender tricks.
- The pre-bake scripts are Windows-friendly (`py -3`) and use only Pillow + stdlib, matching repo tooling conventions ([scripts/README.md](../../../scripts/README.md)).
- Do not touch `Models/`, `Services/` or the WPF app; the desktop build and tests must stay green (`.\scripts\verify.ps1`) after any repo change in this plan.
