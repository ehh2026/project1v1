---
status: active
owner: agent
started: 2026-06-29
requirements_ref: ../../superpowers/specs/2026-06-29-touch-scrollable-thumbnail-browser-design.md
---

# Touch-Scrollable Thumbnail Browser Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make the right-side thumbnail browser vertically scrollable by touch and mouse wheel with no visible scrollbar, while preserving stationary tap/click selection and verifying its WPF touch arbitration as far as the development environment permits.

**Architecture:** Keep input ownership inside `ThumbnailBrowserWindow`. A WPF `ScrollViewer` owns vertical manipulation and overflow, while each thumbnail uses completed `Button.Click` semantics so a drag can be claimed as scrolling before selection fires; the existing `ThumbnailSelected` event contract remains unchanged. A separate Windows-only developer tool opens the real thumbnail window and uses a custom WPF `TouchDevice` to exercise routed touch and manipulation directly; UI Automation verifies the exact hit-tested thumbnail button's completed activation path. The tool is not exposed through production configuration or the Tuning panel.

**Tech Stack:** C# 10, WPF/.NET 6, XAML, xUnit, LINQ to XML, WPF `TouchDevice`, UI Automation

---

## Acceptance Criteria

- The thumbnail viewport scrolls vertically when its content exceeds the available height.
- The vertical scrollbar remains hidden even when thumbnails overflow; touch and mouse-wheel scrolling still work.
- Mouse-wheel scrolling works while the pointer is over the thumbnail viewport.
- A touchscreen swipe may start on empty panel space or directly on a thumbnail.
- A swipe scrolls without raising `ThumbnailSelected` or changing the center content.
- A stationary tap/click raises `ThumbnailSelected` once and loads that thumbnail through the existing `MainWindow.Content.partial.cs` subscription.
- Horizontal scrolling and horizontal panning are disabled.
- No custom touch-distance state machine or `MainWindow` changes are introduced.
- An interactive Windows smoke harness reports a WPF touch swipe beginning over a thumbnail and proves that the vertical offset changes without selection.
- The same harness proves that a stationary synthetic touch routes one down/up pair without scrolling, then invokes the exact hit-tested thumbnail button and proves completed activation selects exactly once.
- The harness states that custom WPF touch does not exercise Windows touch-to-mouse promotion; final gallery acceptance still requires the physical touchscreen.

## Progress

- Automated red-green coverage is complete; the focused tests, full test project, and `scripts/verify.ps1` pass.
- The WPF app launches with the new view, but the overflowing thumbnail fixture could not be opened reliably through automated map input.
- The original `InjectTouchInput` experiment is recorded below: this Parallels/Windows ARM64 environment rejects the first valid in-bounds contact with Win32 error 87, including through an independent Python `ctypes` probe.
- The replacement WPF `TouchDevice` harness passes: a swipe over a thumbnail scrolls without selection, a stationary touch routes down/up without scrolling, and UI Automation activation of the exact hit-tested thumbnail selects once.
- Mouse interaction with the overflowing panel and real touchscreen tap-versus-swipe arbitration remain open. Keep this plan active until those checks pass.

## File Map

- Create `Tests/ThumbnailBrowserWindowTests.cs`: structural regression tests for scroll configuration and completed-click wiring.
- Modify `Views/ThumbnailBrowserWindow.xaml`: bounded vertical `ScrollViewer` plus button-based thumbnail activation.
- Modify `Views/ThumbnailBrowserWindow.xaml.cs`: change the selection handler from press-time mouse input to completed routed clicks.
- Create `Tools/ThumbnailTouchSmoke/ThumbnailTouchSmoke.csproj`: Windows-only WPF smoke-harness project.
- Create `Tools/ThumbnailTouchSmoke/TouchGestureBuilder.cs`: pure touch-frame construction for unit testing.
- Create `Tools/ThumbnailTouchSmoke/SyntheticTouchDevice.cs`: WPF routed-touch and manipulation driver.
- Create `Tools/ThumbnailTouchSmoke/Program.cs`: visible STA test host, assertions, and process exit result.
- Create `Tests/TouchInputSmokeTests.cs`: regression tests for down/move/up frame construction and coordinate conversion.
- Create `scripts/verify_thumbnail_touch.ps1`: explicit interactive entry point; not part of headless `verify.ps1`.
- Modify `Tests/InteractiveWorldMap.Tests.csproj`: reference the smoke tool for pure gesture tests.
- Modify `InteractiveWorldMap.sln`: build the smoke tool with the solution.
- Modify `docs/TO_DO.md`: keep the active item concise and linked while work is in progress; remove it when all acceptance criteria are complete.
- Modify `docs/exec-plans/active/README.md`: register this active plan, then move it to Recently completed when archived.
- Modify `CHANGELOG.md`: record the completed user-visible interaction under `[Unreleased]`.

## Task 1: Lock the Scroll and Activation Contract with Failing Tests

**Files:**
- Create: `Tests/ThumbnailBrowserWindowTests.cs`
- Inspect: `Views/ThumbnailBrowserWindow.xaml`
- Inspect: `Views/ThumbnailBrowserWindow.xaml.cs`

- [x] **Step 1: Add structural tests for vertical panning and automatic overflow**

Create `Tests/ThumbnailBrowserWindowTests.cs`:

```csharp
using System.IO;
using System.Xml.Linq;
using Xunit;

namespace InteractiveWorldMap.Tests;

public class ThumbnailBrowserWindowTests
{
    private static string RepoRoot =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));

    private static XDocument LoadView() =>
        XDocument.Load(Path.Combine(RepoRoot, "Views", "ThumbnailBrowserWindow.xaml"));

    [Fact]
    public void ThumbnailViewport_UsesAutomaticVerticalTouchScrolling()
    {
        var scrollViewer = LoadView()
            .Descendants()
            .Single(element =>
                element.Name.LocalName == "ScrollViewer" &&
                (string?)element.Attribute(XName.Get("Name", "http://schemas.microsoft.com/winfx/2006/xaml")) ==
                    "ThumbnailScrollViewer");

        Assert.Equal("Hidden", (string?)scrollViewer.Attribute("VerticalScrollBarVisibility"));
        Assert.Equal("Disabled", (string?)scrollViewer.Attribute("HorizontalScrollBarVisibility"));
        Assert.Equal("VerticalFirst", (string?)scrollViewer.Attribute("PanningMode"));
        Assert.Equal("Transparent", (string?)scrollViewer.Attribute("Background"));
        Assert.Contains(
            scrollViewer.Descendants(),
            element =>
                element.Name.LocalName == "ItemsControl" &&
                (string?)element.Attribute(XName.Get("Name", "http://schemas.microsoft.com/winfx/2006/xaml")) ==
                    "ThumbnailList");
    }

    [Fact]
    public void ThumbnailItems_UseCompletedClickInsteadOfPressTimeSelection()
    {
        var document = LoadView();
        var thumbnailButton = document
            .Descendants()
            .Single(element =>
                element.Name.LocalName == "Button" &&
                (string?)element.Attribute("Click") == "Thumbnail_Click");

        Assert.Equal("False", (string?)thumbnailButton.Attribute("Focusable"));
        Assert.Contains(
            thumbnailButton.Descendants(),
            element => element.Name.LocalName == "ControlTemplate");
        Assert.DoesNotContain(
            document.Descendants().Attributes(),
            attribute => attribute.Name.LocalName == "MouseLeftButtonDown");

        var source = File.ReadAllText(
            Path.Combine(RepoRoot, "Views", "ThumbnailBrowserWindow.xaml.cs"));

        Assert.Contains(
            "private void Thumbnail_Click(object sender, RoutedEventArgs e)",
            source);
        Assert.Contains("sender is System.Windows.Controls.Button button", source);
        Assert.Contains("button.DataContext is ThumbnailItem item", source);
    }
}
```

- [x] **Step 2: Run the focused tests and confirm the expected red state**

Run:

```powershell
dotnet test Tests/InteractiveWorldMap.Tests.csproj --filter FullyQualifiedName~ThumbnailBrowserWindowTests
```

Expected: both tests fail because `ThumbnailScrollViewer`, the thumbnail `Button`, and the `RoutedEventArgs` handler do not exist yet.

- [ ] **Step 3: Commit the failing contract tests**

```powershell
git add Tests/ThumbnailBrowserWindowTests.cs
git commit -m "test: define thumbnail touch scrolling contract"
```

## Task 2: Implement Native Scroll and Tap Arbitration

**Files:**
- Modify: `Views/ThumbnailBrowserWindow.xaml:40-67`
- Modify: `Views/ThumbnailBrowserWindow.xaml.cs:1-5,117-123`
- Test: `Tests/ThumbnailBrowserWindowTests.cs`

- [x] **Step 1: Put the thumbnail list inside a hit-testable vertical scroll viewport**

Replace the existing `ThumbnailList` block in `Views/ThumbnailBrowserWindow.xaml` with:

```xml
<ScrollViewer x:Name="ThumbnailScrollViewer"
              Grid.Row="1"
              Background="Transparent"
              VerticalScrollBarVisibility="Hidden"
              HorizontalScrollBarVisibility="Disabled"
              PanningMode="VerticalFirst">
    <ItemsControl x:Name="ThumbnailList">
        <ItemsControl.ItemTemplate>
            <DataTemplate>
                <Button Click="Thumbnail_Click"
                        Focusable="False"
                        Cursor="Hand"
                        Background="Transparent"
                        BorderThickness="0"
                        Padding="0"
                        HorizontalContentAlignment="Center"
                        VerticalContentAlignment="Center">
                    <Button.Template>
                        <ControlTemplate TargetType="{x:Type Button}">
                            <ContentPresenter
                                HorizontalAlignment="{TemplateBinding HorizontalContentAlignment}"
                                VerticalAlignment="{TemplateBinding VerticalContentAlignment}"/>
                        </ControlTemplate>
                    </Button.Template>
                    <Border x:Name="ThumbnailBorder"
                            Margin="0,0,0,8"
                            BorderThickness="2"
                            BorderBrush="Transparent"
                            CornerRadius="8"
                            Background="#33000000"
                            Width="140"
                            Height="80">
                        <Viewbox Stretch="Uniform">
                            <Image Source="{Binding Thumbnail}"
                                   RenderOptions.BitmapScalingMode="HighQuality"/>
                        </Viewbox>
                    </Border>
                </Button>
                <DataTemplate.Triggers>
                    <DataTrigger Binding="{Binding IsSelected}" Value="True">
                        <Setter TargetName="ThumbnailBorder" Property="BorderBrush" Value="#FF2196F3"/>
                        <Setter TargetName="ThumbnailBorder" Property="Background" Value="#662196F3"/>
                    </DataTrigger>
                </DataTemplate.Triggers>
            </DataTemplate>
        </ItemsControl.ItemTemplate>
    </ItemsControl>
</ScrollViewer>
```

`Background="Transparent"` makes otherwise empty viewport pixels participate in touch hit testing. `PanningMode="VerticalFirst"` gives a predominantly vertical gesture to the `ScrollViewer`; the button receives a completed click only when the gesture remains a tap.

- [x] **Step 2: Convert selection from press-time mouse input to completed clicks**

Remove:

```csharp
using System.Windows.Input;
```

Replace `Thumbnail_Click` in `Views/ThumbnailBrowserWindow.xaml.cs` with:

```csharp
private void Thumbnail_Click(object sender, RoutedEventArgs e)
{
    if (sender is System.Windows.Controls.Button button &&
        button.DataContext is ThumbnailItem item)
    {
        ThumbnailSelected?.Invoke(this, item.Index);
    }
}
```

Do not add `TouchDown`, `TouchUp`, manipulation handlers, or a manual movement threshold. The `ScrollViewer` and `Button` must remain the sole gesture owners.

- [x] **Step 3: Run the focused tests and confirm the green state**

Run:

```powershell
dotnet test Tests/InteractiveWorldMap.Tests.csproj --filter FullyQualifiedName~ThumbnailBrowserWindowTests
```

Expected: 2 passed, 0 failed.

- [x] **Step 4: Run the complete test project**

Run:

```powershell
dotnet test Tests/InteractiveWorldMap.Tests.csproj
```

Expected: all tests pass with no new warnings or failures.

- [ ] **Step 5: Commit the implementation**

```powershell
git add Views/ThumbnailBrowserWindow.xaml Views/ThumbnailBrowserWindow.xaml.cs
git commit -m "feat: add touch scrolling to thumbnail browser"
```

## Task 3: Investigate OS-Level Touch Injection

This experiment is complete but not the final harness architecture. The current Parallels/Windows ARM64 environment rejects valid `InjectTouchInput` frames, so the temporary native adapter described in this task was removed. Task 4 implements the environment-independent WPF replacement.

**Files:**
- Create: `Tools/ThumbnailTouchSmoke/ThumbnailTouchSmoke.csproj`
- Create: `Tools/ThumbnailTouchSmoke/TouchGestureBuilder.cs`
- Create: `Tools/ThumbnailTouchSmoke/NativeTouchInjector.cs`
- Create: `Tools/ThumbnailTouchSmoke/Program.cs`
- Create: `Tests/TouchInputSmokeTests.cs`
- Create: `scripts/verify_thumbnail_touch.ps1`
- Modify: `Tests/InteractiveWorldMap.Tests.csproj`
- Modify: `InteractiveWorldMap.sln`

Implementation reference: use the current Win32 [`InjectTouchInput`](https://learn.microsoft.com/windows/win32/api/winuser/nf-winuser-injecttouchinput) contract with [`POINTER_TOUCH_INFO`](https://learn.microsoft.com/windows/win32/api/winuser/ns-winuser-pointer_touch_info), not the older `TOUCHINPUT` structure used to read `WM_TOUCH`.

- [x] **Step 1: Add failing tests for deterministic touch-frame construction**

Create `Tests/TouchInputSmokeTests.cs` with tests that call a pure builder rather than invoking desktop input:

```csharp
using InteractiveWorldMap.Tools.ThumbnailTouchSmoke;
using Xunit;

namespace InteractiveWorldMap.Tests;

public class TouchInputSmokeTests
{
    [Fact]
    public void BuildSwipe_ProducesDownMovesAndUpInScreenPixels()
    {
        var frames = TouchGestureBuilder.BuildSwipe(
            startX: 240,
            startY: 420,
            endX: 240,
            endY: 220,
            moveCount: 4);

        Assert.Equal(6, frames.Count);
        Assert.Equal(TouchContactPhase.Down, frames[0].Phase);
        Assert.All(frames.Skip(1).Take(4), frame =>
            Assert.Equal(TouchContactPhase.Move, frame.Phase));
        Assert.Equal(TouchContactPhase.Up, frames[^1].Phase);
        Assert.Equal(240, frames[0].X);
        Assert.Equal(420, frames[0].Y);
        Assert.Equal(240, frames[^1].X);
        Assert.Equal(220, frames[^1].Y);
        Assert.Equal(frames[^2].X, frames[^1].X);
        Assert.Equal(frames[^2].Y, frames[^1].Y);
    }

    [Fact]
    public void BuildTap_ProducesStationaryDownAndUp()
    {
        var frames = TouchGestureBuilder.BuildTap(125, 310);

        Assert.Collection(
            frames,
            down =>
            {
                Assert.Equal(TouchContactPhase.Down, down.Phase);
                Assert.Equal((125, 310), (down.X, down.Y));
            },
            up =>
            {
                Assert.Equal(TouchContactPhase.Up, up.Phase);
                Assert.Equal((125, 310), (up.X, up.Y));
            });
    }
}
```

Add a project reference to `Tests/InteractiveWorldMap.Tests.csproj`:

```xml
<ProjectReference Include="..\Tools\ThumbnailTouchSmoke\ThumbnailTouchSmoke.csproj" />
```

Run:

```powershell
dotnet test Tests/InteractiveWorldMap.Tests.csproj --filter FullyQualifiedName~TouchInputSmokeTests
```

Expected: compilation fails because the smoke project and gesture types do not exist.

- [x] **Step 2: Create the smoke project and pure gesture builder**

Create `Tools/ThumbnailTouchSmoke/ThumbnailTouchSmoke.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net6.0-windows</TargetFramework>
    <UseWPF>true</UseWPF>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="..\..\InteractiveWorldMap.csproj" />
  </ItemGroup>
</Project>
```

Create `Tools/ThumbnailTouchSmoke/TouchGestureBuilder.cs`:

```csharp
namespace InteractiveWorldMap.Tools.ThumbnailTouchSmoke;

public enum TouchContactPhase
{
    Down,
    Move,
    Up
}

public readonly record struct TouchContactFrame(
    int X,
    int Y,
    TouchContactPhase Phase);

public static class TouchGestureBuilder
{
    public static IReadOnlyList<TouchContactFrame> BuildTap(int x, int y) =>
        new[]
        {
            Frame(x, y, TouchContactPhase.Down),
            Frame(x, y, TouchContactPhase.Up)
        };

    public static IReadOnlyList<TouchContactFrame> BuildSwipe(
        int startX,
        int startY,
        int endX,
        int endY,
        int moveCount)
    {
        if (moveCount < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(moveCount));
        }

        var frames = new List<TouchContactFrame>(moveCount + 2)
        {
            Frame(startX, startY, TouchContactPhase.Down)
        };

        for (var index = 1; index <= moveCount; index++)
        {
            var progress = index / (double)moveCount;
            frames.Add(Frame(
                (int)Math.Round(startX + ((endX - startX) * progress)),
                (int)Math.Round(startY + ((endY - startY) * progress)),
                TouchContactPhase.Move));
        }

        frames.Add(Frame(endX, endY, TouchContactPhase.Up));
        return frames;
    }

    private static TouchContactFrame Frame(
        int x,
        int y,
        TouchContactPhase phase) =>
        new(x, y, phase);
}
```

Add the project to `InteractiveWorldMap.sln` with a new GUID and Debug/Release `Any CPU` configuration entries, following the existing tool-project pattern.

- [x] **Step 3: Verify the pure gesture tests pass**

Run:

```powershell
dotnet test Tests/InteractiveWorldMap.Tests.csproj --filter FullyQualifiedName~TouchInputSmokeTests
```

Expected: 2 passed, 0 failed.

- [x] **Step 4: Add the narrow Win32 injection adapter**

Create `Tools/ThumbnailTouchSmoke/NativeTouchInjector.cs`:

```csharp
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace InteractiveWorldMap.Tools.ThumbnailTouchSmoke;

public sealed class NativeTouchInjector
{
    private const uint TouchFeedbackDefault = 0x1;
    private const uint PointerTypeTouch = 0x00000002;
    private const uint PointerFlagInRange = 0x00000002;
    private const uint PointerFlagInContact = 0x00000004;
    private const uint PointerFlagDown = 0x00010000;
    private const uint PointerFlagUpdate = 0x00020000;
    private const uint PointerFlagUp = 0x00040000;
    private const uint TouchMaskContactArea = 0x00000001;
    private const int ContactRadius = 4;
    private const uint ContactId = 1;

    public NativeTouchInjector()
    {
        if (!InitializeTouchInjection(1, TouchFeedbackDefault))
        {
            throw LastError("InitializeTouchInjection");
        }
    }

    public void Inject(IReadOnlyList<TouchContactFrame> frames)
    {
        foreach (var frame in frames)
        {
            var contact = CreateContact(frame);

            if (!InjectTouchInput(1, new[] { contact }))
            {
                throw LastError("InjectTouchInput");
            }

            if (frame.Phase != TouchContactPhase.Up)
            {
                Thread.Sleep(16);
            }
        }
    }

    private static PointerTouchInfo CreateContact(TouchContactFrame frame)
    {
        var point = new NativePoint { X = frame.X, Y = frame.Y };
        return new PointerTouchInfo
        {
            PointerInfo = new PointerInfo
            {
                PointerType = PointerTypeTouch,
                PointerId = ContactId,
                PointerFlags = FlagsFor(frame.Phase),
                PixelLocation = point,
                PixelLocationRaw = point
            },
            TouchMask = TouchMaskContactArea,
            Contact = new NativeRect
            {
                Left = frame.X - ContactRadius,
                Top = frame.Y - ContactRadius,
                Right = frame.X + ContactRadius,
                Bottom = frame.Y + ContactRadius
            }
        };
    }

    private static uint FlagsFor(TouchContactPhase phase) =>
        phase switch
        {
            TouchContactPhase.Down =>
                PointerFlagInRange | PointerFlagInContact | PointerFlagDown,
            TouchContactPhase.Move =>
                PointerFlagInRange | PointerFlagInContact | PointerFlagUpdate,
            TouchContactPhase.Up => PointerFlagUp,
            _ => throw new ArgumentOutOfRangeException(nameof(phase))
        };

    private static Win32Exception LastError(string operation) =>
        new(Marshal.GetLastWin32Error(), $"{operation} failed");

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool InitializeTouchInjection(
        uint maxCount,
        uint feedbackMode);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool InjectTouchInput(
        uint count,
        [In] PointerTouchInfo[] contacts);

    [StructLayout(LayoutKind.Sequential)]
    private struct PointerTouchInfo
    {
        public PointerInfo PointerInfo;
        public uint TouchFlags;
        public uint TouchMask;
        public NativeRect Contact;
        public NativeRect ContactRaw;
        public uint Orientation;
        public uint Pressure;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PointerInfo
    {
        public uint PointerType;
        public uint PointerId;
        public uint FrameId;
        public uint PointerFlags;
        public IntPtr SourceDevice;
        public IntPtr TargetWindow;
        public NativePoint PixelLocation;
        public NativePoint HimetricLocation;
        public NativePoint PixelLocationRaw;
        public NativePoint HimetricLocationRaw;
        public uint Time;
        public uint HistoryCount;
        public int InputData;
        public uint KeyStates;
        public ulong PerformanceCount;
        public uint ButtonChangeType;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }
}
```

Keep all P/Invoke structures, flags, and native calls in this file. `InjectTouchInput` takes `POINTER_TOUCH_INFO` records in physical screen pixels. The final `Up` frame must use the same pixel location as the preceding `Update` frame or Windows rejects the sequence.

- [x] **Step 5: Build the visible STA smoke host with outcome assertions**

Create `Tools/ThumbnailTouchSmoke/Program.cs`:

```csharp
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using InteractiveWorldMap.Views;

namespace InteractiveWorldMap.Tools.ThumbnailTouchSmoke;

internal static class Program
{
    private static int _exitCode = 1;

    [STAThread]
    private static int Main()
    {
        if (!Environment.UserInteractive)
        {
            Console.Error.WriteLine(
                "Synthetic touch requires an unlocked interactive Windows desktop.");
            return 2;
        }

        var application = new Application
        {
            ShutdownMode = ShutdownMode.OnExplicitShutdown
        };

        application.Startup += async (_, _) =>
        {
            _exitCode = await RunSmokeAsync(application);
            application.Shutdown(_exitCode);
        };

        application.Run();
        return _exitCode;
    }

    private static async Task<int> RunSmokeAsync(Application application)
    {
        ThumbnailBrowserWindow? window = null;

        try
        {
            var selectionCount = 0;
            window = new ThumbnailBrowserWindow
            {
                Height = 360,
                Left = Math.Max(0, (SystemParameters.PrimaryScreenWidth - 180) / 2),
                Top = Math.Max(0, (SystemParameters.PrimaryScreenHeight - 360) / 2),
                Opacity = 1
            };
            window.ThumbnailSelected += (_, _) => selectionCount++;
            window.LoadThumbnails(CreateThumbnails(12), selectedIndex: 0);
            window.Show();
            window.Activate();

            await application.Dispatcher.InvokeAsync(
                window.UpdateLayout,
                DispatcherPriority.Loaded);
            await Task.Delay(200);

            var scrollViewer =
                (ScrollViewer?)window.FindName("ThumbnailScrollViewer")
                ?? throw new InvalidOperationException(
                    "ThumbnailScrollViewer was not found.");

            var swipeStart = ToScreenPixel(
                scrollViewer,
                new Point(
                    scrollViewer.ActualWidth / 2,
                    Math.Min(scrollViewer.ActualHeight - 30, 180)));
            var swipeEnd = (
                X: swipeStart.X,
                Y: Math.Max(0, swipeStart.Y - 140));
            var offsetBefore = scrollViewer.VerticalOffset;

            var injector = new NativeTouchInjector();
            await Task.Run(() => injector.Inject(
                TouchGestureBuilder.BuildSwipe(
                    swipeStart.X,
                    swipeStart.Y,
                    swipeEnd.X,
                    swipeEnd.Y,
                    moveCount: 12)));
            await Task.Delay(600);

            var offsetAfter = scrollViewer.VerticalOffset;
            if (offsetAfter <= offsetBefore + 1 || selectionCount != 0)
            {
                Console.Error.WriteLine(
                    $"Swipe failed: offset {offsetBefore:F1} -> {offsetAfter:F1}; " +
                    $"selections = {selectionCount}");
                return 1;
            }

            Console.WriteLine(
                $"Swipe: offset {offsetBefore:F1} -> {offsetAfter:F1}; " +
                "selections = 0");

            scrollViewer.ScrollToTop();
            window.UpdateLayout();
            selectionCount = 0;

            var tapPoint = ToScreenPixel(
                scrollViewer,
                new Point(scrollViewer.ActualWidth / 2, 40));
            await Task.Run(() => injector.Inject(
                TouchGestureBuilder.BuildTap(tapPoint.X, tapPoint.Y)));
            await Task.Delay(300);

            if (selectionCount != 1)
            {
                Console.Error.WriteLine(
                    $"Tap failed: selections = {selectionCount}");
                return 1;
            }

            Console.WriteLine("Tap: selections = 1");
            return 0;
        }
        catch (Win32Exception exception)
        {
            Console.Error.WriteLine(
                "Touch injection failed. Run from an unlocked interactive " +
                $"Windows desktop. {exception.Message}");
            return 2;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception);
            return 1;
        }
        finally
        {
            window?.Close();
        }
    }

    private static (int X, int Y) ToScreenPixel(
        FrameworkElement element,
        Point point)
    {
        var screenPoint = element.PointToScreen(point);
        return (
            (int)Math.Round(screenPoint.X),
            (int)Math.Round(screenPoint.Y));
    }

    private static BitmapImage[] CreateThumbnails(int count)
    {
        var pixels = new byte[80 * 50 * 4];
        for (var index = 0; index < pixels.Length; index += 4)
        {
            pixels[index] = 0x65;
            pixels[index + 1] = 0x96;
            pixels[index + 2] = 0x21;
            pixels[index + 3] = 0xFF;
        }

        var source = BitmapSource.Create(
            80,
            50,
            96,
            96,
            PixelFormats.Bgra32,
            palette: null,
            pixels,
            stride: 80 * 4);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(source));

        using var stream = new MemoryStream();
        encoder.Save(stream);
        var pngBytes = stream.ToArray();

        return Enumerable.Range(0, count)
            .Select(_ => LoadBitmap(pngBytes))
            .ToArray();
    }

    private static BitmapImage LoadBitmap(byte[] pngBytes)
    {
        using var stream = new MemoryStream(pngBytes);
        var image = new BitmapImage();
        image.BeginInit();
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.StreamSource = stream;
        image.EndInit();
        image.Freeze();
        return image;
    }
}
```

The host calls injection on a background task so the WPF dispatcher remains free to process the synthetic touch frames. It uses `PointToScreen` rather than hard-coded desktop coordinates and exits 0 only when both behavioral assertions pass. It must not modify `VisualConfig`, production startup, `ThumbnailBrowserWindow`, or the Tuning panel.

- [x] **Step 6: Add an explicit interactive runner**

Create `scripts/verify_thumbnail_touch.ps1`:

```powershell
$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent $PSScriptRoot
Set-Location $Root

Write-Host "=== Thumbnail Synthetic Touch Smoke Check ==="
dotnet run --project Tools/ThumbnailTouchSmoke/ThumbnailTouchSmoke.csproj --configuration Release
if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}

Write-Host "=== Synthetic touch behavior PASSED ==="
```

Do not call this script from `scripts/verify.ps1`: CI and headless sessions do not provide a suitable interactive input desktop. Building the tool through `InteractiveWorldMap.sln` and testing `TouchGestureBuilder` remain part of the normal verification gate.

- [x] **Step 7: Run the synthetic touch smoke check and record the environmental failure**

Run from an unlocked local Windows desktop:

```powershell
.\scripts\verify_thumbnail_touch.ps1
```

The intended output was:

```text
Swipe: offset increased; selections = 0
Tap: selections = 1
=== Synthetic touch behavior PASSED ===
```

Windows rejected injection, so the exact evidence is recorded below rather than treating mouse input as equivalent evidence.

Current environment result (2026-06-29): `InitializeTouchInjection` succeeds, but the first `InjectTouchInput` down frame returns `ERROR_INVALID_PARAMETER` (87). Windows reports `SM_DIGITIZER=201` and `SM_MAXIMUMTOUCHES=2`. The installed Windows SDK layout is 96 bytes for `POINTER_INFO` and 144 bytes for `POINTER_TOUCH_INFO`, matching the harness. An independent Python `ctypes` probe using those sizes and an in-bounds `(500,500)` contact fails identically, isolating the limitation to the current Parallels/Windows ARM64 input environment.

- [x] **Step 8: Remove the blocked native adapter before implementing the WPF replacement**

```powershell
Remove-Item Tools/ThumbnailTouchSmoke/NativeTouchInjector.cs
```

Expected: no unused or misleading native injection path remains in the tool.

## Task 4: Verify Through WPF Routed Touch and Manipulation

**Files:**
- Create: `Tools/ThumbnailTouchSmoke/SyntheticTouchDevice.cs`
- Modify: `Tools/ThumbnailTouchSmoke/Program.cs`
- Modify: `Tests/TouchInputSmokeTests.cs`
- Test: `scripts/verify_thumbnail_touch.ps1`

- [x] **Step 1: Add a failing type-contract test**

Add a focused assertion proving that the harness device derives from WPF's real touch abstraction:

```csharp
[Fact]
public void SyntheticTouchDevice_UsesWpfTouchInputPipeline()
{
    Assert.True(typeof(SyntheticTouchDevice).IsSubclassOf(typeof(TouchDevice)));
}
```

Run:

```powershell
dotnet test Tests/InteractiveWorldMap.Tests.csproj --filter FullyQualifiedName~TouchInputSmokeTests
```

Observed red state: compilation failed because `SyntheticTouchDevice` did not exist.

- [x] **Step 2: Implement the WPF touch device**

Create `SyntheticTouchDevice` as a focused `TouchDevice` subclass that:

1. Sets the active `PresentationSource`.
2. Accepts the existing down/move/up frames in root-visual coordinates.
3. Calls `Activate`/`ReportDown`, `ReportMove`, and `ReportUp`/`Deactivate` on the WPF dispatcher.
4. Returns touch points translated from the root visual to the requested `UIElement`.
5. Uses an 8-by-8 device-independent contact rectangle.
6. Delays 16 milliseconds between frames so manipulation processing can observe the movement.

- [x] **Step 3: Replace native injection in the smoke host**

Update `Program.cs` so it:

1. Converts points from `ThumbnailScrollViewer` coordinates to root-visual coordinates with `TranslatePoint`.
2. Sends the upward swipe through `SyntheticTouchDevice`.
3. Proves `VerticalOffset` increases and `ThumbnailSelected` remains at zero.
4. Sends a stationary touch over a visible thumbnail and proves one routed down/up pair, no offset change, and no selection.
5. Finds the exact hit-tested ancestor `Button`, invokes it through `ButtonAutomationPeer` / `IInvokeProvider`, and proves selection fires exactly once.
6. Prints that custom WPF touch does not perform the OS touch-to-mouse promotion step.

- [x] **Step 4: Run the focused tests**

Run:

```powershell
dotnet test Tests/InteractiveWorldMap.Tests.csproj --filter FullyQualifiedName~TouchInputSmokeTests
```

Observed: 3 passed, 0 failed.

- [x] **Step 5: Run the interactive WPF smoke harness**

Run:

```powershell
.\scripts\verify_thumbnail_touch.ps1
```

Observed:

```text
Swipe: offset 0.0 -> 222.0; selections = 0
Touch tap: routed down/up once; offset unchanged; no synthetic mouse promotion
Button activation: selections = 1
=== Synthetic touch behavior PASSED ===
```

This verifies WPF routing, manipulation, hit testing, and completed button activation independently. Only a physical touchscreen can verify that the Windows touch/stylus promotion layer connects a stationary hardware tap to `Button.Click`.

## Task 5: Verify Real Input Behavior and Finish Bookkeeping

**Files:**
- Modify: `docs/TO_DO.md`
- Modify: `docs/exec-plans/active/README.md`
- Modify: `CHANGELOG.md`
- Move: `docs/exec-plans/active/touch-scrollable-thumbnail-browser-plan.md` to `docs/exec-plans/completed/touch-scrollable-thumbnail-browser-plan.md`

- [ ] **Step 1: Launch an overflowing thumbnail set**

Run:

```powershell
dotnet run --project InteractiveWorldMap.csproj
```

Open a location containing enough images to exceed the right-side thumbnail window. Confirm the panel size remains stable and no scrollbar appears.

- [ ] **Step 2: Verify mouse input**

With the pointer over the thumbnail viewport:

1. Use the mouse wheel to reach the last thumbnail.
2. Use the mouse wheel to return toward the first thumbnail.
3. Click a thumbnail and confirm the center content window loads that image exactly once.

Expected: scrolling does not select items; clicking selects one item.

- [ ] **Step 3: Verify physical touchscreen tap-versus-swipe arbitration**

On a Windows touchscreen:

1. Swipe upward starting on empty viewport space.
2. Swipe upward starting directly on a thumbnail.
3. Swipe downward directly on a thumbnail.
4. Make a stationary tap on a thumbnail.

Expected: each swipe scrolls without changing the center content; the stationary tap changes the center content exactly once. Passing `scripts/verify_thumbnail_touch.ps1` increases confidence but does not satisfy this physical-device step.

- [x] **Step 4: Run the full Windows verification gate**

Run:

```powershell
.\scripts\verify.ps1
```

Expected: build, tests, vulnerability scan, documentation checks, taste checks, manual-layout seed verification, and headless startup all pass.

- [ ] **Step 5: Complete the documentation state**

After all acceptance criteria, including real touch verification, pass:

1. Remove the thumbnail scrolling bullet from `docs/TO_DO.md`.
2. Add under `[Unreleased]` in `CHANGELOG.md`:

```markdown
- Made the thumbnail browser vertically scrollable by touch and mouse wheel with its scrollbar hidden, while preventing swipe gestures from selecting thumbnails.
```

3. Check every plan checkbox and change front matter to:

```yaml
status: completed
completed: 2026-06-29
```

4. Move this plan to `docs/exec-plans/completed/touch-scrollable-thumbnail-browser-plan.md`.
5. Remove its active row from `docs/exec-plans/active/README.md` and add it to Recently completed.

If only the real-device touch check remains, do not archive the plan. Narrow the TO_DO bullet to:

```markdown
- [ ] Touchscreen smoke check: swiping over a thumbnail scrolls without selecting it; a stationary tap selects it. See the active `touch-scrollable-thumbnail-browser-plan.md`.
```

- [ ] **Step 6: Validate the final documentation and diff**

Run:

```powershell
py -3 scripts/doc_gardening.py
git diff --check
git status --short
```

Expected: documentation checks pass, no whitespace errors are reported, and only intended files are present in the feature diff.

- [ ] **Step 7: Commit completion bookkeeping**

```powershell
git add CHANGELOG.md docs/TO_DO.md docs/exec-plans/active/README.md docs/exec-plans/active/touch-scrollable-thumbnail-browser-plan.md docs/exec-plans/completed/touch-scrollable-thumbnail-browser-plan.md
git commit -m "docs: complete thumbnail touch scrolling plan"
```

## Modularity / File Size Impact

- `ThumbnailBrowserWindow.xaml.cs` remains responsible only for thumbnail window behavior and selection events; no input logic moves into `MainWindow`.
- The XAML gains one `ScrollViewer` and one lightweight `Button` per item; no new service, model, config option, or cross-layer dependency is needed.
- The new test file owns the complete structural contract and avoids adding unrelated assertions to broad rendering test classes.
- `Tools/ThumbnailTouchSmoke/TouchGestureBuilder.cs` owns deterministic gesture construction, `SyntheticTouchDevice.cs` owns WPF touch reporting and coordinate translation, and `Program.cs` owns only the visible smoke scenario and assertions.
- The production app does not reference the smoke project and gains no synthetic-input configuration surface.
- No touched C# file approaches the 800-line limit.

## Out of Scope

- Momentum or deceleration tuning beyond WPF defaults.
- Horizontal thumbnail layouts.
- Custom scrollbars or visual redesign of the thumbnail panel.
- Keyboard focus/navigation changes.
- Changing thumbnail loading, selected-item styling, or center-window content orchestration.
- Mouse-to-touch translation in the Tuning panel or production application.
- Treating synthetic injection as a substitute for testing the actual gallery touchscreen, display scaling, and touch driver.
