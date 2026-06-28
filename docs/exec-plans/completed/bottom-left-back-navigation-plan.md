# Bottom-Left Back Navigation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Move the renamed `← Back to Full Map` button to the lower-left corner, stack the manual-layout indicator above it, and prevent that indicator from appearing when developer tools are disabled.

**Architecture:** `MainWindow.xaml` will own one bottom-left overlay stack whose children retain independent visibility. `MainWindow.LayoutEditor.partial.cs` will extend the existing indicator visibility predicate with the established `AreDeveloperToolsEnabled()` master gate. A focused test class will parse XAML and inspect the event wiring so the visual hierarchy and production-mode behavior cannot silently regress.

**Tech Stack:** WPF XAML, C# 10, .NET 6, xUnit, LINQ to XML

**Design:** [2026-06-28-bottom-left-back-navigation-design.md](../../superpowers/specs/2026-06-28-bottom-left-back-navigation-design.md)

---

**Completed:** 2026-06-28. The back button now occupies the lower-left overlay, the manual-layout indicator stacks above it and respects `EnableDeveloperTools`, the focused red-green regressions pass, and `.\scripts\verify.ps1` passes with 468 tests.

## File Structure

| File | Responsibility |
|------|----------------|
| `Tests/NavigationOverlayTests.cs` | Structural regressions for overlay placement, label, ordering, and developer-tools gating. |
| `MainWindow.xaml` | Bottom-left overlay container and presentation order. |
| `MainWindow.LayoutEditor.partial.cs` | Manual-layout indicator visibility decision. |
| `docs/TO_DO.md` | Remove the completed overlap item. |
| `CHANGELOG.md` | Record the user-visible navigation move under `[Unreleased]`. |

The production edits are small and do not justify a new runtime class or config field. No modified C# file approaches the 800-line limit because the C# change adds only one predicate term.

### Task 1: Add Failing Overlay Regression Tests

**Files:**
- Create: `Tests/NavigationOverlayTests.cs`
- Inspect: `MainWindow.xaml`
- Inspect: `MainWindow.LayoutEditor.partial.cs`

- [x] **Step 1: Create the XAML placement test**

Create `Tests/NavigationOverlayTests.cs`:

```csharp
using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Xunit;

namespace InteractiveWorldMap.Tests;

public class NavigationOverlayTests
{
    private static readonly string RepoRoot =
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));

    [Fact]
    public void BackNavigation_IsBottomLeftWithManualLayoutIndicatorAboveIt()
    {
        var document = XDocument.Load(Path.Combine(RepoRoot, "MainWindow.xaml"));
        XNamespace presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";

        var backButton = document
            .Descendants(presentation + "Button")
            .Single(element => (string?)element.Attribute(x + "Name") == "BackButton");
        var overlay = backButton.Parent;

        Assert.NotNull(overlay);
        Assert.Equal("StackPanel", overlay!.Name.LocalName);
        Assert.Equal("Left", (string?)overlay.Attribute("HorizontalAlignment"));
        Assert.Equal("Bottom", (string?)overlay.Attribute("VerticalAlignment"));
        Assert.Equal("← Back to Full Map", (string?)backButton.Attribute("Content"));

        var namedChildren = overlay.Elements()
            .Select(element => (string?)element.Attribute(x + "Name"))
            .Where(name => name != null)
            .ToArray();

        Assert.Equal(new[] { "ManualLayoutIndicator", "BackButton" }, namedChildren);
    }
}
```

- [x] **Step 2: Run the placement test and verify RED**

Run:

```powershell
dotnet test Tests\InteractiveWorldMap.Tests.csproj --filter "NavigationOverlayTests.BackNavigation_IsBottomLeftWithManualLayoutIndicatorAboveIt"
```

Expected: FAIL because `BackButton` is still a direct top-aligned child of `RootGrid`, its label is still `← Back to Map`, and the indicator is not its sibling in a shared stack.

- [x] **Step 3: Add the developer-tools gate test**

Append inside `NavigationOverlayTests`:

```csharp
[Fact]
public void ManualLayoutIndicator_RequiresDeveloperToolsGate()
{
    var source = File.ReadAllText(
        Path.Combine(RepoRoot, "MainWindow.LayoutEditor.partial.cs"));

    var eventIndex = source.IndexOf(
        "_layoutEditor.ManualLayoutActivityChanged += isActive =>",
        StringComparison.Ordinal);
    Assert.True(eventIndex >= 0, "Manual-layout activity handler not found.");

    var eventEnd = source.IndexOf("};", eventIndex, StringComparison.Ordinal);
    Assert.True(eventEnd > eventIndex, "Manual-layout activity handler end not found.");

    var handler = source.Substring(eventIndex, eventEnd - eventIndex);
    Assert.Contains("AreDeveloperToolsEnabled()", handler);
}
```

- [x] **Step 4: Run both tests and verify RED**

Run:

```powershell
dotnet test Tests\InteractiveWorldMap.Tests.csproj --filter NavigationOverlayTests
```

Expected: 2 failed. The placement test reports the current root grid/top alignment or label, and the gate test reports that `AreDeveloperToolsEnabled()` is absent from the activity handler.

### Task 2: Implement the Bottom-Left Overlay

**Files:**
- Modify: `MainWindow.xaml:20-59`
- Modify: `MainWindow.xaml:416-431`
- Modify: `MainWindow.LayoutEditor.partial.cs:136-142`
- Test: `Tests/NavigationOverlayTests.cs`

- [x] **Step 1: Replace the independent controls with one overlay stack**

In `MainWindow.xaml`, replace the current top-level `BackButton` and later independent `ManualLayoutIndicator` with:

```xml
<!-- Bottom-left navigation and developer status -->
<StackPanel HorizontalAlignment="Left"
            VerticalAlignment="Bottom"
            Margin="20">
    <Border x:Name="ManualLayoutIndicator"
            Visibility="Collapsed"
            Margin="0,0,0,8"
            HorizontalAlignment="Left"
            Background="#CC4169E1"
            BorderBrush="White"
            BorderThickness="1"
            CornerRadius="6"
            Padding="10,5">
        <TextBlock Text="Manual Layout Active"
                   Foreground="White"
                   FontSize="11"
                   FontWeight="SemiBold"/>
    </Border>

    <Button x:Name="BackButton"
            Content="← Back to Full Map"
            Visibility="Collapsed"
            HorizontalAlignment="Left"
            Padding="15,8"
            FontSize="14"
            FontWeight="SemiBold"
            Foreground="White"
            Background="#CC000000"
            BorderBrush="White"
            BorderThickness="2"
            Cursor="Hand"
            Click="OnBackButtonClick">
        <!-- Retain the existing BackButton style and template unchanged. -->
    </Button>
</StackPanel>
```

Move the existing complete `Button.Style` block into the new `BackButton`; do not replace it with the comment shown above. Remove the old independent `ManualLayoutIndicator` block so each named control appears exactly once.

- [x] **Step 2: Gate the manual-layout indicator**

Change the existing activity handler in `MainWindow.LayoutEditor.partial.cs` to:

```csharp
_layoutEditor.ManualLayoutActivityChanged += isActive =>
{
    ManualLayoutIndicator.Visibility =
        isActive &&
        AreDeveloperToolsEnabled() &&
        _visualConfig.ManualLayoutEditor.ShowLayoutIndicator
            ? Visibility.Visible
            : Visibility.Collapsed;
};
```

- [x] **Step 3: Run focused tests and verify GREEN**

Run:

```powershell
dotnet test Tests\InteractiveWorldMap.Tests.csproj --filter NavigationOverlayTests
```

Expected: 2 passed, 0 failed.

- [x] **Step 4: Run nearby gate and navigation regressions**

Run:

```powershell
dotnet test Tests\InteractiveWorldMap.Tests.csproj --filter "DeveloperToolsGateTests|ManualLayoutZoomAnimationTests|NavigationOverlayTests"
```

Expected: all selected tests pass with 0 failures.

- [x] **Step 5: Commit the tested behavior**

```powershell
git add Tests/NavigationOverlayTests.cs MainWindow.xaml MainWindow.LayoutEditor.partial.cs
git commit -m "feat: move back navigation to lower left"
```

### Task 3: Complete Documentation and Verification

**Files:**
- Modify: `docs/TO_DO.md`
- Modify: `CHANGELOG.md`
- Move: `docs/exec-plans/active/bottom-left-back-navigation-plan.md`
- Modify: `docs/exec-plans/active/README.md` only if it explicitly lists this plan

- [x] **Step 1: Remove the completed backlog item**

Delete this bullet from `docs/TO_DO.md`:

```markdown
- [ ] Content view: prevent the Information panel from covering `Back to Map`; prefer moving `Back to Map` to the lower-left corner.
```

- [x] **Step 2: Add the changelog entry**

Under `[Unreleased]` in `CHANGELOG.md`, add:

```markdown
- **Bottom-left full-map navigation:** Moved and renamed the zoomed-content back button to `← Back to Full Map` in the lower-left corner, placed the manual-layout status above it, and hid that developer status when developer tools are disabled.
```

- [x] **Step 3: Run the full Windows verification gate**

Run:

```powershell
.\scripts\verify.ps1
```

Expected: build, tests, vulnerability scan, seed checks, documentation links, taste checks, and headless startup validation all pass.

- [x] **Step 4: Review the diff against the design**

Run:

```powershell
git diff --check
git diff -- MainWindow.xaml MainWindow.LayoutEditor.partial.cs Tests/NavigationOverlayTests.cs docs/TO_DO.md CHANGELOG.md
```

Confirm:

- the button is lower-left and says `← Back to Full Map`;
- the indicator is directly above it;
- the back button is not developer-gated;
- the indicator requires the central developer-tools gate;
- no unrelated UI or navigation behavior changed.

- [x] **Step 5: Archive this completed exec plan**

Move:

```text
docs/exec-plans/active/bottom-left-back-navigation-plan.md
```

to:

```text
docs/exec-plans/completed/bottom-left-back-navigation-plan.md
```

If `docs/exec-plans/active/README.md` lists the plan, move that registry entry to its completed-plan section or remove the active entry according to the existing format.

- [x] **Step 6: Commit completion bookkeeping**

```powershell
git add CHANGELOG.md docs/TO_DO.md docs/exec-plans/active/README.md docs/exec-plans/completed/bottom-left-back-navigation-plan.md
git commit -m "docs: complete bottom-left navigation plan"
```
