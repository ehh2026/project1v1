using System;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using InteractiveWorldMap.Services;
using InteractiveWorldMap.Tests.TestHelpers;
using Xunit;

namespace InteractiveWorldMap.Tests;

public class ZoomedRegionCacheTests
{
    [Fact]
    public void GenerateAndCacheRegion_SameSizeFullResSource_UsesOriginalSourceRect()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "iwm-zrc-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        var imagePath = Path.Combine(tempDir, "source.png");

        try
        {
            var source = CreateFourByFourTestBitmap();
            SaveBitmap(source, imagePath);

            var cache = new ZoomedRegionCache(new MockLogger(), imagePath);
            var result = cache.GenerateAndCacheRegion(
                source,
                new Int32Rect(1, 1, 2, 2),
                centerX: 2,
                centerY: 2,
                zoomLevel: 2,
                displayWidth: 2,
                displayHeight: 2);

            var firstPixel = ReadFirstPixel(result);

            Assert.Equal((byte)50, firstPixel.R);
            Assert.Equal((byte)51, firstPixel.G);
            Assert.Equal((byte)52, firstPixel.B);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    private static BitmapSource CreateFourByFourTestBitmap()
    {
        const int width = 4;
        const int height = 4;
        const int stride = width * 4;
        var pixels = new byte[height * stride];

        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var offset = (y * stride) + (x * 4);
                var value = (byte)((y * width + x) * 10);
                pixels[offset] = (byte)(value + 2);
                pixels[offset + 1] = (byte)(value + 1);
                pixels[offset + 2] = value;
                pixels[offset + 3] = 255;
            }
        }

        var bitmap = BitmapSource.Create(
            width,
            height,
            96,
            96,
            PixelFormats.Bgra32,
            null,
            pixels,
            stride);
        bitmap.Freeze();
        return bitmap;
    }

    private static void SaveBitmap(BitmapSource bitmap, string path)
    {
        using var fileStream = new FileStream(path, FileMode.Create);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        encoder.Save(fileStream);
    }

    private static (byte R, byte G, byte B) ReadFirstPixel(BitmapSource bitmap)
    {
        var converted = new FormatConvertedBitmap(bitmap, PixelFormats.Bgra32, null, 0);
        var pixels = new byte[4];
        converted.CopyPixels(new Int32Rect(0, 0, 1, 1), pixels, 4, 0);
        return (pixels[2], pixels[1], pixels[0]);
    }
}
