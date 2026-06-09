// Flood-fill shadow removal for shaft PNGs (--clean mode).
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Text.Json;

internal static class ShaftCleaner
{
    internal static void CleanShaft(JsonElement pin, string pinId, string partsDir, string outputDir, bool litSuffix)
    {
        var baseFile  = pin.GetProperty("shaft_file").GetString()!;
        var shaftFile = litSuffix ? baseFile.Replace(".png", "_lit.png") : baseFile;
        var srcPath   = Path.Combine(partsDir, shaftFile);
        if (!File.Exists(srcPath)) return;

        var shaft = pin.GetProperty("shaft");
        var tipPt = PinGeometryHelpers.ParsePoint(shaft.GetProperty("local_tip"));
        int seedX = (int)Math.Round(tipPt.X);
        int seedY = (int)Math.Round(tipPt.Y);

        using var orig    = new Bitmap(srcPath);
        using var cleaned = KeepConnectedComponent(orig, seedX, seedY);

        var outName = litSuffix ? $"{pinId}_shaft_lit_clean.png" : $"{pinId}_shaft_clean.png";
        var outPath = Path.Combine(outputDir, outName);
        cleaned.Save(outPath, ImageFormat.Png);
        Console.WriteLine($"    → {outPath}");
    }

    /// <summary>
    /// Returns a copy of <paramref name="src"/> with all pixels zeroed except those
    /// in the 8-connected non-transparent component that contains the seed pixel.
    /// </summary>
    internal static Bitmap KeepConnectedComponent(Bitmap src, int seedX, int seedY)
    {
        const int AlphaThreshold = 1;

        int w = src.Width, h = src.Height;
        var rect    = new Rectangle(0, 0, w, h);
        var srcData = src.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        int stride  = srcData.Stride;
        var pixels  = new byte[stride * h];
        Marshal.Copy(srcData.Scan0, pixels, 0, pixels.Length);
        src.UnlockBits(srcData);

        byte GetA(int x, int y) => pixels[(y * stride) + (x * 4) + 3];

        int sx = Math.Max(0, Math.Min(w - 1, seedX));
        int sy = Math.Max(0, Math.Min(h - 1, seedY));

        if (GetA(sx, sy) < AlphaThreshold)
        {
            bool found = false;
            for (int r = 1; r <= 30 && !found; r++)
            for (int dy = -r; dy <= r && !found; dy++)
            for (int dx = -r; dx <= r && !found; dx++)
            {
                if (Math.Abs(dx) != r && Math.Abs(dy) != r) continue;
                int nx = sx + dx, ny = sy + dy;
                if ((uint)nx >= (uint)w || (uint)ny >= (uint)h) continue;
                if (GetA(nx, ny) >= AlphaThreshold) { sx = nx; sy = ny; found = true; }
            }
        }

        var keep  = new bool[w * h];
        var queue = new Queue<int>();

        if (GetA(sx, sy) >= AlphaThreshold)
        {
            int start = sy * w + sx;
            keep[start] = true;
            queue.Enqueue(start);
        }

        while (queue.Count > 0)
        {
            int idx = queue.Dequeue();
            int x   = idx % w;
            int y   = idx / w;

            for (int dy = -1; dy <= 1; dy++)
            for (int dx = -1; dx <= 1; dx++)
            {
                if (dx == 0 && dy == 0) continue;
                int nx = x + dx, ny = y + dy;
                if ((uint)nx >= (uint)w || (uint)ny >= (uint)h) continue;
                int ni = ny * w + nx;
                if (!keep[ni] && GetA(nx, ny) >= AlphaThreshold)
                {
                    keep[ni] = true;
                    queue.Enqueue(ni);
                }
            }
        }

        var result  = new Bitmap(w, h, PixelFormat.Format32bppArgb);
        var dstData = result.LockBits(rect, ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
        int dstStride = dstData.Stride;
        var outPx   = new byte[dstStride * h];

        for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
        {
            if (!keep[y * w + x]) continue;
            int si = (y * stride)    + (x * 4);
            int di = (y * dstStride) + (x * 4);
            outPx[di]     = pixels[si];
            outPx[di + 1] = pixels[si + 1];
            outPx[di + 2] = pixels[si + 2];
            outPx[di + 3] = pixels[si + 3];
        }

        Marshal.Copy(outPx, 0, dstData.Scan0, outPx.Length);
        result.UnlockBits(dstData);
        return result;
    }
}
