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
- [ ] Write `scripts/prepare_web_assets.py` (venv + Pillow, `Image.MAX_IMAGE_PIXELS = None` required — the 181 MP master trips Pillow's safety limit):
  1. **Base:** open `World Map 1976.jpg` (16397×11085), downscale to ~4096 px wide (≤ 16.7 MP), save as progressive JPEG quality ~82 → `web/images/map-base.jpg` (expected 3–5 MB)
  2. **Popup images + text sidecars:** copy per-location images **and** `didactic.txt` / `*-caption.txt` sidecars (the captions feed alt text; the audit script treats .txt as sidecar-skips but they are web content) to `web/images/content/...`, original bytes
  3. Emit `web/data/locations.json` for the chosen content set (Excel-first precedence, validated against desktop loader output)
  4. **Alt-text contract (CodeRabbit, 2026-09-05):** in `web/data/locations.json`, every image entry gets `altText` populated at pre-bake time: caption text if a caption exists in `CaptionsByImageFileName`/caption sidecars, else a safe fallback `"<Location name> image <N>"`. No empty `alt` attributes may ship — blocked-popup-with-missing-alt is a launch failure (it breaks the Stage 3 accessibility basics).
  4. **Do not build the cluster-crop step yet** — gate it behind the Stage 2 phone test
  - Must run on the machine holding the real production content (`Production-Content/` is not committed). Note `web/` build output itself is also not committed — it's generated on demand; if the gallery later wants the built site version-controlled, that needs its own repo/pipeline decision.
- [ ] **Stage 2 starts on a new PR** — this PR stays docs+script only.
- [ ] Phone-network sanity check once the MVP loads: time the first paint on a mid-range phone over cellular throttling; only if unacceptable, revisit re-encoding quality (still same dimensions) or progressive JPEG — record the measured numbers in this plan

**Exit criteria:** `web/data/` + `web/images/` built from a script, total payload reported, never-referenced report produced and reviewed.

## Stage 2 — Static MVP (~3–5 days)

New top-level `web/` folder (static; not referenced by the WPF build).

**Coordinate contract (decided — CodeRabbit finding, 2026-09-05):**

- `web/data/locations.json` and `crops.json` carry **raw lat/lon only — never pre-baked pixels**. `CoordinateMapper.LatLongToScreen` is top-down (lat +90 → y=0), while Leaflet `CRS.Simple` y increases upward; mixing pixel spaces here is the easiest way to flip the map. Staying in lat/lon entirely avoids the conversion: markers take `[lat, lon]` directly, and the overlay bounds are geographic
- Overlay bounds: `L.imageOverlay(url, [[-90, -180], [90, 180]])` matches the app's full-world mapping. Note the image aspect (1.48:1) differs from the full-world equirectangular 2:1 — the overlay will stretch slightly in latitude, which exactly mirrors the desktop's own linear mapping; accept the same behavior for parity
- **Fixture before any marker work:** assert the four image corners and two known locations (e.g. New York, London) land at the same lat/lon in both `CoordinateMapper` and the Leaflet map — a tiny `web/test-projection.html` page that prints expected vs actual, checked by eye in one browser run

**Minimal working skeleton** (saves tutorial-hunting; adapted from the coordinate contract):

```html
<style>html, body { height: 100%; margin: 0; } #map { height: 100%; }</style>
<link rel="stylesheet" href="https://unpkg.com/leaflet@1.9.4/dist/leaflet.css">
<script src="https://unpkg.com/leaflet@1.9.4/dist/leaflet.js"></script>
<div id="map"></div>
<script>
  const bounds = [[-90, -180], [90, 180]];          // full-world, matches CoordinateMapper mapping
  const map = L.map('map', { crs: L.CRS.Simple, minZoom: -2, maxZoom: 4 });
  L.imageOverlay('images/map-base.jpg', bounds).addTo(map);
  map.fitBounds(bounds);
  // markers: L.marker([lat, lon]).addTo(map).bindPopup(...)  — lat/lon raw from locations.json
</script>
```

Tasks:

- [ ] `web/index.html`: full-viewport Leaflet map, `CRS.Simple`, `L.imageOverlay` per the coordinate contract above. Base = `web/images/map-base.jpg` (~4096 px progressive)
- [ ] **Projection fixture (do this before marker work):** `web/test-projection.html` prints expected vs actual for the four image corners + two known cities (New York, London), checked once by eye in a browser
- [ ] Load `web/data/locations.json`; place one simple drawn pin marker per location
- [ ] Click → popup styled like a simplified kiosk content window: images (lazy-loaded) + captions + bio text
- [ ] Responsive: works on desktop browser and phone; initial view fitted to the map; pinch zoom on mobile
- [ ] Local test: `py -3 -m http.server` in `web/`, verify in Chrome/Edge/Firefox + phone over LAN
- [ ] **Phone sharpness check:** zoom into the densest cluster (NYC) on a phone. If pins/city labels are unacceptably soft, promote the regional-crop work into Stage 3; if fine, crops stay deferred

**Exit criteria:** every location clickable, every popup shows its real content, on desktop and a phone.

## Stage 3 — Experience parity pass (~3–5 days)

- [ ] **Conditional — regional crops, only if Stage 2's phone sharpness check failed:** extend `prepare_web_assets.py` to compute dense-cluster bounding boxes from `locations.json` and cut full-res crops from the 16397-px master → `web/images/crops/` + `web/data/crops.json`; in the site, add each crop as a second `L.imageOverlay` toggled on `zoomend` (zoom ≥ threshold and view intersects bounds; keep overlay count small, single-digit regions)
- [ ] Clustering: group nearby pins (leaflet.markercluster or the existing `LocationClusterer` logic ported); cluster marker = **stamp image + count badge**
- [ ] Deep links: `#location=<id>` opens that location's popup (shareable links)
- [ ] Accessibility basics: keyboard tab-through pins (focus ring, Enter opens), `aria-label` = location name, `alt` text on images from `CaptionsByImageFileName`, check pin/badge contrast against the map
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
