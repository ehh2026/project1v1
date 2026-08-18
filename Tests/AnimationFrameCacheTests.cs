using System;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using InteractiveWorldMap.Services;
using InteractiveWorldMap.Tests.TestHelpers;
using Xunit;

namespace InteractiveWorldMap.Tests;

public class AnimationFrameCacheTests : IDisposable
{
    private readonly string _tempDir;

    public AnimationFrameCacheTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "AnimationFrameCache_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    private static BitmapSource CreateTestBitmap()
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

        var bitmap = BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgra32, null, pixels, stride);
        bitmap.Freeze();
        return bitmap;
    }

    private static AnimationFrameCache CreateCache(string dir) =>
        new AnimationFrameCache(new MockLogger(), dir);

    [Fact]
    public void Constructor_CreatesDirectory()
    {
        var newDir = Path.Combine(_tempDir, "sub_" + Guid.NewGuid().ToString("N"));
        Assert.False(Directory.Exists(newDir));

        var _ = CreateCache(newDir);

        Assert.True(Directory.Exists(newDir));
    }

    [Fact]
    public void Constructor_WritesCacheVersionFile()
    {
        var cache = CreateCache(_tempDir);

        var versionFile = Path.Combine(_tempDir, "cache_version.txt");
        Assert.True(File.Exists(versionFile));
        var content = File.ReadAllText(versionFile);
        Assert.False(string.IsNullOrWhiteSpace(content));
    }

    [Fact]
    public void TryLoadFrame_Miss_ReturnsNull()
    {
        var cache = CreateCache(_tempDir);

        var result = cache.TryLoadFrame(0, 0, 100, 100, 50, 50, 200, 200, 800, 600, 3);

        Assert.Null(result);
    }

    [Fact]
    public void SaveFrame_ThenTryLoadFrame_ReturnsFrozenBitmap()
    {
        var cache = CreateCache(_tempDir);
        var bitmap = CreateTestBitmap();

        cache.SaveFrame(bitmap, 0, 0, 100, 100, 50, 50, 200, 200, 800, 600, 3);
        var loaded = cache.TryLoadFrame(0, 0, 100, 100, 50, 50, 200, 200, 800, 600, 3);

        Assert.NotNull(loaded);
        Assert.True(loaded!.IsFrozen);
    }

    [Fact]
    public void ClearCache_RemovesContents_AndRecreatesDirectory()
    {
        var cache = CreateCache(_tempDir);
        var bitmap = CreateTestBitmap();
        cache.SaveFrame(bitmap, 0, 0, 100, 100, 50, 50, 200, 200, 800, 600, 3);

        var pngExistsBefore = Directory.GetFiles(_tempDir, "*.png").Length > 0;
        Assert.True(pngExistsBefore);

        cache.ClearCache();

        Assert.True(Directory.Exists(_tempDir));
        var pngFiles = Directory.GetFiles(_tempDir, "*.png");
        Assert.Empty(pngFiles);
    }

    [Fact]
    public void VersionMismatch_ClearsPngFiles_AndPreservesNonPngFiles()
    {
        File.WriteAllText(Path.Combine(_tempDir, "cache_version.txt"), "1");
        File.WriteAllText(Path.Combine(_tempDir, "frame.png"), "fake-png");
        File.WriteAllText(Path.Combine(_tempDir, "notes.txt"), "keep-me");

        var cache = CreateCache(_tempDir);

        var pngFiles = Directory.GetFiles(_tempDir, "*.png");
        Assert.Empty(pngFiles);
        Assert.True(File.Exists(Path.Combine(_tempDir, "notes.txt")));
        Assert.Equal("keep-me", File.ReadAllText(Path.Combine(_tempDir, "notes.txt")));
        Assert.True(File.Exists(Path.Combine(_tempDir, "cache_version.txt")));
    }

    [Fact]
    public void MissingVersion_ClearsExistingPng_AndWritesVersion()
    {
        File.WriteAllText(Path.Combine(_tempDir, "oldframe.png"), "fake-png");

        var cache = CreateCache(_tempDir);

        var pngFiles = Directory.GetFiles(_tempDir, "*.png");
        Assert.Empty(pngFiles);
        Assert.True(File.Exists(Path.Combine(_tempDir, "cache_version.txt")));
    }

    [Fact]
    public void CorruptedCachedFile_ReturnsNull()
    {
        var cache = CreateCache(_tempDir);
        var bitmap = CreateTestBitmap();
        cache.SaveFrame(bitmap, 0, 0, 100, 100, 50, 50, 200, 200, 800, 600, 3);

        var pngFiles = Directory.GetFiles(_tempDir, "*.png");
        Assert.Single(pngFiles);
        File.WriteAllBytes(pngFiles[0], new byte[] { 0x00, 0x01, 0x02, 0x03 });

        var result = cache.TryLoadFrame(0, 0, 100, 100, 50, 50, 200, 200, 800, 600, 3);

        Assert.Null(result);

        // Release the file lock held by the abandoned BitmapImage (URI source)
        // so the temp directory can be cleaned up in Dispose.
        GC.Collect();
        GC.WaitForPendingFinalizers();
    }

    [Fact]
    public void SaveFrame_InvalidCachePath_DoesNotThrow()
    {
        var cache = CreateCache(_tempDir);
        var bitmap = CreateTestBitmap();

        Directory.Delete(_tempDir, recursive: true);

        var exception = Record.Exception(() =>
            cache.SaveFrame(bitmap, 0, 0, 100, 100, 50, 50, 200, 200, 800, 600, 3));

        Assert.Null(exception);
    }
}
