using System;
using System.IO;
using System.Threading;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using InteractiveWorldMap.Models;
using InteractiveWorldMap.Services;
using InteractiveWorldMap.Utilities;
using InteractiveWorldMap.Tests.TestHelpers;

namespace InteractiveWorldMap.Tests;

/// <summary>
/// Shared temp-folder and bitmap fixtures for ContentLoader test classes.
/// </summary>
internal static class ContentLoaderTestFixtures
{
    /// <summary>
    /// Loader that skips the repo Excel file so JSON-only tests stay isolated.
    /// </summary>
    internal static ContentLoader CreateLoaderForJsonTests(string contentFolderPath)
    {
        return new ContentLoader(new MockLogger(), new ContentSetResolver())
        {
            ContentFolderPath = contentFolderPath,
            ExcelCoordinateFilePath = Path.Combine(contentFolderPath, "no-excel-for-test.xlsx"),
        };
    }

    internal static string CreateContentFolderWithMap()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "iwm-cl-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        File.WriteAllText(Path.Combine(tempDir, ContentFileNames.WorldMapFileName), "fake-image-data");
        return tempDir;
    }

    internal static string CreateContentFolderWithAssets()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "iwm-assets-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        var assetsDir = Path.Combine(tempDir, ContentFileNames.AssetsFolderName);
        Directory.CreateDirectory(assetsDir);
        File.WriteAllText(Path.Combine(assetsDir, ContentFileNames.WorldMapFileName), "fake-image-data");
        return tempDir;
    }

    internal static void SafeDeleteDirectory(string path, int maxRetries = 6)
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        for (int attempt = 0; attempt < maxRetries; attempt++)
        {
            try
            {
                if (Directory.Exists(path))
                {
                    foreach (var file in Directory.GetFiles(path, "*", SearchOption.AllDirectories))
                    {
                        try { File.Delete(file); }
                        catch (IOException) { /* file still locked; retry on next pass */ }
                    }
                    Directory.Delete(path, recursive: true);
                }
                return;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                if (attempt == maxRetries - 1)
                    return;

                GC.Collect();
                GC.WaitForPendingFinalizers();
                Thread.Sleep(100 * (attempt + 1));
            }
        }
    }

    internal static void SaveTinyPng(string path, int width = 1, int height = 1)
    {
        var pixels = new byte[width * height * 4];
        for (var pixel = 3; pixel < pixels.Length; pixel += 4)
            pixels[pixel] = 255;

        var bitmap = BitmapSource.Create(
            width,
            height,
            96,
            96,
            PixelFormats.Bgra32,
            null,
            pixels,
            width * 4);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));

        using var stream = File.Create(path);
        encoder.Save(stream);
    }
}
