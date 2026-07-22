---
status: completed
owner: agent
started: 2026-07-22
---

# Content Auto-Fullscreen Open Fix Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Prevent a marker click/tap that opens a content popup from immediately toggling that new popup into presentation mode on the same input release.

**Architecture:** Keep presentation toggling owned by `ContentSubwindow`, but add a one-shot suppression flag that consumes the first content-surface mouse-up after marker-triggered popup creation. `MainWindow.MarkerInteraction.partial.cs` marks only direct marker-opened content for suppression; programmatic auto-open after zoom and thumbnail selection keep normal content tap/click behavior.

**Tech Stack:** WPF / .NET 6 / C# / xUnit source-guard tests.

---

## Files

- Modify: `Views/ContentSubwindow.xaml.cs` - add the one-shot suppression API and consume it before toggling presentation mode.
- Modify: `MainWindow.Content.partial.cs` - add an optional parameter to pass the suppression intent into newly created content windows.
- Modify: `MainWindow.MarkerInteraction.partial.cs` - request suppression for the direct already-zoomed marker-open path.
- Modify: `Tests/ContentPresentationModeTests.cs` - assert the content surface consumes the one-shot suppression before `TryTogglePresentationMode`.
- Modify: `Tests/SingleLocationZoomClickTests.cs` - assert direct marker content opens request suppression while deferred post-zoom auto-open does not.
- Modify: `CHANGELOG.md` - record the user-visible bugfix.

## Task 1: Add Regression Tests

- [x] Add a source-guard test to `Tests/ContentPresentationModeTests.cs`:

```csharp
[Fact]
public void ContentSurface_ConsumesInitialActivationSuppressionBeforePresentationToggle()
{
    var source = File.ReadAllText(
        Path.Combine(RepoRoot, "Views", "ContentSubwindow.xaml.cs"));
    var body = ExtractMethodBody(source, "private void ContentSurface_MouseLeftButtonUp");

    Assert.Contains("_suppressNextContentActivation", source);
    Assert.Contains("public void SuppressNextContentActivation()", source);
    Assert.Contains("TryConsumeSuppressedContentActivation()", body);
    Assert.Contains("TryTogglePresentationMode(ownerBounds)", body);

    var suppressCheck = body.IndexOf("TryConsumeSuppressedContentActivation()", StringComparison.Ordinal);
    var toggleCall = body.IndexOf("TryTogglePresentationMode(ownerBounds)", StringComparison.Ordinal);
    Assert.True(suppressCheck >= 0, "The content mouse-up handler must consume one-shot suppression.");
    Assert.True(toggleCall > suppressCheck, "Suppression must run before presentation mode can toggle.");
}
```

- [x] Add a source-guard test to `Tests/SingleLocationZoomClickTests.cs`:

```csharp
[Fact]
public void DirectZoomedMarkerOpen_SuppressesInitialContentRelease()
{
    var source = File.ReadAllText(
        Path.Combine(RepoRoot, "MainWindow.MarkerInteraction.partial.cs"));
    var methodBody = ExtractMethodBody(
        source,
        "private void HandleIndividualMarkerPrimaryAction");

    Assert.Contains("ShowContentForLocation(location, suppressNextContentActivation: true);", methodBody);
}
```

- [x] Add a source-guard assertion to the existing auto-open callback test in `Tests/SingleLocationZoomClickTests.cs`:

```csharp
Assert.Contains("ShowContentForLocation(toOpen);", callbackBody);
Assert.DoesNotContain("ShowContentForLocation(toOpen, suppressNextContentActivation: true)", callbackBody);
```

- [x] Run:

```powershell
dotnet test Tests\InteractiveWorldMap.Tests.csproj --filter "FullyQualifiedName~ContentPresentationModeTests|FullyQualifiedName~SingleLocationZoomClickTests"
```

Expected before implementation: fails because the suppression API and marker-open parameter do not exist.

## Task 2: Implement One-Shot Suppression

- [x] In `Views/ContentSubwindow.xaml.cs`, add:

```csharp
private bool _suppressNextContentActivation;

public void SuppressNextContentActivation()
{
    _suppressNextContentActivation = true;
}

private bool TryConsumeSuppressedContentActivation()
{
    if (!_suppressNextContentActivation)
        return false;

    _suppressNextContentActivation = false;
    return true;
}
```

- [x] At the top of `ContentSurface_MouseLeftButtonUp`, before owner-bounds logic, add:

```csharp
if (TryConsumeSuppressedContentActivation())
{
    e.Handled = true;
    return;
}
```

- [x] In `MainWindow.Content.partial.cs`, change:

```csharp
public async void ShowContentForLocation(Location location)
```

to:

```csharp
public async void ShowContentForLocation(
    Location location,
    bool suppressNextContentActivation = false)
```

- [x] After each new `_activeSubwindow = CreateContentSubwindow(location);` and before `_activeSubwindow.ShowContent(...)`, add:

```csharp
if (suppressNextContentActivation)
    _activeSubwindow.SuppressNextContentActivation();
```

- [x] Change the already-zoomed direct marker open in `MainWindow.MarkerInteraction.partial.cs` to:

```csharp
ShowContentForLocation(location, suppressNextContentActivation: true);
```

- [x] Leave deferred post-zoom auto-open as:

```csharp
ShowContentForLocation(toOpen);
```

## Task 3: Verify and Bookkeep

- [x] Run the focused tests from Task 1. Expected after implementation: pass.
- [x] Add a `[Unreleased]` `CHANGELOG.md` fixed entry:

```markdown
- **Content presentation activation:** Marker clicks/taps that open a content popup no longer reuse the same input release to immediately toggle that popup into full-screen presentation mode.
```

- [x] Run:

```powershell
.\scripts\verify.ps1
```

Expected: full verification passes.

- [x] Move this plan to `docs/exec-plans/completed/content-auto-fullscreen-open-fix-plan.md`, remove its active README row, add it to Recently completed, and remove the temporary `docs/TO_DO.md` bullet if one was added.
