# Map Render Quality Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Preserve thin map strokes in the settled full-map view, remove forced bitmap aliasing, and make animation scaling explicit while keeping the current `Fill` presentation and high-resolution settled zoom path.

**Architecture:** Keep each rendering decision in its existing owner: `MapDisplayControl` uses `Fant` for settled viewport crops, animation keyframes use `Linear` before materialization, and `ZoomedRegionCache` retains `Fant` for settled high-resolution zoom crops. Invalidate existing animation frames by incrementing their cache version; do not add aspect-mode geometry or a new settled-map cache in this slice.

**Tech Stack:** C# 10, WPF, .NET 6, xUnit, XML/source contract tests

**Design:** [2026-07-01-map-render-quality-design.md](../specs/2026-07-01-map-render-quality-design.md)

**Owning exec plan:** [zoom-performance-appearance-plan.md](../../exec-plans/active/zoom-performance-appearance-plan.md), items 2.6 and 2.7

---

## File Structure

- Create `Tests/MapImageRenderingPolicyTests.cs`: structural regression coverage for settled full-map, animation-keyframe, and settled zoom scaling policies.
- Modify `Views/MapDisplayControl.xaml`: retain `Fill` and pixel snapping, select `Fant`, and remove forced aliased edges.
- Modify `Views/MapDisplayControl.xaml.cs`: stop resetting nearest-neighbor and move the unchanged cache hint out of the viewport-update hot path.
- Modify `MainWindow.Navigation.partial.cs`: select `Linear` before animation keyframes are materialized.
- Modify `Services/AnimationFrameCache.cs`: invalidate frames rendered with the prior pixel policy.
- Verify, but do not otherwise modify, `Services/ZoomedRegionCache.cs`: its settled zoom path remains `Fant`.
- Modify `docs/exec-plans/active/zoom-performance-appearance-plan.md`, `docs/TO_DO.md`, and `CHANGELOG.md`: completion bookkeeping and deferred follow-ups.

## Task 1: Correct Settled Full-Map Rendering

**Files:**
- Create: `Tests/MapImageRenderingPolicyTests.cs`
- Modify: `Views/MapDisplayControl.xaml`
- Modify: `Views/MapDisplayControl.xaml.cs`

- [x] **Step 1: Write failing full-map rendering-policy tests**

Create `Tests/MapImageRenderingPolicyTests.cs`:

```csharp
using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Xunit;

namespace InteractiveWorldMap.Tests;

public class MapImageRenderingPolicyTests
{
    private static string RepoRoot =>
        Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", ".."));

    [Fact]
    public void SettledMapImage_KeepsFillAndUsesFantWithoutAliasedEdges()
    {
        var document = XDocument.Load(
            Path.Combine(RepoRoot, "Views", "MapDisplayControl.xaml"));
        XNamespace presentation =
            "http://schemas.microsoft.com/winfx/2006/xaml/presentation";

        var image = document
            .Descendants(presentation + "Image")
            .Single(element =>
                (string?)element.Attribute(
                    XName.Get("Name",
                        "http://schemas.microsoft.com/winfx/2006/xaml")) ==
                "MapImage");

        Assert.Equal("Fill", image.Attribute("Stretch")?.Value);
        Assert.Equal("True", image.Attribute("SnapsToDevicePixels")?.Value);
        Assert.Equal(
            "Fant",
            image.Attribute("RenderOptions.BitmapScalingMode")?.Value);
        Assert.Null(image.Attribute("RenderOptions.EdgeMode"));
    }

    [Fact]
    public void UpdateViewport_DoesNotRestoreNearestNeighbor()
    {
        var source = File.ReadAllText(
            Path.Combine(RepoRoot, "Views", "MapDisplayControl.xaml.cs"));

        Assert.DoesNotContain(
            "BitmapScalingMode.NearestNeighbor",
            source,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "SetBitmapScalingMode(MapImage",
            source,
            StringComparison.Ordinal);
    }
}
```

- [x] **Step 2: Run the focused tests and verify RED**

Run:

```powershell
dotnet test Tests\InteractiveWorldMap.Tests.csproj --filter "FullyQualifiedName~MapImageRenderingPolicyTests" --no-restore
```

Expected: `SettledMapImage_KeepsFillAndUsesFantWithoutAliasedEdges` fails
because the XAML contains `NearestNeighbor` and `EdgeMode="Aliased"`;
`UpdateViewport_DoesNotRestoreNearestNeighbor` fails on the code-behind
override.

- [x] **Step 3: Apply the settled rendering policy**

Change the `MapImage` attributes in `Views/MapDisplayControl.xaml` to:

```xml
<Image x:Name="MapImage"
       Stretch="Fill"
       HorizontalAlignment="Stretch"
       VerticalAlignment="Stretch"
       RenderOptions.BitmapScalingMode="Fant"
       SnapsToDevicePixels="True"/>
```

In `MapDisplayControl`, set the unchanged cache hint once in the constructor:

```csharp
public MapDisplayControl()
{
    InitializeComponent();
    RenderOptions.SetCachingHint(MapImage, CachingHint.Cache);
    SizeChanged += OnSizeChanged;
}
```

Remove both attached-property calls from `UpdateViewport`:

```csharp
RenderOptions.SetBitmapScalingMode(
    MapImage, BitmapScalingMode.NearestNeighbor);
RenderOptions.SetCachingHint(MapImage, CachingHint.Cache);
```

Keep the crop creation and source assignment unchanged:

```csharp
var croppedBitmap = new CroppedBitmap(_sourceImage, sourceRect);
MapImage.Source = croppedBitmap;
```

- [x] **Step 4: Run focused tests and verify GREEN**

Run the Step 2 command.

Expected: both `MapImageRenderingPolicyTests` tests pass.

- [x] **Step 5: Run viewport regression tests**

Run:

```powershell
dotnet test Tests\InteractiveWorldMap.Tests.csproj --filter "FullyQualifiedName~ViewportStateTests|FullyQualifiedName~MarkerInteractionWiringTests" --no-restore
```

Expected: all selected tests pass, proving the quality change did not alter
`Fill` coordinate mapping or marker-interaction layer wiring.

- [x] **Step 6: Commit settled-map rendering**

```powershell
git add Views\MapDisplayControl.xaml Views\MapDisplayControl.xaml.cs Tests\MapImageRenderingPolicyTests.cs
git commit -m "fix: use high-quality settled map scaling"
```

## Task 2: Make Animation Scaling Explicit and Preserve Settled Zoom Quality

**Files:**
- Modify: `Tests/MapImageRenderingPolicyTests.cs`
- Modify: `MainWindow.Navigation.partial.cs`
- Modify: `Services/AnimationFrameCache.cs`
- Verify: `Services/ZoomedRegionCache.cs`

- [x] **Step 1: Add failing animation and settled-zoom policy tests**

Append these tests to `MapImageRenderingPolicyTests`:

```csharp
[Fact]
public void AnimationFrames_SelectLinearBeforeMaterialization()
{
    var source = File.ReadAllText(
        Path.Combine(RepoRoot, "MainWindow.Navigation.partial.cs"));

    var transform = source.IndexOf(
        "var scaledBitmap = new TransformedBitmap(",
        StringComparison.Ordinal);
    var linear = source.IndexOf(
        "BitmapScalingMode.Linear",
        transform,
        StringComparison.Ordinal);
    var materialize = source.IndexOf(
        "new WriteableBitmap(scaledBitmap)",
        transform,
        StringComparison.Ordinal);

    Assert.True(transform >= 0, "Keyframe transform not found.");
    Assert.True(
        linear > transform && linear < materialize,
        "Linear scaling must be selected before keyframe materialization.");
}

[Fact]
public void AnimationFrameCache_VersionInvalidatesPriorPixelPolicy()
{
    var source = File.ReadAllText(
        Path.Combine(RepoRoot, "Services", "AnimationFrameCache.cs"));

    Assert.Contains(
        "private const int CacheVersion = 16;",
        source,
        StringComparison.Ordinal);
}

[Fact]
public void SettledZoomCrop_ContinuesToUseFant()
{
    var source = File.ReadAllText(
        Path.Combine(RepoRoot, "Services", "ZoomedRegionCache.cs"));

    Assert.Contains(
        "BitmapScalingMode.Fant",
        source,
        StringComparison.Ordinal);
}
```

- [x] **Step 2: Run the focused tests and verify RED**

Run:

```powershell
dotnet test Tests\InteractiveWorldMap.Tests.csproj --filter "FullyQualifiedName~MapImageRenderingPolicyTests" --no-restore
```

Expected: the animation test fails because no explicit `Linear` policy exists,
and the cache-version test fails because the version is still 15. The settled
zoom characterization test already passes.

- [x] **Step 3: Select Linear at the keyframe transformation boundary**

In `PreRenderKeyframes`, set the scaling mode after constructing
`scaledBitmap` and before constructing `WriteableBitmap`:

```csharp
var scaledBitmap = new TransformedBitmap(
    croppedBitmap,
    new ScaleTransform(
        displayWidth / (double)sourceRect.Width,
        displayHeight / (double)sourceRect.Height));
RenderOptions.SetBitmapScalingMode(
    scaledBitmap, BitmapScalingMode.Linear);

prerenderedFrames[i] = new WriteableBitmap(scaledBitmap);
prerenderedFrames[i].Freeze();
```

Do not change `ZoomedRegionCache.ScaleBitmap`; it must retain:

```csharp
RenderOptions.SetBitmapScalingMode(
    scaledBitmap, BitmapScalingMode.Fant);
```

- [x] **Step 4: Invalidate old materialized animation frames**

Change the cache version in `AnimationFrameCache`:

```csharp
// Increment when interpolation geometry or pixel-resampling policy changes.
private const int CacheVersion = 16;
```

- [x] **Step 5: Run focused tests and verify GREEN**

Run the Step 2 command.

Expected: all rendering-policy tests pass.

- [x] **Step 6: Run zoom cache and tracking regressions**

Run:

```powershell
dotnet test Tests\InteractiveWorldMap.Tests.csproj --filter "FullyQualifiedName~ZoomedRegionCacheTests|FullyQualifiedName~ZoomOutTrackingTests" --no-restore
```

Expected: all selected tests pass.

- [x] **Step 7: Commit animation rendering policy**

```powershell
git add MainWindow.Navigation.partial.cs Services\AnimationFrameCache.cs Tests\MapImageRenderingPolicyTests.cs
git commit -m "fix: smooth map animation resampling"
```

## Task 3: Verify Image Quality and Performance on Windows

**Files:**
- No source changes expected.
- Record results in: `docs/exec-plans/active/zoom-performance-appearance-plan.md`

- [x] **Step 1: Run the complete automated gate**

Run:

```powershell
.\scripts\verify.ps1
```

Expected: build, tests, harness checks, architecture checks, documentation
checks, startup validation, and configured security checks pass.

- [ ] **Step 2: Run the fixed full-map visual comparison**

Pending: Windows app-control approval timed out on July 1, 2026 before the
application could be launched for inspection. Automated startup validation
passed, but this visual step was not run.

Launch:

```powershell
dotnet run --project InteractiveWorldMap.csproj
```

At the settled full-map view, inspect:

- the `U` and neighboring strokes in `SOUTH AMERICA`;
- thin latitude/longitude grid lines;
- representative coastline and border segments;
- large labels over blue ocean and beige land.

Expected:

- the `U` remains continuous rather than losing a sampled stroke;
- thin lines retain partial gray/color coverage instead of dropping out;
- the map remains full-screen with the same non-uniform `Fill` geometry;
- markers and hit targets remain aligned with their prior map locations.

- [ ] **Step 3: Verify all rendering phases**

Pending for the same app-control approval timeout recorded in Step 2.

Exercise one standalone location and one dense cluster:

1. settled full map;
2. zoom-in animation;
3. settled zoom;
4. zoom-out animation;
5. settled return to full map.

Expected:

- no blocky nearest-neighbor appearance during animation;
- no new blank/incorrect frame from cache invalidation;
- settled zoom remains at least as detailed as before;
- no visible quality regression when returning to the full map.

- [ ] **Step 4: Check available output sizes**

Pending. The available 2560 x 1440 display was detected during the
investigation, but no live application image was approved for inspection;
1080p and 4K were not available.

Repeat the visual comparison at 1920 x 1080, 2560 x 1440, and 3840 x 2160
when those outputs are available. If only one physical display is available,
record the tested physical resolution and leave the unavailable resolutions
explicitly unverified.

Expected: the map remains `Fill` at every tested size and the reference strokes
do not exhibit nearest-neighbor dropout.

- [ ] **Step 5: Measure whether deferred settled-map caching is warranted**

Pending. Keep the separate cache backlog item active until the live timing
check can run.

Using the existing frame timing/logging workflow, observe full-map settle and
resize behavior on target hardware.

Keep the cache deferred unless either condition is reproduced:

- a settled full-map update or resize consistently exceeds 33 ms; or
- returning to/resizing the full map produces repeated UI-thread frame gaps
  above 33 ms.

Record observed timings in the owning exec plan. Do not add the cache in this
implementation slice.

- [x] **Step 6: Commit verification evidence if it changes the plan**

If measurements or unavailable hardware require a plan note:

```powershell
git add docs\exec-plans\active\zoom-performance-appearance-plan.md
git commit -m "docs: record map rendering verification"
```

If no documentation changes were needed, do not create an empty commit.

## Task 4: Completion Bookkeeping

**Files:**
- Modify: `docs/exec-plans/active/zoom-performance-appearance-plan.md`
- Modify: `docs/TO_DO.md`
- Modify: `CHANGELOG.md`

- [ ] **Step 1: Mark the rendering-quality slice complete**

In the active exec plan:

- mark item 2.6 complete after settled `Fant`, cache-hint cleanup, and edge-mode
  removal are verified;
- mark item 2.7 complete after explicit `Linear` animation frames, cache
  invalidation, and the full phase visual check are verified;
- record the tested display resolutions and reference regions;
- leave unrelated items 2.4, 2.5, and 2.8-2.10 unchanged.

- [x] **Step 2: Narrow the active backlog bullet**

Rewrite the smooth/fast zoom bullet in `docs/TO_DO.md` so its remaining scope
does not list completed render-options or anti-pop work. Preserve the separate
aspect/display-mode and settled-map-cache bullets until those follow-ups are
implemented or intentionally parked.

- [x] **Step 3: Add the user-visible changelog entry**

Add under `[Unreleased]`:

```markdown
- **Map image rendering quality:** Settled full-map rendering now uses Fant
  downsampling without forced aliased edges, and zoom animation frames use
  explicit linear scaling. The full-screen Fill presentation is unchanged,
  while the existing high-resolution settled zoom path remains Fant-filtered.
```

- [x] **Step 4: Re-run documentation and full verification**

Run:

```powershell
py -3 scripts\verify_doc_links.py
py -3 scripts\doc_gardening.py
.\scripts\verify.ps1
```

Expected: all commands pass.

- [x] **Step 5: Review the final diff**

Run:

```powershell
git diff --check
git status --short
git diff -- Views\MapDisplayControl.xaml Views\MapDisplayControl.xaml.cs MainWindow.Navigation.partial.cs Services\AnimationFrameCache.cs Tests\MapImageRenderingPolicyTests.cs docs\exec-plans\active\zoom-performance-appearance-plan.md docs\TO_DO.md CHANGELOG.md
```

Expected: only the planned rendering, tests, and bookkeeping changes appear;
no aspect-mode, viewport-coordinate, marker-placement, or source-image changes
are present.

- [x] **Step 6: Commit completion bookkeeping**

```powershell
git add docs\exec-plans\active\zoom-performance-appearance-plan.md docs\TO_DO.md CHANGELOG.md
git commit -m "docs: record map rendering quality fix"
```

## Modularity / File Size Impact

- `MainWindow.Navigation.partial.cs` is currently about 609 lines. This plan
  adds one attached-property call and no new orchestration branch, keeping it
  below the 800-line limit.
- `MapDisplayControl.xaml.cs` is currently about 188 lines and shrinks slightly
  by removing repeated render-option work.
- `ZoomedRegionCache.cs` is currently about 239 lines and receives no behavior
  change.
- `AnimationFrameCache.cs` receives only a version/comment update.
- Rendering ownership stays in the existing View, navigation partial, and
  cache services. No View-to-Service dependency or new composition-root logic
  is introduced.
- `MapImageRenderingPolicyTests` protects the cross-file rendering contract so
  future performance changes cannot silently restore nearest-neighbor or
  aliased settled rendering.

## Deferred Follow-Ups

The following remain tracked in `docs/TO_DO.md`, not implemented here:

- optional `Uniform`, `UniformToFill`, letterboxed, or cropped map presentation
  modes and their required marker/input/animation geometry;
- a physical-resolution/DPI-aware settled full-map render cache, activated only
  by the measured 33 ms criteria in the approved design;
- the separate animation-frame disk-cache keep/delete decision in active-plan
  item 2.5.
