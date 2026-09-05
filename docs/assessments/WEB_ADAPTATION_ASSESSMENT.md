# Web Adaptation Assessment

**Date:** September 4, 2026 (revised September 5, 2026)
**Purpose:** Assess feasibility and effort required to adapt the Interactive World Map WPF desktop application so the gallery can offer it on their website.

## Executive Summary

**Difficulty level depends entirely on scope.** This document previously treated "web version" as "full feature parity with the desktop app," which is a rewrite. For the actual ask — **a gallery website visitors can browse** — a scoped-down, read-only web version is significantly cheaper than a rewrite.

- **Read-only web map (browse + click pins + read popups):** LOW-MEDIUM effort. Weeks, not months. The map is a static raster image with an equirectangular projection and JSON/PNG content — this is a textbook case for standard web mapping libraries (Leaflet with an image overlay, OpenSeadragon). **No .NET required at all for the public site.**
- **Full feature parity web app** (manual layout editor, dev tools, watchdog/unattended kiosk behavior, visual-config hot-tuning): HIGH effort. Essentially a rewrite of `Views/`, `MainWindow`, and file-system-dependent Services. Months.
- **Zero-code interim option:** remote/browser streaming of the existing Windows app.

**Key insight the original version of this document missed:** the single most valuable and most reusable asset is not the C# code — it is the **content**: ~193 MB of curated PNG/JPG imagery plus `locations.json` and the Excel-fed data model. All of it can be served to a browser as-is. A static web map needs a new presentation layer, not a new data pipeline.

## Current Architecture Analysis

### Technology Stack
- **Framework:** WPF on .NET 6.0-windows (Windows-specific)
- **Language:** C# 10
- **UI Technology:** WPF UserControls, storyboard animations, hardware-accelerated rendering
- **Architecture:** Layered (Models ← Utilities/Services ← Views, orchestrated by MainWindow/App)
- **Dependencies:** Newtonsoft.Json

### Key Technical Constraints
1. **WPF is Windows-only** — cannot run in a browser under any packaging.
2. **Local file system assumed everywhere:** `Images&Content/` loading, `visual-config.json` merge, logging to `%APPDATA%`, manual-layout persistence.
3. **Window/full-screen management and input handling** are desktop concepts.
4. **.NET 6 is end-of-life (since Nov 2024).** Any serious web investment should first move shared code to a supported LTS runtime (.NET 8/10), regardless of which web technology is chosen.

### What the app actually is (matters a lot for the web)
- A **single high-resolution world map image** rendered full-screen.
- Markers placed by a **custom equirectangular lon/lat → pixel projection** (`Utilities/CoordinateMapper`).
- Clickable pins → popups over static images/text from `locations.json` + location folders.
- Desktop-only extras layered on top: manual layout editor, dev tools, seed generators, watchdog, visual-config overlays.

This means the "hard GIS problem" (tile pyramids, slippy-map math) does **not** exist here: one image + one affine-ish projection. Standard libraries eat this for breakfast (`L.imageOverlay` with `CRS.Simple` in Leaflet; OpenSeadragon for deep-zoom if the base image is very large).

## Scope Tiers (choose the tier before the technology)

**Confirmed by the owner (Sept 5, 2026):** the web version is the **end-user portion only** — the same experience a gallery visitor gets at the in-person kiosk (browse the map, click pins, view content). Dev tools, visual-config tuning, layout editing, content editing, and unattended-kiosk operations all remain desktop-only and are **explicitly out of scope** for the web. That fixes the target at T1, growing toward T2; T3 is not a goal at all.

| Tier | What it includes | Status |
|------|------------------|--------|
| **T1 — Read-only web map** | Pan/zoom of the map image, pins, click → popup with image/text content | **Target** |
| **T2 — T1 + richer parity** | Clustering, composite pins/multi-image carousels, deep links per location | Likely, as increments on T1 |
| **T3 — Full desktop parity** | Manual layout editor, dev tools, visual-config overlays, unattended kiosk behavior | **Out of scope** — those are operator tools, not visitor experience |

Costing below is therefore for T1 → T2 paths only.

## Adaptation Approaches

### Option A: Static web map, new minimal front end (RECOMMENDED for T1/T2)

Plain HTML/CSS/JS (or TypeScript) + **Leaflet** (image overlay, custom CRS) or **OpenSeadragon** (if the 8K map needs tiled deep-zoom). Content pipeline: a one-time build step converts the existing Excel/`locations.json`/image folders into static web-ready JSON + optimized images, then host on GitHub Pages / Azure Static Web Apps / S3+CDN. **No server code at all.**

**Pros:**
- Smallest possible scope; no backend, no Windows dependency, trivial hosting cost.
- Reuses the *content* and the *projection math* (CoordinateMapper's equirectangular formula is ~20 lines and ports literally).
- Leaflet gives zoom, pan, clustering (`leaflet.markercluster`), and mobile/touch support out of the box — things the WPF app had to hand-build.
- Cheapest to host and maintain for a gallery; embeddable via iframe if they want it inside an existing CMS page.

**Cons:**
- Code reuse is limited to data and math; the UI is new (but the T1 UI is small).
- Optional features from the desktop app (manual layout editor etc.) simply don't come along — which matches the gallery's need.
- If the map image is updated frequently, the pre-bake step needs automation.

**Estimated effort:**
- T1 prototype with real content: **1–2 weeks**
- T1 production-quality (responsive, a11y basics, gallery branding, deep links): **3–6 weeks**
- T2 features (clustering behaviors, carousels, content-set switcher): **+2–6 weeks**

### Option B: Blazor (WebAssembly or Server) (for T2/T3, or if C# reuse is strategic)

Keep C#, port Models/Utilities/Services (minus file-system bits), rewrite Views as Blazor components.

- **Blazor WebAssembly:** runs client-side, but the payload is heavy for a gallery website and the Views layer is still a full rewrite.
- **Blazor Server:** smaller download, but needs a stateful server and is latency-sensitive for pan/zoom.

**Pros:** reuse of Models, CoordinateMapper, clustering, validation logic; one language if the team stays C#; ASP.NET Core can also serve the content API directly from the existing folders.

**Cons:** UI rewrite either way; larger hosting/maintenance footprint than Option A for a mostly-static exhibit; effort barely lower than a JS rewrite for T1.

**Estimated effort:** T2 2–4 months; T3 4–6 months. **Not justified for T1.**

### Option C: ASP.NET Core API + JS front end ("reuse the business logic server-side")

Thin ASP.NET Core backend exposes the existing ContentLoader output over HTTP; front end is Option A's static map calling the API.

**Pros:** real reuse of Models/Utilities/Services; keeps a single source of truth for content ingestion (Excel → API → any client); familiar stack.

**Cons:** needs hosted server + ops; overkill if content changes rarely (pre-baked JSON is simpler and free to host).

**Estimated effort:** 1–3 months depending on how much of Services is surfaced. Good middle path **if content changes often or multiple front ends are planned.**

### Option D: Remote access to the existing app (zero code changes)

Stream the running WPF app to browsers.

- **Apache Guacamole** (RDP → HTML5 browser client) or **Azure Virtual Desktop / Windows 365** or self-hosted RDP gateway.

**Pros:** literally zero code changes; exact current behavior; can be live in days.

**Cons:** per-session server cost and scaling ceiling; latency; poor mobile experience; it's a video feed of a desktop app, not a website — fine as an interim "preview," not as the gallery's permanent web presence. Also: one user's session is everyone's session unless you provision per-user instances.

**Estimated effort:** days–2 weeks of infrastructure work. **Interim only.**

### (Removed) MAUI

Previous version of this doc listed .NET MAUI. It does not run in browsers and therefore **does not address the requirement at all**; dropped from consideration for this goal.

## Code Reuse Analysis (revised)

### Reusable as-is (the real asset)
- **Content:** `Images&Content/` — 193 MB of PNGs/JPGs, `locations.json`, location folders, the Excel source. Directly web-hostable after a size/optimization pass.
- **Projection math:** `Utilities/CoordinateMapper` equirectangular mapping — trivially portable to any language (~20 lines).
- **Data model shape:** `Models/` (Location, composite pins, VisualConfig defaults) — ports to TypeScript interfaces or is consumed as JSON directly.

### Reusable only if staying in .NET (Options B/C)
- Clustering (`LocationClusterer`, `SpatialGrid`), validation, content loading logic (after swapping file I/O), parts of navigation/state.

### Must be rewritten for any web target
- All of `Views/`, `MainWindow*` orchestration, animations (WPF Storyboards → CSS/JS), input handling.
- File-system-dependent services (content paths, logging, layout persistence, visual-config merge) → replaced by HTTP/CDN, server-side config, or simply build-time constants.
- Desktop-only operational features: watchdog, unattended launcher, dev tools, manual layout editor (T3 only).

## Technical Challenges (re-calibrated)

1. **Map image size in browser.** If the source map is very high resolution, naive single-image loading is slow on the web. Mitigations: build a tile pyramid (gdal2tiles / libvips) for **OpenSeadragon deep-zoom**, or pre-render 2–3 resolution tiers for Leaflet. One-time tooling, not ongoing work.
2. **Projection fidelity.** CoordinateMapper's assumption (linear lon→x across the image width) remains valid; just verify the web image isn't re-cropped/resized relative to the coordinates. Keep the authoritative bounds in `locations.json`/config and derive the overlay bounds from it.
3. **Content weight (193 MB).** Fine for CDN, but popups should lazy-load images; consider responsive image sizes. Do not ship raw originals as the first paint.
   - **There is real headroom here:** an initial review suggests a meaningful share of the packaged images are never referenced — likely **50+ MB could be pruned** from any web bundle without affecting functionality. This needs care, not bulk deletion: references come from multiple places (`locations.json`, the Excel source, per-location folders, and composite-pin composition rules), so the safe move is a small audit script (in `scripts/`) that cross-references every asset against all reference sources and emits a "never referenced" report, which a human then confirms before exclusion. Web bundles should ship the deduplicated/audited subset regardless; whether the desktop package also prunes is a separate decision.
4. **Excel ingestion.** Browsers shouldn't parse Excel. Pre-bake to JSON at build time (T1) or keep ingestion server-side (Option C). This also removes ClosedXML/Excel concerns from the web surface entirely.
5. **Maintenance split.** After a successful T1/T2, the desktop app and web map share content but not code: decide whether content edits flow Excel → desktop build → web pre-bake (recommended single pipeline) or diverge.

## Recommendations

1. **Immediate (this month): stand up an Option A T1 prototype** with the real map image and real `locations.json` — a few days of work, and it answers every stakeholder question better than any document. Host the draft on a free static host and show it to the gallery.
2. **If the gallery approves:** finish T1 production (responsive, branding, deep links, lazy-loaded popups), add a `scripts/` pre-bake step so content updates regenerate the web bundle from the same Excel/JSON source. If the base map image proves too heavy, switch Leaflet → OpenSeadragon with tiles — that decision is reversible and cheap.
3. **Keep the desktop app as-is** (it's the kiosk product). Share only the content pipeline.
4. **Revisit Option C only if** content changes frequently, multiple front ends appear, or dynamic features (search index, user submissions, i18n) are requested.
5. **Treat Option D (streaming)** strictly as a stopgap if a web presence is demanded *before* even the T1 prototype can be built.
6. **Regardless of path:** plan the repo's move off EOL .NET 6 → .NET 8/10 LTS on the desktop side as independent hygiene (tracked separately, not a web blocker for Option A).

## Conclusion (revised)

"How difficult is it to put this on our website?" has two very different answers:

- **What the gallery is actually asking for — web visitors getting the same experience as in-person kiosk visitors, nothing more — is a small project.** The data model, imagery, and projection math transfer directly, no backend is required, and standard libraries (Leaflet/OpenSeadragon) cover interactions the desktop app hand-rolled. Estimate: **1–2 weeks to a working prototype, 3–6 weeks to production-quality T1**, plus optional T2 increments. An asset-referencing audit can likely shed another 50+ MB from the shipped bundle.
- **Porting the desktop application itself is a rewrite** (4–6 months) and is not what the gallery needs. Previous framing of this assessment conflated the two.

**Corrected recommendation:** build a scoped, static web map that reuses the content pipeline and projection math; do not attempt to port WPF to the browser.

## Related Documentation

- [ARCHITECTURE.md](../../ARCHITECTURE.md) — layer rules; note the web front end would be a new top-most layer consuming content, not a new consumer of Services.
- [docs/guides/CONTENT_SETS.md](../guides/CONTENT_SETS.md) — Demo vs Production content the web pre-bake would key off.
- [Utilities/CoordinateMapper.cs](../../Utilities/CoordinateMapper.cs) — the equirectangular math to port.
- [docs/TO_DO.md](../TO_DO.md) — backlog item for web version.
