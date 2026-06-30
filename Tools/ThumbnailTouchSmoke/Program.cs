using System.IO;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Input;
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
            var source = PresentationSource.FromVisual(window)
                ?? throw new InvalidOperationException(
                    "The thumbnail window has no presentation source.");
            var root = source.RootVisual as UIElement
                ?? throw new InvalidOperationException(
                    "The thumbnail window has no UIElement root.");

            var swipeStart = ToRootPoint(
                scrollViewer,
                root,
                new Point(
                    scrollViewer.ActualWidth / 2,
                    Math.Min(scrollViewer.ActualHeight - 30, 180)));
            var swipeEnd = (
                X: swipeStart.X,
                Y: Math.Max(0, swipeStart.Y - 140));
            var offsetBefore = scrollViewer.VerticalOffset;
            Console.WriteLine(
                $"Swipe target: ({swipeStart.X}, {swipeStart.Y}) -> " +
                $"({swipeEnd.X}, {swipeEnd.Y})");

            var swipeDevice = new SyntheticTouchDevice(1, source);
            await swipeDevice.InjectAsync(TouchGestureBuilder.BuildSwipe(
                swipeStart.X,
                swipeStart.Y,
                swipeEnd.X,
                swipeEnd.Y,
                moveCount: 12));
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
            var touchDownCount = 0;
            var touchUpCount = 0;
            scrollViewer.AddHandler(
                UIElement.TouchDownEvent,
                new EventHandler<TouchEventArgs>((_, _) => touchDownCount++),
                handledEventsToo: true);
            scrollViewer.AddHandler(
                UIElement.TouchUpEvent,
                new EventHandler<TouchEventArgs>((_, _) => touchUpCount++),
                handledEventsToo: true);

            var tapPoint = ToRootPoint(
                scrollViewer,
                root,
                new Point(scrollViewer.ActualWidth / 2, 40));
            var tapHit = root.InputHitTest(new Point(tapPoint.X, tapPoint.Y));
            Console.WriteLine(
                $"Tap target: {tapHit?.GetType().Name ?? "<none>"} at " +
                $"({tapPoint.X}, {tapPoint.Y})");
            var tapDevice = new SyntheticTouchDevice(2, source);
            var tapOffsetBefore = scrollViewer.VerticalOffset;
            await tapDevice.InjectAsync(
                TouchGestureBuilder.BuildTap(tapPoint.X, tapPoint.Y));
            await Task.Delay(300);
            var tapOffsetAfter = scrollViewer.VerticalOffset;

            if (touchDownCount != 1 ||
                touchUpCount != 1 ||
                Math.Abs(tapOffsetAfter - tapOffsetBefore) > 1 ||
                selectionCount != 0)
            {
                Console.Error.WriteLine(
                    $"Touch tap failed: offset {tapOffsetBefore:F1} -> " +
                    $"{tapOffsetAfter:F1}; selections = {selectionCount}; " +
                    $"touch down/up = {touchDownCount}/{touchUpCount}; " +
                    "expected one routed tap without scrolling or selection");
                return 1;
            }

            Console.WriteLine(
                "Touch tap: routed down/up once; offset unchanged; " +
                "no synthetic mouse promotion");

            var thumbnailButton = FindAncestor<Button>(
                tapHit as DependencyObject)
                ?? throw new InvalidOperationException(
                    "The touch target is not inside a thumbnail button.");
            var peer = UIElementAutomationPeer.CreatePeerForElement(thumbnailButton)
                ?? new ButtonAutomationPeer(thumbnailButton);
            var invokeProvider =
                (IInvokeProvider?)peer.GetPattern(PatternInterface.Invoke)
                ?? throw new InvalidOperationException(
                    "The thumbnail button does not support UI Automation invoke.");
            invokeProvider.Invoke();
            await Task.Delay(100);

            if (selectionCount != 1)
            {
                Console.Error.WriteLine(
                    $"Button activation failed: selections = {selectionCount}");
                return 1;
            }

            Console.WriteLine("Button activation: selections = 1");
            return 0;
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

    private static (int X, int Y) ToRootPoint(
        FrameworkElement element,
        UIElement root,
        Point point)
    {
        var rootPoint = element.TranslatePoint(point, root);
        return (
            (int)Math.Round(rootPoint.X),
            (int)Math.Round(rootPoint.Y));
    }

    private static T? FindAncestor<T>(DependencyObject? current)
        where T : DependencyObject
    {
        while (current is not null)
        {
            if (current is T match)
            {
                return match;
            }

            current = VisualTreeHelper.GetParent(current);
        }

        return null;
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
