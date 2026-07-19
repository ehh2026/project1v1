# Content Window Heading Cleanup Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the didactic window's generic heading with the selected Excel-derived location/person name and remove headings from the main content and thumbnail windows without leaving empty layout space.

**Architecture:** Continue passing the already-loaded `Location` through `MainWindow.Content.partial.cs`; no new Excel lookup or view model is needed. `DidacticTextWindow` owns its dynamic heading and blank-name collapse behavior, while the two static headings and their grid rows are removed directly from XAML.

**Tech Stack:** C# 10, WPF, .NET 6, xUnit, LINQ to XML

---

## File Structure

- `Views/DidacticTextWindow.xaml`: name the dynamic heading.
- `Views/DidacticTextWindow.xaml.cs`: set trimmed heading text and collapse it for blank names.
- `MainWindow.Content.partial.cs`: pass `location.Name` with didactic content.
- `Views/ContentSubwindow.xaml`: remove the main content heading and its row.
- `Views/ContentSubwindow.xaml.cs`: remove title-specific presentation-state handling and ignore the retained compatibility parameter.
- `Views/ThumbnailBrowserWindow.xaml`: remove the `Images` heading and its row.
- `Tests/ContentWindowHeadingTests.cs`: exercise the didactic behavior and static XAML contracts.
- `Tests/ContentPresentationModeTests.cs`: remove assertions tied to the deleted main title.
- `docs/TO_DO.md`: remove the three completed bullets.
- `CHANGELOG.md`: describe the user-visible heading cleanup.

### Task 1: Add failing heading-contract tests

**Files:**
- Create: `Tests/ContentWindowHeadingTests.cs`
- Modify: `Tests/ContentPresentationModeTests.cs`

- [x] **Step 1: Add focused tests**

Create `ContentWindowHeadingTests.cs` with STA tests that instantiate `DidacticTextWindow`, call `SetContent("Body", "Dr. Test")`, and assert the named `HeadingText` displays `Dr. Test`; call `SetContent("Body", "   ")` and assert `HeadingText.Visibility == Visibility.Collapsed`. Add LINQ-to-XML tests asserting `ContentSubwindow.xaml` has no `TitleText` element and exactly two grid row definitions, and `ThumbnailBrowserWindow.xaml` contains no `TextBlock` with `Text="Images"` and exactly one grid row definition.

Remove `TitleText` lookup and visibility assertions from the two presentation-mode tests because the title will no longer exist.

- [x] **Step 2: Run the tests and verify RED**

Run:

```powershell
dotnet test Tests\InteractiveWorldMap.Tests.csproj --filter "FullyQualifiedName~ContentWindowHeadingTests|FullyQualifiedName~ContentPresentationModeTests"
```

Expected: FAIL because `HeadingText` and the two simplified XAML structures do not exist yet.

- [x] **Step 3: Commit the red tests**

```powershell
git add Tests\ContentWindowHeadingTests.cs Tests\ContentPresentationModeTests.cs
git commit -m "test: define content heading cleanup"
```

### Task 2: Implement the three heading changes

**Files:**
- Modify: `Views/DidacticTextWindow.xaml`
- Modify: `Views/DidacticTextWindow.xaml.cs`
- Modify: `MainWindow.Content.partial.cs`
- Modify: `Views/ContentSubwindow.xaml`
- Modify: `Views/ContentSubwindow.xaml.cs`
- Modify: `Views/ThumbnailBrowserWindow.xaml`

- [x] **Step 1: Make the didactic heading dynamic**

Rename the didactic title control to `HeadingText`, remove `Text="Information"`, and change the API to:

```csharp
public void SetContent(string text, string? locationName)
{
    DidacticTextBlock.Text = text;
    var heading = locationName?.Trim();
    HeadingText.Text = heading ?? string.Empty;
    HeadingText.Visibility = string.IsNullOrEmpty(heading)
        ? Visibility.Collapsed
        : Visibility.Visible;
}
```

Update the orchestration call:

```csharp
_activeDidacticWindow.SetContent(didacticText, location.Name);
```

- [x] **Step 2: Remove the main content heading**

Delete `TitleText` and its first `Auto` row from `ContentSubwindow.xaml`; move `ContentInteractionSurface` to row 0 and `TranslateButton` to row 1. Delete `_normalTitleVisibility` and all title visibility save/collapse/restore lines from `ContentSubwindow.xaml.cs`. Keep `locationName` in `ShowContent` for source compatibility, documenting it as the associated location name rather than rendering it.

- [x] **Step 3: Remove the thumbnail heading**

Delete the `Images` `TextBlock` and its first `Auto` row from `ThumbnailBrowserWindow.xaml`; move `ThumbnailScrollViewer` to row 0.

- [x] **Step 4: Run focused tests and verify GREEN**

Run:

```powershell
dotnet test Tests\InteractiveWorldMap.Tests.csproj --filter "FullyQualifiedName~ContentWindowHeadingTests|FullyQualifiedName~ContentPresentationModeTests|FullyQualifiedName~ThumbnailBrowserWindowTests"
```

Expected: PASS.

- [x] **Step 5: Commit implementation**

```powershell
git add Views\DidacticTextWindow.xaml Views\DidacticTextWindow.xaml.cs MainWindow.Content.partial.cs Views\ContentSubwindow.xaml Views\ContentSubwindow.xaml.cs Views\ThumbnailBrowserWindow.xaml
git commit -m "feat: simplify content window headings"
```

### Task 3: Finish bookkeeping and verification

**Files:**
- Modify: `docs/TO_DO.md`
- Modify: `CHANGELOG.md`
- Modify: `docs/superpowers/plans/2026-06-30-content-window-heading-cleanup.md`
- Move to: `docs/exec-plans/completed/` only if this plan is registered as an exec plan (it is not registered by default)

- [x] **Step 1: Update user-facing documentation**

Remove these completed backlog bullets:

- Replace the top-left `Information` heading.
- Remove the main content name heading.
- Remove the thumbnail `Images` heading.

Replace the earlier generic backlog changelog note with an `[Unreleased]` user-visible entry stating that the left didactic window now shows the Excel-derived location/person name, blank names hide the heading, and the main content/thumbnail headings were removed.

- [x] **Step 2: Mark this plan complete**

Check every completed step in this plan. This Superpowers implementation plan is not part of the repository's active exec-plan registry, so it remains under `docs/superpowers/plans/`.

- [x] **Step 3: Run the full Windows verification gate**

Run:

```powershell
.\scripts\verify.ps1
```

Expected: build, tests, vulnerability scan, documentation checks, taste checks, and headless startup validation all pass.

- [x] **Step 4: Review the final diff**

Run:

```powershell
git diff --check
git status --short
git diff
```

Expected: no whitespace errors; only scoped implementation, tests, plan, backlog, and changelog changes remain.

- [x] **Step 5: Commit bookkeeping**

```powershell
git add docs\TO_DO.md CHANGELOG.md docs\superpowers\plans\2026-06-30-content-window-heading-cleanup.md
git commit -m "docs: complete content heading cleanup"
```
