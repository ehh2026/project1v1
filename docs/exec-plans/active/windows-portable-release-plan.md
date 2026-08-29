---
status: active
owner: agent
started: 2026-08-27
requirements_ref: windows-portable-release
---

# Windows Portable Release Plan

**Goal:** Publish a reproducible Windows download through GitHub Releases. Users extract one `win-x64` zip and run `InteractiveWorldMap.exe` without installing a .NET SDK or Desktop Runtime. The portable folder accepts a user-supplied `Images&Content\Production-Content` data set and provides release-safe configuration tooling.

**Architecture:** Ordinary CI stays read-only. A dedicated Windows publish workflow calls a checked-in publish profile, then a focused staging script creates the public portable layout and a single validated zip. The app already resolves configuration and content from `AppDomain.CurrentDomain.BaseDirectory`, so no application path-resolution changes are required.

**Tech stack:** .NET 6 WPF now, PowerShell 5.1+, GitHub Actions `windows-latest`, GitHub Releases, Python stdlib archive-layout validation, existing `scripts/verify.ps1`.

## Status (2026-08-27)

Implementation and automated verification are complete. `scripts/verify.ps1` passed, and
`scripts/verify_release_package.py` passed against a locally built
`InteractiveWorldMap-win-x64-0.0.0-local.zip`.

Remaining before this plan can be archived:

- **0.2 / 0.3** — content-owner sign-off on excluding `Production-Content`, and restating the
  first-release choices in the implementation PR.
- **2.4** — extracted-package manual smoke is only partly recorded: first-launch config seeding was
  confirmed, but the Production fixture, `.disabled` Demo fallback, and no-runtime-installed checks
  are still outstanding.
- **3.4** — a manual `workflow_dispatch` was exercised on 2026-08-29 and exposed a nested-zip handoff: GitHub wrapped the uploaded package ZIP in its own artifact ZIP. The workflow now uploads the validated package folder for manual runs; rerun it to confirm one extraction exposes the app and `Tools` helpers, then complete the remaining Phase 2 smoke.
- **4.4** — archive this plan and shorten the `TO_DO.md` bullet once the above are done.

The release job's `gh release create` step passes `GH_REPO` because that job intentionally has no
checkout, so `gh` has no git remote to infer the repository from. That path is unexercised until the
first real `v*` tag.

## Decisions

| Decision | First-release choice | Follow-up |
|---|---|---|
| Target | `win-x64` only | Add `win-arm64` only after demand and a dedicated smoke test. |
| Runtime | Self-contained | Bundles the runtime; recipients install neither SDK nor runtime. |
| Delivery | Zip of a portable folder | Content still needs sibling files even with single-file publish. Extract to a writable folder, not `C:\Program Files`. |
| Public content | `Assets` plus `Demo-Content`; no repository `Production-Content` | Avoid accidental deployment-data publication. Users add/replace Production content. |
| Content activation | Existing Production-over-Demo resolver | Valid Production means `locations.json` or `Coordinates for map.xlsx` is present. |
| Runtime config | Ship `visual-config.default.json`; seed `visual-config.json` at first launch | The initial developer-tools setting is chosen for the target release; the package helper always supports an explicit on/off/toggle change. |
| Config helper | Ship a release-specific helper | Existing helper searches repo `bin\`; package helper targets its sibling exe. |
| Trigger | `v*` tag releases; manual dispatch creates an artifact only | Tags are intentional versioned releases. |
| Signing/installer/updater | Deferred | Unsigned zip is acceptable for internal/demo use only. |
| Framework target | Keep `net6.0-windows` in this slice | Plan a .NET 8 migration before broad public use. |

## Portable Folder Contract

The archive contains exactly one root directory:

```text
InteractiveWorldMap-win-x64-<version>/
  InteractiveWorldMap.exe
  visual-config.default.json
  Images&Content/
    Assets/                         # app-owned map and pin assets
    Demo-Content/                   # shipped fallback/sample data
    Production-Content/             # absent initially; optional user-owned data
  Tools/                            # copied from repo release-tools/
    Configure-InteractiveWorldMap.bat
    Configure-InteractiveWorldMap.ps1
    Run-Unattended.bat              # optional restart-on-exit helper
  README.md
```

`visual-config.json` is intentionally omitted. First launch creates it next to the EXE from the default; it holds user-local tuning and survives a normal app update/extract unless the user replaces it.

Users may add or replace the entire `Images&Content\Production-Content` directory. It must include a coordinate source and location folders that follow [CONTENT_SETS.md](../../guides/CONTENT_SETS.md) and [CONTENT_FEATURES.md](../../guides/CONTENT_FEATURES.md). The application automatically prefers a valid Production set to Demo. `Assets` is not part of the user data set and must be retained.

The package `README.md` is copied from `docs/release/PORTABLE_WINDOWS_RELEASE.md`. It states: extract to a writable location; preserve `Assets`; restart after content/config changes; rename `Production-Content` to `Production-Content.disabled` to return to Demo; and expect Windows SmartScreen warnings before signing is introduced.

## Current-State Evidence

- `InteractiveWorldMap.csproj` is a `net6.0-windows` WPF WinExe and currently copies all `Images&Content` plus `visual-config.default.json` into build output.
- `ContentLoader` roots content at `AppDomain.CurrentDomain.BaseDirectory\Images&Content`; `ContentSetResolver` picks valid Production content before Demo content.
- `MainWindow` reads `visual-config.default.json` and seeds/overlays `visual-config.json` beside the EXE.
- `.github/workflows/ci.yml` builds on Windows but triggers only for main/master push and pull request, has read-only permissions, and publishes no artifact.
- `scripts/toggle-dev-tools.ps1` only searches repository `bin\` without `-PublishDir`; copying its current source layout to a package would not discover a sibling EXE by default.

## Scope

**In scope:** repeatable local publishing; safe public-content staging; package-local configuration tooling; tag/manual GitHub Actions publish workflow; archive validation; documentation and bookkeeping.

**Out of scope:** changing app path resolution; moving mutable data to AppData; installer, auto-update, signing, multi-architecture release, .NET 8 migration, or actual tag/release creation by an agent.

## Modularity and File Size Impact

No C# production logic changes are planned. Keep responsibilities separated:

| Boundary | Responsibility | Guardrail |
|---|---|---|
| `Properties/PublishProfiles/` | MSBuild publish flags | No staging, deletion, or GitHub logic in the project file. |
| `scripts/package_windows_release.ps1` | Staging and zipping | Takes explicit input/output paths. It may remove only resolved paths inside a new staging output, never checkout content. |
| `scripts/verify_release_package.py` | Deterministic package validation | Stdlib-only, validates folder/zip structure without launching WPF. |
| `release-tools/` | End-user configuration helper source | Derives the app root from its own relative package position, never from repo `bin\`. |
| `publish-release.yml` | CI orchestration | Calls profile/scripts; no duplicated staging logic inline. |

No new file should approach the 800-line cap. Automated checks protect the package contract rather than adding application-layer dependencies.

## Files and Ownership

| File | Change |
|---|---|
| `Properties/PublishProfiles/WindowsPortable.pubxml` | Create repeatable self-contained, single-file publish profile with an explicit `RuntimeIdentifier` of `win-x64`. |
| `scripts/package_windows_release.ps1` | Create safe staging and versioned zip script. |
| `scripts/verify_release_package.py` | Create folder/zip contract checker. |
| `release-tools/Configure-InteractiveWorldMap.ps1` | Create app-directory config inspection/reset/developer-tool toggle helper. |
| `release-tools/Configure-InteractiveWorldMap.bat` | Create Explorer/cmd wrapper for package helper. |
| `release-tools/Run-Unattended.bat` | Start the sibling map EXE and restart it after exit; a Startup-folder shortcut makes this optional behavior automatic after login. |
| `docs/release/PORTABLE_WINDOWS_RELEASE.md` | Create the durable download, extraction, config, content replacement, and troubleshooting guide; staging copies it as package-root `README.md`. |
| `.github/workflows/publish-release.yml` | Create Windows tag/manual release workflow. |
| `docs/guides/BUILDING_EXECUTABLE.md` | Link implemented profile, package script, and release workflow. |
| `docs/guides/CONTENT_SETS.md` | Add concise portable-package content replacement note. |
| `scripts/README.md` | Document package/validation commands. |
| `docs/exec-plans/active/README.md`, `docs/TO_DO.md`, `CHANGELOG.md` | Register work and record release-planning bookkeeping. |

## Phase 0: Baseline and Content Safety

- [x] **0.1 Establish baseline.** Run `git status --short`, `dotnet --info`, and `./scripts/verify.ps1`. Expected: changes are limited to this release branch (the active-plan bookkeeping is expected), the SDK comes from `global.json`, and the full gate passes. Record verification evidence in the implementation PR summary.

- [ ] **0.2 Confirm public data eligibility.** Review `Images&Content\Production-Content` with the content owner. It is excluded by default even when it appears harmless. Confirm the package may include only `Assets` and `Demo-Content`.

- [ ] **0.3 Confirm operational choices.** Restate the accepted first-release choices above in the implementation PR: self-contained `win-x64`, zip, unsigned, tag release/manual artifact, and no actual tag/release created in that code PR.

## Phase 1: Reproducible Local Publish and Package

- [x] **1.1 Add the profile.** Create `WindowsPortable.pubxml` with explicit MSBuild properties `Configuration=Release`, `RuntimeIdentifier=win-x64`, `SelfContained=true`, `PublishSingleFile=true`, and `IncludeNativeLibrariesForSelfExtract=true`. Do not enable compression or ReadyToRun initially.

  ```powershell
  dotnet publish InteractiveWorldMap.csproj -c Release -p:PublishProfile=Properties\PublishProfiles\WindowsPortable.pubxml -o artifacts\publish\win-x64
  ```

  Expected: EXE, default config, and copied content exist before staging.

- [x] **1.2 Add safe staging and archive creation.** `package_windows_release.ps1` accepts `-PublishDirectory`, `-OutputDirectory`, and `-Version`. It resolves paths; rejects a publish directory containing checkout markers (such as `.git`, the solution, or the app project) and any output equal to/above the checkout; creates a fresh versioned staging root; copies publish output; removes only the staging copies of `Images&Content\Production-Content`, `Images&Content\Extras`, and the source-content README; excludes release symbols/docs (`*.pdb`, `*.xml`) unless deliberately enabled later; copies `release-tools\` to `<package root>\Tools\`; copies `docs\release\PORTABLE_WINDOWS_RELEASE.md` to `<package root>\README.md`; validates the staged root; creates `InteractiveWorldMap-win-x64-<version>.zip`; then validates the zip. Use Git-ignored `artifacts\` output only.

- [x] **1.3 Add deterministic package validation.** The validator must fail when the EXE, config default, Assets, Demo content, package `Tools` helper, or package-root `README.md` is absent; when `Production-Content`, `Extras`, or runtime `visual-config.json` is present; when symbols/docs (`*.pdb`, `*.xml`) or prohibited repo/build directories (`.git`, `bin`, `obj`, `TestResults`, `Tests`, `scripts`, `docs`, `Models`, `Views`, `Services`, `Utilities`, `.github`, `artifacts`) enter the archive; when the filename/version is unsafe; or when the zip lacks exactly one root directory. Treat the presence of `Images&Content\Production-Content` itself as failure, including an otherwise-empty placeholder. Use temporary fixture folders/archives; do not commit binaries.

- [x] **1.4 Run a local packaging smoke.**

  ```powershell
  dotnet publish InteractiveWorldMap.csproj -c Release -p:PublishProfile=Properties\PublishProfiles\WindowsPortable.pubxml -o artifacts\publish\win-x64
  .\scripts\package_windows_release.ps1 -PublishDirectory artifacts\publish\win-x64 -OutputDirectory artifacts\release -Version 0.0.0-local
  py -3 scripts\verify_release_package.py --zip artifacts\release\InteractiveWorldMap-win-x64-0.0.0-local.zip
  ```

## Phase 2: Config and User Content Handoff

- [x] **2.1 Add the package-local helper.** In an archive, `Tools\Configure-InteractiveWorldMap.ps1` calculates the app root as the parent of its own directory. It verifies the sibling EXE/default config, reports resolved paths, seeds a missing runtime config, toggles `EnableDeveloperTools` only on explicit `on|off|toggle`, and can recover malformed JSON by a recoverable rename. Keep an interactive pause and `-NoPrompt`/`-NoPause` escape. The `.bat` invokes it from cmd/Explorer.

- [x] **2.2 Preserve repository helpers.** Do not silently repurpose `configure.ps1`, `configure.bat`, `toggle-dev-tools.bat`, or `scripts\toggle-dev-tools.ps1`; they remain repo/bin tools. Separate package tools avoid ambiguity and preserve current behavior.

- [x] **2.3 Document content behavior.** Explain the supplied assets/demo, the Production coordinate-source requirement, restart, Demo fallback, writable-folder requirement, and set-namespaced manual layouts/cache under `%AppData%\InteractiveWorldMap`.

- [ ] **2.4 Manually smoke an extracted package.** Extract the zip outside build output. Launch once to confirm config seeding; execute the package `.bat` from Explorer and console; add a minimal valid Production fixture then restart and confirm Production; rename it `.disabled` then confirm Demo fallback; and, when available, run on Windows without the .NET 6 Desktop Runtime. Label this as manual evidence.

## Phase 3: GitHub Actions Artifact and Release

- [x] **3.1 Create `publish-release.yml`.** Trigger on `push.tags: ["v*"]` and `workflow_dispatch`. Set workflow-level `permissions: contents: read`. The `package` job runs on `windows-latest`; checks out the requested ref; installs .NET 6 per `global.json`; runs `./scripts/verify.ps1` before publishing; then invokes the checked-in profile and staging script. Keep `ci.yml` read-only and unchanged because tag builds cannot rely on its branch-only trigger.

- [x] **3.2 Build exactly one release archive.** The workflow invokes the profile and staging script. For a tag build, derive `VERSION` from `github.ref_name` by stripping exactly one leading `v` and reject values that are not filename-safe version strings. Manual runs require a clearly labelled, equally validated version input. Pass that exact value to `-Version`, and name the zip from it. Upload the validated package folder as the manual `portable-release` artifact so its download extracts once into the usable app layout; tag runs additionally upload that same validated zip as `portable-release-archive`. `PortableReleaseWorkflowTests` guards the package tool copy/validator contract and the manual-versus-tag artifact wiring. Do not rebuild/re-zip for the release step.

- [x] **3.3 Create GitHub Releases only for tags.** Use a separate `release` job that `needs: package`, runs only for `refs/tags/v*`, declares `permissions: contents: write`, and downloads the already-uploaded `portable-release-archive` with `actions/download-artifact@v4`. Use the GitHub CLI (`gh release create`) with `GITHUB_TOKEN` to attach that exact zip; do not rebuild or re-zip. Manual dispatch produces the directly extractable folder artifact but no GitHub Release. If signing later uses OIDC, add `id-token: write` only to that signing job.

- [ ] **3.4 Exercise manual dispatch.** Download the workflow artifact, validate/extract it, and complete Phase 2 manual smoke. Verify tag filename logic with a tag-shaped test reference; the maintainer creates the real version tag.

## Phase 4: Documentation, Verification, and Closure

- [x] **4.1 Update durable docs.** Update building/content/script docs and add the release guide. Guides describe behavior and commands; this plan remains the execution checklist.

- [x] **4.2 Complete final review.** Confirm self-contained package; no private Production data, Extras, symbols, or repo/test artifacts; documented Production activation/Demo fallback; package helper does not search `bin\`; workflow permissions are minimal; and signing/installer/.NET 8 remain explicitly deferred.

- [x] **4.3 Run final commands.**

  ```powershell
  .\scripts\verify.ps1
  dotnet publish InteractiveWorldMap.csproj -c Release -p:PublishProfile=Properties\PublishProfiles\WindowsPortable.pubxml -o artifacts\publish\win-x64
  .\scripts\package_windows_release.ps1 -PublishDirectory artifacts\publish\win-x64 -OutputDirectory artifacts\release -Version 0.0.0-local
  py -3 scripts\verify_release_package.py --zip artifacts\release\InteractiveWorldMap-win-x64-0.0.0-local.zip
  ```

- [ ] **4.4 Close bookkeeping after implementation.** Remove/narrow the backlog item, add the final `[Unreleased]` implementation entry, archive this plan, and update the active registry. Do not mark plan steps complete merely for writing this plan.

## Acceptance Criteria

- A maintainer can make the exact local portable zip from checked-in profile/scripts without changing checkout content.
- The zip has one root folder containing the EXE, config default, Assets, Demo content, package `Tools` helper, and root `README.md`; it contains neither Production content, Extras, symbols, nor runtime `visual-config.json`.
- Recipients run it on `win-x64` without installing .NET SDK/runtime.
- A user can add/replace valid `Images&Content\Production-Content`; it wins on restart, and Demo stays fallback.
- Package tooling operates from its declared relative position beside the EXE and never requires repo `bin\` paths.
- Manual dispatch provides a validated artifact; a `v*` tag attaches that same artifact to a GitHub Release.
- `scripts/verify.ps1`, package validator, and recorded extracted-package manual smoke pass before completion.

## Risks and Deferred Follow-ups

| Risk | Mitigation / trigger |
|---|---|
| Unsigned EXE triggers SmartScreen | Document for internal/demo use; add code signing before broad distribution. |
| .NET 6 unsupported | Self-contained removes install dependency, not lifecycle risk; schedule .NET 8 migration before public rollout. |
| Extraction under `Program Files` | Tell users to choose a writable folder; future installer must handle mutable config. |
| User replaces app assets | Keep Assets separate and label them app-owned. |
| Private Production data ships accidentally | Exclude during staging and reject it in archive validation. |
| Need shortcuts, uninstall, updates | Evaluate installer/MSIX/Velopack only after portable release validates demand. |
