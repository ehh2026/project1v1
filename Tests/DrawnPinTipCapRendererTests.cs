using System;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using InteractiveWorldMap.Models;
using InteractiveWorldMap.Views;
using Xunit;

namespace InteractiveWorldMap.Tests;

public class DrawnPinTipCapRendererTests
{
    [Fact]
    public void Sync_RendersOpenGeometryAsSingleUnfilledStroke()
    {
        RunOnStaThread(() =>
        {
            var canvas = new Canvas();
            var renderer = new DrawnPinTipCapRenderer(canvas);
            var cap = new DrawnPinTipCapConfig
            {
                Style = DrawnPinTipCapStyle.Concave,
                Color = "#FF111111",
                LineWeightPx = 3.0
            };

            renderer.Sync(
                new[] { new LineGeometry(new Point(0, 0), new Point(10, 0)) },
                cap,
                new PinMarkerConfig());

            var path = Assert.IsType<Path>(Assert.Single(canvas.Children));
            Assert.Null(path.Fill);
            Assert.Equal(3.0, path.StrokeThickness);
            Assert.Equal(PenLineCap.Round, path.StrokeStartLineCap);
            Assert.Equal(PenLineCap.Round, path.StrokeEndLineCap);
            Assert.Equal(Color.FromRgb(17, 17, 17), ((SolidColorBrush)path.Stroke).Color);
        });
    }

    private static void RunOnStaThread(Action action)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                failure = ex;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (failure != null)
            throw failure;
    }
}
