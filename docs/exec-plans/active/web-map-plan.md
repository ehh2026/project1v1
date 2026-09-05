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
| Map rendering | Base: ship `World Map Extra Large.jpg` (8198×5542) unchanged in Leaflet `imageOverlay`. **Dense-region sharpness** via pre-baked high-res crops cut from the 16397-px master for just the cluster bounding boxes (e.g. NYC), overlaid only when zoomed into that region (Option "regional detail patches"). Fallback: base image alone if crop tooling proves fussy. The 54 MB master itself never ships |
| Markers | Simple drawn pins (CSS/SVG). **Composite-pin rendering is not ported.** |
| Cluster markers | Existing **stamp image + count badge** asset |
| Content pipeline | One-time pre-bake: Excel/`locations.json` + image folders → one web `locations.json` + optimized images |
| Content updates | Not expected. Pipeline exists but cadence is "rerun the script if content ever changes" |
| Desktop app | Untouched. Shares content, no shared code |

## Deferred (only revisit with new requirements)

WCAG audit beyond the basics built into Stage 4 · analytics/consent · PWA/offline install · GeoJSON export · OpenSeadragon tile pyramids · SEO prerendering · Blazor/ASP.NET options · streaming

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
- [x] Write `scripts/audit_unused_assets.py` and run it (2026-09-05): **30.5 MB never-referenced in-repo** (59 files), CSV at `TestResults/unused-assets.csv`. Biggest items: three unused map variants (~20.5 MB) + Extras pin-extraction experiments (~9 MB, already excluded from the public package). The expected ~50+ MB saving on the **web bundle** is real once the web ships an optimized downscaled map instead of the 11.8/54.5 MB desktop sources.
- [ ] Human-confirm the audit candidates; record decisions in this plan (desktop package pruning is a separate decision — only web-bundle exclusion is in scope here)
- [ ] Write `scripts/prepare_web_assets.py` (venv + Pillow): copy `World Map Extra Large.jpg` into `web/images/` **unchanged**; per-location popup images → sensible web copies (keep original JPEG/PNG bytes to stay faithful). Produce `web/data/locations.json` from the content set chosen for the site
- [ ] Same script: compute dense-cluster bounding boxes from `locations.json` (port the proximity grouping rule or reuse its outputs — see `Utilities/LocationClusterer.cs`), cut matching crops from `World Map 1976.jpg`, write `web/images/crops/<region>.jpg` + a `web/data/crops.json` manifest (lon/lat bounds per crop). Must run on the machine holding the real production content, since `Production-Content/` is not committed
- [ ] MVP uses base image + crops overlays; if overlays prove fiddly, ship base-only (documented fallback in Stage 2)
- [ ] Phone-network sanity check once the MVP loads: time the first paint on a mid-range phone over cellular throttling; only if unacceptable, revisit re-encoding quality (still same dimensions) or progressive JPEG — record the measured numbers in this plan

**Exit criteria:** `web/data/` + `web/images/` built from a script, total payload reported, never-referenced report produced and reviewed.

## Stage 2 — Static MVP (~3–5 days)

New top-level `web/` folder (static; not referenced by the WPF build).

- [ ] `web/index.html`: full-viewport Leaflet map, `CRS.Simple`, `L.imageOverlay` with bounds derived from the map's lon/lat extent (keep math consistent with `Utilities/CoordinateMapper.cs` — linear lon→x, lat→y)
- [ ] Load `web/data/locations.json`; place one simple drawn pin marker per location
- [ ] Click → popup styled like a simplified kiosk content window: images (lazy-loaded) + captions + bio text
- [ ] Responsive: works on desktop browser and phone; initial view fitted to the map; pinch zoom on mobile
- [ ] Local test: `py -3 -m http.server` in `web/`, verify in Chrome/Edge/Firefox + phone over LAN

**Exit criteria:** every location clickable, every popup shows its real content, on desktop and a phone.

## Stage 3 — Experience parity pass (~3–5 days)

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
