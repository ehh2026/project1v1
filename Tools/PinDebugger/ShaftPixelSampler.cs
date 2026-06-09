// Bitmap pixel sampling for shaft analysis modes (find-join, fit-axis, measure-shaft).
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

/// <summary>Opaque pixel coordinates and raw buffer from a shaft PNG.</summary>
internal sealed record ShaftPixelSample(
    int Width,
    int Height,
    byte[] Pixels,
    int Stride,
    IReadOnlyList<(int X, int Y)> OpaquePixels)
{
    internal int OpaqueCount => OpaquePixels.Count;
}

internal static class ShaftPixelSampler
{
    private const byte DefaultAlphaThreshold = 10;

    /// <summary>Resolves cleaned shaft path when present, otherwise original in partsDir.</summary>
    internal static (string Path, string Label) ResolveShaftPath(
        string pinId, string baseFile, string partsDir, string cleanedDir)
    {
        var cleanedPath  = Path.Combine(cleanedDir, $"{pinId}_shaft_clean.png");
        var originalPath = Path.Combine(partsDir, baseFile);
        if (File.Exists(cleanedPath))
            return (cleanedPath, "cleaned");
        return (originalPath, "original");
    }

    internal static ShaftPixelSample? TryRead(string imagePath, byte alphaThreshold = DefaultAlphaThreshold)
    {
        if (!File.Exists(imagePath))
            return null;

        using var bmp = new Bitmap(imagePath);
        int w = bmp.Width, h = bmp.Height;
        var rect    = new Rectangle(0, 0, w, h);
        var bmpData = bmp.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        int stride  = bmpData.Stride;
        var pixels  = new byte[stride * h];
        Marshal.Copy(bmpData.Scan0, pixels, 0, pixels.Length);
        bmp.UnlockBits(bmpData);

        var opaque = new List<(int X, int Y)>(w * h / 4);
        for (int py = 0; py < h; py++)
        for (int px = 0; px < w; px++)
        {
            if (pixels[(py * stride) + (px * 4) + 3] < alphaThreshold) continue;
            opaque.Add((px, py));
        }

        return new ShaftPixelSample(w, h, pixels, stride, opaque);
    }
}
